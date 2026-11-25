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
        private Stage lastPhase;
        private int lastCloseShot;
        private bool lastOpening, rendered;
        private Vector3 position, sight, velocity, sightVelocity;
        private float sectionAge, lastTravelTime, lastDistance;
        public bool Ready => rendered && Texture != null;
        public RenderTexture Texture => graded != null ? graded : scene;
        public Vector2 ActorScreen { get; private set; } = new Vector2(.5f,.75f);
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

        private static readonly Vector3 SkyKey = new Vector3(-.57f,.55f,-.53f).normalized;
        private static readonly Vector3 ForestKey = new Vector3(.58f,.70f,.39f).normalized;
        private static readonly Color[] DayColors = {
            new Color(.17f,.245f,.220f), new Color(.233f,.194f,.134f),
            new Color(.13f,.231f,.255f), new Color(.055f,.095f,.162f), new Color(.262f,.254f,.201f)
        };

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
            Vector2 amber, Vector2 scarlet)
        {
            if (set==null) return;
            bool opening=frame.openingTime>=0;
            bool forest=!opening && journey.InForest;
            bool ruins=!opening && journey.InRuins;
            Vector3 forestActor=forest ? ForestActor(journey,frame) : Vector3.zero;
            Vector3 ruinActor=ruins ? RuinActor(journey,frame) : Vector3.zero;
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
                else if (ruins) RuinShot(journey,ruinActor,out wanted,out focus);
                else PlayCamera(journey,frame,speed,out wanted,out focus);
                int closeShot = !forest ? 0 : journey.Phase==Stage.ForestDoor && journey.Progress>=.47f ? 1 :
                    journey.ForestTrap==ForestTrapKind.Cable && journey.Progress>=.59f ? 2 : 0;
                bool cut=lastWorld!=journey.World || (lastOpening && !journey.EnteredFromOpening) ||
                    (!lastOpening && frame.time < lastTravelTime) ||
                    (forest && !lastOpening && Mathf.Abs(journey.Distance-lastDistance)>7.5f) ||
                    (lastPhase==Stage.ForestDoor && journey.Phase!=Stage.ForestDoor) ||
                    (ruins && (lastRuinRoom!=journey.RuinRoomId || lastPhase==Stage.RuinTransfer && journey.Phase==Stage.Complete));
                bool matchCut=closeShot!=0 && closeShot!=lastCloseShot;
                lastCloseShot=closeShot;
                if (cut)
                {
                    position=wanted; sight=focus;
                    velocity=sightVelocity=Vector3.zero;
                    sectionAge=0;
                }
                else
                {
                    float track=ruins ? .16f : !forest ? .48f :
                        journey.Phase==Stage.ForestDoor || journey.ForestTrap==ForestTrapKind.Cable ? .082f : .17f;
                    position=matchCut ? wanted : Vector3.SmoothDamp(position,wanted,ref velocity,track,90,dt);
                    sight=matchCut ? focus : Vector3.SmoothDamp(sight,focus,ref sightVelocity,track,90,dt);
                    if(matchCut) velocity=sightVelocity=Vector3.zero;
                }
                float fov=ruins ? 55-3*frame.voice : 49-3*frame.voice;
                camera.fieldOfView=Mathf.Lerp(camera.fieldOfView,fov,dt*4);
            }
            sectionAge+=dt;
            lastWorld=journey.World;
            lastOpening=opening;
            lastPhase=journey.Phase;
            lastRuinRoom=ruins ? journey.RuinRoomId : -1;
            lastTravelTime=frame.time;
            lastDistance=journey.Distance;
            camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(sight-position));
            float time=opening?frame.openingTime:frame.motionTime;
            Vector3 key=LightScene(journey,frame,period,time,opening);

            set.Respond(journey,frame.openingTime);
            Shader.SetGlobalVector(TerrainSink, forest ? WorldSet.ForestSink(journey) : Vector4.zero);
            set.DrawMaze(journey,forest,time);
            set.DrawMachine(journey,forest,time,forestActor);
            set.ForestScenery(journey,forest,time,forestActor,frame.glide,split);
            set.DrawRuinTunnel(journey,ruins,time);
            Vector3 shotActor = opening ? OpeningStoryboard.Sample(time).Actor :
                new Vector3(.5f,SpiritFlight.ScreenY(frame.altitude),1);
            Vector2 screen = new Vector2(shotActor.x,shotActor.y);
            float body=162*shotActor.z;
            bool spatial=forest || ruins;
            Vector3 viewport=spatial ? camera.WorldToViewportPoint(spatialActor) : Vector3.zero;
            float depth=spatial ? Mathf.Max(.2f,viewport.z) : ActorDepth(screen,body,journey,opening);
            if(spatial)
            {
                screen=new Vector2(viewport.x,1-viewport.y);
                float modelScale=ruins ? .43f : .41f;
                if (forest && journey.Phase == Stage.ForestDoor && journey.ForestDoor == 0)
                    modelScale *= Mathf.Lerp(1,.08f,OpeningStoryboard.Ease(.62f,.86f,journey.Progress));
                body=modelScale*720*(5.6f*Mathf.Tan(15.25f*Mathf.Deg2Rad))/
                    (SpiritRig.HeightScale*depth*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad));
            }
            ActorScreen=screen;
            ActorBody=body;
            Vector3 actorAt=spatial ? spatialActor : camera.ViewportToWorldPoint(new Vector3(screen.x,1-screen.y,depth));
            float floor=forest ? WorldSet.ForestPoint(journey,frame.forestLane,journey.Distance,0).y-.54f :
                ruins ? WorldSet.RuinY : position.y<-.8f ? WorldSet.CaveFloor : WorldSet.Ground(actorAt.x,actorAt.z);
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

            performer.Clear();
            if (!split) spirit.DrawInWorld(camera,screen,body,depth,key,performer,spatial);

            camera.Render();
            if (lens!=null)
            {
                float fade=opening?1:Mathf.SmoothStep(.25f,1,Mathf.InverseLerp(0,.56f,sectionAge));
                if (forest && journey.Phase==Stage.ForestTransition)
                    fade=Mathf.Lerp(fade,.07f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.76f,1,journey.Progress)));
                if (forest && (journey.Phase==Stage.ForestFalling || journey.Phase==Stage.ForestDoor))
                    fade *= 1-.93f*OpeningStoryboard.Ease(.79f,1,journey.Progress);
                if (ruins && journey.Phase==Stage.RuinTransfer)
                    fade *= 1-.94f*OpeningStoryboard.Ease(.60f,1,journey.Progress);
                lens.SetVector("_Lens",new Vector4(fade,opening?1:.25f,0,0));
                RenderTexture before=RenderTexture.active;
                Graphics.Blit(scene,graded,lens);
                RenderTexture.active=before;
            }
            rendered=scene.IsCreated();
        }

        private static Vector3 RuinActor(Journey journey,SpiritFrame frame)
        {
            Vector3 actor=WorldSet.RuinPoint(journey.RuinLane,journey.Distance,frame.altitude);
            RuinPortal portal=journey.RuinEntrance;
            if (journey.Phase==Stage.Complete && portal!=null && portal.Direction==RuinDirection.Exit)
                return WorldSet.RuinPoint(portal.Lane*.48f,portal.Depth+6.55f,.21f);
            if (journey.Phase!=Stage.RuinTransfer || portal==null) return actor;
            float t=journey.Progress;
            Vector3 mouth=WorldSet.RuinPoint(portal.Lane,portal.Depth,portal.Altitude);
            float move=OpeningStoryboard.Ease(0,.56f,t);
            float cross=OpeningStoryboard.Ease(.35f,1,t);
            Vector3 passage=new Vector3(portal.Direction==RuinDirection.Left ? -.68f : portal.Direction==RuinDirection.Right ? .68f : 0,
                portal.Direction==RuinDirection.Upper ? 1.01f : portal.Direction==RuinDirection.Lower ? -.74f : 0,3.13f);
            return Vector3.Lerp(actor,mouth,move)+passage*cross;
        }

        private static void RuinShot(Journey journey,Vector3 actor,out Vector3 at,out Vector3 focus)
        {
            bool transfer=journey.Phase==Stage.RuinTransfer;
            float cameraY=Mathf.Min(actor.y+1.50f,WorldSet.RuinY+6.56f);
            float elevation=Mathf.Lerp(WorldSet.RuinY+1.65f,actor.y*.68f+(WorldSet.RuinY+1.9f)*.32f,.70f);
            at=new Vector3(actor.x*.91f-.16f,cameraY,actor.z-4.9f);
            focus=new Vector3(actor.x*.70f,elevation,actor.z+9.6f);
            if (!transfer) return;
            float approach=OpeningStoryboard.Ease(.12f,.80f,journey.Progress);
            float chase=Mathf.Lerp(-4.9f,-2.25f,approach);
            at=Vector3.Lerp(at,actor+new Vector3(.10f,.45f,chase),approach);
            focus=Vector3.Lerp(focus,actor+new Vector3(0,.18f,2.2f),approach);
        }

        private static Vector3 ForestActor(Journey journey,SpiritFrame frame)
        {
            bool transition=journey.Phase==Stage.ForestTransition;
            Vector3 point=WorldSet.ForestPoint(journey,journey.ForestLane,journey.Distance,frame.altitude);
            point.x += journey.ForestLoopOffset;
            float phase = journey.Progress;
            if (journey.Phase == Stage.ForestFalling)
            {
                Vector3 floor = WorldSet.ForestPoint(journey,journey.ForestLane,journey.Distance,0);
                float fall = OpeningStoryboard.Ease(0,.44f,phase);
                point = Vector3.Lerp(point,floor + Vector3.up*.13f,fall);
                float catchBeat = OpeningStoryboard.Ease(.43f,.84f,phase);
                point += new Vector3(Mathf.Sin(phase*14)*.13f,journey.ForestTrap==ForestTrapKind.Web?2.9f*catchBeat:-2.2f*catchBeat,catchBeat*.57f);
                return point;
            }
            if (journey.Phase == Stage.ForestDoor)
                return WorldSet.MachineDoorActor(point,journey.ForestDoor,phase);
            if (journey.Phase==Stage.ForestTrapped)
            {
                float draw=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.31f,journey.Age));
                Vector3 trap=WorldSet.ForestPoint(journey,journey.ForestTrapLane,journey.ForestTrapDepth,
                    journey.ForestTrapAltitude);
                return Vector3.Lerp(point,trap,draw*.68f)+Vector3.up*(Mathf.Sin(journey.Age*8)*.034f*draw);
            }
            if (!transition) return point;

            if (journey.ForestTrap==ForestTrapKind.Cable)
                return WorldSet.MachineCableActor(point,phase,journey.ForestCableAnchor);
            Vector3 mouth=WorldSet.ForestPoint(journey,journey.ForestTrapLane,journey.ForestTrapDepth+2.35f,0);
            float t=journey.Progress;
            float move=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.20f,.75f,t));
            point=Vector3.Lerp(point,mouth,move);
            if(journey.ForestTrap==ForestTrapKind.Web)
            {
                float tug=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.23f,t));
                float drop=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.34f,.67f,t));
                point.y+=.73f*tug*(1-drop);
                point.y-=3.55f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.63f,1,t));
            }
            else
            {
                float scoop=Mathf.Sin(Mathf.PI*Mathf.Clamp01(Mathf.InverseLerp(.10f,.61f,t)));
                point.y+=.49f*scoop;
                point.y-=3.42f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.47f,1,t));
            }
            return point;
        }

        private static void ForestShot(Journey j,SpiritFrame frame,Vector3 actor,out Vector3 at,out Vector3 focus)
        {
            float ground=WorldSet.ForestPoint(j,frame.forestLane,j.Distance,0).y-.54f;
            float corridorX=j.ForestCenterAt(j.Distance);
            float bank=actor.x-corridorX;
            at=new Vector3(corridorX+bank*.91f-.20f,ground+1.86f+frame.altitude*3.20f,actor.z-4.73f);
            focus=new Vector3(corridorX+bank*.82f,ground+1.43f+frame.altitude*3.52f,actor.z+7.85f);
            if(j.Phase==Stage.ForestDoor)
            {
                float enter=OpeningStoryboard.Ease(.05f,.44f,j.Progress);
                at=Vector3.Lerp(at,actor+new Vector3(.18f,1.02f,-1.56f),enter);
                focus=Vector3.Lerp(focus,actor+new Vector3(0,-.20f,.91f),enter);
                return;
            }
            if(j.ForestTrap==ForestTrapKind.Cable)
            {
                float reveal=OpeningStoryboard.Window(j.Progress,.15f,.27f,.39f,.55f);
                float close=OpeningStoryboard.Ease(.42f,.59f,j.Progress);
                at=Vector3.Lerp(at,actor+new Vector3(21.5f,13.6f,-13.2f),reveal);
                focus=Vector3.Lerp(focus,new Vector3(corridorX,2.0f,220),reveal);
                at=Vector3.Lerp(at,actor+new Vector3(.26f,1.23f,-2.22f),close);
                focus=Vector3.Lerp(focus,actor+Vector3.forward*1.11f,close);
                return;
            }
            if(j.Phase==Stage.ForestFalling)
            {
                float move=OpeningStoryboard.Ease(.06f,.51f,j.Progress);
                at=Vector3.Lerp(at,actor+new Vector3(.61f,1.31f,-4.13f),move);
                focus=Vector3.Lerp(focus,actor+Vector3.forward*1.11f,move);
                return;
            }
            if (j.Phase==Stage.ForestTransition)
            {
                bool web=j.ForestTrap==ForestTrapKind.Web;
                float tuck=Mathf.SmoothStep(0,1,Mathf.InverseLerp(web?.25f:.13f,web?.69f:.57f,j.Progress));
                Vector3 closer=actor+new Vector3(web?1.08f:-.63f,.76f,-3.24f);
                at=Vector3.Lerp(at,closer,tuck);
                focus=Vector3.Lerp(focus,actor+new Vector3(0,.12f,1.44f),tuck);

                // Follow above the performer when entering the narrow vertical shaft.
                float descent=Mathf.SmoothStep(0,1,Mathf.InverseLerp(web?.61f:.50f,web?.87f:.78f,j.Progress));
                Vector3 shaft=WorldSet.ForestPoint(j,j.ForestTrapLane,j.ForestTrapDepth+2.35f,0);
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
                Color layerLight=WorldSet.RuinLight(journey.RuinRoomStyle);
                air=Color.Lerp(new Color(.018f,.024f,.049f),layerLight,.106f);
                dome=Color.Lerp(new Color(.012f,.011f,.030f),layerLight,.125f);
                fill=Color.Lerp(new Color(.38f,.44f,.58f),layerLight,.23f);
                key=new Vector3(-.37f,.58f,-.70f).normalized;
                lit=.395f;
                mist=journey.RuinRoomStyle==5 ? .023f : .032f;
            }
            float red=opening?OpeningStoryboard.Contact(time,true):journey.FireflyResponse==FireflyKind.Scarlet?journey.FireflyPulse:0;
            float amber=opening?OpeningStoryboard.Contact(time,false):journey.Light*.38f;
            Vector3 pool=position+camera.transform.forward*4.76f;
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
                float z=8.6f+6.2f*j.StarCount+Mathf.Min(6,travel*.24f);
                at=new Vector3(-1.7f+j.StarCount*1.0f+drift,WorldSet.StarY+1.3f+frame.altitude*1.66f,z);
                focus=new Vector3(2.15f,WorldSet.StarY+2.61f,z+18.7f);
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
