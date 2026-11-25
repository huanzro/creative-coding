using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesView
    {
        private static readonly Color GlassBlue = new Color(.43f, .84f, 1f);
        private static readonly Color MemoryViolet = new Color(.77f, .67f, 1f);
        private static readonly Color PulseRose = new Color(1f, .45f, .49f);
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

        private void DrawRuinHud(Journey journey)
        {
            PrepareInterface();
            if (journey.Phase == Stage.RuinRetry)
            {
                DrawRuinEnding(journey);
                return;
            }
            DrawRuinInventory(journey);

            string level = journey.RuinLayer == 0 ? "中层" : journey.RuinLayer > 0
                ? $"上层 +{journey.RuinLayer}" : $"下层 {journey.RuinLayer}";
            Paint(new Rect(26, 130, 332, 35), InkGlass);
            string discipline = RuinDisciplineExhibits.Name(journey.RuinDiscipline);
            GUI.Label(new Rect(43, 134, 310, 27), $"{discipline}展厅  ·  {level}  ·  {journey.RuinRoomName}", body);
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
                    string action = journey.RuinCanTake ? journey.RuinTakeLabel :
                        journey.RuinSearchTarget.Kind == RuinObjectKind.Chest ? "靠近宝箱了，停声查看" : "靠近托台了，停声查看";
                    DrawRuinHint(hintY, action, Amber, journey.RuinCanTake);
                }
                else if (journey.RuinNearbyClue is RuinObject clue)
                {
                    string side = clue.Lane < 0 ? "← 左侧" : clue.Lane > 0 ? "→ 右侧" : "↑ 前方";
                    DrawRuinHint(hintY, side + (clue.Kind == RuinObjectKind.Chest ? "有宝箱" : "有亮着的托台"), Amber, true);
                }
            }

            if (journey.RuinLootOpen) DrawRuinLoot(journey);
        }

        private void DrawRuinEntranceHint(float y, Journey journey, RuinPortal portal)
        {
            var (text, tint) = portal.Direction switch
            {
                RuinDirection.Left => ("← 左岔道 · 空中也可按住 A", GlassBlue),
                RuinDirection.Right => ("→ 右岔道 · 空中也可按住 B", GlassBlue),
                RuinDirection.Upper => ("↑ 上层入口 · 握拳后张手上升", MemoryViolet),
                _ => ("↓ 下层入口 · 下降时进入", Mint)
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
            string heading = journey.RuinLootSwap ? "腾出一格" : journey.RuinLootSource.Kind == RuinObjectKind.Chest ? "打开的宝箱" : "途中发现的设备";
            GUI.Label(new Rect(x + 16, y + 9, 325, 29), heading, body);
            GUI.Label(new Rect(x + width - 184, y + 9, 167, 29), $"余量  {Journey.RuinCapacity - journey.RuinInventory} 格", body);
            for (int i = 0; i < choices.Count; i++)
            {
                var option = choices[i];
                bool selected = i == journey.RuinLootIndex;
                float row = y + 39 + 27 * i;
                Color accent = option.Action == RuinLootAction.Release ? PulseRose : option.Action == RuinLootAction.Leave ? Mint : Amber;
                if (selected) Paint(new Rect(x + 8, row, width - 16, 25), Opacity(accent, .16f));
                Orb(x + 23, row + 12, selected ? 10 : 6, accent, selected ? .85f : .38f);
                GUI.Label(new Rect(x + 37, row, width - 64, 28), option.Label, body);
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
                GUI.Label(new Rect(x + 18, y + 7, 270, 27), "观察隧道骨架", body);
                GUI.Label(new Rect(x + width - 62, y + 7, 50, 27), $"{Mathf.CeilToInt(remaining)} 秒", body);
                GUI.Label(new Rect(x + 18, y + 34, width - 36, 30), "A/B 左右看  ·  张手向上看  ·  发声向下看", body);
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
            GUI.Label(new Rect(left + 18, ySubtitle + 5, 90, 25), "机器人", body);
            GUI.Label(new Rect(left + 18, ySubtitle + 30, widthSubtitle - 36, 27), journey.Message, body);
        }

        private void DrawRuinTokenLedger(Journey journey)
        {
            const float x = 948, y = 79, width = 303;
            bool bargaining = journey.RuinRetryState == RuinRetryPhase.Bargain;
            bool settled = journey.RuinRevealedTokens == journey.RuinTokenCost;
            float height = bargaining ? 151 : 123;
            Paint(new Rect(x, y, width, height), InkGlass);
            Paint(new Rect(x, y, width, 2), settled ? Mint : GlassBlue);
            GUI.Label(new Rect(x + 14, y + 8, 136, 27), "出口结算", body);
            GUI.Label(new Rect(x + 171, y + 8, 119, 27),
                $"{journey.RuinRevealedTokens} / {journey.RuinTokenCost} token", body);
            Paint(new Rect(x + 15, y + 41, width - 30, 3), Opacity(GlassBlue, .20f));
            Paint(new Rect(x + 15, y + 41, (width - 30) * journey.RuinRevealedTokens / journey.RuinTokenCost, 3),
                settled ? Mint : GlassBlue);
            GUI.Label(new Rect(x + 14, y + 52, width - 28, 24),
                $"{Journey.RuinPhoneName} ×{journey.RuinPhones}  ·  每部 {Journey.RuinPhoneTokenRate} token", body);
            GUI.Label(new Rect(x + 14, y + 77, width - 28, 24),
                $"电脑 ×{journey.RuinComputers}  ·  每台 {Journey.RuinComputerTokenRate} token", body);
            if (bargaining)
                GUI.Label(new Rect(x + 14, y + 110, width - 28, 30),
                    $"补足报价  {journey.RuinTradePhones} 部手机 + {journey.RuinTradeComputers} 台电脑", body);
        }

        private void DrawRuinInventory(Journey journey)
        {
            if (ruinInventoryNumber == null)
            {
                ruinInventoryNumber = new GUIStyle(body)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false
                };
                ruinInventoryNumber.normal.textColor = Color.white;
            }
            const float top = 84;
            Vector2 heart = new Vector2(1025, top);
            Vector2 brain = new Vector2(1085, top);
            Vector2 phone = new Vector2(1145, top);
            Vector2 computer = new Vector2(1205, top);
            DrawRuinGlyph(heart, HeartGlyph, PulseRose);
            DrawRuinGlyph(brain, BrainGlyph, MemoryViolet);
            DrawRuinGlyph(brain, BrainFoldLeft, MemoryViolet);
            DrawRuinGlyph(brain, BrainFoldRight, MemoryViolet);
            DrawRuinGlyph(phone, PhoneGlyph, GlassBlue);
            DrawRuinGlyph(phone, PhoneHomeLine, GlassBlue);
            DrawRuinGlyph(computer, ComputerGlyph, Mint);
            DrawRuinGlyph(computer, ComputerStand, Mint);
            DrawRuinInventoryNumber(heart, journey.RuinHearts);
            DrawRuinInventoryNumber(brain, journey.RuinBrains);
            DrawRuinInventoryNumber(phone, journey.RuinPhones);
            DrawRuinInventoryNumber(computer, journey.RuinComputers);
        }

        private void DrawRuinGlyph(Vector2 origin, Vector2[] points, Color color)
        {
            for (int i = 1; i < points.Length; i++)
                Stroke(origin + points[i - 1], origin + points[i], 3.8f, RuinGlyphShadow, RuinGlyphShadow.a);
            for (int i = 1; i < points.Length; i++)
                Stroke(origin + points[i - 1], origin + points[i], 2.2f, color, 1);
        }

        private void DrawRuinInventoryNumber(Vector2 origin, int count)
        {
            var rect = new Rect(origin.x + 13, origin.y - 13, 32, 26);
            string number = count.ToString();
            Color previous = GUI.contentColor;
            GUI.contentColor = RuinGlyphShadow;
            GUI.Label(new Rect(rect.x - 1, rect.y + 1, rect.width, rect.height), number, ruinInventoryNumber);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), number, ruinInventoryNumber);
            GUI.contentColor = Color.white;
            GUI.Label(rect, number, ruinInventoryNumber);
            GUI.contentColor = previous;
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
                float alpha = find.visibility * .84f;
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
                        color = Amber;
                        RuinScreen(icon, size * 1.15f, size * .69f, color, alpha);
                        Orb(icon.x, icon.y, size * .26f, color, alpha);
                        break;
                }
                Orb(icon.x, icon.y, size * 1.95f, color, alpha * .18f);
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
