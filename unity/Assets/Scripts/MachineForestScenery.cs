using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private const float MachineCenter = 11, HeartFloorDrop = 18;
        private const float DoorDepth = Journey.MachineDoorDepth, ShaftDepth = Journey.MachineShaftDepth,
            HeartDepth = Journey.MachineHeartDepth;
        private const float UpperDeck = Journey.ForestMachineUpperFloor * 5.2f;
        private const float StoreyHeight = 7.42f, RoofThickness = 1.04f, GalleryHalfWidth = 4.87f;
        private const float OuterGateDepth = DoorDepth + 1.94f, InnerGateDepth = DoorDepth + 6.12f,
            GalleryRevealDepth = DoorDepth + 3.87f;
        private static readonly Color Iron = new Color(.29f,.40f,.45f), Bronze = new Color(.62f,.40f,.24f),
            Coal = new Color(.105f,.16f,.20f), Circuit = new Color(.32f,.80f,.91f),
            HeartAmber = new Color(1f,.61f,.25f), PoolBlue = new Color(.49f,.56f,1f);
        private static readonly Vector3[] CablePorts =
        {
            new Vector3(-2.7f,.65f,.85f), new Vector3(2.7f,1.20f,1.22f), new Vector3(.22f,2.84f,2.22f)
        };

        private sealed class Recycler
        {
            public Transform Root, Upper, Lower, Grip;
            public MeshRenderer Eye;
        }

        private sealed class Prediction
        {
            public Transform Root, LeftArm, RightArm;
            public MeshRenderer Body, LeftLight, RightLight;
        }

        private Transform machineRoute, machineBrain, machineHeart, machineGearRoom, machineRobotRoom, machinePoolRoom;
        private Transform pressDeck, hatchLeft, hatchRight, poolRipple, heartSeat, heartSeed;
        private Transform leftHeartValve, rightHeartValve, heartTendon;
        private Transform leftCrusher, rightCrusher, crusherLight, poolSurface, irisLeft, irisRight, heartCore;
        private Transform[] heartCable, heartCableInner;
        private MeshRenderer poolFilm, poolRippleLight, heartGlow, heartSeatLight;
        private readonly MeshRenderer[] pressSparks = new MeshRenderer[12];
        private readonly MeshRenderer[] doorwayNodes = new MeshRenderer[6];
        private Material machineGlass, machineSign, machinePool;
        private Mesh machineBar, machineSphere, machineRobotShell;
        private readonly Recycler[] recyclers = new Recycler[9];
        private readonly Prediction[] predictions = new Prediction[15];
        private readonly MeshRenderer[] brainPulses = new MeshRenderer[21];
        private readonly MeshRenderer[] returnPulses = new MeshRenderer[42];
        private readonly MeshRenderer[] heartCurrent = new MeshRenderer[14];
        private readonly Transform[] threshold = new Transform[3], crashPanel = new Transform[3];
        private readonly Transform[] galleryShutter = new Transform[9], galleryBulkhead = new Transform[9];
        private readonly Vector3[] doorFrameCentres = new Vector3[3];

        public static float MachineFloor(float depth) => -HeartFloorDrop*Ease(ShaftDepth,HeartDepth,depth);

        public static Vector4 MachineDoorSink(int door,float progress)
        {
            Vector3 thresholdPosition = MachinePoint(DoorDepth+.95f,(door-1)*LaneWidth);
            float radius = 2.47f*Ease(.16f,.32f,progress);
            return new Vector4(thresholdPosition.x,thresholdPosition.z,radius*radius,1);
        }

        private static float UpperSupport(float depth) => UpperDeck*(1-Ease(ShaftDepth,HeartDepth,depth));

        public static bool MachineTravelCamera(Journey journey,Vector3 actor,out Vector3 position,out Vector3 focus)
        {
            float depth=journey.Distance;
            position=focus=Vector3.zero;
            if(journey.Branch!=ForestBranch.Right || depth<168) return false;
            float altitude=journey.Flight.Height;
            if(depth<DoorDepth)
            {
                float framing=Ease(168,174.4f,depth);
                float rise=Ease(.74f,1.52f,altitude);
                float back=Mathf.Lerp(Mathf.Max(4.73f,9.25f-(DoorDepth-depth)),3.95f,rise);
                float ground=Ground(actor.x,actor.z);
                Vector3 hall=new Vector3(Mathf.Lerp(actor.x,MachineCenter,.30f),ground+2.21f,actor.z-back);
                Vector3 face=new Vector3(Mathf.Lerp(actor.x,MachineCenter,.32f),ground+Mathf.Lerp(1.64f,3.63f,framing),DoorDepth+25);
                position=Vector3.Lerp(hall,actor+new Vector3(.12f,1.01f,-3.95f),rise);
                focus=Vector3.Lerp(face,actor+new Vector3(0,.14f,3.2f),rise);
                return true;
            }

            float gallery=Ease(InnerGateDepth,InnerGateDepth+3.2f,depth)*(1-Ease(ShaftDepth-6,ShaftDepth-2,depth));
            float slope=Ease(ShaftDepth-1.3f,ShaftDepth+5,depth);
            float opening=Ease(HeartDepth-7,HeartDepth-1,depth);
            float distance=Mathf.Lerp(Mathf.Lerp(3.68f+.79f*gallery,4.31f,slope),3.06f,opening);
            float cameraDepth=depth-distance;
            float floorBehind=MachinePoint(cameraDepth,actor.x-MachineCenter,UpperSupport(cameraDepth)).y;
            float ride=Mathf.Max(actor.y+1.03f+.28f*gallery,floorBehind+1.24f);
            position=new Vector3(actor.x+.12f,ride,actor.z-distance);
            focus=actor+new Vector3(0,Mathf.Lerp(.32f+1.42f*gallery,-.45f,slope)+opening,Mathf.Lerp(4.45f,3.91f,slope));
            return true;
        }

        public static void MachineHeartCamera(Vector3 actor,float progress,out Vector3 position,out Vector3 focus)
        {
            Vector3 chamber=MachinePoint(HeartDepth);
            float pan=OpeningStoryboard.Window(progress,.13f,.27f,.40f,.58f);
            float pursuit=Ease(.53f,.72f,progress);
            position=Vector3.Lerp(actor+new Vector3(.22f,1.45f,-3.08f),
                chamber+new Vector3(-2.80f,2.51f,-1.19f),pan);
            position=Vector3.Lerp(position,actor+new Vector3(.43f,1.58f,-2.28f),pursuit);
            focus=Vector3.Lerp(actor+new Vector3(0,.83f,3.53f),
                chamber+new Vector3(.20f,2.53f,3.36f),pan);
            focus=Vector3.Lerp(focus,actor+new Vector3(.09f,.35f,1.42f),pursuit);
        }

        public static void MachineDoorCamera(Vector3 actor,int door,float progress,out Vector3 position,out Vector3 focus)
        {
            float observe = Ease(.35f,.58f,progress);
            float close = Ease(.64f,.79f,progress);
            Vector3 wide = door == 0 ? new Vector3(1.42f,1.29f,-3.32f) :
                door == 1 ? new Vector3(1.49f,1.45f,-2.93f) : new Vector3(-1.13f,1.03f,-2.76f);
            Vector3 tight = door == 0 ? new Vector3(.79f,.67f,-1.81f) :
                door == 1 ? new Vector3(.63f,1.89f,-1.67f) : new Vector3(.69f,1.49f,-2.05f);
            Vector3 entry = new Vector3(.28f,1.12f,-2.05f);
            position = actor + Vector3.Lerp(Vector3.Lerp(entry,wide,observe),tight,close);
            focus = actor + new Vector3(0,-.10f,.65f+observe*.29f-close*.25f);
            if(door==2)
            {
                float submerged=Ease(.68f,.89f,progress);
                float waterline=MachinePoint(DoorDepth,LaneWidth,-1.51f).y;
                position.y=Mathf.Lerp(position.y,waterline+.92f,submerged);
                focus.y=Mathf.Lerp(focus.y,waterline-.21f,submerged);
            }
        }

        public static Vector3 MachineDoorActor(Vector3 start,int door,float progress)
        {
            float step = Ease(.01f,.18f,progress);
            float drop = Ease(.18f,.34f,progress);
            Vector3 chamber = MachinePoint(DoorDepth,(door-1)*LaneWidth,-2.06f);
            start.x = Mathf.Lerp(start.x,chamber.x,.72f*step+.27f*drop);
            start.y = Mathf.Lerp(Mathf.Lerp(start.y,MachinePoint(DoorDepth,(door-1)*LaneWidth,.74f).y,step),chamber.y,drop);
            start.z += 1.26f*step+1.94f*drop;
            if (door == 0)
            {
                float grip = Ease(.45f,.59f,progress)-Ease(.69f,.74f,progress);
                float slide = Ease(.59f,.79f,progress);
                return start + new Vector3(.38f*grip,.24f*grip-.31f*slide,-.38f*grip+.24f*slide);
            }
            if (door == 1)
            {
                float retreat = Ease(.42f,.56f,progress)-Ease(.57f,.68f,progress);
                float pass = Ease(.60f,.81f,progress);
                return start + new Vector3(-.42f*retreat+.30f*Mathf.Sin(pass*Mathf.PI),
                    .19f*pass-.98f*Ease(.81f,.95f,progress),-.66f*retreat+.94f*pass);
            }
            return start + new Vector3(.20f*Mathf.Sin(progress*12)*Ease(.45f,.72f,progress),
                -1.35f*Ease(.58f,.91f,progress),.26f*Ease(.39f,.72f,progress));
        }

        public static Vector3 MachineCableActor(Vector3 start,float progress,int port)
        {
            Vector3 origin = CablePorts[Mathf.Clamp(port,0,CablePorts.Length-1)];
            float pull = Ease(.69f,.98f,progress);
            float catchUp = Mathf.Sin(Ease(.58f,.93f,progress)*Mathf.PI);
            return start + new Vector3(origin.x*.22f*catchUp,
                .25f*catchUp-2.52f*Ease(.79f,1,progress),3.22f*pull);
        }

        public void DrawMachine(Journey journey,bool forest,float time,Vector3 actor)
        {
            bool visible = forest && journey.Distance >= 101 && journey.Branch != ForestBranch.Left;
            if (!visible)
            {
                if (machineRoute != null) machineRoute.gameObject.SetActive(false);
                return;
            }

            if (machineRoute == null) BuildMachine();
            machineRoute.gameObject.SetActive(true);
            float depth = journey.Distance;
            bool inDoor = journey.Phase == Stage.ForestDoor;
            bool crossed = journey.Branch == ForestBranch.Right && depth >= DoorDepth && !inDoor;
            bool privateRoom=crossed && depth>=GalleryRevealDepth;
            machineBrain.gameObject.SetActive(privateRoom);
            machineHeart.gameObject.SetActive(crossed && depth>=HeartDepth-16);
            float hatchLift=crossed?Ease(DoorDepth,OuterGateDepth+.06f,depth):0;
            float bulkheadLift=privateRoom?Ease(GalleryRevealDepth+.30f,InnerGateDepth+.06f,depth):0;
            PoseGalleryGate(galleryShutter,hatchLift);
            PoseGalleryGate(galleryBulkhead,bulkheadLift);

            machineGearRoom.gameObject.SetActive(inDoor && journey.ForestDoor == 0);
            machineRobotRoom.gameObject.SetActive(inDoor && journey.ForestDoor == 1);
            machinePoolRoom.gameObject.SetActive(inDoor && journey.ForestDoor == 2);
            for(int i=0;i<3;i++)
            {
                float open=inDoor && journey.ForestDoor==i ? Ease(.14f,.32f,journey.Progress) : 0;
                threshold[i].localPosition=doorFrameCentres[i]+new Vector3(0,-.65f*open,-.55f*open);
                threshold[i].localRotation=Quaternion.Euler(-70*open,0,0);
                crashPanel[i].localPosition=doorFrameCentres[i]+Vector3.up*(8.0f*open);
                for (int light = 0; light < 2; light++)
                    Light(doorwayNodes[i*2+light],Color.Lerp(Circuit,HeartAmber,open),.11f+.47f*open);
            }
            if (inDoor) AnimateMachineDoor(journey,time,actor);
            if (privateRoom) AnimateMachineInterior(journey,time,actor);
        }

        private static Vector3 MachinePoint(float depth,float x = 0,float y = 0)
        {
            float worldX = MachineCenter + x, z = depth + 25;
            return new Vector3(worldX,Ground(worldX,z)+MachineFloor(depth)+y,z);
        }

        private static Vector3 GalleryPoint(float depth,float x,float y) =>
            MachinePoint(depth,0,UpperDeck+y)+Vector3.right*x;

        private static void PoseGalleryGate(Transform[] bands,float lift)
        {
            for(int i=0;i<bands.Length;i++)
            {
                float closed=(i+.5f)*StoreyHeight/bands.Length;
                float pocket=StoreyHeight+.52f;
                float height=Mathf.Min(pocket,closed+lift*(StoreyHeight+.60f));
                float intoRoof=Ease(pocket-.27f,pocket,height);
                bands[i].localPosition=new Vector3(0,height,intoRoof*(.16f+(bands.Length-1-i)*.17f));
            }
        }

        private Transform MachinePart(string name,WorldMesh geometry,Material material,Transform parent) =>
            Piece(name,geometry.Finish(name),material,parent,Vector3.zero);

        private void BuildMachine()
        {
            machineRoute = Group("Buried machine, forest route",root.transform);
            Shader glassShader = Resources.Load<Shader>("EchoesMachine");
            if (glassShader != null)
            {
                machineGlass = new Material(glassShader) { name = "Tinted neural glass" };
                machineSign = new Material(glassShader) { name = "Painted entrance plaque" };
                machineSign.SetFloat("_Sign",1);
                machineSign.SetTexture("_MainTex",Resources.Load<Texture2D>("EchoesMachineSign"));
                machinePool = new Material(glassShader) { name = "Mirror-data liquid" };
                machinePool.SetFloat("_Pool",1);
                machinePool.SetFloat("_Opacity",.64f);
            }

            var bar = Geometry();
            bar.Tube(new[]{Vector3.down*.5f,Vector3.up*.5f},new[]{1f,1f},8,Color.white);
            machineBar = bar.Finish("Reusable plated link");
            var sphere = Geometry();
            sphere.Rock(Vector3.zero,Vector3.one,Color.white,.21f,10,16,.035f);
            machineSphere = sphere.Finish("Reusable curved machine plate");
            var robot = Geometry();
            robot.Box(new Vector3(0,.38f,0),new Vector3(.54f,.55f,.64f),Quaternion.Euler(0,45,0),Iron);
            robot.Box(new Vector3(0,.69f,-.065f),new Vector3(.52f,.24f,.49f),Quaternion.identity,Coal);
            robot.Box(new Vector3(0,.125f,0),new Vector3(.68f,.20f,.72f),Quaternion.Euler(0,45,0),Bronze);
            for (int side=-1;side<=1;side+=2)
                robot.Box(new Vector3(side*.29f,.19f,.015f),new Vector3(.15f,.25f,.67f),Quaternion.identity,Coal);
            machineRobotShell = robot.Finish("Crawler sorting unit");

            BuildMachineApproach();
            BuildMachineGates();
            BuildMachineVault();
            BuildDoorRooms();
            BuildMachineBrain();
            BuildMachineHeart();
        }

        private void BuildMachineApproach()
        {
            var hardware = Geometry();
            var roots = Geometry();
            var inlays = Geometry();
            for (int i=0;i<17;i++)
            {
                float depth = 120+i*3.08f;
                float decay = Ease(118,166,depth);
                for (int side=-1;side<=1;side+=2)
                {
                    float offset = side*(3.45f+1.02f*Mathf.Sin(i*2.31f + side));
                    Vector3 foot = MachinePoint(depth,offset,-.09f);
                    var angles = Quaternion.Euler(74-47*decay,i*38.1f,23*side);
                    Cog(hardware,foot+Vector3.up*(.28f+.49f*decay),.46f+.66f*decay,.15f,9+i%5,angles,
                        Color.Lerp(Bronze,Iron,decay*.7f));
                    roots.Tube(new[]{foot+new Vector3(side*2.6f,-.31f,-2.04f),foot+new Vector3(side*.83f,.26f,-.64f),
                        foot+Vector3.up*(.47f-.18f*decay),foot+new Vector3(side*.71f,.12f,1.26f)},
                        new[]{.22f,.15f,.10f,.016f},7,new Color(.30f,.21f,.135f));
                    if (i<6) continue;
                    Vector3 near = MachinePoint(depth,side*3.85f,.16f);
                    Vector3 next = MachinePoint(depth+3.13f,side*3.85f,.16f);
                    hardware.Tube(new[]{near,next},new[]{.16f,.16f},10,WorldMesh.Shade(Iron,.68f+decay*.2f));
                    Ring(hardware,Vector3.Lerp(near,next,.38f),.21f,.041f,Bronze,Quaternion.identity,14,1);
                    hardware.Box(near+Vector3.up*.17f,new Vector3(.57f,.20f,.42f),Quaternion.identity,Coal);
                    if (i>10) inlays.Box(near+new Vector3(0,.30f,.04f),new Vector3(.14f,.026f,.23f),
                        Quaternion.identity,WorldMesh.Shade(Circuit,.27f));
                }
            }
            for (int i=0;i<21;i++)
            {
                float depth = 160+i*.89f;
                float width = 5.8f+Mathf.Min(3.35f,(depth-160)*.38f);
                hardware.Box(MachinePoint(depth,0,.02f),new Vector3(width,.13f,.885f),Quaternion.identity,
                    WorldMesh.Shade(i%4==0?Iron:Coal,.86f+Mathf.Sin(i*1.9f)*.08f));
                for(int side=-1;side<=1;side+=2)
                {
                    hardware.Box(MachinePoint(depth,side*(width*.47f),.16f),new Vector3(.23f,.26f,.86f),
                        Quaternion.identity,Bronze);
                    if (i%3==0) inlays.Box(MachinePoint(depth,side*3.34f,.13f),new Vector3(.23f,.034f,.53f),
                        Quaternion.identity,WorldMesh.Shade(HeartAmber,.56f));
                }
            }
            MachinePart("Rust, buried gear teeth and deck",hardware,stone,machineRoute);
            MachinePart("Roots growing into the bearings",roots,wood,machineRoute);
            MachinePart("Vestibule deck inlays",inlays,glow,machineRoute);
        }

        private void BuildMachineGates()
        {
            var slabs = Geometry();
            var seam = Geometry();
            for (int door=0;door<3;door++)
            {
                float x = (door-1)*LaneWidth;
                Vector3 at = MachinePoint(DoorDepth+.16f,x);
                doorFrameCentres[door]=at;
                for (int side=-1;side<=1;side+=2)
                {
                    float xPost = side*.94f;
                    slabs.Box(at+new Vector3(xPost,StoreyHeight*.5f,.17f),new Vector3(.37f,StoreyHeight,.94f),
                        Quaternion.identity,Iron);
                    slabs.Box(at+new Vector3(xPost,StoreyHeight-.26f,-.16f),new Vector3(.39f,.41f,.83f),
                        Quaternion.Euler(0,0,side*31),Bronze);
                    slabs.Box(at+new Vector3(xPost,.21f,-.43f),new Vector3(.47f,.46f,.31f),Quaternion.identity,Coal);
                    slabs.Box(at+new Vector3(xPost+side*.16f,StoreyHeight*.42f,-.21f),
                        new Vector3(.095f,StoreyHeight*.72f,.36f),Quaternion.identity,WorldMesh.Shade(Bronze,.81f));
                    for(int rivet=0;rivet<8;rivet++)
                    {
                        Vector3 bolt=at+new Vector3(xPost+side*.13f,.53f+rivet*.81f,-.425f);
                        slabs.Rock(bolt,new Vector3(.052f,.052f,.032f),WorldMesh.Shade(Bronze,.62f+rivet*.071f),rivet*.56f,3,7,.03f);
                    }
                    seam.Box(at+new Vector3(xPost,1.61f,-.410f),new Vector3(.031f,.83f,.022f),
                        Quaternion.identity,WorldMesh.Shade(Circuit,.24f));
                    var node=Lamp("Low portal lock light",at+new Vector3(xPost,1.13f,-.417f),.067f,Circuit,.11f);
                    node.transform.SetParent(machineRoute,false);
                    doorwayNodes[door*2+(side==1?1:0)]=node;
                }
                slabs.Box(at+new Vector3(0,StoreyHeight+.18f,.08f),new Vector3(2.05f,.36f,.98f),Quaternion.identity,Iron);
                slabs.Box(at+new Vector3(0,StoreyHeight+.12f,-.42f),new Vector3(1.60f,.19f,.21f),Quaternion.identity,
                    WorldMesh.Shade(Bronze,.74f));
                var panel=Geometry();
                panel.Box(new Vector3(0,StoreyHeight*.5f,1.72f),new Vector3(1.78f,StoreyHeight+.09f,.25f),
                    Quaternion.identity,new Color(.022f,.030f,.042f));
                for(int louvre=0;louvre<13;louvre++)
                {
                    Vector3 section=new Vector3(0,.38f+louvre*.55f,1.55f);
                    panel.Box(section,new Vector3(1.65f,.08f,.21f),Quaternion.Euler(12,0,0),
                        WorldMesh.Shade(Iron,.50f+.12f*Mathf.Sin(louvre*1.4f)));
                }
                crashPanel[door]=MachinePart("Recess behind portal",panel,stone,machineRoute);
                Cog(slabs,at+new Vector3(0,StoreyHeight+.26f,-.59f),.24f,.11f,9,Quaternion.identity,Bronze);
                var sill=Geometry();
                sill.Box(new Vector3(0,-.034f,-.29f),new Vector3(1.68f,.068f,.28f),Quaternion.identity,Bronze);
                threshold[door]=MachinePart("Tilting portal sill",sill,stone,machineRoute);
            }

            Vector3 signAt = MachinePoint(DoorDepth-2.85f,3.90f,1.28f);
            slabs.Box(signAt+Vector3.down*.73f,new Vector3(.14f,1.55f,.18f),Quaternion.identity,Bronze);
            slabs.Box(signAt,new Vector3(1.58f,.62f,.135f),Quaternion.identity,Coal);
            for (int i=0;i<4;i++)
                slabs.Rock(signAt+new Vector3((i%2*2-1)*.71f,(i/2*2-1)*.24f,-.076f),
                    new Vector3(.048f,.038f,.023f),Bronze,i,3,6,.10f);
            if (machineSign != null)
            {
                var signMesh = new Mesh { name = "Choose one engraving" };
                signMesh.vertices = new[]{new Vector3(-.73f,-.24f,0),new Vector3(-.73f,.24f,0),
                    new Vector3(.73f,.24f,0),new Vector3(.73f,-.24f,0)};
                signMesh.uv = new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};
                signMesh.colors = new[]{Color.white,Color.white,Color.white,Color.white};
                signMesh.triangles = new[]{0,1,2,0,2,3};
                signMesh.RecalculateNormals();
                meshes.Add(signMesh);
                Piece("选择一个",signMesh,machineSign,machineRoute,signAt+Vector3.back*.080f);
            }
            MachinePart("Three equally weighted doorframes",slabs,stone,machineRoute);
            MachinePart("Recessed entrance lines",seam,glow,machineRoute);
        }

        private void BuildMachineVault()
        {
            var rests=Geometry();
            var jacket=Geometry();
            var foundations=Geometry();
            var bark=Geometry();
            var canopy=Geometry();
            Vector3 archCentre=MachinePoint(178.0f,0,1.11f);
            for(int arc=0;arc<3;arc++)
            {
                Vector3 baseline=archCentre+Vector3.forward*(arc*2.36f);
                var rim=new Vector3[25];
                var widths=new float[rim.Length];
                for(int i=0;i<rim.Length;i++)
                {
                    float theta=Mathf.PI*i/(rim.Length-1);
                    float arch=Mathf.Sin(theta);
                    float clearance=2.96f*Mathf.Pow(arch,3);
                    rim[i]=baseline+new Vector3(Mathf.Cos(theta)*5.81f,arch*7.52f+clearance,0);
                    widths[i]=.19f+.10f*arch;
                    if(i==0 || i+1==rim.Length) continue;
                    Vector3 rib=baseline+new Vector3(Mathf.Cos(theta)*6.17f,arch*7.97f+clearance,.31f);
                    rests.Box(rib,new Vector3(.72f,1.08f,1.09f),Quaternion.Euler(0,0,21-theta*Mathf.Rad2Deg),
                        WorldMesh.Shade(Iron,.71f+.20f*Mathf.Sin(i*1.33f+arc)));
                    if(arc==2) continue;
                    Vector3 before=rib-Vector3.forward*.58f, after=rib+Vector3.forward*2.22f;
                    jacket.Tube(new[]{before,rib+Vector3.forward*.51f,after},new[]{.13f,.17f,.14f},8,Coal);
                }
                jacket.Tube(rim,widths,12,WorldMesh.Shade(Bronze,.84f));
            }
            var brow=MachinePoint(DoorDepth,.01f,StoreyHeight+.097f);
            rests.Box(brow+new Vector3(0,0,.36f),new Vector3(7.65f,.194f,1.76f),Quaternion.identity,
                WorldMesh.Shade(Iron,.64f));
            for(int rib=0;rib<7;rib++)
            {
                Vector3 brace=brow+new Vector3((rib-3)*.98f,.20f,-.50f);
                rests.Box(brace,new Vector3(.32f,.34f,.66f),Quaternion.Euler(19,0,0),
                    WorldMesh.Shade(Bronze,.71f+.073f*rib));
            }
            BuildUpperEnvelope(rests);
            BuildGalleryGate(galleryShutter,OuterGateDepth,true);
            BuildGalleryGate(galleryBulkhead,InnerGateDepth,false);
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 buttress=GalleryPoint(DoorDepth+2.76f,side*5.51f,3.29f);
                rests.Box(buttress,new Vector3(1.17f,7.66f,8.2f),Quaternion.identity,WorldMesh.Shade(Iron,.41f));
                rests.Box(buttress+new Vector3(side*.28f,4.16f,-.11f),new Vector3(1.05f,.48f,8.34f),
                    Quaternion.identity,WorldMesh.Shade(Bronze,.48f));
            }
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<10;i++)
                {
                    float d=169.7f+i*3.05f;
                    float width=5.87f+.53f*Mathf.Sin(i*.86f);
                    Vector3 bed=MachinePoint(d,side*width,-.57f);
                    foundations.Rock(bed+Vector3.up*(.61f+.411f*Mathf.Sin(i*1.77f)),
                        new Vector3(2.85f,1.50f+.27f*Mathf.Sin(i),2.39f),
                        Color.Lerp(Coal,Iron,.39f+.22f*Mathf.Cos(i)),i*.40f+side*.17f,6,11,.21f);
                    rests.Box(bed+new Vector3(-side*.87f,.19f,0),new Vector3(2.02f,1.4f,2.83f),
                        Quaternion.Euler(0,side*6,side*13),WorldMesh.Shade(Iron,.64f));
                    if(i<6) continue;
                    BuildMachineShoulder(rests,d,side);
                }
                for(int j=0;j<3;j++)
                {
                    float depth=166.8f+j*6.4f;
                    var foot=MachinePoint(depth,side*(8.38f+j*.41f),-.05f);
                    Tree(bark,canopy,foot,10.9f+j*1.39f,.63f+j*.12f);
                    bark.Tube(new[]{foot,MachinePoint(depth+.66f,side*6.59f,1.83f),
                            MachinePoint(depth+1.42f,side*5.80f,.40f)},new[]{.34f,.19f,.026f},9,
                        new Color(.35f,.24f,.148f));
                }
            }
            MachinePart("Buried foundation merging into the forest",foundations,stone,machineRoute);
            MachinePart("Cast machine vault and service shoulders",rests,stone,machineRoute);
            MachinePart("Root-gripped vault ribs",jacket,stone,machineRoute);
            MachinePart("Trees rooting into the vault",bark,wood,machineRoute);
            MachinePart("Canopy beyond the buried machine",canopy,leaves,machineRoute);
        }

        private void BuildGalleryGate(Transform[] bands,float depth,bool facade)
        {
            Transform frame=Group(facade?"Blank stone exterior":"Inner blackout bulkhead",machineRoute);
            frame.localPosition=GalleryPoint(depth,0,0);
            float stride=StoreyHeight/bands.Length;
            for(int i=0;i<bands.Length;i++)
            {
                var geometry=Geometry();
                Color face=facade?new Color(.22f,.27f,.25f):WorldMesh.Shade(Coal,.84f);
                geometry.Box(Vector3.zero,new Vector3(2*GalleryHalfWidth+.49f,stride+.16f,.49f),Quaternion.identity,
                    WorldMesh.Shade(face,.88f+.11f*Mathf.Sin(i*1.45f)));
                if(facade)
                {
                    geometry.Box(new Vector3(0,-.28f,-.246f),new Vector3(2*GalleryHalfWidth+.44f,.17f,.16f),
                        Quaternion.identity,WorldMesh.Shade(Iron,.42f+.085f*Mathf.Sin(i*1.33f)));
                }
                bands[i]=MachinePart(facade?"Overlapping cast stone course":"Unlit shutter slat",geometry,stone,frame);
            }
            PoseGalleryGate(bands,0);
        }

        private static void BuildUpperEnvelope(WorldMesh shell)
        {
            const float begin=DoorDepth-.84f, end=ShaftDepth+.16f;
            const int spans=42;
            float Depth(int z) => Mathf.Lerp(begin,end,z/(float)spans);
            float Across(int x) => (x/8f-1)*GalleryHalfWidth;
            Color Tarnish(Vector3 at) => WorldMesh.Shade(Coal,.78f+.23f*Mathf.PerlinNoise(at.z*.71f,at.x*.89f));

            shell.Surface(16,spans,(x,z)=>GalleryPoint(Depth(z),Across(x),0),Tarnish,null);
            shell.Surface(16,spans,(x,z)=>GalleryPoint(Depth(z),-Across(x),StoreyHeight-UpperDeck),Tarnish,null);
            shell.Surface(16,spans,(x,z)=>GalleryPoint(Depth(z),-Across(x),StoreyHeight),Tarnish,null);
            shell.Surface(16,spans,(x,z)=>GalleryPoint(Depth(z),Across(x),StoreyHeight+RoofThickness),Tarnish,null);
            for(int side=-1;side<=1;side+=2)
            {
                int wallSide=side;
                shell.Surface(16,spans,(y,z)=>GalleryPoint(Depth(z),wallSide*GalleryHalfWidth,
                    Mathf.Lerp(-.51f,StoreyHeight+RoofThickness,(wallSide>0?y:16-y)/16f)),Tarnish,null);
                shell.Surface(16,spans,(y,z)=>GalleryPoint(Depth(z),wallSide*(GalleryHalfWidth+.35f),
                    Mathf.Lerp(-.51f,StoreyHeight+RoofThickness,(wallSide<0?y:16-y)/16f)),Tarnish,null);
                shell.Box(GalleryPoint(ShaftDepth,side*4.20f,StoreyHeight*.50f),new Vector3(1.51f,StoreyHeight,.27f),
                    Quaternion.identity,WorldMesh.Shade(Coal,.79f));
                shell.Box(GalleryPoint(OuterGateDepth,side*GalleryHalfWidth,StoreyHeight*.50f),
                    new Vector3(.49f,StoreyHeight+.34f,1.19f),Quaternion.identity,WorldMesh.Shade(Iron,.51f));
            }
            shell.Box(GalleryPoint(ShaftDepth,0,6.81f),new Vector3(7.22f,1.29f,.31f),
                Quaternion.identity,WorldMesh.Shade(Coal,.78f));
        }

        private static void BuildMachineShoulder(WorldMesh mesh,float depth,int side)
        {
            Vector3 shoulder=MachinePoint(depth,side*4.49f,-.40f);
            mesh.Box(shoulder,new Vector3(.57f,2.26f,2.98f),Quaternion.identity,WorldMesh.Shade(Coal,1.19f));
            mesh.Box(shoulder+Vector3.up*.77f,new Vector3(.39f,.19f,2.95f),Quaternion.identity,Iron);
        }

        private void BuildDoorRooms()
        {
            machineGearRoom = MachineRoom("Compression chamber",0);
            machineRobotRoom = MachineRoom("Sorting chamber",1);
            machinePoolRoom = MachineRoom("Prediction pool",2);

            leftCrusher = BuildCrusher(machineGearRoom,Bronze);
            rightCrusher = BuildCrusher(machineGearRoom,Iron);
            crusherLight = Lamp("Compressed silhouette",Vector3.zero,.095f,HeartAmber,.94f).transform;
            crusherLight.SetParent(machineGearRoom,false);
            var deck=Geometry();
            deck.Box(new Vector3(0,.02f,0),new Vector3(2.97f,.21f,1.74f),Quaternion.identity,
                WorldMesh.Shade(Iron,.84f));
            for(int i=0;i<9;i++)
                deck.Box(new Vector3(-1.23f+i*.305f,.146f,-.21f),new Vector3(.18f,.055f,1.34f),Quaternion.identity,
                    WorldMesh.Shade(i%3==0?Bronze:Coal,.72f+i*.052f));
            deck.Box(new Vector3(.93f,.49f,-.42f),new Vector3(.075f,.79f,.077f),Quaternion.identity,Bronze);
            deck.Box(new Vector3(1.02f,.88f,-.42f),new Vector3(.43f,.075f,.079f),Quaternion.identity,Bronze);
            pressDeck=MachinePart("Tilting maintenance floor and handhold",deck,stone,machineGearRoom);
            for(int i=0;i<pressSparks.Length;i++)
            {
                pressSparks[i]=Lamp("Compression spark",Vector3.zero,.027f,HeartAmber,0);
                pressSparks[i].transform.SetParent(machineGearRoom,false);
            }
            var chute = Geometry();
            Ring(chute,Vector3.up*.04f+Vector3.forward*.81f,1.10f,.116f,Bronze,Quaternion.Euler(90,0,0),23,1);
            chute.Box(new Vector3(0,-1.35f,1.39f),new Vector3(1.9f,.16f,1.9f),Quaternion.identity,Coal);
            MachinePart("Recycler hopper",chute,stone,machineRobotRoom);
            var hatch=Geometry();
            hatch.Box(new Vector3(.47f,0,0),new Vector3(.95f,.14f,1.87f),Quaternion.identity,Iron);
            for(int i=0;i<5;i++)
                hatch.Box(new Vector3(.47f,.095f,-.67f+i*.33f),new Vector3(.91f,.057f,.085f),Quaternion.identity,
                    WorldMesh.Shade(Bronze,.74f+i*.07f));
            var hatchMesh=hatch.Finish("Twin fluted hopper gates");
            hatchLeft=Piece("Recycler left cover",hatchMesh,stone,machineRobotRoom,Vector3.zero);
            hatchRight=Piece("Recycler right cover",hatchMesh,stone,machineRobotRoom,Vector3.zero);
            hatchLeft.localScale=new Vector3(-1,1,1);
            for (int i=0;i<recyclers.Length;i++)
            {
                Transform unit = Group("Sorting crawler "+i,machineRobotRoom);
                Piece("Stamped crawler shell",machineRobotShell,stone,unit,Vector3.zero);
                var eye = Lamp("Crawler status light",new Vector3(0,.69f,-.325f),.095f,Circuit,.55f);
                eye.transform.SetParent(unit,false);
                eye.transform.localScale = new Vector3(.172f,.033f,.021f);
                recyclers[i] = new Recycler { Root=unit, Eye=eye,
                    Upper=Piece("Upper sorting arm",machineBar,stone,machineRobotRoom,Vector3.zero),
                    Lower=Piece("Lower sorting arm",machineBar,stone,machineRobotRoom,Vector3.zero),
                    Grip=Piece("Sorting clamp",machineSphere,stone,machineRobotRoom,Vector3.zero) };
            }

            poolSurface = Piece("Predictive liquid",machineSphere,machinePool??glow,machinePoolRoom,Vector3.zero);
            poolSurface.localScale=new Vector3(2.45f,.13f,2.78f);
            poolFilm=poolSurface.GetComponent<MeshRenderer>();
            var ripple=Geometry();
            Ring(ripple,Vector3.zero,1,.014f,PoolBlue,Quaternion.Euler(90,0,0),30,1);
            poolRipple=MachinePart("Surface response around the performer",ripple,glow,machinePoolRoom);
            poolRippleLight=poolRipple.GetComponent<MeshRenderer>();
            for (int i=0;i<predictions.Length;i++) predictions[i]=BuildPrediction(i);
        }

        private Transform MachineRoom(string name,int door)
        {
            Transform room = Group(name,machineRoute);
            room.localPosition = MachinePoint(DoorDepth,(door-1)*LaneWidth,-3.10f)+Vector3.forward*3.13f;
            var structure = Geometry();
            var fixtures = Geometry();
            if(door==1)
            {
                structure.Box(new Vector3(0,-.27f,-1.30f),new Vector3(5.48f,.35f,3.51f),Quaternion.identity,Coal);
                structure.Box(new Vector3(0,-.27f,2.91f),new Vector3(5.48f,.35f,1.23f),Quaternion.identity,Coal);
                for(int side=-1;side<=1;side+=2)
                    structure.Box(new Vector3(side*1.86f,-.27f,1.37f),new Vector3(1.76f,.35f,1.90f),Quaternion.identity,Coal);
            }
            else structure.Box(new Vector3(0,door==2?-1.53f:-.27f,.30f),new Vector3(5.48f,.35f,6.55f),Quaternion.identity,Coal);
            float wallDepth=door==2?.63f:1.13f, wallHeight=door==2?4.42f:3.42f;
            structure.Box(new Vector3(0,wallDepth,3.33f),new Vector3(5.48f,wallHeight,.25f),Quaternion.identity,
                WorldMesh.Shade(Iron,.60f));
            for (int side=-1;side<=1;side+=2)
            {
                structure.Box(new Vector3(side*2.68f,wallDepth-.03f,.41f),new Vector3(.19f,wallHeight-.09f,5.85f),
                    Quaternion.identity,WorldMesh.Shade(Iron,.59f));
                for (int i=0;i<5;i++)
                {
                    Vector3 at = new Vector3(side*2.57f,.28f+i*.41f,3.09f-i*.12f);
                    fixtures.Box(at,new Vector3(.095f,.093f,.80f),Quaternion.identity,door==2?PoolBlue:Circuit);
                    structure.Box(at+Vector3.forward*.072f,new Vector3(.15f,.16f,.93f),Quaternion.identity,Coal);
                }
                for(int duct=0;duct<5;duct++)
                {
                    Vector3 place=new Vector3(side*(.51f+duct*.35f),1.82f,3.15f);
                    structure.Tube(new[]{place+Vector3.up*.83f,place,place-Vector3.up*.42f+Vector3.forward*.17f},
                        new[]{.066f,.055f,.055f},9,WorldMesh.Shade(Iron,.70f+duct*.06f));
                    Ring(fixtures,place+Vector3.down*.21f,.066f,.019f,door==2?PoolBlue:HeartAmber,
                        Quaternion.Euler(90,0,0),10,1);
                }
            }
            MachinePart(name+" walls",structure,stone,room);
            MachinePart(name+" industrial status bars",fixtures,glow,room);
            return room;
        }

        private Transform BuildCrusher(Transform room,Color metal)
        {
            var jaw = Geometry();
            var rings = Geometry();
            Cog(jaw,Vector3.zero,1.04f,.68f,14,Quaternion.identity,metal);
            Ring(rings,Vector3.back*.38f,.54f,.035f,WorldMesh.Shade(HeartAmber,.60f),Quaternion.identity,18,1);
            Transform hub=MachinePart("Rotating press cog",jaw,stone,room);
            MachinePart("Cog collar indicator",rings,glow,hub);
            return hub;
        }

        private Prediction BuildPrediction(int index)
        {
            Transform echo=Group("Forecast echo "+index,machinePoolRoom);
            var body=Geometry();
            Vector3 mid=new Vector3(0,.58f,0);
            body.Rock(mid,new Vector3(.21f,.31f,.14f),Color.white,.42f,7,12,.03f);
            body.Rock(new Vector3(0,1.01f,0),new Vector3(.168f,.195f,.17f),Color.white,.40f,7,12,.035f);
            for (int side=-1;side<=1;side+=2)
            {
                body.Tube(new[]{new Vector3(side*.10f,.43f,0),new Vector3(side*.148f,.12f,.015f),
                    new Vector3(side*.15f,.025f,-.085f)},new[]{.080f,.059f,.025f},7,Color.white);
            }
            var shape=MachinePart("Light silhouette",body,machineGlass??glow,echo).GetComponent<MeshRenderer>();
            Transform left=Piece("Echo left arm",machineBar,machineGlass??glow,echo,Vector3.zero);
            Transform right=Piece("Echo right arm",machineBar,machineGlass??glow,echo,Vector3.zero);
            return new Prediction { Root=echo, Body=shape, LeftArm=left, RightArm=right,
                LeftLight=left.GetComponent<MeshRenderer>(),RightLight=right.GetComponent<MeshRenderer>() };
        }

        private void AnimateMachineDoor(Journey journey,float time,Vector3 actor)
        {
            float t = journey.Progress;
            if (journey.ForestDoor == 0)
            {
                float startup=Ease(.31f,.48f,t);
                float reverse=Ease(.49f,.63f,t);
                float press=Ease(.58f,.87f,t);
                float angle=(startup-reverse)*47+Ease(.37f,.54f,t)*65-Ease(.52f,.89f,t)*230;
                float width=Mathf.Lerp(1.65f,.91f,press);
                leftCrusher.localPosition = new Vector3(-width,1.04f,.63f);
                rightCrusher.localPosition = new Vector3(width,1.14f,.65f);
                leftCrusher.localRotation = Quaternion.Euler(4,0,angle);
                rightCrusher.localRotation = Quaternion.Euler(-5,0,-angle-15);
                pressDeck.localPosition=new Vector3(0,.31f-.54f*press,-.14f-.66f*press);
                pressDeck.localRotation=Quaternion.Euler(33*press,0,-12*reverse);
                crusherLight.localPosition = machineGearRoom.InverseTransformPoint(actor)+Vector3.forward*.12f;
                crusherLight.localScale = new Vector3(.021f,.67f,.067f)*Ease(.80f,.89f,t)*(1-.94f*Ease(.91f,1,t));
                for(int i=0;i<pressSparks.Length;i++)
                {
                    float burst=Ease(.84f+i*.002f,.867f+i*.003f,t)*(1-Ease(.89f,.97f,t));
                    var spark=pressSparks[i];
                    float angleAt=i*2.39996f;
                    var direction=new Vector3(Mathf.Cos(angleAt)*.40f,Mathf.Sin(angleAt)*.60f,-.44f-(i%4)*.14f);
                    spark.transform.localPosition=crusherLight.localPosition+direction*(.11f+2.62f*burst)+
                        Vector3.down*Mathf.Pow(burst,1.4f)*.21f;
                    spark.transform.localScale=Vector3.one*(.017f+burst*.034f);
                    Light(spark,HeartAmber,burst*1.34f);
                    spark.gameObject.SetActive(burst>.009f);
                }
                return;
            }
            if (journey.ForestDoor == 1)
            {
                float hatchOpen=Ease(.67f,.78f,t)*(1-Ease(.88f,.97f,t));
                hatchLeft.localPosition=hatchRight.localPosition=new Vector3(0,.07f,1.39f);
                hatchLeft.localRotation=Quaternion.Euler(0,0,-79*hatchOpen);
                hatchRight.localRotation=Quaternion.Euler(0,0,79*hatchOpen);
                Vector3 target=machineRobotRoom.InverseTransformPoint(actor);
                for (int i=0;i<recyclers.Length;i++)
                {
                    Recycler unit=recyclers[i];
                    float phase=i*Mathf.PI*2/recyclers.Length;
                    float creep=Ease(.34f+i*.014f,.67f+i*.017f,t);
                    float radius=Mathf.Lerp(2.40f,1.09f,creep);
                    var from=new Vector3(Mathf.Sin(phase)*radius,.038f,Mathf.Cos(phase)*radius+1.21f);
                    if(i==0) from=Vector3.Lerp(new Vector3(-.50f,.04f,2.62f),from, Ease(.24f,.49f,t));
                    from.y+=.015f*Mathf.Sin(time*12.7f+i*1.2f)*creep;
                    unit.Root.localPosition=from;
                    Vector3 toward=target-from;
                    toward.y=0;
                    unit.Root.localRotation=Quaternion.LookRotation(toward.sqrMagnitude>.003f?-toward:Vector3.back,Vector3.up);
                    Vector3 shoulder=from+Vector3.up*.55f;
                    float cue=.52f+(i%4)*.061f;
                    float present=i==0 ? Ease(.35f,.41f,t)*(1-Ease(.44f,.51f,t))*.65f : 0;
                    float move=Ease(cue,cue+.115f,t)*(1-Ease(cue+.15f,cue+.20f,t));
                    Vector3 contact=target+new Vector3(Mathf.Sin(phase)*.22f,.04f+.18f*Mathf.Sin(i*1.3f),Mathf.Cos(phase)*.19f);
                    Vector3 hand=Vector3.Lerp(shoulder,contact,Mathf.Max(present,move));
                    Vector3 elbow=Vector3.Lerp(shoulder,hand,.58f)+Vector3.up*(.17f+.15f*creep);
                    Link(unit.Upper,shoulder,elbow,.058f);
                    Link(unit.Lower,elbow,hand,.042f);
                    unit.Grip.localPosition=hand;
                    unit.Grip.localScale=new Vector3(.12f-.038f*move,.072f+.024f*move,.080f);
                    float alarm=Ease(.43f+i*.009f,.57f+i*.011f,t);
                    Light(unit.Eye,Color.Lerp(Circuit,Red,alarm),.41f+alarm*.91f+present*.24f);
                }
                return;
            }

            float water = Ease(.44f,.91f,t);
            float surface=1.51f*water-.18f;
            poolSurface.localPosition=new Vector3(0,surface,.74f);
            poolSurface.localScale=new Vector3(2.47f,.096f+.082f*water,2.53f);
            Light(poolFilm,PoolBlue,.29f+1.11f*water);
            float ripple=Ease(.34f,.62f,t)*(1-Ease(.77f,.93f,t));
            poolRipple.gameObject.SetActive(ripple>.002f);
            Vector3 poolActor=machinePoolRoom.InverseTransformPoint(actor);
            poolRipple.localPosition=new Vector3(poolActor.x,surface+.11f,poolActor.z);
            poolRipple.localScale=Vector3.one*(.18f+1.24f*ripple);
            Light(poolRippleLight,Circuit,ripple*.58f);
            float latest = journey.ForestEchoLane(0), prior = journey.ForestEchoLane(.55f);
            bool predicts = t>=.57f;
            for(int i=0;i<predictions.Length;i++)
            {
                Prediction echo=predictions[i];
                float cue=i==0?.34f:.51f+i*.014f;
                float active=Ease(cue,cue+.13f,t);
                echo.Root.gameObject.SetActive(active>.012f);
                if (active<=.012f) continue;
                float a=i*2.39996f;
                float radius = i<5 ? .62f+i*.32f : 1.65f+(i%3)*.23f;
                float imitation = predicts ? Mathf.Clamp(latest+(latest-prior)*.93f+.15f*Mathf.Sin(time*2+i),-1,1) : journey.ForestEchoLane(.19f+i*.091f);
                bool fist = journey.ForestEchoHand(predicts?0:.25f+i*.081f)==Hand.Fist;
                float skew = Mathf.Sin(time*2.4f+i)*.24f;
                float remaining = i==0 ? 1 : 1-Ease(.85f+i*.002f,.97f,t);
                echo.Root.localPosition = new Vector3(Mathf.Sin(a)*radius+.36f*imitation,
                    surface-1.03f+active*1.05f-(1-remaining)*.77f+.045f*Mathf.Sin(time*4f+i),.78f+Mathf.Cos(a)*radius+skew);
                echo.Root.localScale=Vector3.one*(.55f+active*.49f);
                echo.Root.localRotation=Quaternion.Euler(0,imitation*36,imitation*-11);
                Color tint=Color.Lerp(PoolBlue,Circuit,Mathf.Repeat(i*.37f,1));
                Light(echo.Body,tint,(.72f+water*.47f+skew)*remaining);
                Vector3 left = new Vector3(-.17f,.77f,0), right = new Vector3(.17f,.77f,0);
                float raise = fist ? .14f : .42f+.09f*Mathf.Sin(time*7+i);
                Link(echo.LeftArm,left,new Vector3(-raise,.56f+raise,.08f),.071f);
                Link(echo.RightArm,right,new Vector3(raise,.56f+raise,-.07f),.071f);
                Light(echo.LeftLight,tint,.75f*remaining);
                Light(echo.RightLight,tint,.75f*remaining);
            }
        }

        private void BuildMachineBrain()
        {
            machineBrain = Group("Upper service floor, two hemispheres and descending spine",machineRoute);
            var housing = Geometry();
            var glass = Geometry();
            var nerves = Geometry();
            var spine = Geometry();
            FurnishUpperGallery(housing,glass,nerves);
            BuildNeuralCanopy(glass,nerves);
            var sheath=new Vector3[returnPulses.Length];
            var widths=new float[sheath.Length];
            for(int i=0;i<sheath.Length;i++)
            {
                float depth=Mathf.Lerp(HeartDepth+3.72f,InnerGateDepth+4.4f,i/(sheath.Length-1f));
                float back=Ease(0,1,i/(sheath.Length-1f));
                float chamberLead=(1-Ease(.05f,.115f,back))*(1.43f+1.10f*Ease(0,.05f,back));
                float height=2.23f+UpperSupport(depth)+.50f*back+chamberLead;
                float side=Mathf.Lerp(.58f,3.58f,Ease(0,.075f,back))*(1-.49f*Ease(.89f,1,back));
                sheath[i]=MachinePoint(depth,side,height);
                widths[i]=.20f+.053f*Mathf.Sin(i*.6f);
                returnPulses[i]=Lamp("Impulse returning toward the brain",sheath[i]+Vector3.up*.16f,
                    .14f,HeartAmber,.008f);
                returnPulses[i].transform.SetParent(machineBrain,false);
                returnPulses[i].gameObject.SetActive(false);
            }
            spine.Tube(sheath,widths,12,WorldMesh.Shade(Iron,.62f));
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 top=sheath[sheath.Length-1];
                housing.Tube(new[]{top,top+new Vector3(side*1.05f,.18f,1.51f),
                    GalleryPoint(InnerGateDepth+5.7f,side*3.22f,4.06f)},new[]{.13f,.16f,.20f},11,Bronze);
            }
            for (int side=-1;side<=1;side+=2)
            {
                for (int lobe=0;lobe<8;lobe++)
                {
                    float depth=InnerGateDepth+.9f+lobe*2.24f;
                    float crest=3.76f+.58f*Mathf.Sin(lobe*.70f);
                    Vector3 basePosition=GalleryPoint(depth,side*(3.13f+Mathf.Sin(lobe*.9f)*.31f),crest);
                    glass.Rock(basePosition,new Vector3(1.16f,1.51f,1.68f),
                        WorldMesh.Shade(lobe%2==0?PoolBlue:Circuit,.95f),lobe*.13f+side*.05f,10,14,.05f);
                    for (int groove=0;groove<3;groove++)
                    {
                        var sweep = new Vector3[15];
                        var width = new float[sweep.Length];
                        for (int p=0;p<sweep.Length;p++)
                        {
                            float angle = (p/(sweep.Length-1f)*1.25f+.23f*groove)*Mathf.PI;
                            sweep[p]=basePosition+new Vector3(Mathf.Cos(angle)*(1.14f+.1f*Mathf.Sin(p*.8f)),
                                Mathf.Sin(angle)*1.48f + .067f*Mathf.Sin(p*.95f+groove),-.72f+groove*.63f+Mathf.Sin(angle*2)*.34f);
                            width[p]=.013f+groove*.004f+.005f*Mathf.Sin(angle);
                        }
                        nerves.Tube(sweep,width,7,WorldMesh.Shade(Circuit,.58f+.15f*groove));
                    }
                    Vector3 mounting=GalleryPoint(depth,side*4.63f,.26f);
                    housing.Tube(new[]{mounting,mounting+new Vector3(0,2.11f,-.32f),basePosition+new Vector3(side*.72f,0,0)},
                        new[]{.17f,.14f,.21f},10,Iron);
                    housing.Box(mounting,new Vector3(.69f,1.03f,.73f),Quaternion.identity,Coal);
                }
                for (int lane=-1;lane<=1;lane++)
                {
                    Vector3 fil=GalleryPoint(InnerGateDepth+6.87f,side*3.05f,3.68f+lane*.35f);
                    Vector3 outlet=MachinePoint(181,lane*LaneWidth,3.53f);
                    Vector3 feeder=GalleryPoint(InnerGateDepth+4.98f,side*(4.06f+lane*.14f),1.56f);
                    Vector3 throat=GalleryPoint(InnerGateDepth+1.70f,side*(3.87f+lane*.13f),-.72f);
                    Vector3 below=MachinePoint(184.2f,side*(3.02f+lane*.21f),5.31f);
                    nerves.Tube(new[]{fil,feeder,throat,below,outlet},
                        new[]{.032f,.052f,.039f,.032f,.024f},7,Color.Lerp(PoolBlue,Circuit,(lane+1)*.38f));
                    Ring(housing,outlet,.21f,.041f,Bronze,Quaternion.Euler(84,0,0),13,1);
                }
                const int ribCount=35;
                const float ribStride=(HeartDepth-ShaftDepth)/ribCount;
                for (int ring=0;ring<=ribCount;ring++)
                {
                    float depth=ShaftDepth+ring*ribStride;
                    float floor=UpperSupport(depth);
                    float width=3.57f+.45f*Mathf.Sin(ring*.48f)+.71f*Ease(HeartDepth-12,HeartDepth,depth);
                    Vector3 at=MachinePoint(depth,side*width,floor+2.24f);
                    housing.Box(at,new Vector3(.39f,4.56f,.30f),Quaternion.Euler(0,0,side*15),Iron);
                    Vector3 lower=MachinePoint(depth,side*(width-.49f),floor-.12f);
                    Vector3 upper=MachinePoint(depth,side*(width-.42f),floor+4.27f);
                    Vector3 inner=MachinePoint(depth,side*1.82f,floor+5.16f);
                    spine.Tube(new[]{lower,at,upper,inner},new[]{.15f,.20f,.16f,.099f},9,
                        Color.Lerp(Bronze,new Color(.51f,.18f,.13f),Ease(HeartDepth-23,HeartDepth-4,depth)));
                    if (ring==ribCount) continue;
                    float next=depth+ribStride;
                    Vector3 wire=MachinePoint(depth,side*(width*.76f),floor+1.62f+.25f*Mathf.Sin(ring));
                    Vector3 wireNext=MachinePoint(next,side*(width*.76f),UpperSupport(next)+1.62f+.25f*Mathf.Sin(ring+1));
                    Color pulseColor=Color.Lerp(Circuit,HeartAmber,Ease(HeartDepth-25,HeartDepth-1,depth));
                    nerves.Tube(new[]{wire,wireNext},new[]{.042f,.040f},7,WorldMesh.Shade(pulseColor,.60f));
                    housing.Tube(new[]{wire+Vector3.down*.20f,wireNext+Vector3.down*.20f},
                        new[]{.108f,.099f},8,WorldMesh.Shade(Iron,.61f));
                    if(ring%5==0)
                    {
                        var lockAt=MachinePoint(depth,side*(width-.45f),floor+.74f);
                        housing.Box(lockAt,new Vector3(.61f,1.19f,.53f),Quaternion.Euler(0,0,side*12),Coal);
                        Ring(nerves,lockAt+new Vector3(-side*.19f,.30f,-.29f),.136f,.019f,pulseColor,Quaternion.identity,12,1);
                    }
                }
            }

            BuildDescentFabric(housing,spine);

            MachinePart("Hemispheres worn glass",glass,machineGlass??glow,machineBrain);
            MachinePart("Wiring to the three false exits",nerves,glow,machineBrain);
            MachinePart("Jointed spine liner",spine,stone,machineBrain);
            MachinePart("Machine chambers and sloping deck",housing,stone,machineBrain);
            for(int i=0;i<brainPulses.Length;i++)
            {
                float depth=Mathf.Lerp(InnerGateDepth+1.1f,HeartDepth-1,i/(brainPulses.Length-1f));
                float side=i%2==0?-1:1;
                float y=UpperSupport(depth)+1.43f+.24f*Mathf.Sin(i*.93f);
                float width=depth<ShaftDepth?2.05f:2.67f;
                brainPulses[i]=Lamp("Impulse in the spine",MachinePoint(depth,side*width,y),.083f,Circuit,.36f);
                brainPulses[i].transform.SetParent(machineBrain,false);
            }
        }

        private static void BuildNeuralCanopy(WorldMesh membrane,WorldMesh gyri)
        {
            for(int side=-1;side<=1;side+=2)
            {
                int bank=side;
                Vector3 Gyrus(float along,float around)
                {
                    float depth=Mathf.Lerp(InnerGateDepth+.33f,ShaftDepth-1.20f,along);
                    float end=Mathf.Pow(Mathf.Sin(Mathf.PI*along),.36f);
                    float turn=around*Mathf.PI*2;
                    float ridge=.12f*Mathf.Cos(turn*7+along*30)+.072f*Mathf.Sin(turn*13-along*22);
                    float x=bank*3.16f+(1.23f+ridge)*Mathf.Cos(turn)*end;
                    float y=3.94f+(1.74f+ridge)*Mathf.Sin(turn)*end;
                    return GalleryPoint(depth,x,y);
                }
                membrane.Surface(35,78,(u,v)=>Gyrus(.003f+.994f*v/78,1-u/35f),
                    vertex=>WorldMesh.Shade(bank<0?PoolBlue:Circuit,.59f+.13f*Mathf.Sin(vertex.z*.83f+vertex.y*1.2f)),null);
                for(int i=0;i<11;i++)
                {
                    var trace=new Vector3[35];
                    var size=new float[trace.Length];
                    for(int p=0;p<trace.Length;p++)
                    {
                        float along=.036f+.926f*p/(trace.Length-1f);
                        float turn=i/11f+.029f*Mathf.Sin(along*31+i*.76f)+.013f*Mathf.Sin(along*59+i);
                        trace[p]=Gyrus(along,turn)+new Vector3(Mathf.Cos(turn*Mathf.PI*2),Mathf.Sin(turn*Mathf.PI*2),0)*.018f;
                        size[p]=.011f+.012f*Mathf.Sin(along*Mathf.PI);
                    }
                    gyri.Tube(trace,size,6,WorldMesh.Shade(bank<0?PoolBlue:Circuit,.53f+.20f*i/11));
                }
            }
        }

        private static void FurnishUpperGallery(WorldMesh housing,WorldMesh glass,WorldMesh nerves)
        {
            const int bays=18;
            for(int slab=0;slab<bays;slab++)
            {
                float depth=Mathf.Lerp(InnerGateDepth+.38f,ShaftDepth-.42f,slab/(bays-1f));
                for(int side=-1;side<=1;side+=2)
                {
                    Vector3 rim=GalleryPoint(depth,side*3.31f,.10f);
                    housing.Box(rim,new Vector3(.16f,.28f,1.21f),Quaternion.identity,WorldMesh.Shade(Bronze,.82f));
                    housing.Box(rim+new Vector3(side*.94f,.30f,0),new Vector3(.17f,.90f,1.21f),Quaternion.identity,Coal);
                    if(slab%3==0)
                    {
                        Vector3 wall=GalleryPoint(depth,side*4.62f,StoreyHeight*.48f);
                        housing.Box(wall+Vector3.right*side*.16f,new Vector3(.30f,StoreyHeight*.64f,1.77f),Quaternion.identity,
                            WorldMesh.Shade(Coal,.89f+.19f*Mathf.Sin(slab*1.24f)));
                        glass.Box(wall,new Vector3(.13f,2.78f,1.36f),Quaternion.identity,WorldMesh.Shade(PoolBlue,.52f));
                        housing.Box(wall+Vector3.up*1.47f,new Vector3(.25f,.22f,1.39f),Quaternion.identity,Iron);
                        housing.Box(wall+Vector3.down*1.43f,new Vector3(.25f,.25f,1.39f),Quaternion.identity,Bronze);
                        for(int rung=0;rung<4;rung++)
                            housing.Box(rim+new Vector3(side*(.21f+rung*.24f),-.08f,0),
                                new Vector3(.062f,.13f,1.03f),Quaternion.identity,Iron);
                    }
                    if(slab%3==1)
                    {
                        Vector3 pillar=GalleryPoint(depth,side*4.58f,StoreyHeight*.50f);
                        housing.Box(pillar,new Vector3(.53f,StoreyHeight,.46f),Quaternion.identity,Iron);
                        housing.Box(pillar+new Vector3(-side*.22f,2.17f,0),new Vector3(.32f,1.36f,.58f),
                            Quaternion.Euler(0,0,-side*17),WorldMesh.Shade(Bronze,.65f));
                        housing.Box(pillar+new Vector3(0,-2.65f,0),new Vector3(.69f,.51f,.66f),
                            Quaternion.identity,WorldMesh.Shade(Iron,.82f));
                        nerves.Box(rim+new Vector3(side*.03f,.151f,.18f),new Vector3(.12f,.040f,.42f),Quaternion.identity,
                            WorldMesh.Shade(Circuit,.70f+slab*.021f));
                    }
                }
                if(slab%3==1)
                {
                    Vector3 crown=GalleryPoint(depth,0,StoreyHeight+.091f);
                    housing.Box(crown,new Vector3(9.31f,.182f,.54f),Quaternion.identity,WorldMesh.Shade(Iron,.79f));
                    for(int cell=-1;cell<=1;cell++)
                        nerves.Box(crown+new Vector3(cell*1.93f,-.101f,0),new Vector3(1.76f,.023f,.26f),Quaternion.identity,
                            Color.Lerp(PoolBlue,Circuit,slab/(float)bays));
                }
            }
        }

        private static void BuildDescentFabric(WorldMesh housing, WorldMesh ribs)
        {
            const int spans=100;
            float Begin= ShaftDepth-.18f;
            float End= HeartDepth+1.1f;
            float Width(float depth) => 3.74f+.32f*Mathf.Sin((depth-ShaftDepth)*.24f)+
                .78f*Ease(HeartDepth-12,HeartDepth,depth);

            housing.Surface(18,spans,(across,along)=>{
                    float depth=Mathf.Lerp(Begin,End,along/(float)spans);
                    return MachinePoint(depth,(across/9f-1)*Width(depth),UpperSupport(depth)-.034f);
                },at=>{
                    float metal=.69f+.16f*Mathf.Pow(Mathf.Sin(at.z*3.18f),2)+.12f*Mathf.PerlinNoise(at.x*4,at.z*.95f);
                    var tint=Color.Lerp(Iron,new Color(.43f,.20f,.14f),Ease(HeartDepth+1,HeartDepth+23,at.z));
                    return WorldMesh.Shade(tint,metal);
                },null);

            housing.Surface(16,spans,(across,along)=>{
                    float depth=Mathf.Lerp(Begin,End,along/(float)spans);
                    float x=1-across/8f;
                    return MachinePoint(depth,x*Width(depth),UpperSupport(depth)+5.79f+.62f*(1-x*x));
                },at=>WorldMesh.Shade(Coal,.69f+.26f*Mathf.PerlinNoise(at.z*.47f,at.x*.78f)),null);

            for(int side=-1;side<=1;side+=2)
            {
                int wallSide=side;
                housing.Surface(9,spans,(across,along)=>{
                        float depth=Mathf.Lerp(Begin,End,along/(float)spans);
                        float y=(wallSide>0?across:9-across)/9f;
                        float wall=Width(depth)-.18f*Mathf.Sin(y*Mathf.PI);
                        return MachinePoint(depth,wall*wallSide,UpperSupport(depth)+y*5.89f);
                    },at=>WorldMesh.Shade(Iron,.46f+.29f*Mathf.PerlinNoise(at.y*.91f,at.z*.62f)),null);
                for(int strip=0;strip<18;strip++)
                {
                    float depth=ShaftDepth+strip*3.81f;
                    float clamping=Width(depth);
                    Vector3 anchor=MachinePoint(depth,side*(clamping-.15f),UpperSupport(depth)+.47f);
                    ribs.Box(anchor,new Vector3(.28f,.80f,.96f),Quaternion.Euler(0,0,side*12),
                        WorldMesh.Shade(Bronze,.48f+.22f*Mathf.Sin(strip*.66f)));
                    if(strip%3!=0) continue;
                    Vector3 pad=MachinePoint(depth,-side*.86f,UpperSupport(depth)+.052f);
                    ribs.Box(pad,new Vector3(.21f,.083f,1.17f),Quaternion.identity,WorldMesh.Shade(Iron,.95f));
                }
            }
        }

        private void BuildMachineHeart()
        {
            machineHeart=Group("Buried heart chamber",machineRoute);
            machineHeart.localPosition=MachinePoint(HeartDepth);
            var ribs=Geometry();
            var pipes=Geometry();
            var channels=Geometry();
            var valve=Geometry();
            var iris=Geometry();
            var textures=Geometry();
            var seat=Geometry();

            Vector3 core = new Vector3(0,2.43f,4.55f);
            for (int i=0;i<12;i++)
            {
                float z=-2.1f+i*.81f;
                Ring(ribs,new Vector3(0,2.08f,z),4.70f,.13f,WorldMesh.Shade(Iron,.62f+i*.027f),
                    Quaternion.identity,27,1);
                ribs.Box(new Vector3(0,-.19f,z),new Vector3(9.07f,.28f,.82f),Quaternion.identity,
                    WorldMesh.Shade(i%2==0?Iron:Coal,.54f+i*.045f));
            }
            ribs.Box(new Vector3(0,6.74f,2.53f),new Vector3(9.09f,.36f,9.32f),Quaternion.identity,
                WorldMesh.Shade(Coal,1.12f));
            ribs.Box(new Vector3(0,2.93f,8.46f),new Vector3(9.12f,7.22f,.29f),Quaternion.identity,
                WorldMesh.Shade(Coal,.93f));
            for (int side=-1;side<=1;side+=2)
            {
                Vector3 line = new Vector3(side*1.82f,2.68f,4.51f);
                Vector3 bank = new Vector3(side*4.04f,5.01f,2.75f);
                Vector3 spineEnd = new Vector3(side*3.12f,2.40f,-3.52f);
                Color flow= side<0 ? new Color(.39f,.14f,.14f) : new Color(.18f,.31f,.33f);
                pipes.Tube(new[]{core,core+new Vector3(side*.64f,1.05f,.22f),line,bank,
                        bank+new Vector3(side*.14f,.25f,-2.35f),spineEnd},
                    new[]{.29f,.47f,.42f,.36f,.32f,.23f},14,flow);
                channels.Tube(new[]{core+Vector3.back*.36f,line+Vector3.back*.28f,bank+Vector3.down*.22f,
                        bank+new Vector3(side*.09f,-.20f,-2.35f),spineEnd+Vector3.down*.18f},
                    new[]{.033f,.053f,.071f,.052f,.028f},7,WorldMesh.Shade(side<0?HeartAmber:Circuit,.60f));
                ribs.Box(new Vector3(side*4.59f,3.1f,2.88f),new Vector3(.22f,6.43f,9.39f),Quaternion.identity,Coal);
                for (int i=0;i<4;i++)
                {
                    float z=-.8f+i*2f;
                    Vector3 at=new Vector3(side*4.42f,.18f,z);
                    ribs.Box(at,new Vector3(.34f,.56f,.44f),Quaternion.identity,Iron);
                    channels.Box(at+new Vector3(-side*.198f,.09f,0),new Vector3(.035f,.18f,.35f),Quaternion.identity,
                        WorldMesh.Shade(Circuit,.61f));
                }
            }
            Vector3 aortaIn=core+new Vector3(.21f,1.10f,.27f);
            Vector3 aortaOut=new Vector3(-2.94f,5.27f,6.22f);
            pipes.Tube(new[]{aortaIn,core+new Vector3(.41f,2.02f,.22f),
                    core+new Vector3(-.22f,2.92f,.2f), core+new Vector3(-1.39f,3.32f,.39f),aortaOut},
                new[]{.52f,.45f,.43f,.48f,.39f},17,new Color(.54f,.16f,.15f));
            for (int cuff=0;cuff<3;cuff++)
            {
                float side=cuff*.57f;
                Vector3 at=core+new Vector3(-.82f-side,3.04f+.16f*Mathf.Sin(side*3),.45f+side*.21f);
                Ring(ribs,at,.48f,.052f,Bronze,Quaternion.Euler(0,62,16),22,1);
            }
            for(int i=0;i<CablePorts.Length;i++)
            {
                Vector3 port=CablePorts[i];
                Vector3 side=i==2?Vector3.up:Vector3.right*Mathf.Sign(port.x);
                pipes.Tube(new[]{port+side*.65f+Vector3.forward*.95f,port+side*.31f+Vector3.forward*.26f,port},
                    new[]{.29f,.26f,.24f},11,Bronze);
                Ring(channels,port+Vector3.back*.066f,.172f,.036f,WorldMesh.Shade(HeartAmber,.61f),Quaternion.identity,16,1);
            }

            BuildVentricles(valve,textures);
            heartCore=Group("Asymmetric pump chambers",machineHeart);
            heartCore.localPosition=core;
            MachinePart("Heart-shaped ventricle shells",valve,stone,heartCore);
            heartGlow=MachinePart("Flow through the heart-shaped chambers",textures,glow,heartCore).GetComponent<MeshRenderer>();
            leftHeartValve=BuildHeartValve(heartCore,new Vector3(-.78f,.63f,-.91f),new Color(.37f,.10f,.095f));
            rightHeartValve=BuildHeartValve(heartCore,new Vector3(.74f,.48f,-.90f),new Color(.16f,.26f,.30f));
            heartTendon=Lamp("Receiving light at the ventricular seam",new Vector3(0,.14f,-1.12f),.25f,HeartAmber,0).transform;
            heartTendon.SetParent(heartCore,false);

            Ring(seat,Vector3.up*.11f,.57f,.091f,WorldMesh.Shade(Bronze,.70f),Quaternion.Euler(90,0,0),26,1);
            for(int tooth=0;tooth<8;tooth++)
            {
                float angle=tooth*45f;
                Quaternion yaw=Quaternion.Euler(0,angle,0);
                Vector3 at=yaw*new Vector3(0,.144f,.54f);
                seat.Box(at,new Vector3(.16f,.095f,.38f),yaw*Quaternion.Euler(-22,0,0),
                    WorldMesh.Shade(Bronze,.83f+.071f*tooth));
            }
            heartSeat=MachinePart("Circular seed induction plate",seat,stone,machineHeart);
            var seedPulse=Geometry();
            Ring(seedPulse,Vector3.zero,.43f,.026f,Circuit,Quaternion.Euler(90,0,0),26,1);
            heartSeatLight=MachinePart("Charge ring on the induction plate",seedPulse,glow,heartSeat).GetComponent<MeshRenderer>();
            heartSeatLight.transform.localPosition=Vector3.up*.138f;
            for(int rail=0;rail<2;rail++)
            {
                float side=rail==0?-1:1;
                var trace=new Vector3[heartCurrent.Length];
                var radius=new float[trace.Length];
                for(int i=0;i<trace.Length;i++)
                {
                    float link=i/(trace.Length-1f);
                    trace[i]=new Vector3(side*Mathf.Sin(link*Mathf.PI)*.64f,.17f+link*2.51f,link*3.46f);
                    radius[i]=.027f+.013f*link;
                    if(rail!=0) continue;
                    heartCurrent[i]=Lamp("Seed current into the heart",trace[i]+Vector3.up*.046f,.083f,HeartAmber,0);
                    heartCurrent[i].transform.SetParent(machineHeart,false);
                }
                pipes.Tube(trace,radius,8,Bronze);
            }
            heartSeed=Lamp("The light seed entering the heart",Vector3.up*.28f,.17f,HeartAmber,0).transform;
            heartSeed.SetParent(machineHeart,false);

            for(int i=0;i<5;i++)
            {
                float y=(i-2)*.54f;
                iris.Box(new Vector3(.52f+Mathf.Abs(y)*.09f,y,.046f*Mathf.Sin(i)),new Vector3(1.58f,.49f,.29f),
                    Quaternion.Euler(0,0,18f*Mathf.Sin(i)),WorldMesh.Shade(i%2==0?Iron:Bronze,.88f+Mathf.Sin(i)*.13f));
            }
            Mesh blade=iris.Finish("Five overlapping heart shutters");
            irisLeft=Piece("Left mechanical valve",blade,stone,machineHeart,Vector3.zero);
            irisRight=Piece("Right mechanical valve",blade,stone,machineHeart,Vector3.zero);
            irisLeft.localScale=new Vector3(-1,1,1);
            MachinePart("Buried ribs and access deck",ribs,stone,machineHeart);
            MachinePart("Vessels and cable sleeves",pipes,stone,machineHeart);
            MachinePart("Return circuit and ports",channels,glow,machineHeart);

            heartCable=new Transform[42];
            heartCableInner=new Transform[42];
            for (int i=0;i<heartCable.Length;i++)
            {
                heartCable[i]=Piece("Rubber binding",machineBar,stone,machineHeart,Vector3.zero);
                heartCableInner[i]=Piece("Exposed wire edge",machineBar,glow,machineHeart,Vector3.zero);
                Light(heartCable[i].GetComponent<MeshRenderer>(),Coal,0);
                Light(heartCableInner[i].GetComponent<MeshRenderer>(),HeartAmber,.56f);
            }
        }

        private void AnimateMachineInterior(Journey journey,float time,Vector3 actor)
        {
            bool handoff=journey.Phase==Stage.ForestTransition && journey.ForestTrap==ForestTrapKind.Cable;
            float t=handoff?journey.Progress:0;
            for(int i=0;i<returnPulses.Length;i++)
            {
                float peak=.203f+.265f*i/(returnPulses.Length-1f);
                float current=handoff?Mathf.Exp(-Mathf.Pow((t-peak)/.038f,2)):0;
                returnPulses[i].gameObject.SetActive(current>.023f);
                if(current>.023f) Light(returnPulses[i],HeartAmber,current*2.68f);
            }
            for(int i=0;i<brainPulses.Length;i++)
            {
                float ripple=.5f+.5f*Mathf.Sin(time*4.2f-i*.74f);
                float travel=(brainPulses.Length-1-i)/(brainPulses.Length-1f);
                float kick=handoff?Mathf.Exp(-Mathf.Pow((t-(.212f+travel*.252f))/.042f,2)):0;
                Color roomTone=Color.Lerp(Circuit,HeartAmber,Ease(0,6,7-travel*21));
                Light(brainPulses[i],Color.Lerp(roomTone,HeartAmber,kick),.27f+.54f*ripple+2.26f*kick);
            }
            if (!machineHeart.gameObject.activeSelf) return;

            float beat=handoff?Mathf.Exp(-Mathf.Pow((t-.21f)/.047f,2)):0;
            float afterBeat=handoff?.38f*Ease(.24f,.35f,t)*(1-Ease(.37f,.48f,t)):0;
            float stutter=handoff?.79f*Mathf.Exp(-Mathf.Pow((t-.514f)/.039f,2)):0;
            float breath=.5f+.5f*Mathf.Sin(time*1.48f);
            float ignition=Mathf.Max(journey.ForestPlantCharge,beat+afterBeat+stutter);
            float expansion=beat+stutter;
            heartCore.localScale=new Vector3(1+.092f*expansion,1+.128f*expansion,1+.106f*expansion);
            heartCore.localRotation=Quaternion.Euler(2*stutter,4.5f*stutter,2.9f*stutter);
            leftHeartValve.localRotation=Quaternion.Euler(0,-11f*beat-23f*stutter,16f*expansion);
            rightHeartValve.localRotation=Quaternion.Euler(0,16f*beat+23f*stutter,-13f*expansion);
            Light(heartGlow,Color.Lerp(new Color(.58f,.18f,.14f),HeartAmber,Mathf.Clamp01(ignition)),.20f+.10f*breath+2.38f*ignition);
            Light(heartSeatLight,Color.Lerp(Circuit,HeartAmber,Mathf.Clamp01(ignition)),.15f+1.1f*ignition);
            heartSeatLight.transform.localScale=Vector3.one*(1+.24f*beat+.08f*stutter);
            AnimateHeartCurrent(handoff,t,ignition,journey.ForestPlantCharge,time);
            float shutters=Ease(.87f,.99f,t);
            irisLeft.localPosition=new Vector3(Mathf.Lerp(-2.61f,-.18f,shutters),1.29f,3.58f);
            irisRight.localPosition=new Vector3(Mathf.Lerp(2.61f,.18f,shutters),1.29f,3.58f);
            irisLeft.localRotation=Quaternion.Euler(0,20*(1-shutters),8*shutters);
            irisRight.localRotation=Quaternion.Euler(0,-20*(1-shutters),-8*shutters);

            float reach=Ease(.56f,.72f,t);
            bool show=handoff && reach>.002f;
            for(int i=0;i<heartCable.Length;i++)
            {
                heartCable[i].gameObject.SetActive(show);
                heartCableInner[i].gameObject.SetActive(show && i%3!=1);
            }
            if(!show) return;

            Vector3 body=machineHeart.InverseTransformPoint(actor);
            Vector3 port=CablePorts[Mathf.Clamp(journey.ForestCableAnchor,0,CablePorts.Length-1)];
            float coil=Ease(.67f,.85f,t);
            int links=heartCable.Length/3;
            for(int strand=0;strand<3;strand++)
            {
                Vector3 start=port+new Vector3((strand-1)*.12f,(strand-1)*.087f,.021f*strand);
                Vector3 end=Vector3.Lerp(start,body+new Vector3((strand-1)*.30f,(1-strand)*.28f,-.04f),reach);
                Vector3 previous=start;
                for(int link=0;link<links;link++)
                {
                    float unit=(link+1f)/links;
                    Vector3 target=Vector3.Lerp(start,end,unit);
                    float sweep=Mathf.Sin(unit*Mathf.PI);
                    target+=Vector3.down*sweep*(.19f+.39f*(1-coil))*reach;
                    target.z+=sweep*.58f*(1-coil);
                    float winding=Ease(.55f,1,unit)*coil;
                    float turn=unit*15.0f+strand*2.398f-time*2.8f;
                    target+=new Vector3(Mathf.Cos(turn)*.29f,Mathf.Sin(turn*.5f)*.098f,Mathf.Sin(turn)*.25f)*winding;
                    int index = strand*links+link;
                    float thickness=.068f*(1-.18f*unit)*Mathf.Lerp(.18f,1f,reach);
                    Link(heartCable[index],previous,target,thickness);
                    Vector3 edge=new Vector3(.017f,.029f,-.029f);
                    Link(heartCableInner[index],previous+edge,target+edge,thickness*.30f);
                    previous=target;
                }
            }
        }

        private void AnimateHeartCurrent(bool handoff,float t,float ignition,float seedCharge,float time)
        {
            float stem=Ease(.006f,.197f,t);
            float visible=handoff?(1-Ease(.169f,.215f,t)):0;
            heartSeed.gameObject.SetActive(visible>.002f);
            if(visible>.002f)
            {
                float bloom=Ease(.015f,.084f,t)*(1-Ease(.15f,.205f,t));
                heartSeed.localPosition=new Vector3(-.48f*Mathf.Sin(stem*Mathf.PI),.23f+2.44f*stem,3.52f*stem);
                heartSeed.localScale=Vector3.one*(.12f+.07f*bloom)*Mathf.Lerp(.2f,1,visible);
                Light(heartSeed.GetComponent<MeshRenderer>(),HeartAmber,visible*(.54f+1.10f*bloom));
            }
            for(int i=0;i<heartCurrent.Length;i++)
            {
                float rail=i/(heartCurrent.Length-1f);
                float peak=.061f+.145f*rail;
                float wave=handoff?Mathf.Exp(-Mathf.Pow((t-peak)/.019f,2)):0;
                float primed=seedCharge*(1-Ease(.05f,.78f,rail))*(.10f+.08f*Mathf.Sin(time*7-i*.6f));
                Light(heartCurrent[i],HeartAmber,.018f+primed+wave*1.63f);
            }
            float sync=handoff?Ease(.15f,.207f,t):0;
            Light(heartTendon.GetComponent<MeshRenderer>(),HeartAmber,sync*(.07f+1.80f*ignition));
            heartTendon.localScale=Vector3.one*(.11f+.14f*ignition);
        }

        private static Vector2 HeartOutline(float angle)
        {
            float sine=Mathf.Sin(angle);
            float x=1.85f*sine*sine*sine;
            float y=.118f*(13*Mathf.Cos(angle)-5*Mathf.Cos(2*angle)-2*Mathf.Cos(3*angle)-Mathf.Cos(4*angle)+2.5f);
            return new Vector2(x,y);
        }

        private static void BuildVentricles(WorldMesh shells,WorldMesh veins)
        {
            shells.Surface(70,24,(around,row)=>{
                    float theta=around/70f*Mathf.PI*2;
                    float polar=.035f+row/24f*(Mathf.PI-.07f);
                    Vector2 outline=HeartOutline(theta);
                    float radius=Mathf.Sin(polar);
                    float seam=.035f*Mathf.Sin(theta*12)*Mathf.Sin(polar*9);
                    return new Vector3(outline.x*radius+.12f*radius*(outline.y+.15f),outline.y*radius,
                        -1.08f*Mathf.Cos(polar)+seam);
                },point=>{
                    float grain=Mathf.PerlinNoise(point.x*4.2f+13,point.y*5.2f+point.z*2);
                    float hinge=Mathf.Pow(Mathf.Abs(Mathf.Sin(point.y*11-point.x*2)),4);
                    var copper=Color.Lerp(new Color(.28f,.073f,.085f),new Color(.61f,.175f,.14f),
                        Mathf.Clamp01(.24f+.58f*grain+.12f*hinge));
                    return WorldMesh.Shade(copper,.86f+.22f*grain);
                },null);
            var seam=new Vector3[51];
            var widths=new float[seam.Length];
            for(int i=0;i<seam.Length;i++)
            {
                float turn=i/(seam.Length-1f)*Mathf.PI*2;
                Vector2 shell=HeartOutline(turn);
                seam[i]=new Vector3(shell.x*.993f+.108f*(shell.y+.15f),shell.y*.993f,-.174f);
                widths[i]=.022f+.009f*Mathf.Pow(Mathf.Sin(turn*12),2);
            }
            shells.Tube(seam,widths,8,new Color(.57f,.245f,.164f));
            for(int side=-1;side<=1;side+=2)
            for(int trace=0;trace<3;trace++)
            {
                float y=.75f+.11f*trace;
                float x=side*(.21f+trace*.13f);
                var paths=new[]{new Vector3(-.04f,.16f,-1.115f),new Vector3(x,.42f,-1.07f),
                    new Vector3(x+side*.29f,y,-.96f),new Vector3(side*(.70f+trace*.18f),y+.28f,-.83f),
                    new Vector3(side*(1.03f+trace*.15f),y+.18f,-.66f)};
                veins.Tube(paths,new[]{.024f,.023f,.019f,.017f,.008f},7,WorldMesh.Shade(HeartAmber,.67f+.08f*trace));
            }
            veins.Tube(new[]{new Vector3(-.035f,.30f,-1.12f),new Vector3(.007f,-.17f,-1.07f),
                    new Vector3(.21f,-.89f,-.94f),new Vector3(.05f,-1.57f,-.52f)},
                new[]{.036f,.033f,.029f,.010f},9,new Color(1,.36f,.19f));
        }

        private Transform BuildHeartValve(Transform parent,Vector3 at,Color tone)
        {
            var gear=Geometry();
            Ring(gear,Vector3.zero,.36f,.048f,Bronze,Quaternion.identity,24,1);
            for(int blade=0;blade<6;blade++)
            {
                Quaternion rotary=Quaternion.Euler(0,0,60*blade);
                gear.Box(rotary*new Vector3(0,.13f,-.020f),new Vector3(.076f,.38f,.092f),
                    rotary*Quaternion.Euler(0,0,17),WorldMesh.Shade(tone,.81f+.11f*blade));
            }
            Transform flap=MachinePart("Driven leaf valve",gear,stone,parent);
            flap.localPosition=at;
            return flap;
        }

        private static void Link(Transform node,Vector3 from,Vector3 to,float radius)
        {
            Vector3 delta=to-from;
            node.localPosition=(from+to)*.5f;
            if (delta.sqrMagnitude>.0001f) node.localRotation=Quaternion.FromToRotation(Vector3.up,delta);
            node.localScale=new Vector3(radius,Mathf.Max(.002f,delta.magnitude),radius);
        }

        private static void Cog(WorldMesh mesh,Vector3 at,float radius,float thickness,int teeth,Quaternion rotation,Color metal)
        {
            for (int i=0;i<teeth;i++)
            {
                float angle=i*360f/teeth;
                Vector3 tooth=rotation*Quaternion.Euler(0,0,angle)*Vector3.up*radius;
                mesh.Box(at+tooth,new Vector3(radius*.31f,radius*.24f,thickness*.98f),
                    rotation*Quaternion.Euler(0,0,angle),WorldMesh.Shade(metal,.79f+.18f*Mathf.Sin(i*2.4f)));
            }
            Ring(mesh,at,radius*.79f,radius*.171f,metal,rotation,teeth*2,1);
            Ring(mesh,at,radius*.26f,radius*.104f,WorldMesh.Shade(Coal,1.20f),rotation,teeth*2,1);
            for (int i=0;i<5;i++)
            {
                float angle=i*72f;
                Quaternion spoke=rotation*Quaternion.Euler(0,0,angle);
                mesh.Box(at+(spoke*Vector3.up)*radius*.55f,new Vector3(radius*.16f,radius*.69f,thickness*.62f),
                    spoke,WorldMesh.Shade(metal,.73f+i*.058f));
            }
        }

        public void DisposeMachine()
        {
            if (machineGlass!=null) Object.Destroy(machineGlass);
            if (machinePool!=null) Object.Destroy(machinePool);
            if (machineSign!=null) Object.Destroy(machineSign);
        }
    }
}
