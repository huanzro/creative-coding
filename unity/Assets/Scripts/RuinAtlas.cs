using System;

namespace Echoes
{
    public enum RuinDirection { Left, Right, Upper, Lower, Forward, Exit }
    public enum RuinGate { Open, Palm, Voice }
    public enum RuinObjectKind { Heart, Brain, Phone, Computer, Chest }

    public sealed class RuinPortal
    {
        public int Id { get; }
        public float Depth { get; }
        public float Altitude { get; }
        public int Lane { get; }
        public RuinDirection Direction { get; }
        public RuinGate Gate { get; }
        public bool Hidden { get; }
        public int TargetRoom { get; }
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
        internal const int ExitRoom = 9;
        private static readonly (string name, int layer, int style, int length, int chest)[] Rooms =
        {
            ("文明长廊", 0, 0, 48, -1), ("铭文断层", 0, 2, 40, 0),
            ("机械年轮", 0, 4, 44, -1), ("穹顶档案", 1, 1, 46, 1),
            ("镜像回廊", 0, 3, 50, -1), ("回声索引", -1, 2, 43, 2),
            ("沉底书库", -2, 4, 42, -1), ("计算回廊", -1, 1, 49, 3),
            ("天窗交汇", 2, 3, 45, 4), ("往日边缘", -2, 0, 51, -1),
            ("自我碑阵", 3, 1, 40, -1), ("琥珀夹层", -1, 5, 27, 5),
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
                var portals = new RuinPortal[connections.Length];
                for (int slot = 0; slot < portals.Length; slot++)
                {
                    var (dir, gate, hidden, destination) = connections[slot];
                    bool end = dir == RuinDirection.Forward;
                    float depth = end ? room.length : 13 + (room.length - 19) * slot / (float)(portals.Length - 1) + Offset(random, .85f);
                    int lane = dir == RuinDirection.Left || dir == RuinDirection.Exit ? -1 :
                        dir == RuinDirection.Right ? 1 : 0;
                    float height = dir == RuinDirection.Upper ? 1.23f + Offset(random, .07f) :
                        dir == RuinDirection.Lower ? .045f :
                        dir == RuinDirection.Exit ? .19f :
                        .30f + Offset(random, .11f);
                    int id = roomId * PassageSlots + slot;
                    portals[slot] = new RuinPortal(id, depth, height, lane, dir, gate, hidden,
                        destination, (roomId * 7 + slot * 3 + (seed & 7)) % 10);
                }
                var objects = new RuinObject[room.chest < 0 ? 3 : 4];
                for (int i = 0; i < 3; i++)
                {
                    RuinObjectKind kind = i == 2 && roomId % 4 == 1 ? RuinObjectKind.Heart :
                        i == 2 && roomId % 4 == 3 ? RuinObjectKind.Brain :
                        (i + random.Next(2)) % 2 == 0 ? RuinObjectKind.Phone : RuinObjectKind.Computer;
                    // Put the first heartbeat where it can be heard even when travelling low.
                    if (roomId == 0 && i == 1) kind = RuinObjectKind.Heart;
                    float stop = room.length * (.18f + .22f * i) + Offset(random, 1.1f);
                    float height = kind == RuinObjectKind.Heart ? .25f + Offset(random, .05f) :
                        i == 0 ? .12f + Offset(random, .05f) : .36f + (float)random.NextDouble() * .89f;
                    int lane = roomId == 0 && i == 1 ? 0 : random.Next(-1, 2);
                    objects[i] = new RuinObject(roomId * ObjectSlots + i, stop, height, lane, kind, -1);
                }
                if (room.chest >= 0)
                    objects[3] = new RuinObject(roomId * ObjectSlots + 3, room.length * .72f + Offset(random, 1),
                        room.style == 5 ? .06f : .32f + (float)random.NextDouble() * .70f,
                        room.style == 5 ? 0 : random.Next(-1, 2), RuinObjectKind.Chest, room.chest);
                atlas[roomId] = new RuinRoom(room.name, room.layer, room.style, room.length, portals, objects);
            }
            return atlas;
        }

        private static float Offset(Random random, float radius) => (float)(random.NextDouble() * 2 - 1) * radius;
    }
}
