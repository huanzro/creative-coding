using System;
using System.Collections.Generic;

namespace Echoes
{
    public enum RuinLootAction { Take, Leave, Release }

    public readonly struct RuinLootChoice
    {
        public RuinLootAction Action { get; }
        public RuinObjectKind Kind { get; }
        public int Slot { get; }
        public int Variant { get; }
        public string Label { get; }

        internal RuinLootChoice(RuinLootAction action, RuinObjectKind kind, int slot, int variant, string label)
        {
            Action = action;
            Kind = kind;
            Slot = slot;
            Variant = variant;
            Label = label;
        }
    }

    public sealed partial class Journey
    {
        private const int SlotsPerRuinChest = 4;
        public const int RuinChestCount = RuinAtlas.RoomCount;
        private static readonly string[] ComputerNames = {
            "星图薄型电脑", "雕刻掌上电脑", "晶棱投影电脑", "赤铜三轴电脑", "雪石双屏电脑",
            "螺旋铰链电脑", "飞轮键盘电脑", "双页书脊电脑", "莲纹便携电脑", "沙漏支架电脑"
        };

        private readonly List<RuinLootChoice> ruinLootDevices = new List<RuinLootChoice>(SlotsPerRuinChest);
        private readonly List<RuinLootChoice> ruinLootChoices = new List<RuinLootChoice>(SlotsPerRuinChest + 1);
        private readonly List<RuinObject> ruinDeferredLoot = new List<RuinObject>(4);
        private ulong ruinChestLootTaken, ruinReleasedMemories, ruinReleasedMemoriesExtra;
        private RuinObject ruinLootReleaseSource;
        private RuinObject ruinAutomaticChest;
        private float ruinChestPickupAt;
        private RuinLootChoice ruinPendingDevice;
        private float ruinLootReleaseAt = float.NegativeInfinity;

        public RuinObject RuinLootSource { get; private set; }
        public RuinObject RuinLootDisplaySource => RuinLootSource ?? ruinAutomaticChest ??
            (RuinLootReleaseAge >= 0 ? ruinLootReleaseSource : null);
        public bool RuinLootOpen => RuinLootSource != null;
        public bool RuinChestOpening => ruinAutomaticChest != null;
        public bool RuinLootSwap { get; private set; }
        public int RuinLootIndex { get; private set; }
        public int RuinLootPendingSlot => RuinLootSwap ? ruinPendingDevice.Slot : -1;
        public IReadOnlyList<RuinLootChoice> RuinLootChoices => ruinLootChoices;
        public IReadOnlyList<RuinLootChoice> RuinLootDevices => ruinLootDevices;
        public RuinObjectKind RuinLootReleasedKind { get; private set; }
        public float RuinLootReleaseAge
        {
            get
            {
                float age = PoseTime - ruinLootReleaseAt;
                return age <= 1.05f ? age : -1;
            }
        }

        public int RuinChestItemCount(RuinObject chest)
        {
            if (chest == null || chest.Kind != RuinObjectKind.Chest) return 0;
            return 2 + (int)(RuinLootSeed(chest.ChestStyle) % 3u);
        }

        public int RuinChestRemaining(RuinObject chest)
        {
            int count = RuinChestItemCount(chest), available = 0;
            for (int slot = 0; slot < count; slot++)
                if ((ruinChestLootTaken & RuinDeviceBit(chest, slot)) == 0) available++;
            return available;
        }

        private uint RuinLootSeed(int value)
        {
            unchecked
            {
                uint seed = (uint)ruinSeed ^ ((uint)value + 1u) * 0x9E3779B9u;
                seed = (seed ^ (seed >> 16)) * 0x85EBCA6Bu;
                return seed ^ (seed >> 13);
            }
        }

        private RuinLootChoice RuinChestDevice(RuinObject chest, int slot)
        {
            uint seed = RuinLootSeed(chest.ChestStyle * SlotsPerRuinChest + 25 + slot);
            bool welcomePhone = chest.ChestStyle == RuinAtlas.WelcomeChestStyle && slot == 0;
            var kind = welcomePhone || (seed & 1u) != 0 ? RuinObjectKind.Phone : RuinObjectKind.Computer;
            int variant = (chest.ChestStyle + slot * 3) % ComputerNames.Length;
            return new RuinLootChoice(RuinLootAction.Take, kind, slot, variant,
                kind == RuinObjectKind.Phone ? RuinPhoneName : ComputerNames[variant]);
        }

        private static ulong RuinDeviceBit(RuinObject chest, int slot) => 1UL << (chest.ChestStyle * SlotsPerRuinChest + slot);
        private static bool IsRuinDevice(RuinObjectKind kind) => kind == RuinObjectKind.Phone || kind == RuinObjectKind.Computer;
        private static bool IsRuinMemory(RuinObjectKind kind) => kind == RuinObjectKind.Heart || kind == RuinObjectKind.Brain;
        private bool RuinMemoryReleased(RuinObject item) => IsRuinMemory(item.Kind) && IsRuinMemoryReleased(item.Id);

        private bool IsRuinMemoryReleased(int id) =>
            ((id < 64 ? ruinReleasedMemories : ruinReleasedMemoriesExtra) & (1UL << (id & 63))) != 0;

        private void RefreshRuinLootContact()
        {
            for (int i = ruinDeferredLoot.Count - 1; i >= 0; i--)
            {
                RuinObject item = ruinDeferredLoot[i];
                if (Math.Abs(item.Depth - Distance) > 1.98f * RuinAtlas.PreviousGalleryScale(ruinRoomId) ||
                    Math.Abs(item.Lane - RuinLane) > .80f || Math.Abs(item.Altitude - Flight.Height) > .78f)
                    ruinDeferredLoot.RemoveAt(i);
            }
        }

        private bool OfferRuinLoot(RuinObject item)
        {
            if (RuinLootOpen || RuinChestOpening) return true;
            RefreshRuinLootContact();
            if (ruinDeferredLoot.Contains(item)) return false;
            if (item.Kind == RuinObjectKind.Chest)
            {
                if (RuinChestRemaining(item) == 0) return false;
                item.OpenAge = 0;
                if (!item.Collected)
                {
                    RecordRuinPickup(item);
                    Revision++;
                }
                ruinAutomaticChest = item;
                ruinChestPickupAt = .73f;
                FillRuinChestDevices(item);
                RuinForwardSpeed = 0;
                StopRuinSteering();
                RuinSearchTarget = null;
                Message = "宝箱打开，设备正在入包。";
                return true;
            }
            else if (!IsRuinDevice(item.Kind) || item.Collected || RuinInventory < RuinCapacity) return false;
            else if (RuinHearts + RuinBrains == 0)
            {
                Message = $"{RuinCapacity} 格全是已锁定的设备。这件设备留在原处。";
                ruinDeferredLoot.Add(item);
                return false;
            }

            RuinLootSource = item;
            RuinForwardSpeed = 0;
            StopRuinSteering();
            RuinSearchTarget = null;
            ShowRuinLootDevices();
            return true;
        }

        private bool AdvanceAutomaticRuinChest(float dt)
        {
            var chest = ruinAutomaticChest;
            if (chest == null) return false;
            chest.OpenAge += dt;
            if (chest.OpenAge < ruinChestPickupAt) return true;
            if (RuinInventory < RuinCapacity && ruinLootDevices.Count > 0)
            {
                var device = ruinLootDevices[0];
                ClaimRuinLoot(chest, device);
                ruinLootDevices.RemoveAt(0);
                ruinChestPickupAt += .28f;
                Message = "「" + device.Label + "」已入包并锁定。";
                if (ruinLootDevices.Count > 0) return true;
            }
            ruinAutomaticChest = null;
            if (ruinLootDevices.Count == 0)
            {
                ruinDeferredLoot.Add(chest);
                return true;
            }
            RuinLootSource = chest;
            ShowRuinLootDevices();
            return true;
        }

        private void FillRuinChestDevices(RuinObject chest)
        {
            ruinLootDevices.Clear();
            int count = RuinChestItemCount(chest);
            for (int slot = 0; slot < count; slot++)
                if ((ruinChestLootTaken & RuinDeviceBit(chest, slot)) == 0)
                    ruinLootDevices.Add(RuinChestDevice(chest, slot));
        }

        private void ShowRuinLootDevices()
        {
            RuinLootSwap = false;
            ruinLootDevices.Clear();
            ruinLootChoices.Clear();
            var source = RuinLootSource;
            if (source.Kind == RuinObjectKind.Chest)
            {
                FillRuinChestDevices(source);
            }
            else ruinLootDevices.Add(new RuinLootChoice(RuinLootAction.Take, source.Kind, 0, source.Id % ComputerNames.Length,
                RuinObjectLabels[(int)source.Kind]));

            ruinLootChoices.AddRange(ruinLootDevices);
            ruinLootChoices.Add(new RuinLootChoice(RuinLootAction.Leave, RuinObjectKind.Chest, -1, 0,
                source.Kind == RuinObjectKind.Chest ? "合上箱盖，继续前进" : "留在原处，继续前进"));
            RuinLootIndex = 0;
            Message = RuinInventory == RuinCapacity ? $"背包 {RuinCapacity} / {RuinCapacity}。设备入包后锁定。" :
                "挑选想带走的设备。每件占一格，入包后锁定。";
        }

        private void ShowRuinLootRelease(RuinLootChoice device)
        {
            RuinLootSwap = true;
            ruinPendingDevice = device;
            ruinLootChoices.Clear();
            AddRuinReleaseOption(RuinObjectKind.Heart, RuinHearts);
            AddRuinReleaseOption(RuinObjectKind.Brain, RuinBrains);
            ruinLootChoices.Add(new RuinLootChoice(RuinLootAction.Leave, RuinObjectKind.Chest, -1, 0, "保留收藏，返回挑选"));
            RuinLootIndex = 0;
            Message = "再腾出一格就能带走「" + device.Label + "」。选中后按 B 一起确认。";
        }

        private void AddRuinReleaseOption(RuinObjectKind kind, int available)
        {
            if (available == 0) return;
            foreach (var room in ruinRooms)
            foreach (var item in room.Objects)
            {
                if (item.Kind != kind || !HasRuinFind(item.Id) || RuinMemoryReleased(item)) continue;
                string label = RuinLootSource.Kind == RuinObjectKind.Chest ? "放回一个" : "留下一个";
                ruinLootChoices.Add(new RuinLootChoice(RuinLootAction.Release, kind, item.Id, 0,
                    label + RuinObjectLabels[(int)kind] + "并拾取设备"));
                return;
            }
        }

        private bool TryRuinLootTouch(int option)
        {
            if (RuinChestOpening) return true;
            if (!RuinLootOpen) return false;
            if (option == 0)
            {
                RuinLootIndex = (RuinLootIndex + 1) % ruinLootChoices.Count;
                return true;
            }

            RuinLootChoice chosen = ruinLootChoices[RuinLootIndex];
            if (chosen.Action == RuinLootAction.Leave)
            {
                if (RuinLootSwap) ShowRuinLootDevices();
                else CloseRuinLoot(ruinHint);
            }
            else if (chosen.Action == RuinLootAction.Release) TakeRuinDevice(ruinPendingDevice, chosen);
            else if (RuinInventory < RuinCapacity) TakeRuinDevice(chosen, null);
            else if (RuinHearts + RuinBrains > 0) ShowRuinLootRelease(chosen);
            else Message = $"{RuinCapacity} 格全是已锁定的设备。这件设备留在原处。";
            return true;
        }

        private void TakeRuinDevice(RuinLootChoice device, RuinLootChoice? released)
        {
            if (released.HasValue)
            {
                var memory = released.Value;
                if (!HasRuinFind(memory.Slot) || IsRuinMemoryReleased(memory.Slot) || !IsRuinMemory(memory.Kind)) return;
                ref ulong releasedMask = ref (memory.Slot < 64 ? ref ruinReleasedMemories : ref ruinReleasedMemoriesExtra);
                releasedMask |= 1UL << (memory.Slot & 63);
                if (memory.Kind == RuinObjectKind.Heart) RuinHearts--;
                else RuinBrains--;
                RuinLootReleasedKind = memory.Kind;
                ruinLootReleaseAt = PoseTime;
                ruinLootReleaseSource = RuinLootSource;
                RememberRuinDiscard(memory.Kind);
            }

            var source = RuinLootSource;
            ClaimRuinLoot(source, device);
            string result = "「" + device.Label + "」已入包并锁定。";
            if (source.Kind != RuinObjectKind.Chest || RuinChestRemaining(source) == 0) CloseRuinLoot(result);
            else
            {
                ShowRuinLootDevices();
                Message = result;
            }
        }

        private void ClaimRuinLoot(RuinObject source, RuinLootChoice device)
        {
            if (source.Kind == RuinObjectKind.Chest)
            {
                ruinChestLootTaken |= RuinDeviceBit(source, device.Slot);
                AddRuinFind(device.Kind);
            }
            else RecordRuinPickup(source);
            Notify(JourneyCue.RuinCollect);
            Revision++;
        }

        private void RecordRuinPickup(RuinObject item)
        {
            item.Collected = true;
            item.OpenAge = 0;
            SetRuinFind(item.Id, true);
            AddRuinFind(item.Kind);
        }

        private void CloseRuinLoot(string message)
        {
            ruinDeferredLoot.Add(RuinLootSource);
            ClearRuinLootPanel();
            Message = message;
        }

        private void ClearRuinLoot()
        {
            ClearRuinLootPanel();
            ruinAutomaticChest = null;
            ruinDeferredLoot.Clear();
            ruinLootReleaseSource = null;
            ruinLootReleaseAt = float.NegativeInfinity;
        }

        private void ClearRuinLootPanel()
        {
            RuinLootSource = null;
            RuinLootSwap = false;
            RuinLootIndex = 0;
            ruinLootDevices.Clear();
            ruinLootChoices.Clear();
        }

        private void ResetRuinLootHistory()
        {
            ruinChestLootTaken = ruinReleasedMemories = ruinReleasedMemoriesExtra = 0;
            ClearRuinLoot();
        }

        private void RestoreRuinLootFinds()
        {
            ulong validTaken = 0, validReleased = 0, validReleasedExtra = 0;
            foreach (var room in ruinRooms)
            foreach (var item in room.Objects)
            {
                if (!item.Collected) continue;
                if (RuinMemoryReleased(item))
                {
                    if (item.Id < 64) validReleased |= 1UL << item.Id;
                    else validReleasedExtra |= 1UL << (item.Id & 63);
                }
                if (item.Kind != RuinObjectKind.Chest) continue;
                int count = RuinChestItemCount(item);
                for (int slot = 0; slot < count; slot++)
                {
                    ulong bit = RuinDeviceBit(item, slot);
                    if ((ruinChestLootTaken & bit) == 0 || RuinInventory >= RuinCapacity) continue;
                    validTaken |= bit;
                    AddRuinFind(RuinChestDevice(item, slot).Kind);
                }
            }
            ruinChestLootTaken = validTaken;
            ruinReleasedMemories = validReleased;
            ruinReleasedMemoriesExtra = validReleasedExtra;
        }
    }
}
