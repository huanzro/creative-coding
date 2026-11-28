/* ARTISTIC RATIONALE
 * Expose the structure that guided the player, then have its operator assign a price to leaving.
 * The player can trade their own heart and brain or keep them. Both replies open the sky.
 */
using System;

namespace Echoes
{
    public enum RuinRetryRecordKind { None, Left, Right, Upper, Lower, HeartPutDown, BrainPutDown }
    public enum RuinRetryPhase
    {
        Inactive, Impact, Gate, Survey, Arrival, Valuation, Bargain, Accepted, Declined
    }
    // Keep the historic numeric values readable in checkpoints.
    public enum RuinRetryResolution { None, Guided, Takeover, Remembered, Traded, Kept }

    public sealed partial class Journey
    {
        private const float WallApproach = 1.65f;
        private const float WallStance = 1.06f;
        private const float ImpactAt = .43f;
        private const float ImpactDuration = 1.31f;
        public const float RuinGateDuration = 3.2f;
        public const float RuinSurveyDuration = 10f;
        public const float RuinArrivalDuration = 3.8f;
        public const float RuinValuationFirstLineDuration = 8.6f, RuinValuationDuration = 16f;
        public const float RuinExitDuration = 10f;
        public const int RuinHeartCoinRate = 3, RuinBrainCoinRate = 4, RuinPhoneTokenRate = 5, RuinComputerTokenRate = 6;

        private int ruinRetryRooms, ruinRetryLastLane, ruinWallPassages;
        private bool ruinRetryWall, retryCollision;
        private RuinRetryRecordKind ruinRetryRecord;
        private RuinRetryPhase retryPhase;
        private float retryFromLane, retryFromHeight;
        private float lookYawVelocity, lookPitchVelocity;
        private bool surveySawClosedHand;
        internal int RuinExitPortalRoom { get; private set; } = -1;

        public RuinRetryPhase RuinRetryState => Phase == Stage.RuinRetry ? retryPhase : RuinRetryPhase.Inactive;
        public RuinRetryRecordKind RuinRetryRecord => ruinRetryRecord;
        public RuinRetryResolution RuinRetryOutcome { get; private set; }
        public int RuinRetryVisited => CountRuinPassages(ruinRetryRooms);
        public int RuinWallPassages => ruinWallPassages;
        public int RuinWallPassagesRequired => 6 + (ruinSeed & 3);
        private bool RuinWallReady => ruinWallPassages >= RuinWallPassagesRequired;
        public int RuinRetryAttempts { get; private set; }
        public float RuinRetryWallDepth { get; private set; }
        public float RuinLookYaw { get; private set; }
        public float RuinLookPitch { get; private set; }
        public bool RuinDirectPalmLook { get; set; } = true;
        public static int RuinCoinValue(RuinObjectKind kind) => kind switch
        {
            RuinObjectKind.Heart => RuinHeartCoinRate,
            RuinObjectKind.Brain => RuinBrainCoinRate,
            RuinObjectKind.Phone => RuinPhoneTokenRate,
            RuinObjectKind.Computer => RuinComputerTokenRate,
            _ => 0
        };
        public int RuinCoins => RuinHearts * RuinHeartCoinRate + RuinBrains * RuinBrainCoinRate +
            RuinPhones * RuinPhoneTokenRate + RuinComputers * RuinComputerTokenRate;
        public RuinObjectKind? RuinRetainedKind => RuinHearts > 0 ? RuinObjectKind.Heart :
            RuinBrains > 0 ? RuinObjectKind.Brain : (RuinObjectKind?)null;
        public int RuinTokenCost => RuinCoins - (RuinRetainedKind.HasValue ? RuinCoinValue(RuinRetainedKind.Value) : 0);
        public int RuinOfferedTokens => RuinCoins;
        public int RuinTradeQuote => RuinTokenCost;
        public int RuinTradePhones => RuinPhones;
        public int RuinTradeComputers => RuinComputers;
        public bool RuinTradeCommitted => RuinRetryOutcome == RuinRetryResolution.Traded;
        public int RuinRevealedTokens => retryPhase == RuinRetryPhase.Valuation
            ? (int)Math.Round(RuinOfferedTokens * EaseRetry(.8f, RuinValuationFirstLineDuration, Age))
            : retryPhase == RuinRetryPhase.Accepted && Age >= 2.55f ? RuinTokenCost : RuinOfferedTokens;

        public string RuinRetryLeftLabel => retryPhase == RuinRetryPhase.Survey ? "A · 向左看" :
            retryPhase == RuinRetryPhase.Bargain ? "A · 同意交换" : "";
        public string RuinRetryRightLabel => retryPhase == RuinRetryPhase.Survey ? "B · 向右看" :
            retryPhase == RuinRetryPhase.Bargain ? "B · 拒绝交换" : "";

        public string RuinRetryClue => Message;

        private static int CountRuinPassages(int mask)
        {
            int count = 0;
            for (; mask != 0; mask &= mask - 1) count++;
            return count;
        }

        private void ResetRuinRetry()
        {
            ruinRetryRooms = ruinRetryLastLane = ruinWallPassages = RuinRetryAttempts = 0;
            RuinExitPortalRoom = -1;
            ruinRetryWall = retryCollision = false;
            ruinRetryRecord = RuinRetryRecordKind.None;
            retryPhase = RuinRetryPhase.Inactive;
            RuinRetryOutcome = RuinRetryResolution.None;
            RuinRetryWallDepth = 0;
            ResetRuinLook();
        }

        private void RestoreRuinRetry(JourneyCheckpoint checkpoint)
        {
            RuinExitPortalRoom = -1;
            ruinRetryRooms = checkpoint.ruinRetryRooms & 65534;
            ruinWallPassages = Math.Min(RuinWallPassagesRequired, Math.Max(checkpoint.ruinWallPassages, RuinRetryVisited));
            ruinRetryRecord = (uint)checkpoint.ruinRetryRecord <= (uint)RuinRetryRecordKind.BrainPutDown
                ? (RuinRetryRecordKind)checkpoint.ruinRetryRecord : RuinRetryRecordKind.None;
            ruinRetryLastLane = Math.Sign(checkpoint.ruinRetryLastLane);
            RuinRetryAttempts = Math.Max(0, checkpoint.ruinRetryAttempts);
            float exitLength = RuinAtlas.PreviousExitLength * RuinAtlas.GalleryLengthMultiplier;
            ruinRetryWall = checkpoint.ruinRetryWall && (checkpoint.ruinRetryDepth == RuinAtlas.PreviousExitLength ||
                checkpoint.ruinRetryDepth == exitLength);
            RuinRetryWallDepth = ruinRetryWall ? exitLength : 0;
            RuinRetryOutcome = (uint)checkpoint.ruinRetryOutcome <= (uint)RuinRetryResolution.Kept
                ? (RuinRetryResolution)checkpoint.ruinRetryOutcome : RuinRetryResolution.None;
        }

        private bool ResumeRuinRetry()
        {
            if (!ruinRetryWall) return false;
            RuinLane = 0;
            Flight.Reset(.025f, RuinHeightLimit);
            Distance = RuinRetryWallDepth - WallStance;
            if (RuinRetryOutcome == RuinRetryResolution.Traded || RuinRetryOutcome == RuinRetryResolution.Kept)
                BeginRuinExit(RuinRetryOutcome == RuinRetryResolution.Traded);
            else
                SetRuinRetryPhase(RuinRetryPhase.Gate, "");
            return true;
        }

        private void RememberRuinPortal(RuinPortal portal)
        {
            if ((uint)portal.TargetRoom >= RuinAtlas.RoomCount) return;
            ruinWallPassages = Math.Min(ruinWallPassages + 1, RuinWallPassagesRequired);
            if (portal.TargetRoom == 0 || portal.Direction == RuinDirection.Forward ||
                portal.Direction == RuinDirection.Exit) return;
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

        private void ArrangeRuinExitRoutes()
        {
            if (ruinRoomId == RuinAtlas.ExitRoom || ruinWallPassages != RuinWallPassagesRequired - 1 ||
                RuinExitPortalRoom == ruinRoomId) return;
            // The selected entrance extends seamlessly into the full-length terminal gallery.
            foreach (RuinPortal portal in CurrentRuinRoom.Portals)
                portal.TargetRoom = RuinAtlas.ExitRoom;
            RuinExitPortalRoom = ruinRoomId;
        }

        private void RememberRuinDiscard(RuinObjectKind kind)
        {
            if (kind == RuinObjectKind.Heart) ruinRetryRecord = RuinRetryRecordKind.HeartPutDown;
            else if (kind == RuinObjectKind.Brain) ruinRetryRecord = RuinRetryRecordKind.BrainPutDown;
        }

        private bool TryStartRuinRetry()
        {
            if (ruinRetryWall || !RuinWallReady || ruinRoomId != RuinAtlas.ExitRoom ||
                Distance < RuinRoomLength - WallApproach || RuinForwardSpeed <= 0) return false;
            StartRuinWallImpact(RuinRoomLength);
            return true;
        }

        public void JumpToRuinWallDebugPoint()
        {
            if (!InRuins) SelectChapter(1);
            if (RuinInventory == 0)
            {
                AddRuinFind(RuinObjectKind.Heart);
                AddRuinFind(RuinObjectKind.Brain);
                AddRuinFind(RuinObjectKind.Phone);
                AddRuinFind(RuinObjectKind.Computer);
            }
            SetHand(Hand.Open);
            RuinRetryOutcome = RuinRetryResolution.None;
            ruinWallPassages = RuinWallPassagesRequired;
            EnterRuinRoom(RuinAtlas.ExitRoom, false);
            Distance = RuinRoomLength - WallApproach;
            StartRuinWallImpact(RuinRoomLength);
        }

        private void StartRuinWallImpact(float wallDepth)
        {
            RuinRetryWallDepth = wallDepth;
            ruinRetryWall = true;
            retryCollision = false;
            RuinApproach = null;
            RuinSearchTarget = RuinNearbyClue = null;
            RuinForwardSpeed = 0;
            Flight.FollowForestCourse(Flight.Height, .02f);
            SetRuinRetryPhase(RuinRetryPhase.Impact, "");
            Revision++;
        }

        private void SetRuinRetryPhase(RuinRetryPhase phase, string message)
        {
            retryPhase = phase;
            retryFromLane = RuinLane;
            retryFromHeight = Flight.Height;
            Change(Stage.RuinRetry, message);
        }

        private void RetryRuinRetry()
        {
            if (RuinRetryOutcome == RuinRetryResolution.Traded || RuinRetryOutcome == RuinRetryResolution.Kept ||
                retryPhase == RuinRetryPhase.Bargain) return;
            ResetRuinLook();
            StopRuinSteering();
            RuinLane = 0;
            Distance = RuinRetryWallDepth - WallStance;
            Flight.Reset(.025f, RuinHeightLimit);
            SetRuinRetryPhase(RuinRetryPhase.Gate, "");
        }

        private bool TouchRuinRetry(int option)
        {
            if (retryPhase != RuinRetryPhase.Bargain) return false;
            RuinRetryOutcome = option == 0 ? RuinRetryResolution.Traded : RuinRetryResolution.Kept;
            BeginRuinExit(option == 0);
            Revision++;
            return true;
        }

        private void BeginRuinExit(bool accepted)
        {
            SetRuinRetryPhase(accepted ? RuinRetryPhase.Accepted : RuinRetryPhase.Declined,
                accepted ? "交易确认。请留在光环中央。" : "你的大脑和我比不过是玩具");
            Notify(accepted ? JourneyCue.RuinTradeAccepted : JourneyCue.RuinTradeDeclined);
        }

        private void ResetRuinLook()
        {
            RuinLookYaw = RuinLookPitch = lookYawVelocity = lookPitchVelocity = 0;
            surveySawClosedHand = false;
        }

        private void UpdateRuinRetry(float dt)
        {
            switch (retryPhase)
            {
                case RuinRetryPhase.Impact:
                    UpdateRuinImpact(dt);
                    break;
                case RuinRetryPhase.Gate:
                    Progress = Clamp(Age / RuinGateDuration);
                    float crossing = EaseRetry(.8f, 2.9f, Age);
                    Distance = RuinRetryWallDepth - WallStance + crossing * 3.18f;
                    MoveRuinRetryActor(dt, retryFromLane * (1 - EaseRetry(0, .7f, Age)), .025f);
                    if (Progress >= 1)
                    {
                        ResetRuinLook();
                        SetRuinRetryPhase(RuinRetryPhase.Survey, "");
                        Notify(JourneyCue.RuinSurvey);
                    }
                    break;
                case RuinRetryPhase.Survey:
                    UpdateRuinSurvey(dt);
                    if (Age + .0001f >= RuinSurveyDuration)
                    {
                        SetRuinRetryPhase(RuinRetryPhase.Arrival, "");
                        Notify(JourneyCue.RuinBroker);
                    }
                    break;
                case RuinRetryPhase.Arrival:
                    Progress = Clamp(Age / RuinArrivalDuration);
                    if (Progress >= 1)
                    {
                        SetRuinRetryPhase(RuinRetryPhase.Valuation, "我会把每一件藏品换算成金币。先看看你一路带来了什么。");
                        Notify(JourneyCue.RuinValuation);
                    }
                    break;
                case RuinRetryPhase.Valuation:
                    Progress = Clamp(Age / RuinValuationDuration);
                    if (Age >= RuinValuationFirstLineDuration)
                        Message = RuinCoins == 0 ? "你没有带来藏品，可以免费离开。" :
                            $"你有 {RuinCoins} 枚金币。离开需要 {RuinTokenCost} 枚金币。";
                    if (Progress >= 1)
                    {
                        SetRuinRetryPhase(RuinRetryPhase.Bargain, "我可以放你过去。你愿意把自己的心脏和大脑也交给我吗？");
                        Notify(JourneyCue.RuinOffer);
                    }
                    break;
                case RuinRetryPhase.Accepted:
                case RuinRetryPhase.Declined:
                    UpdateRuinExit();
                    break;
            }
        }

        private void UpdateRuinImpact(float dt)
        {
            Progress = Clamp(Age / ImpactDuration);
            float approach = EaseRetry(0, ImpactAt, Age);
            float recoil = EaseRetry(ImpactAt, .92f, Age);
            float settle = EaseRetry(ImpactAt, ImpactDuration, Age);
            Distance = RuinRetryWallDepth - WallApproach + 1.18f * approach - (WallStance - .47f) * recoil;
            float lift = Math.Max(0, (1 - settle) * retryFromHeight + .08f * (recoil - settle));
            MoveRuinRetryActor(dt, retryFromLane * (1 - .78f * settle), lift + .025f * settle);
            if (!retryCollision && Age >= ImpactAt)
            {
                retryCollision = true;
                Notify(JourneyCue.RuinWall);
            }
            if (Progress < 1) return;
            SetRuinRetryPhase(RuinRetryPhase.Gate, "");
            Notify(JourneyCue.RuinDoor);
        }

        private void UpdateRuinSurvey(float dt)
        {
            Progress = Clamp(Age / RuinSurveyDuration);
            if (Gesture != Hand.Open) surveySawClosedHand = true;
            float response = 1 - (float)Math.Exp(-dt / .19f);
            float requestedYaw = ruinSteerHeld * 64f;
            bool palmUp = Gesture == Hand.Open && (RuinDirectPalmLook || surveySawClosedHand);
            float requestedPitch = PoseVoice >= .16f ? -42f : palmUp ? 33f : 0;
            lookYawVelocity += (requestedYaw - lookYawVelocity) * response;
            lookPitchVelocity += (requestedPitch - lookPitchVelocity) * response;
            RuinLookYaw += lookYawVelocity * dt;
            if (RuinLookYaw > 180) RuinLookYaw -= 360;
            else if (RuinLookYaw < -180) RuinLookYaw += 360;
            RuinLookPitch = Math.Max(-68, Math.Min(74, RuinLookPitch + lookPitchVelocity * dt));
        }

        private void UpdateRuinExit()
        {
            Progress = Clamp(Age / RuinExitDuration);
            if (retryPhase == RuinRetryPhase.Declined)
                Message = Age < 2.15f ? "你的大脑和我比不过是玩具" :
                    Age < 4.12f ? "你的心脏终有停跳的那一天" : Age < 6f ? "何必如此执着呢" : "";
            else if (Age >= 2.55f)
                Message = Age < 5.2f ? "金币已付清。出口开放。" : "";

            if (Age + .0001f < RuinExitDuration) return;
            RuinResult = RuinPath.Explored;
            Enter(2, false, false);
        }

        private void MoveRuinRetryActor(float dt, float lane, float height)
        {
            RuinSteering = Math.Max(-1, Math.Min(1, (lane - RuinLane) / (dt * 4)));
            RuinLane = lane;
            Flight.FollowForestCourse(height, dt);
        }

        private static float EaseRetry(float from, float to, float time)
        {
            float value = Clamp((time - from) / (to - from));
            return value * value * (3 - 2 * value);
        }
    }
}
