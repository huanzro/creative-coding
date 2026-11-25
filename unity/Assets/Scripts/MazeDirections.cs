using System;

namespace Echoes
{
    internal sealed class MazeDirections
    {
        private readonly ForestMaze maze;
        private readonly float[] distance;
        private readonly int[][] paths;

        internal MazeDirections(ForestMaze maze, int guideFloor = -1)
        {
            this.maze = maze;
            int count = maze.Nodes.Count;
            distance = new float[count];
            paths = new int[count][];
            var visited = new bool[count];
            var next = new int[count];
            for (int i = 0; i < count; i++)
            {
                distance[i] = float.PositiveInfinity;
                next[i] = -1;
            }
            int topFloor = maze.Nodes[maze.RootChamber].Level;
            if (guideFloor >= 0 && guideFloor < topFloor)
            {
                foreach (ForestMazeNode node in maze.Nodes)
                    if (node.Level == guideFloor + 1) distance[node.Id] = 0;
            }
            else distance[maze.RootChamber] = 0;

            // The same weighted graph drives the passages drawn and walked by the game.
            for (int settled = 0; settled < count; settled++)
            {
                int current = -1;
                for (int i = 0; i < count; i++)
                    if (!visited[i] && (current < 0 || distance[i] < distance[current])) current = i;
                if (current < 0 || float.IsInfinity(distance[current])) break;
                visited[current] = true;

                foreach (ForestMazeEdge edge in maze.Connections(current))
                {
                    int other = edge.Other(current);
                    if (guideFloor >= 0 && maze.Nodes[other].Level != guideFloor) continue;
                    float candidate = distance[current] + edge.Length;
                    if (candidate >= distance[other]) continue;
                    distance[other] = candidate;
                    next[other] = current;
                }
            }

            for (int start = 0; start < count; start++)
            {
                int stops = 1;
                for (int at = start; next[at] >= 0; at = next[at]) stops++;
                var route = new int[stops];
                int node = start;
                for (int i = 0; i < route.Length; i++)
                {
                    route[i] = node;
                    node = next[node];
                }
                paths[start] = route;
            }
        }

        internal int[] RouteFrom(int node, int target, float x, float y, float z, out float remaining)
        {
            ForestMazeNode start = maze.Nodes[node];
            float toStart = Length(x - start.X, y - start.Y, z - start.Z);
            remaining = toStart + distance[node];
            foreach (ForestMazeEdge edge in maze.Connections(node))
            {
                if (edge.Other(node) != target) continue;
                ForestMazeNode end = maze.Nodes[target];
                float viaTarget = Length(x - end.X, y - end.Y, z - end.Z) + distance[target];
                if (viaTarget >= remaining) break;
                remaining = viaTarget;
                return paths[target];
            }
            return paths[node];
        }

        private static float Length(float x, float y, float z) => (float)Math.Sqrt(x * x + y * y + z * z);
    }
}
