using System;
using System.Collections.Generic;

namespace Echoes
{
    public sealed partial class Journey
    {
        internal const float MazeEntranceDepth = 124;
        private const float MazeTurnSeconds = .96f;
        private const float MazeBacktrackSpeed = 2.28f;
        private const float MazeTurnRate = 112f, MazeTapAngle = 12f;
        private const float MazeThroatLength = 2.0f, MazeApproachLength = 4.5f;
        private const float MazeReverseAngle = 108f, MazeForwardAngle = 72f;
        internal const string MazeWalkHint = "A/B 转向，张手走、握拳停。声音加速，岔口沿朝向进入通道。";
        public const float MinimumMazeWalkScale = .65f, DefaultMazeWalkScale = 1.30f, MaximumMazeWalkScale = 2f;
        private readonly List<int> mazePath = new List<int>();
        private int mazeFrom, mazeTarget = -1, mazeIncoming = -1, mazeReturnIndex, mazeReturnStop;
        private float mazeAlong, mazeYaw, mazeTurnFrom, mazeTurnTo, mazeTurnAge, mazeTurnDuration;
        private float mazeX, mazeY, mazeZ, mazeSpeed, mazeTravel;
        private float mazeWallHeading, mazeWallOffset, mazeGateStarted;
        private int mazeSteering;
        private float mazeThroatRemaining;
        private bool mazeAlignToPassage, mazeTurnHeld, mazeMayReverse;
        private float mazeWalkScale = DefaultMazeWalkScale;
        private ForestMazeMotion mazeMotion;

        public float MazeWalkScale
        {
            get => mazeWalkScale;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) return;
                mazeWalkScale = Math.Max(MinimumMazeWalkScale, Math.Min(MaximumMazeWalkScale, value));
            }
        }

        public ForestMaze ForestMaze { get; private set; }
        public bool ForestMazeActive => InForest && Branch == ForestBranch.Left && Distance >= MazeEntranceDepth && ForestMaze != null;
        internal bool ForestMazeWalkArea => ForestMazeActive && !AtMazeRoots;
        public float ForestMazeX => mazeX + (AtMazeRoots ? ForestLane * 2.1f : 0);
        public float ForestMazeY => mazeY;
        public float ForestMazeZ => mazeZ;
        public float ForestMazeHeading => mazeYaw;
        public float ForestMazeSpeed => mazeSpeed;
        public float ForestMazeTravel => mazeTravel;
        public float ForestMazeWallOffset => mazeWallOffset;
        public float ForestMazeGateClosure => Smooth((Time - mazeGateStarted) / .78f);
        public ForestMazeMotion MazeMotion => mazeMotion;
        public int ForestMazeNode => mazeFrom;
        public int ForestMazeTarget => mazeTarget;
        internal bool CanSteerMaze => Started && Gesture != Hand.Missing && ForestMazeWalkArea &&
            (Phase == Stage.ForestExplore || Phase == Stage.ForestLoop && mazeMotion == ForestMazeMotion.Retracing);
        internal float ForestMazePassageHeading => mazeTarget >= 0 ? ForestMaze.Heading(mazeFrom, mazeTarget) : mazeYaw;

        private bool AtMazeRoots => ForestMazeActive && Distance >= ClearingDepth;
        private float MazeWalkingPace => Gesture == Hand.Open
            ? mazeWalkScale * Math.Min(.98f + 2.55f * Voice, ForestMaze.MaximumSpeed / DefaultMazeWalkScale) : 0;

        // Both held inputs cancel. Rotation is independent of the gesture used to walk.
        public void SetMazeSteering(bool left, bool right)
        {
            mazeSteering = (right ? 1 : 0) - (left ? 1 : 0);
            mazeTurnHeld = left || right;
            if (mazeTurnHeld) mazeAlignToPassage = false;
        }

        private void InitializeMaze()
        {
            mazePath.Clear();
            mazeTarget = mazeIncoming = -1;
            mazeSteering = 0;
            mazeThroatRemaining = 0;
            mazeAlignToPassage = mazeTurnHeld = mazeMayReverse = false;
            mazeFrom = 0;
            mazeX = mazeY = mazeZ = mazeAlong = mazeYaw = mazeTurnAge = mazeSpeed = mazeTravel = 0;
            mazeMotion = ForestMazeMotion.Walking;
            mazeWallOffset = 0;
            mazeGateStarted = Distance >= ClearingDepth ? Time - 1 : float.PositiveInfinity;
            if (ForestMaze == null) return;
            if (Distance >= ClearingDepth) mazeFrom = ForestMaze.RootChamber;
            mazePath.Add(mazeFrom);
            SetMazePosition(ForestMaze.Nodes[mazeFrom]);
            if (mazeFrom == ForestMaze.RootChamber)
            {
                int approach = ForestMaze.Connections(mazeFrom)[0].Other(mazeFrom);
                mazeYaw = ForestMaze.ArrivalHeading(mazeFrom, approach);
            }
        }

        private void PopulateMaze(Random random)
        {
            ForestMaze = ForestForkMaze;
            foreach (ForestMazeEdge edge in ForestMaze.Edges)
            {
                ForestMazeNode a = ForestMaze.Nodes[edge.A], b = ForestMaze.Nodes[edge.B];
                if (a.Kind == ForestMazeNodeKind.Entry || b.Kind == ForestMazeNodeKind.RootChamber ||
                    a.Level != b.Level || edge.Length < 10) continue;
                float fraction = .40f + (float)random.NextDouble() * .23f;
                ForestFeatureKind kind = random.Next(5) == 0 ? ForestFeatureKind.Scarlet : ForestFeatureKind.Amber;
                if (a.Kind == ForestMazeNodeKind.Junction && b.Kind == ForestMazeNodeKind.Corridor && random.Next(3) == 0)
                    kind = a.Level == 2 ? ForestFeatureKind.Web : ForestFeatureKind.Maw;
                float altitude = kind == ForestFeatureKind.Web ? .58f : kind == ForestFeatureKind.Maw ? .035f : .08f;
                AddMazeFeature(kind, 0, altitude, Lerp(a.X,b.X,fraction), Lerp(a.Y,b.Y,fraction), Lerp(a.Z,b.Z,fraction));
            }
            ForestMazeNode root = ForestMaze.Nodes[ForestMaze.RootChamber];
            forestSites[0] = AddMazeFeature(ForestFeatureKind.Plant, -1, .064f, root.X - 2.1f, root.Y, root.Z);
            forestSites[1] = AddMazeFeature(ForestFeatureKind.Plant, 1, .63f, root.X + 2.1f, root.Y, root.Z);
        }

        private ForestFeature AddMazeFeature(ForestFeatureKind kind, int lane, float altitude, float x,float floor,float z)
        {
            ForestFeature feature = AddForestFeature(kind, lane, 124, altitude);
            feature.InMaze = true;
            feature.WorldX = x;
            feature.WorldY = floor + .54f + altitude * 5.2f;
            feature.WorldZ = z;
            return feature;
        }

        private float MazeFeatureDepth(ForestFeature feature)
        {
            double heading=mazeYaw*Math.PI/180;
            float x=feature.WorldX+(feature.LaneAt(Time)-feature.Lane)*2.1f-ForestMazeX;
            float z=feature.WorldZ+feature.DepthAt(Time)-feature.Depth-ForestMazeZ;
            return x*(float)Math.Sin(heading)+z*(float)Math.Cos(heading);
        }

        private float MazeFeatureLane(ForestFeature feature)
        {
            double heading=mazeYaw*Math.PI/180;
            float x=feature.WorldX+(feature.LaneAt(Time)-feature.Lane)*2.1f-ForestMazeX;
            float z=feature.WorldZ+feature.DepthAt(Time)-feature.Depth-ForestMazeZ;
            return Math.Abs(x*(float)Math.Cos(heading)-z*(float)Math.Sin(heading))/2.1f;
        }

        private void BeginMazeEntrance()
        {
            Distance = MazeEntranceDepth;
            ForestLane = ForestTargetLane = 0;
            forestLaneSpeed = 0;
            Flight.Reset(Math.Min(Flight.Height,ForestHeightLimit),ForestHeightLimit);
            InitializeMaze();
            SetMazeRoom();
            mazeSpeed = MazeWalkingPace;
        }

        private void AdvancePhysicalMaze(float dt)
        {
            if (AtMazeRoots) return;
            StepMazeSteering(dt);

            if (mazeMotion == ForestMazeMotion.ApproachingWall)
            {
                float remaining = ForestMaze.WallContactRadius - mazeWallOffset;
                float wallSpeed = (float)Math.Sqrt(2 * 5.1f * remaining);
                UpdateMazeSpeed(Math.Min(MazeWalkingPace, wallSpeed), dt);
                ShiftInMazeRoom(Math.Min(remaining, mazeSpeed * dt));
                if (mazeWallOffset >= ForestMaze.WallContactRadius - .001f) BeginDeadEnd();
                return;
            }

            if (mazeTarget < 0)
            {
                if (MazeWalkingPace <= 0)
                {
                    mazeSpeed = 0;
                    return;
                }
                StartMazeEdge(ForestMaze.ClosestExit(mazeFrom, mazeYaw, -1));
            }

            float deviation = Math.Abs(ForestMaze.DeltaAngle(mazeYaw, ForestMazePassageHeading));
            if (deviation < MazeForwardAngle) mazeMayReverse = true;
            if (!mazeMayReverse && !mazeTurnHeld) mazeAlignToPassage = true;
            bool aimingAtFork = ForestMaze.Nodes[mazeTarget].Kind == ForestMazeNodeKind.Junction &&
                CurrentMazeEdgeLength - mazeAlong <= MazeApproachLength;
            if (!aimingAtFork && mazeMayReverse && !mazeAlignToPassage && mazeThroatRemaining <= 0 &&
                Gesture == Hand.Open && deviation > MazeReverseAngle)
                ReverseMazeEdge();

            float beforeRoom = ForestMaze.Nodes[mazeTarget].Kind == ForestMazeNodeKind.Junction
                ? Lerp(.43f, 1, Clamp((CurrentMazeEdgeLength - mazeAlong) / MazeApproachLength)) : 1;
            UpdateMazeSpeed(MazeWalkingPace * beforeRoom, dt);
            if (mazeSpeed <= 0) return;
            AlignMazeWalk(dt);
            mazeThroatRemaining = Math.Max(0, mazeThroatRemaining - mazeSpeed * dt);
            if (TraverseMazeEdge(mazeSpeed * dt))
            {
                ReachMazeNode();
                return;
            }
            if (mazeFrom == ForestMaze.Entry && mazeAlong >= 1.75f)
                CloseMazeGate();
        }

        private void StepMazeSteering(float dt)
        {
            if (mazeSteering == 0 || !CanSteerMaze) return;
            mazeAlignToPassage = false;
            mazeYaw = ForestMaze.DeltaAngle(0, mazeYaw + mazeSteering * MazeTurnRate * dt);
        }

        private void AlignMazeWalk(float dt)
        {
            if (!mazeAlignToPassage || Gesture != Hand.Open || mazeSteering != 0) return;
            float delta = ForestMaze.DeltaAngle(mazeYaw, ForestMazePassageHeading);
            float step = MoveTowards(0, delta, 185f * dt);
            mazeYaw = ForestMaze.DeltaAngle(0, mazeYaw + step);
            if (Math.Abs(delta - step) < .01f) mazeAlignToPassage = false;
        }

        private void ReverseMazeEdge()
        {
            float remaining = CurrentMazeEdgeLength - mazeAlong;
            int back = mazeFrom;
            mazeFrom = mazeTarget;
            mazeTarget = back;
            mazeAlong = remaining;
            mazeThroatRemaining = 0;
            mazeMayReverse = false;
        }

        private void UpdateMazeSpeed(float target, float dt) =>
            mazeSpeed = MoveTowards(mazeSpeed, target, dt * (target < mazeSpeed ? 9.5f : 5.1f));

        private void CloseMazeGate()
        {
            if (float.IsPositiveInfinity(mazeGateStarted)) mazeGateStarted = Time;
        }

        private void ShiftInMazeRoom(float step)
        {
            mazeWallOffset += step;
            mazeTravel += Math.Abs(step);
            ForestMazeNode center = ForestMaze.Nodes[mazeFrom];
            double heading = mazeWallHeading * Math.PI / 180;
            mazeX = center.X + mazeWallOffset * (float)Math.Sin(heading);
            mazeZ = center.Z + mazeWallOffset * (float)Math.Cos(heading);
        }

        private float CurrentMazeEdgeLength =>
            EdgeLength(ForestMaze.Nodes[mazeFrom], ForestMaze.Nodes[mazeTarget]);

        private bool TraverseMazeEdge(float step)
        {
            ForestMazeNode start = ForestMaze.Nodes[mazeFrom], end = ForestMaze.Nodes[mazeTarget];
            float length = CurrentMazeEdgeLength;
            float advanced = Math.Min(step, Math.Max(0, length - mazeAlong));
            mazeAlong += advanced;
            mazeTravel += advanced;
            float t = Clamp(mazeAlong / length);
            mazeX = Lerp(start.X, end.X, t);
            mazeY = Lerp(start.Y, end.Y, t);
            mazeZ = Lerp(start.Z, end.Z, t);
            return mazeAlong >= length;
        }

        private void ReachMazeNode()
        {
            int previous=mazeFrom;
            mazeFrom=mazeTarget;
            mazeTarget=-1;
            mazeIncoming=previous;
            mazeAlong=0;
            int back=mazePath.LastIndexOf(mazeFrom);
            if (back>=0) mazePath.RemoveRange(back+1,mazePath.Count-back-1);
            else mazePath.Add(mazeFrom);

            if (mazeFrom==ForestMaze.RootChamber)
            {
                Distance=ClearingDepth;
                mazeSpeed=0;
                Flight.Reset(0);
                EnterForestPlanting();
                SaveForestSpot((int)ClearingDepth);
                return;
            }
            ForestMazeNodeKind kind = ForestMaze.Nodes[mazeFrom].Kind;
            if (kind == ForestMazeNodeKind.DeadEnd || kind == ForestMazeNodeKind.Entry)
            {
                if (kind == ForestMazeNodeKind.Entry) CloseMazeGate();
                mazeWallHeading = ForestMaze.ArrivalHeading(mazeFrom, mazeIncoming);
                mazeMotion = ForestMazeMotion.ApproachingWall;
                return;
            }
            SetMazeRoom();
        }

        private void SetMazeRoom()
        {
            mazeMotion = ForestMaze.Nodes[mazeFrom].Kind == ForestMazeNodeKind.Junction
                ? ForestMazeMotion.Choosing : ForestMazeMotion.Walking;
            Message = mazeMotion == ForestMazeMotion.Choosing
                ? "转向想走的入口，张手沿最接近朝向的隧道前进。"
                : MazeWalkHint;
        }

        private void StartMazeEdge(int next)
        {
            mazeTarget = next;
            mazeAlong = 0;
            mazeThroatRemaining = MazeThroatLength;
            mazeMayReverse = false;
            mazeAlignToPassage = !mazeTurnHeld;
            mazeMotion = ForestMazeMotion.Walking;
            Message = MazeWalkHint;
        }

        private bool TouchMaze(int option)
        {
            if (!ForestMazeWalkArea) return false;
            if (CanSteerMaze)
            {
                mazeAlignToPassage = false;
                mazeYaw = ForestMaze.DeltaAngle(0, mazeYaw + (option == 0 ? -MazeTapAngle : MazeTapAngle));
            }
            return true;
        }

        private void BeginDeadEnd()
        {
            mazeReturnIndex = mazePath.Count - 2;
            mazeReturnStop = mazeReturnIndex;
            while (mazeReturnStop > 0 && ForestMaze.Nodes[mazePath[mazeReturnStop]].Kind != ForestMazeNodeKind.Junction)
                mazeReturnStop--;
            mazeSpeed=0;
            mazeMotion=ForestMazeMotion.WaitingAtWall;
            Change(Stage.ForestLoop,"");
        }

        private void AdvanceMazeTurn(float dt)
        {
            switch (mazeMotion)
            {
                case ForestMazeMotion.WaitingAtWall:
                    if (Age<1f) return;
                    StartMazeEdge(ForestMaze.ClosestExit(mazeFrom, mazeYaw, -1));
                    ArmMazeTurn(ForestMaze.Heading(mazeFrom, mazeTarget));
                    mazeMotion=ForestMazeMotion.TurningBack;
                    return;
                case ForestMazeMotion.TurningBack:
                    if (!StepMazeTurn(dt)) return;
                    if (mazeReturnIndex < 0 && mazeWallOffset <= 0)
                    {
                        mazeMotion=ForestMazeMotion.Walking;
                        Change(Stage.ForestExplore,"");
                        return;
                    }
                    mazeMotion=ForestMazeMotion.Retracing;
                    return;
                case ForestMazeMotion.Retracing:
                    StepMazeSteering(dt);
                    UpdateMazeSpeed(Gesture == Hand.Open ? MazeBacktrackSpeed * mazeWalkScale : 0, dt);
                    if (mazeSpeed <= 0) return;
                    if (mazeWallOffset > 0)
                    {
                        ShiftInMazeRoom(-Math.Min(mazeWallOffset, mazeSpeed * dt));
                        if (mazeWallOffset <= 0 && mazeReturnIndex < 0)
                        {
                            mazeMotion = ForestMazeMotion.Walking;
                            Change(Stage.ForestExplore, "");
                        }
                        return;
                    }
                    int previous = mazeFrom;
                    if (!TraverseMazeEdge(mazeSpeed * dt)) return;
                    mazeFrom = mazeTarget;
                    if (mazeReturnIndex > mazeReturnStop)
                    {
                        StartMazeEdge(mazePath[--mazeReturnIndex]);
                        ArmMazeTurn(ForestMazePassageHeading);
                        mazeMotion = ForestMazeMotion.TurningBack;
                        return;
                    }
                    mazeTarget = -1;
                    mazeAlong = mazeSpeed = 0;
                    mazePath.RemoveRange(mazeReturnStop + 1, mazePath.Count - mazeReturnStop - 1);
                    mazeIncoming = previous;
                    Change(Stage.ForestExplore, "");
                    SetMazeRoom();
                    return;
            }
        }

        private void ArmMazeTurn(float heading, float duration = MazeTurnSeconds)
        {
            mazeTurnFrom=mazeYaw;
            mazeTurnTo=mazeYaw+ForestMaze.DeltaAngle(mazeYaw,heading);
            mazeTurnAge=mazeSpeed=0;
            mazeTurnDuration=duration;
        }

        private bool StepMazeTurn(float dt)
        {
            mazeTurnAge+=dt;
            mazeYaw=Lerp(mazeTurnFrom,mazeTurnTo,Smooth(mazeTurnAge/mazeTurnDuration));
            return mazeTurnAge>=mazeTurnDuration;
        }

        private void SetMazePosition(ForestMazeNode node)
        {
            mazeX=node.X;
            mazeY=node.Y;
            mazeZ=node.Z;
        }

        private static float EdgeLength(ForestMazeNode a,ForestMazeNode b) =>
            (float)Math.Sqrt(ForestMazeEdge.Square(a.X-b.X)+ForestMazeEdge.Square(a.Y-b.Y)+ForestMazeEdge.Square(a.Z-b.Z));

        private static float Lerp(float a,float b,float amount) => a+(b-a)*amount;

        private static float MoveTowards(float value,float target,float delta) =>
            value+Math.Sign(target-value)*Math.Min(Math.Abs(target-value),delta);
    }
}
