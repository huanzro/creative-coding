using System;

namespace Echoes
{
    public enum RuinRetryRecordKind { None, Left, Right, Upper, Lower, HeartPutDown, BrainPutDown }
    public enum RuinRetryPhase { Inactive, Impact, Review, Choice, Guided, Trace, Listen, Release, Fault, Departure }
    public enum RuinRetryResolution { None, Guided, Takeover, Remembered }

    public sealed partial class Journey
    {
        private const float WallApproach = 1.65f;
        private const float WallStance = 1.06f;
        private const float ImpactAt = .43f;
        private const float ImpactDuration = 1.31f;
        private const float ReviewDuration = 2.65f;
        private const float GuidedTurnAt = 3.65f;
        private const float GuidedDuration = 6.75f;
        private const float TraceReadyAt = 1.10f;
        private const float TraceDuration = 4.42f;
        private const float SilenceReadyAt = .67f;
        private const float ListenDuration = 5.45f;
        private const float HeartTwoBeats = 1.03f;
        private const float DepartureDuration = 3.35f;

        private int ruinRetryRooms, ruinRetryLastLane;
        private bool ruinRetryWall, retryCollision;
        private RuinRetryRecordKind ruinRetryRecord;
        private RuinRetryPhase retryPhase;
        private RuinRetryResolution retryDeparture;
        private float retryFromDepth, retryFromLane, retryFromHeight, retrySilence, retryVoice;
        private float retryBeatAt = -1;
        private int retryInputLane;

        public RuinRetryPhase RuinRetryState => InRuins ? retryPhase : RuinRetryPhase.Inactive;
        public RuinRetryRecordKind RuinRetryRecord => ruinRetryRecord;
        public RuinRetryResolution RuinRetryOutcome { get; private set; }
        public RuinRetryResolution RuinRetryDisplayOutcome => retryPhase == RuinRetryPhase.Departure &&
            retryDeparture != RuinRetryResolution.None ? retryDeparture : RuinRetryOutcome;
        public int RuinRetryRequired => RuinDisciplineCount - 1;
        public int RuinRetryVisited => CountRuinPassages(ruinRetryRooms);
        public int RuinRetryAttempts { get; private set; }
        public int RuinRetryTraceLane => ruinRetryLastLane < 0 ? -1 : 1;
        public int RuinRetryLastAttemptLane { get; private set; }
        public float RuinRetryWallDepth { get; private set; }
        public bool RuinRetryReady => retryPhase == RuinRetryPhase.Trace && Age >= TraceReadyAt;
        public float RuinRetryTraceLight => retryPhase == RuinRetryPhase.Trace
            ? EaseRetry(.27f, .84f, Age) * (1 - .76f * EaseRetry(RuinBrains > 0 ? 3.1f : 2.0f, 4.30f, Age)) : 0;
        public float RuinRetryPulse
        {
            get
            {
                float beat = retryPhase == RuinRetryPhase.Guided ? Age - GuidedTurnAt :
                    retryPhase == RuinRetryPhase.Listen && retryBeatAt >= 0 ? Age - retryBeatAt : -1;
                float doubleBeat = HeartFlash(beat, .08f) + .72f * HeartFlash(beat, .46f);
                return doubleBeat * (RuinHearts > 0 ? 1 : .76f);
            }
        }

        public string RuinRetryLeftLabel => retryPhase == RuinRetryPhase.Choice ? "A · 让系统带一段" :
            retryPhase == RuinRetryPhase.Trace ? "A · 走左侧" :
            retryPhase == RuinRetryPhase.Fault ? "A · 回到墙前" : "";
        public string RuinRetryRightLabel => retryPhase == RuinRetryPhase.Choice ? "B · 带着记忆重试" :
            retryPhase == RuinRetryPhase.Trace ? "B · 走右侧" :
            retryPhase == RuinRetryPhase.Fault ? "B · 再试一次" :
            retryPhase == RuinRetryPhase.Guided && Age >= GuidedTurnAt ? "B · 收回引导" : "";

        public string RuinRetryClue
        {
            get
            {
                switch (retryPhase)
                {
                    case RuinRetryPhase.Choice: return "系统已根据你的选择生成路线。是否按这条路线重试？";
                    case RuinRetryPhase.Trace: return "等旧影走完，选还留着暖色光迹的一侧。";
                    case RuinRetryPhase.Listen: return retryBeatAt < 0 ? "安静片刻，等机器的节拍退下去。" : "听完两下心跳。";
                    case RuinRetryPhase.Release: return "张手并轻声约半秒（Z），按自己的节奏开门。";
                    case RuinRetryPhase.Guided: return Age < GuidedTurnAt ? "沿蓝光前行。接近最后一个弯。" :
                        "暖色回声出现在侧面。B 收回引导，继续跟随也可以到达出口。";
                    default: return "";
                }
            }
        }

        private static int CountRuinPassages(int mask)
        {
            int count = 0;
            for (; mask != 0; mask &= mask - 1) count++;
            return count;
        }

        private void ResetRuinRetry()
        {
            ruinRetryRooms = ruinRetryLastLane = RuinRetryAttempts = RuinRetryLastAttemptLane = 0;
            ruinRetryWall = retryCollision = false;
            ruinRetryRecord = RuinRetryRecordKind.None;
            retryPhase = RuinRetryPhase.Inactive;
            retryDeparture = RuinRetryOutcome = RuinRetryResolution.None;
            RuinRetryWallDepth = 0;
            retryBeatAt = -1;
        }

        private void RestoreRuinRetry(JourneyCheckpoint checkpoint)
        {
            ruinRetryRooms = checkpoint.ruinRetryRooms & 65534;
            ruinRetryRecord = (uint)checkpoint.ruinRetryRecord <= (uint)RuinRetryRecordKind.BrainPutDown
                ? (RuinRetryRecordKind)checkpoint.ruinRetryRecord : RuinRetryRecordKind.None;
            ruinRetryLastLane = Math.Sign(checkpoint.ruinRetryLastLane);
            RuinRetryAttempts = Math.Max(0, checkpoint.ruinRetryAttempts);
            ruinRetryWall = checkpoint.ruinRetryWall && checkpoint.ruinRetryDepth > WallApproach &&
                checkpoint.ruinRetryDepth <= 53;
            RuinRetryWallDepth = ruinRetryWall ? checkpoint.ruinRetryDepth : 0;
            RuinRetryOutcome = ruinRetryWall && (uint)checkpoint.ruinRetryOutcome <= (uint)RuinRetryResolution.Remembered
                ? (RuinRetryResolution)checkpoint.ruinRetryOutcome : RuinRetryResolution.None;
        }

        private bool ResumeRuinRetry()
        {
            if (!ruinRetryWall) return false;
            RuinLane = 0;
            Flight.Reset(.025f, RuinHeightLimit);
            Distance = RuinRetryWallDepth - WallStance;
            if (RuinRetryOutcome != RuinRetryResolution.None)
            {
                Distance = RuinRetryWallDepth + 5.85f;
                SetRuinRetryPhase(RuinRetryPhase.Departure, "回声留在石壁上。");
                Progress = 1;
                return true;
            }
            ShowRuinRetryChoice();
            return true;
        }

        private void RememberRuinPortal(RuinPortal portal)
        {
            if (portal.Direction == RuinDirection.Forward || portal.Direction == RuinDirection.Exit ||
                portal.TargetRoom <= 0 || portal.TargetRoom >= RuinAtlas.RoomCount) return;
            ruinRetryRooms |= 1 << portal.TargetRoom;
            bool retainDiscard = ruinRetryRecord == RuinRetryRecordKind.HeartPutDown ||
                ruinRetryRecord == RuinRetryRecordKind.BrainPutDown;
            switch (portal.Direction)
            {
                case RuinDirection.Left:
                    ruinRetryLastLane = -1;
                    if (!retainDiscard) ruinRetryRecord = RuinRetryRecordKind.Left;
                    break;
                case RuinDirection.Right:
                    ruinRetryLastLane = 1;
                    if (!retainDiscard) ruinRetryRecord = RuinRetryRecordKind.Right;
                    break;
                case RuinDirection.Upper:
                    if (!retainDiscard) ruinRetryRecord = RuinRetryRecordKind.Upper;
                    break;
                case RuinDirection.Lower:
                    if (!retainDiscard) ruinRetryRecord = RuinRetryRecordKind.Lower;
                    break;
            }
            if (ruinRetryLastLane == 0) ruinRetryLastLane = portal.Direction == RuinDirection.Upper ? -1 : 1;
        }

        private void RememberRuinDiscard(RuinObjectKind kind)
        {
            if (kind == RuinObjectKind.Heart) ruinRetryRecord = RuinRetryRecordKind.HeartPutDown;
            else if (kind == RuinObjectKind.Brain) ruinRetryRecord = RuinRetryRecordKind.BrainPutDown;
        }

        private bool TryStartRuinRetry()
        {
            bool traversedGallery = RuinRoomId == 0 && ruinLoops >= 1 && RuinDisciplinesSeen == 1;
            float firstWallDepth = traversedGallery ? 5f : RuinRoomLength * .58f;
            if (ruinRetryWall || (!traversedGallery && RuinDisciplinesSeen < RuinDisciplineCount) ||
                Distance < firstWallDepth || Distance > RuinRoomLength - 4.5f) return false;
            float at = Distance + WallApproach;
            foreach (var portal in RuinPortals)
                if (Math.Abs(portal.Depth - at) < 3.2f) return false;

            RuinRetryWallDepth = at;
            ruinRetryWall = true;
            retryCollision = false;
            RuinApproach = null;
            Flight.FollowForestCourse(Flight.Height, .02f);
            SetRuinRetryPhase(RuinRetryPhase.Impact, "");
            Revision++;
            return true;
        }

        private void ShowRuinRetryChoice() => SetRuinRetryPhase(RuinRetryPhase.Choice,
            "系统已根据你的选择生成路线。是否按这条路线重试？");

        private void SetRuinRetryPhase(RuinRetryPhase phase, string message)
        {
            retryPhase = phase;
            retryFromDepth = Distance;
            retryFromLane = RuinLane;
            retryFromHeight = Flight.Height;
            Change(Stage.RuinRetry, message);
        }

        private void RetryRuinRetry()
        {
            retryDeparture = RuinRetryResolution.None;
            RuinLane = 0;
            RuinSteering = ruinSteeringSpeed = 0;
            Distance = RuinRetryWallDepth - WallStance;
            Flight.Reset(.025f, RuinHeightLimit);
            ShowRuinRetryChoice();
        }

        private void TouchRuinRetry(int option)
        {
            switch (retryPhase)
            {
                case RuinRetryPhase.Choice:
                    if (option == 0)
                    {
                        Notify(JourneyCue.RuinReplay);
                        SetRuinRetryPhase(RuinRetryPhase.Guided, "允许蓝线引导。沿着它飞过最暗的一段。");
                    }
                    else BeginRuinTrace();
                    break;
                case RuinRetryPhase.Guided:
                    if (option == 1 && Age >= GuidedTurnAt)
                    {
                        Notify(JourneyCue.Withdraw);
                        BeginRuinDeparture(RuinRetryResolution.Takeover, "你收回了引导，沿着墙缝里的暖色回声转向。");
                    }
                    break;
                case RuinRetryPhase.Trace:
                    retryInputLane = option == 0 ? -1 : 1;
                    if (RuinRetryReady) ResolveRuinTrace();
                    break;
                case RuinRetryPhase.Fault:
                    if (option == 0) RetryRuinRetry();
                    else BeginRuinTrace();
                    break;
            }
        }

        private void BeginRuinTrace()
        {
            if (RuinRetryAttempts < int.MaxValue) RuinRetryAttempts++;
            retryInputLane = 0;
            Notify(JourneyCue.RuinReplay);
            SetRuinRetryPhase(RuinRetryPhase.Trace, "你看见之前的自己再次飞过。找出它留下的暖色光迹。");
            Revision++;
        }

        private void ResolveRuinTrace()
        {
            if (retryInputLane != RuinRetryTraceLane)
            {
                RuinRetryLastAttemptLane = retryInputLane;
                FaultRuinRetry("墙面保留了这次选错的折线。旧影的暖色光迹会再亮一次。");
                return;
            }
            retryBeatAt = -1;
            retrySilence = 0;
            SetRuinRetryPhase(RuinRetryPhase.Listen, "你找回了自己走过的路线。先安静，听两下心跳。");
        }

        private void FaultRuinRetry(string message)
        {
            Notify(JourneyCue.RuinReplay);
            SetRuinRetryPhase(RuinRetryPhase.Fault, message);
        }

        private void BeginRuinDeparture(RuinRetryResolution outcome, string message)
        {
            retryDeparture = outcome;
            Notify(JourneyCue.RuinEscape);
            SetRuinRetryPhase(RuinRetryPhase.Departure, message);
        }

        private void UpdateRuinRetry(float dt)
        {
            switch (retryPhase)
            {
                case RuinRetryPhase.Impact:
                    UpdateRuinImpact(dt);
                    break;
                case RuinRetryPhase.Review:
                    Progress = Clamp(Age / ReviewDuration);
                    if (Progress >= 1) ShowRuinRetryChoice();
                    break;
                case RuinRetryPhase.Guided:
                    UpdateRuinGuidance(dt);
                    break;
                case RuinRetryPhase.Trace:
                    if (Gesture == Hand.Missing) { Age = Math.Max(0, Age - dt); break; }
                    Progress = Clamp(Age / TraceDuration);
                    if (RuinRetryReady && retryInputLane != 0) ResolveRuinTrace();
                    else if (Progress >= 1) FaultRuinRetry("旧影消散了。等另一轮光迹靠近后，再决定往哪一侧走。");
                    break;
                case RuinRetryPhase.Listen:
                    UpdateRuinListening(dt);
                    break;
                case RuinRetryPhase.Release:
                    UpdateRuinBreath(dt);
                    break;
                case RuinRetryPhase.Fault:
                    Progress = Clamp(Age / 1.45f);
                    if (Progress >= 1) BeginRuinTrace();
                    break;
                case RuinRetryPhase.Departure:
                    UpdateRuinDeparture(dt);
                    break;
            }
        }

        private void UpdateRuinImpact(float dt)
        {
            Progress = Clamp(Age / ImpactDuration);
            float approach = EaseRetry(0, ImpactAt, Age);
            float recoil = EaseRetry(ImpactAt, .92f, Age);
            float settle = EaseRetry(.43f, ImpactDuration, Age);
            Distance = RuinRetryWallDepth - WallApproach + 1.18f * approach - (WallStance - .47f) * recoil;
            float lift = Math.Max(0, (1 - settle) * retryFromHeight + .08f * (recoil - settle));
            MoveRuinRetryActor(dt, retryFromLane * (1 - .78f * settle), lift + .025f * settle);
            if (!retryCollision && Age >= ImpactAt)
            {
                retryCollision = true;
                Notify(JourneyCue.RuinWall);
            }
            if (Progress < 1) return;
            string memory;
            switch (ruinRetryRecord)
            {
                case RuinRetryRecordKind.HeartPutDown: memory = "墙里重现了刚才那一刻。你留下了心脏，让设备进了背包。"; break;
                case RuinRetryRecordKind.BrainPutDown: memory = "墙里重现了刚才那一刻。你留下了大脑，让设备进了背包。"; break;
                case RuinRetryRecordKind.Upper: memory = "墙里回放了你上升进入拱顶层道的转折。"; break;
                case RuinRetryRecordKind.Lower: memory = "墙里回放了你沿地面钻入低处层道的转折。"; break;
                case RuinRetryRecordKind.Left: memory = "墙里回放了你上一次选择左侧入口的路线。"; break;
                case RuinRetryRecordKind.Right: memory = "墙里回放了你上一次选择右侧入口的路线。"; break;
                default: memory = "墙里回放了一段走过的路线。系统正在用它推测你会往哪里走。"; break;
            }
            Notify(JourneyCue.RuinReplay);
            SetRuinRetryPhase(RuinRetryPhase.Review, memory);
        }

        private void UpdateRuinGuidance(float dt)
        {
            Progress = Clamp(Age / GuidedDuration);
            float lead = EaseRetry(0, 3.22f, Age);
            Distance = RuinRetryWallDepth - WallStance + .35f * lead;
            MoveRuinRetryActor(dt, retryFromLane + (-RuinRetryTraceLane * .61f - retryFromLane) * lead,
                .025f + .21f * lead);
            if (Age >= GuidedTurnAt && Age - dt < GuidedTurnAt)
            {
                Message = RuinRetryClue;
                Notify(JourneyCue.RuinRetryBeat);
            }
            if (Progress >= 1) BeginRuinDeparture(RuinRetryResolution.Guided, "蓝线引你抵达出口。引导在这里结束。");
        }

        private void UpdateRuinListening(float dt)
        {
            if (Gesture == Hand.Missing) { Age = Math.Max(0, Age - dt); return; }
            Progress = Clamp(Age / ListenDuration);
            Distance = RuinRetryWallDepth - WallStance - .25f * EaseRetry(0, .72f, Age);
            MoveRuinRetryActor(dt, retryFromLane + (RuinRetryTraceLane * .47f - retryFromLane) * EaseRetry(0, .62f, Age), .025f);
            if (retryBeatAt < 0 && Age >= .72f)
            {
                retrySilence = Voice <= .12f ? retrySilence + dt : 0;
                if (retrySilence >= SilenceReadyAt)
                {
                    retryBeatAt = Age;
                    Message = RuinRetryClue;
                    Notify(JourneyCue.RuinRetryBeat);
                }
            }
            if (retryBeatAt >= 0 && Age - retryBeatAt >= HeartTwoBeats)
            {
                retryVoice = 0;
                SetRuinRetryPhase(RuinRetryPhase.Release, "心跳落稳了。张手并轻声约半秒，让门和这个节拍对上（Z）。");
            }
            else if (Progress >= 1) FaultRuinRetry("机器声盖住了心跳。下一次先停声，等急促的回声消退。");
        }

        private void UpdateRuinBreath(float dt)
        {
            if (Gesture == Hand.Missing) { Age = Math.Max(0, Age - dt); return; }
            bool voicing = Gesture == Hand.Open && Voice >= .17f && Voice <= .95f;
            retryVoice = Math.Max(0, retryVoice + dt * (voicing ? 1 : -.8f));
            Progress = Clamp(retryVoice / .55f);
            if (Progress >= 1) BeginRuinDeparture(RuinRetryResolution.Remembered, "墙回响了你的声音。真正的白光从裂隙里亮起来。");
            else if (Age >= 4.10f) FaultRuinRetry("心跳的余音淡下去了。重试时听完两拍，再张手轻声。");
        }

        private void UpdateRuinDeparture(float dt)
        {
            Progress = Clamp(Age / DepartureDuration);
            float unlock = EaseRetry(.18f, DepartureDuration, Age);
            Distance = retryFromDepth + (RuinRetryWallDepth + 5.85f - retryFromDepth) * unlock;
            float exitLane = retryDeparture == RuinRetryResolution.Guided ? -RuinRetryTraceLane * .36f : RuinRetryTraceLane * .34f;
            float lane = retryFromLane + (exitLane - retryFromLane) * EaseRetry(0, 1.25f, Age);
            float altitude = retryFromHeight + (.36f - retryFromHeight) * EaseRetry(.18f, 1.65f, Age);
            MoveRuinRetryActor(dt, lane, altitude);
            if (Progress < 1) return;
            RuinRetryOutcome = retryDeparture;
            RuinResult = RuinPath.Explored;
            Complete();
        }

        private void MoveRuinRetryActor(float dt, float lane, float height)
        {
            RuinSteering = Math.Max(-1, Math.Min(1, (lane - RuinLane) / (dt * 4)));
            RuinLane = lane;
            Flight.FollowForestCourse(height, dt);
        }

        private static float HeartFlash(float t, float center) => 1 - EaseRetry(.024f, .22f, Math.Abs(t - center));
        private static float EaseRetry(float from, float to, float time)
        {
            float value = Clamp((time - from) / (to - from));
            return value * value * (3 - 2 * value);
        }
    }
}
