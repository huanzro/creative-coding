using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private sealed class ForestProp
        {
            public Transform Root, Upper, Lower, Threat, Clue;
            public MeshRenderer Lamp, Accent;
            public Transform[] Clearings;
            public Quaternion DeploymentHeading;
            public bool HeadingSet;
        }

        private const int FlightWisps = 16;
        private const float ForestStart = 25, LaneWidth = 2.1f;
        private readonly Dictionary<int, ForestProp> forestProps = new Dictionary<int, ForestProp>();
        private readonly List<int> unusedForestProps = new List<int>();
        private readonly Dictionary<ForestFeatureKind, Stack<ForestProp>> forestPool =
            new Dictionary<ForestFeatureKind, Stack<ForestProp>>();
        private readonly Dictionary<int, ForestFeatureKind> forestPropKinds = new Dictionary<int, ForestFeatureKind>();
        private readonly MeshRenderer[] forestWisps = new MeshRenderer[FlightWisps];
        private readonly Vector3[] wispPositions = new Vector3[FlightWisps];
        private readonly float[] wispTimes = new float[FlightWisps];
        private readonly Transform[] spiderLegs = new Transform[8];
        private readonly Transform[] watchingLegs = new Transform[8];
        private readonly Transform[] rescueFibers = new Transform[8];
        private readonly MeshRenderer[] rescueSparks = new MeshRenderer[7];
        private readonly Mesh[] clearingBeds = new Mesh[3];
        private Transform forestEffects, spider, watchingSpider, forestMouth;
        private ForestProp closingWeb, closingMaw, rescueFly;
        private Mesh silkMesh, webAnchorMesh, seedMesh, seedRootsMesh, mawStemMesh, mawBudMesh, mawLipMesh;
        private Mesh mawTeethMesh, mawThroatMesh, dormantSilkMesh, dormantMawMesh;
        private float lastWispAt = -1;
        private int wispIndex;

        public static Vector3 ForestPoint(float lane, float depth, float altitude)
        {
            float x = LaneWidth * lane, z = ForestStart + depth;
            return new Vector3(x, Ground(x,z) + .54f + 5.2f * altitude,z);
        }

        public static Vector3 ForestPoint(Journey journey, float lane, float depth, float altitude)
        {
            Vector3 point = ForestPoint(lane, depth, altitude);
            float oldGround = Ground(point.x, point.z);
            point.x += journey.ForestCenterAt(depth);
            point.y += Ground(point.x, point.z) - oldGround;
            if (journey.Branch == ForestBranch.Right) point.y += MachineFloor(depth);
            return point;
        }

        public static Vector3 MazeActor(Journey journey, float altitude) =>
            new Vector3(journey.ForestMazeX,journey.ForestMazeY + .54f + 5.2f*altitude,journey.ForestMazeZ);

        public static Quaternion ForestOrientation(Journey journey) =>
            Quaternion.Euler(0,journey.ForestMazeActive ? journey.ForestMazeHeading : 0,0);

        private static Vector3 ForestFeaturePoint(Journey journey, ForestFeature feature, bool animate)
        {
            if (!feature.InMaze)
                return ForestPoint(journey,animate?feature.LaneAt(journey.Time):feature.Lane,
                    animate?feature.DepthAt(journey.Time):feature.Depth,animate?feature.AltitudeAt(journey.Time):feature.Altitude);
            Vector3 point=new Vector3(feature.WorldX,feature.WorldY,feature.WorldZ);
            if (animate) point+=new Vector3(2.1f*(feature.LaneAt(journey.Time)-feature.Lane),
                5.2f*(feature.AltitudeAt(journey.Time)-feature.Altitude),feature.DepthAt(journey.Time)-feature.Depth);
            return point;
        }

        public static Vector3 ForestTrapSite(Journey journey)
        {
            if (!journey.ForestMazeActive)
                return ForestPoint(journey,journey.ForestTrapLane,journey.ForestTrapDepth,journey.ForestTrapAltitude);
            return journey.ForestContact != null && journey.ForestContact.InMaze
                ? ForestFeaturePoint(journey,journey.ForestContact,false) : MazeActor(journey,journey.ForestTrapAltitude);
        }

        public static Vector3 ForestDescentMouth(Journey journey)
        {
            if (!journey.ForestMazeActive) return ForestPoint(journey,journey.ForestTrapLane,journey.ForestTrapDepth+2.35f,0);
            return MazeActor(journey,0) + ForestOrientation(journey)*Vector3.forward*2.35f;
        }

        public static Vector4 ForestSink(Journey journey)
        {
            if (journey.Phase != Stage.ForestTransition || journey.ForestTrap == ForestTrapKind.Cable) return Vector4.zero;
            Vector3 point = ForestDescentMouth(journey);
            float start = journey.ForestTrap == ForestTrapKind.Web ? .53f : .28f;
            float radius = 1.38f * Ease(start,start+.19f,journey.Progress);
            return new Vector4(point.x,point.z,radius*radius,1);
        }

        public void ForestScenery(Journey journey, bool active, float time, Vector3 actor, float glide, bool split)
        {
            if (!active)
            {
                if (forestEffects != null) forestEffects.gameObject.SetActive(false);
                return;
            }
            if (forestEffects == null) BuildForestEffects();
            forestEffects.gameObject.SetActive(true);

            unusedForestProps.Clear();
            foreach (int id in forestProps.Keys) unusedForestProps.Add(id);
            int clearing=0;
            ForestFeature visibleSpider=null;
            float closestSpider=9.3f*9.3f;
            foreach (ForestFeature feature in journey.ForestFeatures)
            {
                bool isPlant = feature.Kind == ForestFeatureKind.Plant;
                int seed = isPlant ? clearing++ : -1;
                if (journey.Branch==ForestBranch.Right && journey.Distance<DoorDepth && feature.Depth>DoorDepth) continue;
                if ((feature.Collected && !isPlant) || feature.InMaze != journey.ForestMazeActive) continue;
                Vector3 featurePoint=ForestFeaturePoint(journey,feature,false);
                if (feature.InMaze ? (featurePoint-actor).sqrMagnitude > 29*29 :
                    feature.Depth < journey.Distance-8 || feature.Depth > journey.Distance+33) continue;
                unusedForestProps.Remove(feature.Id);
                if (!forestProps.TryGetValue(feature.Id,out ForestProp prop) || forestPropKinds[feature.Id]!=feature.Kind)
                {
                    if (prop!=null) ReturnProp(feature.Id);
                    prop=TakeProp(feature.Kind);
                    forestProps.Add(feature.Id,prop);
                    forestPropKinds.Add(feature.Id,feature.Kind);
                }
                PoseFeature(journey,feature,prop,time,seed,split);
                if(feature.Kind==ForestFeatureKind.Web && feature.Reveal>.08f && (featurePoint-actor).sqrMagnitude<closestSpider)
                {
                    visibleSpider=feature;
                    closestSpider=(featurePoint-actor).sqrMagnitude;
                }
            }
            foreach (int id in unusedForestProps) ReturnProp(id);

            PoseWatchingSpider(visibleSpider,journey,time);
            PoseForestThreat(journey,time,actor,split);
            PoseFlightWake(journey,time,actor,glide);
        }

        private void BuildForestEffects()
        {
            forestEffects=new GameObject("Forest flight, encounters and roots") {layer=Layer,hideFlags=HideFlags.HideAndDontSave}.transform;
            forestEffects.SetParent(root.transform,false);

            var silk=Geometry();
            var anchors=Geometry();
            const int spokes=12;
            for(int strand=0;strand<spokes;strand++)
            {
                float angle=strand*2*Mathf.PI/spokes;
                Vector3 outer=SilkPoint(angle,1,strand);
                silk.Tube(new[]{Vector3.zero,outer*.43f+Vector3.forward*.077f,outer},
                    new[]{.010f,.012f,.016f},5,new Color(.58f,.69f,.71f));
                if(strand%3==0)
                {
                    var tie=outer*1.64f+Vector3.forward*(strand%2==0?.48f:-.38f);
                    silk.Tube(new[]{outer,Vector3.Lerp(outer,tie,.53f)+Vector3.down*.10f,tie},
                        new[]{.016f,.012f,.018f},5,new Color(.48f,.58f,.58f));
                    anchors.Tube(new[]{tie+Vector3.forward*.54f,tie,tie*1.32f+Vector3.up*.24f},
                        new[]{.078f,.064f,.022f},6,new Color(.27f,.18f,.12f));
                }
                for(int ring=1;ring<=5;ring++)
                {
                    float next=(strand+1)*2*Mathf.PI/spokes;
                    float radius=.126f+ring*.161f;
                    Vector3 a=SilkPoint(angle,radius,strand), b=SilkPoint(next,radius,strand+1);
                    Vector3 sag=(a+b)*.45f+Vector3.down*(.016f+.021f*radius)+Vector3.forward*.073f;
                    silk.Tube(new[]{a,sag,b},new[]{.010f,.008f,.010f},5,new Color(.62f,.73f,.76f));
                }
            }
            silkMesh=silk.Finish("Irregular wet silk threads");
            webAnchorMesh=anchors.Finish("Snagged canopy twigs");

            var clue=Geometry();
            var glint=new Color(.39f,.48f,.44f);
            clue.Tube(new[]{new Vector3(-.18f,.28f,.02f),new Vector3(-.038f,.085f,-.01f),new Vector3(.022f,-.079f,.017f)},
                new[]{.0055f,.004f,.0025f},5,glint);
            clue.Tube(new[]{new Vector3(-.038f,.085f,-.01f),new Vector3(.086f,.102f,.015f),new Vector3(.19f,.172f,.027f)},
                new[]{.0033f,.003f,.0017f},5,glint);
            clue.Rock(new Vector3(.012f,-.050f,.014f),new Vector3(.012f,.017f,.008f),glint,.3f,4,6,.10f);
            dormantSilkMesh=clue.Finish("Short folded strand and dew point");

            var seed=Geometry();
            var roots=Geometry();
            seed.Rock(new Vector3(0,-.34f,0),new Vector3(.59f,.19f,.57f),new Color(.37f,.46f,.33f),.51f,6,12,.24f);
            for(int i=0;i<5;i++)
            {
                float angle=i*2*Mathf.PI/5;
                Vector3 edge=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                seed.Tube(new[]{edge*.62f+Vector3.down*.46f,edge*.36f+Vector3.down*.16f,edge*.18f+Vector3.up*.04f},
                    new[]{.116f,.093f,.017f},8,new Color(.32f,.43f,.27f));
                roots.Tube(new[]{edge*.10f,edge*.37f+Vector3.down*.10f,edge*.69f+Vector3.down*.37f,
                    edge*1.03f+Vector3.down*.40f},new[]{.024f,.021f,.016f,.008f},5,Warm);
            }
            Ring(roots,Vector3.up*.024f,.50f,.012f,Color.Lerp(Warm,Jade,.34f),Quaternion.Euler(90,0,0),32,1);
            seedMesh=seed.Finish("Moss cradle around the seed");
            seedRootsMesh=roots.Finish("Five illuminated root veins");
            BuildClearingBeds();

            var stem=Geometry();
            stem.Tube(new[]{new Vector3(.15f,-.54f,.38f),new Vector3(.25f,-.25f,.34f),new Vector3(.10f,-.06f,.10f),Vector3.zero},
                new[]{.22f,.173f,.14f,.21f},13,new Color(.24f,.37f,.17f));
            for(int i=0;i<7;i++)
            {
                float angle=i*2*Mathf.PI/7;
                Vector3 radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Vector3 baseLeaf=Vector3.down*.38f+radial*.19f;
                stem.Leaf(baseLeaf,baseLeaf+radial*(.59f+.18f*Mathf.Sin(i)) + Vector3.up*(.33f+.15f*Mathf.Cos(i)),
                    Vector3.Cross(radial,Vector3.up),.18f,new Color(.31f,.42f,.20f));
            }
            mawStemMesh=stem.Finish("Curved root and shield leaves");
            var bud=Geometry();
            var lip=Geometry();
            var teeth=Geometry();
            var throat=Geometry();
            bud.Rock(new Vector3(0,.075f,-.29f),new Vector3(.57f,.137f,.52f),new Color(.43f,.26f,.29f),.13f,6,13,.18f);
            throat.Rock(new Vector3(0,-.02f,-.35f),new Vector3(.43f,.039f,.42f),new Color(.24f,.095f,.13f),.05f,4,11,.08f);
            for(int i=0;i<9;i++)
            {
                float a=Mathf.PI*(i+1)/10;
                Vector3 rim=new Vector3(Mathf.Cos(a)*.51f,.019f,-.31f-Mathf.Sin(a)*.49f);
                Vector3 tip=rim*.88f+new Vector3(0,-(.13f+.055f*Mathf.Sin(i*2)),.028f);
                teeth.Tube(new[]{rim,Vector3.Lerp(rim,tip,.54f),tip},new[]{.038f,.021f,.002f},5,new Color(.79f,.72f,.46f));
                lip.Tube(new[]{rim,new Vector3(rim.x*.74f,.081f,rim.z*.69f),new Vector3(rim.x*.44f,.087f,rim.z*.31f)},
                    new[]{.018f,.013f,.006f},5,new Color(.73f,.37f,.33f));
            }
            mawBudMesh=bud.Finish("Velvety lobes of the pitcher");
            mawLipMesh=lip.Finish("Serrated petal veins");
            mawTeethMesh=teeth.Finish("Tapered nectar spines");
            mawThroatMesh=throat.Finish("Pitcher throat");

            clue=Geometry();
            var folded=new Color(.27f,.31f,.135f);
            clue.Leaf(new Vector3(-.071f,0,.15f),new Vector3(.012f,.078f,-.11f),Vector3.right,.051f,folded);
            clue.Leaf(new Vector3(.057f,-.019f,.16f),new Vector3(-.021f,.053f,-.09f),Vector3.right,.045f,
                new Color(.39f,.26f,.18f));
            clue.Tube(new[]{new Vector3(.009f,.028f,.079f),new Vector3(.007f,.072f,-.045f),new Vector3(.012f,.079f,-.106f)},
                new[]{.005f,.005f,.002f},5,new Color(.49f,.34f,.23f));
            dormantMawMesh=clue.Finish("Seam in the forest floor");

            closingWeb=MakeProp(ForestFeatureKind.Web);
            closingMaw=MakeProp(ForestFeatureKind.Maw);
            spider=BuildCanopySpider(spiderLegs);
            watchingSpider=BuildCanopySpider(watchingLegs);
            forestMouth=BuildForestHollow();
            rescueFly=MakeProp(ForestFeatureKind.Amber);
            var fiberMesh=Geometry();
            fiberMesh.Tube(new[]{Vector3.down*.5f,Vector3.up*.5f},new[]{1f,1f},5,Color.white);
            Mesh fiber=fiberMesh.Finish("Silk strand");
            for(int i=0;i<rescueFibers.Length;i++)
            {
                rescueFibers[i]=Piece("Taut rescue silk",fiber,glow,forestEffects,Vector3.zero);
                Light(rescueFibers[i].GetComponent<MeshRenderer>(),new Color(.53f,.64f,.66f),.18f);
            }
            for(int i=0;i<forestWisps.Length;i++)
            {
                forestWisps[i]=Lamp("A trace of airborne light",Vector3.zero,.01f,Warm,.1f);
                forestWisps[i].transform.SetParent(forestEffects,false);
                wispTimes[i]=float.NegativeInfinity;
            }
            for(int i=0;i<rescueSparks.Length;i++)
            {
                rescueSparks[i]=Lamp("Released gold firefly",Vector3.zero,.06f,Warm,.72f);
                rescueSparks[i].transform.SetParent(forestEffects,false);
            }
        }

        private void BuildClearingBeds()
        {
            var brook=Geometry();
            var stump=Geometry();
            var stones=Geometry();
            for(int i=0;i<9;i++)
            {
                float a=i*2*Mathf.PI/9;
                Vector3 radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                float scatter=.10f*Mathf.Sin(i*13.91f);
                brook.Rock(radial*(.79f+scatter)+Vector3.down*.49f,
                    new Vector3(.24f,.09f,.22f),new Color(.31f,.46f,.41f),i*.33f,4,8,.18f);
                if (i!=2 && i!=3)
                {
                    Vector3 rim=radial*(.73f+scatter)+Vector3.down*.33f;
                    stump.Tube(new[]{radial*(1.47f+scatter)+Vector3.down*.57f,rim+radial*.18f,
                        rim+Vector3.up*(.23f+.09f*Mathf.Sin(i*8.9f)),rim*.94f+Vector3.up*.32f},
                        new[]{.039f,.14f,.096f,.013f},7,new Color(.28f,.29f,.17f));
                }
                float height=.29f+.16f*Mathf.Sin(i*7.9f);
                stones.Rock(radial*(.91f+scatter)+Vector3.up*(height*.57f-.49f),
                    new Vector3(.20f,height,.21f),new Color(.40f,.43f,.37f),i*.27f,5,8,.32f);
            }
            clearingBeds[0]=brook.Finish("Water-worn stepping stones at the moss basin");
            clearingBeds[1]=stump.Finish("Sprouting stump and separating roots");
            clearingBeds[2]=stones.Finish("Fallen standing stones at the last seed");
        }

        private static Vector3 SilkPoint(float angle,float radius,int strand)
        {
            float irregular=1+.10f*Mathf.Sin(strand*2.4f);
            return new Vector3(1.19f*Mathf.Cos(angle)*radius*irregular,
                1.05f*Mathf.Sin(angle)*radius, .09f*Mathf.Sin(strand*3.2f)*radius);
        }

        private ForestProp TakeProp(ForestFeatureKind kind)
        {
            if(!forestPool.TryGetValue(kind,out Stack<ForestProp> pool))
            {
                pool=new Stack<ForestProp>();
                forestPool.Add(kind,pool);
            }
            ForestProp prop=pool.Count>0 ? pool.Pop() : MakeProp(kind);
            prop.HeadingSet=false;
            prop.Root.gameObject.SetActive(true);
            return prop;
        }

        private void ReturnProp(int id)
        {
            ForestProp item=forestProps[id];
            item.Root.gameObject.SetActive(false);
            forestPool[forestPropKinds[id]].Push(item);
            forestProps.Remove(id);
            forestPropKinds.Remove(id);
        }

        private Transform Group(string name,Transform parent)
        {
            var group=new GameObject(name) {layer=Layer,hideFlags=HideFlags.HideAndDontSave};
            group.transform.SetParent(parent,false);
            return group.transform;
        }

        private Transform Piece(string name,Mesh mesh,Material material,Transform parent,Vector3 at)
        {
            Transform piece=Put(name,mesh,material,at,Vector3.one);
            piece.SetParent(parent,false);
            return piece;
        }

        private ForestProp MakeProp(ForestFeatureKind kind)
        {
            var prop=new ForestProp {Root=Group(kind.ToString(),forestEffects)};
            switch(kind)
            {
                case ForestFeatureKind.Web:
                    prop.Threat=Group("Spring-loaded web",prop.Root);
                    prop.Clue=Piece("Dormant silk glint",dormantSilkMesh,glow,prop.Root,Vector3.zero);
                    prop.Clue.gameObject.SetActive(false);
                    prop.Lamp=Piece("Moonlit silk",silkMesh,glow,prop.Threat,Vector3.zero).GetComponent<MeshRenderer>();
                    prop.Accent=Piece("Canopy anchors",webAnchorMesh,wood,prop.Threat,Vector3.zero).GetComponent<MeshRenderer>();
                    break;
                case ForestFeatureKind.Maw:
                    prop.Threat=Group("Subterranean pitcher",prop.Root);
                    prop.Clue=Piece("Folded moss-coloured petal tip",dormantMawMesh,leaves,prop.Root,Vector3.zero);
                    prop.Clue.gameObject.SetActive(false);
                    Piece("Trailing stem",mawStemMesh,leaves,prop.Threat,Vector3.zero);
                    prop.Upper=Group("Fleshy upper petal",prop.Threat);
                    prop.Lower=Group("Fleshy lower petal",prop.Threat);
                    foreach(Transform jaw in new[]{prop.Upper,prop.Lower})
                    {
                        Piece("Petal",mawBudMesh,leaves,jaw,Vector3.zero);
                        Piece("Veins",mawLipMesh,glow,jaw,Vector3.zero);
                        Piece("Nectar spines",mawTeethMesh,stone,jaw,Vector3.zero);
                        Piece("Throat",mawThroatMesh,leaves,jaw,Vector3.zero);
                    }
                    prop.Lower.localScale=new Vector3(1,-1,1);
                    break;
                case ForestFeatureKind.Plant:
                    Piece("Root cradle",seedMesh,stone,prop.Root,Vector3.zero);
                    prop.Accent=Piece("Cradle veins",seedRootsMesh,glow,prop.Root,Vector3.zero).GetComponent<MeshRenderer>();
                    prop.Lamp=Lamp("Waiting seed bead",Vector3.zero,.095f,Warm,.45f);
                    prop.Lamp.transform.SetParent(prop.Root,false);
                    prop.Clearings=new Transform[clearingBeds.Length];
                    for(int i=0;i<clearingBeds.Length;i++)
                        prop.Clearings[i]=Piece(clearingBeds[i].name,clearingBeds[i],i==1?wood:stone,prop.Root,Vector3.zero);
                    break;
                default:
                    prop.Lamp=Lamp("Firefly abdomen",Vector3.zero,.085f,Warm,1.7f);
                    prop.Lamp.transform.SetParent(prop.Root,false);
                    prop.Upper=Lamp("Firefly left wing",Vector3.zero,.06f,Jade,.13f).transform;
                    prop.Lower=Lamp("Firefly right wing",Vector3.zero,.06f,Jade,.13f).transform;
                    prop.Upper.SetParent(prop.Root,false);
                    prop.Lower.SetParent(prop.Root,false);
                    break;
            }
            return prop;
        }

        private void PoseFeature(Journey journey,ForestFeature feature,ForestProp prop,float time,int seed,bool split)
        {
            Vector3 at=ForestFeaturePoint(journey,feature,false);
            float beat=time*2.9f+feature.Id*1.13f;
            prop.Root.localPosition=at;
            prop.Root.localRotation=Quaternion.identity;
            prop.Root.localScale=Vector3.one;
            switch(feature.Kind)
            {
                case ForestFeatureKind.Web:
                    PoseWebReveal(journey,feature,prop,beat);
                    break;
                case ForestFeatureKind.Maw:
                    PoseMawReveal(journey,feature,prop,beat);
                    break;
                case ForestFeatureKind.Plant:
                    for(int i=0;i<prop.Clearings.Length;i++)
                        prop.Clearings[i].gameObject.SetActive(!feature.InMaze && journey.Branch!=ForestBranch.Right && i==seed%prop.Clearings.Length);
                    float charge=feature.Collected ? 1 : journey.ForestPlantTargetId==feature.Id ? journey.ForestPlantCharge : 0;
                    prop.Lamp.transform.localPosition=Vector3.up*(.09f+.05f*Mathf.Sin(beat));
                    prop.Lamp.transform.localScale=Vector3.one*(.114f+.060f*charge);
                    Light(prop.Lamp,Color.Lerp(Jade,Warm,charge),.48f+.72f*charge+.18f*Mathf.Sin(beat));
                    Light(prop.Accent,Color.Lerp(Jade,Warm,charge),.085f+charge*.81f);
                    break;
                default:
                    ForestFlyView fly = ForestFlyView.Read(feature,journey.Time);
                    bool red=fly.red;
                    prop.Root.localPosition=ForestFeaturePoint(journey,feature,true);
                    prop.Root.localRotation=Quaternion.Euler(0,22*Mathf.Sin(beat*.73f),14*Mathf.Cos(beat));
                    float flare=.72f+.22f*Mathf.Sin(beat);
                    if(red) flare=.34f+.66f*fly.ScarletPulse(journey.Time);
                    prop.Lamp.transform.localScale=new Vector3(.075f,.083f,.105f)*(1+flare*.30f);
                    float handoff = split ? ForestFlyView.RelativeTo(feature,journey).visibility : 0;
                    float backdrop=journey.Phase==Stage.ForestTrapped ? 1-.65f*Ease(.38f,1.9f,journey.Age) : 1;
                    Light(prop.Lamp,red?Red:Warm,(1.4f+.90f*flare)*(1-.52f*handoff)*backdrop);
                    float flutter=Mathf.Sin(time*35+feature.Id*2.1f);
                    PoseWing(prop.Upper,-1,flutter);
                    PoseWing(prop.Lower,1,flutter);
                    break;
            }
        }

        private static bool BeginThreatReveal(Journey journey, ForestProp prop,float reveal)
        {
            bool deploying=reveal>.075f;
            prop.Clue.gameObject.SetActive(reveal<.22f);
            prop.Threat.gameObject.SetActive(deploying);
            if(!deploying)
            {
                prop.HeadingSet=false;
                return false;
            }
            if (!prop.HeadingSet)
            {
                prop.DeploymentHeading=ForestOrientation(journey);
                prop.HeadingSet=true;
            }
            return true;
        }

        private void PoseWebReveal(Journey journey,ForestFeature feature,ForestProp prop,float beat)
        {
            float reveal=feature.Reveal;
            prop.Clue.localPosition=new Vector3(.59f,.39f+.019f*Mathf.Sin(beat*.61f),.19f);
            prop.Clue.localRotation=Quaternion.Euler(12,0,Mathf.Sin(beat*.42f)*8);
            if (!BeginThreatReveal(journey,prop,reveal)) return;

            float stretch=Ease(.11f,.77f,reveal);
            float ring=Mathf.Sin(stretch*Mathf.PI);
            prop.Threat.localPosition=prop.DeploymentHeading*Vector3.Lerp(new Vector3(.58f,.39f,.30f),
                new Vector3(0,.02f,-.17f),stretch);
            prop.Threat.localScale=new Vector3(Mathf.Lerp(.13f,1.18f,stretch)+.19f*ring,
                Mathf.Lerp(.31f,1.13f,stretch)+.12f*ring,1);
            prop.Threat.localRotation=prop.DeploymentHeading*Quaternion.Euler(44*(1-stretch)-12*ring,
                78*(1-stretch)-16*ring,11*(1-stretch)+Mathf.Sin(beat*.51f)*2.5f*stretch);
            Light(prop.Lamp,Color.white,.13f+.19f*stretch+.21f*ring);
        }

        private static void PoseMawReveal(Journey journey,ForestFeature feature,ForestProp prop,float beat)
        {
            float reveal=feature.Reveal;
            prop.Clue.localPosition=new Vector3(.081f,-.52f-feature.Altitude*5.2f,.027f);
            prop.Clue.localRotation=Quaternion.Euler(4*Mathf.Sin(beat),-14,5);
            if (!BeginThreatReveal(journey,prop,reveal)) return;

            float spring=Ease(.10f,.64f,reveal), jaw=Ease(.21f,.80f,reveal);
            float overshoot=Mathf.Sin(spring*Mathf.PI);
            prop.Threat.localScale=new Vector3(Mathf.Lerp(.34f,1.44f,spring),Mathf.Lerp(.39f,1.45f,spring)+.24f*overshoot,
                Mathf.Lerp(.40f,1.53f,spring));
            prop.Threat.localPosition=prop.DeploymentHeading*new Vector3(-.26f*(1-spring),
                Mathf.Lerp(-1.11f,-.21f,spring)+.34f*overshoot,Mathf.Lerp(.41f,-.11f,spring));
            prop.Threat.localRotation=prop.DeploymentHeading*Quaternion.Euler(Mathf.Lerp(38,0,spring)-13*overshoot,
                17*(1-spring),-14*overshoot+Mathf.Sin(beat*.53f)*3*spring);
            OpenMaw(prop,2+48*jaw+19*Mathf.Sin(jaw*Mathf.PI)+Mathf.Sin(beat)*3*jaw);
        }

        private static void PoseWing(Transform wing,int side,float flutter)
        {
            wing.localPosition=new Vector3(side*.089f,.034f+.016f*flutter,-.012f);
            wing.localRotation=Quaternion.Euler(-15+flutter*22,side*34,-side*(23-flutter*37));
            wing.localScale=new Vector3(.096f,.009f,.044f);
        }

        private static void OpenMaw(ForestProp prop,float degrees)
        {
            prop.Upper.localRotation=Quaternion.Euler(degrees,0,0);
            prop.Lower.localRotation=Quaternion.Euler(-degrees*.76f,0,0);
        }

        private Transform BuildCanopySpider(Transform[] limbs)
        {
            Transform creature=Group("Canopy spider",forestEffects);
            var body=Geometry();
            var shell=new Color(.23f,.14f,.11f);
            body.Rock(new Vector3(0,.078f,.29f), new Vector3(.42f,.33f,.59f),shell,.30f,8,13,.14f);
            body.Rock(new Vector3(0,-.025f,-.31f), new Vector3(.29f,.23f,.33f),new Color(.30f,.18f,.12f),.41f,6,12,.13f);
            for(int i=0;i<5;i++)
            {
                float along=.08f+i*.16f;
                body.Tube(new[]{new Vector3(-.24f,.28f,along-.07f),new Vector3(0,.395f-i*.01f,along),
                    new Vector3(.24f,.28f,along-.07f)},new[]{.022f,.031f,.022f},6,new Color(.53f,.31f,.17f));
            }
            Piece("Striped chitin",body.Finish("Canopy spider abdomen"),wood,creature,Vector3.zero);
            for(int i=0;i<4;i++)
            {
                MeshRenderer eye=Lamp("Spider eye",new Vector3((i-1.5f)*.10f,.052f+.035f*(i%2),-.59f),
                    .038f,Color.Lerp(Red,Warm,.35f),.71f);
                eye.transform.SetParent(creature,false);
            }
            for(int row=0;row<4;row++)
            for(int side=-1;side<=1;side+=2)
            {
                var leg=Geometry();
                float spread=(row-1.5f)*.43f;
                leg.Tube(new[]{Vector3.zero,new Vector3(side*.41f,.32f,spread*.39f),new Vector3(side*.81f,.42f,spread*.86f),
                    new Vector3(side*.99f,-.15f,spread*1.40f),new Vector3(side*1.13f,-.29f,spread*1.61f)},
                    new[]{.078f,.062f,.039f,.027f,.009f},8,WorldMesh.Shade(shell,1.12f));
                limbs[row*2+(side+1)/2]=Piece("Jointed spider leg",leg.Finish("Spider leg "+row+" "+side),wood,
                    creature,new Vector3(side*.19f,-.034f,.21f-row*.17f));
            }
            return creature;
        }

        private Transform BuildForestHollow()
        {
            Transform hollow=Group("Root-lined earth opening",forestEffects);
            var wall=Geometry();
            var roots=Geometry();
            var glowworms=Geometry();
            Color loam=new Color(.13f,.102f,.092f), timber=new Color(.31f,.22f,.15f);
            for(int ring=0;ring<6;ring++)
            for(int rock=0;rock<16;rock++)
            {
                float a=(rock+ring*.52f)*2*Mathf.PI/16;
                float radius=1.38f+.072f*Mathf.Sin(rock*2.1f+ring);
                var position=new Vector3(Mathf.Cos(a)*radius,-ring*.67f,Mathf.Sin(a)*radius);
                wall.Rock(position,new Vector3(.29f,.44f,.29f),WorldMesh.Shade(loam,.91f+.26f*Mathf.Sin(a*3+ring)),
                    (rock+ring*2)*.17f,5,8,.33f);
                if(ring>1 && (ring+rock)%5==0)
                    glowworms.Rock(position*.90f+Vector3.down*.17f,new Vector3(.045f,.035f,.041f),
                        Color.Lerp(Jade,Warm,.19f*ring),a,3,6,.12f);
            }
            for(int i=0;i<11;i++)
            {
                float a=i*2*Mathf.PI/11;
                Vector3 edge=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                roots.Tube(new[]{edge*1.84f+Vector3.up*.018f,edge*1.32f+Vector3.up*.11f,edge*1.18f+Vector3.down*.77f,
                    edge*1.25f+Vector3.down*1.73f,edge*1.35f+Vector3.down*2.87f},
                    new[]{.044f,.126f,.071f,.060f,.018f},8,WorldMesh.Shade(timber,.93f+.23f*Mathf.Sin(i*1.31f)));
            }
            Piece("Broken forest earth",wall.Finish("Uneven earthen shaft"),stone,hollow,Vector3.zero);
            Piece("Earth roots",roots.Finish("Pendant twisted roots"),wood,hollow,Vector3.zero);
            Piece("Glowworms beneath roots",glowworms.Finish("Deep glowworms"),glow,hollow,Vector3.zero);
            return hollow;
        }

        private void PoseWatchingSpider(ForestFeature feature,Journey journey,float time)
        {
            watchingSpider.gameObject.SetActive(feature!=null && journey.Phase!=Stage.ForestTrapped);
            if (!watchingSpider.gameObject.activeSelf) return;
            float reveal=feature.Reveal;
            Vector3 anchor=ForestFeaturePoint(journey,feature,false);
            Quaternion heading=forestProps[feature.Id].DeploymentHeading;
            float lunge=Ease(.11f,.66f,reveal), check=Ease(.57f,.97f,reveal);
            watchingSpider.localPosition=anchor+heading*(Vector3.Lerp(new Vector3(1.11f,1.68f,.72f),
                new Vector3(.26f,.70f,.34f),lunge)+new Vector3(.12f,.16f,.36f)*check);
            watchingSpider.localRotation=heading*Quaternion.Euler(34-33*lunge+12*check,-29+44*lunge,-17*lunge+9*check);
            watchingSpider.localScale=Vector3.one*Mathf.Lerp(.38f,1.08f,lunge);
            for(int i=0;i<watchingLegs.Length;i++)
            {
                float side=i%2==0?-1:1;
                float kick=Mathf.Sin(reveal*16+i*.86f)*(1-.81f*check);
                watchingLegs[i].localRotation=Quaternion.Euler(11*kick+2.5f*Mathf.Sin(time*8+i),side*kick*14,
                    side*(18*lunge+kick*11));
            }
        }

        private void PoseForestThreat(Journey j,float time,Vector3 actor,bool split)
        {
            bool trapped=j.Phase==Stage.ForestTrapped;
            bool falling=j.Phase==Stage.ForestFalling;
            bool exit=j.Phase==Stage.ForestTransition && j.ForestTrap!=ForestTrapKind.Cable;
            bool catchNow=falling && j.Progress>=.26f;
            bool web=(trapped || exit || catchNow) && j.ForestTrap==ForestTrapKind.Web;
            bool maw=(trapped || exit || catchNow) && j.ForestTrap==ForestTrapKind.Maw;
            closingWeb.Root.gameObject.SetActive(web);
            closingMaw.Root.gameObject.SetActive(maw);
            spider.gameObject.SetActive((exit || catchNow) && web);
            forestMouth.gameObject.SetActive(exit);
            float t = trapped ? j.Age : falling ? Mathf.Clamp01((j.Progress-.26f)/.74f) : j.Progress;
            Vector3 catchAt=ForestTrapSite(j);
            Vector3 mouthAt=ForestDescentMouth(j)-Vector3.up*.54f;
            if(falling) catchAt=actor+(web ? Vector3.up*.85f : Vector3.down*.69f);
            bool rescue=trapped && j.ForestHasRescue;
            PoseRescuingLight(j,actor,time,rescue,split);
            if (rescue) return;
            if(web)
            {
                float draw=trapped?Ease(0,.33f,t):Ease(.13f,.34f,t);
                closingWeb.Root.localPosition=Vector3.Lerp(catchAt,actor,draw)+new Vector3(0,0,-.24f);
                closingWeb.Root.localRotation=Quaternion.Euler(12*draw,Mathf.Sin(time*5)*9,Mathf.Sin(time*9)*5);
                closingWeb.Root.localScale=new Vector3(1.03f-.54f*draw,1.04f-.21f*draw,1+.83f*draw);
                Light(closingWeb.Lamp,Color.white,.21f+.10f*Mathf.Sin(time*16));
                closingWeb.Accent.gameObject.SetActive(draw<.24f);
            }
            if(maw)
            {
                float sprout=trapped ? Ease(0,.36f,t) : Ease(.11f,.30f,t);
                float swallow=trapped ? Ease(.24f,.52f,t) : Ease(.19f,.41f,t);
                float burrow=exit ? Ease(.49f,1,t) : 0;
                closingMaw.Root.localPosition=Vector3.Lerp(catchAt+Vector3.down*.58f,actor+Vector3.forward*.34f,sprout)+
                    Vector3.down*(.18f+.37f*burrow);
                closingMaw.Root.localRotation=Quaternion.Euler(11+burrow*79,Mathf.Sin(time*2.5f)*8,Mathf.Sin(time*7)*3);
                closingMaw.Root.localScale=Vector3.one*Mathf.Lerp(.18f,1.76f,sprout);
                OpenMaw(closingMaw,57*sprout*(1-swallow)+13);
            }
            if(exit)
            {
                Vector4 sink=ForestSink(j);
                float size=Mathf.Sqrt(sink.z)/1.38f;
                forestMouth.localPosition=mouthAt;
                forestMouth.localScale=new Vector3(Mathf.Max(.014f,size),1,Mathf.Max(.014f,size));
            }
            if((exit || catchNow) && web)
            {
                float leap=Ease(.22f,.63f,t);
                float dive=Ease(.58f,1,t);
                Vector3 canopy=catchAt+new Vector3(j.ForestTrapLane==1?1.8f:-1.8f,3.10f,.86f);
                Vector3 landing=falling?actor+new Vector3(0,.32f,.46f):mouthAt+new Vector3(0,.34f,-.24f);
                spider.localPosition=Vector3.Lerp(canopy,landing,leap)+Vector3.up*(Mathf.Sin(leap*Mathf.PI)*1.02f-3.55f*dive);
                spider.localRotation=Quaternion.Euler(-18*Mathf.Sin(leap*Mathf.PI)+54*dive,35*(1-leap),Mathf.Sin(t*26)*4);
                spider.localScale=Vector3.one*Mathf.Lerp(.75f,1.02f,leap);
                for(int i=0;i<spiderLegs.Length;i++)
                {
                    float side=i%2==0?-1:1, gait=Mathf.Sin(time*14+i*1.13f);
                    spiderLegs[i].localRotation=Quaternion.Euler(gait*12,side*(gait*17-14*Mathf.Sin(leap*Mathf.PI)),side*gait*13);
                }
            }
        }

        private void PoseRescuingLight(Journey journey,Vector3 actor,float time,bool active,bool split)
        {
            bool web=journey.ForestTrap==ForestTrapKind.Web;
            float t=journey.Age;
            var shot=ForestRescueStoryboard.Sample(active?t:0,web);
            Quaternion orientation=ForestOrientation(journey);
            Vector3 fly=actor+orientation*shot.fly;
            rescueFly.Root.gameObject.SetActive(active && shot.bright>.005f);
            if(rescueFly.Root.gameObject.activeSelf)
            {
                rescueFly.Root.localPosition=fly;
                rescueFly.Root.localRotation=orientation*Quaternion.Euler(0,21*Mathf.Sin(t*8),14*Mathf.Sin(t*14));
                rescueFly.Root.localScale=Vector3.one*Mathf.Lerp(.43f,1,shot.bright);
                Light(rescueFly.Lamp, Warm,shot.bright*(split?1.4f:2.8f));
                float wingBeat=Mathf.Sin(t*41)*(1-Ease(6.89f,7.60f,t));
                PoseWing(rescueFly.Upper,-1,wingBeat);
                PoseWing(rescueFly.Lower,1,wingBeat);
            }
            for(int i=0;i<rescueSparks.Length;i++)
            {
                bool show=active && shot.splinters>.015f;
                var spark=rescueSparks[i];
                spark.gameObject.SetActive(show);
                if(!show) continue;
                float from=Mathf.Max(0,t-6.70f-i*.047f), angle=i*2.39996f;
                spark.transform.localPosition=fly+orientation*new Vector3(Mathf.Cos(angle)*from*.31f,
                    -.08f-from*(.18f+i*.041f),Mathf.Sin(angle)*from*.26f);
                spark.transform.localScale=Vector3.one*(.009f+.014f*shot.splinters);
                Light(spark,Warm,shot.splinters*.7f);
            }
            for(int i=0;i<rescueFibers.Length;i++)
            {
                float breakAt=6.56f+i*.17f;
                Transform line=rescueFibers[i];
                bool show=active && web && t>.24f+i*.085f && t<breakAt+.67f;
                line.gameObject.SetActive(show);
                if(!show) continue;
                float angle=i*2.39996f;
                Vector3 held=actor+orientation*new Vector3(Mathf.Sin(angle)*.29f,.38f-i*.095f,.03f);
                Vector3 heldOut=actor+orientation*new Vector3(Mathf.Sin(angle)*1.30f,.76f-i*.15f,.73f);
                float recoil=Ease(breakAt,breakAt+.52f,t);
                Vector3 inner=Vector3.Lerp(held,heldOut,recoil*.87f);
                inner+=orientation*Vector3.up*(Mathf.Sin(t*18+i)*shot.effort*.031f - recoil*.19f);
                Vector3 delta=heldOut-inner;
                line.localPosition=(inner+heldOut)*.5f;
                line.localRotation=Quaternion.FromToRotation(Vector3.up,delta);
                float thread=.0038f*(1-recoil)+.0014f;
                line.localScale=new Vector3(thread,delta.magnitude,thread);
            }
            if(!active) return;
            if(web)
            {
                closingWeb.Root.localPosition=actor+orientation*new Vector3(.05f,.08f,-.32f);
                closingWeb.Root.localRotation=orientation*Quaternion.Euler(18*shot.bind,26*shot.effort,4*Mathf.Sin(t*5));
                closingWeb.Root.localScale=new Vector3(1-.58f*shot.bind+.39f*shot.free,.92f,1+shot.effort*.42f)*
                    (1-.96f*Ease(6.53f,8.43f,t));
                closingWeb.Accent.gameObject.SetActive(t<.8f);
                Light(closingWeb.Lamp,Color.Lerp(Pale,Warm,shot.bright),.24f+shot.bright*.15f);
            }
            else
            {
                float release=Ease(6.26f,7.54f,t);
                closingMaw.Root.localPosition=actor+orientation*new Vector3(.17f,-.72f-.66f*shot.free,.45f);
                closingMaw.Root.localRotation=orientation*Quaternion.Euler(10+18*release,29+7*shot.effort,11*shot.effort);
                closingMaw.Root.localScale=Vector3.one*Mathf.Lerp(1.08f,1.64f,shot.bind)*(1-.65f*Ease(7.88f,9.27f,t));
                closingMaw.Upper.localRotation=Quaternion.Euler(Mathf.Lerp(28,6,shot.bind)+67*release,0,0);
                closingMaw.Lower.localRotation=Quaternion.Euler(-20*(1-shot.bind)-49*Ease(6.51f,7.91f,t),0,0);
            }
        }

        private void PoseFlightWake(Journey journey,float time,Vector3 actor,float glide)
        {
            Vector3 wake=actor+new Vector3(0,-.27f,-.28f);
            if(time<lastWispAt || (lastWispAt>=0 && (wake-wispPositions[wispIndex]).sqrMagnitude>16))
            {
                lastWispAt=-1;
                for(int i=0;i<wispTimes.Length;i++) wispTimes[i]=float.NegativeInfinity;
            }
            bool leaveWake=journey.ForestAirborne && (journey.Phase==Stage.ForestExplore || journey.Phase==Stage.ForestPlanting);
            if(leaveWake && time-lastWispAt>=.043f)
            {
                wispIndex=(wispIndex+1)%forestWisps.Length;
                wispPositions[wispIndex]=wake;
                wispTimes[wispIndex]=time;
                lastWispAt=time;
            }
            for(int i=0;i<forestWisps.Length;i++)
            {
                float age=time-wispTimes[i];
                float vivid=1-Mathf.Clamp01(age/.69f);
                forestWisps[i].gameObject.SetActive(vivid>.008f);
                if(vivid<=.008f) continue;
                forestWisps[i].transform.localPosition=wispPositions[i]+new Vector3(Mathf.Sin(i*2.4f)*age*.27f,-age*.21f,-age*.38f);
                float width=vivid*(.022f+.013f*glide+.014f*journey.Voice);
                forestWisps[i].transform.localScale=new Vector3(width,width*.70f,width*2.14f);
                Light(forestWisps[i],Color.Lerp(Warm,Jade,.25f),(vivid*.75f+.09f)*(1+journey.Voice*.32f));
            }
        }

        private static float Ease(float start,float end,float time) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,time));
    }
}
