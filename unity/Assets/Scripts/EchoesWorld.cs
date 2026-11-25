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
        private int lastWorld = -1;
        private bool lastOpening, rendered;
        private Vector3 position, sight, velocity, sightVelocity;
        private float sectionAge;
        public bool Ready => rendered && Texture != null;
        public RenderTexture Texture => graded != null ? graded : scene;

        private static readonly int Air = Shader.PropertyToID("_EchoesAir");
        private static readonly int Key = Shader.PropertyToID("_EchoesKey");
        private static readonly int SkyHigh = Shader.PropertyToID("_EchoesSkyHigh");
        private static readonly int Ambient = Shader.PropertyToID("_EchoesAmbience");
        private static readonly int Weather = Shader.PropertyToID("_EchoesWeather");
        private static readonly int Pool = Shader.PropertyToID("_EchoesPool");
        private static readonly int PoolColor = Shader.PropertyToID("_EchoesPoolColor");
        private static readonly int Footfall = Shader.PropertyToID("_EchoesFootfall");

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
            float dt=Mathf.Min(.04f,UnityEngine.Time.unscaledDeltaTime);
            if (opening)
            {
                WorldCameraPath.Evaluate(frame.openingTime,out position,out sight,out float fov);
                camera.fieldOfView=fov;
                velocity=sightVelocity=Vector3.zero;
            }
            else
            {
                PlayCamera(journey,frame,speed,out Vector3 wanted,out Vector3 focus);
                bool cut=lastWorld!=journey.World || (lastOpening && !journey.EnteredFromOpening);
                if (cut)
                {
                    position=wanted; sight=focus;
                    velocity=sightVelocity=Vector3.zero;
                    sectionAge=0;
                }
                else
                {
                    position=Vector3.SmoothDamp(position,wanted,ref velocity,.48f,90,dt);
                    sight=Vector3.SmoothDamp(sight,focus,ref sightVelocity,.45f,90,dt);
                }
                camera.fieldOfView=Mathf.Lerp(camera.fieldOfView,49-3*frame.voice,dt*4);
            }
            sectionAge+=dt;
            lastWorld=journey.World;
            lastOpening=opening;
            camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(sight-position));
            float time=opening?frame.openingTime:frame.motionTime;
            Vector3 key=LightScene(journey,frame,period,time,opening);

            set.Respond(journey,frame.openingTime);
            Vector3 shotActor = opening ? OpeningStoryboard.Sample(time).Actor :
                new Vector3(.5f,SpiritFlight.ScreenY(frame.altitude),1);
            Vector2 screen = new Vector2(shotActor.x,shotActor.y);
            float body=162*shotActor.z;
            float depth = ActorDepth(screen,body,journey,opening);
            Vector3 actorAt=camera.ViewportToWorldPoint(new Vector3(screen.x,1-screen.y,depth));
            float floor=position.y<-.8f?WorldSet.CaveFloor:WorldSet.Ground(actorAt.x,actorAt.z);
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
            if (!split) spirit.DrawInWorld(camera,screen,body,depth,key,performer);

            camera.Render();
            if (lens!=null)
            {
                float fade=opening?1:Mathf.SmoothStep(.25f,1,Mathf.InverseLerp(0,.56f,sectionAge));
                lens.SetVector("_Lens",new Vector4(fade,opening?1:.25f,0,0));
                RenderTexture before=RenderTexture.active;
                Graphics.Blit(scene,graded,lens);
                RenderTexture.active=before;
            }
            rendered=scene.IsCreated();
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
