using System;
using System.Collections.Generic;

namespace Echoes
{
    public sealed partial class Journey
    {
        public const int RuinCapacity = 15;
        public const float RuinHeightLimit = 1.65f;
        public const float RuinTransitionDuration = 3.35f;
        public const float RuinEntryAltitude = .072f;
        public const int RuinRoomCount = RuinAtlas.RoomCount;
        private const float PalmTime = .38f;
        private const float VoiceTime = .48f;
        private const float HeartStartleTime = 1.45f;
        private const float EntryAltitude = .17f;
        private static readonly string[] RuinObjectLabels = { "心脏", "大脑", "手机", "电脑", "宝箱" };

        private RuinRoom[] ruinRooms;
        private int ruinSeed, ruinVisited, ruinRoomId, ruinLoops, ruinBypassedPortal = -1;
        private ulong ruinFinds, ruinOpenLow, ruinOpenHigh;
        private float ruinHeartAt, ruinSteeringSpeed, ruinVoiceCharge, ruinTravelTime;
        private bool ruinHeartSeen;
        private string ruinHint;

        public bool InRuins => Chapter == 1 && Lesson < 0;
        private bool IsRuinSequence => InRuins && Phase == Stage.RuinTransfer;
        private RuinRoom CurrentRuinRoom => ruinRooms == null ? null : ruinRooms[ruinRoomId];
        public int RuinRoomId => ruinRoomId;
        public int RuinVisualSeed => ruinSeed;
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
        public float RuinLane { get; private set; }
        public int RuinTargetLane { get; private set; }
        public float RuinSteering { get; private set; }
        public RuinPortal RuinApproach { get; private set; }
        public RuinPortal RuinGuide { get; private set; }
        public RuinPortal RuinEntrance { get; private set; }
        public bool RuinCanChoosePassage => Phase == Stage.RuinExplore && !RuinLootOpen &&
            RuinApproach != null && (RuinGuide != null || CanChooseRuinPortal(RuinApproach));
        public float RuinGateCharge => RuinGuide == null || RuinGuide.Opened ? 0 :
            RuinGuide.Gate == RuinGate.Palm ? Clamp(Held / PalmTime) : Clamp(ruinVoiceCharge / VoiceTime);
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
            ruinTravelTime = 0;
            ruinFinds = ruinOpenLow = ruinOpenHigh = 0;
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
            }
            ruinHeartAt = float.NegativeInfinity;
            EnterRuinRoom(ruinRoomId, true);
            ResumeRuinRetry();
        }

        private void EnterRuinRoom(int room, bool atThreshold)
        {
            ClearRuinLoot();
            ruinRoomId = room;
            if ((ruinVisited & (1 << room)) == 0)
            {
                ruinVisited |= 1 << room;
                RuinVisitedCount++;
            }
            Distance = 0;
            RuinLane = RuinTargetLane = 0;
            RuinSteering = ruinSteeringSpeed = ruinVoiceCharge = 0;
            RuinApproach = RuinGuide = RuinEntrance = null;
            ruinBypassedPortal = -1;
            foreach (var item in CurrentRuinRoom.Objects) item.Reveal = 0;
            float entryHeight = atThreshold && room == 0 ? EntryAltitude : RuinEntryAltitude;
            Flight.Reset(entryHeight, RuinHeightLimit);
            ruinHint = room == 0 ? "A / B 换道，握拳后张手上升。对准岔口后，按 B 进入。" :
                CurrentRuinRoom.Style == 5 ? "这里的光回不到主廊。靠近暗处，看清里面藏了什么。" :
                room == RuinAtlas.ExitRoom ? "墙上的光纹开始朝同一个方向延伸。" :
                "主路会绕回本层。横移或改变高度，在想走的岔口按 B。";
            Change(Stage.RuinExplore, ruinHint);
        }

        private void RuinTouch(int option)
        {
            if (!RuinCanChoosePassage)
            {
                RuinTargetLane = Math.Max(-1, Math.Min(1, RuinTargetLane + (option == 0 ? -1 : 1)));
                return;
            }
            RuinPortal portal = RuinApproach;
            if (option == 0)
            {
                ruinBypassedPortal = portal.Id;
                RuinApproach = RuinGuide = null;
                ruinVoiceCharge = 0;
                Message = "沿当前隧道继续飞行。";
                return;
            }
            if (portal.Opened)
            {
                TravelRuins(portal);
                return;
            }
            RuinGuide = portal;
            Message = portal.Gate == RuinGate.Palm ? "掌印门。握拳半秒，再张开手。A 可返回主路。" :
                "共鸣门。张手并轻声约半秒。A 可返回主路。";
        }

        private bool CanChooseRuinPortal(RuinPortal portal)
        {
            float ahead = portal.Depth - Distance;
            float height = portal.Direction == RuinDirection.Upper ? .31f : .26f;
            return portal.Id != ruinBypassedPortal && ahead >= -.74f && ahead <= 2.20f &&
                RuinTargetLane == portal.Lane && Math.Abs(RuinLane - portal.Lane) <= .34f &&
                Math.Abs(Flight.Height - portal.Altitude) <= height;
        }

        // Gate-assisted hovering makes the short prompt forgiving without committing the player to that route.
        private bool HoldAtRuinGate() => RuinGuide != null && RuinTargetLane == RuinGuide.Lane;

        private bool ReleaseRuinGate()
        {
            if (!HoldAtRuinGate() || RuinGuide.Gate != RuinGate.Palm || RuinGuide.Opened) return false;
            if (Held >= PalmTime) OpenRuinGate(RuinGuide);
            return true;
        }

        private void OpenRuinGate(RuinPortal gate)
        {
            gate.Opened = true;
            gate.OpenAge = 0;
            if (gate.Id < 64) ruinOpenLow |= 1UL << gate.Id;
            else ruinOpenHigh |= 1UL << (gate.Id - 64);
            Notify(JourneyCue.RuinDoor);
            Revision++;
            Message = "门已打开。即将穿过弯道，按 A 可返回主路。";
        }

        private void UpdateRuins(float dt)
        {
            if (IsRuinSequence)
            {
                Progress = Clamp(Age / RuinTransitionDuration);
                if (Progress < 1) return;
                RememberRuinPortal(RuinEntrance);
                EnterRuinRoom(RuinEntrance.TargetRoom, false);
                Revision++;
                return;
            }

            foreach (var portal in CurrentRuinRoom.Portals)
                if (portal.OpenAge >= 0) portal.OpenAge = Math.Min(10, portal.OpenAge + dt);
            if (RuinLootOpen)
            {
                if (RuinLootSource != null && RuinLootSource.OpenAge >= 0)
                    RuinLootSource.OpenAge = Math.Min(10, RuinLootSource.OpenAge + dt);
                return;
            }
            UpdateRuinSteering(dt);
            if (HoldAtRuinGate())
            {
                float aim = Flight.Height + (RuinGuide.Altitude - Flight.Height) * Follow(dt, 7.5f);
                Flight.FollowForestCourse(aim, dt);
                if (RuinGuide.Opened)
                {
                    if (RuinGuide.OpenAge >= .52f) TravelRuins(RuinGuide);
                }
                else if (RuinGuide.Gate == RuinGate.Voice)
                {
                    bool speaking = Voice >= .17f && Gesture == Hand.Open;
                    ruinVoiceCharge = Math.Max(0, ruinVoiceCharge + dt * (speaking ? 1 : -1.35f));
                    if (ruinVoiceCharge >= VoiceTime) OpenRuinGate(RuinGuide);
                }
                CollectRuinObjects(dt);
                return;
            }
            RuinGuide = null;
            ruinVoiceCharge = 0;

            float air = Clamp(Flight.Height / .31f);
            float speed = 2.35f + air * 2.1f + Voice * (1 + air * 3.5f);
            if (RuinCanChoosePassage) speed *= .56f;
            ruinTravelTime = Math.Min(180f, ruinTravelTime + dt);
            Distance += speed * dt;
            if (Distance >= RuinRoomLength)
            {
                Distance %= RuinRoomLength;
                ruinLoops = Math.Min(ruinLoops + 1, 999);
                ruinBypassedPortal = -1;
                RuinApproach = null;
                Revision++;
            }
            CollectRuinObjects(dt);
            if (RuinLootOpen || TryStartRuinRetry()) return;
            RuinPortal approached = null;
            foreach (var portal in CurrentRuinRoom.Portals)
            {
                float ahead = portal.Depth - Distance;
                if (portal.Id == ruinBypassedPortal || ahead < -.74f || ahead > 7.0f) continue;
                float lateral = Math.Abs(RuinLane - portal.Lane);
                float vertical = Math.Abs(Flight.Height - portal.Altitude);
                if (CanChooseRuinPortal(portal))
                {
                    approached = portal;
                    break;
                }
                if (approached == null && lateral < .72f && vertical < .57f) approached = portal;
            }
            if (Phase != Stage.RuinExplore || RuinGuide != null) return;
            if (approached != RuinApproach)
            {
                RuinApproach = approached;
                if (approached != null)
                    Message = approached.Hidden ? "裂缝里有光，还有一层空间。" :
                        "隧道在这里分开。贴近后按 B 进入，继续飞就会留在当前层。";
                else if (CueAge > 1.2f) Message = ruinHint;
            }
        }

        private void UpdateRuinSteering(float dt)
        {
            float desired = (RuinTargetLane - RuinLane) * 5.6f;
            ruinSteeringSpeed += (Math.Max(-3.0f, Math.Min(3.0f, desired)) - ruinSteeringSpeed) * Follow(dt, 11);
            RuinLane = Math.Max(-1, Math.Min(1, RuinLane + ruinSteeringSpeed * dt));
            RuinSteering = ruinSteeringSpeed / 3.0f;
        }

        private void TravelRuins(RuinPortal portal)
        {
            RuinPassageStartDepth = Distance;
            RuinPassageStartLane = RuinLane;
            RuinPassageStartAltitude = Flight.Height;
            RuinEntrance = portal;
            RuinApproach = RuinGuide = null;
            RuinSteering = ruinSteeringSpeed = 0;
            Notify(JourneyCue.RuinTravel);
            Change(Stage.RuinTransfer, "进入新的层道。");
        }

        private void CollectRuinObjects(float dt)
        {
            RefreshRuinLootContact();
            foreach (var item in CurrentRuinRoom.Objects)
            {
                float depth = item.Depth - Distance;
                float lateral = Math.Abs(RuinLane - item.Lane);
                float vertical = Math.Abs(Flight.Height - item.Altitude);
                float reveal = depth > -1.8f && depth < 6.8f ?
                    Clamp((6.8f - Math.Abs(depth)) / 5.10f) * Clamp(1.17f - .28f * lateral - .26f * vertical) : 0;
                item.Reveal += (reveal - item.Reveal) * Follow(dt, 8.5f);
                if (item.Collected)
                {
                    item.OpenAge = Math.Min(10, item.OpenAge + dt);
                    if (item.Kind != RuinObjectKind.Chest) continue;
                }
                if (item.Kind == RuinObjectKind.Heart && !ruinHeartSeen && depth >= -.55f && depth < 2.45f &&
                    lateral < .76f && vertical < .36f)
                {
                    ruinHeartAt = PoseTime;
                    ruinHeartSeen = true;
                    Notify(JourneyCue.RuinHeartbeat);
                    Revision++;
                }
                if (Math.Abs(depth) > .62f || lateral > .34f || vertical > .23f) continue;
                if (item.Kind == RuinObjectKind.Chest)
                {
                    OfferRuinLoot(item);
                    if (RuinLootOpen) return;
                    continue;
                }
                if (RuinInventory >= RuinCapacity)
                {
                    if (item.Kind == RuinObjectKind.Phone || item.Kind == RuinObjectKind.Computer)
                    {
                        OfferRuinLoot(item);
                        if (RuinLootOpen) return;
                    }
                    if (RuinGuide == null) Message = "收藏已满，15 件。仍可继续寻找出口和宝箱。";
                    continue;
                }
                RecordRuinPickup(item);
                if (RuinHeartAge < 0 || item.Kind != RuinObjectKind.Heart) Notify(JourneyCue.RuinCollect);
                if (RuinGuide == null)
                    Message = $"已收集{RuinObjectLabels[(int)item.Kind]}。";
                Revision++;
            }
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
                    item.Collected = (ruinFinds & (1UL << item.Id)) != 0;
                    if (item.Collected)
                    {
                        if (RuinMemoryReleased(item)) continue;
                        // Older or malformed save masks cannot raise the inventory above its limit.
                        if (item.Kind != RuinObjectKind.Chest && RuinInventory >= RuinCapacity)
                        {
                            item.Collected = false;
                            ruinFinds &= ~(1UL << item.Id);
                            continue;
                        }
                        item.OpenAge = 10;
                        AddRuinFind(item.Kind);
                    }
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

        private static float Follow(float dt, float speed) => 1 - (float)Math.Exp(-dt * speed);
    }
}
