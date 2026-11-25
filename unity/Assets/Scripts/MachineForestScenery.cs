using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private const float MachineCenter = 11, DoorDepth = 179, HeartDepth = 208;
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
        private Transform leftCrusher, rightCrusher, crusherLight, poolSurface, irisLeft, irisRight, heartCore;
        private Transform[] heartCable, heartCableInner;
        private MeshRenderer poolFilm, heartGlow;
        private Material machineGlass, machineSign;
        private Mesh machineBar, machineSphere, machineRobotShell;
        private readonly Recycler[] recyclers = new Recycler[9];
        private readonly Prediction[] predictions = new Prediction[15];
        private readonly MeshRenderer[] brainPulses = new MeshRenderer[21];
        private readonly MeshRenderer[] returnPulses = new MeshRenderer[24];
        private readonly Transform[] threshold = new Transform[3], crashPanel = new Transform[3];
        private readonly Vector3[] doorFrameCentres = new Vector3[3];

        public static float MachineFloor(float depth) =>
            -3.70f * Mathf.SmoothStep(0,1,Mathf.InverseLerp(194,HeartDepth,depth));

        public static Vector3 MachineDoorActor(Vector3 start,int door,float progress)
        {
            float drop = Ease(.04f,.53f,progress);
            float travel = Ease(.02f,.69f,progress);
            Vector3 chamber = MachinePoint(DoorDepth,(door-1)*LaneWidth,-2.06f);
            start.x = Mathf.Lerp(start.x,chamber.x,drop*.92f);
            start.y = Mathf.Lerp(start.y,chamber.y,drop);
            start.z += 3.05f*travel;
            if (door == 0) return start + Vector3.down*(.16f*Ease(.43f,.84f,progress));
            if (door == 1)
                return start + new Vector3(.23f*Mathf.Sin(progress*19)*Ease(.24f,.56f,progress),
                    -.68f*Ease(.7f,.98f,progress),.86f*Ease(.61f,.91f,progress));
            return start + Vector3.down*(1.35f*Ease(.51f,1,progress));
        }

        public static Vector3 MachineCableActor(Vector3 start,float progress,int port)
        {
            Vector3 origin = CablePorts[Mathf.Clamp(port,0,CablePorts.Length-1)];
            float pull = Ease(.52f,.98f,progress);
            float catchUp = Mathf.Sin(Ease(.44f,.91f,progress)*Mathf.PI);
            return start + new Vector3(origin.x*.22f*catchUp,
                .25f*catchUp-2.52f*Ease(.64f,1,progress),3.22f*pull);
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
            bool crossed = journey.Branch == ForestBranch.Right && depth > DoorDepth && !inDoor;
            machineBrain.gameObject.SetActive(crossed);
            machineHeart.gameObject.SetActive(crossed && depth >= 187);

            machineGearRoom.gameObject.SetActive(inDoor && journey.ForestDoor == 0);
            machineRobotRoom.gameObject.SetActive(inDoor && journey.ForestDoor == 1);
            machinePoolRoom.gameObject.SetActive(inDoor && journey.ForestDoor == 2);
            for(int i=0;i<3;i++)
            {
                float open=inDoor && journey.ForestDoor==i ? Ease(.024f,.31f,journey.Progress) : 0;
                threshold[i].localPosition=doorFrameCentres[i]+new Vector3(0,-.65f*open,-.55f*open);
                threshold[i].localRotation=Quaternion.Euler(-70*open,0,0);
                crashPanel[i].localPosition=doorFrameCentres[i]+Vector3.up*(3.87f*open);
            }
            if (inDoor) AnimateMachineDoor(journey,time,actor);
            if (crossed) AnimateMachineInterior(journey,time,actor);
        }

        private static Vector3 MachinePoint(float depth,float x = 0,float y = 0)
        {
            float worldX = MachineCenter + x, z = depth + 25;
            return new Vector3(worldX,Ground(worldX,z)+MachineFloor(depth)+y,z);
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
                    slabs.Box(at+new Vector3(side*.94f,1.43f,0),new Vector3(.35f,2.86f,.74f),
                        Quaternion.Euler(0,0,side*2),Iron);
                    slabs.Box(at+new Vector3(side*.91f,2.89f,0),new Vector3(.39f,.41f,.68f),
                        Quaternion.Euler(0,0,side*31),Bronze);
                    slabs.Box(at+new Vector3(side*.88f,.19f,-.31f),new Vector3(.43f,.37f,.24f),
                        Quaternion.identity,Coal);
                    seam.Box(at+new Vector3(side*.93f,1.72f,-.398f),new Vector3(.043f,1.08f,.03f),
                        Quaternion.identity,WorldMesh.Shade(Circuit,.28f));
                }
                slabs.Box(at+new Vector3(0,3.18f,0),new Vector3(1.79f,.54f,.78f),Quaternion.identity,Iron);
                var panel=Geometry();
                panel.Box(new Vector3(0,1.56f,2.16f),new Vector3(1.75f,3.32f,.14f),
                    Quaternion.identity,new Color(.022f,.030f,.042f));
                crashPanel[door]=MachinePart("Recess behind portal",panel,stone,machineRoute);
                Cog(slabs,at+new Vector3(0,3.19f,-.49f),.265f,.13f,9,Quaternion.identity,Bronze);
                var sill=Geometry();
                sill.Box(new Vector3(0,.015f,-.29f),new Vector3(1.68f,.068f,.28f),Quaternion.identity,Bronze);
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
                    rim[i]=baseline+new Vector3(Mathf.Cos(theta)*5.81f,Mathf.Sin(theta)*7.52f,0);
                    widths[i]=.19f+.10f*Mathf.Sin(theta);
                    if(i==0 || i+1==rim.Length) continue;
                    Vector3 rib=baseline+new Vector3(Mathf.Cos(theta)*6.17f,Mathf.Sin(theta)*7.97f,.31f);
                    rests.Box(rib,new Vector3(.72f,1.08f,1.09f),Quaternion.Euler(0,0,21-theta*Mathf.Rad2Deg),
                        WorldMesh.Shade(Iron,.71f+.20f*Mathf.Sin(i*1.33f+arc)));
                    if(arc==2) continue;
                    Vector3 before=rib-Vector3.forward*.58f, after=rib+Vector3.forward*2.22f;
                    jacket.Tube(new[]{before,rib+Vector3.forward*.51f,after},new[]{.13f,.17f,.14f},8,Coal);
                }
                jacket.Tube(rim,widths,12,WorldMesh.Shade(Bronze,.84f));
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
            var chute = Geometry();
            Ring(chute,Vector3.up*.04f+Vector3.forward*.81f,1.10f,.116f,Bronze,Quaternion.Euler(90,0,0),23,1);
            chute.Box(new Vector3(0,-.13f,.83f),new Vector3(1.9f,.16f,1.9f),Quaternion.identity,Coal);
            MachinePart("Recycler hopper",chute,stone,machineRobotRoom);
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

            poolSurface = Piece("Predictive liquid",machineSphere,machineGlass??glow,machinePoolRoom,Vector3.zero);
            poolSurface.localScale=new Vector3(2.45f,.13f,2.78f);
            poolFilm=poolSurface.GetComponent<MeshRenderer>();
            for (int i=0;i<predictions.Length;i++) predictions[i]=BuildPrediction(i);
        }

        private Transform MachineRoom(string name,int door)
        {
            Transform room = Group(name,machineRoute);
            room.localPosition = MachinePoint(DoorDepth,(door-1)*LaneWidth,-3.10f)+Vector3.forward*3.13f;
            var structure = Geometry();
            var fixtures = Geometry();
            structure.Box(new Vector3(0,-.27f,.30f),new Vector3(5.48f,.35f,6.55f),Quaternion.identity,Coal);
            structure.Box(new Vector3(0,1.13f,3.33f),new Vector3(5.48f,3.42f,.25f),Quaternion.identity,
                WorldMesh.Shade(Iron,.60f));
            for (int side=-1;side<=1;side+=2)
            {
                structure.Box(new Vector3(side*2.68f,1.1f,.41f),new Vector3(.19f,3.33f,5.85f),
                    Quaternion.identity,WorldMesh.Shade(Iron,.59f));
                for (int i=0;i<5;i++)
                {
                    Vector3 at = new Vector3(side*2.57f,.28f+i*.41f,3.09f-i*.12f);
                    fixtures.Box(at,new Vector3(.095f,.093f,.80f),Quaternion.identity,door==2?PoolBlue:Circuit);
                    structure.Box(at+Vector3.forward*.072f,new Vector3(.15f,.16f,.93f),Quaternion.identity,Coal);
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
                float press = Ease(.42f,.83f,t);
                float width = Mathf.Lerp(1.46f,.94f,press);
                float angle = time*77+197*press;
                leftCrusher.localPosition = new Vector3(-width,1.03f,.38f);
                rightCrusher.localPosition = new Vector3(width,1.13f,.39f);
                leftCrusher.localRotation = Quaternion.Euler(9,0,angle);
                rightCrusher.localRotation = Quaternion.Euler(-11,0,-angle-15);
                crusherLight.localPosition = new Vector3(0,1.0f,-.05f);
                crusherLight.localScale = new Vector3(.021f,.67f,.067f)*Ease(.66f,.77f,t)*(1-.72f*Ease(.84f,1,t));
                return;
            }
            if (journey.ForestDoor == 1)
            {
                for (int i=0;i<recyclers.Length;i++)
                {
                    Recycler unit=recyclers[i];
                    float phase=i*Mathf.PI*2/recyclers.Length;
                    float creep=Ease(.22f+i*.014f,.66f+i*.019f,t);
                    float radius=Mathf.Lerp(2.40f,.92f,creep);
                    var from=new Vector3(Mathf.Sin(phase)*radius,.038f,Mathf.Cos(phase)*radius+1.17f);
                    from.y+= .035f*Mathf.Sin(time*12.7f+i*1.2f);
                    unit.Root.localPosition=from;
                    Vector3 target=machineRobotRoom.InverseTransformPoint(actor);
                    Vector3 toward=target-from;
                    toward.y=0;
                    unit.Root.localRotation=Quaternion.LookRotation(toward.sqrMagnitude>.003f?-toward:Vector3.back,Vector3.up);
                    Vector3 shoulder=from+Vector3.up*.55f;
                    Vector3 hand=Vector3.Lerp(shoulder,target+Vector3.up*(.12f+.15f*Mathf.Sin(i*1.3f)),creep*.71f);
                    Vector3 elbow=Vector3.Lerp(shoulder,hand,.58f)+Vector3.up*(.16f+.20f*creep);
                    Link(unit.Upper,shoulder,elbow,.058f);
                    Link(unit.Lower,elbow,hand,.042f);
                    unit.Grip.localPosition=hand;
                    unit.Grip.localScale=new Vector3(.12f,.072f,.080f);
                    Light(unit.Eye,Color.Lerp(Circuit,Red,creep),.41f+creep*.91f);
                }
                return;
            }

            float water = Ease(.30f,.95f,t);
            poolSurface.localPosition=new Vector3(0,1.51f*water-.18f,.74f);
            poolSurface.localScale=new Vector3(2.47f,.096f+.082f*water,2.53f);
            Light(poolFilm,PoolBlue,.29f+1.11f*water);
            float latest = journey.ForestEchoLane(0), prior = journey.ForestEchoLane(.55f);
            bool predicts = t>=.51f;
            for(int i=0;i<predictions.Length;i++)
            {
                Prediction echo=predictions[i];
                float active=Ease(.16f+i*.036f,.31f+i*.035f,t);
                echo.Root.gameObject.SetActive(active>.012f);
                if (active<=.012f) continue;
                float a=i*2.39996f;
                float radius= i<5 ? .64f+i*.34f: 1.65f+(i%3)*.27f;
                float imitation = predicts ? Mathf.Clamp(latest+(latest-prior)*.93f,-1,1) : journey.ForestEchoLane(.19f+i*.091f);
                bool fist = journey.ForestEchoHand(predicts?0:.25f+i*.081f)==Hand.Fist;
                float skew = Mathf.Sin(time*2.4f+i)*.24f;
                echo.Root.localPosition = new Vector3(Mathf.Sin(a)*radius+.36f*imitation,
                    -1.07f+active*1.06f+water*.40f+.071f*Mathf.Sin(time*4f+i),.81f+Mathf.Cos(a)*radius+skew);
                echo.Root.localScale=Vector3.one*(.55f+active*.49f);
                echo.Root.localRotation=Quaternion.Euler(0,imitation*36,imitation*-11);
                Color tint=Color.Lerp(PoolBlue,Circuit,Mathf.Repeat(i*.37f,1));
                Light(echo.Body,tint,.72f+water*.47f+skew);
                Vector3 left = new Vector3(-.17f,.77f,0), right = new Vector3(.17f,.77f,0);
                float raise = fist ? .14f : .42f+.09f*Mathf.Sin(time*7+i);
                Link(echo.LeftArm,left,new Vector3(-raise,.56f+raise,.08f),.071f);
                Link(echo.RightArm,right,new Vector3(raise,.56f+raise,-.07f),.071f);
                Light(echo.LeftLight,tint,.75f);
                Light(echo.RightLight,tint,.75f);
            }
        }

        private void BuildMachineBrain()
        {
            machineBrain = Group("Two hemispheres and descending spine",machineRoute);
            var housing = Geometry();
            var glass = Geometry();
            var nerves = Geometry();
            var spine = Geometry();
            var sheath=new Vector3[returnPulses.Length];
            var widths=new float[sheath.Length];
            for(int i=0;i<sheath.Length;i++)
            {
                float depth=211.72f-i*1.255f;
                float back=Ease(0,1,i/(sheath.Length-1f));
                float height=1.89f+back*4.82f;
                float side=Mathf.Sin(Mathf.PI*Mathf.Pow(back,.75f))*3.91f+.22f;
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
                    MachinePoint(184.8f,side*3.22f,6.27f)},new[]{.13f,.16f,.20f},11,Bronze);
            }
            for (int side=-1;side<=1;side+=2)
            {
                for (int lobe=0;lobe<6;lobe++)
                {
                    float depth=181+lobe*1.95f;
                    float crest=5.83f+.65f*Mathf.Sin(lobe*.70f);
                    Vector3 basePosition=MachinePoint(depth,side*(3.18f+Mathf.Sin(lobe*.9f)*.31f),crest);
                    glass.Rock(basePosition,new Vector3(1.14f,1.09f,1.48f),
                        WorldMesh.Shade(lobe%2==0?PoolBlue:Circuit,.95f),lobe*.13f+side*.05f,10,14,.05f);
                    for (int groove=0;groove<3;groove++)
                    {
                        var sweep = new Vector3[15];
                        var width = new float[sweep.Length];
                        for (int p=0;p<sweep.Length;p++)
                        {
                            float angle = (p/(sweep.Length-1f)*1.25f+.23f*groove)*Mathf.PI;
                            sweep[p]=basePosition+new Vector3(Mathf.Cos(angle)*(1.14f+.1f*Mathf.Sin(p*.8f)),
                                Mathf.Sin(angle)*1.12f + .067f*Mathf.Sin(p*.95f+groove),-.72f+groove*.63f+Mathf.Sin(angle*2)*.34f);
                            width[p]=.013f+groove*.004f+.005f*Mathf.Sin(angle);
                        }
                        nerves.Tube(sweep,width,7,WorldMesh.Shade(Circuit,.58f+.15f*groove));
                    }
                    Vector3 mounting=MachinePoint(depth,side*4.64f,1.12f);
                    housing.Tube(new[]{mounting,mounting+new Vector3(0,3.17f,-.32f),basePosition+new Vector3(side*.72f,0,0)},
                        new[]{.19f,.17f,.29f},10,Iron);
                    housing.Box(mounting,new Vector3(.69f,2.76f,.73f),Quaternion.identity,Coal);
                }
                for (int lane=-1;lane<=1;lane++)
                {
                    Vector3 fil=MachinePoint(188.5f,side*3.05f,6.2f+lane*.35f);
                    Vector3 outlet=MachinePoint(181,lane*LaneWidth,3.53f);
                    Vector3 throat=MachinePoint(183.5f,side*(1.5f+lane*.21f),4.9f);
                    nerves.Tube(new[]{fil,MachinePoint(185.8f,side*2.70f+lane*.38f,5.48f),throat,outlet},
                        new[]{.032f,.052f,.032f,.024f},7,Color.Lerp(PoolBlue,Circuit,(lane+1)*.38f));
                    Ring(housing,outlet,.21f,.041f,Bronze,Quaternion.Euler(84,0,0),13,1);
                }
                for (int ring=0;ring<14;ring++)
                {
                    float depth=194+ring*.98f;
                    Vector3 at=MachinePoint(depth,side*3.44f,2.18f);
                    housing.Box(at,new Vector3(.35f,4.4f,.31f),Quaternion.Euler(0,0,side*17),Iron);
                    Vector3 lower=MachinePoint(depth,side*2.52f,-.12f);
                    Vector3 upper=MachinePoint(depth,side*2.52f,4.15f);
                    Vector3 inner=MachinePoint(depth,side*1.87f,4.65f);
                    spine.Tube(new[]{lower,at,upper,inner},new[]{.13f,.17f,.14f,.082f},8,Bronze);
                    if (ring==13) continue;
                    Vector3 wire=MachinePoint(depth,side*(2.67f+.22f*Mathf.Sin(ring*.67f)),1.72f+.29f*Mathf.Sin(ring));
                    Vector3 wireNext=MachinePoint(depth+.98f,side*(2.67f+.22f*Mathf.Sin((ring+1)*.67f)),1.72f+.29f*Mathf.Sin(ring+1));
                    nerves.Tube(new[]{wire,wireNext},new[]{.028f,.028f},6,WorldMesh.Shade(Circuit,.61f));
                    housing.Tube(new[]{wire+Vector3.down*.20f,wireNext+Vector3.down*.20f},
                        new[]{.072f,.072f},7,WorldMesh.Shade(Iron,.61f));
                }
            }

            for (int i=0;i<19;i++)
            {
                float depth=190.2f+i*1.02f;
                if (i==18) depth=HeartDepth;
                float pitch=Mathf.Atan2(MachineFloor(depth+.5f)-MachineFloor(depth-.5f),1)*Mathf.Rad2Deg;
                housing.Box(MachinePoint(depth,0,-.04f),new Vector3(7.42f,.12f,1.13f),Quaternion.Euler(-pitch,0,0),
                    WorldMesh.Shade(i%3==0?Iron:Coal,.82f+.14f*Mathf.Sin(i*2.0f)));
            }

            MachinePart("Hemispheres worn glass",glass,machineGlass??glow,machineBrain);
            MachinePart("Wiring to the three false exits",nerves,glow,machineBrain);
            MachinePart("Jointed spine liner",spine,stone,machineBrain);
            MachinePart("Machine chambers and sloping deck",housing,stone,machineBrain);
            for(int i=0;i<brainPulses.Length;i++)
            {
                float depth=181+i*1.33f;
                float side=i%2==0?-1:1;
                float y=depth<194?5.17f+.42f*Mathf.Sin(i*.9f):1.66f+.19f*Mathf.Sin(i);
                float width=depth<194?2.05f:2.67f;
                brainPulses[i]=Lamp("Impulse in the spine",MachinePoint(depth,side*width,y),
                    .083f,Circuit,.36f);
                brainPulses[i].transform.SetParent(machineBrain,false);
            }
        }

        private void BuildMachineHeart()
        {
            machineHeart=Group("Still heart and descending pelvic passage",machineRoute);
            machineHeart.localPosition=MachinePoint(HeartDepth);
            var ribs=Geometry();
            var pipes=Geometry();
            var channels=Geometry();
            var valve=Geometry();
            var iris=Geometry();
            var textures=Geometry();

            Vector3 core = new Vector3(0,1.59f,4.37f);
            for (int i=0;i<9;i++)
            {
                float z=-2.1f+i*.81f;
                Ring(ribs,new Vector3(0,1.44f,z),3.58f,.14f,WorldMesh.Shade(Iron,.77f+i*.032f),
                    Quaternion.identity,27,1);
                ribs.Box(new Vector3(0,-.23f,z),new Vector3(7.85f,.25f,.80f),Quaternion.identity,
                    WorldMesh.Shade(i%2==0?Iron:Coal,.54f+i*.045f));
            }
            for (int side=-1;side<=1;side+=2)
            {
                Vector3 line = new Vector3(side*1.65f,1.78f,4.28f);
                Vector3 bank = new Vector3(side*3.54f,3.91f,2.44f);
                Vector3 spineEnd = new Vector3(side*2.70f,2.21f,-3.52f);
                pipes.Tube(new[]{core,core+new Vector3(side*.64f,.85f,-.18f),line,bank,
                        bank+new Vector3(side*.13f,.10f,-2.35f),spineEnd},
                    new[]{.38f,.61f,.46f,.36f,.32f,.23f},12,WorldMesh.Shade(Iron,1.08f));
                channels.Tube(new[]{core+Vector3.back*.36f,line+Vector3.back*.28f,bank+Vector3.down*.22f,
                        bank+new Vector3(side*.09f,-.20f,-2.35f),spineEnd+Vector3.down*.18f},
                    new[]{.033f,.053f,.071f,.052f,.028f},7,WorldMesh.Shade(Circuit,.66f));
                ribs.Box(new Vector3(side*3.74f,1.58f,2.27f),new Vector3(.21f,3.38f,7.15f),Quaternion.identity,Coal);
                for (int i=0;i<4;i++)
                {
                    float z=-.8f+i*2f;
                    Vector3 at=new Vector3(side*3.65f,.18f,z);
                    ribs.Box(at,new Vector3(.34f,.56f,.44f),Quaternion.identity,Iron);
                    channels.Box(at+new Vector3(-side*.198f,.09f,0),new Vector3(.035f,.18f,.35f),Quaternion.identity,
                        WorldMesh.Shade(Circuit,.61f));
                }
            }
            for(int i=0;i<CablePorts.Length;i++)
            {
                Vector3 port=CablePorts[i];
                Vector3 side=i==2?Vector3.up:Vector3.right*Mathf.Sign(port.x);
                pipes.Tube(new[]{port+side*.65f+Vector3.forward*.95f,port+side*.31f+Vector3.forward*.26f,port},
                    new[]{.29f,.26f,.24f},11,Bronze);
                Ring(channels,port+Vector3.back*.066f,.172f,.036f,WorldMesh.Shade(HeartAmber,.61f),Quaternion.identity,16,1);
            }

            valve.Rock(new Vector3(-.34f,.27f,0),new Vector3(.73f,.89f,.66f),WorldMesh.Shade(Bronze,.84f),.04f,11,15,.075f);
            valve.Rock(new Vector3(.42f,.20f,.04f),new Vector3(.61f,.79f,.63f),WorldMesh.Shade(Bronze,.71f),.19f,11,15,.062f);
            valve.Rock(new Vector3(.15f,-.31f,-.02f),new Vector3(.66f,.77f,.60f),Bronze,.23f,10,16,.080f);
            for(int i=0;i<8;i++)
            {
                float angle = i*Mathf.PI/4;
                Vector3 radius = new Vector3(Mathf.Sin(angle),Mathf.Cos(angle),0);
                textures.Tube(new[]{radius*.22f+Vector3.back*.68f,radius*.63f+Vector3.back*.59f,radius*.83f+Vector3.back*.33f},
                    new[]{.020f,.021f,.013f},7,WorldMesh.Shade(HeartAmber,.72f+.16f*Mathf.Sin(angle*2)));
            }
            heartCore=Group("Asymmetric pump chambers",machineHeart);
            heartCore.localPosition=core;
            MachinePart("Piston housing",valve,stone,heartCore);
            heartGlow=MachinePart("Heart veins",textures,glow,heartCore).GetComponent<MeshRenderer>();

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
                float peak=.201f+i*.0085f;
                float current=handoff?Mathf.Exp(-Mathf.Pow((t-peak)/.041f,2)):0;
                returnPulses[i].gameObject.SetActive(current>.023f);
                if(current>.023f) Light(returnPulses[i],HeartAmber,current*2.68f);
            }
            for(int i=0;i<brainPulses.Length;i++)
            {
                float ripple=.5f+.5f*Mathf.Sin(time*4.2f-i*.74f);
                float kick=handoff?Mathf.Exp(-Mathf.Pow((t-(.21f+(brainPulses.Length-1-i)*.011f))/.045f,2)):0;
                Light(brainPulses[i],Color.Lerp(Circuit,HeartAmber,kick),.23f+.58f*ripple+2.05f*kick);
            }
            if (!machineHeart.gameObject.activeSelf) return;

            float beat=handoff?Mathf.Exp(-Mathf.Pow((t-.21f)/.047f,2)):0;
            float afterBeat=handoff?.45f*Ease(.24f,.35f,t)*(1-Ease(.30f,.48f,t)):0;
            float breath=.5f+.5f*Mathf.Sin(time*1.48f);
            float ignition=Mathf.Max(journey.ForestPlantCharge,beat+afterBeat);
            heartCore.localScale=new Vector3(1+.17f*beat,1+.22f*beat,1+.15f*beat);
            Light(heartGlow,Color.Lerp(PoolBlue,HeartAmber,Mathf.Clamp01(ignition)),.12f+.085f*breath+2.50f*ignition);
            float shutters=Ease(.77f,.99f,t);
            irisLeft.localPosition=new Vector3(Mathf.Lerp(-2.61f,-.18f,shutters),1.29f,3.58f);
            irisRight.localPosition=new Vector3(Mathf.Lerp(2.61f,.18f,shutters),1.29f,3.58f);
            irisLeft.localRotation=Quaternion.Euler(0,20*(1-shutters),8*shutters);
            irisRight.localRotation=Quaternion.Euler(0,-20*(1-shutters),-8*shutters);

            float reach=Ease(.42f,.61f,t);
            bool show=handoff && reach>.002f;
            for(int i=0;i<heartCable.Length;i++)
            {
                heartCable[i].gameObject.SetActive(show);
                heartCableInner[i].gameObject.SetActive(show && i%3!=1);
            }
            if(!show) return;

            Vector3 body=machineHeart.InverseTransformPoint(actor);
            Vector3 port=CablePorts[Mathf.Clamp(journey.ForestCableAnchor,0,CablePorts.Length-1)];
            float coil=Ease(.59f,.81f,t);
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
            if (machineSign!=null) Object.Destroy(machineSign);
        }
    }
}
