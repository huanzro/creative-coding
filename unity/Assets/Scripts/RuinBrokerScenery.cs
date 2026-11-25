/* ARTISTIC RATIONALE
 * The archive’s broker treats a person's organs and devices as equivalent entries in a ledger.
 * Symmetrical ceramic plates, orbiting counters and motor-driven joints make its accounting
 * visible. The warm pulse and irregular folds of the extracted light break that symmetry.
 */
using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private sealed class BrokerArm
        {
            public Transform Upper, Forearm, Elbow, Hand;
            public Transform[] Finger = new Transform[4];
        }

        private static readonly Color BrokerCeramic = new Color(.80f,.87f,.93f);
        private static readonly Color BrokerTitanium = new Color(.39f,.53f,.62f);
        private static readonly Color BrokerCarbon = new Color(.085f,.15f,.22f);
        private static readonly Color BrokerGlass = new Color(.072f,.20f,.26f);
        private static readonly Color BrokerCyan = new Color(.34f,.86f,1);
        private static readonly Color BrokerAmber = new Color(1,.71f,.28f);
        private static readonly Color BrokerHeartLight = new Color(1,.32f,.27f);
        private static readonly Color BrokerBrainLight = new Color(.65f,.61f,1);
        private const int BrokerLedgerSlots = 15;

        private Transform brokerStage, brokerBody, brokerTorso, brokerHead, brokerHeart, brokerBrain;
        private Transform brokerPhone, brokerComputer, brokerLedger;
        private BrokerArm brokerLeft, brokerRight;
        private MeshRenderer brokerVisor, brokerChest, brokerHeartBeam, brokerBrainBeam;
        private MeshRenderer[] brokerHalo, brokerParticles, brokerTokens;

        private void BuildRuinBroker()
        {
            brokerStage = Group("Ruin token broker and exchange floor",root.transform);
            brokerBody = Group("Suspended transaction android",brokerStage);
            brokerTorso = Group("Counterbalanced spine",brokerBody);
            brokerHead = Group("Sensor crown and gimballed neck",brokerTorso);

            var armor = Geometry();
            var grille = Geometry();
            var circuits = Geometry();
            // A faceted chest shape with a separate contracting waist and an exposed spine.
            BrokerHull(armor, .64f, 2.38f, 1.33f, .43f, .48f, BrokerCeramic);
            BrokerHull(armor, .42f, 1.49f, .68f, .34f, .32f, BrokerCarbon);
            BrokerHull(armor, .75f, .99f, 1.34f, .40f, .84f, BrokerTitanium);
            for (int joint = 0; joint < 8; joint++)
            {
                float y = 1.04f + joint * .245f;
                armor.Box(new Vector3(0,y,.33f),new Vector3(.25f,.115f,.21f),Quaternion.identity,BrokerCarbon);
                RuinBar(circuits,new Vector3(-.065f,y,.45f),new Vector3(.065f,y,.45f),.019f,.014f,BrokerCyan);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 shoulder = new Vector3(side*.71f,2.48f,.045f);
                armor.Rock(shoulder,new Vector3(.33f,.29f,.32f),BrokerTitanium,0,8,22,0);
                armor.Box(shoulder+new Vector3(side*.06f,.165f,-.015f),new Vector3(.39f,.25f,.54f),
                    Quaternion.Euler(0,side*10,-side*18),BrokerCeramic);
                armor.Tube(new[] { new Vector3(side*.66f,2.49f,.19f),new Vector3(side*.57f,2.12f,.40f),
                    new Vector3(side*.29f,1.58f,.38f) },new[] { .064f,.056f,.075f },10,BrokerTitanium);
                for (int i = 0; i < 5; i++)
                {
                    var vent = new Vector3(side*(.28f+i*.061f),2.25f-i*.043f,-.435f+i*.019f);
                    grille.Box(vent,new Vector3(.045f,.31f,.040f),Quaternion.Euler(0,0,-side*20),BrokerCarbon);
                    circuits.Box(vent+Vector3.back*.026f,new Vector3(.014f,.095f,.012f),
                        Quaternion.Euler(0,0,-side*20),i == 0 ? BrokerAmber : BrokerCyan);
                }
                armor.Box(new Vector3(side*.34f,1.07f,-.32f),new Vector3(.21f,.34f,.16f),
                    Quaternion.Euler(0,side*21,-side*12),BrokerCeramic);
                RuinBar(circuits,new Vector3(side*.262f,1.17f,-.429f),new Vector3(side*.346f,.97f,-.434f),
                    .021f,.020f,BrokerCyan);
                RuinBar(circuits,new Vector3(side*.28f,2.64f,-.424f),new Vector3(side*.53f,2.58f,-.329f),
                    .028f,.025f,BrokerCyan);
                Vector3 jet = new Vector3(side*.40f,.64f,.02f);
                armor.Rock(jet,new Vector3(.145f,.33f,.16f),BrokerCarbon,0,7,20,0);
                BrokerArc(circuits,jet-Vector3.up*.25f,.105f,.105f,Vector3.right,Vector3.forward,
                    0,360,.025f,BrokerCyan);
            }
            BrokerPart("Ceramic plate shell and flight thrusters",armor,tunnelMetal,brokerTorso);
            BrokerPart("Separate carbon vent slots",grille,tunnelMetal,brokerTorso);
            brokerChest = BrokerPart("Visible diagnostic circuits",circuits,glow,brokerTorso).GetComponent<MeshRenderer>();
            var reactor = Geometry();
            Vector3 housing=new Vector3(0,2.24f,-.44f);
            reactor.Rock(housing,new Vector3(.24f,.255f,.10f),BrokerCarbon,0,8,20,0);
            BrokerPart("Recessed power cell",reactor,tunnelMetal,brokerTorso);
            var core = Geometry();
            core.Rock(housing+Vector3.back*.095f,new Vector3(.092f,.151f,.028f),BrokerCyan,0,6,12,0);
            BrokerArc(core,housing+Vector3.back*.091f,.17f,.185f,Vector3.right,Vector3.up,24,307,.022f,BrokerCyan);
            BrokerArc(core,housing+Vector3.back*.105f,.205f,.216f,Vector3.right,Vector3.up,194,258,.015f,BrokerAmber);
            Light(BrokerPart("Twin diagnostic rings and luminous core",core,glow,brokerTorso).GetComponent<MeshRenderer>(),Color.white,.75f);

            brokerHead.localPosition = new Vector3(0,2.92f,-.055f);
            var face = Geometry();
            BrokerHull(face,.35f,.14f,.75f,.26f,.57f,BrokerCeramic);
            face.Rock(new Vector3(0,-.22f,.015f),new Vector3(.17f,.19f,.16f),BrokerCarbon,0,8,18,0);
            // The dark glass sits entirely in front of the helmet bevel.
            face.Box(new Vector3(0,.094f,-.258f),new Vector3(.67f,.203f,.113f),Quaternion.identity,BrokerCarbon);
            face.Box(new Vector3(0,.094f,-.318f),new Vector3(.60f,.154f,.034f),Quaternion.identity,BrokerGlass);
            var vision = Geometry();
            for (int i = -4; i <= 4; i++)
            {
                float x = i*.069f;
                face.Box(new Vector3(x,.093f,-.335f),new Vector3(.049f,.12f,.022f),
                    Quaternion.Euler(0,i*2,0),BrokerCarbon);
                vision.Box(new Vector3(x,.091f,-.350f),new Vector3(.042f,.022f+(.034f-Mathf.Abs(i)*.007f),.020f),
                    Quaternion.identity,i == 0 ? BrokerAmber : BrokerCyan);
            }
            for (int side=-1; side<=1; side+=2)
            {
                face.Rock(new Vector3(side*.382f,.165f,.035f),new Vector3(.092f,.151f,.139f),BrokerTitanium,0,7,20,0);
                RuinBar(face,new Vector3(side*.311f,.093f,-.330f),new Vector3(side*.420f,.116f,-.151f),.115f,.108f,BrokerCarbon);
                RuinBar(vision,new Vector3(side*.279f,.091f,-.365f),new Vector3(side*.422f,.112f,-.203f),.023f,.030f,BrokerCyan);
                BrokerArc(vision,new Vector3(side*.459f,.165f,.03f),.108f,.116f,Vector3.forward,Vector3.up,
                    25,300,.016f,BrokerCyan);
                RuinBar(face,new Vector3(side*.28f,.393f,.08f),new Vector3(side*.34f,.65f,.20f),.045f,.045f,BrokerCeramic);
                RuinBar(vision,new Vector3(side*.34f,.60f,.18f),new Vector3(side*.354f,.67f,.21f),.021f,.023f,BrokerCyan);
            }
            BrokerPart("Faceted helmet and recessed black-glass mask",face,tunnelMetal,brokerHead);
            brokerVisor = BrokerPart("Nine-element face display",vision,glow,brokerHead).GetComponent<MeshRenderer>();
            brokerLeft = BuildBrokerArm("Near side servo arm");
            brokerRight = BuildBrokerArm("Far side servo arm");

            brokerHalo = new MeshRenderer[3];
            for (int i = 0; i < brokerHalo.Length; i++)
            {
                var circle = Geometry();
                for (int piece = 0; piece < 6; piece++)
                {
                    float angle = i*21 + piece*60;
                    float r = 1.35f+i*.35f;
                    BrokerArc(circle,Vector3.zero,r,r,Vector3.right,Vector3.forward,angle,angle+44-i*5,
                        i == 0 ? .024f : .014f,piece%3 == 0 ? BrokerAmber : BrokerCyan);
                }
                brokerHalo[i] = BrokerPart("Airborne calibration halo",circle,glow,brokerStage).GetComponent<MeshRenderer>();
            }
            brokerParticles = new MeshRenderer[18];
            for (int i=0; i<brokerParticles.Length; i++)
                brokerParticles[i] = RuinPiece("Calibration point",beaconMesh,glow,brokerStage).GetComponent<MeshRenderer>();
            brokerPhone = BuildBrokerDevice(true);
            brokerComputer = BuildBrokerDevice(false);
            BuildBrokerLedger();
            brokerHeart = BuildBrokerOrgan(true);
            brokerBrain = BuildBrokerOrgan(false);
            var strand = Geometry();
            BrokerArc(strand,Vector3.forward*.13f,.048f,.048f,Vector3.right,Vector3.up,0,360,.021f,Color.white);
            RuinBar(strand,new Vector3(-.013f,0,0),new Vector3(.016f,0,1),.013f,.010f,Color.white);
            RuinBar(strand,new Vector3(.012f,0,0),new Vector3(-.011f,0,1),.007f,.007f,Color.white);
            Mesh strandMesh = strand.Finish("Twined transfer light");
            brokerHeartBeam = RuinPiece("Heart projection light",strandMesh,glow,brokerStage).GetComponent<MeshRenderer>();
            brokerBrainBeam = RuinPiece("Brain projection light",strandMesh,glow,brokerStage).GetComponent<MeshRenderer>();
        }

        private Transform BrokerPart(string name, WorldMesh geometry, Material material, Transform parent) =>
            RuinPiece(name,geometry.Finish(name),material,parent);

        private static void BrokerHull(WorldMesh mesh, float radius, float y, float height, float depth,
            float waist, Color color)
        {
            // Three beveled octagons make a hollow space for visible gaps and dark seals.
            for (int edge = 0; edge < 8; edge++)
            {
                float a = (edge+.5f)*Mathf.PI/4, b = a+Mathf.PI/4;
                Vector3 one = new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Vector3 two = new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                Vector3 scale = new Vector3(radius,0,depth);
                Vector3 first = Vector3.Scale(one,scale), second = Vector3.Scale(two,scale);
                Vector3 low = Vector3.up*(y-height*.51f), middle = Vector3.up*(y-height*.18f), high = Vector3.up*(y+height*.49f);
                Color plate = WorldMesh.Shade(color,.74f+.067f*(edge%5));
                mesh.Quad(low+first*waist, middle+first, middle+second, low+second*waist, plate);
                mesh.Quad(middle+first, high+first*.83f, high+second*.83f, middle+second, color);
                mesh.Triangle(high+first*.83f,high,high+second*.83f,WorldMesh.Shade(color,1.07f));
                mesh.Triangle(low+second*waist,low,low+first*waist,plate);
            }
        }

        private static void BrokerArc(WorldMesh mesh, Vector3 center, float rx, float ry, Vector3 x, Vector3 y,
            float from, float to, float width, Color tint)
        {
            int steps = Mathf.Max(3,Mathf.CeilToInt((to-from)/8));
            float first = from*Mathf.Deg2Rad;
            Vector3 previous = center+x*(rx*Mathf.Cos(first))+y*(ry*Mathf.Sin(first));
            for (int i = 1; i <= steps; i++)
            {
                float angle = Mathf.Lerp(from,to,i/(float)steps)*Mathf.Deg2Rad;
                Vector3 next = center+x*(rx*Mathf.Cos(angle))+y*(ry*Mathf.Sin(angle));
                RuinBar(mesh,previous,next,width,width,tint);
                previous = next;
            }
        }

        private BrokerArm BuildBrokerArm(string name)
        {
            Transform mount = Group(name,brokerBody);
            var arm = new BrokerArm();
            var joint = Geometry();
            joint.Rock(Vector3.zero,new Vector3(.135f,.135f,.135f),BrokerTitanium,0,8,20,0);
            arm.Elbow = BrokerPart("Ball joint and servo",joint,tunnelMetal,mount);
            for (int segment = 0; segment < 2; segment++)
            {
                Transform link = Group(segment == 0 ? "Upper actuator" : "Wrist actuator",mount);
                var shell = Geometry();
                var indicators = Geometry();
                float width = segment == 0 ? .31f : .25f;
                shell.Tube(new[] {Vector3.zero,Vector3.forward*.50f,Vector3.forward},
                    new[] {width*.36f,width*.33f,width*.32f},12,BrokerCarbon);
                shell.Box(Vector3.forward*.47f+Vector3.up*.053f,new Vector3(width,.19f,.49f),Quaternion.identity,BrokerCeramic);
                shell.Box(Vector3.forward*.72f+Vector3.up*.042f,new Vector3(width*.85f,.115f,.13f),Quaternion.identity,BrokerTitanium);
                for (int rail = -1; rail <= 1; rail += 2)
                {
                    Vector3 near=new Vector3(rail*.087f,-.071f,.13f), far=new Vector3(rail*.088f,-.062f,.89f);
                    RuinBar(shell,near,far,.040f,.041f,BrokerTitanium);
                    RuinBar(indicators,Vector3.Lerp(near,far,.19f)+Vector3.down*.024f,
                        Vector3.Lerp(near,far,.52f)+Vector3.down*.024f,.014f,.014f,BrokerCyan);
                }
                BrokerPart("Segmented manipulator plates",shell,tunnelMetal,link);
                Light(BrokerPart("Arm status circuit",indicators,glow,link).GetComponent<MeshRenderer>(),Color.white,.44f);
                if (segment == 0) arm.Upper=link;
                else arm.Forearm=link;
            }
            arm.Hand = Group("Open grip processor",mount);
            var palm = Geometry();
            palm.Rock(new Vector3(0,0,.082f),new Vector3(.151f,.058f,.145f),BrokerCeramic,0,6,16,.035f);
            palm.Box(new Vector3(0,.045f,.10f),new Vector3(.209f,.032f,.183f),Quaternion.identity,BrokerCarbon);
            RuinBar(palm,new Vector3(.112f,0,.071f),new Vector3(.205f,.047f,.163f),.067f,.064f,BrokerTitanium);
            BrokerPart("Wrist plate, palm and thumb",palm,tunnelMetal,arm.Hand);
            for (int i = 0; i < arm.Finger.Length; i++)
            {
                var finger = Geometry();
                float length = .149f-Mathf.Abs(i-1.55f)*.026f;
                RuinBar(finger,Vector3.zero,Vector3.forward*length*.64f,.038f,.039f,BrokerCeramic);
                RuinBar(finger,Vector3.forward*length*.64f,new Vector3(0,.030f,length),.030f,.032f,BrokerTitanium);
                arm.Finger[i]=BrokerPart("Capped mechanical finger",finger,tunnelMetal,arm.Hand);
                arm.Finger[i].localPosition=new Vector3((i-1.5f)*.069f,.006f,.179f-Mathf.Abs(i-1.5f)*.016f);
            }
            return arm;
        }

        private Transform BuildBrokerDevice(bool phone)
        {
            Transform prop = Group(phone ? "ipone18p exchange preview" : "Computer exchange preview",brokerStage);
            var casework=Geometry();
            var screen=Geometry();
            if (phone)
            {
                casework.Box(Vector3.zero,new Vector3(.37f,.69f,.043f),Quaternion.identity,BrokerTitanium);
                casework.Box(Vector3.back*.026f,new Vector3(.32f,.635f,.016f),Quaternion.identity,BrokerGlass);
                screen.Box(Vector3.back*.037f,new Vector3(.27f,.53f,.006f),Quaternion.identity,new Color(.23f,.48f,.81f));
                for (int row=0; row<5; row++)
                    for (int col=0; col<3; col++)
                        screen.Box(new Vector3((col-1)*.073f,.18f-row*.086f,-.045f),new Vector3(.038f,.035f,.008f),
                            Quaternion.identity,(row+col)%3==0 ? BrokerAmber : BrokerCyan);
            }
            else
            {
                casework.Box(new Vector3(0,.23f,.15f),new Vector3(.67f,.47f,.043f),Quaternion.Euler(11,0,0),BrokerTitanium);
                casework.Box(new Vector3(0,-.008f,-.064f),new Vector3(.67f,.039f,.448f),Quaternion.identity,BrokerCarbon);
                screen.Box(new Vector3(0,.242f,.118f),new Vector3(.59f,.376f,.012f),Quaternion.Euler(11,0,0),new Color(.20f,.40f,.73f));
                for (int row=0; row<4; row++)
                {
                    screen.Box(new Vector3(-.095f,.355f-row*.072f,.089f-row*.014f),new Vector3(.23f+row*.030f,.018f,.015f),
                        Quaternion.Euler(11,0,0),row==0 ? BrokerAmber : BrokerCyan);
                    for (int col=0; col<10; col++)
                        casework.Box(new Vector3((col-4.5f)*.052f,.017f,.059f-row*.053f),new Vector3(.035f,.017f,.033f),
                            Quaternion.identity,BrokerTitanium);
                }
            }
            BrokerPart("Device case and machined details",casework,tunnelMetal,prop);
            Light(BrokerPart("Powered device screen",screen,glow,prop).GetComponent<MeshRenderer>(),Color.white,.24f);
            return prop;
        }

        private Transform BuildBrokerOrgan(bool heart)
        {
            Transform organ = Group(heart ? "Luminous extracted heart" : "Luminous extracted brain",brokerStage);
            var volume = Geometry();
            var fine = Geometry();
            Color color = heart ? BrokerHeartLight : BrokerBrainLight;
            if (heart)
            {
                volume.Rock(new Vector3(-.074f,.005f,.02f),new Vector3(.121f,.172f,.10f),color,0,10,24,.05f);
                volume.Rock(new Vector3(.074f,-.031f,0),new Vector3(.102f,.165f,.091f),color,0,10,24,.04f);
                volume.Rock(new Vector3(-.014f,-.113f,0),new Vector3(.074f,.128f,.068f),color,0,8,22,.04f);
                fine.Tube(new[] {new Vector3(-.035f,.10f,0),new Vector3(-.055f,.213f,.023f),new Vector3(.068f,.237f,.027f),
                    new Vector3(.105f,.177f,.029f)},new[] {.024f,.026f,.021f,.017f},10,BrokerAmber);
                for (int i = 0; i < 3; i++)
                    fine.Tube(new[] {new Vector3(-.06f,.13f,.092f),new Vector3(.015f+.048f*i,.031f,.110f),
                        new Vector3(-.06f+.064f*i,-.086f,.088f)},new[] {.012f,.008f,.004f},8,BrokerAmber);
            }
            else
            {
                for (int side=-1; side<=1; side+=2)
                {
                    var hemisphere = new Vector3(side*.083f,.01f,0);
                    volume.Rock(hemisphere,new Vector3(.082f,.121f,.166f),color,0,12,26,.11f);
                    for (int i=0; i<8; i++)
                    {
                        float z = (i-3.5f)*.038f;
                        float edge = 1-Mathf.Abs(i-3.5f)*.052f;
                        fine.Tube(new[] {hemisphere+new Vector3(side*.014f,.113f*edge,z),
                            hemisphere+new Vector3(side*.047f,.112f*edge,z+.023f),
                            hemisphere+new Vector3(side*.079f,.069f*edge,z-.012f),
                            hemisphere+new Vector3(side*.084f,.011f,z+.024f)},
                            new[] {.012f,.016f,.013f,.010f},8,Color.Lerp(color,Color.white,.42f));
                    }
                }
                volume.Rock(new Vector3(0,-.105f,.054f),new Vector3(.045f,.059f,.054f),color,0,7,12,.04f);
            }
            Light(BrokerPart("Volumetric light made tangible",volume,glow,organ).GetComponent<MeshRenderer>(),Color.white,.16f);
            Light(BrokerPart("Fine glistening anatomical folds",fine,glow,organ).GetComponent<MeshRenderer>(),Color.white,.54f);
            return organ;
        }

        private void BuildBrokerLedger()
        {
            brokerLedger = Group("Token counter",brokerStage);
            var sockets = Geometry();
            var channel = Geometry();
            var token = Geometry();
            token.Box(Vector3.zero,new Vector3(.067f,.128f,.048f),Quaternion.Euler(0,0,45),BrokerAmber);
            BrokerArc(token,Vector3.back*.031f,.046f,.046f,Vector3.right,Vector3.up,0,360,.011f,Color.white);
            Mesh credit=token.Finish("Transferable token");
            brokerTokens = new MeshRenderer[BrokerLedgerSlots];
            for (int i=0; i<brokerTokens.Length; i++)
            {
                Vector3 socket=BrokerTokenSlot(i);
                sockets.Box(socket+Vector3.forward*.046f,new Vector3(.108f,.161f,.067f),Quaternion.Euler(0,0,45),BrokerCarbon);
                BrokerArc(channel,socket+Vector3.forward*.011f,.061f,.069f,Vector3.right,Vector3.up,24,319,.013f,BrokerCyan);
                brokerTokens[i]=RuinPiece("Filled token position",credit,glow,brokerLedger).GetComponent<MeshRenderer>();
            }
            BrokerPart("Unpaid credit slots",sockets,tunnelMetal,brokerLedger);
            Light(BrokerPart("Token scan rail",channel,glow,brokerLedger).GetComponent<MeshRenderer>(),Color.white,.13f);
        }

        private static Vector3 BrokerTokenSlot(int i)
        {
            float offset = (i - (BrokerLedgerSlots - 1)*.5f)*.139f;
            return new Vector3(offset,.10f*offset*offset,Mathf.Abs(offset)*.105f);
        }

        private void ShowBrokerLedger(Journey journey, RuinRetryPhase phase, float age, float time)
        {
            float open=phase==RuinRetryPhase.Valuation ? OpeningStoryboard.Ease(.26f,.78f,age) :
                phase==RuinRetryPhase.Bargain ? 1 :
                phase==RuinRetryPhase.Accepted || phase==RuinRetryPhase.Declined
                    ? 1-OpeningStoryboard.Ease(phase==RuinRetryPhase.Accepted ? 3.29f : .16f,phase==RuinRetryPhase.Accepted ? 4.02f : .68f,age) : 0;
            brokerLedger.gameObject.SetActive(open>.002f);
            if (open<=.002f) return;
            brokerLedger.localPosition=new Vector3(.48f,1.29f+.04f*Mathf.Sin(time*2),3.12f);
            brokerLedger.localScale=new Vector3(open,Mathf.Lerp(.52f,1,open),1);
            float filled=BrokerLedgerSlots*journey.RuinRevealedTokens/(float)Mathf.Max(1,journey.RuinTokenCost);
            for (int i=0; i<brokerTokens.Length; i++)
            {
                float amount=Mathf.Clamp01(filled-i);
                Transform token=brokerTokens[i].transform;
                token.gameObject.SetActive(amount>.002f);
                if (amount<=.002f) continue;
                token.localPosition=BrokerTokenSlot(i)+Vector3.up*((1-amount)*.26f)+Vector3.back*(.04f+.065f*(1-amount));
                token.localScale=Vector3.one*amount;
                token.localRotation=Quaternion.Euler(0,(1-amount)*160,0);
                Light(brokerTokens[i],Color.white,.56f+.12f*Mathf.Sin(time*6.4f-i*.77f));
            }
        }

        private void DrawRuinBroker(Journey journey, int cast, Vector3 floorOrigin, Vector3 actorFoot, Vector3 forward)
        {
            RuinRetryPhase phase = journey.RuinRetryState;
            bool on = phase == RuinRetryPhase.Arrival || phase == RuinRetryPhase.Valuation ||
                phase == RuinRetryPhase.Bargain || phase == RuinRetryPhase.Accepted || phase == RuinRetryPhase.Declined;
            if (!on)
            {
                if (brokerStage != null) brokerStage.gameObject.SetActive(false);
                return;
            }
            if (brokerStage == null) BuildRuinBroker();
            brokerStage.gameObject.SetActive(true);
            brokerStage.SetPositionAndRotation(floorOrigin,Quaternion.LookRotation(forward,Vector3.up));
            float age=journey.Age, time=journey.Time;
            bool accepted=phase==RuinRetryPhase.Accepted, declined=phase==RuinRetryPhase.Declined;
            float descent=phase==RuinRetryPhase.Arrival ? OpeningStoryboard.Ease(.08f,3.75f,age) : 1;
            float lean=accepted ? OpeningStoryboard.Window(age,.12f,1.20f,2.76f,3.86f) : 0;
            float sigh=declined ? OpeningStoryboard.Window(age,.22f,.83f,1.27f,2.25f) : 0;
            float dismiss=declined ? OpeningStoryboard.Window(age,1.75f,2.38f,2.96f,4.10f) : 0;
            float hover=.095f*Mathf.Sin(time*2.0f);
            float transfer=accepted ? OpeningStoryboard.Ease(2.80f,4.22f,age) : 0;
            float departure=accepted||declined ? OpeningStoryboard.Ease(3.0f,4.64f,age) : 0;
            brokerBody.localPosition=new Vector3(1.35f+departure*.72f,
                Mathf.Lerp(16.4f,.24f+hover,descent)-.38f*lean-.055f*sigh,
                Mathf.Lerp(6.45f,4.35f,descent)-1.93f*lean);
            brokerBody.localRotation=Quaternion.Euler(0,Mathf.Lerp(17,33,lean)-17*departure,0);
            brokerTorso.localRotation=Quaternion.Euler(-lean*13-sigh*10,0,3*sigh);
            brokerHead.localRotation=Quaternion.Euler(-13*lean-22*sigh+6*Mathf.Sin(time*1.1f),dismiss*19,3*Mathf.Sin(time*.71f));
            Light(brokerVisor,Color.Lerp(Color.white,BrokerAmber,sigh*.55f),.68f-.31f*sigh+.10f*Mathf.Sin(time*4.3f));
            Light(brokerChest,Color.white,.38f+.12f*Mathf.Sin(time*3.4f)+.30f*lean);

            // No part of the gripper crosses the actor's front envelope. The organ shapes begin outside it.
            Vector3 settledFoot = brokerStage.InverseTransformPoint(actorFoot);
            (Vector3 heart, Vector3 brain, Vector3 wrist) = cast switch
            {
                // The birds carry their hearts lower, and the swan has a projecting bill.
                4 => (new Vector3(-.095f,.42f,.34f),new Vector3(.095f,.72f,.36f),new Vector3(.27f,.70f,.85f)),
                5 => (new Vector3(-.10f,.48f,.44f),new Vector3(.095f,.92f,.48f),new Vector3(.27f,.77f,1.02f)),
                _ => (new Vector3(-.075f,.68f,.24f),new Vector3(.075f,.92f,.24f),new Vector3(.26f,.79f,.79f))
            };
            Vector3 contact = settledFoot + wrist;
            Vector3 localPalm = brokerBody.InverseTransformPoint(brokerStage.TransformPoint(contact));
            Vector3 left = Vector3.Lerp(new Vector3(-.73f,1.39f,-.34f),localPalm,lean);
            Vector3 palmDirection = Vector3.Lerp(new Vector3(-.28f,-.92f,-.28f),new Vector3(-.21f,.12f,-1),lean);
            if (dismiss>0)
            {
                left = Vector3.Lerp(left,new Vector3(-1.72f,2.0f,-.56f),dismiss);
                palmDirection = Vector3.Lerp(palmDirection,new Vector3(-1,.35f,-.22f),dismiss);
            }
            PoseBrokerArm(brokerLeft,-1,left,palmDirection,Mathf.Max(lean,dismiss));
            float tally=phase==RuinRetryPhase.Valuation || phase==RuinRetryPhase.Bargain
                ? .77f+.13f*Mathf.Sin(time*1.6f) : 0;
            PoseBrokerArm(brokerRight,1,Vector3.Lerp(new Vector3(.77f,1.31f,-.31f),new Vector3(.78f,2.04f,-1.05f),tally),
                Vector3.Lerp(new Vector3(.31f,-.87f,-.30f),new Vector3(.05f,.02f,-1),tally),tally);
            for (int i = 0; i < brokerHalo.Length; i++)
            {
                Transform halo = brokerHalo[i].transform;
                halo.localPosition=brokerBody.localPosition+Vector3.up*(.17f+i*.45f+(1-descent)*.73f);
                halo.localRotation=Quaternion.Euler(11*(1-descent)*i,time*(i%2==0 ? 29 : -40)+i*30,4*i);
                halo.localScale=Vector3.one*(.63f+descent*.36f+i*.11f);
                Light(brokerHalo[i],Color.white,.22f+(1-descent)*.36f+.20f*lean);
            }
            for (int i=0; i<brokerParticles.Length; i++)
            {
                float angle=(time*(.9f+i*.033f)+i*.67f);
                float radius=.64f+i*.071f+.32f*(1-descent);
                Transform mote=brokerParticles[i].transform;
                mote.localPosition=brokerBody.localPosition+new Vector3(Mathf.Cos(angle)*radius,.28f+Mathf.Repeat(time*.47f+i*.271f,1)*2.51f,
                    Mathf.Sin(angle)*radius);
                mote.localScale=new Vector3(.014f,.048f+.08f*(1-descent),.014f);
                Light(brokerParticles[i],i%4==0 ? BrokerAmber : BrokerCyan,.35f+(1-descent)*.31f);
            }
            float sample=phase==RuinRetryPhase.Valuation ? OpeningStoryboard.Window(age,.18f,.76f,2.95f,3.65f) : 0;
            bool validPhone=journey.RuinPhones>0 && sample>.001f, validComputer=journey.RuinComputers>0 && sample>.001f;
            Vector3 palm=brokerStage.InverseTransformPoint(brokerLeft.Hand.TransformPoint(new Vector3(0,.17f,.10f)));
            Vector3 reservoir=brokerBody.localPosition+new Vector3(-.42f,2.25f,-.48f);
            float invoice=accepted ? OpeningStoryboard.Window(age,3.10f,3.48f,3.87f,4.43f) : 0;
            PlaceBrokerDevice(brokerPhone,validPhone || invoice>.001f && journey.RuinTradePhones>0,
                accepted ? reservoir+new Vector3(-.49f,.12f,-.25f) : brokerBody.localPosition+new Vector3(-1.12f,1.82f,-1.23f),
                time,accepted ? invoice*.58f : sample,-1);
            PlaceBrokerDevice(brokerComputer,validComputer || invoice>.001f && journey.RuinTradeComputers>0,
                accepted ? reservoir+new Vector3(.46f,.03f,-.23f) : brokerBody.localPosition+new Vector3(-.36f,1.67f,-1.52f),
                time,accepted ? invoice*.58f : sample,1);
            ShowBrokerLedger(journey,phase,age,time);
            DrawBrokerTransfer(brokerHeart,brokerHeartBeam,accepted,age,time,1.08f,1.98f,
                settledFoot+heart,palm+Vector3.left*.13f,reservoir+Vector3.left*.14f,transfer,BrokerHeartLight,true);
            DrawBrokerTransfer(brokerBrain,brokerBrainBeam,accepted,age,time,1.78f,2.66f,
                settledFoot+brain,palm+Vector3.right*.13f,reservoir+Vector3.right*.14f,transfer,BrokerBrainLight,false);
        }

        private void PoseBrokerArm(BrokerArm arm, int side, Vector3 hand, Vector3 handDirection, float open)
        {
            Vector3 shoulder = brokerTorso.localRotation * new Vector3(side*.71f,2.48f,.045f);
            Vector3 reach=hand-shoulder;
            Vector3 axis=reach.normalized;
            Vector3 hinge=Vector3.ProjectOnPlane(new Vector3(side*.86f,-.50f,.34f),axis).normalized;
            float span=Mathf.Max(.05f,reach.magnitude);
            float reachMax=2.18f, upper=1.11f, lower=1.08f;
            float a=(upper*upper-lower*lower+span*span)/(2*span);
            float lift=Mathf.Sqrt(Mathf.Max(.004f,upper*upper-a*a));
            Vector3 elbow=shoulder+axis*a+hinge*lift;
            if (span>reachMax) elbow=Vector3.Lerp(shoulder,hand,.51f);
            arm.Elbow.localPosition=elbow;
            AimBrokerLink(arm.Upper,shoulder,elbow);
            AimBrokerLink(arm.Forearm,elbow,hand);
            arm.Hand.localPosition=hand;
            arm.Hand.localRotation=Quaternion.LookRotation(handDirection,Vector3.up);
            for (int i=0; i<arm.Finger.Length; i++)
                arm.Finger[i].localRotation=Quaternion.Euler(Mathf.Lerp(-50+i*5,-6+i*2,open),Mathf.Lerp(0,(i-1.5f)*8,open),0);
        }

        private static void AimBrokerLink(Transform link, Vector3 start, Vector3 end)
        {
            Vector3 direction=end-start;
            link.localPosition=start;
            link.localRotation=Quaternion.LookRotation(direction,Vector3.up);
            link.localScale=new Vector3(1,1,direction.magnitude);
        }

        private static void PlaceBrokerDevice(Transform prop, bool active, Vector3 point, float time, float scale, int side)
        {
            prop.gameObject.SetActive(active);
            if (!active) return;
            prop.localPosition=point+Vector3.up*(Mathf.Sin(time*2.5f+side)*.074f);
            prop.localRotation=Quaternion.Euler(4*Mathf.Sin(time*2),side*(13+25*Mathf.Sin(time*.80f)),side*4);
            prop.localScale=Vector3.one*scale;
        }

        private void DrawBrokerTransfer(Transform organ,MeshRenderer beam,bool active,float age,float time,
            float begin,float catchAt,Vector3 source,Vector3 palm,Vector3 reservoir,float rising,Color color,bool heart)
        {
            float visible=active ? OpeningStoryboard.Window(age,begin,begin+.19f,3.75f,4.33f) : 0;
            organ.gameObject.SetActive(visible>.002f);
            float linking=active ? OpeningStoryboard.Window(age,begin-.14f,begin+.14f,catchAt-.20f,catchAt+.16f) : 0;
            beam.gameObject.SetActive(linking>.002f);
            if (linking>.002f)
            {
                AimBrokerLink(beam.transform,source,palm);
                Vector3 stretch=beam.transform.localScale;
                stretch.x=stretch.y=linking;
                beam.transform.localScale=stretch;
                Light(beam,color,.27f+.48f*linking);
            }
            if (visible<=.002f) return;
            float travel=OpeningStoryboard.Ease(begin+.07f,catchAt,age);
            organ.localPosition=Vector3.Lerp(source,palm,travel)+Vector3.up*(Mathf.Sin(travel*Mathf.PI)*.31f);
            organ.localPosition=Vector3.Lerp(organ.localPosition,reservoir,rising);
            organ.localRotation=Quaternion.Euler(9*Mathf.Sin(time*2),time*53,12);
            float beat=heart ? Mathf.Pow(Mathf.Max(0,Mathf.Sin(time*6.25f)),5)*.13f : 0;
            organ.localScale=Vector3.one*((.67f+beat)*visible*(1-.53f*rising));
        }
    }
}
