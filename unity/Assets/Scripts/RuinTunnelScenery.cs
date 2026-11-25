using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        internal const float RuinY = -125;
        private Transform tunnelRoot, roomShell, wayfinding, roomEmblem;
        private Material tunnelStone, tunnelMetal;
        private RuinArtifacts archiveObjects;
        private readonly List<RuinGateView> tunnelGates = new List<RuinGateView>();
        private readonly Dictionary<RuinDirection, RuinGateParts> gateParts = new Dictionary<RuinDirection, RuinGateParts>();
        private MeshRenderer[] axonSignals;
        private Mesh[] chamberEmblems;
        private int visibleRoom = -1, visibleStyle = -1;

        private sealed class RuinGateView
        {
            public Transform Root, LeftDoor, RightDoor;
            public MeshRenderer Rim, Signal, RiseHint;
            public RuinDirection Direction;
            public float OpenFraction;
        }

        private sealed class RuinGateParts
        {
            public Mesh Structure, Rim, Door, Signal, Palm;
        }

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

            DrawRuinEnvelopes(journey);
            for (int i = 0; i < journey.RuinPortals.Count; i++)
            {
                RuinPortal portal = journey.RuinPortals[i];
                if (portal.Direction == RuinDirection.Exit || journey.RuinRetryState != RuinRetryPhase.Inactive)
                    tunnelGates[i].Root.gameObject.SetActive(false);
                else PoseRuinGate(tunnelGates[i], portal, journey, time);
            }

            float archiveTime = journey.RuinRetryState == RuinRetryPhase.Impact ? time - journey.Age : time;
            for (int i = 0; i < axonSignals.Length; i++)
            {
                float phase = archiveTime * (.31f + (i % 3) * .12f) + i * .417f;
                float depth = journey.Distance + Mathf.Repeat(phase * 3.9f, 48) + 2;
                float side = (i & 1) == 0 ? -1 : 1;
                var trace = RuinTrack.Frame(journey.RuinRoomId,journey.RuinRoomLength,depth);
                axonSignals[i].transform.localPosition = trace.Point(side*(4.61f+.22f*Mathf.Sin(depth*.30f)),5.2f+.67f*Mathf.Sin(depth*.15f+i*.57f));
                axonSignals[i].transform.localRotation = trace.Rotation;
                Light(axonSignals[i], RuinGalleryAccentFor(style,journey.RuinRoomId), .52f + .26f * Mathf.Sin(phase * 5));
            }
            DrawRuinGalleryStyle(journey,archiveTime);
            DrawRuinExhibits(journey,archiveTime);
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

            wayfinding.gameObject.SetActive(journey.RuinRoomId == 0 && journey.RuinVisitedCount == 1 &&
                journey.RuinLoops == 0 && journey.Distance < 8.2f);
            var entranceRoad=RuinTrack.Frame(journey.RuinRoomId,journey.RuinRoomLength,2.6f);
            wayfinding.localPosition=entranceRoad.Point(-3.0f,2.56f);
            wayfinding.localRotation=entranceRoad.Rotation*Quaternion.Euler(1,-12,0);
            bool hiddenChamber = style == 5;
            roomEmblem.gameObject.SetActive(hiddenChamber);
            if (hiddenChamber)
            {
                int identity = Mathf.Max(0, journey.RuinRoomId - 11) % chamberEmblems.Length;
                roomEmblem.GetComponent<MeshFilter>().sharedMesh = chamberEmblems[identity];
                var arch=RuinTrack.Frame(journey.RuinRoomId,journey.RuinRoomLength,journey.RuinRoomLength*.46f);
                roomEmblem.localPosition=arch.Point(0,6.68f);
                roomEmblem.localRotation = arch.Rotation*Quaternion.Euler(0,0,identity*8-15);
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

            RuinPiece("Gallery entrance stonework", parts.Structure, tunnelStone, rootGate);
            return new RuinGateView {
                Root = rootGate, Direction = direction,
                Rim = RuinPiece("Layer mark", parts.Rim, glow, rootGate).GetComponent<MeshRenderer>(),
                Signal = RuinPiece("Threshold signal", parts.Signal, glow, rootGate).GetComponent<MeshRenderer>(),
                RiseHint = parts.Palm == null ? null : RuinPiece("Open hand to ascend", parts.Palm, glow, rootGate).GetComponent<MeshRenderer>(),
                LeftDoor = RuinPiece("Left folding shutter", parts.Door, tunnelMetal, rootGate),
                RightDoor = RuinPiece("Right folding shutter", parts.Door, tunnelMetal, rootGate)
            };
        }

        private RuinGateParts MakeRuinGateParts(RuinDirection direction)
        {
            var stonework = Geometry();
            var trim = Geometry();

            var shutter = Geometry();
            var indicator = Geometry();
            Color dark = new Color(.20f, .26f, .34f);
            bool left = direction == RuinDirection.Left, right = direction == RuinDirection.Right;
            bool upper = direction == RuinDirection.Upper, lower = direction == RuinDirection.Lower;
            bool exit = direction == RuinDirection.Exit;
            int faces = left ? 6 : right ? 8 : upper ? 20 : lower ? 8 : exit ? 12 : 10;
            float angleOffset = lower || right ? Mathf.PI / faces : 0;
            Vector2 radius = RuinGateRadius(direction);
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
                if (!left || i % 3 != 0)
                    RuinBar(trim, p * 1.082f - Vector3.forward * .108f, q * 1.082f - Vector3.forward * .108f,
                        upper || lower ? .052f : .047f, .047f, Color.white);
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
            if (upper || lower)
            {
                float facing = upper ? 1 : -1;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * radius.x * .66f;
                    for (int band = 0; band < 2; band++)
                    {
                        float y = facing * (.58f * radius.y + band * .18f);
                        Vector3 peak = new Vector3(x, y + facing * .13f, -.17f);
                        RuinBar(indicator, new Vector3(x - .17f, y, -.17f), peak, .035f, .023f, Color.white);
                        RuinBar(indicator, peak, new Vector3(x + .17f, y, -.17f), .035f, .023f, Color.white);
                    }
                }
            }
            Mesh palmSymbol = null;
            if (upper)
            {
                var palm = Geometry();
                Color highlight = new Color(.66f, .76f, .77f);
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
                palmSymbol = palm.Finish("Open-palm illustration");
            }

            return new RuinGateParts { Structure = stonework.Finish("Entrance relief"), Rim = trim.Finish("Entrance inlay"),
                Door = shutter.Finish("Machined shutters"),
                Signal = indicator.Finish("Passage markers"), Palm = palmSymbol };
        }

        private static bool IsVerticalRuinGate(RuinDirection direction) =>
            direction == RuinDirection.Upper || direction == RuinDirection.Lower;

        private static Vector2 RuinGateRadius(RuinDirection direction)
        {
            switch (direction)
            {
                case RuinDirection.Upper: return new Vector2(2.00f, 1.66f);
                case RuinDirection.Lower: return new Vector2(2.08f, 1.37f);
                case RuinDirection.Exit: return new Vector2(1.66f, 1.86f);
                default: return new Vector2(1.14f, 1.23f);
            }
        }

        private static float RuinGateScale(RuinDirection direction) => direction == RuinDirection.Lower ? 1.47f : 1.35f;

        private static Vector3 GatePoint(float angle, Vector2 radius, float z) =>
            new Vector3(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y, z);

        private void PoseRuinGate(RuinGateView view, RuinPortal gate, Journey journey, float time)
        {
            var layout=RuinTrack.Frame(journey.RuinRoomId,journey.RuinRoomLength,gate.Depth);
            Vector2 hole=RuinGallerySection(journey.RuinRoomStyle,RuinTrack.PortalAngle(gate.Direction));
            Vector3 bore=RuinTrack.PortalForward(layout,gate);
            view.Root.localPosition=layout.Point(hole.x,hole.y)-bore*.20f;
            view.Root.localRotation=Quaternion.LookRotation(bore);
            view.Root.localScale=Vector3.one*RuinGateScale(gate.Direction);
            bool top=gate.Direction==RuinDirection.Upper, bottom=gate.Direction==RuinDirection.Lower;
            float range=RuinTrack.Offset(gate.Depth,journey.Distance,journey.RuinRoomLength);
            bool reveal = !gate.Hidden || range < 24;
            bool visible = range > -7 && range < 25;
            view.Root.gameObject.SetActive(visible);
            if (!visible) return;
            bool shutter = gate.Gate != RuinGate.Open;
            if (gate.Opened && gate.OpenAge >= 0)
                view.OpenFraction = Mathf.SmoothStep(0, 1, Mathf.Clamp01(gate.OpenAge / .68f));
            float opened = gate.Opened ? view.OpenFraction : 0;
            view.LeftDoor.gameObject.SetActive(shutter && opened < .998f);
            view.RightDoor.gameObject.SetActive(shutter && opened < .998f);
            float halfDoor = RuinGateRadius(gate.Direction).x * .48f;
            float doorShift = halfDoor * (1 + 2.15f * opened);
            view.LeftDoor.localPosition = new Vector3(-doorShift, opened * .115f, .145f);
            view.RightDoor.localPosition = new Vector3(doorShift, -opened * .08f, .145f);
            view.LeftDoor.localRotation = Quaternion.Euler(0, opened * -70, opened * -6);
            view.RightDoor.localRotation = Quaternion.Euler(0, opened * 70, opened * 6);
            Color hue = top ? new Color(.61f, .62f, 1) : bottom ? new Color(.35f, .83f, .86f) :
                gate.Direction == RuinDirection.Left ? new Color(.41f,.93f,.75f) : new Color(.97f,.62f,.34f);
            hue = Color.Lerp(hue, RuinGalleryAccentFor(journey.RuinRoomStyle,journey.RuinRoomId), .15f + (gate.FrameStyle % 5) * .04f);
            bool ready = gate == journey.RuinEntrance || gate == journey.RuinApproach && journey.RuinCanChoosePassage;
            float pulse = .13f * Mathf.Sin(time * 2.6f + gate.Id * 1.71f);
            float light = reveal ? (ready ? 1.15f : .82f) : .19f;
            Light(view.Rim, hue, light + pulse);
            Light(view.Signal, hue, reveal ? (ready ? 1.32f : .94f) + pulse : .38f);
            if (view.RiseHint != null) Light(view.RiseHint, hue, reveal ? (ready ? 1.04f : .76f) : .22f);
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
            int niches = 0;
            foreach (RuinObject item in journey.RuinObjects)
            {
                float gap = RuinTrack.Offset(item.Depth, journey.Distance, journey.RuinRoomLength);
                if (gap < -6 || gap > 22) continue;
                var frame = RuinTrack.Frame(journey.RuinRoomId, journey.RuinRoomLength, item.Depth);
                float height = .54f + 3.30f * item.Altitude;
                float heading = frame.Rotation.eulerAngles.y;
                if (item.Kind == RuinObjectKind.Chest)
                {
                    Vector3 at = frame.Point(item.Lane * 2.45f, height - .29f);
                    bool displaying = journey.RuinLootDisplaySource == item || item.Collected && journey.RuinChestRemaining(item) == 0;
                    archiveObjects.UpdateChest(chests++, item.ChestStyle, at, time, item.Reveal, displaying, heading - item.Lane * 10);
                    continue;
                }
                DrawRuinNiche(niches++, item, frame, journey, time, gap);
                if (item.Collected || item == journey.RuinLootSource || item.Reveal <= .17f) continue;
                float reveal = Mathf.InverseLerp(.17f, .88f, item.Reveal);
                float reach = Mathf.SmoothStep(0, 1, reveal);
                int side = item.Lane < 0 ? -1 : 1;
                // Moving toward the front corner clears the screen from the trailing camera.
                Vector3 relic = frame.Point(side * (RuinNicheLateral - reach * .78f), height + reach * .11f)
                    + frame.Forward * (.26f - reach * .86f);
                archiveObjects.UpdateArtifact(artifacts++, (int)item.Kind, relic, time, reveal, heading + side * 23);
            }
            HideRuinNichesFrom(niches);
            archiveObjects.HideFrom(artifacts, chests);
            DrawRuinLootDevices(journey, time);
        }

        private void DisposeRuinTunnel()
        {
            archiveObjects?.Dispose();
            DisposeRuinEnvelopes();
            DisposeRuinExhibits();
            if (tunnelStone != null) UnityEngine.Object.Destroy(tunnelStone);
            if (tunnelMetal != null) UnityEngine.Object.Destroy(tunnelMetal);
        }
    }
}
