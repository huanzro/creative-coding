using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Echoes
{
    internal sealed class RuinArtifacts : IDisposable
    {
        private const float Tau = Mathf.PI * 2;
        private static readonly Quaternion Upright = Quaternion.identity;
        private static readonly int Reveal = Shader.PropertyToID("_Reveal");
        private static readonly int Luminance = Shader.PropertyToID("_Luminance");
        private static readonly Color Gold = new Color(.83f,.57f,.29f), Brass = new Color(.70f,.46f,.24f),
            PaleGold = new Color(1,.83f,.48f), Jade = new Color(.25f,.82f,.69f),
            Indigo = new Color(.42f,.50f,1), Rose = new Color(1,.43f,.44f), Silver = new Color(.67f,.77f,.84f);
        private static readonly string[] ArtifactNames = { "Pulsing heart", "Folded brain", Journey.RuinPhoneName, "Portable computer" };
        private static readonly string[] ChestNames = { "Cedar travel trunk", "Brass astrolabe", "Crystal reliquary",
            "Lacquer coffer", "Alabaster sarcophagus", "Nautilus case", "Mechanical strongbox", "Illuminated folio",
            "Lotus vessel", "Hourglass cabinet", "Lantern chest", "Relay chest", "Prism coffer",
            "Fern reliquary", "Copper map case", "Survey folio" };
        private static readonly int[] AdditionalChestShapes = { 0, 6, 2, 8, 1, 7 };

        private sealed class Model
        {
            public Mesh Shell, Lid;
            public Vector3 Hinge;
        }

        private sealed class Element
        {
            public Transform Root, Lid;
            public MeshFilter BodyFilter, LidFilter;
            public MeshRenderer BodyRenderer, LidRenderer;
            public int Design = -1;
            public float Open;
        }

        private readonly Transform parent;
        private readonly int layer;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Element> artifacts = new List<Element>();
        private readonly List<Element> chests = new List<Element>();
        private readonly List<Element> loot = new List<Element>();
        private readonly Model[] artifactModels = new Model[4], chestModels = new Model[Journey.RuinChestCount];
        private readonly Model[,] lootModels = new Model[2, 10];
        private readonly Material artifactMaterial, chestMaterial;
        private readonly MaterialPropertyBlock state = new MaterialPropertyBlock();

        public RuinArtifacts(Transform parent, int layer)
        {
            this.parent = parent;
            this.layer = layer;
            var shader = Resources.Load<Shader>("EchoesRuinRelic");
            if (shader == null || !shader.isSupported) shader = Shader.Find("Hidden/Echoes/WorldSurface");
            artifactMaterial = new Material(shader) { name = "Partially veiled ruin artifacts" };
            chestMaterial = new Material(shader) { name = "Crafted ruin chests" };
            artifactMaterial.SetFloat("_Concealed", 1);
        }

        public void UpdateArtifact(int slot, int artKind, Vector3 localPos, float time, float proximity, float yaw)
        {
            if ((uint)artKind >= artifactModels.Length) return;
            Model model = artifactModels[artKind] ?? (artifactModels[artKind] = BuildArtifact(artKind));
            Element item = Place(artifacts, slot, artKind, model, ArtifactNames[artKind], artifactMaterial);
            float phase = time * 1.7f + slot * 1.29f;
            float approach = Ease(proximity);
            item.Root.localPosition = localPos + Vector3.up * (.055f * Mathf.Sin(phase));
            item.Root.localRotation = Quaternion.Euler(Mathf.Sin(phase*.7f)*7, yaw + Mathf.Sin(phase*.44f)*15, Mathf.Sin(phase)*4);
            float beat = 0;
            if (artKind == 0)
            {
                float cycle = Mathf.Repeat(time*(1.03f+.21f*approach)+slot*.61f, 1);
                beat = Pulse(cycle, .13f, .072f) + .55f*Pulse(cycle,.35f,.061f);
            }
            float amount = .035f + approach*(.14f + (artKind == 0 ? .044f : 0));
            item.Root.localScale = new Vector3(1+beat*amount, 1+beat*amount*.74f, 1+beat*amount);
            SetSurface(item.BodyRenderer, .22f + .75f*approach, .12f + .48f*approach + beat*.73f*approach);
        }

        public void UpdateChest(int slot, int design, Vector3 localPos, float time, float proximity, bool opened, bool stocked, float yaw)
        {
            if ((uint)design >= chestModels.Length) return;
            Model model = chestModels[design] ?? (chestModels[design] = BuildChest(design));
            Element item = Place(chests, slot, design, model, ChestNames[design], chestMaterial);
            item.Root.localPosition = localPos;
            item.Root.localScale = Vector3.one * 1.16f;
            item.Root.localRotation = Quaternion.Euler(0, yaw, 0);
            item.Open = Mathf.MoveTowards(item.Open, opened ? 1 : 0, Time.unscaledDeltaTime*1.75f);
            item.Lid.localRotation = Quaternion.Euler(68 * Ease(item.Open), 0, 0);
            float sparkle = .12f + .10f*Mathf.Sin(time*1.85f+design*1.57f);
            float light = stocked ? .52f + sparkle + .29f*Ease(proximity) + (opened ? .15f : 0) : .04f;
            SetSurface(item.BodyRenderer, 1, light);
            SetSurface(item.LidRenderer, 1, light);
        }

        public void HideFrom(int artifactCount, int chestCount)
        {
            Hide(artifacts, artifactCount);
            Hide(chests, chestCount);
        }

        public void UpdateLootDevice(int slot, RuinObjectKind kind, int variant, Vector3 position, float yaw, float size, bool selected)
        {
            int type = kind == RuinObjectKind.Phone ? 0 : 1;
            variant = Mathf.Clamp(variant, 0, lootModels.GetLength(1) - 1);
            Model model = lootModels[type, variant];
            if (model == null)
            {
                var mesh = new WorldMesh(meshes);
                if (type == 0 && (variant == 0 || variant == 8)) FoldingPhone(mesh, variant);
                else if (type == 0) Phone(mesh, variant);
                else Computer(mesh, variant);
                model = lootModels[type, variant] = new Model { Shell = mesh.Finish("Archive device") };
            }
            Element item = Place(loot, slot, 10 * type + variant, model,
                kind == RuinObjectKind.Phone ? Journey.RuinPhoneName : "Archive device tray", chestMaterial);
            item.Root.localPosition = position;
            item.Root.localScale = Vector3.one * size;
            item.Root.localRotation = Quaternion.Euler(-5, yaw, 0);
            SetSurface(item.BodyRenderer, 1, selected ? .78f : .31f);
        }

        public void UpdateLootMemory(int slot, RuinObjectKind kind, Vector3 position, float time, float size, float glow)
        {
            int type = (int)kind;
            if (type > 1) return;
            Model model = artifactModels[type] ?? (artifactModels[type] = BuildArtifact(type));
            Element item = Place(loot, slot, 20 + type, model, ArtifactNames[type], artifactMaterial);
            float beat = kind == RuinObjectKind.Heart ? Pulse(time, .56f, .10f) : 0;
            item.Root.localPosition = position;
            item.Root.localScale = Vector3.one * (size * (1 + .16f * beat));
            item.Root.localRotation = Quaternion.Euler(0, 20 * time, 0);
            SetSurface(item.BodyRenderer, glow, .53f * glow + beat * .28f);
        }

        public void HideLoot(int count) => Hide(loot, count);

        private static void Hide(List<Element> items, int count)
        {
            for (int i = count; i < items.Count; i++)
            {
                items[i].Root.gameObject.SetActive(false);
                items[i].Open = 0;
            }
        }

        private Element Place(List<Element> pool, int slot, int design, Model model, string label, Material material)
        {
            while (pool.Count <= slot)
            {
                var node = Node("Ruin artifact", parent);
                var lid = Node("Hinged lid", node);
                var item = new Element { Root=node, Lid=lid };
                item.BodyRenderer = Renderer(node, material, out item.BodyFilter);
                item.LidRenderer = Renderer(lid, material, out item.LidFilter);
                pool.Add(item);
            }
            Element placed = pool[slot];
            if (placed.Design != design)
            {
                placed.Design = design;
                placed.Root.name = label;
                placed.BodyFilter.sharedMesh = model.Shell;
                placed.LidFilter.sharedMesh = model.Lid;
                placed.Lid.localPosition = model.Hinge;
                placed.Lid.gameObject.SetActive(model.Lid != null);
                placed.Open = 0;
            }
            placed.Root.gameObject.SetActive(true);
            return placed;
        }

        private Transform Node(string label, Transform under)
        {
            var node = new GameObject(label) { layer=layer, hideFlags=HideFlags.HideAndDontSave };
            node.transform.SetParent(under, false);
            return node.transform;
        }

        private static MeshRenderer Renderer(Transform node, Material material, out MeshFilter filter)
        {
            filter = node.gameObject.AddComponent<MeshFilter>();
            var renderer = node.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private void SetSurface(MeshRenderer renderer, float reveal, float brightness)
        {
            state.SetFloat(Reveal, reveal);
            state.SetFloat(Luminance, brightness);
            renderer.SetPropertyBlock(state);
        }

        private static float Ease(float t) => Mathf.SmoothStep(0,1,t);
        private static float Pulse(float x, float center, float radius) => Mathf.Exp(-Mathf.Pow((x-center)/radius,2));
        private static Color Gem(Color color, float charge=.72f) => new Color(color.r,color.g,color.b,1-charge);
        private static Vector3 P(float x,float y,float z) => new Vector3(x,y,z);

        private Model BuildArtifact(int kind)
        {
            var mesh = new WorldMesh(meshes);
            switch (kind)
            {
                case 0: Heart(mesh); break;
                case 1: Brain(mesh); break;
                case 2: Phone(mesh); break;
                case 3: Computer(mesh); break;
            }
            return new Model { Shell = mesh.Finish(ArtifactNames[kind]) };
        }

        private static void Heart(WorldMesh mesh)
        {
            Color muscle = new Color(.53f,.25f,.33f), pale = new Color(.78f,.42f,.46f), vein = new Color(.31f,.16f,.35f);
            // Offset shells give the ventricles an asymmetric apex and a readable auricle rim.
            mesh.Rock(P(-.055f,-.07f,0),P(.235f,.28f,.172f),muscle,.17f,19,28,.09f);
            mesh.Rock(P(.091f,.037f,-.024f),P(.157f,.237f,.169f),pale,.12f,17,25,.07f);
            mesh.Rock(P(-.127f,.146f,.006f),P(.126f,.119f,.131f),pale,.23f,15,20,.06f);
            mesh.Rock(P(.053f,-.197f,-.025f),P(.136f,.164f,.122f),muscle,.30f,15,22,.06f);
            mesh.Tube(new[]{P(.037f,.168f,.027f),P(.024f,.267f,.017f),P(.069f,.342f,.023f),P(.155f,.352f,.039f),P(.185f,.338f,.093f)},
                new[]{.061f,.061f,.056f,.047f,.044f},15,pale);
            mesh.Tube(new[]{P(-.052f,.143f,.044f),P(-.037f,.252f,.097f),P(-.116f,.300f,.123f),P(-.170f,.287f,.144f)},
                new[]{.054f,.053f,.040f,.035f},13,vein);
            mesh.Tube(new[]{P(.080f,.151f,-.064f),P(.022f,.231f,-.085f),P(-.050f,.281f,-.126f),P(-.111f,.278f,-.146f)},
                new[]{.050f,.048f,.036f,.030f},13,pale);
            for (int side = -1; side <= 1; side += 2)
            for (int branch=0; branch<5; branch++)
            {
                float z=.155f*side, bias=(branch-2)*.065f;
                mesh.Tube(new[]{P(.034f,.211f,z*.68f),P(.038f+.42f*bias,.118f,z*.99f),P(.035f+bias,.006f,z*(1.18f-.085f*branch)),
                    P(.074f+bias*1.21f,-.095f-.020f*branch,z*.94f),P(.091f+bias*.72f,-.197f,z*.71f)},
                    new[]{.009f,.012f,.008f,.006f,.003f},7,branch%2==0?Gem(Rose,.65f):pale);
            }
            mesh.Rock(P(.185f,.338f,.091f),P(.037f,.019f,.041f),vein,.2f,9,14,.07f);
            mesh.Rock(P(-.128f,.27f,.04f),P(.033f,.021f,.031f),Gem(Rose,.78f),.1f,6,10,.1f);
        }

        private static Vector3 BrainSurface(int side,float theta,float phi,float margin)
        {
            float body = 1 + .043f*Mathf.Sin(theta*11+phi*9) + .038f*Mathf.Sin(phi*18-theta*6);
            return P(side*.116f + Mathf.Cos(theta)*Mathf.Cos(phi)*(.116f+margin)*body,
                .046f+Mathf.Sin(phi)*(.205f+margin)*body,
                Mathf.Sin(theta)*Mathf.Cos(phi)*(.25f+margin)*body);
        }

        private static void Brain(WorldMesh mesh)
        {
            Color fold = new Color(.59f,.55f,.73f), lower = new Color(.39f,.37f,.61f), dark = new Color(.29f,.31f,.47f);
            for (int side=-1;side<=1;side+=2)
            {
                int direction=side;
                mesh.Surface(35,22,(x,y)=>BrainSurface(direction,Tau*x/35,Mathf.Lerp(-Mathf.PI/2,Mathf.PI/2,y/22f),0),
                    p=>WorldMesh.Shade(fold,.91f+.13f*Mathf.Cos(p.z*97+p.y*124)),null);
                for (int tract=0;tract<11;tract++)
                {
                    var points = new Vector3[27];
                    var thickness = new float[27];
                    for (int i=0; i<points.Length; i++)
                    {
                        float fraction=i/(points.Length-1f);
                        float phi=Mathf.Lerp(-.88f,1.41f,fraction);
                        float theta=tract*Tau/11 + .42f*Mathf.Sin(phi*4.3f+tract*2.6f) +
                            .25f*Mathf.Sin(phi*8.5f+tract*.96f);
                        points[i]=BrainSurface(side,theta,phi,.010f);
                        thickness[i]=.0065f+.006f*Mathf.Sin(Mathf.PI*fraction);
                    }
                    mesh.Tube(points,thickness,7,tract%4==0?Gem(Indigo,.64f):WorldMesh.Shade(fold,1.12f));
                }
            }
            mesh.Rock(P(0,-.139f,.090f),P(.151f,.089f,.139f),lower,.31f,11,20,.13f);
            mesh.Tube(new[]{P(.003f,-.073f,.101f),P(.024f,-.157f,.085f),P(.015f,-.221f,.076f),P(-.019f,-.267f,.047f)},
                new[]{.072f,.049f,.030f,.025f},13,lower);
            mesh.Tube(new[]{P(0,.225f,-.107f),P(0,.246f,-.024f),P(0,.241f,.046f),P(0,.217f,.124f)},
                new[]{.008f,.012f,.014f,.005f},8,dark);
            for(int i=0;i<6;i++)
                mesh.Tube(new[]{P(-.10f,-.138f-.007f*i,.184f-.011f*i),P(0,-.137f-.012f*i,.213f-.012f*i),
                    P(.11f,-.135f-.006f*i,.179f-.011f*i)},new[]{.003f,.005f,.003f},7,fold);
        }

        private static readonly Color[] DeviceAlloys = {
            new Color(.62f,.36f,.21f), new Color(.18f,.49f,.53f), new Color(.60f,.55f,.71f),
            new Color(.43f,.24f,.32f), new Color(.28f,.40f,.62f), new Color(.59f,.61f,.53f)
        };

        private static void FoldingPhone(WorldMesh mesh, int variant)
        {
            Color alloy = DeviceAlloys[variant % DeviceAlloys.Length];
            Color dark = new Color(.055f,.096f,.125f);
            for (int part = -1; part <= 1; part += 2)
            {
                Vector3 center = P(0, part * .167f, .026f);
                Quaternion hinge = Quaternion.Euler(part * 17, 0, 0);
                Rounded(mesh,center,.376f,.326f,.058f,.048f,hinge,alloy);
                Rounded(mesh,center+hinge*P(0,0,-.034f),.337f,.291f,.014f,.039f,hinge,dark);
                for (int line = 0; line < 4; line++)
                    ScreenDash(mesh,center+hinge*P(-.028f,.091f-line*.051f,-.043f),.14f+.036f*line,.01f,
                        hinge,line%2==0 ? Gem(Jade,.67f) : Gem(PaleGold,.46f));
                Lens(mesh,center+hinge*P(.116f,.071f,-.046f),.022f,.005f,Gem(Indigo),true);
            }
            mesh.Tube(new[] { P(-.177f,0,.024f), P(0,0,.024f), P(.177f,0,.024f) },
                new[] { .035f,.029f,.035f },16,Brass);
            Lens(mesh,P(.182f,0,.024f),.037f,.009f,PaleGold,false);
        }

        private static void Phone(WorldMesh mesh, int variant = -1)
        {
            Color anodized = variant < 0 ? new Color(.59f,.69f,.73f) : DeviceAlloys[variant % DeviceAlloys.Length];
            Color glass = new Color(.07f,.13f,.19f);
            Rounded(mesh,P(0,0,0),.348f,.642f,.054f,.050f,Upright,anodized);
            Rounded(mesh,P(0,0,-.032f),.322f,.610f,.011f,.047f,Upright,new Color(.05f,.069f,.10f));
            Rounded(mesh,P(0,-.007f,-.041f),.288f,.549f,.006f,.038f,Upright,glass);
            Rounded(mesh,P(0,.251f,-.047f),.077f,.019f,.004f,.009f,Upright,new Color(.04f,.06f,.10f));
            Rounded(mesh,P(.107f,.255f,-.048f),.025f,.011f,.003f,.004f,Upright,Gem(Jade));
            Rounded(mesh,P(0,-.265f,-.049f),.080f,.009f,.002f,.004f,Upright,Gem(Silver,.45f));
            Rounded(mesh,P(-.083f,.276f,.041f),.113f,.117f,.022f,.024f,Upright,WorldMesh.Shade(anodized,.77f));
            for (int i=0;i<2;i++)
            {
                float y=.258f+i*.048f;
                Lens(mesh,P(-.097f,y,.058f),.022f,.008f,SofterSilver(),false);
                Lens(mesh,P(-.097f,y,.064f),.015f,.004f,Gem(Indigo,.78f),false);
            }
            Lens(mesh,P(-.055f,.283f,.057f),.010f,.005f,Gem(PaleGold,.62f),false);
            Rounded(mesh,P(-.177f,.167f,0),.009f,.078f,.018f,.004f,Upright,anodized);
            Rounded(mesh,P(.177f,.105f,0),.010f,.092f,.020f,.004f,Upright,anodized);
            for (int i=0;i<4;i++)
            {
                float y=.147f-i*.097f;
                Rounded(mesh,P(0,y,-.046f),.239f,.069f,.003f,.013f,Upright,new Color(.14f,.24f,.29f));
                ScreenDash(mesh,P(-.074f,y+.013f,-.049f),.048f,.007f,Upright,i%2==0?Gem(Jade,.56f):Gem(Indigo,.62f));
                ScreenDash(mesh,P(-.037f,y-.010f,-.049f),.117f-.017f*i,.005f,Upright,new Color(.39f,.67f,.70f));
                Lens(mesh,P(.086f,y,-.049f),.014f,.002f,i%2==0?Gem(PaleGold,.54f):Gem(Jade,.56f),true);
            }
        }

        private static void Computer(WorldMesh mesh, int variant = -1)
        {
            Color alloy = variant < 0 ? new Color(.47f,.59f,.69f) : DeviceAlloys[(variant + 2) % DeviceAlloys.Length];
            Color ink = new Color(.06f,.09f,.14f), key = new Color(.21f,.28f,.37f);
            Quaternion deck = Quaternion.Euler(90,0,0);
            Rounded(mesh,P(0,-.164f,-.065f),.739f,.446f,.047f,.037f,deck,alloy);
            Rounded(mesh,P(0,-.139f,-.028f),.681f,.297f,.011f,.018f,deck,ink);
            Rounded(mesh,P(0,-.137f,-.216f),.210f,.083f,.012f,.012f,deck,new Color(.66f,.74f,.80f));
            if (variant == 4 || variant == 7)
            {
                Rounded(mesh,P(0,-.126f,-.022f),.614f,.237f,.013f,.031f,deck,new Color(.15f,.25f,.30f));
                for (int trace = 0; trace < 5; trace++)
                    ScreenDash(mesh,P(-.075f+.016f*trace,-.116f,.056f-.034f*trace),.24f+.047f*trace,.010f,deck,
                        trace%2==0 ? Gem(Jade,.53f) : Gem(Indigo,.58f));
            }
            else
            {
                for(int row=0;row<4;row++)
                for(int col=0;col<11;col++)
                {
                    var point=P((col-5)*.053f,-.125f,.078f-row*.055f);
                    Rounded(mesh,point,.043f,.045f,.010f,.004f,deck,key);
                    ScreenDash(mesh,point+Vector3.up*.007f,.014f,.007f,deck,row==0?Gem(Jade,.55f):alloy);
                }
            }
            Rounded(mesh,P(0,-.122f,-.124f),.272f,.018f,.010f,.006f,deck,key);
            for(int side=-1;side<=1;side+=2)
                for(int slot=0;slot<3;slot++)
                    mesh.Box(P(side*.368f,-.161f,-.014f-slot*.046f), P(.009f,.011f,.026f), Upright,ink);
            Quaternion panel = Quaternion.Euler(17,0,0);
            Vector3 center=P(0,.049f,.226f);
            Rounded(mesh,center,.743f,.423f,.033f,.036f,panel,alloy);
            Rounded(mesh,center+panel*P(0,-.002f,-.020f),.696f,.384f,.010f,.028f,panel,ink);
            Rounded(mesh,center+panel*P(0,-.004f,-.027f),.644f,.333f,.005f,.018f,panel,new Color(.11f,.18f,.25f));
            Rounded(mesh,center+panel*P(-.206f,-.023f,-.034f),.187f,.273f,.003f,.008f,panel,new Color(.22f,.35f,.40f));
            for(int line=0;line<9;line++)
            {
                float y=.090f-line*.027f;
                ScreenDash(mesh,center+panel*P(-.206f,y,-.038f),.10f+.034f*Mathf.Sin(line*2.2f),.008f,panel,
                    line%3==0?Gem(Jade,.66f):new Color(.47f,.60f,.67f));
                ScreenDash(mesh,center+panel*P(.098f+.025f*Mathf.Sin(line),y,-.037f),.24f+.072f*Mathf.Sin(line*2.21f),.008f,panel,
                    line%2==0?Gem(Indigo,.62f):Gem(Jade,.55f));
            }
            Lens(mesh,center+panel*P(0,.181f,-.027f),.006f,.004f,Gem(Jade,.65f),true);
            mesh.Tube(new[]{P(-.295f,-.149f,.168f),P(0,-.149f,.168f),P(.295f,-.149f,.168f)},
                new[]{.026f,.023f,.026f},14,SofterSilver());
        }

        private static Color SofterSilver() => new Color(.79f,.88f,.91f);

        private static void ScreenDash(WorldMesh mesh,Vector3 center,float width,float height,Quaternion angle,Color color) =>
            mesh.Box(center,P(width,height,.0028f),angle,color);

        private static void Rounded(WorldMesh mesh,Vector3 center,float width,float height,float depth,float radius,Quaternion angle,Color color)
        {
            if (radius<=0) { mesh.Box(center,P(width,height,depth),angle,color); return; }
            const int curve=4;
            const int count=4*curve;
            for (int i=0;i<count;i++)
            {
                int next=(i+1)%count;
                Vector3 near=RoundedPoint(i,curve,width,height,depth,radius);
                Vector3 after=RoundedPoint(next,curve,width,height,depth,radius);
                Vector3 nearBack=P(near.x,near.y,depth*.5f), afterBack=P(after.x,after.y,depth*.5f);
                mesh.Quad(center+angle*near,center+angle*after,center+angle*afterBack,center+angle*nearBack,
                    WorldMesh.Shade(color,.82f));
                mesh.Triangle(center+angle*P(0,0,-depth*.5f),center+angle*after,center+angle*near,color);
                mesh.Triangle(center+angle*P(0,0,depth*.5f),center+angle*nearBack,center+angle*afterBack,color);
            }
        }

        private static Vector3 RoundedPoint(int i,int curve,float width,float height,float depth,float radius)
        {
            int quadrant=i/curve;
            float start=quadrant*Mathf.PI*.5f;
            float angle=start+(Mathf.PI*.5f)*(i%curve)/(curve-1);
            float mid=start+Mathf.PI*.25f;
            return P(Mathf.Sign(Mathf.Cos(mid))*(width*.5f-radius)+radius*Mathf.Cos(angle),
                Mathf.Sign(Mathf.Sin(mid))*(height*.5f-radius)+radius*Mathf.Sin(angle),-depth*.5f);
        }

        private static void Lens(WorldMesh mesh,Vector3 center,float radius,float depth,Color color,bool front)
        {
            for (int i=0;i<16;i++)
            {
                float a=i*Tau/16,b=(i+1)*Tau/16;
                Vector3 p=center+P(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0);
                Vector3 q=center+P(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,0);
                Vector3 dome=center+Vector3.forward*(front?-depth:depth);
                if(front) mesh.Triangle(dome,q,p,color);
                else mesh.Triangle(dome,p,q,color);
            }
        }

        private Model BuildChest(int design)
        {
            var body=new WorldMesh(meshes);
            var lid=new WorldMesh(meshes);
            float depth;
            float height;
            int shape = design < RuinAtlas.WelcomeChestStyle ? design : AdditionalChestShapes[design - RuinAtlas.WelcomeChestStyle];
            switch (shape)
            {
                case 0:
                    depth=.69f;
                    height=.45f;
                    Trunk(body,lid,depth,height);
                    break;
                case 1:
                    depth=.69f;
                    height=.46f;
                    Astrolabe(body,lid,depth,height);
                    break;
                case 2:
                    depth=.56f;
                    height=.42f;
                    Reliquary(body,lid,depth,height);
                    break;
                case 3:
                    depth=.57f;
                    height=.38f;
                    Lacquer(body,lid,depth,height);
                    break;
                case 4:
                    depth=.94f;
                    height=.32f;
                    Sarcophagus(body,lid,depth,height);
                    break;
                case 5:
                    depth=.56f;
                    height=.49f;
                    Nautilus(body,lid,depth,height);
                    break;
                case 6:
                    depth=.64f;
                    height=.46f;
                    Strongbox(body,lid,depth,height);
                    break;
                case 7:
                    depth=.68f;
                    height=.25f;
                    Folio(body,lid,depth,height);
                    break;
                case 8:
                    depth=.68f;
                    height=.34f;
                    Lotus(body,lid,depth,height);
                    break;
                default:
                    depth=.56f;
                    height=.58f;
                    Hourglass(body,lid,depth,height);
                    break;
            }
            AddChestLanternPlinth(body, depth, height, design >= RuinAtlas.WelcomeChestStyle);
            return new Model {
                Shell=body.Finish(ChestNames[design]+" · base"),
                Lid=lid.Finish(ChestNames[design]+" · lid"),
                Hinge=P(0,height,depth*.5f)
            };
        }

        private static void AddChestLanternPlinth(WorldMesh body,float depth,float height,bool discovery)
        {
            Color stone = new Color(.34f,.24f,.155f);
            Color amber = Gem(new Color(1f,.69f,.26f),.91f);
            Rounded(body,P(0,-.194f,0),1.15f,.30f,depth + .24f,.025f,Upright,stone);
            Rounded(body,P(0,-.040f,0),1.29f,.075f,depth + .32f,.031f,Upright,Brass);
            float front = -(depth + .32f) * .5f - .010f;
            body.Box(P(0,-.036f,front),P(.93f,.041f,.016f),Upright,amber);
            for(int s=-1;s<=1;s+=2)
            {
                body.Box(P(s*.651f,-.034f,0),P(.022f,.039f,depth*.61f),Upright,amber);
                if(discovery) Lens(body,P(s*.40f,height*.54f,front),.049f,.022f,amber,true);
            }
        }

        private static void Trunk(WorldMesh body,WorldMesh lid,float depth,float height)
        {
            Color cedar=new Color(.34f,.17f,.12f), leather=new Color(.19f,.11f,.10f);
            float z=-depth*.5f;
            Rounded(body,P(0,.23f,0),.98f,.38f,depth,.072f,Upright,cedar);
            Rounded(body,P(0,.08f,0),1.01f,.10f,depth+.035f,.043f,Upright,leather);
            Rounded(body,P(0,height-.026f,0),1.04f,.053f,depth+.018f,.023f,Upright,Gold);
            Vault(lid,P(0,.010f,z),1.02f,depth*.505f,.24f,cedar,8,0);
            for(int row=0;row<3;row++)
                body.Box(P(0,.15f+row*.083f,z-.014f),P(.85f,.013f,.014f),Upright,WorldMesh.Shade(cedar,1.3f));
            for(int side=-1;side<=1;side+=2)
            {
                body.Box(P(side*.348f,.226f,z-.026f),P(.096f,.341f,.028f),Upright,Gold);
                Vault(lid,P(side*.349f,.016f,z),.091f,depth*.516f,.253f,Gold,10,0);
                for(int row=0;row<3;row++) Lens(body,P(side*.348f,.130f+row*.105f,z-.045f),.024f,.014f,PaleGold,true);
                Foot(body,side*.391f,.019f,depth*.357f,Brass);
                Foot(body,side*.391f,.019f,-depth*.357f,Brass);
            }
            Lock(body,P(0,height-.096f,z-.037f),.084f,Gold,PaleGold);
            Scroll(body,P(-.145f,.273f,z-.038f),.107f,PaleGold,1);
            Scroll(body,P(.145f,.273f,z-.038f),.107f,PaleGold,-1);
        }

        private static void Astrolabe(WorldMesh body,WorldMesh lid,float depth,float height)
        {
            Color navy=new Color(.12f,.25f,.39f);
            float z=-depth*.5f;
            Drum(body,Vector3.up*.027f,.065f,.50f,.37f,.48f,.35f,Gold,8,Mathf.PI/8);
            Drum(body,Vector3.up*.073f,.325f,.481f,.355f,.456f,.358f,navy,8,Mathf.PI/8);
            Drum(body,Vector3.up*.395f,.058f,.47f,.357f,.482f,.365f,Brass,8,Mathf.PI/8);
            Drum(lid,P(0,.002f,z),.09f,.487f,.357f,.477f,.35f,Gold,8,Mathf.PI/8);
            Drum(lid,P(0,.088f,z),.11f,.474f,.345f,.20f,.196f,navy,8,Mathf.PI/8);
            Drum(lid,P(0,.196f,z),.047f,.218f,.213f,.152f,.155f,Brass,16,0);
            Vector3 dial=P(0,.275f,z-.001f);
            Ring(lid,dial,.14f,.013f,PaleGold,Quaternion.Euler(90,0,0));
            Ring(lid,dial+Vector3.up*.012f,.104f,.009f,Gem(Indigo),Quaternion.Euler(90,0,0));
            for(int i=0;i<16;i++)
            {
                float a=i*Tau/16;
                lid.Box(dial+P(Mathf.Cos(a)*.122f,-.024f,Mathf.Sin(a)*.122f),P(.037f,.012f,.009f),
                    Quaternion.Euler(0,-a*Mathf.Rad2Deg,0),i%4==0?PaleGold:Brass);
            }
            lid.Box(dial+Vector3.up*.011f,P(.195f,.009f,.013f),Quaternion.Euler(0,-24,0),Gem(PaleGold,.8f));
            lid.Rock(dial+Vector3.up*.027f,Vector3.one*.034f,Gem(Indigo,.8f),.7f,7,14,.03f);
            Lock(body,P(0,.253f,z-.020f),.090f,Gold,Indigo);
            Ring(body,P(0,.253f,z-.039f),.145f,.012f,Brass,Upright);
            for(int s=-1;s<=1;s+=2)
                for(int i=0;i<3;i++)
                    Lens(body,P(s*(.18f+i*.04f),.363f-i*.055f,z+.013f),.021f,.008f,PaleGold,true);
        }

        private static void Reliquary(WorldMesh body,WorldMesh lid,float depth,float height)
        {
            Color blue=new Color(.14f,.26f,.42f), crystal=new Color(.46f,.73f,.93f);
            float z=-depth*.5f;
            Drum(body,Vector3.up*.024f,.075f,.445f,.320f,.353f,.262f,Silver,8,Mathf.PI/8);
            Drum(body,Vector3.up*.094f,.262f,.353f,.268f,.292f,.233f,blue,8,Mathf.PI/8);
            Drum(body,Vector3.up*.351f,.070f,.290f,.228f,.333f,.258f,PaleGold,8,Mathf.PI/8);
            Drum(lid,P(0,0,z),.054f,.332f,.258f,.296f,.236f,Silver,8,Mathf.PI/8);
            Crystal(lid,P(0,.051f,z),.282f,.43f,crystal);
            for(int i=0;i<6;i++)
            {
                float a=i*Tau/6;
                Vector3 c=P(Mathf.Cos(a)*.199f,.049f,z+Mathf.Sin(a)*.158f);
                Crystal(lid,c,.087f,.175f+.064f*Mathf.Sin(i*3.71f),i%2==0?crystal:Indigo);
                Vector3 at=P(Mathf.Cos(a)*.305f,.129f,Mathf.Sin(a)*.244f);
                body.Box(at+Vector3.up*.1f,P(.037f,.219f,.028f),Quaternion.Euler(0,-a*Mathf.Rad2Deg,0),PaleGold);
            }
            Lock(body,P(0,.272f,z+.011f),.079f,Silver,Indigo);
        }

        private static void Lacquer(WorldMesh body,WorldMesh lid,float depth,float height)
        {
            Color lacquer=new Color(.49f,.10f,.16f), walnut=new Color(.23f,.13f,.19f);
            float z=-depth*.5f;
            Rounded(body,P(0,.205f,0),1.01f,.29f,depth,.043f,Upright,lacquer);
            Rounded(body,P(0,.071f,0),1.06f,.064f,depth+.03f,.025f,Upright,walnut);
            Rounded(body,P(0,.350f,0),1.05f,.054f,depth+.023f,.025f,Upright,Gold);
            Rounded(lid,P(0,.020f,z),1.124f,.048f,depth+.05f,.022f,Upright,walnut);
            Roof(lid,P(0,.042f,z),1.07f,depth*.49f,.255f,lacquer);
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=-2;i<=2;i++)
                {
                    float u=i*.182f;
                    lid.Tube(new[]{P(u,.251f,z),P(u,.190f,z+side*.123f),P(u,.105f,z+side*.249f),
                        P(u,.075f,z+side*.277f)},new[]{.018f,.016f,.016f,.020f},8,Gold);
                }
                Foot(body,side*.451f,.02f,depth*.394f,Gold);
                Foot(body,side*.451f,.02f,-depth*.394f,Gold);
                Scroll(body,P(side*.165f,.228f,z-.017f),.125f,Jade,side);
            }
            lid.Tube(new[]{P(-.507f,.279f,z),P(-.38f,.248f,z),P(0,.265f,z),P(.38f,.248f,z),P(.507f,.279f,z)},
                new[]{.021f,.032f,.030f,.032f,.021f},9,Gold);
            Lock(body,P(0,.236f,z-.016f),.070f,Gold,Jade);
        }

        private static void Sarcophagus(WorldMesh body,WorldMesh lid,float depth,float height)
        {
            Color ivory=new Color(.72f,.66f,.48f), lapis=new Color(.21f,.39f,.54f);
            float z=-depth*.5f;
            TombBand(body,Vector3.up*.026f,.095f,.62f,depth,ivory);
            TombBand(body,Vector3.up*.116f,.150f,.585f,depth*.962f,lapis);
            TombBand(body,Vector3.up*.259f,.060f,.615f,depth*.984f,Gold);
            TombBand(lid,P(0,0,z),.079f,.60f,depth*.968f,ivory);
            TombBand(lid,P(0,.076f,z),.043f,.532f,depth*.86f,lapis);
            Quaternion relief=Quaternion.Euler(90,0,0);
            Ring(lid,P(0,.133f,z+.131f),.133f,.010f,Gold,relief);
            var eye=P(0,.141f,z+.135f);
            for(int i=-1;i<=1;i+=2)
                lid.Tube(new[]{eye+P(-.15f,0,0),eye+P(-.076f,0,.048f*i),eye+P(.064f,0,.048f*i),eye+P(.15f,0,0)},
                    new[]{.005f,.012f,.012f,.005f},8,PaleGold);
            lid.Rock(eye, P(.029f,.021f,.035f),Gem(Indigo),.4f,7,13,.08f);
            for(int branch=-3;branch<=3;branch++)
            {
                float x=branch*.044f;
                lid.Tube(new[]{P(x,.133f,z+.023f),P(x*1.24f,.126f,z-.102f),P(x*.68f,.121f,z-.25f),
                    P(x*.45f,.116f,z-.379f)},new[]{.009f,.009f,.006f,.003f},7,branch%2==0?Gold:Gem(Jade,.6f));
            }
            Lock(body,P(0,.185f,z+.039f),.067f,Brass,Indigo);
        }

        private static void Nautilus(WorldMesh body,WorldMesh lid,float depth,float height)
        {
            Color shell=new Color(.64f,.45f,.38f), pearl=new Color(.80f,.66f,.55f), bronze=new Color(.34f,.35f,.44f);
            float z=-depth*.5f;
            Drum(body,Vector3.up*.028f,.09f,.364f,.29f,.444f,.31f,bronze,18,0);
            Drum(body,Vector3.up*.111f,.331f,.445f,.31f,.392f,.28f,shell,18,0);
            Drum(body,Vector3.up*.435f,.055f,.390f,.280f,.408f,.294f,pearl,18,0);
            Drum(lid,P(0,0,z),.059f,.408f,.294f,.394f,.285f,bronze,18,0);
            lid.Rock(P(0,.076f,z),P(.387f,.18f,.267f),pearl,.2f,12,24,.06f);
            for(int i=0;i<15;i++)
            {
                float a=i*Tau/15;
                float x=Mathf.Cos(a), f=Mathf.Sin(a);
                lid.Tube(new[]{P(x*.389f,.049f,z+f*.283f),P(x*.311f,.185f,z+f*.231f),
                    P(x*.183f,.225f,z+f*.139f),P(x*.034f,.254f,z+f*.04f)},new[]{.012f,.015f,.013f,.008f},8,
                    i%3==0?Gold:bronze);
            }
            lid.Rock(P(0,.252f,z),P(.060f,.04f,.055f),Gem(Jade,.68f),.6f,8,14,.04f);
            Spiral(body,P(-.105f,.285f,z-.029f),.157f,.014f,PaleGold);
            Ring(body,P(-.105f,.285f,z-.03f),.167f,.007f,bronze,Upright);
            Lock(body,P(.170f,.221f,z-.008f),.065f,Gold,Jade);
        }

        private static void Strongbox(WorldMesh body,WorldMesh lid,float depth,float height)
        {
            Color gunmetal=new Color(.22f,.31f,.36f), ceramic=new Color(.31f,.41f,.47f), copper=new Color(.73f,.39f,.22f);
            float z=-depth*.5f;
            Rounded(body,P(0,.241f,0),.98f,.375f,depth,.093f,Upright,gunmetal);
            Rounded(body,P(0,.416f,0),1.03f,.077f,depth+.035f,.028f,Upright,ceramic);
            Rounded(lid,P(0,.080f,z),1.018f,.157f,depth+.027f,.047f,Upright,gunmetal);
            Rounded(lid,P(0,.170f,z),.670f,.083f,.395f,.035f,Upright,ceramic);
            for(int side=-1;side<=1;side+=2)
            {
                Rounded(body,P(side*.417f,.208f,z-.021f),.149f,.241f,.041f,.025f,Upright,ceramic);
                for(int i=0;i<3;i++)
                {
                    Lens(body,P(side*.425f,.13f+i*.075f,z-.053f),.021f,.010f,Silver,true);
                    lid.Box(P(side*.42f,.073f,-.138f-i*.163f),P(.198f,.199f,.041f),Upright,ceramic);
                }
                for(int row=0;row<3;row++)
                    body.Box(P(side*.24f,.17f+row*.058f,z-.032f),P(.127f,.014f,.018f),Upright,
                        row==1?Gem(Jade,.75f):copper);
            }
            Ring(body,P(0,.249f,z-.046f),.134f,.024f,copper,Upright,12);
            Ring(body,P(0,.249f,z-.065f),.093f,.012f,Silver,Upright,16);
            for(int spoke=0;spoke<3;spoke++)
                body.Box(P(0,.249f,z-.064f),P(.029f,.161f,.027f),Quaternion.Euler(0,0,spoke*60),Silver);
            Lens(body,P(0,.249f,z-.088f),.041f,.014f,Gem(Jade,.82f),true);
        }

        private static void Folio(WorldMesh body,WorldMesh lid,float depth,float height)
        {
            Color midnight=new Color(.20f,.13f,.38f), pages=new Color(.73f,.63f,.42f);
            float z=-depth*.5f;
            Rounded(body,P(0,.041f,0),.965f,.062f,depth,.034f,Upright,midnight);
            for(int i=0;i<13;i++)
                Rounded(body,P(0,.073f+i*.011f,.007f),.901f,.010f,.618f+Mathf.Sin(i*2.12f)*.014f,.004f,Upright,
                    WorldMesh.Shade(pages,.84f+.18f*Mathf.Cos(i*1.14f)));
            Rounded(body,P(0,.135f,.317f),.911f,.20f,.085f,.031f,Upright,midnight);
            Rounded(lid,P(0,.031f,z),.972f,.061f,depth,.028f,Upright,midnight);
            Rounded(lid,P(0,.064f,z),.847f,.013f,.555f,.012f,Upright,Brass);
            Rounded(lid,P(0,.074f,z),.809f,.012f,.516f,.009f,Upright,midnight);
            Quaternion cover=Quaternion.Euler(90,0,0);
            Ring(lid,P(0,.084f,z),.192f,.009f,PaleGold,cover,22);
            Ring(lid,P(0,.084f,z),.170f,.004f,Gold,cover,22);
            for(int side=-1;side<=1;side+=2)
            {
                lid.Box(P(side*.323f,.047f,-.644f),P(.053f,.111f,.064f),Upright,Gold);
                for(int edge=-1;edge<=1;edge+=2)
                    lid.Box(P(side*.441f,.072f,z+edge*.300f),P(.078f,.018f,.066f),
                        Quaternion.Euler(0,side*edge*31,0),Gold);
                body.Box(P(side*.322f,.194f,z-.018f),P(.062f,.105f,.035f),Upright,Brass);
                Lens(body,P(side*.322f,.194f,z-.042f),.028f,.009f,Gem(Indigo,.74f),true);
            }
            lid.Rock(P(0,.094f,z),P(.078f,.031f,.076f),Gem(Indigo,.65f),.43f,8,16,.06f);
            for(int i=0;i<8;i++)
            {
                float a=i*Tau/8;
                lid.Tube(new[]{P(0,.088f,z),P(Mathf.Cos(a)*.095f,.091f,z+Mathf.Sin(a)*.095f),
                    P(Mathf.Cos(a)*.137f,.089f,z+Mathf.Sin(a)*.137f)},new[]{.003f,.010f,.007f},7,Gold);
            }
        }

        private static void Lotus(WorldMesh body,WorldMesh lid,float depth,float height)
        {
            Color celadon=new Color(.23f,.44f,.42f), vine=new Color(.21f,.37f,.32f), amber=new Color(.78f,.48f,.37f);
            float z=-depth*.5f;
            Drum(body,Vector3.up*.023f,.075f,.348f,.316f,.264f,.245f,Gold,18,0);
            Drum(body,Vector3.up*.095f,.11f,.252f,.237f,.362f,.337f,celadon,20,0);
            Drum(body,Vector3.up*.201f,.14f,.364f,.34f,.356f,.334f,vine,20,0);
            Drum(lid,P(0,0,z),.083f,.354f,.335f,.333f,.314f,celadon,20,0);
            for(int i=0;i<12;i++)
            {
                float a=i*Tau/12;
                Vector3 radial=P(Mathf.Cos(a),0,Mathf.Sin(a)), tangent=P(-Mathf.Sin(a),0,Mathf.Cos(a));
                body.Leaf(radial*.26f+Vector3.up*.06f,radial*.403f+Vector3.up*.243f,tangent,.116f,
                    i%2==0?celadon:WorldMesh.Shade(celadon,.78f));
                lid.Leaf(P(0,.054f,z)+radial*.315f,P(0,.333f,z)+radial*.093f,tangent,.142f,
                    i%2==0?amber:WorldMesh.Shade(amber,.71f));
                lid.Leaf(P(0,.122f,z)+radial*.193f,P(0,.359f,z)+radial*.04f,tangent,.090f,
                    i%2==0?Gold:amber);
                lid.Tube(new[]{P(0,.049f,z)+radial*.33f,P(0,.16f,z)+radial*.256f,P(0,.33f,z)+radial*.087f},
                    new[]{.011f,.013f,.004f},8,i%2==0?Gold:Gem(Jade,.59f));
            }
            lid.Rock(P(0,.316f,z),P(.094f,.098f,.086f),Gem(PaleGold,.79f),.14f,12,18,.09f);
            Lock(body,P(0,.261f,z-.011f),.068f,Gold,Jade);
        }

        private static void Hourglass(WorldMesh body,WorldMesh lid,float depth,float height)
        {
            Color cage=new Color(.57f,.44f,.29f), cyan=new Color(.30f,.58f,.68f);
            float z=-depth*.5f;
            Drum(body,Vector3.up*.026f,.064f,.383f,.311f,.39f,.315f,Gold,8,Mathf.PI/8);
            Drum(body,Vector3.up*.085f,.042f,.385f,.310f,.340f,.282f,cage,8,Mathf.PI/8);
            Drum(body,Vector3.up*.126f,.197f,.241f,.207f,.064f,.063f,cyan,14,0);
            Drum(body,Vector3.up*.317f,.187f,.064f,.063f,.221f,.193f,cyan,14,0);
            Drum(body,Vector3.up*.496f,.063f,.324f,.283f,.377f,.31f,Gold,8,Mathf.PI/8);
            for(int side=-1;side<=1;side+=2)
            for(int edge=-1;edge<=1;edge+=2)
            {
                body.Tube(new[]{P(side*.257f,.100f,edge*.213f),P(side*.293f,.244f,edge*.206f),
                    P(side*.303f,.373f,edge*.223f),P(side*.259f,.518f,edge*.218f)},new[]{.034f,.022f,.023f,.031f},10,Gold);
                body.Box(P(side*.264f,.072f,edge*.236f),P(.125f,.077f,.111f),Upright,cage);
            }
            Ring(body,P(0,.299f,-.064f),.073f,.012f,Gem(Jade,.74f),Upright,15);
            for(int i=0;i<5;i++)
                body.Rock(P(.011f*Mathf.Sin(i*2.2f),.143f+i*.029f,-.049f),Vector3.one*.015f,Gem(PaleGold,.83f),i*.4f,4,9,.12f);
            Drum(lid,P(0,0,z),.065f,.384f,.312f,.343f,.287f,cage,8,Mathf.PI/8);
            Drum(lid,P(0,.059f,z),.084f,.33f,.281f,.233f,.209f,Gold,8,Mathf.PI/8);
            Drum(lid,P(0,.137f,z),.044f,.231f,.208f,.106f,.101f,cage,8,Mathf.PI/8);
            lid.Rock(P(0,.207f,z),Vector3.one*.060f,Gem(Jade,.65f),.46f,8,12,.047f);
            Lock(body,P(0,.519f,z-.028f),.061f,Gold,Jade);
        }

        private static void Vault(WorldMesh mesh,Vector3 center,float width,float radius,float rise,Color color,int segments,float roll)
        {
            Quaternion tilt=Quaternion.Euler(0,0,roll);
            Vector3 last=P(0,0,radius);
            for(int row=0;row<segments;row++)
            {
                float angle=(row+1)*Mathf.PI/segments;
                var next=P(0,Mathf.Sin(angle)*rise,Mathf.Cos(angle)*radius);
                Vector3 edge=tilt*Vector3.right*(width*.5f);
                Vector3 a=center+tilt*last,b=center+tilt*next;
                mesh.Quad(a-edge,a+edge,b+edge,b-edge,WorldMesh.Shade(color,.87f+.16f*Mathf.Sin(angle)));
                mesh.Triangle(center-edge,b-edge,a-edge,WorldMesh.Shade(color,.86f));
                mesh.Triangle(center+edge,a+edge,b+edge,WorldMesh.Shade(color,1.07f));
                last=next;
            }
        }

        private static void Roof(WorldMesh mesh,Vector3 center,float width,float depth,float rise,Color color)
        {
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 ridge=center+Vector3.up*rise;
                Vector3 shoulder=center+P(0,rise*.47f,.64f*depth*side);
                Vector3 eave=center+P(0,rise*.16f,depth*side);
                float x=width*.5f;
                mesh.Quad(ridge+Vector3.right*x*.86f,ridge-Vector3.right*x*.86f,
                    shoulder-Vector3.right*x*.94f,shoulder+Vector3.right*x*.94f,color);
                mesh.Quad(shoulder+Vector3.right*x*.94f,shoulder-Vector3.right*x*.94f,
                    eave-Vector3.right*x,eave+Vector3.right*x,WorldMesh.Shade(color,.83f));
                mesh.Triangle(ridge+Vector3.right*x*.86f,shoulder+Vector3.right*x*.94f,
                    eave+Vector3.right*x,Gold);
                mesh.Triangle(ridge-Vector3.right*x*.86f,eave-Vector3.right*x,
                    shoulder-Vector3.right*x*.94f,Gold);
            }
        }

        private static void Drum(WorldMesh mesh,Vector3 center,float height,float lowerX,float lowerZ,
            float upperX,float upperZ,Color color,int edges,float phase)
        {
            for(int index=0;index<edges;index++)
            {
                float angle=phase+index*Tau/edges, next=phase+(index+1)*Tau/edges;
                float x=Mathf.Cos(angle), z=Mathf.Sin(angle), u=Mathf.Cos(next), v=Mathf.Sin(next);
                Vector3 bottom=center+P(x*lowerX,0,z*lowerZ), top=center+P(x*upperX,height,z*upperZ);
                Vector3 bottomNext=center+P(u*lowerX,0,v*lowerZ), topNext=center+P(u*upperX,height,v*upperZ);
                mesh.Quad(bottom,top,topNext,bottomNext,WorldMesh.Shade(color,.84f+.13f*Mathf.Cos(angle*2)));
                mesh.Triangle(center,bottom,bottomNext,WorldMesh.Shade(color,.74f));
                mesh.Triangle(center+Vector3.up*height,topNext,top,color);
            }
        }

        private static void Crystal(WorldMesh mesh,Vector3 point,float radius,float height,Color color)
        {
            Drum(mesh,point,height*.71f,radius,radius*.86f,radius*.81f,radius*.72f,color,6,Mathf.PI/6);
            Drum(mesh,point+Vector3.up*(height*.71f),height*.29f,radius*.81f,radius*.72f,.003f,.003f,
                Gem(color,.42f),6,Mathf.PI/6);
        }

        private static readonly Vector2[] MonumentOutline = {
            new Vector2(-.29f,-.48f), new Vector2(.29f,-.48f), new Vector2(.44f,-.30f), new Vector2(.50f,.18f),
            new Vector2(.35f,.43f), new Vector2(0,.52f), new Vector2(-.35f,.43f), new Vector2(-.50f,.18f),
            new Vector2(-.44f,-.30f)
        };

        private static void TombBand(WorldMesh mesh,Vector3 center,float height,float width,float depth,Color color)
        {
            for(int i=0;i<MonumentOutline.Length;i++)
            {
                Vector2 a=MonumentOutline[i], b=MonumentOutline[(i+1)%MonumentOutline.Length];
                Vector3 p=center+P(a.x*width,0,a.y*depth), q=center+P(b.x*width,0,b.y*depth);
                Vector3 top=center+P(a.x*width*.96f,height,a.y*depth*.97f);
                Vector3 after=center+P(b.x*width*.96f,height,b.y*depth*.97f);
                mesh.Quad(p,q,after,top,WorldMesh.Shade(color,.91f+.09f*Mathf.Sin(i*2.8f)));
                mesh.Triangle(center+Vector3.up*height,after,top,color);
                mesh.Triangle(center,p,q,WorldMesh.Shade(color,.78f));
            }
        }

        private static void Foot(WorldMesh mesh,float x,float y,float z,Color color)
        {
            Drum(mesh,P(x,y,z),.030f,.060f,.050f,.058f,.049f,color,10,0);
            Drum(mesh,P(x,y+.028f,z),.068f,.057f,.049f,.032f,.036f,WorldMesh.Shade(color,.82f),10,0);
        }

        private static void Lock(WorldMesh mesh,Vector3 at,float size,Color metal,Color jewel)
        {
            Rounded(mesh,at,size*1.43f,size*1.18f,.038f,size*.17f,Quaternion.Euler(0,0,45),metal);
            Lens(mesh,at-Vector3.forward*.026f,size*.40f,.012f,Gem(jewel,.80f),true);
            Ring(mesh,at-Vector3.forward*.026f,size*.49f,size*.055f,WorldMesh.Shade(metal,1.16f),Upright,13);
        }

        private static void Ring(WorldMesh mesh,Vector3 at,float radius,float thickness,Color metal,Quaternion turn,int segments=30)
        {
            var path=new Vector3[segments+1];
            var sizes=new float[path.Length];
            for(int i=0;i<path.Length;i++)
            {
                float a=i*Tau/segments;
                path[i]=at+turn*P(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0);
                sizes[i]=thickness;
            }
            mesh.Tube(path,sizes,7,metal);
        }

        private static void Scroll(WorldMesh mesh,Vector3 at,float width,Color enamel,int direction)
        {
            Vector3[] curve=new Vector3[25];
            var sizes=new float[curve.Length];
            for(int i=0;i<curve.Length;i++)
            {
                float t=i/(curve.Length-1f);
                float a=t*Tau*1.18f;
                curve[i]=at+P(direction*width*(Mathf.Sin(a)*t*.56f+t*.36f),width*Mathf.Cos(a)*t*.61f,0);
                sizes[i]=.003f+.008f*Mathf.Sin(t*Mathf.PI);
            }
            mesh.Tube(curve,sizes,8,enamel);
            Lens(mesh,curve[curve.Length-1],.012f,.006f,Gem(enamel,.63f),true);
        }

        private static void Spiral(WorldMesh mesh,Vector3 at,float width,float thickness,Color enamel)
        {
            const int points=43;
            var shape=new Vector3[points];
            var sizes=new float[points];
            for(int i=0;i<points;i++)
            {
                float t=i/(points-1f), a=t*Tau*1.47f;
                shape[i]=at+P(width*t*Mathf.Cos(a),width*t*Mathf.Sin(a),0);
                sizes[i]=thickness*(.33f+.67f*t);
                if (i%3==0)
                {
                    float start=width*t*.8f;
                    var p=at+P(start*Mathf.Cos(a-.23f),start*Mathf.Sin(a-.23f),0);
                    if ((shape[i]-p).sqrMagnitude>.00001f)
                        mesh.Tube(new[]{shape[i],p},new[]{thickness*.45f,thickness*.27f},7,enamel);
                }
            }
            mesh.Tube(shape,sizes,8,enamel);
        }

        public void Dispose()
        {
            foreach(Element item in artifacts) Object.Destroy(item.Root.gameObject);
            foreach(Element item in chests) Object.Destroy(item.Root.gameObject);
            foreach(Mesh mesh in meshes) Object.Destroy(mesh);
            Object.Destroy(artifactMaterial);
            Object.Destroy(chestMaterial);
        }
    }
}
