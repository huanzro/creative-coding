using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    internal sealed class EchoesWorld : IDisposable
    {
        private readonly Camera camera;
        private readonly WorldSet set;
        private readonly Light sun;
        private readonly CommandBuffer performer;
        private readonly RenderTexture scene, graded;
        private readonly Material lens;
        private int lastWorld = -1, lastRuinRoom = -1;
        private RuinPortal lastRuinPortal;
        private RuinTrackConnection ruinPassage;
        private RuinRouteFrame ruinPassageStart;
        private bool ruinFarSide;
        private float ruinPassageLight;
        private Vector3 ruinViewUp=Vector3.up;
        private Stage lastPhase;
        private int lastCloseShot;
        private bool lastOpening, lastRuinDetached, lastRuinExit, rendered;
        private Vector3 position, sight, velocity, sightVelocity;
        private float sectionAge, lastTravelTime, lastDistance;
        public bool Ready => rendered && Texture != null;
        public RenderTexture Texture => graded != null ? graded : scene;
        public Vector2 ActorScreen { get; private set; } = new Vector2(.5f,.75f);
        internal Vector3 ProjectToViewport(Vector3 point) => camera.WorldToViewportPoint(point);
        public float ActorBody { get; private set; } = 162;

        private static readonly int Air = Shader.PropertyToID("_EchoesAir");
        private static readonly int Key = Shader.PropertyToID("_EchoesKey");
        private static readonly int SkyHigh = Shader.PropertyToID("_EchoesSkyHigh");
        private static readonly int Ambient = Shader.PropertyToID("_EchoesAmbience");
        private static readonly int Weather = Shader.PropertyToID("_EchoesWeather");
        private static readonly int Pool = Shader.PropertyToID("_EchoesPool");
        private static readonly int PoolColor = Shader.PropertyToID("_EchoesPoolColor");
        private static readonly int Footfall = Shader.PropertyToID("_EchoesFootfall");
        private static readonly int TerrainSink = Shader.PropertyToID("_EchoesTerrainSink");
        private static readonly int ProjectionProfile = Shader.PropertyToID("_ProjectionProfile");
        private static readonly int ProjectionGrade = Shader.PropertyToID("_ProjectionGrade");
        private static readonly int BackgroundBrightness = Shader.PropertyToID("_BackgroundBrightness");

        private static readonly Vector3 SkyKey = new Vector3(-.57f,.55f,-.53f).normalized;
        private static readonly Vector3 ForestKey = new Vector3(.58f,.70f,.39f).normalized;
        private static readonly Color[] DayColors = {
            new Color(.17f,.245f,.220f), new Color(.233f,.194f,.134f),
            new Color(.13f,.231f,.255f), new Color(.055f,.095f,.162f), new Color(.262f,.254f,.201f)
        };
        private static readonly Vector2 StarActorViewport = new Vector2(.5f,.56f);
        private const float StarActorBody=142;
        private static readonly float StarActorScale=StarActorBody*SpiritRig.HeightScale/720 *
            (StarInkSpace.PerformerDepth*Mathf.Tan(StarInkSpace.FieldOfView*.5f*Mathf.Deg2Rad))/
            (5.6f*Mathf.Tan(15.25f*Mathf.Deg2Rad));

        public EchoesWorld()
        {
            Shader world=Resources.Load<Shader>("EchoesWorld");
            Shader sky=Resources.Load<Shader>("EchoesSky");
            Shader scatter=Resources.Load<Shader>("EchoesMist");
            if (world==null || !world.isSupported || sky==null || !sky.isSupported || scatter==null) return;

            var node = new GameObject("Echoes world camera") { hideFlags=HideFlags.HideAndDontSave };
            camera=node.AddComponent<Camera>();
            camera.enabled=false;
            camera.cullingMask=1 << WorldSet.Layer;
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.027f,.047f,.059f);
            camera.allowHDR=SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf);
            camera.allowMSAA=false;
            camera.useOcclusionCulling=false;
            camera.renderingPath=RenderingPath.Forward;
            camera.nearClipPlane=.095f;
            camera.farClipPlane=485;
            camera.aspect=16f/9;
            int width = Application.platform==RuntimePlatform.WebGLPlayer ? 1152 : 1536;
            int height=width*9/16;
            scene = new RenderTexture(width,height,24,camera.allowHDR?RenderTextureFormat.ARGBHalf:RenderTextureFormat.ARGB32)
                {name="Echoes spatial camera color and depth",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            camera.targetTexture=scene;

            Shader optics=Resources.Load<Shader>("EchoesLens");
            if (optics!=null && optics.isSupported)
            {
                lens=new Material(optics);
                graded=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32)
                    {name="Echoes cinema grade",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            }
            performer=new CommandBuffer { name="Depth-tested Echoes performer" };
            camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha,performer);
            var light=new GameObject("Echoes key light") { hideFlags=HideFlags.HideAndDontSave, layer=WorldSet.Layer };
            sun=light.AddComponent<Light>();
            sun.type=LightType.Directional;
            sun.shadows=LightShadows.Soft;
            sun.shadowStrength=.77f;
            sun.shadowBias=.057f;
            sun.shadowNormalBias=.29f;
            sun.cullingMask=1 << WorldSet.Layer;
            set=new WorldSet(world,sky,scatter);
        }

        public void Render(Journey journey, SpiritFrame frame, SpiritRig spirit, bool split, int period, float speed,
            Vector2 amber, Vector2 scarlet, float brightness, float falloff, bool projectionGradient,
            float backgroundBrightness = 1, float spiderBrightness = 1, float mawBrightness = 1, StarInkSpace starInk = null,
            Rect starWindow = default)
        {
            if (set==null) return;
            bool opening=frame.openingTime>=0;
            bool stars=!opening && journey.InStars;
            bool writing = stars && starInk != null;
            if (writing) starInk.Update(journey, split ? (starWindow.width > 0 ? starWindow : StarProjection.DefaultWindow) : default);
            bool forest=!opening && journey.InForest;
            bool ruins=!opening && journey.InRuins;
            RuinRetryPhase ruinEnding=journey.RuinRetryState;
            bool framework=ruins && WorldSet.IsRuinFramework(ruinEnding);
            bool detached=framework && WorldSet.IsRuinDetached(journey);
            bool ruinExit=framework && WorldSet.IsRuinExit(ruinEnding);
            bool previousFarSide=ruinFarSide;
            ruinFarSide=false;
            ruinPassageLight=1;
            Vector3 forestActor=forest ? ForestActor(journey,frame) : Vector3.zero;
            RuinRouteFrame ruinRoute=framework ? RuinEndingActor(journey,detached) : ruins ? RuinActor(journey,frame) : default;
            Vector3 ruinActor=framework ? ruinRoute.Position : ruins ? ruinRoute.Position+Vector3.up*WorldSet.RuinY : Vector3.zero;
            Vector3 spatialActor=forest ? forestActor : ruinActor;
            float dt=Mathf.Min(.04f,UnityEngine.Time.unscaledDeltaTime);
            if (opening)
            {
                WorldCameraPath.Evaluate(frame.openingTime,out position,out sight,out float fov);
                camera.fieldOfView=fov;
                velocity=sightVelocity=Vector3.zero;
            }
            else
            {
                Vector3 wanted,focus;
                if (forest) ForestShot(journey,frame,forestActor,out wanted,out focus);
                else if (framework) RuinFrameworkShot(journey,detached,out wanted,out focus);
                else if (ruins) RuinShot(journey,ruinRoute,out wanted,out focus);
                else PlayCamera(journey,frame,speed,out wanted,out focus);
                int closeShot = !forest ? 0 : journey.Phase==Stage.ForestDoor && journey.Progress>=.37f ? 1 :
                    journey.ForestTrap==ForestTrapKind.Cable && journey.Progress>=.66f ? 2 : 0;
                bool frameworkMatch=(detached!=lastRuinDetached && framework) ||
                    (lastRuinExit && journey.InStars);
                bool cut=frameworkMatch || lastWorld!=journey.World || (lastOpening && !journey.EnteredFromOpening) ||
                    (!lastOpening && frame.time < lastTravelTime) ||
                    (forest && !lastOpening && Mathf.Abs(journey.Distance-lastDistance)>7.5f) ||
                    (lastPhase==Stage.ForestDoor && journey.Phase!=Stage.ForestDoor) ||
                    (ruins && lastRuinRoom!=journey.RuinRoomId && lastPhase!=Stage.RuinTransfer);
                bool matchCut=closeShot!=0 && closeShot!=lastCloseShot;
                lastCloseShot=closeShot;
                if (cut || stars || ruinExit || ruinFarSide && !previousFarSide)
                {
                    position=wanted; sight=focus;
                    velocity=sightVelocity=Vector3.zero;
                    if (cut && !frameworkMatch) sectionAge=0;
                }
                else
                {
                    float track=ruins ? .11f : !forest ? .48f :
                        journey.Phase==Stage.ForestDoor || journey.ForestTrap==ForestTrapKind.Cable ? .095f :
                        frame.forestMaze ? .18f : .24f;
                    position=matchCut ? wanted : Vector3.SmoothDamp(position,wanted,ref velocity,track,90,dt);
                    sight=matchCut ? focus : Vector3.SmoothDamp(sight,focus,ref sightVelocity,track,90,dt);
                    if(matchCut) velocity=sightVelocity=Vector3.zero;
                }
                float fov=ruins ? 55-3*frame.voice : 49-3*frame.voice;
                if (ruins && ruinEnding == RuinRetryPhase.Impact)
                    fov = 55 - 7 * OpeningStoryboard.Window(journey.Age,.19f,.40f,.45f,1.16f);
                if (framework)
                    fov = ruinExit ? Mathf.Lerp(60,StarInkSpace.FieldOfView,OpeningStoryboard.Ease(7.18f,9.80f,journey.Age)) :
                        ruinEnding == RuinRetryPhase.Gate ? Mathf.Lerp(51,67,OpeningStoryboard.Ease(1.41f,3.06f,journey.Age)) :
                        ruinEnding == RuinRetryPhase.Survey ? 67 : 60;
                if (forest && journey.Section==ForestSection.Vestibule && journey.Phase!=Stage.ForestDoor) fov=54f;
                if (forest && journey.Phase==Stage.ForestTrapped) fov=45f;
                camera.fieldOfView=stars ? StarInkSpace.FieldOfView : ruinExit && journey.Age>=9.46f
                    ? fov : Mathf.Lerp(camera.fieldOfView,fov,dt*4);
            }
            sectionAge+=dt;
            lastWorld=journey.World;
            lastOpening=opening;
            lastRuinDetached=detached;
            lastRuinExit=ruinExit;
            lastPhase=journey.Phase;
            lastRuinRoom=ruins ? journey.RuinRoomId : -1;
            lastTravelTime=frame.time;
            lastDistance=journey.Distance;
            Quaternion rotation = Quaternion.LookRotation(sight-position, ruins && journey.Phase==Stage.RuinTransfer ? ruinViewUp : Vector3.up);
            Vector3 eye = writing ? position + rotation * (Vector3)starInk.Frame.eyeOffset : position;
            camera.transform.SetPositionAndRotation(eye, rotation);
            float time=opening?frame.openingTime:frame.motionTime;
            Vector3 key=LightScene(journey,frame,period,time,opening);

            set.Respond(journey,frame.openingTime);
            Shader.SetGlobalVector(TerrainSink, !forest ? Vector4.zero : journey.Phase==Stage.ForestDoor ?
                WorldSet.MachineDoorSink(journey.ForestDoor,journey.Progress) : WorldSet.ForestSink(journey));
            set.DrawMaze(journey,forest,time);
            set.DrawMachine(journey,forest,time,forestActor);
            set.ForestScenery(journey,forest,time,forestActor,frame.glide,split,spiderBrightness,mawBrightness);
            set.DrawRuinTunnel(journey,ruins && !detached,time,camera.transform.position);
            set.DrawGuideSigns(journey, forest, ruins);
            set.DrawRuinFramework(journey,time,detached,ruinActor,frame.cast);
            set.DrawFilmScenery(opening, ruinExit, journey.Age);
            set.DrawStarFlight(stars, journey.Distance);
            Vector3 shotActor = opening ? OpeningStoryboard.Sample(time).Actor :
                new Vector3(.5f,SpiritFlight.ScreenY(frame.altitude),1);
            Vector2 screen = new Vector2(shotActor.x,shotActor.y);
            float body=162*shotActor.z;
            if (stars)
            {
                screen = StarActorViewport;
                body = StarActorBody;
            }
            bool spatial=forest || ruins;
            Vector3 viewport=spatial ? camera.WorldToViewportPoint(spatialActor) : Vector3.zero;
            bool actorVisible=!spatial || viewport.z>camera.nearClipPlane+.085f;
            float depth=stars ? StarInkSpace.PerformerDepth : spatial ? Mathf.Max(.2f,viewport.z) : ActorDepth(screen,body,journey,opening);
            if(spatial && actorVisible)
            {
                screen=new Vector2(viewport.x,1-viewport.y);
                float modelScale=ruins ? .43f : .41f;
                if (ruinExit)
                    modelScale=Mathf.Lerp(modelScale,StarActorScale,OpeningStoryboard.Ease(4.04f,4.97f,journey.Age));
                if(forest && journey.Section==ForestSection.Vestibule && journey.Phase!=Stage.ForestDoor) modelScale=.36f;
                if (forest && journey.Phase == Stage.ForestDoor && journey.ForestDoor == 0)
                    modelScale *= Mathf.Lerp(1,.09f,OpeningStoryboard.Ease(.77f,.88f,journey.Progress));
                body=modelScale*720*(5.6f*Mathf.Tan(15.25f*Mathf.Deg2Rad))/
                    (SpiritRig.HeightScale*depth*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad));
            }
            performer.Clear();
            Vector3 actorAt=spatial ? spatialActor : camera.ViewportToWorldPoint(new Vector3(screen.x,1-screen.y,depth));
            if (writing)
            {
                StarInkFrame artwork = starInk.Frame;
                Vector3 tip = camera.transform.TransformPoint(artwork.cursor - artwork.Eye);
                actorAt = spirit.PlaceStarWriter(tip, rotation, StarActorScale, key, performer, !split, lens != null);
                var writer = camera.WorldToViewportPoint(actorAt);
                screen = new Vector2(writer.x, 1 - writer.y);
            }
            ActorScreen=screen;
            ActorBody=body;
            float floor=forest ? frame.forestMaze ? journey.ForestMazeY :
                WorldSet.ForestPoint(journey,frame.forestLane,journey.Distance,journey.ForestFlightFloor).y-.54f :
                framework ? ruinRoute.Position.y-.54f :
                ruins ? WorldSet.RuinY+RuinTrack.Frame(journey.RuinRoomId,journey.RuinRoomLength,journey.Distance).Position.y :
                position.y<-.8f ? WorldSet.CaveFloor : WorldSet.Ground(actorAt.x,actorAt.z);
            float weight=Mathf.Clamp01(1-(actorAt.y-floor-.72f)*.76f);
            Shader.SetGlobalVector(Footfall,new Vector4(actorAt.x,floor,actorAt.z,weight*(split?.25f:.58f)));
            if (opening)
            {
                float a = Mathf.Clamp01(OpeningStoryboard.Firefly(time,false).z) *
                    (split?.45f-.31f*OpeningStoryboard.NearHand(time,false):1);
                float r = Mathf.Clamp01(OpeningStoryboard.Firefly(time,true).z) *
                    (split?.45f-.31f*OpeningStoryboard.NearHand(time,true):1);
                set.Flies(camera.ViewportToWorldPoint(new Vector3(amber.x,1-amber.y,depth-.14f)),a,
                    camera.ViewportToWorldPoint(new Vector3(scarlet.x,1-scarlet.y,depth-.17f)),r,time);
            }
            else set.Flies(Vector3.zero,0,Vector3.zero,0,time);

            float? facing=forest && (frame.forestMaze || journey.LeftMazeApproach ||
                journey.Branch==ForestBranch.Right && journey.Distance<140) ? (float?)WorldSet.ForestHeading(journey) :
                ruins ? (float?)(Mathf.Atan2(ruinRoute.Forward.x,ruinRoute.Forward.z)*Mathf.Rad2Deg) : null;
            if (ruinExit)
                facing=Mathf.LerpAngle(facing.GetValueOrDefault(),camera.transform.eulerAngles.y-180,
                    OpeningStoryboard.Ease(8.80f,9.96f,journey.Age));
            if (!writing && !split && actorVisible)
                spirit.DrawInWorld(camera,screen,body,depth,key,performer,spatial,facing,lens!=null);
            if (writing) starInk.DrawWorld(camera, performer);

            camera.Render();
            if (lens!=null)
            {
                float fade=opening?1:Mathf.SmoothStep(.25f,1,Mathf.InverseLerp(0,.56f,sectionAge));
                if (forest && journey.Phase==Stage.ForestTransition)
                    fade=Mathf.Lerp(fade,.07f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.76f,1,journey.Progress)));
                if (forest && (journey.Phase==Stage.ForestFalling || journey.Phase==Stage.ForestDoor))
                    fade *= 1-.93f*OpeningStoryboard.Ease(journey.Phase==Stage.ForestDoor?.89f:.79f,1,journey.Progress);
                if (ruins && journey.RuinRetryState==RuinRetryPhase.Impact)
                    fade *= 1-.22f*OpeningStoryboard.Window(journey.Age,.41f,.45f,.49f,.65f);
                fade *= ruinPassageLight;
                lens.SetVector("_Lens",new Vector4(fade,opening?1:.25f,0,0));
                Vector2 center = new Vector2(Mathf.Clamp01(screen.x), 1-Mathf.Clamp01(screen.y));
                float core = ProjectionLighting.CoreRadius(body);
                lens.SetVector(ProjectionProfile, new Vector4(center.x,center.y,core,ProjectionLighting.Spread(falloff)));
                lens.SetVector(ProjectionGrade, new Vector4(brightness,projectionGradient ? 1 : 0,ProjectionLighting.CenterGain(falloff),camera.aspect));
                lens.SetFloat(BackgroundBrightness,backgroundBrightness);
                RenderTexture before=RenderTexture.active;
                Graphics.Blit(scene,graded,lens);
                RenderTexture.active=before;
            }
            rendered=scene.IsCreated();
        }

        private static RuinRouteFrame RuinEndingActor(Journey journey, bool detached)
        {
            WorldSet.RuinFrameworkPlacement(journey,detached,out Vector3 origin,out Quaternion rotation);
            Vector3 local = WorldSet.RuinFrameworkActor(journey) + Vector3.up*.54f;
            if (WorldSet.IsRuinExit(journey.RuinRetryState))
            {
                // End at the same perspective, depth and screen composition used by the next live chapter.
                var starOrientation=Quaternion.LookRotation(WorldSet.StarEntrySight);
                float depth=StarInkSpace.PerformerDepth;
                Vector3 end=WorldSet.RuinStarLanding+starOrientation*
                    new Vector3(0,(.5f-StarActorViewport.y)*2*depth*
                        Mathf.Tan(StarInkSpace.FieldOfView*.5f*Mathf.Deg2Rad),depth);
                local=Vector3.Lerp(local,end,OpeningStoryboard.Ease(8.62f,9.98f,journey.Age));
            }
            return new RuinRouteFrame(origin+rotation*local,rotation*Vector3.forward);
        }

        private static void RuinFrameworkShot(Journey journey,bool detached,out Vector3 at,out Vector3 focus)
        {
            RuinRetryPhase phase = journey.RuinRetryState;
            float age = journey.Age;
            WorldSet.RuinFrameworkPlacement(journey,detached,out Vector3 origin,out Quaternion rotation);
            Vector3 overview = new Vector3(0,2.29f,-4.10f);
            Vector3 atLocal = new Vector3(.19f,2.24f,-6.3f);
            Vector3 towards = new Vector3(1.03f,2.14f,5.1f)-atLocal;
            if (phase == RuinRetryPhase.Gate)
            {
                atLocal = Vector3.Lerp(new Vector3(-.13f,1.67f,-10.48f),overview,
                    OpeningStoryboard.Ease(.39f,2.96f,age));
                var gateSight = new Vector3(0,1.68f,-4.48f);
                var frameworkSight = overview + new Vector3(0,.18f,13f);
                towards = Vector3.Lerp(gateSight,frameworkSight,OpeningStoryboard.Ease(.86f,2.89f,age))-atLocal;
            }
            else if (phase == RuinRetryPhase.Survey || phase == RuinRetryPhase.Arrival)
            {
                Quaternion inspect=Quaternion.Euler(-journey.RuinLookPitch,journey.RuinLookYaw,0);
                Vector3 look=inspect*new Vector3(0,.18f,13f);
                if (phase == RuinRetryPhase.Survey)
                {
                    atLocal=overview;
                    towards=look;
                }
                else
                {
                    float recenter=OpeningStoryboard.Ease(0,1.16f,age);
                    atLocal=Vector3.Lerp(overview,atLocal,recenter);
                    float overhead=OpeningStoryboard.Window(age,.82f,1.40f,1.84f,3.30f);
                    var landingSight=new Vector3(1.11f,2.44f+overhead*4.35f,5.0f)-atLocal;
                    towards=Vector3.Slerp(look,landingSight,recenter);
                }
            }
            else if (WorldSet.IsRuinExit(phase))
            {
                bool accepted=phase==RuinRetryPhase.Accepted;
                float dolly=OpeningStoryboard.Ease(.14f,accepted?1.56f:1.32f,age);
                Vector3 near=accepted ? new Vector3(3.75f,2.49f,-3.82f) : new Vector3(-2.96f,2.28f,-4.66f);
                Vector3 target=accepted ? new Vector3(.24f,1.21f,1.09f) : new Vector3(.75f,1.82f,4.10f);
                atLocal=Vector3.Lerp(atLocal,near,dolly);
                Vector3 midShot=Vector3.Lerp(new Vector3(1.03f,2.14f,5.1f),target,dolly)-atLocal;
                float travel=OpeningStoryboard.Ease(accepted?3.05f:2.82f,5.38f,age);
                Vector3 advance=accepted ? new Vector3(-.78f,2.65f,12.3f) : new Vector3(-3.04f,3.09f,12.6f);
                float grove=Mathf.Clamp01((age-4.12f)/5.86f);
                Vector3 track = Vector3.Lerp(advance, WorldSet.RuinStarLanding, grove)
                    + new Vector3(.65f, .92f, 0) * Mathf.Sin(grove * Mathf.PI);
                atLocal=Vector3.Lerp(atLocal,track,travel);
                Vector3 throughGrove = Vector3.Lerp(new Vector3(1.1f, .15f, 16), WorldSet.StarEntrySight,
                    OpeningStoryboard.Ease(6.56f, 9.98f, age));
                towards=Vector3.Slerp(midShot,throughGrove,travel);
            }
            at=origin+rotation*atLocal;
            focus=at+rotation*towards;
        }

        private RuinRouteFrame RuinActor(Journey journey,SpiritFrame frame)
        {
            RuinPortal portal=journey.RuinEntrance;
            var track=RuinTrack.Frame(journey.RuinRoomId,journey.RuinRoomLength,journey.Distance);
            if (journey.Phase!=Stage.RuinTransfer || portal==null)
            {
                lastRuinPortal=null;
                var actor=new RuinRouteFrame(track.Point(2.45f*journey.RuinLane,.54f+3.30f*frame.altitude),track.Forward);
                var retry=journey.RuinRetryState;
                if(retry!=RuinRetryPhase.Impact) return actor;
                var barrier=RuinTrack.Frame(journey.RuinRoomId,journey.RuinRoomLength,journey.RuinRetryWallDepth-.18f);
                float lockOn=retry==RuinRetryPhase.Impact ? OpeningStoryboard.Ease(0,.37f,journey.Age) : 1;
                float metres=(journey.Distance-journey.RuinRetryWallDepth+.18f)*2.5f;
                Vector3 clear=barrier.Point(journey.RuinLane*1.73f,.54f+3.30f*frame.altitude)+barrier.Forward*metres;
                return new RuinRouteFrame(Vector3.Lerp(actor.Position,clear,lockOn),Vector3.Slerp(actor.Forward,barrier.Forward,lockOn));
            }
            if(lastRuinPortal!=portal)
            {
                ruinPassage=WorldSet.RuinConnection(journey,portal);
                lastRuinPortal=portal;
                ruinPassageStart=WorldSet.RuinPassageStart(journey);
            }
            float travelled=PassageRouteDistance(journey);
            float length=ruinPassage.TravelMetres(ruinPassageStart);
            var passage=ruinPassage.Travel(ruinPassageStart,travelled/length);
            var facing=RuinTrack.Frame(journey.RuinRoomId,journey.RuinRoomLength,journey.RuinPassageStartDepth).Forward;
            return new RuinRouteFrame(passage.Position,
                Vector3.Slerp(facing,passage.Forward,RuinTrackConnection.SmoothJoin(travelled/3.6f)),
                passage.Right);
        }

        private float PassageRouteDistance(Journey journey)
        {
            float distance=journey.Progress*ruinPassage.GameplayMetres(ruinPassageStart);
            return ruinPassage.RouteDistance(ruinPassageStart,distance);
        }

        private void RuinShot(Journey journey,RuinRouteFrame actor,out Vector3 at,out Vector3 focus)
        {
            if(journey.Phase==Stage.RuinTransfer)
            {
                RuinPassageShot(journey,actor,out at,out focus);
                return;
            }

            int room=journey.RuinRoomId;
            RuinGalleryShot(journey,room,journey.Distance,journey.RuinLane,journey.Flight.Height,journey.RuinApproach,
                out at,out focus);
            Vector3 offset=Vector3.up*WorldSet.RuinY;
            float actorHeight=actor.Position.y-RuinTrack.Frame(room,journey.RuinRoomLength,journey.Distance).Position.y;
            RuinRetryPhase retry = journey.RuinRetryState;
            if (retry != RuinRetryPhase.Inactive)
            {
                float age = journey.Age;
                float recoil = retry == RuinRetryPhase.Impact
                    ? OpeningStoryboard.Window(age,.37f,.45f,.49f,.73f)
                    : 0;
                float closeIn = retry == RuinRetryPhase.Impact ? OpeningStoryboard.Ease(.16f,.73f,age) : 1;
                var barrier=RuinTrack.Frame(journey.RuinRoomId,journey.RuinRoomLength,journey.RuinRetryWallDepth-.18f);
                float separation=Mathf.Max(3.6f,(journey.RuinRetryWallDepth-journey.Distance)*2.1f+2.8f);
                Vector3 stage=offset+barrier.Point(journey.RuinLane*.63f-.15f+recoil*.16f,
                    1.54f+actorHeight*.27f+recoil*.12f)-barrier.Forward*(separation+recoil*.31f);
                at = Vector3.Lerp(at, stage, closeIn);
                focus = Vector3.Lerp(focus,offset+barrier.Point(journey.RuinLane*.49f,1.84f)+barrier.Forward*.42f,closeIn);
                return;
            }
        }

        private void RuinPassageShot(Journey journey,RuinRouteFrame actor,out Vector3 at,out Vector3 focus)
        {
            Vector3 offset=Vector3.up*WorldSet.RuinY;
            float length=ruinPassage.TravelMetres(ruinPassageStart);
            float mouth=ruinPassage.MouthMetres(ruinPassageStart);
            float arrival=ruinPassage.GalleryMetres(ruinPassageStart);
            float metres=PassageRouteDistance(journey);
            float played=journey.Progress*ruinPassage.GameplayMetres(ruinPassageStart);
            float seam=ruinPassage.PassageSeam(ruinPassageStart);
            ruinFarSide=played>=seam;
            float shade=RuinTrackConnection.SmoothJoin(Mathf.InverseLerp(.35f,1.5f,Mathf.Abs(played-seam)));
            ruinPassageLight=Mathf.Lerp(.015f,1,shade);
            float arrivalDepth=ruinPassage.ArrivalRoadDepth(ruinPassageStart,metres);
            set.SetRuinTransitionDistances(metres,mouth,arrival,arrivalDepth);
            float enter=RuinTrackConnection.SmoothJoin(Mathf.InverseLerp(mouth-6f,mouth+1.8f,metres));
            float exit=RuinTrackConnection.SmoothJoin(Mathf.InverseLerp(arrival-8.2f,arrival+3.2f,metres));
            float inside=enter*(1-exit);
            var trailing=ruinPassage.Travel(ruinPassageStart,metres/length,-Mathf.Lerp(4.9f,2.55f,inside));
            var ahead=ruinPassage.Travel(ruinPassageStart,metres/length,Mathf.Lerp(5.9f,3.35f,inside));
            at=offset+trailing.Section(-.08f,Mathf.Lerp(1.44f,.68f,inside));
            focus=offset+Vector3.Lerp(actor.Point(0,.16f),ahead.Point(0,.27f),Mathf.Lerp(.58f,.37f,inside));

            float leadIn=RuinTrackConnection.SmoothJoin(metres/Mathf.Clamp(mouth*.83f,3.2f,7.8f));
            if(leadIn<1)
            {
                // Continue advancing the source follow target while the camera joins the swept curve.
                float inherited=.42f*(1-Mathf.Exp(-journey.Age/.42f));
                float depth=journey.RuinPassageStartDepth+journey.RuinPassageStartForwardSpeed*inherited;
                float lane=Mathf.Clamp(journey.RuinPassageStartLane+journey.RuinPassageStartLateralSpeed*inherited,-1.35f,1.35f);
                float altitude=Mathf.Clamp(journey.RuinPassageStartAltitude+journey.RuinPassageStartVerticalSpeed*inherited,0,Journey.RuinHeightLimit);
                RuinGalleryShot(journey,journey.RuinRoomId,depth,lane,altitude,journey.RuinEntrance,
                    out Vector3 sourceAt,out Vector3 sourceFocus);
                at=Vector3.Lerp(sourceAt,at,leadIn);
                focus=Vector3.Lerp(sourceFocus,focus,leadIn);
            }
            float merge=RuinTrackConnection.SmoothJoin(Mathf.InverseLerp(arrival-1.8f,Mathf.Min(length,arrival+8.2f),metres));
            if(merge>0)
            {
                int destination=journey.RuinEntrance.TargetRoom;
                float baseY=RuinTrack.Frame(destination,journey.RuinRoomLengthFor(destination),arrivalDepth).Position.y;
                float altitude=(actor.Position.y-baseY-.54f)/3.30f;
                RuinGalleryShot(journey,destination,arrivalDepth,0,altitude,null,out Vector3 destinationAt,out Vector3 destinationFocus);
                // The follow camera stays low and centred in the mouth until it clears the last rib.
                float settle=RuinTrackConnection.SmoothJoin(Mathf.InverseLerp(arrival+1.1f,length,metres));
                at=Vector3.Lerp(at,destinationAt,settle);
                focus=Vector3.Lerp(focus,destinationFocus,merge);
            }

            // Keep the camera's up direction stable even when a shaft is nearly vertical.
            var viewDir=(focus-at).normalized;
            float align=RuinTrackConnection.SmoothJoin(Mathf.InverseLerp(.63f,.93f,Mathf.Abs(viewDir.y)))*leadIn*(1-merge);
            ruinViewUp=Vector3.Slerp(Vector3.up,Vector3.Cross(trailing.Forward,trailing.Right),align);
        }

        private static void RuinGalleryShot(Journey journey,int room,float depth,float lane,float altitude,RuinPortal marker,
            out Vector3 at,out Vector3 focus)
        {
            Vector3 offset=Vector3.up*WorldSet.RuinY;
            float length=journey.RuinRoomLengthFor(room);
            var behind=RuinTrack.Frame(room,length,depth-RuinTrack.DepthForMetres(room,length,4.9f));
            var next=RuinTrack.Frame(room,length,depth+RuinTrack.DepthForMetres(room,length,5.9f));
            float actorHeight=.54f+3.30f*altitude;
            float cameraHeight=Mathf.Min(actorHeight+1.5f,6.58f);
            float focusHeight=Mathf.Lerp(1.65f,actorHeight*.70f+.60f,.76f);
            float mouthFraming=0;
            RuinPortal framedMouth=null;
            float centreWeight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,1,Mathf.Abs(lane)));
            foreach(var portal in journey.RuinPortalsFor(room))
            {
                bool upper=portal.Direction==RuinDirection.Upper;
                if (!upper && portal.Direction!=RuinDirection.Lower || portal.Hidden && marker!=portal) continue;
                float ahead=RuinTrack.Offset(portal.Depth,depth,length);
                float window=upper
                    ? OpeningStoryboard.Window(ahead,-1.3f,1.65f,5.0f,9.8f)*(.32f+.68f*centreWeight)
                    : OpeningStoryboard.Window(ahead,-2.4f,.2f,3.9f,7.8f)*centreWeight;
                if(window<=mouthFraming) continue;
                framedMouth=portal;
                mouthFraming=window;
            }
            bool upperMouth=framedMouth?.Direction==RuinDirection.Upper;
            if(upperMouth)
            {
                float rising=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.68f,altitude));
                bool nearChest=room==journey.RuinRoomId &&
                    (journey.RuinLootOpen || journey.RuinChestOpening || journey.RuinSearchTarget?.Kind==RuinObjectKind.Chest);
                // At ground level step back to show both the vault and the objects on the road.
                float pullback=(1-rising)*mouthFraming*(nearChest ? 2.25f : 1.65f);
                behind=RuinTrack.Frame(room,length,depth-RuinTrack.DepthForMetres(room,length,4.9f+pullback));
                mouthFraming*=Mathf.Lerp(nearChest ? .43f : .54f,1,rising);
                cameraHeight=Mathf.Lerp(cameraHeight,Mathf.Min(cameraHeight+.29f,4.9f),mouthFraming);
            }
            else
                cameraHeight=Mathf.Lerp(cameraHeight,Mathf.Min(cameraHeight,5.1f),mouthFraming);
            focusHeight=Mathf.Lerp(focusHeight,upperMouth ? 4.2f : 1.25f,mouthFraming*.52f);
            at=offset+behind.Point(lane*2.23f-.16f,cameraHeight);
            focus=offset+next.Point(lane*1.43f,focusHeight);
            if(upperMouth)
            {
                // Aim toward the actual vault on curved galleries without losing the actor.
                var gate=RuinTrack.Frame(room,length,framedMouth.Depth);
                Vector2 profile=WorldSet.RuinGallerySection(journey.RuinRoomStyleFor(room),Mathf.PI*.5f);
                focus=Vector3.Lerp(focus,offset+gate.Point(profile.x,profile.y-1.06f),mouthFraming*.55f);
            }
        }

        private static Vector3 ForestActor(Journey journey,SpiritFrame frame)
        {
            bool transition=journey.Phase==Stage.ForestTransition;
            Vector3 point=frame.forestMaze ? WorldSet.MazeActor(journey,frame.altitude) :
                WorldSet.ForestPoint(journey,journey.ForestLane,journey.Distance,frame.altitude);
            Quaternion heading=WorldSet.ForestOrientation(journey);
            float phase = journey.Progress;
            if (journey.Phase == Stage.ForestFalling)
            {
                Vector3 floor=frame.forestMaze ? WorldSet.MazeActor(journey,0) :
                    WorldSet.ForestPoint(journey,journey.ForestLane,journey.Distance,0);
                float fall = OpeningStoryboard.Ease(0,.44f,phase);
                point = Vector3.Lerp(point,floor + Vector3.up*.13f,fall);
                bool web=journey.ForestTrap==ForestTrapKind.Web;
                float catchBeat=web ? OpeningStoryboard.Ease(.43f,.84f,phase) : OpeningStoryboard.Ease(.65f,.94f,phase);
                point += heading*new Vector3(Mathf.Sin(phase*14)*.13f,(web ? 2.9f : -2.2f)*catchBeat,catchBeat*.57f);
                return point;
            }
            if (journey.Phase == Stage.ForestDoor)
                return WorldSet.MachineDoorActor(point,journey.ForestDoor,phase);
            if (journey.Phase==Stage.ForestTrapped)
            {
                bool web=journey.ForestTrap==ForestTrapKind.Web;
                float authoredAge=journey.ForestRescueAuthoredAge;
                var acting=ForestRescueStoryboard.Sample(authoredAge,web);
                Vector3 trap=WorldSet.ForestTrapSite(journey);
                Vector3 caught=Vector3.Lerp(point,trap,acting.tight*.37f);
                Vector3 floor=point-Vector3.up*(frame.altitude-journey.ForestFlightFloor)*5.2f;
                if (web)
                {
                    caught+=heading*new Vector3(.13f*Mathf.Sin(authoredAge*7.3f)*acting.effort,
                        .13f*acting.tight+.19f*acting.pull,.19f*acting.tight-.16f*acting.balance);
                    return Vector3.Lerp(caught,floor,OpeningStoryboard.Ease(7.42f,9.32f,authoredAge));
                }
                caught+=heading*new Vector3(.045f*Mathf.Sin(authoredAge*11.8f)*acting.effort,
                    -.16f*acting.tight+.07f*acting.pry,.06f*acting.tight);
                return Vector3.Lerp(caught,floor,OpeningStoryboard.Ease(7.58f,9.32f,authoredAge))+
                    heading*ForestRescueStoryboard.MawEscapeOffset(authoredAge);
            }
            if (!transition) return point;

            if (journey.ForestTrap==ForestTrapKind.Cable)
                return WorldSet.MachineCableActor(point,phase,journey.ForestCableAnchor);
            Vector3 mouth=WorldSet.ForestDescentMouth(journey);
            float move=OpeningStoryboard.Ease(.20f,journey.AtMazeRoots?.60f:.75f,phase);
            point=Vector3.Lerp(point,mouth,move);
            if(journey.ForestTrap==ForestTrapKind.Web)
            {
                float tug=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.23f,phase));
                float drop=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.34f,.67f,phase));
                point.y+=.73f*tug*(1-drop);
                point.y-=3.55f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.63f,1,phase));
            }
            else
            {
                float scoop=Mathf.Sin(Mathf.PI*Mathf.Clamp01(Mathf.InverseLerp(.10f,.61f,phase)));
                point.y+=.49f*scoop;
                point.y-=3.42f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.64f,1,phase));
            }
            return point;
        }

        private static void ForestShot(Journey j,SpiritFrame frame,Vector3 actor,out Vector3 at,out Vector3 focus)
        {
            float ground=frame.forestMaze ? j.ForestMazeY : WorldSet.ForestPoint(j,frame.forestLane,j.Distance,0).y-.54f;
            float corridorX=j.ForestCenterAt(j.Distance);
            float bank=actor.x-corridorX;
            Quaternion heading=WorldSet.ForestOrientation(j);
            at=new Vector3(corridorX+bank*.91f-.20f,ground+1.86f+frame.altitude*3.20f,actor.z-4.73f);
            focus=new Vector3(corridorX+bank*.82f,ground+1.43f+frame.altitude*3.52f,actor.z+7.85f);
            if (frame.forestMaze)
            {
                float follow = 2.84f;
                if (j.ForestMazeTarget >= 0)
                {
                    float side = Mathf.Abs(Mathf.Sin((j.ForestMazeHeading-j.ForestMazePassageHeading)*Mathf.Deg2Rad));
                    follow = Mathf.Min(follow,(ForestMaze.PassageHalf-.48f)/Mathf.Max(.01f,side));
                }
                at=actor+heading*new Vector3(.09f,.93f,-follow);
                focus=actor+heading*new Vector3(0,.29f,3.15f);
                if (j.ForestMazeWallOffset > 0)
                {
                    ForestMazeNode room = j.ForestMaze.Nodes[j.ForestMazeNode];
                    Vector3 outside = new Vector3(actor.x-room.X,0,actor.z-room.Z);
                    Vector3 back = heading * Vector3.back;
                    float radius = ForestMaze.WallContactRadius + .13f;
                    float towardWall = Vector3.Dot(outside,back);
                    float clearance = Mathf.Sqrt(Mathf.Max(0,towardWall*towardWall+radius*radius-outside.sqrMagnitude))-towardWall;
                    float nearWall = 1-Mathf.InverseLerp(.14f,2.84f,clearance);
                    Vector3 roomCamera = new Vector3(room.X,actor.y + 1.40f,room.Z);
                    Vector3 side = Vector3.Cross(Vector3.up,outside.normalized)*1.02f;
                    at = Vector3.Lerp(at,roomCamera+side,nearWall);
                    focus = Vector3.Lerp(focus,actor+Vector3.up*.20f,nearWall);
                }
                if (j.AtMazeRoots)
                {
                    Vector3 center=WorldSet.MazeRootCenter(j);
                    float lift=OpeningStoryboard.Ease(.08f,.66f,frame.altitude);
                    at=center+heading*new Vector3(j.ForestLane*.60f,1.90f+1.30f*lift,-3.26f);
                    focus=center+heading*new Vector3(j.ForestLane*.55f,1.24f+1.72f*lift,.88f);
                }
            }
            else if (j.Branch != ForestBranch.Shared && j.Distance < 136)
            {
                float turn = OpeningStoryboard.Window(j.Distance,112,117,125,136);
                float close = j.ForestMazeApproachBlend;
                Vector3 tail = actor + heading * Vector3.Lerp(new Vector3(-.10f,1.26f,-4.22f),new Vector3(.09f,.93f,-2.84f),close);
                Vector3 lead = actor + heading * Vector3.Lerp(new Vector3(0,.41f,4.6f),new Vector3(0,.29f,3.15f),close);
                at = Vector3.Lerp(at,tail,Mathf.Max(turn*.80f,close));
                focus = Vector3.Lerp(focus,lead,Mathf.Max(turn*.80f,close));
            }
            else if(j.Phase!=Stage.ForestDoor && j.Phase!=Stage.ForestFalling && j.ForestTrap!=ForestTrapKind.Cable &&
                WorldSet.MachineTravelCamera(j,actor,out Vector3 machineAt,out Vector3 machineFocus))
            {
                float blend = OpeningStoryboard.Ease(168,174.4f,j.Distance);
                at=Vector3.Lerp(at,machineAt,blend);
                focus=Vector3.Lerp(focus,machineFocus,blend);
            }
            if (j.Phase==Stage.ForestTrapped)
            {
                bool web=j.ForestTrap==ForestTrapKind.Web;
                float t=j.ForestRescueAuthoredAge;
                float arrive=OpeningStoryboard.Ease(.18f,1.76f,t);
                if (web)
                {
                    float hero=OpeningStoryboard.Window(t,2.12f,3.08f,6.70f,8.71f);
                    Vector3 rescueAt=actor+heading*Vector3.Lerp(new Vector3(.55f,1.04f,-2.91f),new Vector3(-.61f,.40f,-1.92f),hero);
                    at=Vector3.Lerp(at,rescueAt,arrive);
                    focus=Vector3.Lerp(focus,actor+heading*new Vector3(.19f,.08f,.15f),arrive);
                    return;
                }
                var beat=ForestRescueStoryboard.Sample(t,false);
                float detail=OpeningStoryboard.Window(t,1.85f,3.34f,6.42f,7.62f);
                float settle=OpeningStoryboard.Ease(8.15f,9.34f,t);
                Vector3 planted=actor-heading*ForestRescueStoryboard.MawEscapeOffset(t);
                Vector3 close=Vector3.Lerp(new Vector3(1.64f,1.20f,-2.83f),new Vector3(1.37f,.94f,-2.23f),detail);
                Vector3 seam=new Vector3(.09f,.05f,.11f)+beat.fly*(detail*.32f);
                at=Vector3.Lerp(at,planted+heading*close,arrive*(1-settle));
                focus=Vector3.Lerp(focus,Vector3.Lerp(planted+heading*seam,actor,beat.free*.64f),arrive*(1-settle));
                return;
            }
            if(j.Phase==Stage.ForestDoor)
            {
                float enter=OpeningStoryboard.Ease(.14f,.35f,j.Progress);
                WorldSet.MachineDoorCamera(actor,j.ForestDoor,j.Progress,out Vector3 room,out Vector3 center);
                at=Vector3.Lerp(at,room,enter);
                focus=Vector3.Lerp(focus,center,enter);
                return;
            }
            if(j.ForestTrap==ForestTrapKind.Cable)
            {
                WorldSet.MachineHeartCamera(actor,j.Progress,out at,out focus);
                return;
            }
            if(j.Phase==Stage.ForestFalling)
            {
                float move=OpeningStoryboard.Ease(.06f,.51f,j.Progress);
                at=Vector3.Lerp(at,actor+heading*new Vector3(.61f,1.31f,-3.56f),move);
                focus=Vector3.Lerp(focus,actor+heading*Vector3.forward*1.11f,move);
                return;
            }
            if (j.Phase==Stage.ForestTransition)
            {
                bool web=j.ForestTrap==ForestTrapKind.Web;
                float tuck=Mathf.SmoothStep(0,1,Mathf.InverseLerp(web?.25f:.13f,web?.69f:.57f,j.Progress));
                Vector3 closer=actor+heading*new Vector3(web?1.08f:-.63f,.76f,-3.24f);
                at=Vector3.Lerp(at,closer,tuck);
                focus=Vector3.Lerp(focus,actor+new Vector3(0,.12f,1.44f),tuck);

                // Follow above the performer when entering the narrow vertical shaft.
                float descent=Mathf.SmoothStep(0,1,Mathf.InverseLerp(web?.61f:.50f,web?.87f:.78f,j.Progress));
                Vector3 shaft=WorldSet.ForestDescentMouth(j);
                at=Vector3.Lerp(at,new Vector3(shaft.x+.12f,actor.y+1.58f,shaft.z-.34f),descent);
                focus=Vector3.Lerp(focus,actor+new Vector3(0,-.38f,.43f),descent);
            }
        }

        private float ActorDepth(Vector2 screen,float body,Journey journey,bool opening)
        {
            float depth = opening ? 3.62f : 3.73f;
            if (position.y < -.85f || position.y > 5.7f) return depth;
            Vector3 ray = camera.ViewportToWorldPoint(new Vector3(screen.x,1-screen.y,1))-position;
            Vector3 guess = position+ray*depth;
            float floor = journey.Lesson>=0 && !opening ? .34f : WorldSet.Ground(guess.x,guess.z);
            float radius=body*SpiritRig.HeightScale/720*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad)/
                (5.6f*Mathf.Tan(15.25f*Mathf.Deg2Rad));
            float down=radius*1.09f-ray.y;
            if (down>.04f) depth=Mathf.Clamp((position.y-floor-.16f)/down,1.54f,depth);
            return depth;
        }

        private Vector3 LightScene(Journey journey, SpiritFrame frame, int period, float time, bool opening)
        {
            float cave=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.8f,-8.2f,position.y));
            float heaven=Mathf.SmoothStep(0,1,Mathf.InverseLerp(11,26,position.y));
            Color air=DayColors[opening?0:Mathf.Clamp(period,0,4)];
            Color dome=new Color(.095f,.223f,.213f);
            Color fill=new Color(.42f,.52f,.44f);
            Vector3 key=Vector3.Lerp(ForestKey,SkyKey,heaven);
            float lit=.72f;
            if (cave>0)
            {
                air=Color.Lerp(air,new Color(.047f,.096f,.119f),cave);
                dome=Color.Lerp(dome,new Color(.033f,.061f,.072f),cave);
                fill=Color.Lerp(fill,new Color(.29f,.52f,.56f),cave);
                key=Vector3.Lerp(key,new Vector3(-.12f,.97f,-.18f),cave).normalized;
                lit=Mathf.Lerp(lit,.228f,cave);
            }
            if (heaven>0)
            {
                air=Color.Lerp(air,new Color(.095f,.116f,.220f),heaven);
                dome=Color.Lerp(dome,new Color(.016f,.022f,.079f),heaven);
                fill=Color.Lerp(fill,new Color(.44f,.47f,.71f),heaven);
                lit=Mathf.Lerp(lit,.39f,heaven);
            }
            if (!opening && frame.closed) lit*= .77f;
            float mist=Mathf.Lerp(.032f,.049f,cave)-heaven*.025f;
            if (!opening && journey.InRuins)
            {
                Color starAir=air, starDome=dome, starFill=fill;
                Vector3 starKey=key;
                float starLit=lit, starMist=mist;
                Color layerLight=WorldSet.RuinGalleryAccentFor(journey.RuinRoomStyle,journey.RuinRoomId);
                float galleryMist=journey.RuinRoomStyle==5 ? .023f : .032f;
                if(journey.Phase==Stage.RuinTransfer && journey.RuinEntrance!=null)
                {
                    int destination=journey.RuinEntrance.TargetRoom;
                    int destinationStyle=journey.RuinRoomStyleFor(destination);
                    Color arriving=WorldSet.RuinGalleryAccentFor(destinationStyle,destination);
                    float mix=OpeningStoryboard.Ease(.46f,.95f,journey.Progress);
                    layerLight=Color.Lerp(layerLight,arriving,mix);
                    galleryMist=Mathf.Lerp(galleryMist,destinationStyle==5 ? .023f : .032f,mix);
                }
                air=Color.Lerp(new Color(.018f,.024f,.049f),layerLight,.106f);
                dome=Color.Lerp(new Color(.012f,.011f,.030f),layerLight,.125f);
                fill=Color.Lerp(new Color(.38f,.44f,.58f),layerLight,.23f);
                key=new Vector3(-.37f,.58f,-.70f).normalized;
                lit=.395f;
                mist=galleryMist;
                RuinRetryPhase retry = journey.RuinRetryState;
                if (WorldSet.IsRuinFramework(retry))
                {
                    float reveal=retry==RuinRetryPhase.Gate ? OpeningStoryboard.Ease(.66f,2.64f,journey.Age) : 1;
                    float sunward=WorldSet.IsRuinExit(retry) ? OpeningStoryboard.Ease(7.42f,9.98f,journey.Age) : 0;
                    float grove=WorldSet.IsRuinExit(retry)
                        ? OpeningStoryboard.Window(journey.Age, 3.77f, 5.51f, 7.27f, 9.70f) : 0;
                    Color chamberAir=retry==RuinRetryPhase.Declined
                        ? new Color(.053f,.052f,.097f) : new Color(.017f,.038f,.082f);
                    air=Color.Lerp(Color.Lerp(air,chamberAir,reveal),starAir,sunward);
                    dome=Color.Lerp(Color.Lerp(dome,new Color(.006f,.017f,.045f),reveal),starDome,sunward);
                    fill=Color.Lerp(Color.Lerp(fill,new Color(.52f,.64f,.76f),reveal),starFill,sunward);
                    key=Vector3.Slerp(Vector3.Slerp(key,new Vector3(-.48f,.78f,-.41f).normalized,reveal),starKey,sunward);
                    lit=Mathf.Lerp(Mathf.Lerp(lit,.50f,reveal),starLit,sunward);
                    mist=Mathf.Lerp(Mathf.Lerp(mist,.0071f,reveal),starMist,sunward);
                    air=Color.Lerp(air,new Color(.105f,.201f,.153f),grove*.81f);
                    dome=Color.Lerp(dome,new Color(.045f,.108f,.079f),grove*.65f);
                    fill=Color.Lerp(fill,new Color(.71f,.75f,.46f),grove*.63f);
                    lit=Mathf.Lerp(lit,.69f,grove*.46f);
                    mist+=grove*.017f;
                    heaven=WorldSet.IsRuinDetached(journey) ? 1 : heaven;
                }
            }
            float red=opening?OpeningStoryboard.Contact(time,true):journey.FireflyResponse==FireflyKind.Scarlet?journey.FireflyPulse:0;
            float amber=opening?OpeningStoryboard.Contact(time,false):journey.Light*.38f;
            Vector3 pool=position+camera.transform.forward*4.76f;
            if(frame.forest && journey.Branch==ForestBranch.Right && journey.Section>=ForestSection.Brain)
            {
                float warm=OpeningStoryboard.Ease(.78f,1,journey.ForestShaftProgress);
                float beat=journey.ForestTrap==ForestTrapKind.Cable ?
                    OpeningStoryboard.Window(journey.Progress,.15f,.21f,.24f,.32f)+
                    OpeningStoryboard.Window(journey.Progress,.40f,.46f,.49f,.54f) : 0;
                air=Color.Lerp(new Color(.031f,.075f,.10f),new Color(.095f,.047f,.040f),warm);
                dome=Color.Lerp(new Color(.04f,.11f,.15f),new Color(.12f,.062f,.045f),warm);
                fill=Color.Lerp(new Color(.28f,.48f,.54f),new Color(.61f,.38f,.34f),warm);
                lit=.45f+beat*.21f;
                key=new Vector3(.50f,.74f,-.46f).normalized;
                mist=.017f+.013f*(1-warm);
                amber=Mathf.Max(amber,warm*(.24f+beat*.46f));
            }
            Shader.SetGlobalVector(Air,new Vector4(air.r,air.g,air.b,mist));
            Shader.SetGlobalVector(Key,new Vector4(key.x,key.y,key.z,lit));
            Shader.SetGlobalColor(SkyHigh,dome);
            Shader.SetGlobalColor(Ambient,fill);
            Shader.SetGlobalVector(Weather,new Vector4(time,journey.GustWarning,cave,1-heaven));
            Shader.SetGlobalVector(Pool,new Vector4(pool.x,pool.y,pool.z,Mathf.Max(red,amber)));
            Shader.SetGlobalColor(PoolColor,Color.Lerp(new Color(1,.71f,.29f),new Color(1,.19f,.11f),red));
            sun.transform.rotation=Quaternion.LookRotation(-key);
            sun.intensity=Mathf.Lerp(1,.58f,cave);
            return key;
        }

        private static void PlayCamera(Journey j, SpiritFrame frame, float speed, out Vector3 at, out Vector3 focus)
        {
            float travel=j.Distance*speed;
            float drift=Mathf.Sin(frame.motionTime*.49f)*.22f;
            if (j.Lesson>=0)
            {
                at=new Vector3(-.75f+drift,1.82f+j.Progress*.26f,-39.2f+Mathf.Min(2.1f,travel));
                focus=new Vector3(.15f,1.08f,-24);
            }
            else if (j.Chapter==0)
            {
                bool high=j.Phase==Stage.ForestWind;
                float z=19.7f+Mathf.Min(31,travel)*.73f;
                if (j.EnteredFromOpening) z+=2.28f;
                float h=high?Mathf.Sin(j.Progress*Mathf.PI)*4.46f:0;
                at=new Vector3((high?.96f:-.78f)+drift+.56f*Mathf.Sin(z*.19f),1.68f+h+.58f*frame.altitude,z);
                focus=new Vector3(Mathf.Sin((z+13)*.12f)*1.24f,.97f+h*.51f,z+15.2f);
            }
            else if (j.Chapter==1)
            {
                float z=13.6f;
                if (j.Phase==Stage.RuinVoice) z=15.8f;
                else if (j.Phase==Stage.RuinRelease) z=19.8f;
                else if (j.Phase==Stage.RuinMisread || j.Phase==Stage.RuinRepair || j.Phase==Stage.RuinSide) z=24.2f;
                else if (j.Phase==Stage.RuinCrossing || j.Phase==Stage.Complete) z=27+34*j.Progress;
                float side=j.Phase==Stage.RuinSide?2.9f:-.62f;
                at=new Vector3(side+drift,WorldSet.CaveFloor+2.42f+.58f*frame.altitude,z);
                focus=new Vector3(side*.68f,WorldSet.CaveFloor+3.04f,z+15);
            }
            else
            {
                at=WorldSet.StarCameraAt(j.Distance);
                focus=at+WorldSet.StarEntrySight;
            }
        }

        public void Dispose()
        {
            if (camera != null)
            {
                camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha,performer);
                camera.targetTexture=null;
                UnityEngine.Object.Destroy(camera.gameObject);
            }
            performer?.Dispose();
            set?.Dispose();
            if (sun!=null) UnityEngine.Object.Destroy(sun.gameObject);
            if (lens!=null) UnityEngine.Object.Destroy(lens);
            if (scene!=null) { scene.Release(); UnityEngine.Object.Destroy(scene); }
            if (graded!=null) { graded.Release(); UnityEngine.Object.Destroy(graded); }
        }
    }
}
