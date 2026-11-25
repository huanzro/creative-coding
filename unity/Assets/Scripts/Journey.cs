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
        StarsRoute, StarsDelegated, StarsSeal, Complete,
        ForestExplore, ForestPlanting, ForestTrapped, ForestTransition
    }
    public enum JourneyCue
    {
        None, Observe, Assist, Withdraw, Plant, Gust, Gate, Misread, Correct, Star, Seal,
        ForestAmber, ForestScarlet, ForestCaptured, ForestRescued, ForestRestart
    }
    public enum RuinPath { Unfinished, Repaired, Assisted, Arcade }
    public enum StarPath { Unfinished, Manual, Delegated, Corrected }

    [Serializable]
    public struct JourneyCheckpoint
    {
        public int version, chapter, lesson, plants, forestRoute, ruin, starPaths, withdrawals, mistakes;
        public int forestSeed, forestReserve, forestLane;
        public bool complete;
    }

    public sealed partial class Journey
    {
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
        private float heldProgress, gentleTime, loudTime, openTime;
        private bool arrivedByOpening;

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
                RuinResult = RuinPath.Unfinished;
                StarCount = StarBits = Withdrawals = Mistakes = 0;
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
            version = 3, chapter = Chapter, lesson = Lesson, plants = Plants, forestRoute = ForestRoute,
            forestSeed = forestSeed, forestReserve = savedForestLights, forestLane = savedForestLane,
            ruin = (int)RuinResult, starPaths = StarBits, withdrawals = Withdrawals, mistakes = Mistakes,
            complete = Phase == Stage.Complete
        };

        public bool Restore(JourneyCheckpoint checkpoint)
        {
            if ((checkpoint.version != 2 && checkpoint.version != 3) || checkpoint.chapter < 0 || checkpoint.chapter > 2 ||
                checkpoint.lesson < -1 || checkpoint.lesson > 2 || (checkpoint.lesson >= 0 && checkpoint.chapter != 0) ||
                checkpoint.plants < 0 || checkpoint.plants > (checkpoint.version == 2 ? 2 : ForestPlantsRequired) ||
                checkpoint.forestRoute < 0 || checkpoint.forestRoute > 2 ||
                checkpoint.forestReserve < 0 || checkpoint.forestReserve > 32 || Math.Abs((long)checkpoint.forestLane) > 1 ||
                checkpoint.ruin < 0 || checkpoint.ruin > 3 || (checkpoint.starPaths & ~63) != 0) return false;
            Enter(checkpoint.chapter, checkpoint.lesson >= 0, true);
            Lesson = checkpoint.lesson;
            Plants = checkpoint.plants;
            ForestRoute = checkpoint.forestRoute;
            RuinResult = (RuinPath)checkpoint.ruin;
            StarBits = checkpoint.starPaths;
            while (StarCount < 3 && PathAt(StarCount) != StarPath.Unfinished) StarCount++;
            Withdrawals = checkpoint.withdrawals & 7;
            Mistakes = checkpoint.mistakes & 7;
            if (checkpoint.version == 3)
            {
                forestSeed = checkpoint.forestSeed;
                savedForestLights = checkpoint.forestReserve;
                savedForestLane = checkpoint.forestLane;
            }
            else if (InForest && checkpoint.complete)
            {
                Enter(1, false, false);
                return true;
            }
            StartSection();
            if (checkpoint.complete && Lesson < 0 && Chapter != 0) Complete();
            return true;
        }

        public void Retry()
        {
            if (IsOpening) { OpeningTime = 0; return; }
            if (Phase == Stage.Complete || IsForestSequence) return;
            ResetResponses();
            StartSection();
        }

        private void Change(Stage phase, string message)
        {
            Phase = phase;
            Age = Progress = heldProgress = 0;
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
            if (Phase == Stage.Complete)
            {
                if (Chapter < 2) Enter(Chapter + 1, false, false);
                else
                {
                    if (option == 0)
                    {
                        Observe();
                        Message = "暖黄是自己飞过的路，青色是借来的那一步，折线保留了撤回或纠正。";
                    }
                    else
                    {
                        Signal(FireflyKind.Amber);
                        Notify(JourneyCue.Seal);
                    }
                }
                return true;
            }
            if (!Started || IsForestSequence) return false;
            LastTouch = PoseTime;
            TouchOption = option;
            if (Lesson >= 0) PracticeTouch(option);
            else if (Chapter == 0) ForestTouch(option);
            else if (Chapter == 1) RuinTouch(option);
            else StarsTouch(option);
            return true;
        }

        public void SetHand(Hand next)
        {
            if (IsOpening || next == Gesture || (Gesture == Hand.Missing && next == Hand.Fist)) return;
            var previous = Gesture;
            Gesture = next;
            if (next == Hand.Open) Started = true;
            if (previous == Hand.Fist && next == Hand.Open && Held >= .13f && Phase != Stage.Complete && !IsForestSequence)
                Release();
            Held = Charge = 0;
        }

        private void Release()
        {
            LastRelease = PoseTime;
            ReleaseAltitude = Flight.Height;
            Light = 1;
            Flight.Release(Voice);
            if (Held >= .25f && Voice >= .58f && Charge >= .48f && PoseTime - LastSpin >= 1.3f)
                LastSpin = PoseTime;

            switch (Phase)
            {
                case Stage.Tutorial:
                    if (Lesson == 0 && openTime >= .38f && Held >= .46f) NextLesson();
                    break;
                case Stage.RuinRelease:
                    CheckRelease();
                    break;
                case Stage.RuinRepair:
                    if (repairReady && Voice <= .10f)
                    {
                        CrossRuin(RuinPath.Repaired);
                    }
                    break;
                case Stage.RuinSide:
                    CheckReflection();
                    break;
                case Stage.StarsSeal:
                    if (Held >= .4f)
                    {
                        Notify(JourneyCue.Seal);
                        Complete();
                    }
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
            if (!IsForestSequence && (!InForest || advanceJourney && Gesture != Hand.Missing)) Flight.Step(dt, Gesture);
            storyPulse = Math.Max(0, storyPulse - dt);
            Light = Math.Max(0, Light - dt * .85f);
            if (!advanceJourney || Phase == Stage.Complete) return;
            if (IsForestSequence)
            {
                Time += dt;
                Age += dt;
                UpdateForest(dt);
                return;
            }
            if (!Started || Gesture == Hand.Missing) return;

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
            else if (Chapter == 1) UpdateRuins(dt);
            else UpdateStars(dt);
        }

        private void Walk(float dt, float speed) => Distance += dt * speed * (1 + Voice * .73f);

        private void Complete()
        {
            string[] endings = {
                "根光照亮了两处停歇点。触摸 A 或 B，沿光进遗迹。",
                "三束回声都留在了桥上。触摸 A 或 B，去星海。",
                "这是你和系统一起走过的星图。按试玩，开始一段新的旅程。"
            };
            Change(Stage.Complete, endings[Chapter]);
            Progress = 1;
            Revision++;
        }

        private static float Clamp(float value) => Math.Max(0, Math.Min(1, value));

        public string LeftLabel => Phase == Stage.Complete ? (Chapter < 2 ? "A · 下一章" : "A · 回看路线") :
            InForest ? "A · 向左" : Phase == Stage.Tutorial && Lesson == 2 ? "A · 看落点" : "A · 看一眼";

        public string RightLabel
        {
            get
            {
                if (Phase == Stage.Complete) return Chapter < 2 ? "B · 下一章" : "B · 听回响";
                if (InForest) return "B · 向右";
                if (Phase == Stage.StarsDelegated) return "B · 收回授权";
                if (Phase == Stage.StarsSeal) return "B · 留给自己";
                return "B · 借一步";
            }
        }
    }
}
