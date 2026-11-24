/* ARTISTIC RATIONALE
 * Amber offers a path and scarlet warns that a system can lead someone astray.
 * The player learns to lift, read a moving signal, and decide which help to use.
 */
using System;
using System.Collections.Generic;

namespace Echoes
{
    public enum Hand { Missing, Open, Fist }
    public enum Stage { Tutorial, Prologue, Gather, Fork, Travel, Nest, RecordReady, Record,
        ReplayReady, Replay, ReplayFailed, Cross, Wave, WaveResult, Compose, Answer, Complete }

    public sealed class Journey
    {
        private static readonly float[][] Incoming = {
            new[] { -.45f, .55f }, new[] { .35f, -.7f, .7f }, new[] { -.3f, .6f }
        };
        private int windPassed;
        private float exposure, markedTime, openTime, fistTime, softTime, fullTime;
        private bool responseActive, waveCorrect;
        private float responseAmplitude;
        private float lastObserve, lastAssist;
        public readonly SpiritFlight Flight = new SpiritFlight();
        public readonly FireflyCourse Fireflies = new FireflyCourse();
        private readonly List<float> amberMarks = new List<float>(7);
        public IReadOnlyList<float> AmberMarks => amberMarks;
        public float OpeningTime { get; private set; }
        public bool EnteredFromOpening { get; private set; }
        public bool IsOpening => Phase == Stage.Prologue;
        public bool IsObserving => PoseTime - lastObserve < 2f;

        public int Chapter { get; private set; }
        public int Round { get; private set; }
        public int Lesson { get; private set; }
        public int Choice { get; private set; }
        public int Flock { get; private set; }
        public int WaveIndex { get; private set; }
        public int Revision { get; private set; }
        public int Score { get; private set; }
        public Stage Phase { get; private set; }
        public Hand Gesture { get; private set; }
        public bool Started { get; private set; }
        public float Time { get; private set; }
        public float LastRelease { get; private set; }
        public float ReleaseAltitude { get; private set; }
        public float LastSpin { get; private set; }
        public float DancePhase { get; private set; }
        public float PoseTime { get; private set; }
        public float PoseVoice { get; private set; }
        public float LastTouch { get; private set; }
        public int TouchOption { get; private set; }
        public float Age { get; private set; }
        public float Distance { get; private set; }
        public float Progress { get; private set; }
        public float WaveX { get; private set; }
        public float ResponseX { get; private set; }
        public float Light { get; private set; }
        public float Voice { get; private set; }
        public float Charge { get; private set; }
        public float Held { get; private set; }
        public string Message { get; private set; }
        public bool HasResponse => responseActive;
        public int Quota => Round + 3;
        public int Slot { get; private set; }
        public int SlotCount => Round + 2;
        public float IncomingAmplitude => Phase == Stage.Wave || Phase == Stage.WaveResult
            ? Math.Abs(Incoming[Round][WaveIndex]) : 0;
        public bool Storm => (Phase == Stage.Wave || Phase == Stage.WaveResult) && Incoming[Round][WaveIndex] < 0;
        public float Preview => IncomingAmplitude + (Choice == 0 ? Charge : -Charge);
        public float Target => Storm ? 0 : 1;
        public bool Aligned => Math.Abs(Preview - Target) <= .16f;
        public readonly List<Hand> Recording = new List<Hand>(4);
        public readonly List<float> Phrase = new List<float>(3);
        public Hand RequiredHand
        {
            get
            {
                Hand recorded = Slot < Recording.Count ? Recording[Slot] : Hand.Open;
                return Choice == 0 ? recorded : recorded == Hand.Open ? Hand.Fist : Hand.Open;
            }
        }

        public Journey() { SelectPractice(); }

        public void SelectPractice() => Enter(0, true, true);

        public void SelectChapter(int chapter)
        {
            if (chapter < 0 || chapter > 2) return;
            Enter(chapter, false, true);
        }

        private void Enter(int chapter, bool practice, bool newTrip)
        {
            Chapter = chapter;
            EnteredFromOpening = false;
            Round = 0;
            Lesson = practice ? 0 : -1;
            if (newTrip)
            {
                Score = 0;
                amberMarks.Clear();
            }
            Distance = Time = 0;
            DancePhase = 0;
            PoseTime = 0;
            ResetSegment();
            Revision++;
        }

        public void PlayOpening()
        {
            Enter(0, false, true);
            OpeningTime = 0;
            Change(Stage.Prologue, "");
        }

        public void FinishOpening()
        {
            if (!IsOpening) return;
            Enter(0, false, false);
            Flight.Reset(.035f);
            EnteredFromOpening = true;
        }

        public void Restore(int chapter, int round, int lesson, int score)
        {
            if (chapter < 0 || chapter > 2 || round < 0 || round > 3 || score < 0 ||
                lesson < -1 || lesson > 2 || (lesson >= 0 && (chapter != 0 || round != 0))) return;
            SelectChapter(chapter);
            Round = round;
            Lesson = lesson;
            Score = score;
            if (Round == 3) Change(Stage.Complete, "这段旅程已经完成。");
            else ResetSegment();
        }

        public void Retry()
        {
            if (IsOpening) { OpeningTime = 0; return; }
            if (Phase != Stage.Complete) ResetSegment();
        }

        private void ResetSegment()
        {
            Gesture = Hand.Missing;
            Started = false;
            LastRelease = float.NegativeInfinity;
            ReleaseAltitude = 0;
            LastSpin = float.NegativeInfinity;
            LastTouch = float.NegativeInfinity;
            TouchOption = 0;
            lastObserve = lastAssist = float.NegativeInfinity;
            PoseVoice = 0;
            Flock = Slot = WaveIndex = 0;
            Voice = Charge = Held = Light = openTime = fistTime = softTime = fullTime = 0;
            Choice = 0;
            responseActive = false;
            Recording.Clear();
            Phrase.Clear();
            Flight.Reset();
            Fireflies.Reset();
            StartRound();
        }

        private void StartRound()
        {
            if (Lesson >= 0)
            {
                string[] cues = {
                    "先张手让精灵看见你。握拳半秒再张开，看看光圈和滑翔。",
                    "张手轻声，再握拳发声半秒，保持发声并张手，看看光迹。",
                    "继续练习，或触摸 A / B 进入故事。"
                };
                Change(Stage.Tutorial, cues[Lesson]);
            }
            else if (Chapter == 0)
            {
                Flock = 0;
                Change(Stage.Gather, "张手轻声，带黄萤火走过断路。红光急促闪动时，从它下方滑过。");
            }
            else if (Chapter == 1)
            {
                Recording.Clear();
                Slot = 0;
                Change(Stage.RecordReady, "门只记得你的动作片段。触摸 A 或 B，留下自己的节拍。");
            }
            else
            {
                WaveIndex = 0;
                SpawnWave();
            }
        }

        private void Change(Stage stage, string message)
        {
            Phase = stage;
            Age = Progress = 0;
            Message = message;
        }

        public bool Touch(int option)
        {
            if (option < 0 || option > 1 || IsOpening) return false;
            if (Phase == Stage.Complete && Chapter < 2)
            {
                Enter(Chapter + 1, false, false);
                return true;
            }
            if (!Started || Phase == Stage.Complete) return false;
            if (Phase == Stage.Tutorial && Lesson == 2)
            {
                PlayOpening();
                return true;
            }
            LastTouch = PoseTime;
            TouchOption = option;
            if (Lesson < 0 && option == 0) lastObserve = PoseTime;
            if (Chapter == 0 && Lesson < 0 && option == 1 && Phase != Stage.Fork && PoseTime - lastAssist >= 3f)
            {
                lastAssist = PoseTime;
                Flight.Assist();
            }
            if (Phase == Stage.Fork)
            {
                Choice = option;
                Message = option == 0 ? "已选林荫绕路。握拳、张手确认。" : "已选风带捷径。握拳、张手确认。";
            }
            if (Phase == Stage.RecordReady || Phase == Stage.Record)
            {
                Recording.Clear();
                Slot = 0;
                Change(Stage.Record, "每个光印持续两秒。至少记录一次张手和一次握拳。");
            }
            else if (Phase == Stage.ReplayReady || Phase == Stage.ReplayFailed)
            {
                Slot = 0;
                markedTime = 0;
                Choice = option;
                Change(Stage.Replay, option == 0
                    ? "沿用自己的节拍，跟着光印重做，同时保持声音。"
                    : "借用残影的补位，它张手时你握拳，同时保持声音。");
            }
            else if (Chapter == 2)
            {
                Choice = option;
                if (Phase == Stage.Answer && option == 0 && Age >= 2.2f)
                    AdvanceRound("最后一颗星，留给决定何时出发的人。");
            }
            return true;
        }

        public void SetHand(Hand next)
        {
            if (IsOpening || next == Gesture || (Gesture == Hand.Missing && next == Hand.Fist)) return;
            var previous = Gesture;
            Gesture = next;
            if (next == Hand.Open) Started = true;
            if (next != Hand.Open) Held = Charge = 0;
            if (previous == Hand.Fist && next == Hand.Open && Held >= .13f && Phase != Stage.Complete) Release();
        }

        private void Release()
        {
            Light = 1;
            LastRelease = PoseTime;
            ReleaseAltitude = Flight.Height;
            Flight.Release(Voice);
            // Only an intentional, charged release during fast travel starts a turn.
            if (Held >= .25f && Voice >= .58f && Charge >= .48f && PoseTime - LastSpin >= 1.3f)
                LastSpin = PoseTime;
            switch (Phase)
            {
                case Stage.Tutorial:
                    bool passed = Lesson == 0 && openTime >= .5f && fistTime >= .5f ||
                                  Lesson == 1 && softTime >= .7f && fullTime >= .7f;
                    if (!passed) break;
                    Lesson++;
                    Revision++;
                    StartRound();
                    break;
                case Stage.Fork:
                    exposure = windPassed = 0;
                    Change(Stage.Travel, Choice == 0 ? "保持张手、轻声，沿林荫引路。" : "风带靠近时握拳护光。");
                    break;
                case Stage.Wave:
                    if (Held < .25f || responseActive) break;
                    responseAmplitude = Choice == 0 ? Charge : -Charge;
                    responseActive = true;
                    ResponseX = .5f;
                    break;
                case Stage.Compose:
                    if (Held < .25f) break;
                    Phrase.Add(Choice == 0 ? Charge : -Charge);
                    if (Phrase.Count == 3) Change(Stage.Answer, "世界正用三道反相波回应你。");
                    break;
            }
        }

        public void Step(float dt, float voice, bool advanceJourney = true)
        {
            if (dt <= 0) return;
            if (IsOpening)
            {
                OpeningTime += dt;
                if (OpeningTime >= OpeningTiming.Duration) FinishOpening();
                return;
            }
            PoseTime += dt;
            PoseVoice = float.IsNaN(voice) || float.IsInfinity(voice) ? 0 : Math.Max(0, Math.Min(1, voice));
            DancePhase = (DancePhase + dt * (.164f + .122f * PoseVoice)) % 1;
            Flight.Step(dt, Gesture);
            if (!advanceJourney || !Started || Phase == Stage.Complete) return;
            Time += dt;
            Age += dt;
            Voice = PoseVoice;
            Light = Math.Max(0, Light - dt * .9f);
            bool fist = Gesture == Hand.Fist;
            bool gentle = Gesture == Hand.Open && Voice >= .12f && Voice <= .45f;
            if (fist)
            {
                Held += dt;
                Charge += (Voice - Charge) * (1 - (float)Math.Exp(-dt / .16f));
            }
            Distance += dt * (1 + Voice) * (Phase == Stage.Travel ? (Choice == 0 ? .09f : .15f) :
                Phase == Stage.Cross ? .18f : .018f);
            if (Lesson < 0)
            {
                FireflyKind signal = Fireflies.Step(dt, Voice, Flight.Height, Gesture, IsObserving);
                if (signal == FireflyKind.Amber)
                {
                    Light = Math.Max(Light, .78f);
                    if (amberMarks.Count < 7) amberMarks.Add(Fireflies.Altitude);
                }
                else if (signal == FireflyKind.Scarlet)
                {
                    Flight.Deflect();
                    Light *= .35f;
                }
            }
            switch (Phase)
            {
                case Stage.Tutorial:
                    if (fist) fistTime += dt;
                    else if (Gesture == Hand.Open) openTime += dt;
                    if (gentle) softTime += dt;
                    if (fist && Voice >= .55f && Voice <= .9f) fullTime += dt;
                    Progress = Lesson == 0 ? Math.Min(1, (openTime + fistTime) * .6f) :
                        Lesson == 1 ? Math.Min(1, (softTime + fullTime) / 1.4f) : 1;
                    break;
                case Stage.Gather:
                case Stage.Nest:
                    Progress = Math.Max(0, Progress + dt * (gentle ? (Phase == Stage.Gather ? .75f : .83f) : Voice > .7f ? -1 : -.3f));
                    if (Progress < 1) break;
                    Progress = 0;
                    if (Phase == Stage.Nest && Flock >= Quota)
                    {
                        Score += Math.Max(0, Flock - Quota) * 75;
                        AdvanceRound("萤火都回到了花蕾。");
                    }
                    else
                    {
                        Flock++;
                        Light = 1;
                        if (Phase == Stage.Gather && Flock >= Quota)
                            Change(Stage.Fork, "林荫绕路更稳，风带捷径更快。选路后握拳、张手出发。");
                    }
                    break;
                case Stage.Travel:
                    UpdateTravel(dt, fist, gentle);
                    break;
                case Stage.Record:
                case Stage.Replay:
                    UpdateEcho(dt);
                    break;
                case Stage.Cross:
                    if (Age >= 2.5f) AdvanceRound("你与自己的回声一起穿过了门。");
                    break;
                case Stage.Wave:
                    UpdateWave(dt, fist);
                    break;
                case Stage.WaveResult:
                    if (Age >= 1.6f) { if (waveCorrect) WaveIndex++; SpawnWave(); }
                    break;
                case Stage.Answer:
                    Progress = Math.Min(1, Age / 6);
                    if (Age >= 6 && Gesture == Hand.Open) AdvanceRound("你张开了手，光在最后一颗星上停了下来。");
                    break;
            }
        }

        private void UpdateTravel(float dt, bool fist, bool gentle)
        {
            Progress = Math.Min(1, Age / (Choice == 0 ? 10 : 6));
            if (Choice == 1 && windPassed <= Round && Progress >= (windPassed + 1f) / (Round + 2))
            {
                windPassed++;
                if (fist) { Light = 1; Message = "这一阵风被掌心挡住了。"; }
                else { Flock = Math.Max(0, Flock - 1); Message = "风吹散了一束光，下阵风靠近时握拳。"; }
            }
            if (Choice == 0)
            {
                exposure = gentle ? 0 : exposure + dt;
                if (exposure >= 2)
                {
                    Flock = Math.Max(0, Flock - 1);
                    exposure = 0;
                    Message = "萤火掉队了，张手、轻声引导。";
                }
            }
            if (Progress < 1) return;
            if (Choice == 1 && Flock >= Quota) Flock++;
            Change(Stage.Nest, "张手轻声，召回散失的萤火，让它们落进花蕾。");
        }

        private void UpdateEcho(float dt)
        {
            if (Phase == Stage.Replay && Gesture == RequiredHand && Voice >= .18f && Voice <= .8f)
                markedTime += Math.Min(dt, Math.Max(0, Age - .45f));
            Progress = Math.Min(1, Age / 2);
            if (Age < 2) return;
            if (Phase == Stage.Record)
            {
                if (Gesture == Hand.Missing)
                {
                    Recording.Clear();
                    Slot = 0;
                    Change(Stage.RecordReady, "光印没有录到真实手势，把手放回镜头再重新记录。");
                    return;
                }
                Recording.Add(Gesture);
                Slot++;
                Age = 0;
                if (Slot < SlotCount) return;
                if (Recording.Contains(Hand.Open) && Recording.Contains(Hand.Fist))
                    Change(Stage.ReplayReady, "A 按自己的节拍重来，B 让回声补另一半。两种方式都能过门。");
                else Change(Stage.RecordReady, "只记下了一种动作。请重录，加入张手和握拳。");
                return;
            }
            if (markedTime < .85f)
            {
                Change(Stage.ReplayFailed, "回声失去同步，但你的原节拍还在。可按 A 回到原节拍，或按 B 再借一次帮助。");
                return;
            }
            Light = 1;
            Slot++;
            Age = markedTime = 0;
            if (Slot == Recording.Count) Change(Stage.Cross, "两侧光路同时亮起。");
        }

        private void SpawnWave()
        {
            if (WaveIndex >= Incoming[Round].Length)
            {
                if (Round < 2) AdvanceRound("你改变了两道光相遇的结果。");
                else Change(Stage.Compose, "选相位、握拳用声音塑形、张手发出三道波。");
                return;
            }
            responseActive = false;
            WaveX = 1.12f;
            bool incomingStorm = Incoming[Round][WaveIndex] < 0;
            Change(Stage.Wave, incomingStorm ? "风暴靠近。用反相抵消，握拳可以放慢来波。" :
                "星灯能量不足。用同相增强，观察数值再张手。");
        }

        private void UpdateWave(float dt, bool fist)
        {
            WaveX -= dt * (fist ? .035f : .11f);
            if (responseActive)
            {
                ResponseX += dt * .34f;
                if (ResponseX >= WaveX)
                {
                    float result = IncomingAmplitude + responseAmplitude;
                    waveCorrect = Math.Abs(result - Target) <= .16f;
                    Change(Stage.WaveResult, waveCorrect ? "两道光波合成成功。" : "声波强度或相位需要调整，再试一次。");
                    responseActive = false;
                    Light = waveCorrect ? 1 : .25f;
                    return;
                }
            }
            if (WaveX > .5f) return;
            responseActive = waveCorrect = false;
            Change(Stage.WaveResult, "来波已越过小人。握拳会放慢下一次来波。");
        }

        private void AdvanceRound(string completion)
        {
            Round++;
            Score += 300;
            Light = 1;
            Revision++;
            if (Round == 3) Change(Stage.Complete, completion);
            else StartRound();
        }
    }
}
