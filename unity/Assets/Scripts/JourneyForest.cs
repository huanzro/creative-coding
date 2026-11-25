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
        public int Lane { get; }
        public float Depth { get; }
        public float Altitude { get; }
        public bool Collected { get; internal set; }
        public float Reveal { get; internal set; }
        private float warningTime;

        internal ForestFeature(int id, ForestFeatureKind kind, int lane, float depth, float altitude)
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

        internal bool UpdateWarning(float dt, bool close)
        {
            warningTime = close ? warningTime + dt : 0;
            if (close)
            {
                float t = Math.Min(1, warningTime / .22f);
                Reveal = Math.Max(Reveal, t * t * (3 - 2 * t));
            }
            else Reveal *= (float)Math.Exp(-dt * 3.8f);
            return warningTime >= .71f;
        }
    }

    public sealed partial class Journey
    {
        private const int StartingLights = 3;
        private const float PlantTime = .80f;
        private const float RescueDuration = 2.4f;
        private const float BranchDepth = 112;
        private const float ClearingDepth = 188;
        private const float VestibuleDepth = 168;
        private const float DoorDepth = 179;
        private const float ShaftDepth = 194;
        private const float HeartDepth = 208;
        public const float ForestTransitionDuration = 5.2f;
        public const float ForestDoorDuration = 3;
        public const float ForestFallDuration = 3.15f;

        private readonly Random forestRandom;
        private readonly List<ForestFeature> forestFeatures = new List<ForestFeature>();
        private readonly ForestFeature[] forestSites = new ForestFeature[2];
        private ForestFeature forestRenewal;
        private int forestSeed, nextForestFeatureId, savedForestLights, savedForestLane;
        private int forestPlantMask, savedForestPlants, savedForestDepth;
        private ForestBranch savedForestBranch;
        private float forestPlantTime, forestContactAt = float.NegativeInfinity;
        private float forestLaneSpeed, forestProtectedUntil, forestLoopFrom, forestLoopTo, forestShaftHeight;
        private bool forestRescueDeparted;
        private int forestDoorLights;
        private Stage forestResumePhase;
        private ForestSection forestLoopSection;
        private readonly float[] forestEchoTimes = new float[64], forestEchoLanes = new float[64];
        private readonly Hand[] forestEchoHands = new Hand[64];
        private int forestEchoCursor, forestEchoCount;

        public bool InForest => Chapter == 0 && Lesson < 0 && !IsOpening;
        private bool IsForestSequence => Phase == Stage.ForestTrapped || Phase == Stage.ForestTransition ||
            Phase == Stage.ForestFalling || Phase == Stage.ForestDoor || Phase == Stage.ForestLoop;
        public bool ForestAirborne => InForest && (Flight.Height > .002f || Flight.Speed > .02f);
        public float ForestLane { get; private set; }
        public int ForestTargetLane { get; private set; }
        public float ForestSteering { get; private set; }
        public int ForestLights { get; private set; }
        public ForestBranch Branch { get; private set; }
        public int ForestDoor { get; private set; } = -1;
        public float ForestCenter => ForestCenterAt(Distance);
        public float ForestFlightSpeed => InForest && !IsForestSequence && Phase != Stage.ForestPlanting &&
            (ForestAirborne || Section == ForestSection.Shaft) ? 4.35f + 4.15f * Voice : 0;
        public float ForestHeightLimit => Branch == ForestBranch.Right && Distance >= VestibuleDepth && Distance < ShaftDepth ? 1.34f : 1;
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
        public int ForestTrapLane { get; private set; }
        public int ForestCableAnchor { get; private set; }
        public bool ForestHasRescue { get; private set; }
        public float ForestPlantCharge => Clamp(forestPlantTime / PlantTime);
        public int ForestPlantTargetId { get; private set; } = -1;
        public float ForestWindFactor => !ForestAirborne ? 0 :
            Clamp((Flight.Height - .24f) / .66f) * (IsForestSequence ? .35f : 1f);
        public float GustWarning => ForestWindFactor;
        public ForestSection Section => Phase == Stage.ForestLoop ? forestLoopSection : SectionAt(Distance);

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
            float branch = Branch == ForestBranch.Left ? -1 : Branch == ForestBranch.Right ? 1 : 0;
            if (branch == 0) return 0;
            return branch * 11 * Smooth((depth - BranchDepth) / 12);
        }

        public float ForestLoopOffset => Phase == Stage.ForestLoop ?
            (float)Math.Sin(Progress * Math.PI) * (ForestTargetLane <= 0 ? -2.6f : 2.6f) : 0;

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
            if (sites == 1) return depth == HeartDepth && route == 3;
            return sites == 0 && route == 0 && checkpoint.forestReserve > 0 &&
                (depth == BranchDepth || depth == VestibuleDepth || depth == HeartDepth);
        }

        private ForestFeature AddForestFeature(ForestFeatureKind kind, int lane, float depth, float altitude)
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
                int warmLane = random.Next(-1, 2), threatLane = (warmLane + 2) % 3 - 1;
                AddForestFeature(ForestFeatureKind.Amber, warmLane, depth + Jitter(random, .7f),
                    .20f + (float)random.NextDouble() * .45f);
                AddForestFeature(ForestFeatureKind.Scarlet, threatLane, depth + 4.2f + Jitter(random, .55f),
                    .22f + (float)random.NextDouble() * .46f);
                bool canopy = (wave++ + offset) % 2 == 0;
                AddForestFeature(canopy ? ForestFeatureKind.Web : ForestFeatureKind.Maw, random.Next(-1, 2),
                    depth + 7.2f + Jitter(random, .72f), canopy ? .91f : .063f);
            }
        }

        private void BuildForest()
        {
            forestFeatures.Clear();
            forestRenewal = null;
            forestSites[0] = forestSites[1] = null;
            nextForestFeatureId = (int)Branch * 1000;
            var random = new Random(forestSeed);
            if (Branch == ForestBranch.Shared)
            {
                AddForestFeature(ForestFeatureKind.Amber, 0, 3.55f, .42f);
                AddForestFeature(ForestFeatureKind.Amber, -1, 9, .15f);
                AddForestRun(random, 14, 98);
            }
            else if (Branch == ForestBranch.Left)
            {
                AddForestRun(random, 117, 174);
                AddForestFeature(ForestFeatureKind.Amber, 1, 122.1f, .32f);
                AddForestFeature(ForestFeatureKind.Amber, 0, 181, .38f);
                forestSites[0] = AddForestFeature(ForestFeatureKind.Plant, -1, ClearingDepth, .064f);
                forestSites[1] = AddForestFeature(ForestFeatureKind.Plant, 1, ClearingDepth, .63f);
            }
            else
            {
                AddForestRun(random, 117, 159, 1);
                AddForestFeature(ForestFeatureKind.Amber, 0, 164.2f, .37f);
                AddForestFeature(ForestFeatureKind.Amber, -1, 188.8f, 1.09f);
                forestSites[0] = AddForestFeature(ForestFeatureKind.Plant, 0, HeartDepth, .05f);
            }
            for (int i = 0; i < forestSites.Length; i++)
                if (forestSites[i] != null) forestSites[i].Collected = (forestPlantMask & (1 << i)) != 0;
        }

        private void StartForest()
        {
            Branch = savedForestBranch;
            forestPlantMask = savedForestPlants;
            Plants = ((forestPlantMask & 1) != 0 ? 1 : 0) + ((forestPlantMask & 2) != 0 ? 1 : 0);
            BuildForest();
            Distance = savedForestDepth;
            ForestLights = savedForestLights;
            ForestLane = ForestTargetLane = savedForestLane;
            ForestSteering = forestLaneSpeed = forestPlantTime = forestProtectedUntil = 0;
            ForestTrap = ForestTrapKind.None;
            ForestHasRescue = forestRescueDeparted = false;
            ForestDoor = ForestPlantTargetId = -1;
            ForestContact = null;
            forestContactAt = float.NegativeInfinity;
            forestEchoCount = forestEchoCursor = 0;
            Flight.Reset(0);
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
            savedForestLane = ForestTargetLane;
            savedForestLights = ForestLights;
            savedForestPlants = forestPlantMask;
            Revision++;
        }

        private void ForestTouch(int option)
        {
            ForestTargetLane = Math.Max(-1, Math.Min(1, ForestTargetLane + (option == 0 ? -1 : 1)));
        }

        private void StepForestLateral(float dt)
        {
            // Critically damped motion retains momentum across mid-lane reversals.
            const float speed = 13;
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
            ForestSteering = Math.Max(-1, Math.Min(1, forestLaneSpeed / 5));
        }

        private void UpdateForest(float dt)
        {
            switch (Phase)
            {
                case Stage.ForestTransition:
                    Progress = Clamp(Age / ForestTransitionDuration);
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
                    UpdateForestLoop();
                    return;
            }
            StepForestLateral(dt);
            RecordForestEcho();
            AdvanceForestFlight(dt);
            if (IsForestSequence) return;
            CollectForestContacts(dt);
            if (Phase == Stage.ForestPlanting) PlantForestLight(dt);
        }

        private void AdvanceForestFlight(float dt)
        {
            if (Phase == Stage.ForestPlanting) return;
            float next = Distance + ForestFlightSpeed * dt;
            if (Branch == ForestBranch.Shared)
            {
                if (Distance < 100 && next >= 100) Message = "林路前方分成两道。";
                if (next < BranchDepth) { Distance = next; return; }
                if (Math.Abs(ForestLane) < .42f) { Distance = BranchDepth - .01f; return; }
                Branch = ForestLane < 0 ? ForestBranch.Left : ForestBranch.Right;
                Distance = BranchDepth;
                BuildForest();
                SaveForestSpot((int)BranchDepth);
                Message = "";
                return;
            }
            if (Branch == ForestBranch.Left)
            {
                AdvanceMaze(next);
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
                if (Flight.Height >= 1.03f)
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
                Flight.FollowForestCourse(forestShaftHeight * (1 - Smooth((Distance - ShaftDepth) / (HeartDepth - ShaftDepth))), dt);
                if (Distance >= HeartDepth) EnterForestPlanting();
                return;
            }
            Distance = next;
        }

        private void AdvanceMaze(float next)
        {
            ForestSection section = Section;
            float gate = section == ForestSection.DoubleRoots ? 130 :
                section == ForestSection.EchoWell ? 144 :
                section == ForestSection.BrokenBridges ? 159 :
                section == ForestSection.Canopy ? 174 : ClearingDepth;

            if (next < gate) { Distance = next; return; }
            if (section != ForestSection.Clearing && Math.Abs(ForestLane - ForestTargetLane) > .38f)
            {
                Distance = gate - .01f;
                return;
            }

            switch (section)
            {
                case ForestSection.DoubleRoots:
                    if (ForestTargetLane == 1) BeginForestLoop(gate, 133, section);
                    else if (ForestTargetLane == 0) BeginForestLoop(gate, 118, section);
                    else Distance = Flight.Height < .57f ? gate : gate - .01f;
                    break;
                case ForestSection.EchoWell:
                    if (ForestTargetLane != 1) BeginForestLoop(gate, 127, section);
                    else Distance = Flight.Height >= .42f ? gate : gate - .01f;
                    break;
                case ForestSection.BrokenBridges:
                    if (ForestTargetLane != -1) BeginForestLoop(gate, 134, section);
                    else Distance = Flight.Height >= .47f ? gate : gate - .01f;
                    break;
                case ForestSection.Canopy:
                    if (ForestTargetLane != 1) BeginForestLoop(gate, ForestTargetLane == -1 ? 152 : 147, section);
                    else Distance = Flight.Height <= .48f ? gate : gate - .01f;
                    break;
                case ForestSection.Clearing:
                    Distance = ClearingDepth;
                    EnterForestPlanting();
                    break;
            }
        }

        private void BeginForestLoop(float from, float to, ForestSection section)
        {
            forestLoopFrom = Distance = from;
            forestLoopTo = to;
            forestLoopSection = section;
            ForestSteering = forestLaneSpeed = 0;
            Change(Stage.ForestLoop, "");
        }

        private void UpdateForestLoop()
        {
            Progress = Clamp(Age / 2.2f);
            Distance = forestLoopFrom + (forestLoopTo - forestLoopFrom) * Smooth(Progress);
            if (Progress < 1) return;
            Flight.Reset(0);
            Change(Stage.ForestExplore, "");
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

        private void CollectForestContacts(float dt)
        {
            foreach (ForestFeature feature in forestFeatures)
            {
                if (feature.Collected || feature.Kind == ForestFeatureKind.Plant) continue;
                float ahead = feature.DepthAt(Time) - Distance;
                float lateral = Math.Abs(feature.LaneAt(Time) - ForestLane);
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
                        bool height = web ? Flight.Height >= .82f : Flight.Height <= .19f;
                        bool close = height && lateral < .63f && ahead >= -1.02f &&
                            ahead <= Math.Max(1.75f, ForestFlightSpeed * .76f + .95f);
                        bool ready = feature.UpdateWarning(dt, close);
                        if (!ready || Time < forestProtectedUntil || Math.Abs(ahead) > .94f || lateral > .32f) break;
                        feature.Collected = true;
                        BeginForestCapture(feature);
                        return;
                }
            }
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
            Progress = Clamp(Age / RescueDuration);
            if (!forestRescueDeparted && Age >= .72f)
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
            forestProtectedUntil = Time + .8f;
            Signal(FireflyKind.Amber);
            Notify(JourneyCue.ForestRescued);
            Change(forestResumePhase, forestResumePhase == Stage.ForestPlanting ? "靠近光位，张手发出稳定的声音。" : "");
        }

        private void BeginForestFall()
        {
            ForestTrap = forestRandom.Next(2) == 0 ? ForestTrapKind.Web : ForestTrapKind.Maw;
            ForestTrapAltitude = Math.Max(Flight.Height, .01f);
            ForestTrapDepth = Distance;
            ForestTrapLane = ForestTargetLane;
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
                forestRenewal = AddForestFeature(ForestFeatureKind.Amber, 0, ClearingDepth, .115f);

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
