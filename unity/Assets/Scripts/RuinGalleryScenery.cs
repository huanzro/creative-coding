using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private readonly struct GalleryBayMesh
        {
            public readonly Mesh Structure, Light;

            public GalleryBayMesh(Mesh structure, Mesh light)
            {
                Structure = structure;
                Light = light;
            }
        }

        private sealed class GalleryBay
        {
            public Transform Root;
            public MeshFilter Structure, Trim;
            public MeshRenderer TrimRenderer;
            public int Room = -1;
        }

        private const int GalleryBays = 56;
        private readonly GalleryBay[] ruinGalleryBays = new GalleryBay[GalleryBays];
        private readonly GalleryBayMesh?[] ruinGalleryMeshes = new GalleryBayMesh?[16];
        private static readonly string[] GalleryNames = {
            "Limestone aqueduct", "Indigo observatory", "Amber foundry",
            "Flooded archives", "Violet lattice", "Mineral vault"
        };
        private static readonly Color[] GalleryStone = {
            new Color(.61f,.47f,.30f), new Color(.21f,.29f,.53f), new Color(.28f,.16f,.125f),
            new Color(.16f,.35f,.30f), new Color(.37f,.25f,.43f), new Color(.20f,.27f,.35f)
        };
        private static readonly Color[] GalleryAccent = {
            new Color(1,.77f,.34f), new Color(.43f,.72f,1), new Color(1,.33f,.08f),
            new Color(.36f,.94f,.53f), new Color(.83f,.45f,1), new Color(.41f,.94f,1)
        };
        private static readonly Color[] RoomAccents = {
            new Color(1,.77f,.34f), new Color(1,.42f,.15f), new Color(.83f,.45f,1),
            new Color(.44f,.69f,1), new Color(.30f,.93f,.65f), new Color(1,.19f,.105f),
            new Color(.93f,.53f,.71f), new Color(.29f,.89f,1), new Color(.68f,.93f,.33f),
            new Color(.98f,.91f,.64f), new Color(.42f,.49f,1), new Color(.45f,.95f,1),
            new Color(.69f,.58f,1), new Color(.98f,.66f,.29f), new Color(.83f,.47f,.61f),
            new Color(.62f,.94f,.82f)
        };
        private static readonly Color[] RoomStones = {
            new Color(.61f,.47f,.30f), new Color(.29f,.18f,.125f), new Color(.38f,.25f,.51f),
            new Color(.24f,.33f,.57f), new Color(.16f,.34f,.31f), new Color(.26f,.125f,.123f),
            new Color(.46f,.31f,.43f), new Color(.155f,.31f,.42f), new Color(.28f,.40f,.24f),
            new Color(.52f,.43f,.34f), new Color(.27f,.25f,.50f), new Color(.20f,.31f,.37f),
            new Color(.28f,.24f,.35f), new Color(.34f,.255f,.20f), new Color(.30f,.22f,.26f),
            new Color(.20f,.31f,.31f)
        };

        internal static Color RuinGalleryStone(int style) => GalleryStone[Mathf.Clamp(style, 0, 5)];
        internal static Color RuinGalleryAccent(int style) => GalleryAccent[Mathf.Clamp(style, 0, 5)];

        internal static Color RuinGalleryAccentFor(int style, int room) =>
            (uint)room < RoomAccents.Length ? RoomAccents[room] : RuinGalleryAccent(style);

        internal static Color RuinGalleryStoneFor(int style, int room) =>
            (uint)room < RoomStones.Length ? RoomStones[room] : RuinGalleryStone(style);

        internal static Vector2 RuinGallerySection(int style, float angle)
        {
            float x = Mathf.Cos(angle), y = Mathf.Sin(angle);
            switch (style)
            {
                case 0:
                    return new Vector2(6.55f * SignedGalleryPower(x, .66f) * (1 - .23f * Mathf.Max(0,y)),
                        4.23f + 4.41f * SignedGalleryPower(y, .76f));
                case 1:
                    return new Vector2(6.46f * x * (1 + .07f * Mathf.Cos(angle * 3)),
                        4.41f + 4.62f * y);
                case 2:
                    return new Vector2(6.18f * SignedGalleryPower(x, .43f),
                        3.85f + 4.07f * SignedGalleryPower(y, .39f));
                case 3:
                    return new Vector2(6.35f * SignedGalleryPower(x, .73f) * (1 - .24f * Mathf.Max(0,y)),
                        4.22f + 4.47f * y);
                case 4:
                    return new Vector2(6.35f * SignedGalleryPower(x, .76f) + .78f * Mathf.Max(0,y),
                        3.94f + 4.19f * SignedGalleryPower(y, .62f));
                default:
                    float edge = 1 + .07f * Mathf.Sin(angle * 5) + .05f * Mathf.Cos(angle * 3);
                    return new Vector2(7.07f * x * edge, 4.75f + (y >= 0 ? 5.22f : 4.95f) * y * edge);
            }
        }

        private static float SignedGalleryPower(float value, float power) =>
            Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), power);

        private void DrawRuinGalleryStyle(Journey journey, float time)
        {
            bool arrival = RuinArrivalVisible(journey);
            int count = PlaceRuinGallery(journey, journey.RuinRoomId, journey.Distance, time, 0);
            if (arrival)
                count = PlaceRuinGallery(journey, journey.RuinEntrance.TargetRoom, ruinArrivalDepth, time, count);
            for (; count < GalleryBays; count++)
                if (ruinGalleryBays[count] != null) ruinGalleryBays[count].Root.gameObject.SetActive(false);
        }

        private int PlaceRuinGallery(Journey journey, int room, float playerDepth, float time, int count)
        {
            int style = Mathf.Clamp(journey.RuinRoomStyleFor(room), 0, GalleryNames.Length - 1);
            float length = journey.RuinRoomLengthFor(room);
            if (!ruinGalleryMeshes[room].HasValue) ruinGalleryMeshes[room] = BuildRuinGalleryBay(style, room);
            GalleryBayMesh model = ruinGalleryMeshes[room].Value;
            float spacing = style == 2 ? 4.6f : style == 5 ? 5.4f : 5.1f;
            float first = style == 1 ? 6.10f : style == 4 || style == 5 ? 2.10f : 3.25f;
            for (float depth = first; depth < length - 2.5f && count < GalleryBays; depth += spacing)
            {
                float ahead = RuinTrack.Offset(depth, playerDepth, length);
                if (ahead < -9 || ahead > 38) continue;
                if (!RuinGalleryBayClear(journey, room, depth)) continue;
                var bay = ruinGalleryBays[count] ?? (ruinGalleryBays[count] = CreateRuinGalleryBay());
                var frame = RuinTrack.Frame(room, length, depth);
                bay.Root.gameObject.SetActive(true);
                bay.Root.localPosition = frame.Position;
                bay.Root.localRotation = frame.Rotation;
                if (bay.Room != room)
                {
                    bay.Room = room;
                    bay.Root.name = GalleryNames[style];
                    bay.Structure.sharedMesh = model.Structure;
                    bay.Trim.sharedMesh = model.Light;
                }
                float cadence = .5f + .5f * Mathf.Sin(time * (style == 2 ? 2.31f : .82f) + depth * .31f + room);
                Light(bay.TrimRenderer, Color.white, .36f + (style == 3 ? .30f : .22f) * cadence);
                count++;
            }
            return count;
        }

        private static bool RuinGalleryBayClear(Journey journey, int room, float depth)
        {
            float length = journey.RuinRoomLengthFor(room);
            foreach (RuinPortal portal in journey.RuinPortalsFor(room))
            {
                if (portal.Direction == RuinDirection.Forward || portal.Direction == RuinDirection.Exit)
                    continue;
                float clearDepth = IsVerticalRuinGate(portal.Direction) ? 3.5f : 2.7f;
                if (Mathf.Abs(RuinTrack.Offset(portal.Depth, depth, length)) < clearDepth)
                    return false;
            }
            foreach (RuinObject item in journey.RuinObjectsFor(room))
                if (Mathf.Abs(RuinTrack.Offset(item.Depth, depth, length)) < 1.10f)
                    return false;
            return true;
        }

        private GalleryBay CreateRuinGalleryBay()
        {
            var node = Group("Gallery architecture", roomShell);
            var opaque = RuinPiece("Carved structure", null, tunnelMetal, node);
            var lit = RuinPiece("Architectural inlay", null, glow, node);
            return new GalleryBay {
                Root = node, Structure = opaque.GetComponent<MeshFilter>(), Trim = lit.GetComponent<MeshFilter>(),
                TrimRenderer = lit.GetComponent<MeshRenderer>()
            };
        }

        private GalleryBayMesh BuildRuinGalleryBay(int style, int room)
        {
            WorldMesh structure = Geometry(), emit = Geometry();
            Color accent = RuinGalleryAccentFor(style, room);
            switch (style)
            {
                case 0: BuildAqueductBay(structure, emit, accent, room); break;
                case 1: BuildObservatoryBay(structure, emit, accent, room); break;
                case 2: BuildFoundryBay(structure, emit, accent, room); break;
                case 3: BuildFloodedBay(structure, emit, accent, room); break;
                case 4: BuildLatticeBay(structure, emit, accent, room); break;
                default: BuildMineralBay(structure, emit, accent, room); break;
            }
            return new GalleryBayMesh(structure.Finish(GalleryNames[style] + " architecture " + room),
                emit.Finish(GalleryNames[style] + " lighting " + room));
        }

        private void BuildRuinWayfinding(Transform parent,int room,float length,IReadOnlyList<RuinPortal> portals,Color accent,Color stonework)
        {
            var metal=new WorldMesh(ruinSpatialMeshes);
            var lamps=new WorldMesh(ruinSpatialMeshes);
            // Put low markers between the tall bays, so the hall remains readable between exhibits.
            for(float depth=1.8f;depth<length-1.5f;depth+=4.8f)
            {
                var frame=RuinTrack.Frame(room,length,depth);
                for(int side=-1;side<=1;side+=2)
                {
                    bool nearGate=false;
                    foreach(var portal in portals)
                    {
                        float clearance=RuinTrack.Offset(portal.Depth,depth,length);
                        if((portal.Lane==0 || portal.Lane==side) && Mathf.Abs(clearance)<2.6f) { nearGate=true; break; }
                    }
                    if(nearGate) continue;
                    Vector3 at=frame.Point(side*4.47f,1.14f);
                    Color bronze=WorldMesh.Shade(stonework,1.55f);
                    metal.Box(at,new Vector3(.24f,1.27f,.59f),frame.Rotation,bronze);
                    metal.Box(at+Vector3.up*.50f,new Vector3(.34f,.15f,.77f),frame.Rotation,bronze);
                    lamps.Box(at-frame.Right*(side*.13f),new Vector3(.05f,.69f,.31f),frame.Rotation,accent);
                    lamps.Box(at-frame.Right*(side*.39f)-Vector3.up*1.12f,new Vector3(.84f,.024f,.052f),frame.Rotation,
                        WorldMesh.Shade(accent,.68f));
                }
            }
            foreach(var portal in portals)
            {
                if(portal.Hidden || portal.TargetRoom<0 || portal.Direction==RuinDirection.Forward || portal.Direction==RuinDirection.Exit) continue;
                float depth=portal.Depth-RuinTrack.DepthForMetres(room,length,5.8f);
                var frame=RuinTrack.Frame(room,length,depth);
                float side=portal.Lane==0 ? -.92f : portal.Lane;
                Vector3 at=frame.Point(3.26f*side,2.58f);
                metal.Box(at,new Vector3(1.04f,.85f,.16f),frame.Rotation,WorldMesh.Shade(stonework,1.48f));
                Vector3 direction=portal.Lane==0 ? Vector3.up*(portal.Direction==RuinDirection.Upper ? 1 : -1) : frame.Right*portal.Lane;
                Vector3 across=Vector3.Cross(frame.Forward,direction).normalized;
                at-=frame.Forward*.10f;
                Vector3 tip=at+direction*.26f, neck=at+direction*.015f;
                RuinBar(lamps,at-direction*.23f,tip,.052f,.048f,accent);
                RuinBar(lamps,neck+across*.15f,tip,.046f,.042f,accent);
                RuinBar(lamps,neck-across*.15f,tip,.046f,.042f,accent);
            }
            RuinPiece("Archive lanterns and sign frames",metal.Finish("Gallery wayfinding structure"),tunnelMetal,parent);
            var markers=RuinPiece("Gallery wayfinding lamps",lamps.Finish("Lit lanterns and branch arrows"),glow,parent).GetComponent<MeshRenderer>();
            Light(markers,Color.white,1.08f);
        }

        private static Quaternion GalleryTurn(float x, float y = 0, float z = 0) => Quaternion.Euler(x, y, z);

        private static void BuildAqueductBay(WorldMesh solid, WorldMesh lit, Color accent, int room)
        {
            Color stonework = Color.Lerp(RuinGalleryStoneFor(0,room),new Color(.88f,.81f,.58f),.49f);
            Color lapis = room == 0 ? new Color(.22f,.39f,.64f) : new Color(.34f,.29f,.20f);
            Color trim = new Color(.48f,.30f,.17f);
            float span = room == 0 ? 4.65f : 4.1f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * span;
                solid.Box(new Vector3(x,.28f,0),new Vector3(1.12f,.57f,1.55f),Quaternion.identity,trim);
                solid.Box(new Vector3(x,2.46f,0),new Vector3(.72f,4.20f,.99f),Quaternion.identity,stonework);
                for (int flutes = 0; flutes < 5; flutes++)
                {
                    float z = -.38f + flutes * .19f;
                    RuinBar(solid, new Vector3(x-side*.39f,.55f,z),new Vector3(x-side*.39f,4.39f,z),.11f,.12f,
                        WorldMesh.Shade(stonework,.78f+.10f*(flutes%2)));
                }
                solid.Box(new Vector3(x,4.51f,0),new Vector3(1.13f,.32f,1.41f),Quaternion.identity,trim);
                solid.Box(new Vector3(x,4.81f,0),new Vector3(1.54f,.37f,1.62f),Quaternion.identity,lapis);
                lit.Box(new Vector3(x-side*.57f,4.63f,-.72f),new Vector3(.45f,.057f,.045f),Quaternion.identity,accent);
                lit.Box(new Vector3(x-side*.31f,.021f,-.40f),new Vector3(1.2f,.041f,3.95f),Quaternion.identity,
                    WorldMesh.Shade(accent,.22f));
                if (room == 9)
                {
                    Vector3 plaque = new Vector3(x-side*.46f,2.82f,-.54f);
                    solid.Box(plaque,new Vector3(.24f,1.31f,.53f),Quaternion.identity,lapis);
                    for (int glyph = 0; glyph < 5; glyph++)
                        lit.Box(plaque+new Vector3(-side*.131f,.49f-.23f*glyph,0),new Vector3(.018f,.028f,.24f-.029f*glyph),
                            Quaternion.identity,WorldMesh.Shade(accent,.37f));
                }
            }
            for (int section = 0; section < 14; section++)
            {
                float a = section * Mathf.PI / 14, b = (section + 1) * Mathf.PI / 14;
                Vector3 start = new Vector3(span * Mathf.Cos(a),4.91f+2.99f*Mathf.Sin(a),0);
                Vector3 end = new Vector3(span * Mathf.Cos(b),4.91f+2.99f*Mathf.Sin(b),0);
                RuinBar(solid,start,end,.49f,.91f,section%3 == 0 ? lapis : stonework);
                RuinBar(lit,start+new Vector3(0,-.15f,-.46f),end+new Vector3(0,-.15f,-.46f),.041f,.059f,
                    WorldMesh.Shade(accent,section%2 == 0 ? .73f : .39f));
            }
            for (int inlay = -4; inlay <= 4; inlay++)
            {
                float x = inlay * .73f;
                solid.Box(new Vector3(x,.022f,-.13f),new Vector3(.46f,.023f,.46f),GalleryTurn(0,45),
                    inlay % 2 == 0 ? lapis : stonework);
            }
        }

        private static void BuildObservatoryBay(WorldMesh solid, WorldMesh lit, Color accent, int room)
        {
            Color midnight = WorldMesh.Shade(RuinGalleryStoneFor(1,room),1.26f), silver = new Color(.48f,.60f,.83f);
            Vector3 center = new Vector3(0,6.83f,.17f);
            Quaternion face = GalleryTurn(room % 2 == 0 ? 38 : 62, room * 13 % 36 - 18, 0);
            int ringCount = room == 7 ? 2 : 3;
            for (int ring = 0; ring < ringCount; ring++)
            {
                float radius = 1.12f + .53f * ring;
                Quaternion plane = face * GalleryTurn(14 * ring, 34 * ring);
                GalleryOrbit(lit, center, plane, radius, .032f + .011f * ring, 30,
                    WorldMesh.Shade(accent,.69f - .10f * ring));
                for (int spoke = 0; spoke < 8; spoke++)
                {
                    float t = spoke * Mathf.PI / 4 + ring * .31f;
                    Vector3 at = center + plane * new Vector3(radius * Mathf.Cos(t),radius * Mathf.Sin(t),0);
                    lit.Rock(at,Vector3.one*(.078f+.012f*ring),WorldMesh.Shade(accent,.76f),ring+.414f*spoke,4,7,.056f);
                }
            }
            solid.Rock(center, new Vector3(.41f,.36f,.41f),silver,room*.29f,6,14,.086f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 5.17f;
                if (room == 10) BuildStelae(solid,lit,accent,midnight,side);
                else solid.Box(new Vector3(x,1.57f,.35f),new Vector3(.74f,3.5f,1.02f),GalleryTurn(0,0,side*6),midnight);
                Vector3 surface = new Vector3(x-side*.43f,3.70f,.13f);
                GalleryOrbit(lit,surface,GalleryTurn(0,90*side),.82f,.048f,22,accent);
                solid.Rock(surface,new Vector3(.15f,.72f,.67f),silver,.59f*side+room*.17f,4,7,.08f);
                RuinBar(solid,new Vector3(x,3.83f,.40f),new Vector3(x*.56f,6.97f,.31f),.25f,.42f,silver);
                for (int pin = 0; pin < 6; pin++)
                {
                    float z = -2.40f + pin * .77f;
                    lit.Rock(new Vector3(side*(3.00f+.62f*Mathf.Sin(pin*.94f)),.049f,z),
                        new Vector3(.075f,.026f,.10f),WorldMesh.Shade(accent,.44f),side+pin*.39f,3,6,.10f);
                }
            }
        }

        private static void BuildStelae(WorldMesh solid,WorldMesh lit,Color accent,Color stonework,int side)
        {
            for (int slab = -1; slab <= 1; slab++)
            {
                float x=side*(4.60f+.32f*Mathf.Abs(slab)), z=1.31f*slab;
                float height=3.86f-.79f*Mathf.Abs(slab);
                solid.Box(new Vector3(x,height*.5f,z),new Vector3(.79f,height,.78f),
                    GalleryTurn(0,side*slab*8),stonework);
                for (int row=0;row<8;row++)
                {
                    float y=.53f+row*(height-.87f)/7;
                    lit.Box(new Vector3(x-side*.414f,y,z),new Vector3(.025f,.024f,.31f+.17f*Mathf.Sin(row*.89f)),
                        Quaternion.identity,WorldMesh.Shade(accent,.40f+.018f*row));
                }
                lit.Box(new Vector3(x,height+.032f,z),new Vector3(.75f,.09f,.78f),Quaternion.identity,
                    WorldMesh.Shade(accent,.36f));
            }
        }

        private static void GalleryOrbit(WorldMesh mesh, Vector3 center, Quaternion plane, float radius,
            float width, int segments, Color tint)
        {
            for (int n = 0; n < segments; n++)
            {
                float a = n * Mathf.PI * 2 / segments, b = (n + 1) * Mathf.PI * 2 / segments;
                Vector3 start = center + plane * new Vector3(radius * Mathf.Cos(a),radius * Mathf.Sin(a),0);
                Vector3 end = center + plane * new Vector3(radius * Mathf.Cos(b),radius * Mathf.Sin(b),0);
                RuinBar(mesh,start,end,width,width,WorldMesh.Shade(tint,.76f + .24f * Mathf.Sin(a * 2)));
            }
        }

        private static void BuildFoundryBay(WorldMesh solid, WorldMesh lit, Color accent, int room)
        {
            Color iron = WorldMesh.Shade(RuinGalleryStoneFor(2,room),1.42f);
            Color copper = room == 5 ? new Color(.46f,.23f,.22f) : new Color(.56f,.30f,.16f);
            Color slag = new Color(.18f,.15f,.16f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 4.92f;
                solid.Box(new Vector3(x,1.82f,.47f),new Vector3(1.05f,3.64f,2.13f),Quaternion.identity,iron);
                solid.Box(new Vector3(x-side*.54f,1.45f,.47f),new Vector3(.08f,2.09f,1.81f),Quaternion.identity,slag);
                if (room == 5)
                {
                    Vector3 scanner = new Vector3(x-side*.64f,1.50f,.47f);
                    GalleryOrbit(lit,scanner,GalleryTurn(0,90*side),.79f,.048f,18,accent);
                    solid.Rock(scanner,new Vector3(.166f,.54f,.54f),copper,side*.22f,5,10,.028f);
                }
                for (int grill = 0; grill < 6; grill++)
                {
                    float y = .63f + grill * .30f;
                    solid.Box(new Vector3(x-side*.62f,y,.47f),new Vector3(.13f,.092f,1.66f),GalleryTurn(0,0,side*7),copper);
                    lit.Box(new Vector3(x-side*.57f,y+.083f,.47f),new Vector3(.054f,.071f,1.52f),Quaternion.identity,
                        WorldMesh.Shade(accent,.32f+.07f*(grill % 4)));
                }
                for (int pipe = 0; pipe < 2 + room % 2; pipe++)
                {
                    float y = 4.04f + pipe*.78f;
                    Vector3[] path = { new Vector3(x,y,-3.5f),new Vector3(x,y,-.87f),
                        new Vector3(x-side*.50f,y+.47f,.55f),new Vector3(x-side*.50f,y+.47f,3.49f) };
                    solid.Tube(path,new[] {.22f,.23f,.25f,.22f},10,pipe%2 == 0 ? copper : iron);
                    foreach (Vector3 bend in path)
                        solid.Rock(bend,new Vector3(.28f,.28f,.26f),slag,pipe+.29f*bend.z,4,10,.067f);
                }
                for (int stamp = 0; stamp < 6; stamp++)
                {
                    float z = -2.04f + stamp * .77f;
                    lit.Box(new Vector3(side*3.47f,.035f,z),new Vector3(.55f,.035f,.08f),GalleryTurn(0,side*-40),
                        WorldMesh.Shade(accent,stamp%2 == 0 ? .31f : .56f));
                }
            }
            RuinBar(solid,new Vector3(-4.55f,6.36f,.42f),new Vector3(4.55f,6.36f,.42f),.71f,.94f,iron);
            for (int lug = -6; lug <= 6; lug++)
                lit.Box(new Vector3(lug*.60f,6.11f,-.14f),new Vector3(.27f,.062f,.17f),Quaternion.identity,
                    WorldMesh.Shade(accent,lug%2 == 0 ? .66f : .31f));
        }

        private static void BuildFloodedBay(WorldMesh solid, WorldMesh lit, Color accent, int room)
        {
            Color basin = room == 8 ? new Color(.30f,.39f,.23f) : new Color(.12f,.31f,.35f);
            Color fern = room == 8 ? new Color(.54f,.54f,.25f) : new Color(.31f,.54f,.32f);
            Color quiet = WorldMesh.Shade(RuinGalleryStoneFor(3,room),1.32f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 4.70f;
                solid.Box(new Vector3(x,.012f,0),new Vector3(2.13f,.03f,7.1f),Quaternion.identity,basin);
                solid.Box(new Vector3(side*3.79f,.092f,0),new Vector3(.19f,.24f,7.10f),Quaternion.identity,quiet);
                for (int ripple = 0; ripple < 13; ripple++)
                {
                    float at = -3.18f + .49f * ripple;
                    float alpha = .21f + .11f * Mathf.Sin(ripple * 1.7f + room);
                    lit.Box(new Vector3(x + side * .13f * Mathf.Sin(ripple),.037f,at),new Vector3(.64f+.48f*Mathf.Pow(Mathf.Cos(ripple*1.43f),2),.007f,.044f),
                        GalleryTurn(0,side*7*Mathf.Sin(ripple*.61f)),WorldMesh.Shade(accent,alpha));
                }
                solid.Box(new Vector3(side*5.23f,2.59f,.44f),new Vector3(.67f,4.86f,1.50f),GalleryTurn(0,0,side*10),quiet);
                for (int reed = 0; reed < 5; reed++)
                {
                    float z = -1.92f + .84f * reed;
                    float stem = 1.17f + .80f * Mathf.Sin(reed*1.5f + room*.41f);
                    Vector3 a = new Vector3(x+side*.19f,.09f,z), b = a + new Vector3(-side*.19f,stem*.70f,.21f);
                    Vector3 c = a + new Vector3(-side*.39f,stem,.41f);
                    solid.Tube(new[] {a,b,c},new[] {.048f,.033f,.011f},6,fern);
                    solid.Leaf(b,b+new Vector3(-side*.51f,.42f,-.19f),Vector3.forward,room==8?.28f:.19f,fern);
                    solid.Leaf(b,c+new Vector3(.10f,.23f,.22f),Vector3.forward,room==8?.23f:.155f,quiet);
                    lit.Rock(c,Vector3.one*.065f,WorldMesh.Shade(accent,.51f),reed+.38f*room,4,7,.08f);
                }
                solid.Tube(new[] {
                    new Vector3(side*5.72f,3.54f,-2.02f), new Vector3(side*4.53f,4.69f,-.91f),
                    new Vector3(side*2.41f,6.83f,0), new Vector3(side*.77f,7.52f,.58f)
                },new[] {.47f,.33f,.21f,.1f},8,fern);
            }
        }

        private static void BuildLatticeBay(WorldMesh solid, WorldMesh lit, Color accent, int room)
        {
            Color ultraviolet = WorldMesh.Shade(RuinGalleryStoneFor(4,room),1.15f);
            Color frames = room == 6 ? new Color(.67f,.51f,.59f) : new Color(.64f,.44f,.61f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 5.02f;
                Vector3 low = new Vector3(x,.15f,-2.55f), middle = new Vector3(x-side*.29f,3.79f,.35f);
                Vector3 high = new Vector3(x-side*.69f,6.98f,2.74f);
                RuinBar(solid,low,middle,.42f,.70f,frames);
                RuinBar(solid,middle,high,.46f,.68f,frames);
                RuinBar(solid,new Vector3(x,5.35f,-2.75f),middle,.27f,.47f,ultraviolet);
                for (int brace = 0; brace < 4; brace++)
                {
                    float z = -3.12f+1.79f*brace;
                    float y = 4.08f+(brace % 2)*.51f;
                    Quaternion angle = GalleryTurn(0,side*16+(room%3)*6,45);
                    Vector3 heart = new Vector3(x+side*.23f,y,z);
                    solid.Box(heart,new Vector3(.99f,1.03f,.21f),angle,ultraviolet);
                    solid.Box(heart+new Vector3(-side*.13f,-.015f,-.13f),new Vector3(.67f,.74f,.12f),angle,frames);
                    lit.Rock(heart+new Vector3(-side*.19f,.04f,-.17f),new Vector3(.162f,.24f,.126f),
                        WorldMesh.Shade(accent,.58f),brace*.37f+room*.41f,4,6,.12f);
                }
                if (room == 6)
                    for (int shelf=0;shelf<5;shelf++)
                        solid.Box(new Vector3(x-side*.47f,.99f+.48f*shelf,.61f),new Vector3(.54f,.21f,4.31f),
                            Quaternion.identity,WorldMesh.Shade(frames,.71f+.04f*shelf));
                for (int n = 0; n < 5; n++)
                {
                    float z = n*.88f-1.72f;
                    RuinBar(lit,new Vector3(side*2.74f,.036f,z),new Vector3(side*3.62f,.036f,z+.29f),
                        .028f,.019f,WorldMesh.Shade(accent,.29f + .058f*n));
                }
            }
            for (int gem = -2; gem <= 2; gem++)
            {
                Vector3 stone = new Vector3(gem*.92f,6.78f-.24f*(gem%2),.50f+gem*.18f);
                solid.Rock(stone,new Vector3(.30f,.71f,.27f),ultraviolet,gem*.82f+room*.29f,4,5,.055f);
                lit.Rock(stone+new Vector3(-.034f,-.18f,-.06f),new Vector3(.12f,.37f,.13f),
                    WorldMesh.Shade(accent,.49f),gem*1.12f,4,5,.066f);
            }
        }

        private static void BuildMineralBay(WorldMesh solid, WorldMesh lit, Color accent, int room)
        {
            Color rock = Color.Lerp(RuinGalleryStoneFor(5,room),accent,.22f);
            float branch = (room % 5) * .41f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 5.35f;
                solid.Rock(new Vector3(x,.45f,.82f),new Vector3(1.07f,.81f,2.35f),rock,2+branch,5,7,.21f);
                for (int shelf = 0; shelf < 5; shelf++)
                {
                    float z = -2.46f + shelf*1.21f;
                    float rise = .63f + 1.03f * Mathf.Pow(.5f+.5f*Mathf.Sin(shelf*1.54f+branch),2);
                    Vector3 basePoint = new Vector3(x - side * .09f,.21f,z), tip = basePoint+new Vector3(-side*.43f,rise,.4f*Mathf.Sin(shelf));
                    solid.Rock(Vector3.Lerp(basePoint,tip,.45f),new Vector3(.49f,rise*.67f,.46f),
                        rock,shelf*.63f+branch,4,6,.28f);
                    lit.Rock(tip,new Vector3(.20f,.49f+.10f*(shelf%3),.18f),WorldMesh.Shade(accent,.41f+.067f*shelf),
                        shelf*.57f+branch,5,5,.13f);
                    RuinBar(lit,basePoint,tip,.032f,.039f,WorldMesh.Shade(accent,.54f));
                }
                solid.Rock(new Vector3(x-side*1.17f,6.67f,-.89f),new Vector3(.88f,1.35f,1.31f),
                    rock,1.12f+branch,5,7,.23f);
                lit.Rock(new Vector3(x-side*1.43f,5.94f,-.92f),new Vector3(.18f,.64f,.19f),
                    WorldMesh.Shade(accent,.50f),1.21f+branch,4,6,.19f);
            }
        }
    }
}
