/* ARTISTIC RATIONALE
 * The hand first shelters light, then cooperates with a memory, then answers a wave.
 * Keeping the spirit at the center lets the changing world show the player's progress.
 */
using System;
using System.Collections.Generic;

namespace Echoes
{
    public enum Hand { Missing, Open, Fist }
    public enum Stage { Tutorial, Gather, Fork, Travel, Nest, RecordReady, Record,
        ReplayReady, Replay, ReplayFailed, Cross, Wave, WaveResult, Compose, Answer, Complete }

    public sealed class Journey
    {
        private static readonly float[][] Incoming = {
            new[] { -.45f, .55f }, new[] { .35f, -.7f, .7f }, new[] { -.3f, .6f }
        };
        private int windPassed;
        private float exposure, markedTime, openTime, fistTime, softTime, fullTime;
        private bool choiceMade, responseActive, waveCorrect;
        private float responseAmplitude;

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
        public float LastSpin { get; private set; }
        public float DancePhase { get; private set; }
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
        public Hand RequiredHand => Slot < Recording.Count && Recording[Slot] == Hand.Open ? Hand.Fist : Hand.Open;

        public Journey() { SelectChapter(0); }

        public void SelectChapter(int chapter)
        {
            if (chapter < 0 || chapter > 2) return;
            Chapter = chapter;
            Round = 0;
            Lesson = chapter == 0 ? 0 : -1;
            Choice = Score = 0;
            Distance = Time = 0;
            DancePhase = 0;
            ResetSegment();
            Revision++;
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
            if (Phase != Stage.Complete) ResetSegment();
        }

        private void ResetSegment()
        {
            Gesture = Hand.Missing;
            Started = false;
            LastRelease = float.NegativeInfinity;
            LastSpin = float.NegativeInfinity;
            Flock = Slot = WaveIndex = 0;
            Voice = Charge = Held = Light = openTime = fistTime = softTime = fullTime = 0;
            Choice = 0;
            choiceMade = responseActive = false;
            Recording.Clear();
            Phrase.Clear();
            StartRound();
        }

        private void StartRound()
        {
            if (Lesson >= 0)
            {
                string[] cues = {
                    "先张手，再握拳半秒，最后张手释放光。",
                    "张手轻声，握拳舒适发声，再张手释放光。",
                    "触摸 A 或 B 选路，再握拳、张手确认。"
                };
                Change(Stage.Tutorial, cues[Lesson]);
            }
            else if (Chapter == 0)
            {
                Flock = 0;
                Change(Stage.Gather, "张手轻声，邀请萤火靠近。");
            }
            else if (Chapter == 1)
            {
                Recording.Clear();
                Slot = 0;
                Change(Stage.RecordReady, "触摸 A 或 B 开始记录动作。");
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
            if (option < 0 || option > 1) return false;
            if (Phase == Stage.Complete && Chapter < 2)
            {
                int score = Score;
                SelectChapter(Chapter + 1);
                Score = score;
                return true;
            }
            if (!Started || Phase == Stage.Complete) return false;
            if ((Phase == Stage.Tutorial && Lesson == 2) || Phase == Stage.Fork)
            {
                Choice = option;
                choiceMade = true;
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
                Change(Stage.Replay, "做出与回声相反的动作，同时维持舒适的声音。");
            }
            else if (Chapter == 2)
            {
                Choice = option;
            }
            return true;
        }

        public void SetHand(Hand next)
        {
            if (next == Gesture || (Gesture == Hand.Missing && next == Hand.Fist)) return;
            var previous = Gesture;
            Gesture = next;
            if (next == Hand.Open) Started = true;
            if (next != Hand.Open) Held = Charge = 0;
            if (previous == Hand.Fist && next == Hand.Open && Phase != Stage.Complete) Release();
        }

        private void Release()
        {
            Light = 1;
            LastRelease = Time;
            // Only an intentional, charged release during fast travel starts a turn.
            if (Held >= .25f && Voice >= .58f && Charge >= .48f && Time - LastSpin >= 1.3f)
                LastSpin = Time;
            switch (Phase)
            {
                case Stage.Tutorial:
                    bool passed = Lesson == 0 && openTime >= .5f && fistTime >= .5f ||
                                  Lesson == 1 && softTime >= .7f && fullTime >= .7f ||
                                  Lesson == 2 && choiceMade;
                    if (!passed) break;
                    Lesson = Lesson == 2 ? -1 : Lesson + 1;
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

        public void Step(float dt, float voice)
        {
            if (!Started || Phase == Stage.Complete || dt <= 0) return;
            Time += dt;
            Age += dt;
            Voice = float.IsNaN(voice) || float.IsInfinity(voice) ? 0 : Math.Max(0, Math.Min(1, voice));
            DancePhase = (DancePhase + dt * (.164f + .122f * Voice)) % 1;
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
            switch (Phase)
            {
                case Stage.Tutorial:
                    if (fist) fistTime += dt;
                    else if (Gesture == Hand.Open) openTime += dt;
                    if (gentle) softTime += dt;
                    if (fist && Voice >= .55f && Voice <= .9f) fullTime += dt;
                    Progress = Lesson == 0 ? Math.Min(1, (openTime + fistTime) * .6f) :
                        Lesson == 1 ? Math.Min(1, (softTime + fullTime) / 1.4f) : choiceMade ? 1 : 0;
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
                    if (Age >= 6) AdvanceRound("世界学会了接住你的回响。");
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
                    Change(Stage.ReplayReady, "回声已经记下你的动作。触摸 A 或 B 开始合作。");
                else Change(Stage.RecordReady, "只记下了一种动作。请重录，加入张手和握拳。");
                return;
            }
            if (markedTime < .85f)
            {
                Change(Stage.ReplayFailed, "相反动作和声音没有同时稳定。保留记录，触摸重试。");
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
