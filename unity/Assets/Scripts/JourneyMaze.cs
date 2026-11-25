using System;
using System.Collections.Generic;

namespace Echoes
{
    public sealed partial class Journey
    {
        private const float MazeEntranceDepth = 124;
        private const float MazeTurnSeconds = .96f;
        private const float MazeBacktrackSpeed = 2.28f;
        private readonly List<int> mazePath = new List<int>();
        private int mazeFrom, mazeTarget = -1, mazeIncoming = -1, mazeReturnIndex, mazeReturnStop;
        private float mazeAlong, mazeYaw, mazeTurnFrom, mazeTurnTo, mazeTurnAge;
        private float mazeX, mazeY, mazeZ, mazeSpeed, mazeTravel;
        private ForestMazeMotion mazeMotion;

        public ForestMaze ForestMaze { get; private set; }
        public bool ForestMazeActive => InForest && Branch == ForestBranch.Left && Distance >= MazeEntranceDepth && ForestMaze != null;
        public float ForestMazeX => mazeX + (AtMazeRoots ? ForestLane * 2.1f : 0);
        public float ForestMazeY => mazeY;
        public float ForestMazeZ => mazeZ;
        public float ForestMazeHeading => mazeYaw;
        public float ForestMazeSpeed => mazeSpeed;
        public float ForestMazeTravel => mazeTravel;
        public ForestMazeMotion MazeMotion => mazeMotion;
        public int ForestMazeNode => mazeFrom;
        public int ForestMazeTarget => mazeTarget;
        public int ForestMazeLeft { get; private set; } = -1;
        public int ForestMazeRight { get; private set; } = -1;
        public bool ForestCanTurnAround => Started && Gesture != Hand.Missing && ForestMazeActive &&
            Phase == Stage.ForestExplore && (mazeTarget >= 0 || mazeIncoming >= 0);

        private bool AtMazeRoots => ForestMazeActive && Distance >= ClearingDepth;

        private void InitializeMaze()
        {
            mazePath.Clear();
            mazeTarget = mazeIncoming = -1;
            ForestMazeLeft = ForestMazeRight = -1;
            mazeFrom = 0;
            mazeX = mazeY = mazeZ = mazeAlong = mazeYaw = mazeTurnAge = mazeSpeed = mazeTravel = 0;
            mazeMotion = ForestMazeMotion.Walking;
            if (ForestMaze == null) return;
            if (Distance >= ClearingDepth) mazeFrom = ForestMaze.RootChamber;
            mazePath.Add(mazeFrom);
            SetMazePosition(ForestMaze.Nodes[mazeFrom]);
            if (mazeFrom == ForestMaze.RootChamber)
            {
                ForestMazeEdge entry = ForestMaze.Connections(mazeFrom)[0];
                mazeYaw = Heading(ForestMaze.Nodes[entry.Other(mazeFrom)], ForestMaze.Nodes[mazeFrom]);
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
            Message = "根渠迷宫 · 轻声前进，停声驻足。岔口 A 左、B 右，长按可回头。";
        }

        private void AdvancePhysicalMaze(float dt)
        {
            if (AtMazeRoots) return;
            if (mazeTarget < 0)
            {
                mazeSpeed = 0;
                if (mazeMotion == ForestMazeMotion.Choosing) return;
                IReadOnlyList<ForestMazeEdge> exits = ForestMaze.Connections(mazeFrom);
                if (mazeIncoming < 0 && exits.Count == 1)
                    StartMazeEdge(exits[0].Other(mazeFrom));
                else
                    OfferMazeExits();
                return;
            }

            float angle = Heading(ForestMaze.Nodes[mazeFrom],ForestMaze.Nodes[mazeTarget]);
            float travel = Gesture == Hand.Fist ? 0 : ForestAirborne ? .92f + Voice * 2.48f :
                Voice < .08f ? 0 : .75f + Voice * 2.65f;
            float turn = DeltaAngle(mazeYaw,angle);
            mazeYaw += Math.Sign(turn) * Math.Min(Math.Abs(turn), dt*155);
            if (Math.Abs(turn)>54) travel = 0;
            mazeSpeed = MoveTowards(mazeSpeed,travel,dt*(travel < mazeSpeed ? 9.5f : 5.1f));
            if (travel == 0 && mazeSpeed < .035f) return;
            if (TraverseMazeEdge(mazeSpeed * dt)) ReachMazeNode();
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
            if (ForestMaze.Nodes[mazeFrom].Kind==ForestMazeNodeKind.DeadEnd)
            {
                BeginDeadEnd();
                return;
            }
            OfferMazeExits();
        }

        private void OfferMazeExits()
        {
            int first=-1, second=-1;
            foreach (ForestMazeEdge edge in ForestMaze.Connections(mazeFrom))
            {
                int next=edge.Other(mazeFrom);
                if (next==mazeIncoming) continue;
                if (first<0) first=next;
                else second=next;
            }
            if (second<0)
            {
                if (first>=0) StartMazeEdge(first);
                else
                {
                    mazeMotion=ForestMazeMotion.Choosing;
                    Message="长按 A 或 B 回头。";
                }
                return;
            }

            float angleA=DeltaAngle(mazeYaw,Heading(ForestMaze.Nodes[mazeFrom],ForestMaze.Nodes[first]));
            float angleB=DeltaAngle(mazeYaw,Heading(ForestMaze.Nodes[mazeFrom],ForestMaze.Nodes[second]));
            ForestMazeLeft=angleA<angleB ? first : second;
            ForestMazeRight=angleA<angleB ? second : first;
            mazeMotion=ForestMazeMotion.Choosing;
            mazeSpeed=0;
            Message="岔口 · A 左，B 右。长按可原路回头。";
        }

        private void StartMazeEdge(int next)
        {
            mazeTarget=next;
            mazeAlong=0;
            ForestMazeLeft=ForestMazeRight=-1;
            mazeMotion=ForestMazeMotion.Walking;
            Message="";
        }

        private bool TouchMaze(int option)
        {
            if (!ForestMazeActive || AtMazeRoots) return false;
            if (mazeMotion != ForestMazeMotion.Choosing) return true;
            int next=option==0 ? ForestMazeLeft : ForestMazeRight;
            if (next>=0) StartMazeEdge(next);
            return true;
        }

        public bool TurnAroundForest()
        {
            if (!ForestCanTurnAround) return false;
            int back=mazeTarget>=0 ? mazeFrom : mazeIncoming;
            if (mazeTarget>=0)
            {
                float length=EdgeLength(ForestMaze.Nodes[mazeFrom],ForestMaze.Nodes[mazeTarget]);
                mazeFrom=mazeTarget;
                mazeTarget=back;
                mazeAlong=length-mazeAlong;
            }
            else StartMazeEdge(back);
            ArmMazeTurn(Heading(ForestMaze.Nodes[mazeFrom],ForestMaze.Nodes[mazeTarget]));
            mazeReturnIndex=-1;
            mazeMotion=ForestMazeMotion.TurningBack;
            Change(Stage.ForestLoop,"");
            return true;
        }

        private void BeginDeadEnd()
        {
            mazeReturnIndex=mazePath.Count-2;
            if (mazeReturnIndex<0) { OfferMazeExits(); return; }
            mazeReturnStop=mazeReturnIndex;
            while (mazeReturnStop>0 && ForestMaze.Nodes[mazePath[mazeReturnStop]].Kind!=ForestMazeNodeKind.Junction)
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
                    mazeTarget=mazePath[mazeReturnIndex];
                    mazeAlong=0;
                    ArmMazeTurn(Heading(ForestMaze.Nodes[mazeFrom],ForestMaze.Nodes[mazeTarget]));
                    mazeMotion=ForestMazeMotion.TurningBack;
                    return;
                case ForestMazeMotion.TurningBack:
                    if (!StepMazeTurn(dt)) return;
                    if (mazeReturnIndex<0)
                    {
                        mazeMotion=ForestMazeMotion.Walking;
                        Change(Stage.ForestExplore,"");
                        return;
                    }
                    mazeMotion=ForestMazeMotion.Retracing;
                    return;
                case ForestMazeMotion.Retracing:
                    float angle=Heading(ForestMaze.Nodes[mazeFrom],ForestMaze.Nodes[mazeTarget]);
                    float turn=DeltaAngle(mazeYaw,angle);
                    mazeYaw+=Math.Sign(turn)*Math.Min(Math.Abs(turn),dt*125);
                    mazeSpeed=MoveTowards(mazeSpeed,Math.Abs(turn)>42 ? 0 : MazeBacktrackSpeed,4.7f*dt);
                    if (!TraverseMazeEdge(mazeSpeed*dt)) return;
                    mazeFrom=mazeTarget;
                    if (mazeReturnIndex>mazeReturnStop)
                    {
                        mazeTarget=mazePath[--mazeReturnIndex];
                        mazeAlong=0;
                        return;
                    }
                    mazeTarget=-1;
                    mazeAlong=mazeSpeed=0;
                    mazePath.RemoveRange(mazeReturnStop+1,mazePath.Count-mazeReturnStop-1);
                    mazeIncoming=mazeReturnStop>0 ? mazePath[mazeReturnStop-1] : -1;
                    float outward=mazeIncoming>=0 ? Heading(ForestMaze.Nodes[mazeIncoming],ForestMaze.Nodes[mazeFrom]) :
                        Heading(ForestMaze.Nodes[mazeFrom],ForestMaze.Nodes[ForestMaze.Connections(mazeFrom)[0].Other(mazeFrom)]);
                    ArmMazeTurn(outward);
                    mazeMotion=ForestMazeMotion.FacingJunction;
                    return;
                case ForestMazeMotion.FacingJunction:
                    if (!StepMazeTurn(dt)) return;
                    Change(Stage.ForestExplore,"");
                    OfferMazeExits();
                    return;
            }
        }

        private void ArmMazeTurn(float heading)
        {
            mazeTurnFrom=mazeYaw;
            mazeTurnTo=mazeYaw+DeltaAngle(mazeYaw,heading);
            mazeTurnAge=mazeSpeed=0;
        }

        private bool StepMazeTurn(float dt)
        {
            mazeTurnAge+=dt;
            mazeYaw=Lerp(mazeTurnFrom,mazeTurnTo,Smooth(mazeTurnAge/MazeTurnSeconds));
            return mazeTurnAge>=MazeTurnSeconds;
        }

        private void SetMazePosition(ForestMazeNode node)
        {
            mazeX=node.X;
            mazeY=node.Y;
            mazeZ=node.Z;
        }

        private static float EdgeLength(ForestMazeNode a,ForestMazeNode b) =>
            (float)Math.Sqrt(ForestMazeEdge.Square(a.X-b.X)+ForestMazeEdge.Square(a.Y-b.Y)+ForestMazeEdge.Square(a.Z-b.Z));

        private static float Heading(ForestMazeNode a,ForestMazeNode b) =>
            (float)(Math.Atan2(b.X-a.X,b.Z-a.Z)*180/Math.PI);

        private static float DeltaAngle(float current,float target)
        {
            float delta=(target-current+180)%360;
            if (delta<0) delta+=360;
            return delta-180;
        }

        private static float Lerp(float a,float b,float amount) => a+(b-a)*amount;

        private static float MoveTowards(float value,float target,float delta) =>
            value+Math.Sign(target-value)*Math.Min(Math.Abs(target-value),delta);
    }
}
