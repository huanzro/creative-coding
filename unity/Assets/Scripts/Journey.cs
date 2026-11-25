/* ARTISTIC RATIONALE
 * The same hand can explore, disagree, or authorize a small action.
 * The route remembers those choices as light instead of a tally of errors.
 */
using System;

namespace Echoes
{
    public enum Hand { Missing, Open, Fist }
    public enum Stage
    {
        Tutorial, Prologue, ForestRoute, ForestPlant, ForestWind, ForestCrossing,
        RuinGather, RuinVoice, RuinRelease, RuinMisread, RuinRepair, RuinSide, RuinCrossing,
        Complete = 16,
        ForestExplore, ForestPlanting, ForestTrapped, ForestTransition,
        ForestFalling, ForestDoor, ForestLoop, RuinExplore, RuinTransfer, RuinRetry, StarsCanvas
    }
    public enum JourneyCue
    {
        None, Observe, Assist, Withdraw, Plant, Gust, Gate, Misread, Correct, Star, Seal,
        ForestAmber, ForestScarlet, ForestCaptured, ForestRescued, ForestRestart,
        ForestRescueLight, ForestDoor, RuinHeartbeat, RuinCollect, RuinDoor, RuinTravel, RuinEscape,
        RuinWall, RuinReplay, RuinRetryBeat
    }
    public enum RuinPath { Unfinished, Repaired, Assisted, Arcade, Explored }

    [Serializable]
    public struct JourneyCheckpoint
    {
        public int version, chapter, lesson, plants, forestRoute, ruin, starPaths, starMotifs;
        public float starDepth, starTime;
        public string starArtwork;
        public int forestSeed, forestReserve, forestLane, forestBranch, forestSites, forestDepth;
        public int ruinSeed, ruinRoom, ruinVisited;
        public long ruinFinds, ruinFindsExtra, ruinOpenLow, ruinOpenHigh, ruinExhibitOrder;
        public bool ruinHeartSeen;
        public long ruinChestLootTaken, ruinReleasedMemories, ruinReleasedMemoriesExtra;
        public int ruinRetryRooms, ruinRetryRecord, ruinRetryLastLane, ruinRetryOutcome, ruinRetryAttempts;
        public bool ruinRetryWall;
        public float ruinRetryDepth;
        public int ruinLoops;
        public bool complete;
    }

    public sealed partial class Journey
    {
        public const float MinJumpHeightScale = .50f, DefaultJumpHeightScale = 1f, MaxJumpHeightScale = 2f;
        public readonly SpiritFlight Flight = new SpiritFlight();
        public readonly FireflyCourse Fireflies = new FireflyCourse();

        public int Chapter { get; private set; }
        public int Lesson { get; private set; }
        public int Revision { get; private set; }
        public Stage Phase { get; private set; }
        public Hand Gesture { get; private set; }
        public bool Started { get; private set; }
        public float OpeningTime { get; private set; }
        public float Time { get; private set; }
        public float Age { get; private set; }
        public float PoseTime { get; private set; }
        public float PoseVoice { get; private set; }
        public float DancePhase { get; private set; }
        public float Distance { get; private set; }
        public float Progress { get; private set; }
        public float LastRelease { get; private set; }
        public float ReleaseAltitude { get; private set; }
        public float LastSpin { get; private set; }
        public float LastTouch { get; private set; }
        public int TouchOption { get; private set; }
        public float Held { get; private set; }
        public float Charge { get; private set; }
        public float Light { get; private set; }
        public float Voice { get; private set; }
        public string Message { get; private set; }
        public JourneyCue Cue { get; private set; }
        public int CueId { get; private set; }
        public float CueAge => PoseTime - cueAt;
        public bool IsOpening => Phase == Stage.Prologue;
        public bool IsObserving => PoseTime < hintUntil;
        public bool HintReady { get; private set; }
        public float FireflyPulse => Math.Max(Fireflies.Pulse, storyPulse);
        public FireflyKind FireflyResponse => storyPulse > 0 ? storyFly : Fireflies.Response;
        public float WindOutput => ForestWindFactor * .78f;
        public int World => Lesson >= 0 ? 3 : Chapter;

        private float hintUntil, cueAt, storyPulse;
        private FireflyKind storyFly;
        private float gentleTime, loudTime, openTime;
        private float jumpHeightScale = DefaultJumpHeightScale;
        private bool arrivedByOpening;

        public float JumpHeightScale
        {
            get => jumpHeightScale;
            set => jumpHeightScale = TuningValue(value, MinJumpHeightScale, MaxJumpHeightScale, DefaultJumpHeightScale);
        }

        public Journey() : this(new Random()) { }

        public Journey(int seed) : this(new Random(seed)) { }

        private Journey(Random random)
        {
            forestRandom = random;
            SelectPractice();
        }

        public void SelectPractice() => Enter(0, true, true);

        public void SelectChapter(int chapter)
        {
            if (chapter >= 0 && chapter <= 2) Enter(chapter, false, true);
        }

        private void Enter(int chapter, bool practice, bool newTrip)
        {
            if (newTrip)
            {
                Plants = 0;
                ForestRoute = 0;
                ResetForestHistory();
                ResetRuinHistory();
                RuinResult = RuinPath.Unfinished;
            }
            Chapter = chapter;
            Lesson = practice ? 0 : -1;
            arrivedByOpening = false;
            Distance = Time = PoseTime = DancePhase = 0;
            cueAt = float.NegativeInfinity;
            Cue = JourneyCue.None;
            ResetResponses();
            StartSection();
            Revision++;
        }

        private void ResetResponses()
        {
            Gesture = Hand.Missing;
            Started = false;
            Voice = PoseVoice = Held = Charge = Light = 0;
            LastRelease = LastSpin = LastTouch = hintUntil = float.NegativeInfinity;
            TouchOption = 0;
            ReleaseAltitude = 0;
            storyPulse = 0;
            storyFly = FireflyKind.None;
            Flight.Reset();
            Fireflies.Reset();
        }

        public void PlayOpening()
        {
            Enter(0, false, true);
            StartOpening();
        }

        private void StartOpening()
        {
            Lesson = -1;
            OpeningTime = 0;
            Change(Stage.Prologue, "");
        }

        public void FinishOpening()
        {
            if (!IsOpening) return;
            Enter(0, false, false);
            arrivedByOpening = true;
        }

        public bool EnteredFromOpening => arrivedByOpening;

        public JourneyCheckpoint Checkpoint => new JourneyCheckpoint {
            version = 11, chapter = Chapter, lesson = Lesson, plants = Plants, forestRoute = ForestRoute,
            forestSeed = forestSeed, forestReserve = savedForestLights, forestLane = savedForestLane,
            forestBranch = (int)savedForestBranch, forestSites = savedForestPlants, forestDepth = savedForestDepth,
            ruin = (int)RuinResult, starDepth = InStars ? Distance : 0, starTime = InStars ? Time : 0,
            ruinSeed = ruinSeed, ruinRoom = ruinRoomId, ruinVisited = ruinVisited, ruinFinds = (long)ruinFinds,
            ruinFindsExtra = (long)ruinFindsExtra, ruinOpenLow = (long)ruinOpenLow, ruinOpenHigh = (long)ruinOpenHigh, ruinHeartSeen = ruinHeartSeen,
            ruinChestLootTaken = (long)ruinChestLootTaken, ruinReleasedMemories = (long)ruinReleasedMemories,
            ruinReleasedMemoriesExtra = (long)ruinReleasedMemoriesExtra,
            ruinExhibitOrder = (long)ruinExhibitOrder,
            ruinRetryRooms = ruinRetryRooms,
            ruinRetryRecord = (int)ruinRetryRecord, ruinRetryLastLane = ruinRetryLastLane,
            ruinRetryOutcome = (int)RuinRetryOutcome, ruinRetryAttempts = RuinRetryAttempts,
            ruinRetryWall = ruinRetryWall, ruinRetryDepth = RuinRetryWallDepth,
            ruinLoops = ruinLoops,
            complete = Phase == Stage.Complete
        };

        public bool Restore(JourneyCheckpoint checkpoint)
        {
            if (checkpoint.version < 2 || checkpoint.version > 11 || checkpoint.chapter < 0 || checkpoint.chapter > 2 ||
                checkpoint.lesson < -1 || checkpoint.lesson > 2 || (checkpoint.lesson >= 0 && checkpoint.chapter != 0) ||
                checkpoint.plants < 0 || checkpoint.plants > (checkpoint.version == 2 ? 2 : 3) ||
                checkpoint.forestRoute < 0 || checkpoint.forestRoute > (checkpoint.version >= 4 ? 3 : 2) ||
                checkpoint.forestReserve < 0 || checkpoint.forestReserve > 64 || Math.Abs((long)checkpoint.forestLane) > 1 ||
                checkpoint.ruin < 0 || checkpoint.ruin > (checkpoint.version >= 5 ? 4 : 3) ||
                (checkpoint.starPaths & ~63) != 0 ||
                !(checkpoint.starDepth >= 0 && checkpoint.starDepth <= 1000000 &&
                  checkpoint.starTime >= 0 && checkpoint.starTime <= 1000000)) return false;
            if (checkpoint.version >= 4 && !IsForestCheckpointValid(checkpoint)) return false;
            if (checkpoint.version >= 5 &&
                ((uint)checkpoint.ruinRoom >= RuinAtlas.RoomCount || (checkpoint.ruinVisited & ~65535) != 0)) return false;
            Enter(checkpoint.chapter, checkpoint.lesson >= 0, true);
            Lesson = checkpoint.lesson;
            Plants = checkpoint.plants;
            ForestRoute = checkpoint.forestRoute;
            RuinResult = (RuinPath)checkpoint.ruin;
            if (checkpoint.version >= 4)
            {
                forestSeed = checkpoint.forestSeed;
                savedForestLights = checkpoint.forestReserve;
                savedForestLane = checkpoint.forestLane;
                savedForestBranch = (ForestBranch)checkpoint.forestBranch;
                savedForestPlants = checkpoint.forestSites;
                savedForestDepth = checkpoint.forestDepth;
            }
            else if (InForest)
            {
                if (checkpoint.complete || checkpoint.version == 3 && checkpoint.plants == 3)
                {
                    Enter(1, false, false);
                    return true;
                }
                ForestRoute = 0;
            }
            if (checkpoint.version >= 5)
            {
                // Enter may build a temporary entrance room before the saved seed and inventory are available.
                ruinRooms = null;
                RuinVisitedCount = RuinHearts = RuinBrains = RuinPhones = RuinComputers = RuinChests = 0;
                ruinSeed = checkpoint.ruinSeed;
                ruinRoomId = checkpoint.ruinRoom;
                ruinVisited = checkpoint.ruinVisited;
                ruinFinds = (ulong)checkpoint.ruinFinds;
                ruinOpenLow = (ulong)checkpoint.ruinOpenLow;
                ruinOpenHigh = (ulong)checkpoint.ruinOpenHigh;
                ruinHeartSeen = checkpoint.ruinHeartSeen;
            }
            if (checkpoint.version >= 6)
            {
                ruinChestLootTaken = (ulong)checkpoint.ruinChestLootTaken;
                ruinReleasedMemories = (ulong)checkpoint.ruinReleasedMemories;
                RestoreRuinRetry(checkpoint);
            }
            if (checkpoint.version >= 7)
            {
                ruinLoops = Math.Max(0, Math.Min(999, checkpoint.ruinLoops));
            }
            if (checkpoint.version >= 8)
            {
                ruinFindsExtra = (ulong)checkpoint.ruinFindsExtra;
                ruinReleasedMemoriesExtra = (ulong)checkpoint.ruinReleasedMemoriesExtra;
            }
            ruinExhibitOrder = checkpoint.version >= 9 ? (ulong)checkpoint.ruinExhibitOrder : 0;
            if (InStars) RestoreStars(checkpoint);
            else StartSection();
            if (checkpoint.complete && InRuins) Complete();
            return true;
        }

        public void Retry()
        {
            if (IsOpening) { OpeningTime = 0; return; }
            if (InStars) { RetryStar(); return; }
            if (Phase == Stage.RuinRetry) { RetryRuinRetry(); return; }
            if (Phase == Stage.Complete || IsForestSequence || IsRuinSequence) return;
            ResetResponses();
            StartSection();
        }

        private void Change(Stage phase, string message)
        {
            Phase = phase;
            Age = Progress = 0;
            HintReady = false;
            Message = message;
        }

        private void Notify(JourneyCue cue)
        {
            Cue = cue;
            CueId++;
            cueAt = PoseTime;
        }

        private void Signal(FireflyKind signal)
        {
            storyPulse = FireflyCourse.ContactDuration;
            storyFly = signal;
            if (signal == FireflyKind.Amber) Light = Math.Max(Light, .82f);
        }

        private void Observe()
        {
            hintUntil = PoseTime + 2.8f;
            HintReady = true;
            Notify(JourneyCue.Observe);
        }

        private bool Authorize(string prompt)
        {
            if (HintReady) return true;
            Observe();
            Message = prompt;
            return false;
        }

        public bool Touch(int option)
        {
            if ((option != 0 && option != 1) || IsOpening) return false;
            if (InStars)
            {
                if (!StarsTouch(option)) return false;
                LastTouch = PoseTime;
                TouchOption = option;
                return true;
            }
            if (Phase == Stage.Complete)
            {
                Enter(Chapter + 1, false, false);
                return true;
            }
            if (!Started || IsRuinSequence || IsForestSequence && !CanSteerMaze) return false;
            LastTouch = PoseTime;
            TouchOption = option;
            if (Lesson >= 0) PracticeTouch(option);
            else if (Chapter == 0) ForestTouch(option);
            else if (Chapter == 1)
            {
                if (Phase == Stage.RuinRetry) TouchRuinRetry(option);
                else if (!TryRuinLootTouch(option)) RuinTouch(option);
            }
            return true;
        }

        public void SetHand(Hand next) => SetHand(next, false);

        internal void SetHand(Hand next, bool cameraGesture)
        {
            if (IsOpening || next == Gesture || (Gesture == Hand.Missing && next == Hand.Fist)) return;
            var previous = Gesture;
            Gesture = next;
            if (next == Hand.Open) Started = true;
            if (!InStars && previous == Hand.Fist && next == Hand.Open && (cameraGesture || Held >= .13f) && Phase != Stage.Complete &&
                !IsForestSequence && !IsRuinSequence)
                Release();
            Held = Charge = 0;
        }

        private void Release()
        {
            if (InRuins && (Phase == Stage.RuinRetry || RuinLootOpen)) return;
            LastRelease = PoseTime;
            ReleaseAltitude = Flight.Height;
            Light = 1;
            if (InForest || InRuins)
                Flight.ReleaseForest(Voice, InForest && Section == ForestSection.Vestibule ? ForestDoorJumpHeightScale : JumpHeightScale);
            else Flight.Release(Voice, JumpHeightScale);
            if (Held >= .25f && Voice >= .58f && Charge >= .48f && PoseTime - LastSpin >= 1.3f)
                LastSpin = PoseTime;

            switch (Phase)
            {
                case Stage.Tutorial:
                    if (Lesson == 0 && openTime >= .38f && Held >= .46f) NextLesson();
                    break;
            }
        }

        public void Step(float dt, float voice, bool advanceJourney = true)
        {
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            if (IsOpening)
            {
                OpeningTime += dt;
                if (OpeningTime >= OpeningTiming.Duration) FinishOpening();
                return;
            }

            PoseTime += dt;
            PoseVoice = float.IsNaN(voice) || float.IsInfinity(voice) ? 0 : Clamp(voice);
            DancePhase = (DancePhase + dt * (.164f + .122f * PoseVoice)) % 1;
            bool ruinDescending = InRuins && Flight.Speed < -.035f;
            if (InRuins)
            {
                if (!IsRuinSequence && Phase != Stage.RuinRetry && !RuinLootOpen && advanceJourney && Gesture != Hand.Missing)
                    Flight.StepForest(dt, Gesture, RuinHeightLimit);
            }
            else if (Lesson >= 0) Flight.Step(dt, Gesture);
            else if (InForest && !IsForestSequence && Section != ForestSection.Shaft && advanceJourney && Gesture != Hand.Missing)
                Flight.StepForest(dt, Gesture, ForestHeightLimit, ForestFlightFloor);
            storyPulse = Math.Max(0, storyPulse - dt);
            Light = Math.Max(0, Light - dt * .85f);
            if (!advanceJourney || Phase == Stage.Complete) return;
            if (InStars)
            {
                Time += dt;
                Age += dt;
                Voice = PoseVoice;
                UpdateStars(dt);
                return;
            }
            if (IsForestSequence)
            {
                Time += dt;
                Age += dt;
                UpdateForest(dt);
                return;
            }
            if (IsRuinSequence)
            {
                Time += dt;
                Age += dt;
                Voice = Gesture == Hand.Missing ? 0 : PoseVoice;
                UpdateRuins(dt);
                return;
            }
            if (Phase == Stage.RuinRetry)
            {
                Time += dt;
                Age += dt;
                Voice = Gesture == Hand.Missing ? 0 : PoseVoice;
                UpdateRuinRetry(dt);
                return;
            }
            if (!Started || Gesture == Hand.Missing)
            {
                if (InRuins) { RuinForwardSpeed = 0; StopRuinSteering(); }
                return;
            }

            Time += dt;
            Age += dt;
            Voice = PoseVoice;
            if (Gesture == Hand.Fist)
            {
                Held += dt;
                Charge += (Voice - Charge) * (1 - (float)Math.Exp(-dt / .16f));
            }
            if (Lesson >= 0) UpdatePractice(dt);
            else if (Chapter == 0) UpdateForest(dt);
            else UpdateRuins(dt, ruinDescending);
        }

        private void Walk(float dt, float speed) => Distance += dt * speed * (1 + Voice * .73f);

        private void Complete()
        {
            string[] endings = {
                "根光照亮了两处停歇点。触摸 A 或 B，沿光进遗迹。",
                "你找到了离开遗迹的通道。触摸 A 或 B，去星海。"
            };
            Change(Stage.Complete, endings[Chapter]);
            Progress = 1;
            Revision++;
        }

        private static float Clamp(float value) => Math.Max(0, Math.Min(1, value));

        public string LeftLabel => InStars ? "A · 流线" : Phase == Stage.Complete ? "A · 下一章" :
            RuinLootOpen ? "A · 切换" : Phase == Stage.RuinRetry ? RuinRetryLeftLabel :
            ForestMazeWalkArea ? "A · 左转" : InForest || InRuins ? "A · 向左" :
            Phase == Stage.Tutorial && Lesson == 2 ? "A · 看落点" : "A · 看一眼";

        public string RightLabel
        {
            get
            {
                if (InStars) return "B · 星纹";
                if (Phase == Stage.Complete) return "B · 下一章";
                if (RuinLootOpen) return "B · 确认";
                if (Phase == Stage.RuinRetry) return RuinRetryRightLabel;
                if (RuinCanTake) return RuinTakeLabel;
                if (ForestMazeWalkArea) return "B · 右转";
                if (InForest || InRuins) return "B · 向右";
                return "B · 借一步";
            }
        }
    }
}
