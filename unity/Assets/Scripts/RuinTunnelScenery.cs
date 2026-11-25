using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        internal const float RuinY = -125;
        private const float HallStep = 6.5f;
        private const int HallSlots = 15;
        private Transform tunnelRoot, roomShell, wayfinding, roomEmblem;
        private Material tunnelStone, tunnelMetal;
        private RuinArtifacts archiveObjects;
        private readonly RuinHall[] halls = new RuinHall[6];
        private readonly HallPanel[] hallPanels = new HallPanel[HallSlots];
        private readonly List<RuinGateView> tunnelGates = new List<RuinGateView>();
        private readonly Dictionary<RuinDirection, RuinGateParts> gateParts = new Dictionary<RuinDirection, RuinGateParts>();
        private readonly ArchiveView[] exhibits = new ArchiveView[16];
        private Mesh[] exhibitModels, chamberEmblems;
        private MeshRenderer[] axonSignals;
        private int visibleRoom = -1, visibleStyle = -1;

        private readonly struct RuinHall
        {
            public readonly Mesh Walls, Structure, Inlay;
            public RuinHall(Mesh walls, Mesh structure, Mesh inlay)
            {
                Walls = walls;
                Structure = structure;
                Inlay = inlay;
            }
        }

        private sealed class HallPanel
        {
            public Transform Root;
            public MeshFilter Wall, Structure, Inlay;
        }

        private sealed class RuinGateView
        {
            public Transform Root, LeftDoor, RightDoor, PalmHint, VoiceHint;
            public MeshRenderer Rim, Signal;
            public RuinDirection Direction;
            public float OpenFraction;
        }

        private sealed class RuinGateParts
        {
            public Mesh Structure, Rim, Recess, Door, Signal, Palm, Voice;
        }

        private sealed class ArchiveView
        {
            public Transform Root, Object;
            public MeshFilter Model;
            public float Seed;
        }

        private static readonly Color[] RuinStoneColors = {
            new Color(.20f, .29f, .38f), new Color(.25f, .38f, .42f),
            new Color(.43f, .31f, .24f), new Color(.29f, .27f, .40f),
            new Color(.32f, .26f, .33f), new Color(.23f, .32f, .36f)
        };

        private static readonly Color[] RuinLightColors = {
            new Color(.21f, .83f, .96f), new Color(.32f, .88f, .84f),
            new Color(1, .64f, .31f), new Color(.74f, .61f, 1),
            new Color(.96f, .56f, .44f), new Color(.58f, 1, .72f)
        };

        internal static Color RuinLight(int style) => RuinLightColors[Mathf.Clamp(style, 0, RuinLightColors.Length - 1)];

        internal static Vector3 RuinPoint(float lane, float depth, float height) =>
            new Vector3(lane * 2.45f, RuinY + .54f + 3.30f * height, depth);

        public void DrawRuinTunnel(Journey journey, bool active, float time)
        {
            if (!active)
            {
                if (tunnelRoot != null) tunnelRoot.gameObject.SetActive(false);
                return;
            }

            if (tunnelRoot == null) BuildRuinTunnel();
            tunnelRoot.gameObject.SetActive(true);
            int style = Mathf.Clamp(journey.RuinRoomStyle, 0, 5);
            if (visibleRoom != journey.RuinRoomId || visibleStyle != style)
            {
                visibleRoom = journey.RuinRoomId;
                visibleStyle = style;
                PrepareRuinRoom(journey, style);
            }

            int current = Mathf.FloorToInt(journey.Distance / HallStep);
            for (int i = 0; i < hallPanels.Length; i++)
            {
                int segment = current - 2 + i;
                hallPanels[i].Root.localPosition = Vector3.forward * (segment * HallStep);
                hallPanels[i].Root.gameObject.SetActive(segment >= -2 && segment * HallStep < journey.RuinRoomLength + 22);
            }
            for (int i = 0; i < journey.RuinPortals.Count; i++)
            {
                RuinPortal portal = journey.RuinPortals[i];
                if (portal.Direction == RuinDirection.Exit || journey.RuinRetryState != RuinRetryPhase.Inactive)
                    tunnelGates[i].Root.gameObject.SetActive(false);
                else PoseRuinGate(tunnelGates[i], portal, journey, time);
            }

            float archiveTime = journey.RuinRetryState == RuinRetryPhase.Impact ? time - journey.Age : time;
            for (int i = 0; i < exhibits.Length; i++)
            {
                var exhibit = exhibits[i];
                if (!exhibit.Root.gameObject.activeSelf) continue;
                float phase = archiveTime * .48f + exhibit.Seed;
                exhibit.Object.localPosition = new Vector3(0, 1.05f + .17f * Mathf.Sin(phase), 0);
                exhibit.Object.localRotation = Quaternion.Euler(4 * Mathf.Cos(phase),
                    25 * Mathf.Sin(phase * .67f + exhibit.Seed), 2 * Mathf.Sin(phase * 1.25f));
            }
            for (int i = 0; i < axonSignals.Length; i++)
            {
                float phase = archiveTime * (.31f + (i % 3) * .12f) + i * .417f;
                float depth = journey.Distance + Mathf.Repeat(phase * 3.9f, 48) + 2;
                float side = (i & 1) == 0 ? -1 : 1;
                axonSignals[i].transform.localPosition = new Vector3(side * (4.08f + .22f * Mathf.Sin(depth * .30f)),
                    5.2f + .67f * Mathf.Sin(depth * .15f + i * .57f), depth);
                Light(axonSignals[i], RuinLight(style), .52f + .26f * Mathf.Sin(phase * 5));
            }
            DrawRuinArtifacts(journey, archiveTime);
            DrawRuinRetry(journey, time);
        }

        private void BuildRuinTunnel()
        {
            tunnelRoot = Group("Ancient archive below the forest", root.transform);
            tunnelRoot.localPosition = new Vector3(0, RuinY, 0);
            roomShell = Group("Deep galleries", tunnelRoot);
            tunnelStone = MaterialFor(stone.shader, "Archive glazed stone", 0);
            Grain(tunnelStone, "Stone", .27f, .60f);
            tunnelMetal = MaterialFor(stone.shader, "Archive bronze, ceramic and glass", 0);
            archiveObjects = new RuinArtifacts(tunnelRoot, Layer);
            for (int i = 0; i < halls.Length; i++) halls[i] = BuildRuinHall(i);
            for (int i = 0; i < hallPanels.Length; i++)
            {
                var node = Group("Vault module", roomShell);
                hallPanels[i] = new HallPanel {
                    Root = node,
                    Wall = RuinPiece("Walls", halls[0].Walls, tunnelStone, node).GetComponent<MeshFilter>(),
                    Structure = RuinPiece("Relief and struts", halls[0].Structure, tunnelMetal, node).GetComponent<MeshFilter>(),
                    Inlay = RuinPiece("Neural inlays", halls[0].Inlay, glow, node).GetComponent<MeshFilter>()
                };
                Light(hallPanels[i].Inlay.GetComponent<MeshRenderer>(), Color.white, .64f);
            }
            exhibitModels = BuildCivilizationModels();
            for (int i = 0; i < exhibits.Length; i++) exhibits[i] = BuildArchiveDisplay(i == 0);
            axonSignals = new MeshRenderer[16];
            for (int i = 0; i < axonSignals.Length; i++)
            {
                Transform mote = RuinPiece("Memory traveling across an axon", beaconMesh, glow, roomShell);
                mote.localScale = new Vector3(.028f, .028f, .29f);
                axonSignals[i] = mote.GetComponent<MeshRenderer>();
            }
            wayfinding = BuildFindYourself();
            chamberEmblems = BuildChamberEmblems();
            roomEmblem = RuinPiece("Hidden chamber vault sculpture", chamberEmblems[0], glow, roomShell);
        }

        private Transform RuinPiece(string name, Mesh mesh, Material material, Transform parent)
        {
            Transform value = Piece(name, mesh, material, parent, Vector3.zero);
            var renderer = value.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return value;
        }

        private void PrepareRuinRoom(Journey journey, int style)
        {
            RuinHall hall = halls[style];
            foreach (var panel in hallPanels)
            {
                panel.Wall.sharedMesh = hall.Walls;
                panel.Structure.sharedMesh = hall.Structure;
                panel.Inlay.sharedMesh = hall.Inlay;
            }

            while (tunnelGates.Count < journey.RuinPortals.Count) tunnelGates.Add(null);
            for (int i = 0; i < tunnelGates.Count; i++)
            {
                if (i >= journey.RuinPortals.Count)
                {
                    tunnelGates[i]?.Root.gameObject.SetActive(false);
                    continue;
                }
                RuinPortal portal = journey.RuinPortals[i];
                if (tunnelGates[i] == null || tunnelGates[i].Direction != portal.Direction)
                {
                    if (tunnelGates[i] != null) UnityEngine.Object.Destroy(tunnelGates[i].Root.gameObject);
                    tunnelGates[i] = BuildRuinGate(portal.Direction);
                }
                tunnelGates[i].Root.gameObject.SetActive(true);
                tunnelGates[i].OpenFraction = portal.Opened || portal.Gate == RuinGate.Open ? 1 : 0;
            }

            var scatter = new System.Random(unchecked(journey.RuinRoomId * 5923 + style * 7027 + 1505));
            for (int i = 0; i < exhibits.Length; i++)
            {
                ArchiveView frame = exhibits[i];
                float side = (i & 1) == 0 ? -1 : 1;
                float z = 5.5f + (i / 2) * 7.2f + (float)scatter.NextDouble() * 2.45f + (i & 1) * 2.3f;
                bool show = z < journey.RuinRoomLength + 3.5f && (style != 5 || i < 2);
                frame.Root.gameObject.SetActive(show);
                if (!show) continue;
                float sideOffset = style == 5 ? .55f : 0;
                frame.Root.localPosition = new Vector3(side * (3.92f + sideOffset + (float)scatter.NextDouble() * .25f),
                    .40f + (float)scatter.NextDouble() * 2.15f, z);
                // Keep the large historical models away from the nearer mouth of a matching lane.
                foreach (RuinPortal portal in journey.RuinPortals)
                    if (Mathf.Abs(portal.Depth - z) < 2.4f && Mathf.Sign(portal.Lane) == side)
                        frame.Root.localPosition += new Vector3(side * .48f, .56f, -1.3f);
                frame.Model.sharedMesh = exhibitModels[(i + style * 3 + scatter.Next(4)) % exhibitModels.Length];
                frame.Seed = (float)scatter.NextDouble() * 12;
            }

            wayfinding.gameObject.SetActive(journey.RuinRoomId == 0);
            bool hiddenChamber = style == 5;
            roomEmblem.gameObject.SetActive(hiddenChamber);
            if (hiddenChamber)
            {
                int identity = Mathf.Max(0, journey.RuinRoomId - 11) % chamberEmblems.Length;
                roomEmblem.GetComponent<MeshFilter>().sharedMesh = chamberEmblems[identity];
                roomEmblem.localPosition = new Vector3(0, 6.68f, journey.RuinRoomLength * .46f);
                roomEmblem.localRotation = Quaternion.Euler(0, 0, identity * 8 - 15);
                Color jewel = identity == 0 ? Warm : identity == 1 ? Pale : identity == 2 ? IrisBlue : identity == 3 ? Red : Jade;
                Light(roomEmblem.GetComponent<MeshRenderer>(), jewel, .58f);
            }
        }

        private Mesh[] BuildChamberEmblems()
        {
            var variants = new Mesh[5];
            for (int design = 0; design < variants.Length; design++)
            {
                var mesh = Geometry();
                int points = design == 0 ? 6 : design == 1 ? 16 : design == 2 ? 5 : design == 3 ? 8 : 12;
                for (int ring = 0; ring <= 3; ring++)
                {
                    float radius = (1.78f - ring * .40f) * (design == 1 ? .89f : 1.15f);
                    for (int p = 0; p < points; p++)
                    {
                        float a = (p + ring * .23f) * 2 * Mathf.PI / points;
                        float b = a + 2 * Mathf.PI / points;
                        float y = -ring * .12f;
                        float heightA = design == 2 ? .39f * Mathf.Cos(a * 2) : Mathf.Sin(a * 2 + design) * .25f;
                        float heightB = design == 2 ? .39f * Mathf.Cos(b * 2) : Mathf.Sin(b * 2 + design) * .25f;
                        Vector3 start = new Vector3(radius * Mathf.Cos(a), y + heightA, radius * Mathf.Sin(a));
                        Vector3 end = new Vector3(radius * Mathf.Cos(b), y + heightB, radius * Mathf.Sin(b));
                        RuinBar(mesh, start, end, .019f + ring * .013f, .058f, WorldMesh.Shade(Color.white, .34f + ring * .10f));
                        if (p % 2 == ring % 2 && ring == 0)
                            RuinBar(mesh, start, new Vector3(start.x * .31f, -1.12f, start.z * .31f), .019f, .019f, Color.white);
                    }
                }
                mesh.Rock(new Vector3(0, -.34f, 0), new Vector3(.37f,.52f,.37f), new Color(.86f,.83f,.76f), design*.39f, 5, 6, .18f);
                variants[design] = mesh.Finish("Hidden chamber sculpture " + design);
            }
            return variants;
        }

        private RuinHall BuildRuinHall(int style)
        {
            var walls = Geometry();
            var structure = Geometry();
            var inlay = Geometry();
            Color shell = RuinStoneColors[style];
            Color accent = RuinLight(style);
            bool hidden=style==5;
            float width = hidden ? 7.7f : style == 3 ? 4.95f : 5.2f;
            float height = hidden ? 11.8f : 7.45f + .19f * (style % 3);
            int facets = style == 0 ? 18 : style == 1 ? 12 : style == 2 ? 10 : style == 3 ? 24 : style == 4 ? 8 : 12;
            for (int face = 0; face < facets; face++)
            {
                float a = 2 * Mathf.PI * face / facets;
                float b = 2 * Mathf.PI * (face + 1) / facets;
                Vector3 p = VaultPoint(a, width, height, style);
                Vector3 q = VaultPoint(b, width, height, style);
                Color stoneColor = WorldMesh.Shade(shell, .63f + .35f * Mathf.Abs(Mathf.Cos(a + style * .61f)));
                float z = HallStep * .5f;
                walls.Quad(p - Vector3.forward * z, q - Vector3.forward * z, q + Vector3.forward * z,
                    p + Vector3.forward * z, stoneColor);

                float pInset = style == 3 ? .935f : .966f;
                Vector3 toward = new Vector3(0, height * .5f, 0);
                Vector3 near = Vector3.Lerp(toward, p, pInset);
                Vector3 far = Vector3.Lerp(toward, q, pInset);
                float ribZ = hidden ? -.16f : style == 2 ? .78f + .22f * Mathf.Sin(a) : 1.95f;
                RuinBar(structure, near + Vector3.forward * ribZ, far + Vector3.forward * ribZ,
                    hidden ? .53f : style == 4 ? .35f : .25f, hidden ? .83f : style == 1 ? .62f : .32f,
                    WorldMesh.Shade(shell, 1.67f));
                if (style != 4 || (face % 2) == 0)
                    RuinBar(inlay, Vector3.Lerp(toward, near, .978f) + Vector3.forward * (ribZ - .17f),
                        Vector3.Lerp(toward, far, .978f) + Vector3.forward * (ribZ - .17f), .019f, .027f,
                        WorldMesh.Shade(accent, .39f + .13f * Mathf.Sin(a + 2)));
            }

            if (hidden)
            {
                BuildHiddenVault(walls, structure, inlay, shell, accent);
                return new RuinHall(walls.Finish("Hidden vault"), structure.Finish("Rooted mineral colonnade"),
                    inlay.Finish("Glow from the buried foundation"));
            }

            for (int lane = -1; lane <= 1; lane++)
            {
                float laneX = lane * 2.45f;
                walls.Box(new Vector3(laneX, -.17f, 0), new Vector3(2.45f, .36f, HallStep + .012f),
                    Quaternion.identity, WorldMesh.Shade(shell, 1.04f + .12f * (1 - Mathf.Abs(lane))));
                structure.Box(new Vector3(laneX, .018f, -.55f), new Vector3(1.81f, .028f, HallStep - .77f),
                    Quaternion.identity, WorldMesh.Shade(shell, 1.38f));
                for (int d = 0; d < 3; d++)
                {
                    float z = -2.57f + d * 1.38f;
                    inlay.Box(new Vector3(laneX + .83f, .036f, z), new Vector3(.045f, .018f, .35f),
                        Quaternion.identity, WorldMesh.Shade(accent, .43f));
                    inlay.Box(new Vector3(laneX - .83f, .036f, z), new Vector3(.045f, .018f, .35f),
                        Quaternion.identity, WorldMesh.Shade(accent, .43f));
                }
            }

            for (int side = -1; side <= 1; side += 2)
            {
                for (int beam = 0; beam < 3; beam++)
                {
                    float x = side * (width - .58f - beam * .09f);
                    float y = height * (.58f + beam * .072f);
                    var path = new[] { new Vector3(x, y, -3.27f), new Vector3(x - side * .29f, y + .23f, -.56f),
                        new Vector3(x - side * .14f, y - .13f, 1.48f), new Vector3(x, y, 3.27f) };
                    structure.Tube(path, new[] { .126f, .082f, .101f, .126f }, 6, WorldMesh.Shade(shell, 1.55f));
                    for (int p = 0; p < path.Length; p++) path[p] -= Vector3.right * side * .11f;
                    inlay.Tube(path, new[] { .022f, .017f, .019f, .022f }, 5, WorldMesh.Shade(accent, beam == 1 ? .70f : .21f));
                }

                float ledge = side * (width - .41f);
                int panels = style == 2 ? 4 : 3;
                for (int i = 0; i < panels; i++)
                {
                    float x = ledge - side * (.36f + i * .05f);
                    float y = .74f + i * .77f;
                    float z = -1.36f + .32f * Mathf.Sin(i * .91f + style);
                    structure.Box(new Vector3(x, y, z), new Vector3(.33f, .42f, 4.55f - i * .41f),
                        Quaternion.Euler(side * (style == 1 ? 8 : 0), 0, 0), WorldMesh.Shade(shell, 1.11f + i * .12f));
                    if ((style + i) % 3 != 0)
                        inlay.Box(new Vector3(x - side * .19f, y + .16f, z), new Vector3(.031f, .023f, 2.66f),
                            Quaternion.identity, WorldMesh.Shade(accent, .27f));
                }
            }
            return new RuinHall(walls.Finish("Archive vault " + style), structure.Finish("Archive relief " + style),
                inlay.Finish("Neural circuitry " + style));
        }

        private static void BuildHiddenVault(WorldMesh walls, WorldMesh structure, WorldMesh inlay, Color shell, Color accent)
        {
            Color mineral = new Color(.31f,.41f,.46f);
            walls.Box(new Vector3(0,-.19f,0),new Vector3(14.65f,.38f,HallStep+.012f),Quaternion.identity,WorldMesh.Shade(shell,.77f));
            for (int side=-1;side<=1;side+=2)
            {
                float x=side*4.65f;
                // Set the supports well outside the three traversable lanes; shadows also frame the lower recesses.
                structure.Rock(new Vector3(x,.4f,-1.36f), new Vector3(2.25f,1.0f,2.32f),shell,.59f,4,7,.27f);
                structure.Rock(new Vector3(x,1.83f,-1.44f), new Vector3(1.24f,3.75f,1.31f),mineral,1.50f,5,7,.19f);
                structure.Rock(new Vector3(x+side*.39f,4.58f,-1.28f), new Vector3(1.08f,4.12f,1.06f),
                    WorldMesh.Shade(mineral,.76f),1.33f,5,6,.24f);
                structure.Rock(new Vector3(x+side*.74f,7.39f,-.87f), new Vector3(1.58f,3.28f,1.31f),
                    WorldMesh.Shade(mineral,.83f),.72f,5,6,.26f);
                for (int vein=0;vein<3;vein++)
                {
                    float z=-1.59f+.20f*vein;
                    inlay.Tube(new[]{new Vector3(x-side*.46f, .54f, z),new Vector3(x-side*.58f, 2.22f,z-.07f),
                        new Vector3(x-side*.34f, 3.82f,z+.18f),new Vector3(x+side*.04f,5.5f,z+.25f),
                        new Vector3(x+side*.41f,7.7f,z+.53f)},new[]{.044f,.029f,.022f,.026f,.0f},5,
                        WorldMesh.Shade(accent,.38f+.13f*vein));
                }
                for (int i=0;i<4;i++)
                {
                    float z=-2.0f+i*1.37f;
                    Vector3 crystal=new Vector3(side*(5.40f+.30f*Mathf.Sin(i*2)),.31f+i*.08f,z);
                    structure.Rock(crystal,new Vector3(.73f,.74f+.26f*i,.59f),mineral,i*.62f,4,5,.39f);
                    inlay.Rock(crystal+new Vector3(-side*.07f,.34f+i*.13f,.03f),new Vector3(.185f,.65f+.27f*i,.17f),
                        WorldMesh.Shade(accent,.35f+i*.11f),i*.67f,5,5,.21f);
                }
                // Faint facets continue along the floor while leaving collectible light visually distinct.
                Vector3 a=new Vector3(side*3.62f,.026f,-HallStep*.5f);
                Vector3 b=new Vector3(side*2.83f,.029f,.13f);
                Vector3 c=new Vector3(side*3.62f,.026f,HallStep*.5f);
                RuinBar(inlay,a,b,.038f,.021f,WorldMesh.Shade(accent,.26f));
                RuinBar(inlay,b,c,.038f,.021f,WorldMesh.Shade(accent,.26f));
                RuinBar(structure,new Vector3(side*5.15f,6.22f,-1.37f),new Vector3(side*2.74f,8.08f,.26f),
                    .40f,.87f,WorldMesh.Shade(shell,1.24f));
            }
            for(int i=0;i<3;i++)
            {
                float z=-2.12f+i*2.10f;
                walls.Box(new Vector3(0,.035f,z),new Vector3(3.65f,.07f,2.72f),Quaternion.Euler(0,45,0),
                    WorldMesh.Shade(shell,1.12f+.08f*i));
                for(int side=-1;side<=1;side+=2)
                    RuinBar(inlay,new Vector3(side*.88f,.078f,z+.59f),new Vector3(0,.078f,z+.95f),
                        .027f,.019f,WorldMesh.Shade(accent,.28f));
            }
        }

        private static Vector3 VaultPoint(float angle, float width, float height, int style)
        {
            float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
            float taper = style == 5 ? .95f : style == 4 ? .36f : style == 3 ? .92f : .63f;
            float x = Mathf.Sign(cosine) * Mathf.Pow(Mathf.Abs(cosine), taper) * width;
            float y = (Mathf.Sign(sine) * Mathf.Pow(Mathf.Abs(sine), style == 5 ? 1.60f : .79f) + 1) * height * .5f - .57f;
            return new Vector3(x, y, 0);
        }

        private static void RuinBar(WorldMesh mesh, Vector3 a, Vector3 b, float width, float depth, Color color)
        {
            Vector3 axis = b - a;
            if (axis.sqrMagnitude < .000001f) return;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            mesh.Box((a + b) * .5f, new Vector3(width, axis.magnitude + .007f, depth), rotation, color);
        }

        private RuinGateView BuildRuinGate(RuinDirection direction)
        {
            if (!gateParts.TryGetValue(direction, out RuinGateParts parts))
            {
                parts = MakeRuinGateParts(direction);
                gateParts[direction] = parts;
            }
            Transform rootGate = Group("Approach to the " + direction + " gallery", roomShell);
            RuinPiece("Deep mouth of the gallery", parts.Recess, tunnelMetal, rootGate);
            RuinPiece("Gallery entrance stonework", parts.Structure, tunnelStone, rootGate);
            return new RuinGateView {
                Root = rootGate, Direction = direction,
                Rim = RuinPiece("Layer mark", parts.Rim, glow, rootGate).GetComponent<MeshRenderer>(),
                Signal = RuinPiece("Threshold signal", parts.Signal, glow, rootGate).GetComponent<MeshRenderer>(),
                PalmHint = RuinPiece("Palm seal", parts.Palm, glow, rootGate),
                VoiceHint = RuinPiece("Voice seal", parts.Voice, glow, rootGate),
                LeftDoor = RuinPiece("Left folding shutter", parts.Door, tunnelMetal, rootGate),
                RightDoor = RuinPiece("Right folding shutter", parts.Door, tunnelMetal, rootGate)
            };
        }

        private RuinGateParts MakeRuinGateParts(RuinDirection direction)
        {
            var stonework = Geometry();
            var trim = Geometry();
            var interior = Geometry();
            var shutter = Geometry();
            var indicator = Geometry();
            var palm = Geometry();
            var voice = Geometry();
            Color dark = new Color(.20f, .26f, .34f);
            Color blueBlack = new Color(.027f, .050f, .074f);
            Color highlight = new Color(.66f, .76f, .77f);
            bool left = direction == RuinDirection.Left, right = direction == RuinDirection.Right;
            bool upper = direction == RuinDirection.Upper, lower = direction == RuinDirection.Lower;
            bool exit = direction == RuinDirection.Exit;
            int faces = left ? 6 : right ? 8 : upper ? 20 : lower ? 4 : exit ? 12 : 10;
            float angleOffset = lower || right ? Mathf.PI / faces : 0;
            Vector2 radius = new Vector2(exit ? 1.66f : lower ? 1.39f : upper ? 1.20f : 1.14f,
                exit ? 1.86f : lower ? .73f : upper ? 1.43f : 1.23f);
            for (int i = 0; i < faces; i++)
            {
                float a = 2 * Mathf.PI * i / faces + angleOffset;
                float b = 2 * Mathf.PI * (i + 1) / faces + angleOffset;
                Vector3 p = GatePoint(a, radius, 0), q = GatePoint(b, radius, 0);
                float rim = left ? 1.16f : right ? (i % 2 == 0 ? 1.30f : 1.18f) : lower ? 1.23f : 1.18f;
                Vector3 pWide = p * rim, qWide = q * rim;
                stonework.Quad(p, pWide - Vector3.forward * .13f, qWide - Vector3.forward * .13f, q,
                    WorldMesh.Shade(dark, 1.73f + .20f * Mathf.Sin(i * 1.8f)));
                stonework.Quad(pWide - Vector3.forward * .13f, pWide + Vector3.forward * .83f,
                    qWide + Vector3.forward * .83f, qWide - Vector3.forward * .13f, dark);
                Vector3 pDeep = p * .69f + Vector3.forward * 3.15f;
                Vector3 qDeep = q * .69f + Vector3.forward * 3.15f;
                interior.Quad(p, q, qDeep, pDeep, WorldMesh.Shade(blueBlack, 2.6f - .38f * Mathf.Sin(a)));
                interior.Triangle(pDeep, qDeep, Vector3.forward * 3.18f, blueBlack);
                if (!left || i % 3 != 0)
                    RuinBar(trim, p * 1.082f - Vector3.forward * .108f, q * 1.082f - Vector3.forward * .108f,
                        upper ? .024f : .047f, .047f, Color.white);
                // Repeated ribs fade into the depth of the aperture.
                for (int d = 0; d < 3; d++)
                {
                    float t = .23f + d * .22f;
                    RuinBar(trim, Vector3.Lerp(p, pDeep, t), Vector3.Lerp(q, qDeep, t), .017f + .006f * (2-d), .025f,
                        WorldMesh.Shade(Color.white, .57f - .14f * d));
                }
                if (right && (i & 1) == 0)
                {
                    Vector3 knuckle = (pWide + qWide) * .56f;
                    stonework.Box(knuckle - Vector3.forward * .12f, new Vector3(.29f, .29f, .27f),
                        Quaternion.Euler(0, 0, 22.5f + i * 45), WorldMesh.Shade(dark, 2.52f));
                }
                if (upper || exit)
                {
                    Vector3 starward = p * (i % 2 == 0 ? 1.26f : 1.42f) - Vector3.forward * .09f;
                    RuinBar(trim, p * 1.18f - Vector3.forward * .11f, starward, .027f, .026f, WorldMesh.Shade(Color.white, .65f));
                }
            }
            if (left || right || upper || lower)
                AttachPassage(stonework, trim, faces, angleOffset, radius, direction);

            float halfDoor = radius.x * .48f;
            shutter.Box(new Vector3(0, 0, 0), new Vector3(halfDoor * 2, radius.y * 1.68f, .17f), Quaternion.identity,
                WorldMesh.Shade(dark, 1.20f));
            for (int i = -2; i <= 2; i++)
            {
                Vector3 groove = new Vector3(0, i * radius.y * .27f, -.094f);
                shutter.Box(groove, new Vector3(halfDoor * (1.72f - .11f * Mathf.Abs(i)), .036f, .045f),
                    Quaternion.Euler(0, 0, 6 + i * 3), WorldMesh.Shade(dark, 2.25f));
            }
            for (int i = 0; i < 3; i++)
            {
                float y = (i - 1) * .15f;
                indicator.Rock(new Vector3(-radius.x * 1.19f, y, -.21f), new Vector3(.057f, .05f, .05f),
                    Color.white, i * .37f, 3, 5, .035f);
                indicator.Rock(new Vector3(radius.x * 1.19f, y, -.21f), new Vector3(.057f, .05f, .05f),
                    Color.white, i * .41f, 3, 5, .035f);
            }
            float symbolY = radius.y * .39f;
            palm.Box(new Vector3(0, symbolY, -.29f), new Vector3(.19f, .17f, .028f), Quaternion.Euler(0,0,-8), highlight);
            for (int i = 0; i < 4; i++)
            {
                float h = .10f + .059f * Mathf.Sin(i * .92f + .36f);
                palm.Box(new Vector3(-.064f + i * .044f, symbolY + .073f + h * .5f, -.29f),
                    new Vector3(.027f, h, .028f), Quaternion.Euler(0,0,13 - i * 8), Color.white);
            }
            RuinBar(palm, new Vector3(-.08f, symbolY-.02f, -.29f), new Vector3(-.16f, symbolY+.055f, -.29f),
                .031f, .028f, highlight);
            for (int i = -3; i <= 3; i++)
            {
                float h = .31f * (1 - Mathf.Abs(i) * .21f);
                voice.Box(new Vector3(i * .071f, symbolY, -.30f), new Vector3(.032f, h, .021f), Quaternion.identity, Color.white);
            }

            return new RuinGateParts { Structure = stonework.Finish("Entrance relief"), Rim = trim.Finish("Entrance inlay"),
                Recess = interior.Finish("Passage inside entrance"), Door = shutter.Finish("Machined shutters"),
                Signal = indicator.Finish("Passage markers"), Palm = palm.Finish("Palm illustration"), Voice = voice.Finish("Voice illustration") };
        }

        private static Vector3 GatePoint(float angle, Vector2 radius, float z) =>
            new Vector3(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y, z);

        private static void AttachPassage(WorldMesh masonry, WorldMesh light, int facets, float rotation,
            Vector2 radius, RuinDirection direction)
        {
            bool upper=direction==RuinDirection.Upper, lower=direction==RuinDirection.Lower;
            float side=direction==RuinDirection.Left?-1:direction==RuinDirection.Right?1:0;
            Color stoneColor=new Color(.22f,.35f,.40f);
            for(int face=0;face<facets;face++)
            {
                float a=2*Mathf.PI*face/facets+rotation;
                float b=2*Mathf.PI*(face+1)/facets+rotation;
                float outerScale=1.18f+.055f*(face%2);
                Vector3 p=GatePoint(a,radius,.41f)*outerScale;
                Vector3 q=GatePoint(b,radius,.41f)*outerScale;
                for(int ring=1;ring<=4;ring++)
                {
                    float t=ring*.25f;
                    float extension=3.2f*t;
                    // Bend the sleeve toward the nearest exterior wall. The bottom duct slopes down through the deck.
                    float shift=side*2.14f*t*t;
                    float rising=upper ? .65f*t+1.51f*t*t : lower ? -1.18f*t*t : 0;
                    var center=new Vector3(shift,rising,.48f+extension);
                    float scale=outerScale-.10f*t;
                    Vector3 m=GatePoint(a,radius,0)*scale+center;
                    Vector3 n=GatePoint(b,radius,0)*scale+center;
                    masonry.Quad(p,q,n,m,WorldMesh.Shade(stoneColor, .81f+.11f*(ring%2)+.18f*Mathf.Sin(a)));
                    Color metal=WorldMesh.Shade(stoneColor,1.21f+.13f*(ring%3));
                    if(ring%2==1)
                        RuinBar(masonry, m,n, upper ? .11f : .15f,.17f,metal);
                    if(face%2==0)
                    {
                        RuinBar(masonry, p,q,.127f,.18f,metal);
                        RuinBar(light,p,p+(m-p)*.74f,.025f,.035f,WorldMesh.Shade(Color.white,.32f-.039f*ring));
                    }
                    p=m;
                    q=n;
                }
            }
        }

        private void PoseRuinGate(RuinGateView view, RuinPortal gate, Journey journey, float time)
        {
            Vector3 local = RuinPoint(gate.Lane, gate.Depth, gate.Altitude) - Vector3.up * RuinY;
            view.Root.localPosition = local;
            bool top = gate.Direction == RuinDirection.Upper, bottom = gate.Direction == RuinDirection.Lower;
            bool exit = gate.Direction == RuinDirection.Exit;
            float variant = ((gate.FrameStyle % 3) - 1) * 2.5f;
            view.Root.localRotation = Quaternion.Euler(top ? -17 : bottom ? 15 : 0,
                gate.Direction == RuinDirection.Left ? -16 : gate.Direction == RuinDirection.Right ? 22 : 0, variant);
            float range = gate.Depth - journey.Distance;
            bool reveal = !gate.Hidden || range < 24;
            bool visible = range > -6 && range < 68;
            view.Root.gameObject.SetActive(visible);
            if (!visible) return;
            bool shutter = gate.Gate != RuinGate.Open;
            if (gate.Opened && gate.OpenAge >= 0)
                view.OpenFraction = Mathf.SmoothStep(0, 1, Mathf.Clamp01(gate.OpenAge / .68f));
            float opened = gate.Opened ? view.OpenFraction : 0;
            view.LeftDoor.gameObject.SetActive(shutter && opened < .998f);
            view.RightDoor.gameObject.SetActive(shutter && opened < .998f);
            view.PalmHint.gameObject.SetActive(gate.Gate == RuinGate.Palm && !gate.Opened);
            view.VoiceHint.gameObject.SetActive(gate.Gate == RuinGate.Voice && !gate.Opened);
            view.LeftDoor.localPosition = new Vector3(-.48f - opened * 1.18f, opened * .115f, .145f);
            view.RightDoor.localPosition = new Vector3(.48f + opened * 1.18f, -opened * .08f, .145f);
            view.LeftDoor.localRotation = Quaternion.Euler(0, opened * -70, opened * -6);
            view.RightDoor.localRotation = Quaternion.Euler(0, opened * 70, opened * 6);
            Color hue = exit ? new Color(1, .86f, .55f) : top ? new Color(.61f, .62f, 1) : bottom ? new Color(.35f, .83f, .86f) :
                gate.Direction == RuinDirection.Left ? new Color(.41f,.93f,.75f) : new Color(.97f,.62f,.34f);
            hue = Color.Lerp(hue, RuinLight(journey.RuinRoomStyle), .15f + (gate.FrameStyle % 5) * .04f);
            float pulse = .13f * Mathf.Sin(time * (exit ? 4.7f : 2.6f) + gate.Id * 1.71f);
            float light = reveal ? (shutter && !gate.Opened ? .79f : .96f) : .19f;
            Light(view.Rim, hue, light + pulse);
            Light(view.Signal, exit ? hue : shutter && !gate.Opened ? new Color(1,.76f,.36f) : hue,
                reveal ? 1.14f + pulse : .38f);
            if (view.PalmHint.gameObject.activeSelf) Light(view.PalmHint.GetComponent<MeshRenderer>(), hue, .94f);
            if (view.VoiceHint.gameObject.activeSelf) Light(view.VoiceHint.GetComponent<MeshRenderer>(), hue, .94f);
        }

        private ArchiveView BuildArchiveDisplay(bool first)
        {
            var gallery = Group(first ? "First memory in the living archive" : "Floating memory niche", roomShell);
            var baseMesh = Geometry();
            Color dark = new Color(.21f, .31f, .39f);
            baseMesh.Rock(Vector3.zero, new Vector3(1.16f,.13f,.89f), dark, .31f, 3, 10, .08f);
            baseMesh.Rock(new Vector3(0,.11f,0), new Vector3(.78f,.11f,.59f), WorldMesh.Shade(dark,1.62f), .62f, 3, 10, .07f);
            RuinPiece("Archive niche base", baseMesh.Finish("Engraved bronze plinth"), tunnelMetal, gallery);
            var inlay = Geometry();
            ArchiveRing(inlay, new Vector3(0,.18f,0), .60f,.044f,new Color(.26f,.56f,.67f), false, 24);
            ArchiveRing(inlay, new Vector3(0,.22f,0), .32f,.019f,new Color(.41f,.76f,.74f), false, 16);
            var lamp = RuinPiece("Archive levitation inlay",inlay.Finish("Archive levitation inlay"),glow,gallery).GetComponent<MeshRenderer>();
            Light(lamp,Color.white,.48f);
            Transform sculpt = RuinPiece("Civilization memory", exhibitModels[0], glow, gallery);
            Light(sculpt.GetComponent<MeshRenderer>(),Color.white,.38f);
            return new ArchiveView { Root = gallery, Object = sculpt, Model = sculpt.GetComponent<MeshFilter>() };
        }

        private Mesh[] BuildCivilizationModels()
        {
            Color marble = new Color(.62f,.76f,.79f), bronze = new Color(.62f,.43f,.25f), jade = new Color(.37f,.64f,.59f);
            var memories = new Mesh[12];
            var mesh = Geometry();
            // Giza pyramid and its smaller companion profile.
            ArchivePyramid(mesh, new Vector3(-.16f,-.36f,0), .92f, 1.09f, new Color(.72f,.57f,.38f));
            ArchivePyramid(mesh, new Vector3(.52f,-.36f,.34f), .47f, .61f, new Color(.62f,.50f,.39f));
            memories[0] = mesh.Finish("Memory of Giza");

            mesh = Geometry();
            // Cuneiform tablet with cut wedge impressions.
            mesh.Box(Vector3.zero, new Vector3(1.03f,1.18f,.19f), Quaternion.Euler(4,0,-8), bronze);
            for (int row = 0; row < 6; row++)
            for (int col = 0; col < 6; col++)
            {
                float x = (col - 2.5f) * .136f + .023f * Mathf.Sin(row*3+col);
                float y = (row - 2.5f) * .15f;
                mesh.Triangle(new Vector3(x-.025f,y-.047f,-.115f),new Vector3(x+.025f,y+.055f,-.115f),
                    new Vector3(x+.051f,y+.005f,-.115f), WorldMesh.Shade(bronze,.52f));
            }
            memories[1] = mesh.Finish("Cuneiform tablet");

            mesh = Geometry();
            mesh.Box(new Vector3(0,-.39f,0),new Vector3(1.34f,.17f,.86f),Quaternion.identity,WorldMesh.Shade(marble,.68f));
            for (int c = 0; c < 5; c++)
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 foot = new Vector3((c-2)*.267f,-.29f,s*.31f);
                mesh.Tube(new[]{foot,foot+Vector3.up*.62f},new[]{.061f,.052f},8,marble);
            }
            mesh.Box(new Vector3(0,.37f,0),new Vector3(1.32f,.14f,.80f),Quaternion.identity,marble);
            mesh.Triangle(new Vector3(-.67f,.45f,-.435f),new Vector3(.67f,.45f,-.435f),new Vector3(0,.75f,-.435f),marble);
            mesh.Quad(new Vector3(-.67f,.45f,-.42f),new Vector3(0,.75f,-.42f),new Vector3(0,.75f,.42f),
                new Vector3(-.67f,.45f,.42f),WorldMesh.Shade(marble,.75f));
            mesh.Quad(new Vector3(.67f,.45f,-.42f),new Vector3(.67f,.45f,.42f),new Vector3(0,.75f,.42f),
                new Vector3(0,.75f,-.42f),WorldMesh.Shade(marble,.82f));
            memories[2] = mesh.Finish("Memory of the Parthenon");

            mesh = Geometry();
            // Great Wall: a winding crenellated ridge joining two watchtowers.
            for (int i = -5; i <= 5; i++)
            {
                float x = i * .147f;
                Vector3 pos = new Vector3(x,-.15f + .11f*Mathf.Sin(i*.65f),.23f*Mathf.Sin(i*.39f));
                mesh.Box(pos,new Vector3(.19f,.33f,.28f),Quaternion.Euler(0,-29*Mathf.Cos(i*.39f),0), jade);
                mesh.Box(pos + Vector3.up*.20f,new Vector3(.083f,.10f,.31f),Quaternion.identity, jade);
                if (i == -4 || i == 4)
                {
                    mesh.Box(pos+Vector3.up*.23f,new Vector3(.25f,.47f,.36f),Quaternion.identity,WorldMesh.Shade(jade,1.23f));
                    ArchivePyramid(mesh,pos+new Vector3(0,.47f,0),.34f,.18f,WorldMesh.Shade(jade,.78f));
                }
            }
            memories[3] = mesh.Finish("Memory of the Great Wall");

            mesh = Geometry();
            // Mesoamerican stepped temple and the axial staircase.
            for (int i = 0; i < 7; i++)
            {
                float width=1.2f-i*.149f;
                mesh.Box(new Vector3(0,-.36f+i*.112f,0),new Vector3(width,.124f,width),Quaternion.identity,WorldMesh.Shade(jade,1.13f+i*.025f));
                mesh.Box(new Vector3(0,-.35f+i*.10f,-width*.52f),new Vector3(.19f,.106f,.15f),Quaternion.identity,jade);
            }
            mesh.Box(new Vector3(0,.49f,0),new Vector3(.30f,.20f,.30f),Quaternion.identity,marble);
            memories[4] = mesh.Finish("Memory of Chichen Itza");

            mesh=Geometry();
            for (int tier=0;tier<3;tier++)
            for (int span=0;span<10;span++)
            {
                float a=span*Mathf.PI*.2f;
                Vector3 v=new Vector3(Mathf.Sin(a)*.63f,-.36f+tier*.248f,Mathf.Cos(a)*.40f);
                float yaw=a*Mathf.Rad2Deg;
                mesh.Box(v,new Vector3(.27f,.075f,.125f),Quaternion.Euler(0,yaw,0),bronze);
                mesh.Box(v+Vector3.up*.100f,new Vector3(.082f,.209f,.099f),Quaternion.Euler(0,yaw,0),WorldMesh.Shade(bronze,1.36f));
                mesh.Box(v+Vector3.up*.214f,new Vector3(.36f,.072f,.102f),Quaternion.Euler(0,yaw,0),WorldMesh.Shade(bronze,1.24f));
            }
            memories[5] = mesh.Finish("Memory of the Colosseum");

            mesh = Geometry();
            ArchiveRing(mesh,Vector3.zero,.59f,.051f,bronze,true,28);
            ArchiveRing(mesh,Vector3.zero,.47f,.034f,WorldMesh.Shade(bronze,1.36f),false,24);
            for(int i=0;i<22;i++)
            {
                float angle=2*Mathf.PI*i/22;
                Vector3 a=new Vector3(Mathf.Sin(angle)*.555f,Mathf.Cos(angle)*.555f,-.031f);
                RuinBar(mesh,a,a*.89f,.026f,.024f,marble);
            }
            RuinBar(mesh,new Vector3(-.29f,-.33f,-.10f),new Vector3(.33f,.39f,-.10f),.049f,.035f,marble);
            memories[6] = mesh.Finish("Mariner's astrolabe");

            mesh=Geometry();
            mesh.Box(new Vector3(0,-.31f,0),new Vector3(1.28f,.14f,.77f),Quaternion.identity,bronze);
            for(int side=-1;side<=1;side+=2)
                mesh.Box(new Vector3(side*.43f,.16f,0),new Vector3(.10f,.98f,.53f),Quaternion.identity,bronze);
            mesh.Box(new Vector3(0,.65f,0),new Vector3(1.07f,.15f,.54f),Quaternion.identity,bronze);
            mesh.Tube(new[]{new Vector3(0,.64f,0),new Vector3(0,.02f,0)},new[]{.083f,.083f},8,marble);
            mesh.Box(new Vector3(0,.006f,0),new Vector3(.83f,.061f,.54f),Quaternion.identity,marble);
            RuinBar(mesh,new Vector3(-.38f,.46f,-.17f),new Vector3(.39f,.46f,.11f),.061f,.064f,marble);
            memories[7] = mesh.Finish("Moveable type printing press");

            mesh=Geometry();
            mesh.Rock(new Vector3(0,-.06f,0),new Vector3(.84f,.69f,.76f),bronze,.52f,6,14,.025f);
            mesh.Rock(new Vector3(0,.28f,0),new Vector3(.42f,.16f,.42f),WorldMesh.Shade(bronze,1.3f),1.2f,5,12,.028f);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.25f;
                Vector3 dir=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
                RuinBar(mesh,dir*.33f,dir*.60f+Vector3.up*.09f,.078f,.084f,jade);
                mesh.Rock(dir*.62f+Vector3.up*.09f,Vector3.one*.116f,marble,i,3,6,.045f);
            }
            memories[8] = mesh.Finish("Zhang Heng's seismoscope");

            mesh=Geometry();
            mesh.Box(new Vector3(0,-.22f,.09f),new Vector3(1.35f,.16f,.57f),Quaternion.identity,jade);
            mesh.Box(new Vector3(-.41f,.16f,.09f),new Vector3(.43f,.64f,.54f),Quaternion.identity,WorldMesh.Shade(jade,1.24f));
            mesh.Tube(new[]{new Vector3(-.22f,.05f,.09f),new Vector3(.61f,.05f,.09f)},new[]{.29f,.29f},10,bronze);
            mesh.Box(new Vector3(.48f,.36f,.09f),new Vector3(.13f,.29f,.13f),Quaternion.identity,bronze);
            for (int side = -1; side <= 1; side+=2)
            for (int i=0;i<3;i++)
                ArchiveRing(mesh,new Vector3(-.45f+i*.43f,-.28f,.09f+side*.31f),.15f,.06f,marble,true,12);
            memories[9] = mesh.Finish("Steam locomotive");

            mesh = Geometry();
            mesh.Tube(new[]{new Vector3(0,-.46f,0),new Vector3(0,.45f,0),new Vector3(0,.83f,0)},
                new[]{.205f,.174f,.006f},12,marble);
            for(int side=-1;side<=1;side+=2)
                mesh.Triangle(new Vector3(side*.13f,-.47f,-.012f),new Vector3(side*.41f,-.49f,-.012f),
                    new Vector3(side*.16f,.04f,-.012f),bronze);
            ArchiveRing(mesh,new Vector3(0,.25f,-.168f),.07f,.039f,jade,true,12);
            memories[10] = mesh.Finish("First journeys into orbit");

            mesh = Geometry();
            mesh.Box(new Vector3(-.12f,-.09f,0),new Vector3(.57f,.45f,.37f),Quaternion.Euler(0,-20,13),bronze);
            mesh.Rock(new Vector3(.26f,.23f,-.02f),new Vector3(.65f,.38f,.18f),marble,.9f,5,16,.012f);
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 arm = new Vector3(side*.53f,-.18f,.006f);
                RuinBar(mesh,new Vector3(0,-.18f,0),arm,.055f,.041f,marble);
                mesh.Box(arm,new Vector3(.44f,.17f,.044f),Quaternion.Euler(0,6,8),jade);
            }
            memories[11] = mesh.Finish("Orbital telescope");
            return memories;
        }

        private static void ArchivePyramid(WorldMesh mesh, Vector3 floor, float span, float height, Color body)
        {
            Vector3 top = floor + Vector3.up * height;
            for(int i=0;i<4;i++)
            {
                float a = Mathf.PI*.5f*i+Mathf.PI*.25f;
                float b = a+Mathf.PI*.5f;
                Vector3 p=floor+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*span*.70f;
                Vector3 q=floor+new Vector3(Mathf.Sin(b),0,Mathf.Cos(b))*span*.70f;
                mesh.Triangle(p,q,top,WorldMesh.Shade(body,.78f+i*.10f));
                for(int line=1;line<7;line++)
                {
                    float t=line*.13f;
                    RuinBar(mesh,Vector3.Lerp(p,top,t),Vector3.Lerp(q,top,t),.015f,.016f,WorldMesh.Shade(body,1.10f));
                }
            }
        }

        private static void ArchiveRing(WorldMesh mesh,Vector3 center,float radius,float width,Color color,bool upright,int steps)
        {
            Vector3 last=center+Vector3.right*radius;
            Vector3 axis=upright?Vector3.up:Vector3.forward;
            for (int i=1;i<=steps;i++)
            {
                float a=2*Mathf.PI*i/steps;
                Vector3 next=center+radius*(Vector3.right*Mathf.Cos(a)+axis*Mathf.Sin(a));
                RuinBar(mesh,last,next,width,width,color);
                last=next;
            }
        }

        private Transform BuildFindYourself(Transform parent = null, bool stand = true)
        {
            var marker=Group("Find yourself memorial",parent ?? roomShell);
            marker.localPosition=new Vector3(-2.90f,2.56f,7.60f);
            marker.localRotation=Quaternion.Euler(1,-12,0);
            var board=Geometry();
            Color slate=new Color(.25f,.32f,.34f), metal=new Color(.68f,.50f,.28f);
            board.Box(Vector3.zero,new Vector3(2.73f,.93f,.23f),Quaternion.identity,slate);
            board.Box(new Vector3(0,.477f,.045f),new Vector3(2.83f,.09f,.32f),Quaternion.identity,metal);
            board.Box(new Vector3(0,-.478f,.039f),new Vector3(2.83f,.11f,.32f),Quaternion.identity,metal);
            board.Box(new Vector3(-1.36f,0,.032f),new Vector3(.09f,.91f,.30f),Quaternion.identity,metal);
            board.Box(new Vector3(1.36f,0,.032f),new Vector3(.09f,.91f,.30f),Quaternion.identity,metal);
            if (stand)
            {
                board.Box(new Vector3(-.80f,-1.43f,.20f),new Vector3(.19f,1.93f,.36f),Quaternion.Euler(0,0,-3),slate);
                board.Box(new Vector3(.80f,-1.43f,.20f),new Vector3(.19f,1.93f,.36f),Quaternion.Euler(0,0,3),slate);
                board.Box(new Vector3(0,-2.46f,.20f),new Vector3(2.46f,.20f,.67f),Quaternion.identity,metal);
            }
            for(int i=0;i<4;i++)
            {
                float x=(i&1)==0?1.23f:-1.23f, y=(i&2)==0?.32f:-.32f;
                board.Rock(new Vector3(x,y,-.134f),Vector3.one*.093f,metal,i*.41f,3,7,.04f);
            }
            RuinPiece("Inscribed monument",board.Finish("Bronze-edged archive slate"),tunnelMetal,marker);

            const string inscription="find yourself";
            const float spacing=.030f;
            var letters=Geometry();
            var glyphs=new Dictionary<char,string> {
                { 'f', "01110100001111010000100001000010000" }, { 'i', "00100000000110000100001000010001110" },
                { 'n', "00000000001111010001100011000110001" }, { 'd', "00001000010111110001100011000101111" },
                { 'y', "00000000001000110001011110000101110" }, { 'o', "00000000000111010001100011000101110" },
                { 'u', "00000000001000110001100011001101101" }, { 'r', "00000000001011011001100001000010000" },
                { 's', "00000000000111110000011100000111110" }, { 'e', "00000000000111010001111111000001111" },
                { 'l', "01100001000010000100001000010001110" }
            };
            float start=-(inscription.Length*6-1)*spacing*.5f;
            for(int i=0;i<inscription.Length;i++)
            {
                if(!glyphs.TryGetValue(inscription[i],out string pixels))continue;
                for(int y=0;y<7;y++)
                for(int x=0;x<5;x++)
                {
                    if(pixels[y*5+x]!='1')continue;
                    letters.Box(new Vector3(start+(i*6+x)*spacing,.027f+(3-y)*spacing,-.129f),
                        new Vector3(spacing*1.01f,spacing*1.01f,.015f),Quaternion.identity,new Color(.91f,.76f,.46f));
                }
            }
            for (int s=-1;s<=1;s+=2)
                RuinBar(letters,new Vector3(s*.51f,-.19f,-.131f),new Vector3(s*1.14f,-.19f,-.131f),.018f,.015f,metal);
            var glowText=RuinPiece("find yourself",letters.Finish("find yourself"),glow,marker);
            Light(glowText.GetComponent<MeshRenderer>(),Color.white,1.05f);
            return marker;
        }

        private void DrawRuinArtifacts(Journey journey, float time)
        {
            int artifacts = 0;
            int chests = 0;
            foreach (RuinObject item in journey.RuinObjects)
            {
                if (item.Depth < journey.Distance - 12 || item.Depth > journey.Distance + 52) continue;
                if (item.Collected && item.Kind != RuinObjectKind.Chest) continue;
                if (item == journey.RuinLootSource && item.Kind != RuinObjectKind.Chest) continue;
                Vector3 at = RuinPoint(item.Lane, item.Depth, item.Altitude) - Vector3.up * RuinY;
                float yaw = item.Lane * -10;
                if (item.Kind == RuinObjectKind.Chest)
                {
                    bool displaying = journey.RuinLootDisplaySource == item || item.Collected && journey.RuinChestRemaining(item) == 0;
                    archiveObjects.UpdateChest(chests++, item.ChestStyle, at - Vector3.up * .29f, time, item.Reveal, displaying, yaw);
                }
                else
                    archiveObjects.UpdateArtifact(artifacts++, (int)item.Kind, at, time, item.Reveal, yaw);
            }
            archiveObjects.HideFrom(artifacts, chests);
            DrawRuinLootDevices(journey, time);
        }

        private void DisposeRuinTunnel()
        {
            archiveObjects?.Dispose();
            if (tunnelStone != null) UnityEngine.Object.Destroy(tunnelStone);
            if (tunnelMetal != null) UnityEngine.Object.Destroy(tunnelMetal);
        }
    }
}
