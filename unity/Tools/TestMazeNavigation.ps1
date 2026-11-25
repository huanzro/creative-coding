$ErrorActionPreference = 'Stop'
$sources = 'Journey.cs', 'JourneyChapters.cs', 'JourneyForest.cs', 'ForestMaze.cs', 'JourneyMaze.cs', 'JourneyRuins.cs', 'JourneyRuinLoot.cs', 'JourneyRuinRetry.cs', 'RuinAtlas.cs', 'AtmosphereMotion.cs', 'ExhibitProtocol.cs', 'SpiritFrame.cs', 'OpeningTiming.cs', 'SpiritFlight.cs', 'FireflyCourse.cs' |
    ForEach-Object { Join-Path $PSScriptRoot "../Assets/Scripts/$_" }

$harness = @'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Echoes;

public static class MazeNavigationChecks
{
    const float Tick = .02f;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly Dictionary<string, FieldInfo> Fields = typeof(Journey).GetFields(Private).ToDictionary(field => field.Name);
    static readonly int[][] Touches = { new[] { 0 }, new[] { 1 }, new[] { 0, 0, 1 }, new[] { 1, 1, 0 } };

    public static string Run()
    {
        int approaches = 0, walls = 0, passages = 0, gates = 0;
        foreach (int seed in Enumerable.Range(0, 192).Concat(new[] { -100, 541, 1337, int.MinValue, int.MaxValue }))
        {
            Journey game = NewJourney(seed);
            ForestMaze maze = game.ForestMaze;
            CheckGate(game, maze, seed);
            gates++;
            foreach (ForestMazeNode room in maze.Nodes)
            {
                var roads = maze.Connections(room.Id);
                foreach (ForestMazeEdge entered in roads)
                {
                    int incoming = entered.Other(room.Id);
                    if (room.Kind == ForestMazeNodeKind.RootChamber) continue;
                    if (room.Kind == ForestMazeNodeKind.Junction)
                    {
                        CheckFork(game, room, incoming, seed);
                        approaches++;
                    }
                    else if (room.Kind == ForestMazeNodeKind.DeadEnd || room.Kind == ForestMazeNodeKind.Entry)
                    {
                        CheckWall(game, room, incoming, seed);
                        walls++;
                    }
                    else
                    {
                        Arrive(game, room, incoming, seed);
                        int onward = roads.Single(road => road.Other(room.Id) != incoming).Other(room.Id);
                        Require(game.ForestMazeTarget == onward, seed, room.Id, "corridor selects its only onward opening");
                        FaceAndWalk(game, room, onward, seed);
                        passages++;
                    }
                }
            }
        }
        return $"Passed {approaches} directed fork approaches with both buttons, interrupted turns and automatic continuation, " +
            $"{passages} corridor approaches, {walls} modeled wall and gate contacts, and {gates} entry gate closures across 197 mazes.";
    }

    static Journey NewJourney(int seed)
    {
        var game = new Journey(seed);
        game.SelectChapter(0);
        var saved = game.Checkpoint;
        saved.forestBranch = 1;
        saved.forestDepth = 112;
        saved.forestLane = -1;
        saved.forestSeed = seed;
        if (!game.Restore(saved)) throw new Exception("Could not restore the left approach.");
        game.SetHand(Hand.Open);
        for (int tick = 0; tick < 600 && !game.ForestMazeActive; tick++)
        {
            game.Flight.Reset(.4f, game.ForestHeightLimit);
            game.Step(Tick, .8f);
        }
        if (!game.ForestMazeActive) throw new Exception("Could not enter the maze.");
        ((List<ForestFeature>)Fields["forestFeatures"].GetValue(game)).Clear();
        game.Flight.Reset(0);
        return game;
    }

    static void CheckGate(Journey game, ForestMaze maze, int seed)
    {
        ForestMazeNode entry = maze.Nodes[maze.Entry];
        Require(game.ForestMazeGateClosure == 0, seed, entry.Id, "the modeled gate begins raised");
        while (game.ForestMazeTravel < 1.6f) game.Step(Tick, 1);
        Require(game.ForestMazeGateClosure == 0, seed, entry.Id, "the gate stays raised while the player crosses");
        for (int tick = 0; tick < 80; tick++) game.Step(Tick, 1);
        Require(game.ForestMazeGateClosure == 1, seed, entry.Id, "the gate closes behind the player");
    }

    static void CheckFork(Journey game, ForestMazeNode room, int incoming, int seed)
    {
        ForestMaze maze = game.ForestMaze;
        float approach = Heading(maze.Nodes[incoming], room);
        int[] doors = maze.Connections(room.Id).Select(road => road.Other(room.Id)).Where(id => id != incoming)
            .OrderBy(id => Angle(approach, Heading(room, maze.Nodes[id]))).ThenBy(id => id).ToArray();
        int nearest = doors.OrderBy(id => Math.Abs(Angle(approach, Heading(room, maze.Nodes[id])))).ThenBy(id => id).First();

        for (int sequence = 0; sequence < Touches.Length; sequence++)
        {
            Arrive(game, room, incoming, seed);
            Require(game.MazeMotion == ForestMazeMotion.Choosing && game.ForestMazeLeft == doors[0] &&
                game.ForestMazeRight == doors[1], seed, room.Id, "A/B preserve adjacent doorway order for each approach");
            float travel = game.ForestMazeTravel;
            int previous = -1;
            foreach (int button in Touches[sequence])
            {
                float heading = game.ForestMazeHeading;
                float turnAge = (float)Fields["mazeTurnAge"].GetValue(game);
                Require(game.Touch(button) && game.ForestMazeTarget == doors[button], seed, room.Id,
                    "the press selects its physical door instead of skipping");
                Require(Math.Abs(Angle(heading, game.ForestMazeHeading)) < .001 && game.ForestMazeTravel == travel, seed, room.Id,
                    "a press or reversal cannot teleport the actor");
                if (previous == button)
                    Require((float)Fields["mazeTurnAge"].GetValue(game) == turnAge, seed, room.Id, "a repeated press preserves turn progress");
                if (game.MazeMotion == ForestMazeMotion.FacingPassage)
                {
                    game.Step(Tick, 0);
                    Require(game.ForestMazeTravel == travel, seed, room.Id, "turning keeps the same room position");
                }
                previous = button;
            }
            FaceAndWalk(game, room, doors[previous], seed);
            int target = game.ForestMazeTarget;
            game.Touch(1 - previous);
            Require(game.ForestMazeTarget == target, seed, room.Id, "a late tap cannot redirect an occupied passage or skip the next room");
        }

        Arrive(game, room, incoming, seed);
        float nearestHeading = Heading(room, maze.Nodes[nearest]);
        float nearAngle = Math.Abs(Angle(approach, nearestHeading));
        if (nearAngle > 46)
        {
            float travel = game.ForestMazeTravel;
            for (int tick = 0; tick < 55; tick++) game.Step(Tick, 0);
            Require(game.MazeMotion == ForestMazeMotion.Choosing && game.ForestMazeTravel == travel &&
                Math.Abs(Angle(game.ForestMazeHeading, nearestHeading)) < .01 &&
                game.ForestMazeLeft == doors[0] && game.ForestMazeRight == doors[1],
                seed, room.Id, "a quiet actor faces the nearest real doorway without changing the original choices");
        }

        Arrive(game, room, incoming, seed);
        for (int tick = 0; tick < 49; tick++) game.Step(Tick, 1);
        Require(game.MazeMotion == ForestMazeMotion.Choosing, seed, room.Id, "continuous sound leaves a full beat for a manual choice");
        float atChoice = game.ForestMazeTravel;
        game.ForestChoiceInputHeld = true;
        for (int tick = 0; tick < 18; tick++) game.Step(Tick, 1);
        Require(game.MazeMotion == ForestMazeMotion.Choosing && game.ForestMazeTravel == atChoice,
            seed, room.Id, "a press awaiting tap or hold detection pauses the nearly finished choice window");
        game.ForestChoiceInputHeld = false;
        for (int tick = 0; tick < 20 && game.MazeMotion == ForestMazeMotion.Choosing; tick++) game.Step(Tick, 1);
        Require(game.ForestMazeTarget == nearest, seed, room.Id, "forward sound finds the nearest actual door instead of stopping at air");
        FaceAndWalk(game, room, nearest, seed);

        Arrive(game, room, incoming, seed);
        float heldTravel = game.ForestMazeTravel;
        game.Touch(0);
        game.ForestChoiceInputHeld = true;
        for (int tick = 0; tick < 70; tick++) game.Step(Tick, 1);
        Require(game.MazeMotion == ForestMazeMotion.Walking && game.ForestMazeTravel == heldTravel && game.ForestMazeSpeed == 0,
            seed, room.Id, "a pending opposite button holds position even after the first turn has finished");
        game.ForestChoiceInputHeld = false;
        game.Touch(1);
        FaceAndWalk(game, room, doors[1], seed);
    }

    static void FaceAndWalk(Journey game, ForestMazeNode room, int next, int seed)
    {
        ForestMazeNode door = game.ForestMaze.Nodes[next];
        float travel = game.ForestMazeTravel;
        for (int tick = 0; tick < 70 && game.MazeMotion == ForestMazeMotion.FacingPassage; tick++)
        {
            game.Step(Tick, 1);
            Require(game.ForestMazeTravel == travel, seed, room.Id, "forward sound cannot move the actor during a turn");
        }
        Require(game.MazeMotion == ForestMazeMotion.Walking &&
            Math.Abs(Angle(game.ForestMazeHeading, Heading(room, door))) < .01, seed, room.Id, "the final heading points directly into the real door");
        game.Step(Tick, 1);
        float dx = game.ForestMazeX - room.X, dz = game.ForestMazeZ - room.Z;
        Require(game.ForestMazeTravel > travel && dx * (door.X - room.X) + dz * (door.Z - room.Z) > 0, seed, room.Id,
            "walking resumes toward the selected door");
    }

    static void CheckWall(Journey game, ForestMazeNode room, int incoming, int seed)
    {
        Arrive(game, room, incoming, seed);
        Require(game.MazeMotion == ForestMazeMotion.ApproachingWall && game.ForestMazeWallOffset == 0,
            seed, room.Id, "the room center stays open all the way up to the real wall");
        float startTravel = game.ForestMazeTravel;
        int ticks = 0;
        while (game.MazeMotion == ForestMazeMotion.ApproachingWall && ticks++ < 150) game.Step(Tick, 1);
        float contact = ForestMaze.WallCenterRadius - ForestMaze.WallThickness / 2 - .25f;
        float x = game.ForestMazeX, z = game.ForestMazeZ, heading = game.ForestMazeHeading;
        Require(game.MazeMotion == ForestMazeMotion.WaitingAtWall && Math.Abs(game.ForestMazeTravel - startTravel - contact) < .002 &&
            Math.Abs(Length(x - room.X, z - room.Z) - contact) < .002, seed, room.Id,
            "the actor walks to model clearance instead of meeting an invisible wall");
        if (room.Kind == ForestMazeNodeKind.Entry)
            Require(game.ForestMazeGateClosure == 1, seed, room.Id, "a visible gate blocks the vestibule when reached from inside");
        for (int tick = 0; tick < 45; tick++) game.Step(Tick, 1);
        Require(game.MazeMotion == ForestMazeMotion.WaitingAtWall && game.ForestMazeHeading == heading, seed, room.Id, "wall impact pauses the actor");
        ticks = 0;
        while ((game.MazeMotion == ForestMazeMotion.WaitingAtWall || game.MazeMotion == ForestMazeMotion.TurningBack) && ticks++ < 70)
        {
            game.Step(Tick, 1);
            Require(game.ForestMazeX == x && game.ForestMazeZ == z, seed, room.Id, "wall recovery turns on the spot");
        }
        Require(game.MazeMotion == ForestMazeMotion.Retracing && game.ForestMazeTarget == incoming &&
            Math.Abs(Angle(game.ForestMazeHeading, Heading(room, game.ForestMaze.Nodes[incoming]))) < .01,
            seed, room.Id, "the actor automatically faces the closest available road");
        while (game.ForestMazeWallOffset > 0)
        {
            float former = game.ForestMazeWallOffset;
            game.Step(Tick, 0);
            Require(game.ForestMazeWallOffset < former && game.ForestMazeWallOffset >= Math.Max(0, former - .15f),
                seed, room.Id, "the actor walks away from the wall without teleporting");
        }
        Require(game.ForestMazeX == room.X && game.ForestMazeZ == room.Z, seed, room.Id, "backtracking rejoins the same centerline");
        int guard = 0;
        while (game.Phase == Stage.ForestLoop && guard++ < 2000) game.Step(Tick, 0);
        Require(game.Phase == Stage.ForestExplore, seed, room.Id, "the recovered route becomes controllable");
    }

    static void Arrive(Journey game, ForestMazeNode room, int incoming, int seed)
    {
        // Start one metre before each physical threshold and let the game process the arrival.
        ForestMaze maze = game.ForestMaze;
        ForestMazeNode from = maze.Nodes[incoming];
        float length = maze.Connections(room.Id).Single(road => road.Other(room.Id) == incoming).Length;
        float fraction = (length - 1) / length;
        var path = (List<int>)Fields["mazePath"].GetValue(game);
        path.Clear();
        path.Add(incoming);
        Set(game, "mazeFrom", incoming);
        Set(game, "mazeIncoming", -1);
        Set(game, "mazeTarget", room.Id);
        Set(game, "mazeAlong", length - 1);
        Set(game, "mazeWallOffset", 0f);
        Set(game, "mazeYaw", Heading(from, room));
        Set(game, "mazeSpeed", 0f);
        Set(game, "mazeX", from.X + (room.X - from.X) * fraction);
        Set(game, "mazeY", from.Y + (room.Y - from.Y) * fraction);
        Set(game, "mazeZ", from.Z + (room.Z - from.Z) * fraction);
        Set(game, "mazeMotion", ForestMazeMotion.Walking);
        for (int tick = 0; tick < 60 && game.ForestMazeNode != room.Id; tick++) game.Step(Tick, 1);
        Require(game.ForestMazeNode == room.Id && Math.Abs(game.ForestMazeX - room.X) < .001 &&
            Math.Abs(game.ForestMazeZ - room.Z) < .001, seed, room.Id, "the actual arrival reaches this room");
    }

    static void Set(Journey journey, string field, object value) => Fields[field].SetValue(journey, value);
    static float Heading(ForestMazeNode from, ForestMazeNode to) =>
        (float)(Math.Atan2(to.X - from.X, to.Z - from.Z) * 180 / Math.PI);
    static float Angle(float from, float to) => (to - from + 540) % 360 - 180;
    static double Length(float x, float z) => Math.Sqrt(x * x + z * z);
    static void Require(bool result, int seed, int node, string invariant)
    {
        if (!result) throw new Exception($"Seed {seed}, node {node}: {invariant}");
    }
}
'@

$harnessPath = Join-Path ([System.IO.Path]::GetTempPath()) ("MazeNavigation-{0}.cs" -f [Guid]::NewGuid())
try {
    Set-Content -LiteralPath $harnessPath -Value $harness -Encoding utf8
    Add-Type -Path ($sources + $harnessPath)
    [MazeNavigationChecks]::Run()
} finally {
    if (Test-Path -LiteralPath $harnessPath) { Remove-Item -LiteralPath $harnessPath }
}
