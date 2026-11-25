$ErrorActionPreference = 'Stop'

$sources = 'ForestMaze.cs', 'MazeDirections.cs' | ForEach-Object {
    (Get-Content (Join-Path $PSScriptRoot "../Assets/Scripts/$_") -Raw) -replace '(?m)^using [^;]+;\r?\n', ''
}

$harness=@'
public static class MazeDirectionsCheck
{
    private const float Tolerance = .002f;

    public static string Run()
    {
        int layouts = 0, origins = 0;
        foreach (int seed in Seeds())
        {
            var maze = new Echoes.ForestMaze(seed);
            var directions = new Echoes.MazeDirections(maze);
            float[] distances = IndependentDistances(maze);
            foreach (Echoes.ForestMazeNode node in maze.Nodes)
            {
                int[] route = directions.RouteFrom(node.Id,-1,node.X,node.Y,node.Z,out float distance);
                Require(route[0]==node.Id && Math.Abs(distances[node.Id]-distance) < Tolerance, seed, "node cost");
                CheckPath(maze, route, distance, seed);
                origins++;
            }
            foreach (Echoes.ForestMazeEdge edge in maze.Edges)
            {
                Echoes.ForestMazeNode a=maze.Nodes[edge.A], b=maze.Nodes[edge.B];
                for (int sample=1; sample<=3; sample++)
                {
                    float t = sample*.25f;
                    float x=Mix(a.X,b.X,t), y=Mix(a.Y,b.Y,t), z=Mix(a.Z,b.Z,t);
                    float shortest = Math.Min(distances[edge.A]+edge.Length*t, distances[edge.B]+edge.Length*(1-t));
                    int[] forward = directions.RouteFrom(edge.A,edge.B,x,y,z,out float forwardDistance);
                    int[] backward = directions.RouteFrom(edge.B,edge.A,x,y,z,out float backwardDistance);
                    Require(Math.Abs(shortest-forwardDistance)<Tolerance && Math.Abs(forwardDistance-backwardDistance)<Tolerance,
                        seed, "edge cost survives reversal");
                    Require(forward[0]==edge.A || forward[0]==edge.B, seed, "start on current edge");
                    CheckPath(maze,forward,distances[forward[0]],seed);
                    origins++;
                }
            }
            layouts++;
        }
        return $"Passed {origins} route origins in {layouts} actual maze layouts, including player positions on graded ramps and both edge directions.";
    }

    private static IEnumerable<int> Seeds()
    {
        yield return 1965320713;
        for (int seed=1; seed<=197; seed++) yield return seed;
    }

    private static float[] IndependentDistances(Echoes.ForestMaze maze)
    {
        var result = new float[maze.Nodes.Count];
        for (int i=0; i<result.Length; i++) result[i] = float.PositiveInfinity;
        result[maze.RootChamber] = 0;
        for (int pass=1; pass<result.Length; pass++)
            foreach (Echoes.ForestMazeEdge edge in maze.Edges)
            {
                result[edge.A] = Math.Min(result[edge.A],result[edge.B]+edge.Length);
                result[edge.B] = Math.Min(result[edge.B],result[edge.A]+edge.Length);
            }
        return result;
    }

    private static void CheckPath(Echoes.ForestMaze maze, int[] route, float expected, int seed)
    {
        Require(route[route.Length-1]==maze.RootChamber && route.Length<=maze.Nodes.Count, seed, "route ends in root chamber");
        float length = 0;
        for (int i=1; i<route.Length; i++)
        {
            float segment = float.PositiveInfinity;
            foreach (Echoes.ForestMazeEdge edge in maze.Connections(route[i-1]))
                if (edge.Other(route[i-1])==route[i]) segment=edge.Length;
            Require(!float.IsInfinity(segment), seed, "route only uses real passages");
            length+=segment;
        }
        Require(Math.Abs(expected-length)<Tolerance, seed, "highlighted path has the shortest length");
    }

    private static float Mix(float a,float b,float t) => a+(b-a)*t;
    private static void Require(bool result,int seed,string check)
    {
        if (!result) throw new Exception($"Maze seed {seed}: {check}.");
    }
}
'@

Add-Type -TypeDefinition ('using System; using System.Collections.Generic;' + "`n" + ($sources -join "`n") + "`n" + $harness)
[MazeDirectionsCheck]::Run()
