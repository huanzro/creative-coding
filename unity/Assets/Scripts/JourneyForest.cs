using System;
using System.Collections.Generic;

namespace Echoes
{
    public enum ForestFeatureKind { Amber, Scarlet, Web, Maw, Plant }
    public enum ForestTrapKind { None, Web, Maw, Cable }
    public enum ForestBranch { Shared, Left, Right }
    public enum ForestSection
    {
        Trail, Fork, DoubleRoots, EchoWell, BrokenBridges, Canopy, Clearing,
        MachineApproach, Vestibule, Brain, Shaft, Heart
    }

    public sealed class ForestFeature
    {
        public int Id { get; }
        public ForestFeatureKind Kind { get; }
        public float Lane { get; }
        public float Depth { get; }
        public float Altitude { get; }
        public bool Collected { get; internal set; }

        internal ForestFeature(int id, ForestFeatureKind kind, float lane, float depth, float altitude)
        {
            Id = id;
            Kind = kind;
            Lane = lane;
            Depth = depth;
            Altitude = altitude;
        }

        private float Phase(float time) => time * (Kind == ForestFeatureKind.Scarlet ? 3.85f : 1.7f) + Id * 2.399f;
        private bool IsFly => Kind == ForestFeatureKind.Amber || Kind == ForestFeatureKind.Scarlet;
        public float LaneAt(float time)
        {
            if (!IsFly) return Lane;
            double sway = Math.Sin(Phase(time));
            return Lane + (Kind == ForestFeatureKind.Scarlet ? .087f * (float)Math.Asin(sway) : .10f * (float)sway);
        }
        public float AltitudeAt(float time) => Altitude + (IsFly ? .043f * (float)Math.Cos(Phase(time) * 1.23f) : 0);
        public float DepthAt(float time) => Depth + (IsFly ? .31f * (float)Math.Sin(Phase(time) * .79f) : 0);

        public bool InMaze { get; internal set; }
        public float WorldX { get; internal set; }
        public float WorldY { get; internal set; }
        public float WorldZ { get; internal set; }

    }

    public sealed partial class Journey
    {
        private const int StartingLights = 3;
        private const float PlantTime = .80f;
        public const float ForestRescueDuration = 9.4f;
        public const float ForestRescueSpendAt = 6.8f;
        private const float BranchDepth = 112;
        internal const float ForkEntranceDepth = 103, ForkSplitDepth = BranchDepth, ForkBendEnd = MazeEntranceDepth;
        internal const float ForkLaneWidth = 2.1f, ForkSplitOffset = 1.54f, ForestMachineCenter = 11f;
        private const float ForestLateralRate = 1.35f, ForestTapTime = .18f;
        private const float TrapBaseRadius = 1.05f;
        private const float MazeEntranceCenter = -13.1f;
        private const float ClearingDepth = 188;
        private const float VestibuleDepth = 168;
        internal const float MachineDoorDepth = 179, MachineShaftDepth = 205, MachineHeartDepth = 275;
        private const float DoorDepth = MachineDoorDepth, ShaftDepth = MachineShaftDepth, HeartDepth = MachineHeartDepth;
        private const int PreviousHeartDepth = 208;
        public const float ForestTransitionDuration = 5.2f;
        public const float ForestDoorDuration = 13.4f;
        public const float ForestMachineUpperFloor = 1.46f;
        public float ForestTransitionTime => ForestTrap == ForestTrapKind.Cable ? 8.8f : ForestTransitionDuration;
        public const float ForestFallDuration = 3.15f;

        private readonly Random forestRandom;
        private readonly List<ForestFeature> forestFeatures = new List<ForestFeature>();
        private readonly ForestFeature[] forestSites = new ForestFeature[2];
        private ForestMaze forestForkMaze;
        private ForestFeature forestRenewal;
        private int forestSeed, nextForestFeatureId, savedForestLights, savedForestLane;
        private int forestPlantMask, savedForestPlants, savedForestDepth;
        private ForestBranch savedForestBranch;
        private float forestPlantTime, forestContactAt = float.NegativeInfinity;
        private float forestLaneSpeed, forestProtectedUntil, forestShaftHeight;
        private int forestLateralInput, forestLateralTap;
        private bool forestRescueDeparted;
        private int forestDoorLights;
        private Stage forestResumePhase;
        private readonly float[] forestEchoTimes = new float[64], forestEchoLanes = new float[64];
        private readonly Hand[] forestEchoHands = new Hand[64];
        private int forestEchoCursor, forestEchoCount;
        public const float MinForestLateralScale = .35f, DefaultForestLateralScale = .70f, MaxForestLateralScale = 1.45f;
        public const float MinForestCaptureRange = .45f, DefaultForestCaptureRange = 1, MaxForestCaptureRange = 1.65f;
        public const float MinForestDoorJumpHeightScale = .75f, DefaultForestDoorJumpHeightScale = 2.60f, MaxForestDoorJumpHeightScale = 4f;
        private float forestLateralScale = DefaultForestLateralScale;
        private float forestWebRange = DefaultForestCaptureRange, forestMawRange = DefaultForestCaptureRange;
        private float forestDoorJumpHeightScale = DefaultForestDoorJumpHeightScale;

        public float ForestDoorJumpHeightScale
        {
            get => forestDoorJumpHeightScale;
            set => forestDoorJumpHeightScale = TuningValue(value, MinForestDoorJumpHeightScale,
                MaxForestDoorJumpHeightScale, DefaultForestDoorJumpHeightScale);
        }

        public float ForestLateralScale
        {
            get => forestLateralScale;
            set => forestLateralScale = TuningValue(value, MinForestLateralScale, MaxForestLateralScale, DefaultForestLateralScale);
        }

        public float ForestWebRange
        {
            get => forestWebRange;
            set => forestWebRange = TuningValue(value, MinForestCaptureRange, MaxForestCaptureRange, DefaultForestCaptureRange);
        }

        public float ForestMawRange
        {
            get => forestMawRange;
            set => forestMawRange = TuningValue(value, MinForestCaptureRange, MaxForestCaptureRange, DefaultForestCaptureRange);
        }

        private static float TuningValue(float value, float min, float max, float fallback) =>
            float.IsNaN(value) ? fallback : Math.Max(min, Math.Min(max, value));

        public bool InForest => Chapter == 0 && Lesson < 0 && !IsOpening;
        private bool IsForestSequence => Phase == Stage.ForestTrapped || Phase == Stage.ForestTransition ||
            Phase == Stage.ForestFalling || Phase == Stage.ForestDoor || Phase == Stage.ForestLoop;
        public bool ForestAirborne => InForest && (Flight.Height > ForestFlightFloor + .002f || Flight.Speed > .02f);
        public float ForestLane { get; private set; }
        public float ForestTargetLane { get; private set; }
        public float ForestSteering { get; private set; }
        public int ForestLights { get; private set; }
        public ForestBranch Branch { get; private set; }
        public int ForestDoor { get; private set; } = -1;
        public float ForestCenter => ForestCenterAt(Distance);
        internal ForestMaze ForestForkMaze => forestForkMaze?.Seed == forestSeed ? forestForkMaze :
            forestForkMaze = new ForestMaze(forestSeed);
        public float ForestFlightSpeed => !InForest || IsForestSequence || Phase == Stage.ForestPlanting ? 0 :
            ForestMazeActive ? ForestMazeSpeed : Section == ForestSection.Shaft ? 5.1f + 2.65f * Voice + ForestShaftProgress * .85f :
            LeftMazeApproach ? Lerp(ForestApproachPace, MazeWalkingPace, ForestMazeApproachBlend) :
            ForestApproachPace;
        private float ForestApproachPace => ForestAirborne ? 4.35f + 4.15f * Voice :
            (Distance >= ForestForkPromptDepth && Distance < VestibuleDepth || Section == ForestSection.Brain) &&
            Voice >= .08f && Gesture != Hand.Fist ? .9f + 2.5f * Voice : 0;
        private const float ForestForkPromptDepth = 100;
        public bool LeftMazeApproach => InForest && Branch == ForestBranch.Left && !ForestMazeActive;
        public float ForestMazeApproachBlend => Branch == ForestBranch.Left ? Smooth((Distance - 115) / (MazeEntranceDepth - 115)) : 0;
        public float ForestShaftProgress => Branch==ForestBranch.Right ? Clamp((Distance-ShaftDepth)/(HeartDepth-ShaftDepth)) : 0;
        public float ForestHeightLimit => ForestMazeActive ? .69f :
            LeftMazeApproach ? Lerp(1, .69f, ForestMazeApproachBlend) :
            Branch == ForestBranch.Right && Distance >= VestibuleDepth && Distance < ShaftDepth ? 1.72f : 1;
        public float ForestFlightFloor => InForest && Section == ForestSection.Brain ? ForestMachineUpperFloor : 0;
        public int ForestPlantsRequired => Branch == ForestBranch.Right ? 1 : 2;
        public IReadOnlyList<ForestFeature> ForestFeatures => forestFeatures;
        public ForestFeature ForestContact { get; private set; }
        public float ForestContactAge => PoseTime - forestContactAt;
        public float ForestContactLane { get; private set; }
        public float ForestContactDepth { get; private set; }
        public float ForestContactAltitude { get; private set; }
        public ForestTrapKind ForestTrap { get; private set; }
        public float ForestTrapAltitude { get; private set; }
        public float ForestTrapDepth { get; private set; }
        public float ForestTrapLane { get; private set; }
        public int ForestCableAnchor { get; private set; }
        public bool ForestHasRescue { get; private set; }
        public int ForestThreatPulse { get; private set; }
        public ForestFeatureKind ForestThreatKind { get; private set; }
        public float ForestPlantCharge => Clamp(forestPlantTime / PlantTime);
        public int ForestPlantTargetId { get; private set; } = -1;
        public float ForestWindFactor => !ForestAirborne ? 0 :
            Clamp((Flight.Height - .24f) / .66f) * (IsForestSequence ? .35f : 1f);
        public float GustWarning => ForestWindFactor;
        public ForestSection Section => SectionAt(Distance);

        public ForestSection SectionAt(float depth)
        {
            if (Branch == ForestBranch.Shared) return depth >= 100 ? ForestSection.Fork : ForestSection.Trail;
            if (Branch == ForestBranch.Left)
            {
                if (depth < 130) return ForestSection.DoubleRoots;
                if (depth < 144) return ForestSection.EchoWell;
                if (depth < 159) return ForestSection.BrokenBridges;
                if (depth < 174) return ForestSection.Canopy;
                return ForestSection.Clearing;
            }
            if (depth < VestibuleDepth) return ForestSection.MachineApproach;
            if (depth < DoorDepth) return ForestSection.Vestibule;
            if (depth < ShaftDepth) return ForestSection.Brain;
            return depth < HeartDepth ? ForestSection.Shaft : ForestSection.Heart;
        }

        public float ForestCenterAt(float depth)
        {
            return Branch == ForestBranch.Shared ? 0 : ForkTrackX(Branch, depth);
        }

        internal static float ForkTrackX(ForestBranch branch, float depth)
        {
            float side = branch == ForestBranch.Left ? -1 : 1;
            if (depth <= BranchDepth)
                return side*ForkSplitOffset*Smooth((depth-ForkEntranceDepth)/(BranchDepth-ForkEntranceDepth));
            float end = branch == ForestBranch.Left ? MazeEntranceCenter : ForestMachineCenter;
            return Lerp(side*ForkSplitOffset,end,ForkBend(depth));
        }

        private static float ForkBend(float depth)
        {
            float t = Clamp((depth-BranchDepth)/(ForkBendEnd-BranchDepth));
            return t*t*t*(10+t*(-15+6*t));
        }

        internal float ForestPositionX(float lane, float depth)
        {
            float laneX = lane*ForkLaneWidth;
            if (Branch == ForestBranch.Shared || depth <= BranchDepth) return laneX;
            float blend = ForkBend(depth);
            float track = ForestCenterAt(depth);
            return Branch == ForestBranch.Left ? track+(laneX+ForkSplitOffset)*(1-blend) :
                track+laneX-ForkSplitOffset*(1-blend);
        }

        internal float ForestPathGrade(float lane, float depth) =>
            (ForestPositionX(lane,depth+.1f)-ForestPositionX(lane,depth-.1f))/.2f;

        public float ForestLoopOffset => 0;

        private static float Smooth(float value)
        {
            float t = Clamp(value);
            return t * t * (3 - 2 * t);
        }

        public float ForestEchoLane(float ago) => forestEchoCount == 0 ? ForestLane : forestEchoLanes[EchoIndex(ago)];
        public Hand ForestEchoHand(float ago) => forestEchoCount == 0 ? Gesture : forestEchoHands[EchoIndex(ago)];

        private int EchoIndex(float ago)
        {
            int index = forestEchoCursor;
            for (int i = 0; i < forestEchoCount - 1 && forestEchoTimes[index] > Time - ago; i++)
                index = (index + forestEchoTimes.Length - 1) % forestEchoTimes.Length;
            return index;
        }

        private void RecordForestEcho()
        {
            if (forestEchoCount > 0 && Time - forestEchoTimes[forestEchoCursor] < .058f) return;
            forestEchoCursor = (forestEchoCursor + 1) % forestEchoTimes.Length;
            forestEchoCount = Math.Min(forestEchoCount + 1, forestEchoTimes.Length);
            forestEchoTimes[forestEchoCursor] = Time;
            forestEchoLanes[forestEchoCursor] = ForestLane;
            forestEchoHands[forestEchoCursor] = Gesture;
        }

        private void ResetForestHistory()
        {
            forestSeed = forestRandom.Next();
            forestFeatures.Clear();
            savedForestLights = StartingLights;
            savedForestLane = savedForestPlants = savedForestDepth = 0;
            savedForestBranch = ForestBranch.Shared;
            Branch = ForestBranch.Shared;
        }

        private static bool IsForestCheckpointValid(JourneyCheckpoint checkpoint)
        {
            if (checkpoint.forestBranch < 0 || checkpoint.forestBranch > 2 ||
                checkpoint.forestSites < 0 || checkpoint.forestSites > 3) return false;
            if (checkpoint.chapter != 0 || checkpoint.lesson >= 0) return true;

            int depth = checkpoint.forestDepth, sites = checkpoint.forestSites, route = checkpoint.forestRoute;
            if (checkpoint.forestBranch == 0)
                return depth == 0 && sites == 0 && route == 0 && checkpoint.forestReserve > 0;
            if (checkpoint.forestBranch == 1)
            {
                if (sites == 3)
                    return depth == ClearingDepth && (route == 1 || route == 2);
                return route == 0 && checkpoint.forestReserve > 0 &&
                    (depth == ClearingDepth || depth == BranchDepth && sites == 0);
            }
            if (sites == 1) return (depth == HeartDepth || depth == PreviousHeartDepth) && route == 3;
            return sites == 0 && route == 0 && checkpoint.forestReserve > 0 &&
                (depth == BranchDepth || depth == VestibuleDepth || depth == HeartDepth || depth == PreviousHeartDepth);
        }

        private ForestFeature AddForestFeature(ForestFeatureKind kind, float lane, float depth, float altitude)
        {
            var feature = new ForestFeature(++nextForestFeatureId, kind, lane, depth, altitude);
            forestFeatures.Add(feature);
            return feature;
        }

        private static float Jitter(Random random, float radius) => (float)(random.NextDouble() * 2 - 1) * radius;

        private void AddForestRun(Random random, float start, float end, int offset = 0)
        {
            int wave = 0;
            for (float depth = start; depth + 6 < end; depth += 11.4f)
            {
                float warmLane = Jitter(random, .87f), threatLane = warmLane > 0 ? warmLane - .92f : warmLane + .92f;
                AddForestFeature(ForestFeatureKind.Amber, warmLane, depth + Jitter(random, .7f),
                    .20f + (float)random.NextDouble() * .45f);
                AddForestFeature(ForestFeatureKind.Scarlet, threatLane, depth + 4.2f + Jitter(random, .55f),
                    .22f + (float)random.NextDouble() * .46f);
                bool canopy = (wave++ + offset) % 2 == 0;
                AddForestFeature(canopy ? ForestFeatureKind.Web : ForestFeatureKind.Maw, Jitter(random, .85f),
                    depth + 7.2f + Jitter(random, .72f), canopy ? .91f : .063f);
            }
        }

        private void BuildForest()
        {
            forestFeatures.Clear();
            forestRenewal = null;
            ForestMaze = null;
            forestSites[0] = forestSites[1] = null;
            nextForestFeatureId = (int)Branch * 1000;
            var random = new Random(forestSeed);
            if (Branch == ForestBranch.Shared)
            {
                AddForestFeature(ForestFeatureKind.Amber, 0, 3.55f, .42f);
                AddForestFeature(ForestFeatureKind.Amber, -1, 9, .15f);
                AddForestFeature(ForestFeatureKind.Web, -.26f, 20f, .89f);
                AddForestFeature(ForestFeatureKind.Maw, .31f, 36f, .06f);
                AddForestFeature(ForestFeatureKind.Scarlet, 1, 25f, .43f);
                AddForestRun(random, 43, 98);
            }
            else if (Branch == ForestBranch.Left)
            {
                PopulateMaze(random);
            }
            else
            {
                AddForestRun(random, 117, 159, 1);
                AddForestFeature(ForestFeatureKind.Amber, 0, 164.2f, .37f);
                AddForestFeature(ForestFeatureKind.Amber, -1, 188.8f, ForestMachineUpperFloor + .08f);
                forestSites[0] = AddForestFeature(ForestFeatureKind.Plant, 0, HeartDepth, .05f);
            }
            for (int i = 0; i < forestSites.Length; i++)
                if (forestSites[i] != null) forestSites[i].Collected = (forestPlantMask & (1 << i)) != 0;
        }

        private void StartForest()
        {
            Branch = savedForestBranch;
            if (Branch == ForestBranch.Right && savedForestDepth == PreviousHeartDepth)
                savedForestDepth = (int)HeartDepth;
            forestPlantMask = savedForestPlants;
            Plants = ((forestPlantMask & 1) != 0 ? 1 : 0) + ((forestPlantMask & 2) != 0 ? 1 : 0);
            BuildForest();
            Distance = savedForestDepth;
            ForestLights = savedForestLights;
            ForestLane = ForestTargetLane = savedForestLane;
            if (Branch != ForestBranch.Shared && Distance == BranchDepth)
            {
                ForestLane = ForkTrackX(Branch,BranchDepth)/ForkLaneWidth;
                ForestTargetLane = 0;
            }
            ForestSteering = forestLaneSpeed = forestPlantTime = forestProtectedUntil = 0;
            ForestTrap = ForestTrapKind.None;
            ForestHasRescue = forestRescueDeparted = false;
            ForestDoor = ForestPlantTargetId = -1;
            ForestContact = null;
            forestContactAt = float.NegativeInfinity;
            forestEchoCount = forestEchoCursor = 0;
            forestLateralInput = forestLateralTap = 0;
            Flight.Reset(0);
            InitializeMaze();
            if (Plants > 0 && Plants == ForestPlantsRequired && ForestRoute != 0)
            {
                ForestFeature site = ForestRoute == 2 ? forestSites[1] : forestSites[0];
                ForestLane = ForestTargetLane = site.Lane;
                Flight.Reset(site.Altitude);
                ForestTrapKind ending = ForestRoute == 2 ? ForestTrapKind.Web :
                    ForestRoute == 3 ? ForestTrapKind.Cable : ForestTrapKind.Maw;
                BeginForestTransition(ending, site);
            }
            else if (Distance == ClearingDepth && Branch == ForestBranch.Left ||
                Distance == HeartDepth && Branch == ForestBranch.Right)
                EnterForestPlanting();
            else
                Change(Stage.ForestExplore, Branch == ForestBranch.Shared
                    ? "握拳后张手起飞。A 左、B 右，发声加速。带着三只黄萤飞向森林深处。"
                    : "");
        }

        private void SaveForestSpot(int depth)
        {
            savedForestBranch = Branch;
            savedForestDepth = depth;
            savedForestLane = depth == BranchDepth ? (Branch == ForestBranch.Left ? -1 : 1) : (int)Math.Round(ForestLane);
            savedForestLights = ForestLights;
            savedForestPlants = forestPlantMask;
            Revision++;
        }

        private void ForestTouch(int option)
        {
            if (TouchMaze(option) || Branch==ForestBranch.Left && !ForestMazeActive) return;
            forestLateralTap = option == 0 ? -1 : 1;
        }

        public void SetForestLateralInput(bool left, bool right)
        {
            forestLateralInput = (right ? 1 : 0) - (left ? 1 : 0);
            if (left && right) forestLateralTap = 0;
        }

        private void StepForestLateral(float dt)
        {
            // Move the destination by held time. The character settles at that continuous position on release.
            if (!LeftMazeApproach)
            {
                float input = forestLateralInput == 0 ? forestLateralTap * ForestTapTime : forestLateralInput * dt;
                ForestTargetLane = Math.Max(-1, Math.Min(1, ForestTargetLane + input * ForestLateralRate * ForestLateralScale));
            }
            forestLateralTap = 0;
            float speed = 6.9f * ForestLateralScale;
            float offset = ForestLane - ForestTargetLane;
            float decay = (float)Math.Exp(-speed * dt);
            float motion = (forestLaneSpeed + speed * offset) * dt;
            ForestLane = ForestTargetLane + (offset + motion) * decay;
            forestLaneSpeed = (forestLaneSpeed - speed * motion) * decay;
            ForestLane = Math.Max(-1, Math.Min(1, ForestLane));
            if (Math.Abs(ForestLane - ForestTargetLane) < .002f && Math.Abs(forestLaneSpeed) < .025f)
            {
                ForestLane = ForestTargetLane;
                forestLaneSpeed = 0;
            }
            ForestSteering = Math.Max(-1, Math.Min(1, forestLaneSpeed / 2.55f));
        }

        private void UpdateForest(float dt)
        {
            switch (Phase)
            {
                case Stage.ForestTransition:
                    Progress = Clamp(Age / ForestTransitionTime);
                    if (Progress >= 1) Enter(1, false, false);
                    return;
                case Stage.ForestTrapped:
                    UpdateForestCapture();
                    return;
                case Stage.ForestFalling:
                    Flight.FallForest(dt);
                    Progress = Clamp(Age / ForestFallDuration);
                    if (Progress >= 1)
                    {
                        Enter(0, false, true);
                        Notify(JourneyCue.ForestRestart);
                    }
                    return;
                case Stage.ForestDoor:
                    UpdateForestDoor();
                    return;
                case Stage.ForestLoop:
                    AdvanceMazeTurn(dt);
                    return;
            }
            if (!ForestMazeActive || AtMazeRoots) StepForestLateral(dt);
            RecordForestEcho();
            if (ForestMazeActive) AdvancePhysicalMaze(dt);
            else AdvanceForestFlight(dt);
            if (IsForestSequence) return;
            CollectForestContacts();
            if (Phase == Stage.ForestPlanting) PlantForestLight(dt);
        }

        private void AdvanceForestFlight(float dt)
        {
            if (Phase == Stage.ForestPlanting) return;
            float grade = ForestPathGrade(ForestLane, Distance);
            float speed = ForestFlightSpeed / (float)Math.Sqrt(1+grade*grade);
            if (Branch == ForestBranch.Shared && Math.Abs(ForestTargetLane) < .42f && Math.Abs(ForestLane) < .42f)
                speed *= 1-.70f*Smooth((Distance-108)/(BranchDepth-108));
            float next = Distance + speed * dt;
            if (Branch == ForestBranch.Shared)
            {
                if (Distance < ForestForkPromptDepth && next >= ForestForkPromptDepth) Message = "林路前方分成两道。";
                if (next < BranchDepth) { Distance = next; return; }
                if (Math.Abs(ForestLane) < .42f) { Distance = BranchDepth - .01f; return; }
                Branch = ForestLane < 0 ? ForestBranch.Left : ForestBranch.Right;
                ForestTargetLane=0;
                forestLateralTap=0;
                Distance = next;
                BuildForest();
                SaveForestSpot((int)BranchDepth);
                Message = "";
                return;
            }
            if (Branch == ForestBranch.Left)
            {
                if(next>=MazeEntranceDepth) BeginMazeEntrance();
                else Distance=next;
                return;
            }
            if (Distance < VestibuleDepth && next >= VestibuleDepth)
            {
                Distance = VestibuleDepth;
                SaveForestSpot((int)VestibuleDepth);
                Message = "";
                return;
            }
            if (Distance < DoorDepth && next >= DoorDepth)
            {
                Distance = DoorDepth - .01f;
                if (Flight.Height >= 1.52f)
                {
                    Distance = DoorDepth;
                    Message = "光脉在前方逐段亮起。";
                }
                else if (Flight.Height < .90f)
                {
                    ForestDoor = ForestLane < -.42f ? 0 : ForestLane > .42f ? 2 : 1;
                    forestDoorLights = ForestLights;
                    ForestSteering = 0;
                    Change(Stage.ForestDoor, "");
                    Notify(JourneyCue.ForestDoor);
                }
                return;
            }
            if (Distance < ShaftDepth && next >= ShaftDepth)
            {
                Distance = ShaftDepth;
                forestShaftHeight = Flight.Height;
                return;
            }
            if (Section == ForestSection.Shaft)
            {
                Distance = Math.Min(HeartDepth, next);
                Flight.FollowForestCourse(forestShaftHeight * (1 - Smooth(ForestShaftProgress)), dt);
                if (Distance >= HeartDepth) EnterForestPlanting();
                return;
            }
            Distance = next;
        }

        private void UpdateForestDoor()
        {
            Progress = Clamp(Age / ForestDoorDuration);
            if (Progress < 1) return;
            Distance = VestibuleDepth;
            ForestLights = forestDoorLights;
            ForestDoor = -1;
            ForestLane = forestLaneSpeed = ForestSteering = 0;
            ForestTargetLane = 0;
            forestLateralTap=0;
            Flight.Reset(0);
            Change(Stage.ForestExplore, "");
        }

        private void RememberForestContact(ForestFeature feature)
        {
            ForestContact = feature;
            forestContactAt = PoseTime;
            ForestContactLane = feature.LaneAt(Time);
            ForestContactDepth = feature.DepthAt(Time);
            ForestContactAltitude = feature.AltitudeAt(Time);
        }

        private void CollectForestContacts()
        {
            foreach (ForestFeature feature in forestFeatures)
            {
                if (feature.Collected || feature.Kind == ForestFeatureKind.Plant || feature.InMaze != ForestMazeActive) continue;
                if(feature.InMaze && Math.Abs(feature.WorldY-.54f-feature.Altitude*5.2f-ForestMazeY)>1.7f) continue;
                float ahead = feature.InMaze ? MazeFeatureDepth(feature) : feature.DepthAt(Time) - Distance;
                float lateral = feature.InMaze ? MazeFeatureLane(feature) :
                    Math.Abs(ForestPositionX(feature.LaneAt(Time), feature.DepthAt(Time)) - ForestPositionX(ForestLane, Distance)) / ForkLaneWidth;
                float altitude = Math.Abs(feature.AltitudeAt(Time) - Flight.Height);
                switch (feature.Kind)
                {
                    case ForestFeatureKind.Amber:
                        if (Math.Abs(ahead) > .86f || lateral > .31f || altitude > .14f) break;
                        feature.Collected = true;
                        ForestLights++;
                        RememberForestContact(feature);
                        Notify(JourneyCue.ForestAmber);
                        Signal(FireflyKind.Amber);
                        break;
                    case ForestFeatureKind.Scarlet:
                        if (Time < forestProtectedUntil || Math.Abs(ahead) > .78f || lateral > .30f || altitude > .13f) break;
                        feature.Collected = true;
                        ForestLights = Math.Max(0, ForestLights - 1);
                        forestProtectedUntil = Time + .8f;
                        RememberForestContact(feature);
                        Signal(FireflyKind.Scarlet);
                        Notify(JourneyCue.ForestScarlet);
                        Flight.Deflect();
                        if (ForestLights == 0) { BeginForestFall(); return; }
                        break;
                    default:
                        bool web = feature.Kind == ForestFeatureKind.Web;
                        bool height = web ? Flight.Height >= (feature.InMaze ? .52f : .82f) : Flight.Height <= .19f;
                        float radius = TrapBaseRadius * (web ? ForestWebRange : ForestMawRange);
                        if (!height || Time < forestProtectedUntil || !InsideTrapRange(feature, radius)) break;
                        feature.Collected = true;
                        ForestThreatPulse++;
                        ForestThreatKind = feature.Kind;
                        BeginForestCapture(feature);
                        return;
                }
            }
        }

        private bool InsideTrapRange(ForestFeature feature, float radius)
        {
            // Measure from the same point the scene uses to draw the trap. Facing and route offsets do not change the radius.
            float x = feature.InMaze ? feature.WorldX - ForestMazeX :
                ForestPositionX(feature.Lane, feature.Depth) - ForestPositionX(ForestLane, Distance);
            float z = feature.InMaze ? feature.WorldZ - ForestMazeZ : feature.Depth - Distance;
            return x*x + z*z <= radius*radius;
        }

        private void BeginForestCapture(ForestFeature feature)
        {
            RememberForestContact(feature);
            ForestTrap = feature.Kind == ForestFeatureKind.Web ? ForestTrapKind.Web : ForestTrapKind.Maw;
            ForestTrapAltitude = Flight.Height;
            ForestTrapDepth = feature.Depth;
            ForestTrapLane = feature.Lane;
            ForestHasRescue = ForestLights > 0;
            forestRescueDeparted = false;
            if (!ForestHasRescue) { BeginForestFall(); return; }
            ForestSteering = forestLaneSpeed = 0;
            Flight.Reset(ForestTrapAltitude, ForestHeightLimit);
            forestResumePhase = Phase;
            Change(Stage.ForestTrapped, "");
            Notify(JourneyCue.ForestCaptured);
        }

        private void UpdateForestCapture()
        {
            Progress = Clamp(Age / ForestRescueDuration);
            if (!forestRescueDeparted && Age >= ForestRescueSpendAt)
            {
                forestRescueDeparted = true;
                ForestLights--;
                if (ForestContact != null) RememberForestContact(ForestContact);
                Notify(JourneyCue.ForestRescueLight);
            }
            if (Progress < 1) return;
            if (ForestLights == 0) { BeginForestFall(); return; }
            ForestTrap = ForestTrapKind.None;
            ForestHasRescue = false;
            Flight.Reset(ForestFlightFloor,ForestHeightLimit);
            forestProtectedUntil = Time + .8f;
            Signal(FireflyKind.Amber);
            Notify(JourneyCue.ForestRescued);
            Change(forestResumePhase, forestResumePhase == Stage.ForestPlanting ? "靠近光位，张手发出稳定的声音。" : "");
        }

        private void BeginForestFall()
        {
            if (ForestTrap == ForestTrapKind.None)
                ForestTrap = forestRandom.Next(2) == 0 ? ForestTrapKind.Web : ForestTrapKind.Maw;
            ForestTrapAltitude = Math.Max(Flight.Height, .01f);
            ForestTrapDepth = Distance;
            ForestTrapLane = ForestLane;
            ForestSteering = forestLaneSpeed = 0;
            ForestHasRescue = false;
            Change(Stage.ForestFalling, "");
        }

        private void EnterForestPlanting()
        {
            forestPlantTime = 0;
            ForestPlantTargetId = -1;
            Change(Stage.ForestPlanting, Branch == ForestBranch.Left
                ? "让上、下两处根杯都接到光。靠近后张手，持续发声。"
                : "停在种光台上，张手，持续发声。");
        }

        private void PlantForestLight(float dt)
        {
            if (Branch == ForestBranch.Left && forestPlantMask == 0 && ForestLights == 1 &&
                (forestRenewal == null || forestRenewal.Collected))
                forestRenewal = AddMazeFeature(ForestFeatureKind.Amber,0,.115f,ForestMazeX-ForestLane*2.1f,ForestMazeY,ForestMazeZ);

            ForestFeature aligned = null;
            foreach (ForestFeature site in forestSites)
            {
                if (site != null && !site.Collected && Math.Abs(site.Lane - ForestLane) < .31f &&
                    Math.Abs(site.Altitude - Flight.Height) < (site.Altitude > .5f ? .19f : .17f))
                    aligned = site;
            }

            if (aligned == null || aligned.Id != ForestPlantTargetId)
            {
                forestPlantTime = 0;
                ForestPlantTargetId = aligned?.Id ?? -1;
            }
            bool enough = ForestLights > 1 || ForestLights == 1 && Plants + 1 == ForestPlantsRequired;
            bool voiced = aligned != null && enough && Gesture == Hand.Open && Voice >= .12f;
            forestPlantTime = Math.Max(0, forestPlantTime + dt * (voiced ? 1 : -2.2f));
            Progress = ForestPlantCharge;
            if (forestPlantTime < PlantTime || aligned == null) return;

            ForestLights--;
            Plants++;
            aligned.Collected = true;
            forestPlantMask |= 1 << Array.IndexOf(forestSites, aligned);
            forestPlantTime = 0;
            RememberForestContact(aligned);
            Notify(JourneyCue.Plant);
            Signal(FireflyKind.Amber);
            if (Plants == ForestPlantsRequired)
            {
                ForestTrapKind ending = Branch == ForestBranch.Right ? ForestTrapKind.Cable :
                    aligned.Altitude > .5f ? ForestTrapKind.Web : ForestTrapKind.Maw;
                BeginForestTransition(ending, aligned);
            }
            else
                Message = aligned.Altitude > .5f ? "高处的根杯亮了。滑降到下方的裂石根盆。" : "下方的根盆亮了。飞到另一侧的横枝根杯。";
            SaveForestSpot((int)Distance);
        }

        private void BeginForestTransition(ForestTrapKind kind, ForestFeature site)
        {
            ForestRoute = kind == ForestTrapKind.Web ? 2 : kind == ForestTrapKind.Cable ? 3 : 1;
            ForestTrap = kind;
            ForestTrapAltitude = Flight.Height;
            ForestTrapDepth = Distance;
            ForestTrapLane = site.Lane;
            if (kind == ForestTrapKind.Cable) ForestCableAnchor = forestRandom.Next(3);
            ForestSteering = forestLaneSpeed = 0;
            ForestHasRescue = false;
            Flight.Reset(ForestTrapAltitude, ForestHeightLimit);
            Change(Stage.ForestTransition, "");
        }
    }
}
