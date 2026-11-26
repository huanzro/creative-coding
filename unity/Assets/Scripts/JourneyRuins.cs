using System;
using System.Collections.Generic;

namespace Echoes
{
    public readonly struct RuinTransferMetrics
    {
        public readonly float Distance, SourceScale, DestinationScale;
        public readonly Func<float, float> SpeedLimit;
        public RuinTransferMetrics(float distance, float sourceScale, float destinationScale, Func<float, float> speedLimit = null)
        {
            Distance = distance;
            SourceScale = sourceScale;
            DestinationScale = destinationScale;
            SpeedLimit = speedLimit;
        }
    }

    public sealed partial class Journey
    {
        public const int RuinCapacity = 15;
        public const string RuinPhoneName = "ipone18p";
        public const int RuinDisciplineCount = 9;
        public const float RuinHeightLimit = 1.65f;
        public const float RuinEntryAltitude = .072f;
        public const int RuinRoomCount = RuinAtlas.RoomCount;
        public const float RuinMinimumReach = .65f;
        public const float RuinMaximumReach = RuinPortal.MaximumReach;
        private const float RuinSideLimit = 1.32f;
        private const float RuinSideSpeed = 1.55f;
        private const float HeartStartleTime = 1.45f;
        private const float EntryAltitude = .17f;
        private const float RuinVoiceDeadZone = .12f;
        private const float RuinFastSpeed = 4.2f;
        private static readonly string[] RuinObjectLabels = { "心脏", "大脑", RuinPhoneName, "电脑", "宝箱" };

        private RuinRoom[] ruinRooms;
        private int ruinSeed, ruinVisited, ruinRoomId, ruinLoops, ruinSteerHeld;
        private ulong ruinFinds, ruinFindsExtra, ruinOpenLow, ruinOpenHigh, ruinExhibitOrder;
        private float ruinHeartAt, ruinSteeringSpeed, ruinPassageSpeed;
        private RuinTransferMetrics ruinTransferMetrics;
        private bool ruinHeartSeen;
        private string ruinHint;

        public bool InRuins => Chapter == 1 && Lesson < 0;
        private bool IsRuinSequence => InRuins && Phase == Stage.RuinTransfer;
        private RuinRoom CurrentRuinRoom => ruinRooms == null ? null : ruinRooms[ruinRoomId];
        public int RuinRoomId => ruinRoomId;
        public int RuinVisualSeed => ruinSeed;
        public int RuinDiscipline => RuinDisciplineFor(ruinRoomId);
        public int RuinDisciplinesSeen
        {
            get
            {
                int count = 0;
                for (int room = 0; room < RuinRoomCount; room++)
                {
                    int stored = StoredRuinDiscipline(room);
                    if (stored < RuinDisciplineCount) count = Math.Max(count, stored + 1);
                }
                return count;
            }
        }

        public int RuinDisciplineFor(int room)
        {
            int assigned = StoredRuinDiscipline(room);
            if (assigned >= 0 && assigned < RuinDisciplineCount) return assigned;
            int seen = RuinDisciplinesSeen;
            return seen < RuinDisciplineCount ? seen : room % RuinDisciplineCount;
        }

        private int StoredRuinDiscipline(int room) => (int)((ruinExhibitOrder >> (room * 4)) & 15UL) - 1;

        private void AssignRuinDiscipline(int room)
        {
            int stored = StoredRuinDiscipline(room);
            if (stored >= 0 && stored < RuinDisciplineCount) return;
            int shift = room * 4;
            ruinExhibitOrder = (ruinExhibitOrder & ~(15UL << shift)) | ((ulong)(RuinDisciplineFor(room) + 1) << shift);
        }
        public string RuinRoomName => CurrentRuinRoom?.Name ?? "";
        public int RuinLayer => CurrentRuinRoom?.Layer ?? 0;
        public int RuinRoomStyle => CurrentRuinRoom?.Style ?? 0;
        public float RuinRoomLength => CurrentRuinRoom?.Length ?? 48;
        public IReadOnlyList<RuinPortal> RuinPortals => CurrentRuinRoom?.Portals ?? Array.Empty<RuinPortal>();
        public IReadOnlyList<RuinObject> RuinObjects => CurrentRuinRoom?.Objects ?? Array.Empty<RuinObject>();
        public int RuinVisitedCount { get; private set; }
        public int RuinLoops => ruinLoops;
        public float RuinPassageStartDepth { get; private set; }
        public float RuinPassageStartLane { get; private set; }
        public float RuinPassageStartAltitude { get; private set; }
        public float RuinPassageStartForwardSpeed { get; private set; }
        public float RuinPassageStartLateralSpeed { get; private set; }
        public float RuinPassageStartVerticalSpeed { get; private set; }
        public Func<Journey, RuinTransferMetrics> MeasureRuinPassage { get; set; }
        public float RuinLeftReach { get; set; } = 1;
        public float RuinRightReach { get; set; } = 1;
        public float RuinUpperReach { get; set; } = 1;
        public float RuinLowerReach { get; set; } = 1;
        public float RuinLane { get; private set; }
        public float RuinSteering { get; private set; }
        public float RuinForwardSpeed { get; private set; }
        public RuinObject RuinSearchTarget { get; private set; }
        public RuinObject RuinNearbyClue { get; private set; }
        public bool RuinCanTake => Phase == Stage.RuinExplore && !RuinLootOpen && RuinSearchTarget != null &&
            !RuinChestOpening && RuinSearchTarget.Kind == RuinObjectKind.Chest;
        public string RuinTakeLabel => "B · 打开宝箱";
        public RuinPortal RuinApproach { get; private set; }
        public RuinPortal RuinUpperApproach => Phase == Stage.RuinExplore && !RuinWallReady ? FindRuinUpperPreview() : null;
        public RuinPortal RuinEntrance { get; private set; }
        public bool RuinCanChoosePassage => IsRuinEntranceInRange(RuinApproach);
        public bool IsRuinEntranceInRange(RuinPortal portal) => portal != null && Phase == Stage.RuinExplore &&
            !RuinLootOpen && !RuinWallReady && InRuinPortalRange(portal);
        public float RuinDoorOpening => RuinEntrance != null && RuinEntrance.OpenAge >= 0
            ? Clamp(RuinEntrance.OpenAge / .30f) * (1 - Clamp((RuinEntrance.OpenAge - .36f) / .55f)) : 0;
        public int RuinHearts { get; private set; }
        public int RuinBrains { get; private set; }
        public int RuinPhones { get; private set; }
        public int RuinComputers { get; private set; }
        public int RuinChests { get; private set; }
        public int RuinInventory => RuinHearts + RuinBrains + RuinPhones + RuinComputers;
        public float RuinHeartAge => PoseTime - ruinHeartAt < HeartStartleTime ? PoseTime - ruinHeartAt : -1;

        public float RuinRoomLengthFor(int room) => ruinRooms?[room].Length ?? 48;
        public int RuinRoomStyleFor(int room) => ruinRooms?[room].Style ?? 0;
        public int RuinRoomLayerFor(int room) => ruinRooms?[room].Layer ?? 0;
        public IReadOnlyList<RuinPortal> RuinPortalsFor(int room) => ruinRooms?[room].Portals ?? Array.Empty<RuinPortal>();
        public IReadOnlyList<RuinObject> RuinObjectsFor(int room) => ruinRooms?[room].Objects ?? Array.Empty<RuinObject>();

        private void ResetRuinHistory()
        {
            ResetRuinRetry();
            ResetRuinLootHistory();
            ruinSeed = forestSeed ^ 0x27B4E2F1;
            ruinRooms = null;
            ruinRoomId = ruinVisited = 0;
            ruinLoops = 0;
            ruinFinds = ruinFindsExtra = ruinOpenLow = ruinOpenHigh = ruinExhibitOrder = 0;
            ruinHeartSeen = false;
            ruinHeartAt = float.NegativeInfinity;
            RuinVisitedCount = RuinHearts = RuinBrains = RuinPhones = RuinComputers = RuinChests = 0;
        }

        private void StartRuins()
        {
            if (ruinRooms == null)
            {
                ruinRooms = RuinAtlas.Build(ruinSeed);
                RestoreRuinFinds();
                RestoreRuinLootFinds();
                RestoreRuinMemories();
            }
            ruinHeartAt = float.NegativeInfinity;
            if (ruinRoomId == RuinAtlas.ExitRoom && !ruinRetryWall) ruinWallPassages = RuinWallPassagesRequired;
            else if (RuinWallReady && !ruinRetryWall)
                ruinWallPassages = RuinWallPassagesRequired - 1;
            EnterRuinRoom(ruinRoomId, true);
            ResumeRuinRetry();
        }

        private void EnterRuinRoom(int room, bool atThreshold)
        {
            ClearRuinLoot();
            ruinRoomId = room;
            ArrangeRuinExitRoutes();
            // Room zero anchors the sequence when upgrading an older checkpoint.
            AssignRuinDiscipline(0);
            AssignRuinDiscipline(room);
            if ((ruinVisited & (1 << room)) == 0)
            {
                ruinVisited |= 1 << room;
                RuinVisitedCount++;
            }
            Distance = 0;
            RuinLane = 0;
            RuinForwardSpeed = 0;
            StopRuinSteering();
            RuinSearchTarget = RuinNearbyClue = null;
            RuinApproach = RuinEntrance = null;
            foreach (var item in CurrentRuinRoom.Objects) item.Reveal = 0;
            float entryHeight = atThreshold && room == 0 ? EntryAltitude : RuinEntryAltitude;
            Flight.Reset(entryHeight, RuinHeightLimit);
            ruinHint = room == 0 ? "发声前进，停声停下。按住 A / B 自由横移，握拳后张手上升。" :
                CurrentRuinRoom.Style == 5 ? "墙后还有空间。沿两侧的微光找一找。" :
                "发声前进。可在岔口换层，也可停下来查看墙边。";
            Change(Stage.RuinExplore, ruinHint);
        }

        public void SetRuinSteering(bool left, bool right)
        {
            ruinSteerHeld = left == right ? 0 : left ? -1 : 1;
        }

        private void StopRuinSteering()
        {
            RuinSteering = ruinSteeringSpeed = 0;
            ruinSteerHeld = 0;
        }

        private void RuinTouch(int option)
        {
            if (option == 1 && RuinCanTake) TakeRuinCache(RuinSearchTarget);
        }

        private float RuinReach(RuinDirection direction)
        {
            float reach;
            switch (direction)
            {
                case RuinDirection.Left: reach = RuinLeftReach; break;
                case RuinDirection.Right: reach = RuinRightReach; break;
                case RuinDirection.Upper: reach = RuinUpperReach; break;
                default: reach = RuinLowerReach; break;
            }
            return float.IsNaN(reach) ? 1 : Math.Max(RuinMinimumReach, Math.Min(RuinMaximumReach, reach));
        }

        private bool InRuinPortalRange(RuinPortal portal, float preview = 1)
        {
            float reach = RuinReach(portal.Direction) * preview;
            float ahead = portal.Depth - Distance;
            float depthReach = reach * RuinAtlas.LengthScale(ruinRoomId);
            if (ahead < -RuinPortal.TailDepth * depthReach || ahead > RuinPortal.LeadDepth * depthReach) return false;

            float lateral = Math.Abs(RuinLane - portal.Lane);
            if (portal.Direction == RuinDirection.Upper)
                return lateral <= 1.12f * reach &&
                    (preview > 1 || Flight.Height >= Math.Max(.36f, portal.Altitude - .82f * reach));
            if (portal.Direction == RuinDirection.Lower)
                return lateral <= 1.12f * reach && Flight.Height <= portal.Altitude + .63f * reach;
            // Side entrances receive the current turn direction across the full playable flight height.
            return lateral <= Math.Min(1.90f, 1.22f * reach);
        }

        private RuinPortal FindRuinUpperPreview()
        {
            RuinPortal nearest = null;
            float best = float.PositiveInfinity;
            foreach (var portal in RuinPortals)
            {
                if (portal.Direction != RuinDirection.Upper || portal.Hidden) continue;
                float ahead = portal.Depth - Distance;
                if (ahead < -1.4f || ahead > 10f) continue;
                float score = Math.Abs(ahead);
                if (score >= best) continue;
                nearest = portal;
                best = score;
            }
            return nearest;
        }

        private RuinPortal FindRuinPortal(RuinDirection direction)
        {
            RuinPortal nearest = null;
            foreach (var portal in RuinPortals)
                if (portal.Direction == direction && InRuinPortalRange(portal) &&
                    (nearest == null || Math.Abs(portal.Depth - Distance) < Math.Abs(nearest.Depth - Distance)))
                    nearest = portal;
            return nearest;
        }

        private void OpenRuinGate(RuinPortal gate)
        {
            gate.Opened = true;
            gate.OpenAge = 0;
            if (gate.Id < 64) ruinOpenLow |= 1UL << gate.Id;
            else ruinOpenHigh |= 1UL << (gate.Id - 64);
        }

        private void UpdateRuins(float dt, bool wasDescending = false)
        {
            foreach (var portal in CurrentRuinRoom.Portals)
                if (portal.OpenAge >= 0) portal.OpenAge = Math.Min(10, portal.OpenAge + dt);

            if (IsRuinSequence)
            {
                AdvanceRuinTransfer(dt);
                return;
            }
            if (AdvanceAutomaticRuinChest(dt)) return;
            if (RuinLootOpen)
            {
                if (RuinLootSource != null && RuinLootSource.OpenAge >= 0)
                    RuinLootSource.OpenAge = Math.Min(10, RuinLootSource.OpenAge + dt);
                return;
            }

            UpdateRuinSteering(dt);
            RuinForwardSpeed = ApproachRuinSpeed(RuinForwardSpeed, RuinVoiceSpeed, 5.6f * dt, 18f * dt);
            Distance += RuinForwardSpeed * dt;
            if (TryStartRuinRetry()) return;
            if (Distance >= RuinRoomLength)
            {
                Distance %= RuinRoomLength;
                ruinLoops = Math.Min(ruinLoops + 1, 999);
                RuinApproach = null;
                Revision++;
            }

            if (!RuinWallReady)
            {
                RuinPortal route = ruinSteerHeld == 0 ? null :
                    FindRuinPortal(ruinSteerHeld < 0 ? RuinDirection.Left : RuinDirection.Right);
                if (route == null && Gesture == Hand.Open) route = FindRuinPortal(RuinDirection.Upper);
                if (route == null && (Flight.Speed < -.035f || wasDescending && Flight.Height <= .03f))
                    route = FindRuinPortal(RuinDirection.Lower);
                if (route != null)
                {
                    TravelRuins(route);
                    return;
                }
            }

            if (!RuinWallReady) UpdateRuinApproach();
            UpdateRuinSearch(dt);
        }

        private void UpdateRuinApproach()
        {
            RuinPortal approached = null;
            float bestScore = float.PositiveInfinity;
            foreach (var portal in CurrentRuinRoom.Portals)
            {
                bool available = InRuinPortalRange(portal);
                if (!available && !InRuinPortalRange(portal, 1.82f)) continue;
                float depth = Math.Abs(portal.Depth - Distance);
                float lateral = Math.Abs(RuinLane - portal.Lane);
                float vertical = Math.Abs(Flight.Height - portal.Altitude);
                float score = (available ? 0 : 20) + depth + lateral * 1.55f + vertical * 2;
                if (score >= bestScore) continue;
                approached = portal;
                bestScore = score;
            }
            if (approached == RuinApproach) return;
            RuinApproach = approached;
            if (approached != null)
                Message = approached.Hidden ? "裂缝里有光，还有一层空间。" : "前面有一条岔道。";
            else if (CueAge > 1.2f) Message = ruinHint;
        }

        private float RuinVoiceSpeed => RuinFastSpeed * Clamp((Voice - RuinVoiceDeadZone) / .76f);

        private static float ApproachRuinSpeed(float speed, float target, float accel, float brake) =>
            speed + Math.Max(-brake, Math.Min(accel, target - speed));

        private void UpdateRuinSteering(float dt)
        {
            const float easingAtWalls = .24f;
            const float coastGrip = 5.3f;
            const float turnGrip = 6.8f;
            float space = RuinSideLimit - RuinLane * ruinSteerHeld;
            float wanted = RuinSideSpeed * ruinSteerHeld * Clamp(space / easingAtWalls);
            float previous = ruinSteeringSpeed;
            // Releasing keeps lateral momentum. Reversing first slows it through zero.
            ruinSteeringSpeed += (wanted - ruinSteeringSpeed) * Follow(dt, ruinSteerHeld == 0 ? coastGrip : turnGrip);
            if (Math.Abs(ruinSteeringSpeed) < .004f) ruinSteeringSpeed = 0;
            float lane = RuinLane + (previous + ruinSteeringSpeed) * .5f * dt;
            RuinLane = Math.Max(-RuinSideLimit, Math.Min(RuinSideLimit, lane));
            if (lane != RuinLane) ruinSteeringSpeed = 0;
            RuinSteering = ruinSteeringSpeed / RuinSideSpeed;
        }

        private void TravelRuins(RuinPortal portal)
        {
            RuinPassageStartDepth = Distance;
            RuinPassageStartLane = RuinLane;
            RuinPassageStartAltitude = Flight.Height;
            RuinPassageStartForwardSpeed = RuinForwardSpeed;
            RuinPassageStartLateralSpeed = ruinSteeringSpeed;
            RuinPassageStartVerticalSpeed = Flight.Speed;
            RuinEntrance = portal;
            RuinApproach = null;
            RuinSearchTarget = RuinNearbyClue = null;
            ruinTransferMetrics = MeasureRuinPassage?.Invoke(this) ?? new RuinTransferMetrics(26, 2.75f, 2.75f);
            float forward = RuinForwardSpeed * ruinTransferMetrics.SourceScale;
            float lateral = ruinSteeringSpeed * 2.45f;
            float vertical = Flight.Speed * 3.30f;
            ruinPassageSpeed = (float)Math.Sqrt(forward * forward + lateral * lateral + vertical * vertical);
            if (!portal.Opened)
            {
                OpenRuinGate(portal);
                Notify(JourneyCue.RuinDoor);
                Revision++;
            }
            else Notify(JourneyCue.RuinTravel);
            Change(Stage.RuinTransfer, "沿岔道进入下一层。");
        }

        private void AdvanceRuinTransfer(float dt)
        {
            float distance = ruinTransferMetrics.Distance;
            float bank = Clamp(Age / .85f);
            RuinSteering = RuinPassageStartLateralSpeed / RuinSideSpeed * (1 - bank * bank * (3 - 2 * bank));
            float sourceVoice = RuinVoiceSpeed * ruinTransferMetrics.SourceScale;
            float arrivalVoice = RuinVoiceSpeed * ruinTransferMetrics.DestinationScale;
            float cruise = Math.Max(sourceVoice, Math.Min(10.2f, Math.Max(3.6f, distance * .085f)));
            float brakingDistance = Math.Max(8f, Math.Min(17f, distance * .20f));
            float outside = Clamp(distance * (1 - Progress) / brakingDistance);
            float glide = outside * outside * (3 - 2 * outside);
            float arrival = Math.Max(1.2f, arrivalVoice);
            float target = arrival + (cruise - arrival) * glide;
            if (ruinTransferMetrics.SpeedLimit != null)
                target = Math.Min(target, Math.Max(.85f, ruinTransferMetrics.SpeedLimit(Progress)));
            float oldSpeed = ruinPassageSpeed;
            ruinPassageSpeed = ApproachRuinSpeed(oldSpeed, target, 2.9f * dt, 4.2f * dt);
            float traveled = (oldSpeed + ruinPassageSpeed) * .5f * dt;
            float left = distance * (1 - Progress);
            Progress = Clamp(Progress + traveled / distance);
            RuinForwardSpeed = ruinPassageSpeed / ruinTransferMetrics.SourceScale;
            if (Progress < 1) return;

            float speed = ruinPassageSpeed / ruinTransferMetrics.DestinationScale;
            RememberRuinPortal(RuinEntrance);
            EnterRuinRoom(RuinEntrance.TargetRoom, false);
            RuinForwardSpeed = speed;
            Distance = Math.Max(0, traveled - left) / ruinTransferMetrics.DestinationScale;
            Revision++;
        }

        private void UpdateRuinSearch(float dt)
        {
            RefreshRuinLootContact();
            RuinSearchTarget = RuinNearbyClue = null;
            float depthScale = RuinAtlas.LengthScale(ruinRoomId);
            float nearest = float.PositiveInfinity, nearestClue = 7.3f * depthScale;
            float nearestPickup = float.PositiveInfinity;
            RuinObject pickup = null;
            foreach (var item in CurrentRuinRoom.Objects)
            {
                float depth = item.Depth - Distance;
                float lateral = Math.Abs(RuinLane - item.Lane);
                float vertical = Math.Abs(Flight.Height - item.Altitude);
                bool chest = item.Kind == RuinObjectKind.Chest;
                float along = Math.Abs(depth) / depthScale;
                float proximity = Clamp((6.7f - along) / 5.5f);
                float approach = Clamp((.86f - lateral) / .51f) * Clamp((.86f - vertical) / .55f);
                float reveal = proximity * (chest ? .22f + .78f * approach : .13f + .87f * approach);
                item.Reveal += (reveal - item.Reveal) * Follow(dt, 8.5f);
                if (item.Collected)
                {
                    item.OpenAge = Math.Min(10, item.OpenAge + dt);
                    if (!chest || RuinChestRemaining(item) == 0) continue;
                }
                float clue = Math.Abs(depth) + lateral * .42f;
                if (!item.Collected && clue < nearestClue)
                {
                    RuinNearbyClue = item;
                    nearestClue = clue;
                }
                if (item.Kind == RuinObjectKind.Heart && !ruinHeartSeen && along < 2.05f &&
                    lateral < .48f && vertical < .43f)
                {
                    ruinHeartAt = PoseTime;
                    ruinHeartSeen = true;
                    Notify(JourneyCue.RuinHeartbeat);
                    Revision++;
                }
                if (ruinDeferredLoot.Contains(item) || along > (chest ? 1.25f : 1.12f) ||
                    lateral > (chest ? .58f : .49f) || vertical > (chest ? .54f : .58f)) continue;
                float score = Math.Abs(depth) + lateral + vertical;
                if (!chest)
                {
                    if (score >= nearestPickup) continue;
                    pickup = item;
                    nearestPickup = score;
                    continue;
                }
                if (score >= nearest) continue;
                RuinSearchTarget = item;
                nearest = score;
            }
            if (pickup != null) TakeRuinCache(pickup);
        }

        private void TakeRuinCache(RuinObject item)
        {
            if (item.Kind == RuinObjectKind.Chest)
            {
                OfferRuinLoot(item);
                return;
            }
            if (RuinInventory >= RuinCapacity)
            {
                if (IsRuinDevice(item.Kind)) OfferRuinLoot(item);
                else
                {
                    Message = $"收藏已满，{RuinCapacity} 件。藏品留在原处。";
                    ruinDeferredLoot.Add(item);
                }
                RuinSearchTarget = null;
                return;
            }
            RecordRuinPickup(item);
            Notify(JourneyCue.RuinCollect);
            Message = "已收集" + RuinObjectLabels[(int)item.Kind] + "。";
            Revision++;
        }

        private void AddRuinFind(RuinObjectKind kind)
        {
            switch (kind)
            {
                case RuinObjectKind.Heart: RuinHearts++; break;
                case RuinObjectKind.Brain: RuinBrains++; break;
                case RuinObjectKind.Phone: RuinPhones++; break;
                case RuinObjectKind.Computer: RuinComputers++; break;
                case RuinObjectKind.Chest: RuinChests++; break;
            }
        }

        private void RestoreRuinFinds()
        {
            foreach (var room in ruinRooms)
            {
                foreach (var item in room.Objects)
                {
                    item.Collected = HasRuinFind(item.Id);
                    if (!item.Collected) continue;
                    item.OpenAge = 10;
                    if (!IsRuinMemory(item.Kind)) RestoreRuinInventoryItem(item);
                }
                foreach (var portal in room.Portals)
                {
                    if (portal.Opened) continue;
                    ulong mask = portal.Id < 64 ? ruinOpenLow : ruinOpenHigh;
                    portal.Opened = (mask & (1UL << (portal.Id & 63))) != 0;
                    if (portal.Opened) portal.OpenAge = 10;
                }
            }
            for (int id = 0; id < RuinAtlas.RoomCount; id++)
                if ((ruinVisited & (1 << id)) != 0) RuinVisitedCount++;
        }

        private void RestoreRuinMemories()
        {
            // Older saves may hold more than the new cap. Keep locked devices before loose memories.
            foreach (var room in ruinRooms)
                foreach (var item in room.Objects)
                    if (item.Collected && IsRuinMemory(item.Kind) && !RuinMemoryReleased(item))
                        RestoreRuinInventoryItem(item);
        }

        private void RestoreRuinInventoryItem(RuinObject item)
        {
            if (item.Kind == RuinObjectKind.Chest || RuinInventory < RuinCapacity)
            {
                AddRuinFind(item.Kind);
                return;
            }
            item.Collected = false;
            item.OpenAge = -1;
            SetRuinFind(item.Id, false);
        }

        private bool HasRuinFind(int id) => ((id < 64 ? ruinFinds : ruinFindsExtra) & (1UL << (id & 63))) != 0;

        private void SetRuinFind(int id, bool found)
        {
            ref ulong mask = ref (id < 64 ? ref ruinFinds : ref ruinFindsExtra);
            ulong bit = 1UL << (id & 63);
            mask = found ? mask | bit : mask & ~bit;
        }

        private static float Follow(float dt, float speed) => 1 - (float)Math.Exp(-dt * speed);
    }
}
