using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesView
    {
        private static readonly Color GlassBlue = new Color(.43f, .84f, 1f);
        private static readonly Color MemoryViolet = new Color(.77f, .67f, 1f);
        private static readonly Color PulseRose = new Color(1f, .45f, .49f);
        private static readonly Color ChestGold = new Color(1f, .77f, .18f);
        private static readonly Color RuinGlyphShadow = new Color(.03f, .05f, .09f, .96f);
        private static readonly Vector2[] HeartGlyph = {
            new Vector2(0, 9), new Vector2(-9, 1), new Vector2(-11, -4), new Vector2(-8, -8),
            new Vector2(-4, -9), new Vector2(0, -5), new Vector2(4, -9), new Vector2(8, -8),
            new Vector2(11, -4), new Vector2(9, 1), new Vector2(0, 9)
        };
        private static readonly Vector2[] BrainGlyph = {
            new Vector2(0, -7), new Vector2(-4, -10), new Vector2(-8, -8), new Vector2(-9, -5),
            new Vector2(-11, -2), new Vector2(-11, 3), new Vector2(-9, 5), new Vector2(-7, 8),
            new Vector2(-3, 9), new Vector2(0, 7), new Vector2(3, 9), new Vector2(7, 8),
            new Vector2(9, 5), new Vector2(11, 3), new Vector2(11, -2), new Vector2(9, -5),
            new Vector2(8, -8), new Vector2(4, -10), new Vector2(0, -7), new Vector2(0, 7)
        };
        private static readonly Vector2[] BrainFoldLeft = {
            new Vector2(-6, -5), new Vector2(-4, -2), new Vector2(-6, 1), new Vector2(-4, 4)
        };
        private static readonly Vector2[] BrainFoldRight = {
            new Vector2(6, -5), new Vector2(4, -2), new Vector2(6, 1), new Vector2(4, 4)
        };
        private static readonly Vector2[] PhoneGlyph = {
            new Vector2(-4, -11), new Vector2(4, -11), new Vector2(6, -9), new Vector2(6, 9),
            new Vector2(4, 11), new Vector2(-4, 11), new Vector2(-6, 9), new Vector2(-6, -9), new Vector2(-4, -11)
        };
        private static readonly Vector2[] PhoneHomeLine = { new Vector2(-2.25f, 7), new Vector2(2.25f, 7) };
        private static readonly Vector2[] ComputerGlyph = {
            new Vector2(-10, 5), new Vector2(-10, -8), new Vector2(10, -8), new Vector2(10, 5), new Vector2(-10, 5)
        };
        private static readonly Vector2[] ComputerStand = {
            new Vector2(-8, 10), new Vector2(8, 10), new Vector2(0, 10), new Vector2(0, 5)
        };
        private GUIStyle ruinInventoryNumber;
        private static readonly string[] EnglishGalleryNames = { "Civilization Hall", "Inscription Fault", "Mechanical Rings",
            "Vaulted Archive", "Mirror Gallery", "Echo Index", "Submerged Library", "Computing Gallery", "Skylight Crossing",
            "Far Edge", "Hall of Selves", "Amber Mezzanine", "Quiet Workshop", "Reverse Reading Room", "Sunken Bell", "Unfiled Archive" };
        private static readonly string[] EnglishDisciplines = { "Physics", "Mathematics", "Industry", "Architecture",
            "Literature", "Computing", "Education", "Medicine", "Psychology" };
        private static readonly string[] EnglishDeviceNames = { "Star Chart Laptop", "Engraved Pocket Computer", "Prism Projector",
            "Copper Gimbal Computer", "Stone Dual-Screen Computer", "Spiral-Hinge Computer", "Flywheel Keyboard",
            "Folio Computer", "Lotus Portable", "Hourglass Workstation" };

        internal static string RuinPlayerMessage(Journey journey)
        {
            string message = journey.Message;
            string english = journey.RuinRetryState switch
            {
                RuinRetryPhase.Valuation => journey.Age < Journey.RuinValuationFirstLineDuration
                    ? "I price every artifact in gold. First, let us see what you carried here."
                    : $"You have {journey.RuinCoins} coins. It costs {journey.RuinTokenCost} to leave.",
                RuinRetryPhase.Bargain => "I can let you through. Will you give me your own heart and brain as well?",
                RuinRetryPhase.Accepted => journey.Age < 2.55f ? "Exchange confirmed. Stay within the ring." : "Payment complete. The exit is open.",
                RuinRetryPhase.Declined => journey.Age < 2.15f ? "Beside mine, your brain is only a toy." :
                    journey.Age < 4.12f ? "Your heart will stop one day." : "Why hold on so tightly?",
                _ => RuinExplorationMessage(message)
            };
            return string.IsNullOrEmpty(message) ? "" : EchoesLocale.T(message, english);
        }

        private static string RuinExplorationMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return "";
            if (message.StartsWith("已收集"))
                return message.Contains("ipone18p") ? "Collected an ipone18p." : message.Contains("电脑") ? "Collected a computer." :
                    message.Contains("心脏") ? "Collected a heart." : "Collected a brain.";
            if (message.StartsWith("收藏已满")) return "Inventory full. The item stays here.";
            if (message.StartsWith("背包")) return "Inventory full. Devices are locked once collected.";
            if (message.Contains("已入包并锁定")) return "Device collected and locked.";
            if (message.Contains("格全是已锁定")) return "Your bag is full of locked devices. This one stays here.";
            if (message.StartsWith("再腾出")) return "Free one slot for this device, then press B to confirm.";
            return message switch
            {
                "宝箱打开，设备正在入包。" => "Chest open. Devices are moving into your bag.",
                "沿岔道进入下一层。" => "Follow the passage into the next gallery.",
                "裂缝里有光，还有一层空间。" => "Light filters through the seam. There is another floor.",
                "前面有一条岔道。" => "A branch lies ahead.",
                "墙后还有空间。沿两侧的微光找一找。" => "There is space beyond the walls. Look for the faint lights on either side.",
                _ => "Open a fist to jump and glide forward. Use your voice to speed up. Hold A/B to move sideways."
            };
        }

        private static string RuinLootLabel(RuinLootChoice option, Journey journey)
        {
            string english = option.Action switch
            {
                RuinLootAction.Release => option.Kind == RuinObjectKind.Heart ? "Swap one heart for the device" : "Swap one brain for the device",
                RuinLootAction.Leave => journey.RuinLootSwap ? "Keep my collection and go back" :
                    journey.RuinLootSource.Kind == RuinObjectKind.Chest ? "Close the chest and continue" : "Leave the device and continue",
                _ => option.Kind == RuinObjectKind.Phone ? Journey.RuinPhoneName :
                    journey.RuinLootSource.Kind == RuinObjectKind.Chest ? EnglishDeviceNames[option.Variant] : "Computer"
            };
            return EchoesLocale.T(option.Label, english);
        }

        private void DrawRuinHud(Journey journey)
        {
            PrepareInterface();
            if (journey.Phase == Stage.RuinRetry)
            {
                DrawRuinEnding(journey);
                return;
            }
            DrawRuinInventory(journey);

            string level = journey.RuinLayer == 0 ? EchoesLocale.T("中层", "Level 0") : journey.RuinLayer > 0
                ? EchoesLocale.T($"上层 +{journey.RuinLayer}", $"Level +{journey.RuinLayer}")
                : EchoesLocale.T($"下层 {journey.RuinLayer}", $"Level {journey.RuinLayer}");
            Paint(new Rect(26, 130, 332, 35), InkGlass);
            string discipline = RuinDisciplineExhibits.Name(journey.RuinDiscipline);
            GUI.Label(new Rect(43, 134, 310, 27), EchoesLocale.T($"{discipline}展厅  ·  {level}  ·  {journey.RuinRoomName}",
                $"{EnglishDisciplines[journey.RuinDiscipline]} · {level} · {EnglishGalleryNames[journey.RuinRoomId]}"), body);
            if (journey.Phase == Stage.RuinExplore && !journey.RuinLootOpen)
            {
                float hintY = 173;
                RuinPortal portal = journey.RuinApproach;
                RuinPortal upper = journey.RuinUpperApproach;
                if (portal != null)
                {
                    DrawRuinEntranceHint(hintY, journey, portal);
                    hintY += 36;
                }
                if (upper != null && upper != portal)
                {
                    DrawRuinEntranceHint(hintY, journey, upper);
                    hintY += 36;
                }

                if (journey.RuinSearchTarget != null)
                {
                    DrawRuinHint(hintY, EchoesLocale.T(journey.RuinTakeLabel, "B / Right · Open chest"), Amber, journey.RuinCanTake);
                }
                else if (journey.RuinNearbyClue is RuinObject clue)
                {
                    string side = clue.Lane < 0 ? EchoesLocale.T("← 左侧", "← Left") : clue.Lane > 0
                        ? EchoesLocale.T("→ 右侧", "→ Right") : EchoesLocale.T("↑ 前方", "↑ Ahead");
                    DrawRuinHint(hintY, side + (clue.Kind == RuinObjectKind.Chest
                        ? EchoesLocale.T("有宝箱", " · Chest") : EchoesLocale.T("有亮着的托台", " · Lit display")), Amber, true);
                }
            }

            if (journey.RuinLootOpen) DrawRuinLoot(journey);
        }

        private void DrawRuinEntranceHint(float y, Journey journey, RuinPortal portal)
        {
            var (text, tint) = portal.Direction switch
            {
                RuinDirection.Left => (EchoesLocale.T("← 左岔道 · 空中也可按住 A", "← Left branch · Hold A"), GlassBlue),
                RuinDirection.Right => (EchoesLocale.T("→ 右岔道 · 空中也可按住 B", "→ Right branch · Hold B"), GlassBlue),
                RuinDirection.Upper => (EchoesLocale.T("↑ 上层入口 · 握拳后张手上升", "↑ Upper branch · Open fist"), MemoryViolet),
                _ => (EchoesLocale.T("↓ 下层入口 · 下降时进入", "↓ Lower branch · Descend"), Mint)
            };
            DrawRuinHint(y, text, tint, journey.IsRuinEntranceInRange(portal));
        }

        private void DrawRuinHint(float y, string hint, Color tint, bool ready)
        {
            Paint(new Rect(26, y, 332, 32), InkGlass);
            Paint(new Rect(26, y, 2, 32), Opacity(tint, ready ? .94f : .30f));
            GUI.Label(new Rect(43, y + 3, 305, 26), hint, body);
        }

        private void DrawRuinLoot(Journey journey)
        {
            var choices = journey.RuinLootChoices;
            const float x = 353, width = 574;
            float y = 628 - 48 - 27 * choices.Count;
            Paint(new Rect(x, y, width, 628 - y), InkGlass);
            Paint(new Rect(x, y, width, 2), journey.RuinLootSwap ? PulseRose : Amber);
            string heading = journey.RuinLootSwap ? EchoesLocale.T("腾出一格", "Free a slot") :
                journey.RuinLootSource.Kind == RuinObjectKind.Chest ? EchoesLocale.T("打开的宝箱", "Opened chest") :
                EchoesLocale.T("途中发现的设备", "Found device");
            GUI.Label(new Rect(x + 16, y + 9, 325, 29), heading, body);
            int remaining = Journey.RuinCapacity - journey.RuinInventory;
            GUI.Label(new Rect(x + width - 184, y + 9, 167, 29), EchoesLocale.T($"余量  {remaining} 格", $"{remaining} slots free"), body);
            for (int i = 0; i < choices.Count; i++)
            {
                var option = choices[i];
                bool selected = i == journey.RuinLootIndex;
                float row = y + 39 + 27 * i;
                Color accent = option.Action == RuinLootAction.Release ? PulseRose : option.Action == RuinLootAction.Leave ? Mint : Amber;
                if (selected) Paint(new Rect(x + 8, row, width - 16, 25), Opacity(accent, .16f));
                Orb(x + 23, row + 12, selected ? 10 : 6, accent, selected ? .85f : .38f);
                GUI.Label(new Rect(x + 37, row, width - 64, 28), RuinLootLabel(option, journey), body);
            }
        }

        private void DrawRuinEnding(Journey journey)
        {
            RuinRetryPhase phase = journey.RuinRetryState;
            if (phase == RuinRetryPhase.Survey)
            {
                float remaining = Mathf.Max(0, Journey.RuinSurveyDuration - journey.Age);
                const float x = 443, y = 530, width = 394;
                Paint(new Rect(x, y, width, 77), InkGlass);
                GUI.Label(new Rect(x + 18, y + 7, 270, 27), EchoesLocale.T("观察隧道骨架", "View the tunnel framework"), body);
                GUI.Label(new Rect(x + width - 62, y + 7, 50, 27),
                    EchoesLocale.T($"{Mathf.CeilToInt(remaining)} 秒", $"{Mathf.CeilToInt(remaining)} s"), body);
                GUI.Label(new Rect(x + 18, y + 34, width - 36, 30), EchoesLocale.T("A/B 左右看  ·  张手向上看  ·  发声向下看",
                    "A/B turn · Open palm up · Sound down"), body);
                Paint(new Rect(x + 20, y + 66, width - 40, 2), Opacity(GlassBlue, .20f));
                Paint(new Rect(x + 20, y + 66, (width - 40) * remaining / Journey.RuinSurveyDuration, 2), GlassBlue);
                return;
            }
            bool showLedger = phase == RuinRetryPhase.Valuation || phase == RuinRetryPhase.Bargain ||
                phase == RuinRetryPhase.Accepted && journey.Age < 3.9f;
            if (showLedger) DrawRuinTokenLedger(journey);
            if (string.IsNullOrEmpty(journey.Message)) return;
            const float left = 302, widthSubtitle = 676, ySubtitle = 573;
            Paint(new Rect(left, ySubtitle, widthSubtitle, 62), InkGlass);
            Paint(new Rect(left, ySubtitle, 2, 62), Opacity(GlassBlue, .86f));
            GUI.Label(new Rect(left + 18, ySubtitle + 5, 130, 25), EchoesLocale.T("机器人", "Broker"), body);
            GUI.Label(new Rect(left + 18, ySubtitle + 30, widthSubtitle - 36, 27), RuinPlayerMessage(journey), body);
        }

        private void DrawRuinTokenLedger(Journey journey)
        {
            const float x = 948, y = 79, width = 303;
            bool bargaining = journey.RuinRetryState == RuinRetryPhase.Bargain;
            bool settled = journey.RuinRevealedTokens >= journey.RuinTokenCost;
            float height = bargaining ? 151 : 123;
            Paint(new Rect(x, y, width, height), InkGlass);
            Paint(new Rect(x, y, width, 2), settled ? Mint : GlassBlue);
            GUI.Label(new Rect(x + 14, y + 8, 136, 27), EchoesLocale.T("出口结算", "Exit toll"), body);
            DrawRuinCoin(new Vector2(x + 159, y + 21), 7);
            GUI.Label(new Rect(x + 171, y + 8, 119, 27),
                $"{journey.RuinRevealedTokens} / {journey.RuinTokenCost}", body);
            Paint(new Rect(x + 15, y + 41, width - 30, 3), Opacity(GlassBlue, .20f));
            Paint(new Rect(x + 15, y + 41, (width - 30) * Mathf.Clamp01(journey.RuinRevealedTokens / (float)Mathf.Max(1, journey.RuinTokenCost)), 3),
                settled ? Mint : GlassBlue);
            GUI.Label(new Rect(x + 14, y + 52, width - 28, 24),
                EchoesLocale.T($"{Journey.RuinPhoneName} ×{journey.RuinPhones}  ·  {Journey.RuinPhoneTokenRate} 金币 / 件",
                    $"{Journey.RuinPhoneName} ×{journey.RuinPhones}  ·  {Journey.RuinPhoneTokenRate} coins each"), body);
            GUI.Label(new Rect(x + 14, y + 77, width - 28, 24),
                EchoesLocale.T($"电脑 ×{journey.RuinComputers}  ·  {Journey.RuinComputerTokenRate} 金币 / 件",
                    $"Computers ×{journey.RuinComputers}  ·  {Journey.RuinComputerTokenRate} coins each"), body);
            if (bargaining)
            {
                string reserved = journey.RuinRetainedKind == RuinObjectKind.Heart ? EchoesLocale.T("保留一颗心脏", "Keep one heart") :
                    journey.RuinRetainedKind == RuinObjectKind.Brain ? EchoesLocale.T("保留一个大脑", "Keep one brain") :
                    EchoesLocale.T("本次无保留藏品", "No artifact reserved");
                GUI.Label(new Rect(x + 14, y + 110, width - 28, 30),
                    reserved, body);
            }
        }

        private void DrawRuinInventory(Journey journey)
        {
            if (ruinInventoryNumber == null)
            {
                ruinInventoryNumber = new GUIStyle(body)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false
                };
                ruinInventoryNumber.normal.textColor = Color.white;
            }
            const float top = 84;
            Paint(new Rect(911, top - 15, 343, 31), InkGlass);
            Vector2 heart = new Vector2(931, top);
            Vector2 brain = new Vector2(1014, top);
            Vector2 phone = new Vector2(1097, top);
            Vector2 computer = new Vector2(1180, top);
            DrawRuinGlyph(heart, HeartGlyph, PulseRose);
            DrawRuinGlyph(brain, BrainGlyph, MemoryViolet);
            DrawRuinGlyph(brain, BrainFoldLeft, MemoryViolet);
            DrawRuinGlyph(brain, BrainFoldRight, MemoryViolet);
            DrawRuinGlyph(phone, PhoneGlyph, GlassBlue);
            DrawRuinGlyph(phone, PhoneHomeLine, GlassBlue);
            DrawRuinGlyph(computer, ComputerGlyph, Mint);
            DrawRuinGlyph(computer, ComputerStand, Mint);
            DrawRuinInventoryNumber(heart, journey.RuinHearts, Journey.RuinHeartCoinRate);
            DrawRuinInventoryNumber(brain, journey.RuinBrains, Journey.RuinBrainCoinRate);
            DrawRuinInventoryNumber(phone, journey.RuinPhones, Journey.RuinPhoneTokenRate);
            DrawRuinInventoryNumber(computer, journey.RuinComputers, Journey.RuinComputerTokenRate);
        }

        private void DrawRuinGlyph(Vector2 origin, Vector2[] points, Color color)
        {
            for (int i = 1; i < points.Length; i++)
                Stroke(origin + points[i - 1], origin + points[i], 3.8f, RuinGlyphShadow, RuinGlyphShadow.a);
            for (int i = 1; i < points.Length; i++)
                Stroke(origin + points[i - 1], origin + points[i], 2.2f, color, 1);
        }

        private void DrawRuinInventoryNumber(Vector2 origin, int count, int coins)
        {
            var rect = new Rect(origin.x + 13, origin.y - 13, 32, 26);
            string number = count.ToString();
            Color previous = GUI.contentColor;
            GUI.contentColor = RuinGlyphShadow;
            GUI.Label(new Rect(rect.x - 1, rect.y + 1, rect.width, rect.height), number, ruinInventoryNumber);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), number, ruinInventoryNumber);
            GUI.contentColor = Color.white;
            GUI.Label(rect, number, ruinInventoryNumber);
            var coin = origin + Vector2.right * 41;
            DrawRuinCoin(coin, 6);
            GUI.contentColor = ChestGold;
            GUI.Label(new Rect(coin.x + 9, rect.y, 20, rect.height), coins.ToString(), ruinInventoryNumber);
            GUI.contentColor = previous;
        }

        private void DrawRuinCoin(Vector2 at, float size)
        {
            Orb(at.x, at.y, size * 1.28f, Amber, .56f);
            Ripple(at.x, at.y, size * .91f, ChestGold, .86f);
            Stroke(at - Vector2.up * (size * .36f), at + Vector2.up * (size * .36f), 1.1f, ChestGold, .95f);
        }

        private void DrawRuinFinds(SpiritFrame frame, Vector2 center, float bodySize, bool mirror, bool sideways)
        {
            if (frame.ruinFinds == null) return;
            float direction = mirror ? -1 : 1;
            Vector2 lateral = sideways ? Vector2.down : Vector2.right;
            Vector2 vertical = sideways ? Vector2.right : Vector2.up;
            foreach (RuinFindView find in frame.ruinFinds)
            {
                float distance = Mathf.Clamp01((find.depth + .7f) / 5.7f);
                Vector2 icon = center + lateral * (find.lane * direction * bodySize * .56f) +
                    vertical * bodySize * (find.height * .89f + .20f * distance);
                if (Mathf.Abs(find.lane) < .18f) icon += lateral * bodySize * .25f * direction;
                float size = Mathf.Max(5.5f, bodySize * Mathf.Lerp(.086f, .037f, distance));
                float alpha = Mathf.Clamp01(find.visibility * 1.36f);
                Color color;
                switch (find.kind)
                {
                    case RuinObjectKind.Heart:
                        color = PulseRose;
                        float beat = Mathf.Pow(.5f + .5f * Mathf.Cos(frame.motionTime * 7.9f), 8);
                        size *= 1 + .28f * beat;
                        Orb(icon.x - size * .25f, icon.y - size * .18f, size * .48f, color, alpha * .65f);
                        Orb(icon.x + size * .25f, icon.y - size * .18f, size * .48f, color, alpha * .65f);
                        Stroke(icon + new Vector2(-size * .30f, 0), icon + new Vector2(0, size * .44f), 2, color, alpha);
                        Stroke(icon + new Vector2(size * .30f, 0), icon + new Vector2(0, size * .44f), 2, color, alpha);
                        break;
                    case RuinObjectKind.Brain:
                        color = MemoryViolet;
                        for (int lobes = 0; lobes < 4; lobes++)
                        {
                            float angle = lobes * Mathf.PI / 2 + .8f;
                            Vector2 lobe = icon + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (size * .28f);
                            Orb(lobe.x, lobe.y, size * .45f, color, alpha * .69f);
                        }
                        break;
                    case RuinObjectKind.Phone:
                        color = GlassBlue;
                        RuinScreen(icon, size * .60f, size, color, alpha);
                        break;
                    case RuinObjectKind.Computer:
                        color = Mint;
                        RuinScreen(icon, size * 1.23f, size * .72f, color, alpha);
                        Stroke(icon + new Vector2(-size * .74f, size * .50f), icon + new Vector2(size * .74f, size * .50f), 2, color, alpha);
                        break;
                    default:
                        color = ChestGold;
                        DrawRuinChestLight(icon, lateral * direction, vertical, size * 1.34f, alpha, find.open, frame.motionTime);
                        break;
                }
                Orb(icon.x, icon.y, size * 1.95f, color, alpha * .18f);
            }
        }

        private void DrawRuinChestLight(Vector2 at, Vector2 across, Vector2 up, float size, float alpha, float open, float time)
        {
            Vector2 Point(float x,float y) => at + (across * x + up * y) * size;
            Vector2 left = Point(-.55f, .05f), right = Point(.55f, .05f);
            Vector2 lidLeft = Point(-.55f, .42f + .31f * open), lidRight = Point(.55f, .42f + .31f * open);
            Vector2 belowLeft = Point(-.48f, -.32f), belowRight = Point(.48f, -.32f);
            Color highlight = new Color(1f,.94f,.54f);
            Stroke(left, belowLeft, 2, ChestGold, alpha);
            Stroke(right, belowRight, 2, ChestGold, alpha);
            Stroke(belowLeft, belowRight, 2, ChestGold, alpha);
            Stroke(left, right, 2.4f, highlight, alpha);
            Stroke(Point(-.55f, .18f + .18f * open), lidLeft, 2, ChestGold, alpha);
            Stroke(lidLeft, Point(-.28f, .57f + .36f * open), 2, ChestGold, alpha);
            Stroke(Point(-.28f, .57f + .36f * open), Point(.28f, .57f + .36f * open), 2, ChestGold, alpha);
            Stroke(Point(.28f, .57f + .36f * open), lidRight, 2, ChestGold, alpha);
            Stroke(lidRight, Point(.55f, .18f + .18f * open), 2, ChestGold, alpha);
            Orb(at.x, at.y, size * (1.51f + .26f * open), ChestGold, alpha * (.46f + .41f * open));
            Orb(at.x, at.y, size * .27f, highlight, alpha);
            for (int i = 0; i < 5; i++)
            {
                float rise = Mathf.Repeat(time * (.54f + i * .025f) + i * .27f, 1);
                Vector2 glint = Point(Mathf.Sin(time + i * 1.8f) * (.45f + .45f * open), rise * (1.33f + open));
                Orb(glint.x, glint.y, size * .19f, highlight, alpha * Mathf.Sin(rise * Mathf.PI));
            }
        }

        private void RuinScreen(Vector2 center, float width, float height, Color color, float alpha)
        {
            float stroke = Mathf.Max(1, height * .085f);
            var rect = new Rect(center.x - width / 2, center.y - height / 2, width, height);
            Paint(rect, Opacity(color, alpha * .16f));
            Paint(new Rect(rect.x, rect.y, width, stroke), Opacity(color, alpha));
            Paint(new Rect(rect.x, rect.yMax - stroke, width, stroke), Opacity(color, alpha));
            Paint(new Rect(rect.x, rect.y, stroke, height), Opacity(color, alpha));
            Paint(new Rect(rect.xMax - stroke, rect.y, stroke, height), Opacity(color, alpha));
        }
    }
}
