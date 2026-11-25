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
    static readonly Dictionary<string, FieldInfo> Fields = typeof(Journey)
        .GetFields(BindingFlags.Instance | BindingFlags.NonPublic).ToDictionary(field => field.Name);

    public static string Run()
    {
        int approaches = 0, choices = 0, rollingChoices = 0;
        foreach (int seed in Enumerable.Range(0,192).Concat(new[] { -100,541,1337,int.MinValue,int.MaxValue }))
        {
            Journey game = NewJourney(seed);
            ForestMaze maze = game.ForestMaze;
            foreach (ForestMazeNode room in maze.Nodes.Where(node => node.Kind == ForestMazeNodeKind.Junction))
            {
                var connections = maze.Connections(room.Id);
                foreach (ForestMazeEdge entry in connections)
                {
                    int incoming = entry.Other(room.Id);
                    approaches++;
                    foreach (ForestMazeEdge exit in connections)
                    {
                        int target = exit.Other(room.Id);
                        Arrive(game, room, incoming);
                        float direction = Bearing(room,maze.Nodes[target]);
                        float turn = Delta(game.ForestMazeHeading,direction);
                        float travel = game.ForestMazeTravel;
                        game.SetHand(Hand.Fist);
                        game.SetMazeSteering(turn < 0, turn > 0);
                        for (int i=0; i < (int)Math.Round(Math.Abs(turn) / (112 * Tick)); i++) game.Step(Tick,0);
                        game.SetMazeSteering(false,false);
                        float aim = game.ForestMazeHeading;
                        Check(game.ForestMazeTravel == travel && game.ForestMazeTarget == -1 && Math.Abs(Delta(aim,direction)) <= 1.13,
                            seed,room.Id,"the player rotates freely while stopped without choosing a branch");
                        game.SetHand(Hand.Open);
                        game.Step(Tick,0);
                        Check(game.ForestMazeTarget == target,seed,room.Id,"reopening chooses the nearest 3D tunnel, including the return arm");
                        float dx=game.ForestMazeX-room.X, dz=game.ForestMazeZ-room.Z;
                        Check(dx*(maze.Nodes[target].X-room.X)+dz*(maze.Nodes[target].Z-room.Z)>0,
                            seed,room.Id,"movement enters the intended mouth");
                        game.Step(Tick,0);
                        Check(game.ForestMazeTarget == target,seed,room.Id,"the selected throat holds its direction while the body aligns");
                        choices++;

                        // Arrive already walking, including sharp branches that face partly back along the approach.
                        PositionBeforeRoom(game,room,incoming,direction);
                        game.Step(Tick,0);
                        Check(game.ForestMazeNode==room.Id && game.ForestMazeTarget==-1,seed,room.Id,"walking reaches the physical junction center");
                        game.Step(Tick,0);
                        Check(game.ForestMazeTarget==target,seed,room.Id,"walking uses the current facing without a separate input trigger");
                        rollingChoices++;
                    }
                }
            }
        }

        CheckHeldAndReverse();
        CheckReverseBoundary();
        CheckNearestBehindHeld();
        return $"Passed {approaches} fork approaches, {choices} stopped-heading tunnel selections, {rollingChoices} moving-heading tunnel selections, held input cancellation/wrap and corridor reversals across 197 mazes.";
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
            game.Flight.Reset(.4f,game.ForestHeightLimit);
            game.Step(Tick,.8f);
        }
        if (!game.ForestMazeActive) throw new Exception("Could not enter the maze.");
        ((List<ForestFeature>)Fields["forestFeatures"].GetValue(game)).Clear();
        game.Flight.Reset(0);
        return game;
    }

    static void CheckHeldAndReverse()
    {
        Journey game=NewJourney(541);
        for (int i=0; i<120; i++) game.Step(Tick,0);
        game.SetHand(Hand.Fist);
        for (int i=0; i<25; i++) game.Step(Tick,0);
        float x=game.ForestMazeX,z=game.ForestMazeZ;
        int room=game.ForestMazeNode, formerTarget=game.ForestMazeTarget;
        float angle=game.ForestMazeHeading;
        Check(game.Touch(0) && Delta(angle,game.ForestMazeHeading)<0 && game.ForestMazeTarget==formerTarget,
            541,room,"a quick A click rotates by an angle without choosing an arm");
        game.Touch(1);
        game.SetMazeSteering(true,true);
        for (int i=0; i<25; i++) game.Step(Tick,0);
        Check(Math.Abs(Delta(angle,game.ForestMazeHeading))<.01f,541,room,"holding both buttons cancels yaw");
        game.SetMazeSteering(false,true);
        for (int i=0; i<82; i++) game.Step(Tick,0);
        game.SetMazeSteering(false,false);
        Check(game.ForestMazeHeading >= -180 && game.ForestMazeHeading < 0,541,room,"yaw wraps through 180 degrees");
        Check(x==game.ForestMazeX && z==game.ForestMazeZ,541,room,"a held fist prevents movement during the free turn");
        game.SetHand(Hand.Open);
        game.Step(Tick,0);
        Check(game.ForestMazeNode==formerTarget && game.ForestMazeTarget==room,541,room,"a reversed heading walks along the same corridor backwards");
        Check(Math.Abs(game.ForestMazeX-x)+Math.Abs(game.ForestMazeZ-z)<.035,541,room,"corridor reversal preserves position");
    }

    static void CheckReverseBoundary()
    {
        Journey game=NewJourney(541);
        ForestMazeNode room=game.ForestMaze.Nodes.First(node=>node.Kind==ForestMazeNodeKind.Junction);
        int incoming=game.ForestMaze.Connections(room.Id)[0].Other(room.Id);
        float rear=Bearing(room,game.ForestMaze.Nodes[incoming]);
        PositionBeforeRoom(game,room,incoming,rear,4.35f);
        game.Step(Tick,0);
        Check(game.ForestMazeTarget==room.Id,541,room.Id,"turning sharply inside the marked approach waits for the junction decision");
        PositionBeforeRoom(game,room,incoming,rear,4.65f);
        game.Step(Tick,0);
        Check(game.ForestMazeTarget==incoming,541,room.Id,"facing back just outside the junction approach can reverse immediately");
    }

    static void CheckNearestBehindHeld()
    {
        Journey game=NewJourney(541);
        ForestMaze maze=game.ForestMaze;
        ForestMazeNode room=maze.Nodes.First(node=>node.Kind==ForestMazeNodeKind.Corridor &&
            Math.Abs(Delta(Bearing(node,maze.Nodes[maze.Connections(node.Id)[0].Other(node.Id)]),
                Bearing(node,maze.Nodes[maze.Connections(node.Id)[1].Other(node.Id)]))) < 120);
        int first=maze.Connections(room.Id)[0].Other(room.Id), second=maze.Connections(room.Id)[1].Other(room.Id);
        float a=Bearing(room,maze.Nodes[first]), b=Bearing(room,maze.Nodes[second]);
        float facing=Delta(0, a + Delta(a,b)*.48f+180);
        int expected=maze.Connections(room.Id).OrderBy(edge=>Math.Abs(Delta(facing,Bearing(room,maze.Nodes[edge.Other(room.Id)]))))
            .First().Other(room.Id);
        Set(game,"mazeFrom",room.Id);
        Set(game,"mazeTarget",-1);
        Set(game,"mazeX",room.X);
        Set(game,"mazeY",room.Y);
        Set(game,"mazeZ",room.Z);
        Set(game,"mazeYaw",facing);
        game.SetMazeSteering(true,true);
        for (int tick=0;tick<110;tick++) game.Step(Tick,0);
        Check(game.ForestMazeTarget==expected && Math.Abs(Delta(game.ForestMazeHeading,facing))<.01f &&
            Math.Sqrt(Math.Pow(game.ForestMazeX-room.X,2)+Math.Pow(game.ForestMazeZ-room.Z,2))>2,
            541,room.Id,"a nearest doorway outside the front cone does not reverse after leaving its throat");
        game.SetMazeSteering(false,false);
        float before=Math.Abs(Delta(facing,Bearing(room,maze.Nodes[expected])));
        for (int tick=0;tick<18;tick++) game.Step(Tick,0);
        Check(game.ForestMazeTarget==expected && Math.Abs(Delta(game.ForestMazeHeading,Bearing(room,maze.Nodes[expected]))) < before-30,
            541,room.Id,"releasing the turn aligns to the chosen tunnel while walking instead of bouncing back");
    }

    static void Arrive(Journey game, ForestMazeNode room, int incoming)
    {
        PositionBeforeRoom(game,room,incoming,Bearing(game.ForestMaze.Nodes[incoming],room));
        game.Step(Tick,0);
        Check(game.ForestMazeNode==room.Id && game.ForestMazeTarget==-1,game.ForestMaze.Seed,room.Id,"arrive at room center");
    }

    static void PositionBeforeRoom(Journey game, ForestMazeNode room, int incoming, float heading, float remaining=.012f)
    {
        if (game.Phase!=Stage.ForestExplore) throw new Exception("Expected forest exploration.");
        ForestMazeNode from=game.ForestMaze.Nodes[incoming];
        float length=(float)Math.Sqrt(Math.Pow(room.X-from.X,2)+Math.Pow(room.Y-from.Y,2)+Math.Pow(room.Z-from.Z,2));
        float t=1-remaining/length;
        game.SetHand(Hand.Open);
        game.SetMazeSteering(false,false);
        Set(game,"mazeFrom",incoming);
        Set(game,"mazeTarget",room.Id);
        Set(game,"mazeAlong",length-remaining);
        Set(game,"mazeThroatRemaining",0f);
        Set(game,"mazeWallOffset",0f);
        Set(game,"mazeYaw",heading);
        Set(game,"mazeAlignToPassage",false);
        Set(game,"mazeMayReverse",true);
        Set(game,"mazeSpeed",1.25f);
        Set(game,"mazeX",from.X+(room.X-from.X)*t);
        Set(game,"mazeY",from.Y+(room.Y-from.Y)*t);
        Set(game,"mazeZ",from.Z+(room.Z-from.Z)*t);
        Set(game,"mazeMotion",ForestMazeMotion.Walking);
    }

    static float Bearing(ForestMazeNode a, ForestMazeNode b) =>
        (float)(Math.Atan2(b.X-a.X,b.Z-a.Z)*180/Math.PI);
    static float Delta(float from, float to) => (to-from+540)%360-180;
    static void Set(Journey game, string name, object value) => Fields[name].SetValue(game,value);
    static void Check(bool result,int seed,int node,string message)
    {
        if (!result) throw new Exception($"Seed {seed}, node {node}: {message}");
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
