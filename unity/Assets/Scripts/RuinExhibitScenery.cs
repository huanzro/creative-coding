using System;
using UnityEngine;
using Random = System.Random;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private sealed class ExhibitDisplay
        {
            public Transform Root, Body, Halo, Title;
            public MeshRenderer HaloRenderer, LetterRenderer;
            public float Depth, Height, Side, Size, Phase;
        }

        private static readonly string[] ExhibitTitles = {
            "GREAT WALL", "GIZA", "COLOSSEUM", "PARTHENON", "CHICHEN ITZA", "PRINTING PRESS",
            "STEAM TRAIN", "ASTROLABE", "ORBITAL LAB", "SEISMOSCOPE", "CUNEIFORM", "SAILING SHIP"
        };
        private static readonly Color LimeStone = new Color(.91f,.86f,.72f);
        private static readonly Color PaleJade = new Color(.53f,.81f,.69f);
        private static readonly Color FiredBronze = new Color(.77f,.50f,.28f);
        private static readonly Color ExhibitBlue = new Color(.35f,.68f,.93f);
        private static readonly Color SlateInk = new Color(.105f,.15f,.21f);
        private readonly ExhibitDisplay[] floatingExhibits = new ExhibitDisplay[12];
        private Mesh[] archiveSculptures, archiveTitles;
        private Mesh archiveOrbit, archiveLabel;
        private Material archiveSculptureMaterial;
        private int exhibitionRoom = -1;
        private float exhibitionLength;
        private RuinPortal exhibitionFirstMouth;

        private void DrawRuinExhibits(Journey journey, float time)
        {
            if (archiveSculptures == null) BuildRuinExhibitions();
            int room = journey.RuinRoomId;
            float length = journey.RuinRoomLength;
            RuinPortal firstMouth = journey.RuinPortals.Count > 0 ? journey.RuinPortals[0] : null;
            if (room != exhibitionRoom || length != exhibitionLength || firstMouth != exhibitionFirstMouth)
            {
                ArrangeRuinExhibitions(journey);
                exhibitionRoom = room;
                exhibitionLength = length;
                exhibitionFirstMouth = firstMouth;
            }

            var eye = RuinTrack.Frame(room,length,journey.Distance-2.15f);
            Vector3 viewer = tunnelRoot.TransformPoint(eye.Point(journey.RuinLane*2.45f,2.35f));
            Color tint = Color.Lerp(new Color(.73f,.88f,.95f),RuinGalleryAccentFor(journey.RuinRoomStyle,room),.46f);
            foreach (ExhibitDisplay exhibit in floatingExhibits)
            {
                if (exhibit == null || float.IsPositiveInfinity(exhibit.Depth)) continue;
                float delta = RuinTrack.Offset(exhibit.Depth, journey.Distance, length);
                bool nearby = delta >= -8 && delta <= 40;
                exhibit.Root.gameObject.SetActive(nearby);
                if (!nearby) continue;
                var path = RuinTrack.Frame(room, length, exhibit.Depth);
                float cycle = time * (.68f + .095f * Mathf.Sin(exhibit.Phase)) + exhibit.Phase;
                exhibit.Root.localPosition = path.Point(exhibit.Side * (3.77f + .12f * Mathf.Sin(cycle*.68f)),
                    exhibit.Height + .20f * Mathf.Sin(cycle) + .065f * Mathf.Sin(cycle*1.61f));
                exhibit.Root.localRotation = path.Rotation * Quaternion.Euler(0, exhibit.Side * -11, 0);
                exhibit.Body.localRotation = Quaternion.Euler(4.5f * Mathf.Cos(cycle*.76f),
                    23 * Mathf.Sin(cycle*.58f) + exhibit.Side * 15, 4 * Mathf.Sin(cycle*1.12f));
                exhibit.Halo.localRotation = Quaternion.Euler(15 + 7 * Mathf.Sin(cycle*.47f), time*17 + exhibit.Phase*38, 9);
                exhibit.Halo.localScale = Vector3.one * exhibit.Size * (1 + .036f * Mathf.Sin(cycle*1.2f));
                Light(exhibit.HaloRenderer,tint,.23f+.16f*Mathf.Pow(.5f+.5f*Mathf.Sin(cycle*1.2f),2));
                Vector3 sight = exhibit.Title.position-viewer;
                sight.y = 0;
                if (sight.sqrMagnitude>.01f) exhibit.Title.rotation=Quaternion.LookRotation(sight);
            }
        }

        private void ArrangeRuinExhibitions(Journey journey)
        {
            int room = journey.RuinRoomId;
            float length = journey.RuinRoomLength;
            int count = journey.RuinRoomStyle == 5 ? 8 : floatingExhibits.Length;
            var random = new Random(74443 + room * 92717);
            for (int i = 0; i < count; i++)
            {
                ExhibitDisplay exhibit = floatingExhibits[i];
                int model = room == 0 ? i : (i + room*5) % archiveSculptures.Length;
                // The first sculpture in the entrance hall counterbalances the inscription on the left.
                exhibit.Side = ((i+(room==0 ? 1 : 0)) & 1) == 0 ? -1 : 1;
                float span = length - 11.4f;
                float target = 4.7f + span * i / (count - 1f);
                float previous = i < 2 ? 0 : floatingExhibits[i - 2].Depth;
                if (float.IsInfinity(previous)) previous = i < 4 ? 0 : floatingExhibits[i - 4].Depth;
                if (float.IsInfinity(previous)) previous = 0;
                exhibit.Depth = ExhibitSafeDepth(journey,target,exhibit.Side,previous);
                exhibit.Height = 2.45f + .92f * (float)random.NextDouble() + .17f*Mathf.Sin(i*1.7f);
                exhibit.Phase = (float)random.NextDouble()*Mathf.PI*2;
                Mesh sculpture = archiveSculptures[model];
                var size = sculpture.bounds.size;
                exhibit.Size = 2.84f/Mathf.Max(size.x,size.y*1.13f,size.z*1.15f);
                exhibit.Body.GetComponent<MeshFilter>().sharedMesh = sculpture;
                exhibit.Body.localScale = Vector3.one * exhibit.Size;
                exhibit.Body.localPosition = -sculpture.bounds.center * exhibit.Size;
                exhibit.Title.localPosition = new Vector3(-exhibit.Side*.69f,-.76f,-1.11f);
                exhibit.LetterRenderer.GetComponent<MeshFilter>().sharedMesh = archiveTitles[model];
            }
            for (int i = count; i < floatingExhibits.Length; i++)
            {
                floatingExhibits[i].Depth = float.PositiveInfinity;
                floatingExhibits[i].Root.gameObject.SetActive(false);
            }
        }

        private static float ExhibitSafeDepth(Journey journey, float wanted, float side, float previous)
        {
            float closest = wanted, best = float.NegativeInfinity;
            for (int sample = 0; sample < 13; sample++)
            {
                float offset = ((sample + 1) / 2) * ((sample & 1) == 0 ? -.66f : .66f);
                float tryDepth = Mathf.Clamp(wanted + offset, 3.4f, journey.RuinRoomLength - 6.5f);
                float clearance = tryDepth - previous;
                foreach (RuinPortal mouth in journey.RuinPortals)
                    if (mouth.Lane == side || mouth.Direction == RuinDirection.Upper || mouth.Direction == RuinDirection.Lower)
                        clearance = Mathf.Min(clearance, Mathf.Abs(RuinTrack.Offset(mouth.Depth,tryDepth,journey.RuinRoomLength)));
                float score = Mathf.Min(3.5f,clearance) - Mathf.Abs(offset)*.10f;
                if (score <= best) continue;
                closest = tryDepth;
                best = score;
            }
            return best > 1.37f ? closest : float.PositiveInfinity;
        }

        private void BuildRuinExhibitions()
        {
            Shader shader = Resources.Load<Shader>("EchoesRuinExhibit");
            Material material = glow;
            if (shader != null && shader.isSupported)
            {
                archiveSculptureMaterial = new Material(shader) { name = "Archive projection sculptures" };
                material = archiveSculptureMaterial;
            }
            archiveSculptures = new Mesh[ExhibitTitles.Length];
            archiveTitles = new Mesh[ExhibitTitles.Length];
            for (int model=0; model<archiveSculptures.Length; model++)
            {
                archiveSculptures[model] = MakeExhibitSculpture(model);
                archiveTitles[model] = BuildExhibitTitle(ExhibitTitles[model]);
            }
            archiveOrbit = BuildExhibitHalo();
            var plaque = Geometry();
            plaque.Box(Vector3.zero,new Vector3(2.20f,.31f,.044f),Quaternion.identity,SlateInk);
            plaque.Box(new Vector3(0,-.16f,-.028f),new Vector3(2.22f,.018f,.023f),Quaternion.identity,ExhibitBlue);
            archiveLabel = plaque.Finish("Suspended archive plaque");
            for (int i=0;i<floatingExhibits.Length;i++)
            {
                Transform anchor = Group("Floating civilization achievement",tunnelRoot);
                Transform body = RuinPiece("Historical sculpture",archiveSculptures[0],material,anchor);
                Transform halo = RuinPiece("Suspended scanning arcs",archiveOrbit,glow,anchor);
                Transform title = Group("Floating glass caption",anchor);
                RuinPiece("Smoked archive plaque",archiveLabel,material,title);
                Transform textObject = RuinPiece("Inscribed achievement",archiveTitles[0],glow,title);
                floatingExhibits[i] = new ExhibitDisplay {
                    Root=anchor, Body=body, Halo=halo, Title=title,
                    HaloRenderer=halo.GetComponent<MeshRenderer>(),
                    LetterRenderer=textObject.GetComponent<MeshRenderer>()
                };
                Light(body.GetComponent<MeshRenderer>(), Color.white, .48f);
                Light(floatingExhibits[i].LetterRenderer, new Color(.80f,.94f,1), .42f);
            }
        }

        private void DisposeRuinExhibits()
        {
            if (archiveSculptureMaterial != null) UnityEngine.Object.Destroy(archiveSculptureMaterial);
        }

        private Mesh BuildExhibitHalo()
        {
            var orbit = Geometry();
            for (int row=0;row<2;row++)
            {
                float radius = .94f + row * .18f;
                for (int arc=0;arc<2;arc++)
                for (int index=0;index<18;index++)
                {
                    float a=arc*Mathf.PI + row*.9f + index*.124f;
                    float b=a+.120f;
                    float y=-.13f+row*.23f;
                    Vector3 from=new Vector3(Mathf.Sin(a)*radius,y,Mathf.Cos(a)*radius*.67f);
                    Vector3 to=new Vector3(Mathf.Sin(b)*radius,y,Mathf.Cos(b)*radius*.67f);
                    ExhibitBeam(orbit,from,to,index==0 || index==17 ? .020f : .011f,Color.white);
                    if (index%8==0)
                        orbit.Box(from+new Vector3(0,.11f,0),Vector3.one*.038f,Quaternion.Euler(35,15,45),Color.white);
                }
            }
            return orbit.Finish("Broken holographic orbit and sparks");
        }

        private Mesh MakeExhibitSculpture(int model)
        {
            var form=Geometry();
            switch (model)
            {
                case 0: ExhibitGreatWall(form); break;
                case 1: ExhibitGiza(form); break;
                case 2: ExhibitArena(form); break;
                case 3: ExhibitTemple(form); break;
                case 4: ExhibitSteppedTemple(form); break;
                case 5: ExhibitPress(form); break;
                case 6: ExhibitTrain(form); break;
                case 7: ExhibitAstrolabe(form); break;
                case 8: ExhibitOrbiter(form); break;
                case 9: ExhibitSeismoscope(form); break;
                case 10: ExhibitTablet(form); break;
                case 11: ExhibitShip(form); break;
            }
            return form.Finish("Floating archive · "+ExhibitTitles[model]);
        }

        private static void ExhibitGreatWall(WorldMesh form)
        {
            Color grey=new Color(.66f,.72f,.69f), stone=new Color(.79f,.82f,.69f), ridge=new Color(.28f,.48f,.43f);
            for (int i=-9;i<=9;i++)
            {
                float x=i*.13f, y=.13f*Mathf.Sin(i*.37f), z=.35f*Mathf.Sin(i*.24f);
                Quaternion heading=Quaternion.Euler(0,-32*Mathf.Cos(i*.24f),0);
                Vector3 p=new Vector3(x,y,z);
                form.Rock(p-Vector3.up*.29f,new Vector3(.19f,.22f,.30f),ridge,.54f+i*.023f,3,7,.21f);
                for (int layer=0;layer<3;layer++)
                    form.Box(p+Vector3.up*(layer*.088f),new Vector3(.156f,.082f,.30f-layer*.012f),heading,
                        WorldMesh.Shade(grey,.86f+.065f*layer+.03f*Mathf.Sin(i)));
                form.Box(p+Vector3.up*.256f,new Vector3(.149f,.025f,.318f),heading,stone);
                for (int side=-1;side<=1;side+=2)
                {
                    Vector3 edge=p+heading*(Vector3.forward*.14f*side);
                    form.Box(edge+Vector3.up*.288f,new Vector3(.149f,.055f,.043f),heading,stone);
                    form.Box(edge+Vector3.up*.354f,new Vector3(.065f,.076f,.040f),heading,stone);
                }
                if (i!=-7 && i!=5 && i!=9) continue;
                form.Box(p+Vector3.up*.42f,new Vector3(.34f,.50f,.42f),heading,grey);
                form.Box(p+heading*new Vector3(0,.458f,-.212f),new Vector3(.079f,.137f,.014f),heading,SlateInk);
                form.Box(p+Vector3.up*.70f,new Vector3(.398f,.073f,.48f),heading,stone);
                ExhibitPyramid(form,p+Vector3.up*.72f,.46f,.52f,.19f,ridge);
            }
        }

        private static void ExhibitGiza(WorldMesh form)
        {
            Color sand = new Color(.88f,.68f,.42f), warm=new Color(.96f,.78f,.55f);
            ExhibitPyramid(form,new Vector3(-.62f,-.48f,.21f),1.40f,1.45f,1.29f,warm);
            ExhibitPyramid(form,new Vector3(.64f,-.48f,.37f),1.13f,1.18f,1.04f,sand);
            ExhibitPyramid(form,new Vector3(.47f,-.48f,-.66f),.63f,.65f,.59f,WorldMesh.Shade(warm,.84f));
            for (int i=0;i<6;i++)
                form.Box(new Vector3(-.63f+i*.17f,-.46f,-.69f),new Vector3(.13f,.045f,.23f),Quaternion.identity,
                    WorldMesh.Shade(sand,.94f+.035f*i));
        }

        private static void ExhibitPyramid(WorldMesh form, Vector3 bottom, float width, float depth, float height, Color color)
        {
            var corners=new[] { new Vector3(-width/2,0,-depth/2),new Vector3(width/2,0,-depth/2),
                new Vector3(width/2,0,depth/2),new Vector3(-width/2,0,depth/2) };
            Vector3 apex=bottom+Vector3.up*height;
            for (int face=0;face<4;face++)
            {
                Vector3 a=bottom+corners[face], b=bottom+corners[(face+1)%4];
                form.Triangle(a,b,apex,WorldMesh.Shade(color,.80f+.055f*face));
                for (int course=1;course<=13;course++)
                {
                    float h=course/14f;
                    ExhibitBeam(form,Vector3.Lerp(a,apex,h),Vector3.Lerp(b,apex,h),.009f+.004f*(1-h),
                        WorldMesh.Shade(color,.93f+.04f*Mathf.Sin(course*2.7f)));
                }
            }
            form.Quad(bottom+corners[0],bottom+corners[3],bottom+corners[2],bottom+corners[1],WorldMesh.Shade(color,.76f));
        }

        private static void ExhibitArena(WorldMesh form)
        {
            Color sand=new Color(.88f,.70f,.52f), stone=new Color(.96f,.82f,.65f);
            const int bays=18;
            for (int tier=0;tier<3;tier++)
            for (int bay=0;bay<bays;bay++)
            {
                if (tier>=2 && bay>=5 && bay<=11) continue;
                if (tier==1 && bay>=8 && bay<=9) continue;
                float radians=(bay+.5f)*2*Mathf.PI/bays;
                var pos=new Vector3(1.13f*Mathf.Sin(radians),-.49f+.355f*tier,.77f*Mathf.Cos(radians));
                Quaternion yaw=Quaternion.Euler(0,radians*Mathf.Rad2Deg,0);
                float width=.31f+.076f*Mathf.Abs(Mathf.Cos(radians)), half=width*.34f;
                form.Box(pos+Vector3.up*.028f,new Vector3(width+.019f,.055f,.156f),yaw,sand);
                for (int side=-1;side<=1;side+=2)
                    form.Box(pos+yaw*new Vector3(side*half,.119f,0),new Vector3(.048f,.15f,.15f),yaw,stone);
                Vector3 at=pos+yaw*new Vector3(-half,.177f,0);
                for (int segment=1;segment<=7;segment++)
                {
                    float a=Mathf.PI*(1-segment/7f);
                    Vector3 next=pos+yaw*new Vector3(Mathf.Cos(a)*half,.177f+Mathf.Sin(a)*.104f,0);
                    ExhibitBeam(form,at,next,.054f,segment%2==0?sand:stone,.15f);
                    at=next;
                }
                form.Box(pos+Vector3.up*.331f,new Vector3(width+.025f,.047f,.188f),yaw,stone);
                var inside=pos+new Vector3(-.17f*Mathf.Sin(radians),.10f,-.17f*Mathf.Cos(radians));
                form.Box(inside,new Vector3(.10f,.13f,.085f),yaw,WorldMesh.Shade(sand,.74f));
            }
        }

        private static void ExhibitTemple(WorldMesh form)
        {
            Color white=new Color(.91f,.88f,.78f), trim=new Color(.73f,.59f,.43f);
            for (int step=0;step<4;step++)
                form.Box(new Vector3(0,-.48f+step*.061f,0),new Vector3(2.07f-step*.085f,.065f,1.24f-step*.071f),
                    Quaternion.identity,WorldMesh.Shade(white,.75f+step*.077f));
            for (int side=-1;side<=1;side+=2)
            for (int i=0;i<8;i++)
            {
                Vector3 foot=new Vector3((i-3.5f)*.228f,-.249f,side*.464f);
                form.Tube(new[]{foot,foot+Vector3.up*.60f},new[]{.060f,.048f},12,white);
                form.Box(foot+Vector3.up*.60f,new Vector3(.142f,.043f,.133f),Quaternion.identity,trim);
            }
            for (int i=-1;i<=1;i++)
            for (int side=-1;side<=1;side+=2)
                form.Tube(new[]{new Vector3(side*.798f,-.249f,i*.236f),new Vector3(side*.798f,.342f,i*.236f)},
                    new[]{.060f,.048f},12,white);
            form.Box(new Vector3(0,.405f,0),new Vector3(1.88f,.109f,1.03f),Quaternion.identity,white);
            ExhibitPyramid(form,new Vector3(0,.46f,0),1.91f,1.10f,.33f,white);
            for (int i=0;i<19;i++)
                form.Box(new Vector3((i-9)*.098f,.382f,-.525f),new Vector3(.025f,.031f,.014f),Quaternion.identity,trim);
        }

        private static void ExhibitSteppedTemple(WorldMesh form)
        {
            Color tile=new Color(.72f,.79f,.65f);
            for (int i=0;i<9;i++)
            {
                float width=1.91f-i*.153f;
                float height=-.49f+i*.101f;
                form.Box(new Vector3(0,height,0),new Vector3(width,.106f,width),Quaternion.identity,WorldMesh.Shade(tile,.82f+i*.026f));
                for (int step=0;step<3;step++)
                    form.Box(new Vector3(0,height-.041f+step*.035f,-width*.5f-.045f+step*.034f),
                        new Vector3(.285f,.027f,.137f-step*.031f),Quaternion.identity,LimeStone);
                for (int side=-1;side<=1;side+=2)
                    form.Box(new Vector3(side*.158f,height+.041f,-width*.5f-.077f),
                        new Vector3(.044f,.109f,.152f),Quaternion.identity,WorldMesh.Shade(PaleJade,.81f));
            }
            form.Box(new Vector3(0,.518f,.054f),new Vector3(.474f,.256f,.146f),Quaternion.identity,tile);
            for (int side=-1;side<=1;side+=2)
                form.Box(new Vector3(side*.184f,.518f,-.05f),new Vector3(.102f,.254f,.239f),Quaternion.identity,LimeStone);
            form.Box(new Vector3(0,.668f,-.041f),new Vector3(.509f,.074f,.32f),Quaternion.identity,WorldMesh.Shade(tile,.89f));
        }

        private static void ExhibitPress(WorldMesh form)
        {
            Color timber=new Color(.53f,.29f,.21f), paper=new Color(.93f,.89f,.72f);
            for (int side=-1;side<=1;side+=2)
            {
                form.Box(new Vector3(side*.51f,.11f,0),new Vector3(.155f,1.46f,.295f),Quaternion.identity,timber);
                form.Box(new Vector3(side*.51f,-.66f,0),new Vector3(.23f,.12f,.78f),Quaternion.identity,timber);
                for (int rung=0;rung<3;rung++)
                    form.Box(new Vector3(side*.51f,-.36f+rung*.40f,-.160f),new Vector3(.173f,.071f,.038f),Quaternion.identity,FiredBronze);
            }
            form.Box(new Vector3(0,.77f,0),new Vector3(1.29f,.18f,.37f),Quaternion.identity,timber);
            form.Box(new Vector3(0,-.37f,0),new Vector3(1.14f,.11f,.73f),Quaternion.identity,timber);
            form.Tube(new[]{new Vector3(0,.96f,0),new Vector3(0,.025f,0)},new[]{.094f,.092f},16,FiredBronze);
            Vector3 prior=new Vector3(.106f,.08f,0);
            for (int i=1;i<61;i++)
            {
                Vector3 next=new Vector3(Mathf.Cos(i*.63f)*.107f,.08f+i*.0102f,Mathf.Sin(i*.63f)*.107f);
                ExhibitBeam(form,prior,next,.024f,LimeStone);
                prior=next;
            }
            ExhibitBeam(form,new Vector3(-.41f,.51f,-.16f),new Vector3(.45f,.51f,.09f),.073f,FiredBronze);
            form.Box(new Vector3(0,-.02f,0),new Vector3(.82f,.109f,.51f),Quaternion.identity,timber);
            for (int leaf=0;leaf<3;leaf++)
                form.Box(new Vector3(.034f+leaf*.023f,-.282f+leaf*.013f,-.046f-leaf*.04f),new Vector3(.67f,.014f,.45f),
                    Quaternion.Euler(0,leaf*5,0),paper);
            for (int line=0;line<6;line++)
                form.Box(new Vector3(.015f,-.241f,-.252f+line*.046f),new Vector3(.42f,.005f,.008f),Quaternion.identity,SlateInk);
        }

        private static void ExhibitTrain(WorldMesh form)
        {
            Color cast=new Color(.25f,.34f,.39f), iron=new Color(.48f,.57f,.60f), red=new Color(.67f,.27f,.26f);
            form.Box(new Vector3(0,-.27f,0),new Vector3(2.12f,.111f,.58f),Quaternion.identity,red);
            form.Tube(new[]{new Vector3(-.97f,.16f,0),new Vector3(.429f,.16f,0)},new[]{.261f,.261f},20,cast);
            form.Rock(new Vector3(-.98f,.159f,0),new Vector3(.047f,.266f,.261f),iron,.05f,5,18,.025f);
            for (int i=0;i<4;i++)
                form.Tube(new[]{new Vector3(-.807f+i*.275f,.16f,0),new Vector3(-.772f+i*.275f,.16f,0)},
                    new[]{.267f,.267f},22,FiredBronze);
            form.Box(new Vector3(.64f,.07f,0),new Vector3(.56f,.67f,.66f),Quaternion.identity,cast);
            form.Box(new Vector3(.64f,.432f,0),new Vector3(.64f,.085f,.76f),Quaternion.identity,red);
            form.Tube(new[]{new Vector3(-.68f,.34f,0),new Vector3(-.68f,.71f,0)},new[]{.074f,.116f},16,iron);
            form.Rock(new Vector3(-.224f,.438f,0),new Vector3(.096f,.101f,.100f),FiredBronze,.12f,5,13,.011f);
            for (int side=-1;side<=1;side+=2)
            {
                form.Box(new Vector3(.637f,.184f,side*.332f),new Vector3(.29f,.26f,.026f),Quaternion.identity,ExhibitBlue);
                for (int wheel=0;wheel<4;wheel++)
                {
                    Vector3 at=new Vector3(-.827f+wheel*.503f,-.36f,side*.303f);
                    ExhibitCircle(form,at,.208f,.208f,Vector3.right,Vector3.up,.042f,iron,22);
                    form.Rock(at,new Vector3(.062f,.062f,.021f),FiredBronze,.12f,4,12,.010f);
                    for (int spoke=0;spoke<7;spoke++)
                    {
                        float a=spoke*2*Mathf.PI/7;
                        ExhibitBeam(form,at,new Vector3(Mathf.Cos(a)*.193f,Mathf.Sin(a)*.193f,0)+at,.028f,red);
                    }
                }
                ExhibitBeam(form,new Vector3(-.83f,-.41f,side*.345f),new Vector3(.64f,-.36f,side*.345f),.048f,FiredBronze);
            }
        }

        private static void ExhibitAstrolabe(WorldMesh form)
        {
            ExhibitCircle(form,Vector3.zero,.60f,.60f,Vector3.right,Vector3.up,.053f,FiredBronze,49);
            ExhibitCircle(form,new Vector3(0,0,.020f),.49f,.49f,Vector3.right,Vector3.up,.021f,LimeStone,36);
            var spin=Quaternion.Euler(0,47,13);
            ExhibitCircle(form,Vector3.zero,.54f,.54f,spin*Vector3.right,spin*Vector3.up,.034f,WorldMesh.Shade(FiredBronze,.78f),39);
            for (int tick=0;tick<47;tick++)
            {
                float a=tick*Mathf.PI*2/47;
                Vector3 radial=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                ExhibitBeam(form,radial*.544f-Vector3.forward*.05f,radial*(tick%4==0?.453f:.502f)-Vector3.forward*.05f,
                    .012f,LimeStone);
            }
            ExhibitBeam(form,new Vector3(-.379f,-.32f,-.077f),new Vector3(.311f,.403f,-.077f),.045f,LimeStone);
            form.Rock(new Vector3(0,0,-.089f),new Vector3(.072f,.072f,.053f),ExhibitBlue,.08f,5,14,.021f);
            ExhibitCircle(form,new Vector3(0,.683f,0),.108f,.108f,Vector3.right,Vector3.up,.025f,FiredBronze,15);
        }

        private static void ExhibitOrbiter(WorldMesh form)
        {
            Color foil=new Color(.90f,.72f,.44f), cell=new Color(.26f,.49f,.80f);
            form.Tube(new[]{new Vector3(0,-.39f,0),new Vector3(0,-.20f,0),new Vector3(0,.51f,0)},
                new[]{.145f,.251f,.251f},21,LimeStone);
            form.Rock(new Vector3(0,.511f,0),new Vector3(.254f,.13f,.25f),foil,.20f,5,17,.031f);
            form.Tube(new[]{new Vector3(-.43f,.08f,0),new Vector3(.43f,.08f,0)},new[]{.149f,.129f},17,foil);
            form.Tube(new[]{new Vector3(0,.044f,-.43f),new Vector3(0,.044f,.41f)},new[]{.123f,.119f},16,LimeStone);
            for (int side=-1;side<=1;side+=2)
            {
                ExhibitBeam(form,new Vector3(side*.25f,.11f,0),new Vector3(side*1.25f,.11f,0),.029f,foil);
                form.Box(new Vector3(side*.96f,.11f,0),new Vector3(.96f,.031f,.49f),Quaternion.Euler(-8,0,0),LimeStone);
                for (int row=0;row<3;row++)
                for (int col=0;col<8;col++)
                    form.Box(new Vector3(side*(.535f+col*.121f),.13f-(row-1)*.020f,(row-1)*.153f),
                        new Vector3(.106f,.017f,.137f),Quaternion.Euler(-8,0,0),WorldMesh.Shade(cell,.86f+.22f*Mathf.Sin((row+col)*1.53f)));
            }
            ExhibitBeam(form,new Vector3(.05f,.48f,-.116f),new Vector3(.28f,.76f,-.269f),.023f,foil);
            var bowl=Quaternion.Euler(-34,23,-9);
            ExhibitCircle(form,new Vector3(.28f,.76f,-.269f),.216f,.146f,bowl*Vector3.right,bowl*Vector3.up,.029f,LimeStone,22);
        }

        private static void ExhibitSeismoscope(WorldMesh form)
        {
            Vector3 basePoint=new Vector3(0,-.62f,0);
            form.Rock(basePoint,new Vector3(.49f,.088f,.49f),LimeStone,.37f,3,17,.07f);
            form.Rock(new Vector3(0,-.11f,0),new Vector3(.533f,.48f,.532f),FiredBronze,.17f,9,32,.035f);
            ExhibitCircle(form,new Vector3(0,.239f,0),.429f,.429f,Vector3.right,Vector3.forward,.036f,LimeStone,35);
            ExhibitCircle(form,new Vector3(0,-.372f,0),.427f,.427f,Vector3.right,Vector3.forward,.014f,PaleJade,29);
            form.Rock(new Vector3(0,.382f,0),new Vector3(.268f,.106f,.264f),WorldMesh.Shade(FiredBronze,.84f),.62f,5,16,.067f);
            for (int point=0;point<8;point++)
            {
                float a=point*Mathf.PI/4;
                Vector3 outward=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
                form.Tube(new[]{outward*.40f+Vector3.up*.16f,outward*.544f+Vector3.up*.20f,outward*.629f+Vector3.up*.085f},
                    new[]{.061f,.059f,.044f},9,PaleJade);
                form.Rock(outward*.643f+Vector3.up*.058f,new Vector3(.041f,.039f,.041f),LimeStone,.079f*point,4,10,.035f);
                form.Rock(outward*.632f+Vector3.down*.55f,new Vector3(.105f,.067f,.085f),WorldMesh.Shade(PaleJade,.82f),
                    point*.12f,4,13,.09f);
            }
        }

        private static void ExhibitTablet(WorldMesh form)
        {
            form.Rock(Vector3.zero,new Vector3(.65f,.745f,.166f),new Color(.74f,.48f,.32f),.03f,12,32,.115f);
            for(int row=0;row<9;row++)
            for(int col=0;col<7;col++)
            {
                float x=(col-3)*.14f+.016f*Mathf.Sin(row*5+col), y=.504f-row*.120f;
                float inside=Mathf.Clamp01(1-(x/.67f)*(x/.67f)-(y/.76f)*(y/.76f));
                float z=-.172f*Mathf.Sqrt(inside)-.020f;
                Vector3 p=new Vector3(x,y,z);
                float rotate=col*7+row*4;
                var wedge=Quaternion.Euler(0,0,rotate);
                form.Triangle(p+wedge*new Vector3(-.039f,-.029f,0),p+wedge*new Vector3(.027f,.046f,0),
                    p+wedge*new Vector3(.046f,-.011f,0),new Color(.30f,.20f,.17f));
                ExhibitBeam(form,p+wedge*new Vector3(.018f,.005f,-.003f),p+wedge*new Vector3(.066f,.023f,-.003f),
                    .009f,new Color(.45f,.30f,.21f));
            }
        }

        private static void ExhibitShip(WorldMesh form)
        {
            Color wood=new Color(.59f,.36f,.23f), sail=new Color(.88f,.79f,.59f), stripe=new Color(.63f,.31f,.33f);
            for (int side=-1;side<=1;side+=2)
            for (int plank=0;plank<4;plank++)
            for (int i=0;i<15;i++)
            {
                float start=i/15f, end=(i+1)/15f;
                var a=ExhibitHullPoint(start,plank,side);
                var b=ExhibitHullPoint(end,plank,side);
                form.Quad(a,b,ExhibitHullPoint(end,plank+1,side),ExhibitHullPoint(start,plank+1,side),WorldMesh.Shade(wood,.76f+.10f*plank));
                if (plank==3) ExhibitBeam(form,a+Vector3.up*.07f,b+Vector3.up*.07f,.025f,FiredBronze);
            }
            for (int mast=0;mast<2;mast++)
            {
                float x=-.47f+mast*.72f, height=.92f+mast*.26f;
                ExhibitBeam(form,new Vector3(x,-.31f,0),new Vector3(x+.12f,height,0),.053f,wood);
                for (int batten=0;batten<6;batten++)
                {
                    float t=batten/6f, lower=(batten+1)/6f;
                    Vector3 left=new Vector3(x-.27f+t*.025f,.03f+height*t,-.029f-t*.19f);
                    Vector3 right=new Vector3(x+.44f-.26f*t,.03f+height*t,-.029f-t*.19f);
                    form.Quad(left,right,new Vector3(x+.44f-.26f*lower,.03f+height*lower,-.029f-lower*.19f),
                        new Vector3(x-.27f+lower*.025f,.03f+height*lower,-.029f-lower*.19f),batten%3==0?stripe:sail);
                    ExhibitBeam(form,left-Vector3.forward*.008f,right-Vector3.forward*.008f,.021f,wood);
                }
                ExhibitBeam(form,new Vector3(x+.11f,height*.92f,.017f),new Vector3(x-.55f,-.23f,.13f),.012f,FiredBronze);
            }
        }

        private static Vector3 ExhibitHullPoint(float t,int plank,int side)
        {
            float ridge=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.72f);
            return new Vector3((t-.5f)*2.21f,-.58f+plank*.065f+.23f*Mathf.Abs(t-.5f),
                side*(.056f+plank*.060f)*ridge);
        }

        private static void ExhibitCircle(WorldMesh form,Vector3 center,float x,float y,Vector3 right,Vector3 up,float width,Color color,int steps)
        {
            Vector3 previous=center+right*x;
            for (int i=1;i<=steps;i++)
            {
                float a=2*Mathf.PI*i/steps;
                Vector3 next=center+right*(Mathf.Cos(a)*x)+up*(Mathf.Sin(a)*y);
                ExhibitBeam(form,previous,next,width,color);
                previous=next;
            }
        }

        private static void ExhibitBeam(WorldMesh form,Vector3 a,Vector3 b,float width,Color color,float depth=0)
        {
            Vector3 line=b-a;
            if (line.sqrMagnitude<.0000001f) return;
            Vector3 up=Mathf.Abs(Vector3.Dot(line.normalized,Vector3.up))>.97f?Vector3.forward:Vector3.up;
            form.Box((a+b)*.5f,new Vector3(width,depth>0?depth:width,line.magnitude+width*.18f),Quaternion.LookRotation(line,up),color);
        }

        private static readonly uint[,] ExhibitGlyphs = {
            {14,17,17,31,17,17,17},{30,17,17,30,17,17,30},{15,16,16,16,16,16,15},
            {30,17,17,17,17,17,30},{31,16,16,30,16,16,31},{31,16,16,30,16,16,16},
            {14,17,16,23,17,17,15},{17,17,17,31,17,17,17},{14,4,4,4,4,4,14},
            {7,2,2,2,18,18,12},{17,18,20,24,20,18,17},{16,16,16,16,16,16,31},
            {17,27,21,21,17,17,17},{17,25,21,19,17,17,17},{14,17,17,17,17,17,14},
            {30,17,17,30,16,16,16},{14,17,17,17,21,18,13},{30,17,17,30,20,18,17},
            {15,16,16,14,1,1,30},{31,4,4,4,4,4,4},{17,17,17,17,17,17,14},
            {17,17,17,17,17,10,4},{17,17,17,21,21,27,17},{17,17,10,4,10,17,17},
            {17,17,10,4,4,4,4},{31,1,2,4,8,16,31}
        };

        private Mesh BuildExhibitTitle(string name)
        {
            var letters=Geometry();
            float cell=Mathf.Min(.032f,1.99f/(name.Length*6-1));
            for (int i=0;i<name.Length;i++)
            {
                int glyph=name[i]-'A';
                if ((uint)glyph>=26) continue;
                for (int row=0;row<7;row++)
                for (int col=0;col<5;col++)
                {
                    if ((ExhibitGlyphs[glyph,row] & (1u<<(4-col)))==0) continue;
                    float x=(i*6+col-(name.Length*6-2)*.5f)*cell;
                    letters.Box(new Vector3(x,(3-row)*cell,-.032f),new Vector3(cell*.96f,cell*.96f,.009f),
                        Quaternion.identity,Color.white);
                }
            }
            return letters.Finish("Caption · "+name);
        }
    }
}
