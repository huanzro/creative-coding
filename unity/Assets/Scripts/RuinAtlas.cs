using System;

namespace Echoes
{
    public enum RuinDirection { Left, Right, Upper, Lower, Forward, Exit }
    public enum RuinGate { Open, Palm, Voice }
    public enum RuinObjectKind { Heart, Brain, Phone, Computer, Chest }

    public sealed class RuinPortal
    {
        internal const float LeadDepth = 3.65f;
        internal const float TailDepth = 1.22f;
        internal const float MaximumReach = 1.65f;

        public int Id { get; }
        public float Depth { get; }
        public float Altitude { get; }
        public int Lane { get; }
        public RuinDirection Direction { get; }
        public RuinGate Gate { get; }
        public bool Hidden { get; }
        public int TargetRoom { get; internal set; }
        public int FrameStyle { get; }
        public bool Opened { get; internal set; }
        public float OpenAge { get; internal set; } = -1;

        internal RuinPortal(int id, float depth, float altitude, int lane, RuinDirection direction,
            RuinGate gate, bool hidden, int target, int frameStyle)
        {
            Id = id;
            Depth = depth;
            Altitude = altitude;
            Lane = lane;
            Direction = direction;
            Gate = gate;
            Hidden = hidden;
            TargetRoom = target;
            FrameStyle = frameStyle;
            Opened = gate == RuinGate.Open;
        }
    }

    public sealed class RuinObject
    {
        public int Id { get; }
        public float Depth { get; }
        public float Altitude { get; }
        public int Lane { get; }
        public RuinObjectKind Kind { get; }
        public int ChestStyle { get; }
        public bool Collected { get; internal set; }
        public float Reveal { get; internal set; }
        public float OpenAge { get; internal set; } = -1;

        internal RuinObject(int id, float depth, float altitude, int lane, RuinObjectKind kind, int chestStyle)
        {
            Id = id;
            Depth = depth;
            Altitude = altitude;
            Lane = lane;
            Kind = kind;
            ChestStyle = chestStyle;
        }
    }

    internal sealed class RuinRoom
    {
        public string Name { get; }
        public int Layer { get; }
        public int Style { get; }
        public float Length { get; }
        public RuinPortal[] Portals { get; }
        public RuinObject[] Objects { get; }

        public RuinRoom(string name, int layer, int style, float length, RuinPortal[] portals, RuinObject[] objects)
        {
            Name = name;
            Layer = layer;
            Style = style;
            Length = length;
            Portals = portals;
            Objects = objects;
        }
    }

    internal static class RuinAtlas
    {
        internal const int RoomCount = 16;
        internal const int PassageSlots = 5;
        internal const int ObjectSlots = 4;
        internal const int WelcomeChestStyle = 10;
        private const int ExtraFindsPerRoom = 4;
        private const int FirstExtraFind = RoomCount * ObjectSlots;
        private const int FirstNewVault = RoomCount * PassageSlots;
        internal const int ExitRoom = 9;

        internal static float LengthScale(int room) => room == 0 || room == ExitRoom ? 1f : .5f;

        private static readonly RuinObjectKind[] AdditionalFinds =
        {
            RuinObjectKind.Brain, RuinObjectKind.Computer, RuinObjectKind.Heart, RuinObjectKind.Phone
        };
        private static readonly float[] AdditionalFindFractions = { .109f, .335f, .560f, .825f };
        private static readonly (string name, int layer, int style, int length, int chest)[] Rooms =
        {
            ("文明长廊", 0, 0, 48, WelcomeChestStyle), ("铭文断层", 0, 2, 40, 0),
            ("机械年轮", 0, 4, 44, 11), ("穹顶档案", 1, 1, 46, 1),
            ("镜像回廊", 0, 3, 50, 12), ("回声索引", -1, 2, 43, 2),
            ("沉底书库", -2, 4, 42, 13), ("计算回廊", -1, 1, 49, 3),
            ("天窗交汇", 2, 3, 45, 4), ("往日边缘", -2, 0, 51, 14),
            ("自我碑阵", 3, 1, 40, 15), ("琥珀夹层", -1, 5, 27, 5),
            ("静音工坊", 0, 5, 30, 6), ("逆读藏室", -1, 5, 29, 7),
            ("沉钟暗室", -3, 5, 31, 8), ("未归档之处", -3, 5, 32, 9)
        };

        private static readonly (RuinDirection dir, RuinGate gate, bool hidden, int target)[][] Exits =
        {
            new[] { P(RuinDirection.Left,RuinGate.Palm,1), P(RuinDirection.Upper,RuinGate.Open,3), P(RuinDirection.Right,RuinGate.Open,2), F(4) },
            new[] { P(RuinDirection.Upper,RuinGate.Voice,6), P(RuinDirection.Lower,RuinGate.Open,11,true), F(5) },
            new[] { P(RuinDirection.Left,RuinGate.Open,7), P(RuinDirection.Right,RuinGate.Voice,12,true), F(5) },
            new[] { P(RuinDirection.Right,RuinGate.Palm,7), P(RuinDirection.Lower,RuinGate.Palm,13,true), F(6) },
            new[] { P(RuinDirection.Right,RuinGate.Open,2), P(RuinDirection.Upper,RuinGate.Open,3), P(RuinDirection.Left,RuinGate.Voice,1), F(5) },
            new[] { P(RuinDirection.Left,RuinGate.Open,8), P(RuinDirection.Lower,RuinGate.Open,6), F(7) },
            new[] { P(RuinDirection.Left,RuinGate.Voice,14,true), P(RuinDirection.Right,RuinGate.Open,8), F(5) },
            new[] { P(RuinDirection.Right,RuinGate.Open,9), P(RuinDirection.Upper,RuinGate.Palm,8), F(9) },
            new[] { P(RuinDirection.Left,RuinGate.Open,10), P(RuinDirection.Lower,RuinGate.Open,9), P(RuinDirection.Right,RuinGate.Voice,6), F(4) },
            new[] { P(RuinDirection.Lower,RuinGate.Open,15,true), P(RuinDirection.Exit,RuinGate.Open,-1), F(7) },
            new[] { P(RuinDirection.Left,RuinGate.Voice,8), P(RuinDirection.Right,RuinGate.Palm,3), F(9) },
            new[] { P(RuinDirection.Right,RuinGate.Open,12), F(1) },
            new[] { P(RuinDirection.Upper,RuinGate.Open,3), F(2) },
            new[] { P(RuinDirection.Left,RuinGate.Open,14), F(3) },
            new[] { P(RuinDirection.Upper,RuinGate.Palm,5), F(6) },
            new[] { P(RuinDirection.Right,RuinGate.Open,12), F(9) }
        };

        private static (RuinDirection, RuinGate, bool, int) P(RuinDirection dir, RuinGate gate, int target, bool hidden = false) =>
            (dir, gate, hidden, target);
        private static (RuinDirection, RuinGate, bool, int) F(int target) => P(RuinDirection.Forward, RuinGate.Open, target);

        // Keep the former forward slot and its random draws to preserve saved gate and item IDs.
        private static readonly RuinDirection[] CircuitSpurs = {
            RuinDirection.Lower, RuinDirection.Right, RuinDirection.Lower, RuinDirection.Left,
            RuinDirection.Lower, RuinDirection.Right, RuinDirection.Upper, RuinDirection.Lower,
            RuinDirection.Upper, RuinDirection.Right, RuinDirection.Lower, RuinDirection.Left,
            RuinDirection.Lower, RuinDirection.Right, RuinDirection.Right, RuinDirection.Left
        };

        // New vaults reach a physically higher gallery. Keep the former portal IDs and
        // the original shuffle stream, since both gate progress and loot depend on them.
        private static readonly int[] LiftDestinations =
        {
            8, 3, 8, 8, -1, 1, -1, -1, -1, 4, -1, 0, -1, 10, -1, 7
        };

        internal static RuinRoom[] Build(int seed)
        {
            var random = new Random(seed);
            var atlas = new RuinRoom[RoomCount];
            for (int roomId = 0; roomId < atlas.Length; roomId++)
            {
                var room = Rooms[roomId];
                var connections = (ValueTuple<RuinDirection, RuinGate, bool, int>[])Exits[roomId].Clone();
                // Retain the destination graph but vary the order and spacing of optional spurs.
                // On the entrance hall, preserve a quieter stretch before the first portal.
                for (int i = connections.Length - 2; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    var swap = connections[i];
                    connections[i] = connections[j];
                    connections[j] = swap;
                }
                int lift = LiftDestinations[roomId];
                var portals = new RuinPortal[connections.Length + (lift >= 0 ? 1 : 0)];
                for (int slot = 0; slot < connections.Length; slot++)
                {
                    var (dir, gate, hidden, destination) = connections[slot];
                    bool end = dir == RuinDirection.Forward;
                    float depth = end ? room.length : 13 + (room.length - 19) * slot / (float)(connections.Length - 1) + Offset(random, .85f);
                    float height = dir == RuinDirection.Upper ? 1.23f + Offset(random, .07f) :
                        dir == RuinDirection.Lower ? .045f :
                        dir == RuinDirection.Exit ? .19f :
                        .30f + Offset(random, .11f);
                    if (end)
                    {
                        dir = CircuitSpurs[roomId];
                        depth = room.length - 6.4f;
                        if (dir == RuinDirection.Upper) height = 1.23f + (height - .30f) * .58f;
                        if (dir == RuinDirection.Lower) height = .055f;
                    }
                    else if (dir == RuinDirection.Exit)
                    {
                        dir = RuinDirection.Left;
                        destination = 0;
                    }
                    if (destination == ExitRoom)
                    {
                        // Keep the vault formerly reached through the terminal gallery in the live network.
                        bool vault = roomId == 7 && end;
                        destination = vault ? 15 : roomId == 6 ? 5 : 6;
                        if (vault) hidden = true;
                    }
                    int lane = dir == RuinDirection.Left ? -1 : dir == RuinDirection.Right ? 1 : 0;
                    int id = roomId * PassageSlots + slot;
                    portals[slot] = new RuinPortal(id, depth, height, lane, dir, gate, hidden,
                        destination, (roomId * 7 + slot * 3 + (seed & 7)) % 10);
                }
                if (lift >= 0)
                {
                    portals[connections.Length] = new RuinPortal(FirstNewVault + roomId, 7.25f, .97f, 0,
                        RuinDirection.Upper, RuinGate.Open, false, lift, (roomId * 3 + (seed & 7) + 4) % 10);
                }
                var objects = new RuinObject[ObjectSlots + ExtraFindsPerRoom];
                int lastSide = 0;
                for (int i = 0; i < 3; i++)
                {
                    RuinObjectKind kind = i == 2 && roomId % 4 == 1 ? RuinObjectKind.Heart :
                        i == 2 && roomId % 4 == 3 ? RuinObjectKind.Brain :
                        (i + random.Next(2)) % 2 == 0 ? RuinObjectKind.Phone : RuinObjectKind.Computer;
                    // Keep the original seeded kind and draws so saved item IDs retain their contents.
                    if (roomId == 0 && i == 1) kind = RuinObjectKind.Heart;
                    float stop = room.length * (.18f + .22f * i) + Offset(random, 1.1f);
                    float height = kind == RuinObjectKind.Heart ? .25f + Offset(random, .05f) :
                        i == 0 ? .12f + Offset(random, .05f) : .36f + (float)random.NextDouble() * .89f;
                    int lane = roomId == 0 && i == 1 ? 0 : random.Next(-1, 2);
                    int side = lastSide != 0 ? -lastSide : lane == 0 ? ((roomId & 1) == 0 ? -1 : 1) : lane;
                    lastSide = side;
                    float shelf = Math.Max(.16f, Math.Min(.42f, .12f + height * .28f));
                    objects[i] = new RuinObject(roomId * ObjectSlots + i, stop, shelf, side, kind, -1);
                }
                float chestDepth = room.chest == WelcomeChestStyle ? 4.60f : room.length * .48f;
                int chestLane = room.chest == WelcomeChestStyle ? 0 : roomId % 4 == 0 ? -1 : 1;
                if (room.chest < WelcomeChestStyle)
                {
                    // Retain the ten original random draws so older seeds keep their devices and room layout.
                    chestDepth = room.length * .72f + Offset(random, 1);
                    if (room.style != 5) random.NextDouble();
                    chestLane = room.style == 5 ? 0 : random.Next(-1, 2);
                }
                chestDepth = KeepRuinChestReachable(chestDepth, chestLane, room.length, portals);
                float chestHeight = chestLane == 0 ? .04f : .10f;
                objects[3] = new RuinObject(roomId * ObjectSlots + 3, chestDepth, chestHeight, chestLane,
                    RuinObjectKind.Chest, room.chest);
                PlaceRuinCaches(seed, roomId, room.length, portals, objects);
                float scale = LengthScale(roomId);
                if (scale < 1)
                {
                    for (int i = 0; i < portals.Length; i++)
                    {
                        RuinPortal portal = portals[i];
                        portals[i] = new RuinPortal(portal.Id, portal.Depth * scale, portal.Altitude,
                            portal.Lane, portal.Direction, portal.Gate, portal.Hidden, portal.TargetRoom, portal.FrameStyle);
                    }
                    for (int i = 0; i < objects.Length; i++)
                    {
                        RuinObject item = objects[i];
                        objects[i] = new RuinObject(item.Id, item.Depth * scale, item.Altitude,
                            item.Lane, item.Kind, item.ChestStyle);
                    }
                }
                atlas[roomId] = new RuinRoom(room.name, room.layer, room.style, room.length * scale,
                    roomId == ExitRoom ? Array.Empty<RuinPortal>() : portals, objects);
            }
            return atlas;
        }

        private static float KeepRuinChestReachable(float origin, int side, float length, RuinPortal[] portals)
        {
            float safest = origin;
            float best = float.NegativeInfinity;
            for (int step = -175; step <= 175; step++)
            {
                float depth = origin + step * .12f;
                if (depth < 4.4f || depth > length - 3.8f) continue;
                float score = -Math.Abs(depth - origin);
                foreach (RuinPortal portal in portals)
                {
                    float offset = depth - portal.Depth;
                    if (portal.Lane == 0)
                    {
                        if (Math.Abs(offset) < 1.55f) score -= 75;
                    }
                    else if (WithinSideEntrance(offset, .30f))
                    {
                        if (side == 0 || portal.Lane == side) score -= 40;
                        if (portal.Direction == RuinDirection.Right) score -= side < 0 ? 7 : 25;
                    }
                }
                if (score <= best) continue;
                safest = depth;
                best = score;
            }
            return safest;
        }

        private static bool WithinSideEntrance(float offset, float padding)
        {
            const float reach = RuinPortal.MaximumReach;
            return offset >= -RuinPortal.LeadDepth * reach - padding &&
                offset <= RuinPortal.TailDepth * reach + padding;
        }

        private static void PlaceRuinCaches(int seed, int room, float length,
            RuinPortal[] portals, RuinObject[] objects)
        {
            for (int i = 0; i < 3; i++)
            {
                RuinObject item = objects[i];
                float depth = FindRuinCacheDepth(item.Depth, length, item.Lane, portals, objects, i);
                objects[i] = new RuinObject(item.Id, depth, item.Altitude, item.Lane, item.Kind, item.ChestStyle);
            }

            // A separate stream keeps all legacy chest, gate and artifact identities intact.
            var extraRandom = new Random(unchecked(seed * 397 ^ (room + 1) * 8191));
            int firstSide = -objects[0].Lane;
            for (int i = 0; i < ExtraFindsPerRoom; i++)
            {
                int slot = ObjectSlots + i;
                int side = (i & 1) == 0 ? firstSide : -firstSide;
                float anchor = length * AdditionalFindFractions[i] + Offset(extraRandom, .82f);
                float stop = FindRuinCacheDepth(anchor, length, side, portals, objects, slot);
                float shelf = .25f + Offset(extraRandom, .068f);
                int id = FirstExtraFind + room * ExtraFindsPerRoom + i;
                objects[slot] = new RuinObject(id, stop, shelf, side, AdditionalFinds[i], -1);
            }
        }

        private static float FindRuinCacheDepth(float origin, float length, int side, RuinPortal[] portals,
            RuinObject[] objects, int slot)
        {
            const float minimumDepth = 4.75f;
            float best = Math.Max(minimumDepth, Math.Min(length - 4.8f, origin));
            float bestScore = float.NegativeInfinity;
            for (int step = -138; step <= 138; step++)
            {
                float candidate = origin + step * .35f;
                if (candidate < minimumDepth || candidate > length - 4.8f) continue;
                float portalSpace = 3.25f;
                foreach (RuinPortal portal in portals)
                {
                    float delta = candidate - portal.Depth;
                    if (portal.Lane == side && WithinSideEntrance(delta, .28f))
                    {
                        // A held steering input automatically enters a matching branch.
                        portalSpace = -85;
                        break;
                    }
                    float separation = Math.Abs(delta);
                    float gap = portal.Lane != 0 ? separation + 1.25f :
                        separation < 1.44f ? -65f : separation;
                    portalSpace = Math.Min(portalSpace, gap);
                    if (side < 0 && portal.Direction == RuinDirection.Right && WithinSideEntrance(delta, .16f))
                        portalSpace = Math.Min(portalSpace, -17f);
                }
                float cacheSpace = 3.25f;
                for (int previous = 0; previous < objects.Length; previous++)
                {
                    RuinObject neighbor = objects[previous];
                    if (previous == slot || neighbor == null) continue;
                    float sideGap = neighbor.Lane == side || neighbor.Lane == 0 ? 0 : 1.65f;
                    cacheSpace = Math.Min(cacheSpace, Math.Abs(neighbor.Depth - candidate) + sideGap);
                }
                float overlapPenalty = cacheSpace < 1.40f ? 35f : 0;
                float score = cacheSpace * 1.45f + portalSpace * .74f - Math.Abs(candidate - origin) * .24f - overlapPenalty;
                if (score <= bestScore) continue;
                best = candidate;
                bestScore = score;
            }
            return best;
        }

        private static float Offset(Random random, float radius) => (float)(random.NextDouble() * 2 - 1) * radius;
    }
}
