using System;
using System.Collections.Generic;

namespace Echoes
{
    public enum ForestMazeNodeKind { Entry, Corridor, Junction, DeadEnd, RootChamber }
    public enum ForestMazeMotion { Walking, Choosing, WaitingAtWall, TurningBack, Retracing, ApproachingWall }

    public sealed class ForestMazeNode
    {
        public int Id { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public int Level { get; }
        public ForestMazeNodeKind Kind { get; internal set; }

        internal ForestMazeNode(int id, float x, float z, int level)
        {
            Id = id;
            X = x;
            Z = z;
            Level = level;
            Y = .45f + level * 5.4f;
        }
    }

    public sealed class ForestMazeEdge
    {
        public int A { get; }
        public int B { get; }
        public float Length { get; }

        internal ForestMazeEdge(ForestMazeNode a, ForestMazeNode b)
        {
            A = a.Id;
            B = b.Id;
            Length = (float)Math.Sqrt(Square(a.X - b.X) + Square(a.Y - b.Y) + Square(a.Z - b.Z));
        }

        public int Other(int id) => id == A ? B : A;

        internal static float Square(float value) => value * value;
    }

    public sealed class ForestMaze
    {
        public const float MaximumSpeed = 3.4f;
        public const float RoomRadius = 3.52f;
        internal const float PassageHalf = 2.46f;
        internal const float RootCupLead = 1.50f;
        internal const float WallCenterRadius = RoomRadius - .17f;
        internal const float WallThickness = .46f;
        internal const float WallHeight = 4.64f;
        internal const float WallContactRadius = WallCenterRadius - WallThickness / 2 - .25f;

        private readonly List<ForestMazeNode> nodes = new List<ForestMazeNode>();
        private readonly List<ForestMazeEdge> edges = new List<ForestMazeEdge>();
        private readonly List<List<ForestMazeEdge>> connections = new List<List<ForestMazeEdge>>();
        private readonly float rootForwardX, rootForwardZ;

        public IReadOnlyList<ForestMazeNode> Nodes { get; }
        public IReadOnlyList<ForestMazeEdge> Edges { get; }
        public int Seed { get; }
        public int Entry => 0;
        public int RootChamber { get; private set; }
        internal float RootHeading { get; }

        internal float RootPositionX(float across, float ahead) =>
            nodes[RootChamber].X + across * rootForwardZ + ahead * rootForwardX;

        internal float RootPositionZ(float across, float ahead) =>
            nodes[RootChamber].Z - across * rootForwardX + ahead * rootForwardZ;

        public IReadOnlyList<ForestMazeEdge> Connections(int node) => connections[node];

        internal float Heading(int from, int to) => (float)(Math.Atan2(
            nodes[to].X - nodes[from].X, nodes[to].Z - nodes[from].Z) * 180 / Math.PI);

        internal float ArrivalHeading(int node, int incoming) =>
            incoming >= 0 ? Heading(incoming, node) : Heading(node, connections[node][0].Other(node));

        internal int ClosestExit(int node, float heading, int excluded)
        {
            int closest = -1;
            float smallest = float.PositiveInfinity;
            foreach (ForestMazeEdge edge in connections[node])
            {
                int next = edge.Other(node);
                if (next == excluded) continue;
                float angle = Math.Abs(DeltaAngle(heading, Heading(node, next)));
                if (angle < smallest || angle == smallest && next < closest)
                {
                    smallest = angle;
                    closest = next;
                }
            }
            return closest;
        }

        internal static float DeltaAngle(float current, float target)
        {
            float delta = (target - current + 180) % 360;
            if (delta < 0) delta += 360;
            return delta - 180;
        }

        public ForestMaze(int seed)
        {
            Seed = seed;
            Nodes = nodes.AsReadOnly();
            Edges = edges.AsReadOnly();
            var random = new Random(seed);
            // The folded spine overlaps lower rooms in plan. Broad switchbacks carry the two ramps.
            float[,] spine = {
                { -13.1f, 149 }, { -13.1f, 172 }, { -40, 172 }, { -40, 196 },
                { -63, 196 }, { -63, 170 }, { -34, 170 }, { -34, 146 },
                { -52, 146 }, { -52, 172 }, { -77, 172 }, { -77, 198 }
            };
            for (int i = 0; i < spine.GetLength(0); i++)
            {
                float x = spine[i, 0], z = spine[i, 1];
                if (i > 1)
                {
                    x += Offset(random, 1.65f);
                    z += Offset(random, 1.65f);
                }
                AddNode(x, z, i / 4);
                if (i > 0) Link(i - 1, i);
            }
            RootChamber = nodes.Count - 1;
            RootHeading = Heading(RootChamber - 1, RootChamber);
            double rootRadians = RootHeading * Math.PI / 180;
            rootForwardX = (float)Math.Sin(rootRadians);
            rootForwardZ = (float)Math.Cos(rootRadians);

            int lowLoop = 1 + random.Next(2);
            if (!AddLoop(lowLoop, random) && !AddLoop(3 - lowLoop, random)) AddDiagonalLoop(1, 3);
            int middleLoop = 5 + random.Next(2);
            if (!AddLoop(middleLoop, random)) AddLoop(11 - middleLoop, random);
            int upperLoop = 8 + random.Next(2);
            if (!AddLoop(upperLoop, random) && !AddLoop(17 - upperLoop, random)) AddDiagonalLoop(8, 10);

            // Stop adding branches where a passage already has a three-armed junction.
            for (int i = 1; i < RootChamber; i++)
                if (connections[i].Count == 2) AddBlindPassage(i, random);

            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].Kind = i == Entry ? ForestMazeNodeKind.Entry :
                    i == RootChamber ? ForestMazeNodeKind.RootChamber :
                    connections[i].Count >= 3 ? ForestMazeNodeKind.Junction :
                    connections[i].Count == 1 ? ForestMazeNodeKind.DeadEnd : ForestMazeNodeKind.Corridor;
            }
        }

        public float ShortestDistance()
        {
            var costs = new float[nodes.Count];
            var visited = new bool[nodes.Count];
            for (int i = 0; i < costs.Length; i++) costs[i] = float.PositiveInfinity;
            costs[Entry] = 0;
            while (true)
            {
                int current = -1;
                for (int i = 0; i < costs.Length; i++)
                    if (!visited[i] && (current == -1 || costs[i] < costs[current])) current = i;
                if (current == -1 || current == RootChamber) return costs[RootChamber];
                visited[current] = true;
                foreach (ForestMazeEdge edge in connections[current])
                {
                    int next = edge.Other(current);
                    costs[next] = Math.Min(costs[next], costs[current] + edge.Length);
                }
            }
        }

        private int AddNode(float x, float z, int floor)
        {
            int id = nodes.Count;
            nodes.Add(new ForestMazeNode(id, x, z, floor));
            connections.Add(new List<ForestMazeEdge>(3));
            return id;
        }

        private void Link(int a, int b)
        {
            var edge = new ForestMazeEdge(nodes[a], nodes[b]);
            edges.Add(edge);
            connections[a].Add(edge);
            connections[b].Add(edge);
        }

        private bool AddLoop(int from, Random random)
        {
            int to = from + 1;
            ForestMazeNode a = nodes[from], b = nodes[to];
            float width = 11.8f + Offset(random, 1.1f);
            float direction = random.Next(2) == 0 ? -1 : 1;
            float dx = b.X - a.X, dz = b.Z - a.Z;
            float magnitude = (float)Math.Sqrt(dx * dx + dz * dz);
            for (int side = 0; side < 2; side++, direction = -direction)
            {
                float offsetX = dz / magnitude * width * direction;
                float offsetZ = -dx / magnitude * width * direction;
                var cornerA = new ForestMazeNode(nodes.Count, a.X + offsetX, a.Z + offsetZ, a.Level);
                var cornerB = new ForestMazeNode(nodes.Count + 1, b.X + offsetX, b.Z + offsetZ, b.Level);
                if (!Fits(a, cornerA) || !Fits(cornerA, cornerB) || !Fits(cornerB, b)) continue;
                int idA = AddNode(cornerA.X, cornerA.Z, a.Level);
                int idB = AddNode(cornerB.X, cornerB.Z, a.Level);
                Link(from, idA);
                Link(idA, idB);
                Link(idB, to);
                return true;
            }
            return false;
        }

        private void AddDiagonalLoop(int from,int to)
        {
            if (Fits(nodes[from],nodes[to],.79f)) Link(from,to);
        }

        private void AddBlindPassage(int junction, Random random)
        {
            ForestMazeNode from = nodes[junction];
            int heading = random.Next(8);
            for (int attempt = 0; attempt < 8; attempt++)
            {
                double turn = (heading + attempt) * Math.PI / 4;
                float stepX = (float)Math.Cos(turn);
                float stepZ = (float)Math.Sin(turn);
                float length = 13 + Offset(random, 1.2f);
                var bend = new ForestMazeNode(nodes.Count, from.X + stepX * length, from.Z + stepZ * length, from.Level);
                if (!Fits(from, bend)) continue;
                int side = random.Next(2) == 0 ? -1 : 1;
                for (int corner = 0; corner < 2; corner++, side = -side)
                {
                    var wall = new ForestMazeNode(nodes.Count + 1, bend.X + stepZ * side * 9,
                        bend.Z - stepX * side * 9, from.Level);
                    if (!Fits(bend, wall)) continue;
                    int split = AddNode(bend.X, bend.Z, from.Level);
                    int deadEnd = AddNode(wall.X, wall.Z, from.Level);
                    Link(junction, split);
                    Link(split, deadEnd);
                    if (random.Next(2)==0)
                    {
                        // Some dead ends continue through a second bend before meeting the wall.
                        ForestMazeNode end=nodes[deadEnd];
                        var extension=new ForestMazeNode(nodes.Count,end.X+stepX*8,end.Z+stepZ*8,end.Level);
                        if (Fits(end,extension)) Link(deadEnd,AddNode(extension.X,extension.Z,end.Level));
                    }
                    return;
                }
            }
        }

        private bool Fits(ForestMazeNode start, ForestMazeNode end,float angleLimit=.58f)
        {
            if (end.X > -7 || end.Z < 124) return false;
            foreach (ForestMazeEdge edge in edges)
            {
                ForestMazeNode a = nodes[edge.A], b = nodes[edge.B];
                if (Math.Min(a.Level, b.Level) > start.Level || Math.Max(a.Level, b.Level) < start.Level) continue;
                if (a.Id == start.Id || b.Id == start.Id || a.Id == end.Id || b.Id == end.Id)
                {
                    ForestMazeNode shared = a.Id == start.Id || b.Id == start.Id ? start : end;
                    ForestMazeNode outward = shared == start ? end : start;
                    ForestMazeNode neighbor = a.Id == shared.Id ? b : a;
                    float dx = outward.X - shared.X, dz = outward.Z - shared.Z;
                    float nx = neighbor.X - shared.X, nz = neighbor.Z - shared.Z;
                    if (dx * nx + dz * nz > angleLimit * Math.Sqrt((dx * dx + dz * dz) * (nx * nx + nz * nz))) return false;
                    continue;
                }
                if (Intersects(start, end, a, b) || SegmentDistanceSquared(a, start, end) < 37 ||
                    SegmentDistanceSquared(b, start, end) < 37 || SegmentDistanceSquared(start, a, b) < 37 ||
                    SegmentDistanceSquared(end, a, b) < 37) return false;
            }
            return true;
        }

        private static bool Intersects(ForestMazeNode a, ForestMazeNode b, ForestMazeNode c, ForestMazeNode d) =>
            Cross(a, b, c) * Cross(a, b, d) < 0 && Cross(c, d, a) * Cross(c, d, b) < 0;

        private static float Cross(ForestMazeNode a, ForestMazeNode b, ForestMazeNode c) =>
            (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);

        private static float SegmentDistanceSquared(ForestMazeNode point, ForestMazeNode a, ForestMazeNode b)
        {
            float x = b.X - a.X, z = b.Z - a.Z;
            float along = Math.Max(0, Math.Min(1, ((point.X - a.X) * x + (point.Z - a.Z) * z) / (x * x + z * z)));
            return ForestMazeEdge.Square(point.X - a.X - x * along) + ForestMazeEdge.Square(point.Z - a.Z - z * along);
        }

        private static float Offset(Random random, float range) => ((float)random.NextDouble() * 2 - 1) * range;
    }
}
