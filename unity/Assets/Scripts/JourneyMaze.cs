using System;
using System.Collections.Generic;

namespace Echoes
{
    public sealed partial class Journey
    {
        internal const float MazeEntranceDepth = 124;
        private const float MazeChoiceSeconds = 1.06f;
        private const float MazeTurnSeconds = .96f;
        private const float MazeBacktrackSpeed = 2.28f;
        public const float MinimumMazeWalkScale = .65f, DefaultMazeWalkScale = 1.30f, MaximumMazeWalkScale = 2f;
        private readonly List<int> mazePath = new List<int>();
        private int mazeFrom, mazeTarget = -1, mazeIncoming = -1, mazeReturnIndex, mazeReturnStop;
        private float mazeAlong, mazeYaw, mazeTurnFrom, mazeTurnTo, mazeTurnAge, mazeTurnDuration;
        private float mazeX, mazeY, mazeZ, mazeSpeed, mazeTravel;
        private float mazeChoiceAge, mazeWallHeading, mazeWallOffset, mazeGateStarted;
        private int mazeDefaultExit = -1;
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
        public float ForestMazeX => mazeX + (AtMazeRoots ? ForestLane * 2.1f : 0);
        public float ForestMazeY => mazeY;
        public float ForestMazeZ => mazeZ;
        public float ForestMazeHeading => mazeYaw;
        public float ForestMazeSpeed => mazeSpeed;
        public float ForestMazeTravel => mazeTravel;
        public float ForestMazeWallOffset => mazeWallOffset;
        public float ForestMazeGateClosure => Smooth((Time - mazeGateStarted) / .78f);
        public bool ForestChoiceInputHeld { get; set; }
        public ForestMazeMotion MazeMotion => mazeMotion;
        public int ForestMazeNode => mazeFrom;
        public int ForestMazeTarget => mazeTarget;
        public int ForestMazeLeft { get; private set; } = -1;
        public int ForestMazeRight { get; private set; } = -1;
        public bool ForestCanTurnAround => Started && Gesture != Hand.Missing && ForestMazeActive &&
            Phase == Stage.ForestExplore && mazeMotion != ForestMazeMotion.ApproachingWall &&
            (mazeTarget >= 0 && mazeAlong > .005f || mazeIncoming >= 0);

        private bool AtMazeRoots => ForestMazeActive && Distance >= ClearingDepth;

        private void InitializeMaze()
        {
            mazePath.Clear();
            mazeTarget = mazeIncoming = -1;
            ClearMazeChoice();
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
            ForestMaze = new ForestMaze(forestSeed);
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
            OfferMazeExits();
            mazeSpeed = LeftMazeApproachPace;
            Message = "根渠迷宫 · 轻声前进，停声驻足。岔口 A 左、B 右，长按可回头。";
        }

        private void AdvancePhysicalMaze(float dt)
        {
            if (AtMazeRoots) return;
            if (mazeMotion == ForestMazeMotion.FacingPassage)
            {
                if (StepMazeTurn(dt)) mazeMotion = ForestMazeMotion.Walking;
                return;
            }
            if (mazeMotion == ForestMazeMotion.Choosing)
            {
                if (ForestChoiceInputHeld) return;
                if (mazeTurnAge < mazeTurnDuration) StepMazeTurn(dt);
                if (Gesture == Hand.Open && Voice >= .08f) mazeChoiceAge += dt;
                else mazeChoiceAge = 0;
                if (mazeChoiceAge >= MazeChoiceSeconds) StartMazeEdge(mazeDefaultExit);
                return;
            }
            if (mazeMotion == ForestMazeMotion.ApproachingWall)
            {
                float remaining = ForestMaze.WallContactRadius - mazeWallOffset;
                float wallSpeed = (float)Math.Sqrt(2 * 5.1f * remaining);
                UpdateMazeSpeed(Math.Min(LeftMazeApproachPace, wallSpeed), dt);
                ShiftInMazeRoom(Math.Min(remaining, mazeSpeed * dt));
                if (mazeWallOffset >= ForestMaze.WallContactRadius - .001f) BeginDeadEnd();
                return;
            }
            if (mazeTarget < 0)
            {
                OfferMazeExits();
                return;
            }

            if (ForestMazeLeft >= 0 && ForestChoiceInputHeld) return;
            UpdateMazeSpeed(LeftMazeApproachPace, dt);
            if (mazeSpeed <= 0) return;
            ClearMazeChoice();
            if (TraverseMazeEdge(mazeSpeed * dt)) ReachMazeNode();
            if (mazeFrom == ForestMaze.Entry && mazeAlong >= 1.75f)
                CloseMazeGate();
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

        private bool TraverseMazeEdge(float step)
        {
            ForestMazeNode start=ForestMaze.Nodes[mazeFrom], end=ForestMaze.Nodes[mazeTarget];
            float length=EdgeLength(start,end);
            float advanced=Math.Min(step,Math.Max(0,length-mazeAlong));
            mazeAlong+=advanced;
            mazeTravel+=advanced;
            float t=Clamp(mazeAlong/length);
            mazeX=Lerp(start.X,end.X,t);
            mazeY=Lerp(start.Y,end.Y,t);
            mazeZ=Lerp(start.Z,end.Z,t);
            return t>=1;
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
            OfferMazeExits();
        }

        private void OfferMazeExits()
        {
            ForestMaze.OrderedExits(mazeFrom, mazeIncoming, out int left, out int right);
            ClearMazeChoice();
            if (right < 0)
            {
                StartMazeEdge(left);
                return;
            }

            ForestMazeLeft = left;
            ForestMazeRight = right;
            float approach = ForestMaze.ArrivalHeading(mazeFrom, mazeIncoming);
            mazeDefaultExit = ForestMaze.ClosestExit(mazeFrom, approach, mazeIncoming);
            mazeMotion = ForestMazeMotion.Choosing;
            mazeSpeed = 0;
            mazeTurnAge = mazeTurnDuration;
            float nearest = ForestMaze.Heading(mazeFrom, mazeDefaultExit);
            double angle = Math.Abs(ForestMaze.DeltaAngle(mazeYaw, nearest)) * Math.PI / 180;
            if (angle >= Math.PI / 2 || ForestMaze.RoomRadius * Math.Sin(angle) >= ForestMaze.PassageHalf)
                ArmMazeTurn(nearest);
            Message = "岔口 · A 左，B 右。长按可原路回头。";
        }

        private void StartMazeEdge(int next)
        {
            mazeTarget = next;
            mazeAlong = 0;
            mazeMotion = BeginPassageTurn() ? ForestMazeMotion.FacingPassage : ForestMazeMotion.Walking;
            Message = "";
        }

        private void SetMazeEdge(int next)
        {
            mazeTarget = next;
            mazeAlong = 0;
            ClearMazeChoice();
        }

        private void ClearMazeChoice()
        {
            ForestMazeLeft = ForestMazeRight = mazeDefaultExit = -1;
            mazeChoiceAge = 0;
        }

        private bool BeginPassageTurn()
        {
            float heading = ForestMaze.Heading(mazeFrom, mazeTarget);
            float angle = Math.Abs(ForestMaze.DeltaAngle(mazeYaw, heading));
            if (angle < .001f)
            {
                mazeYaw = heading;
                return false;
            }
            ArmMazeTurn(heading, .36f + .70f * angle / 180f);
            return true;
        }

        private bool TouchMaze(int option)
        {
            if (!ForestMazeActive || AtMazeRoots) return false;
            int next = option == 0 ? ForestMazeLeft : ForestMazeRight;
            // Both buttons keep their room-relative destination until the character moves.
            if (next >= 0 && next != mazeTarget) StartMazeEdge(next);
            return true;
        }

        public bool TurnAroundForest()
        {
            if (!ForestCanTurnAround) return false;
            if (mazeTarget>=0 && mazeAlong>.005f)
            {
                float length=EdgeLength(ForestMaze.Nodes[mazeFrom],ForestMaze.Nodes[mazeTarget]);
                int back=mazeFrom;
                mazeFrom=mazeTarget;
                mazeTarget=back;
                mazeAlong=length-mazeAlong;
            }
            else SetMazeEdge(mazeIncoming);
            ArmMazeTurn(ForestMaze.Heading(mazeFrom, mazeTarget));
            mazeReturnIndex=-1;
            mazeMotion=ForestMazeMotion.TurningBack;
            Change(Stage.ForestLoop,"");
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
                    SetMazeEdge(ForestMaze.ClosestExit(mazeFrom, mazeYaw, -1));
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
                    mazeSpeed=MoveTowards(mazeSpeed,MazeBacktrackSpeed*mazeWalkScale,4.7f*dt);
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
                        SetMazeEdge(mazePath[--mazeReturnIndex]);
                        if (BeginPassageTurn()) mazeMotion = ForestMazeMotion.TurningBack;
                        return;
                    }
                    mazeTarget = -1;
                    mazeAlong = mazeSpeed = 0;
                    mazePath.RemoveRange(mazeReturnStop + 1, mazePath.Count - mazeReturnStop - 1);
                    mazeIncoming = previous;
                    Change(Stage.ForestExplore, "");
                    OfferMazeExits();
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
