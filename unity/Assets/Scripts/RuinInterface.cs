using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesView
    {
        private static readonly Color GlassBlue = new Color(.43f, .84f, 1f);
        private static readonly Color MemoryViolet = new Color(.77f, .67f, 1f);
        private static readonly Color PulseRose = new Color(1f, .45f, .49f);

        private void DrawRuinHud(Journey journey)
        {
            PrepareInterface();
            float x = 982;
            Paint(new Rect(x, 79, 269, 148), InkGlass);
            Paint(new Rect(x, 79, 269, 2), Opacity(GlassBlue, .54f));
            GUI.Label(new Rect(x + 14, 89, 119, 26), "途中所拾", body);
            GUI.Label(new Rect(x + 181, 89, 77, 26), $"{journey.RuinInventory} / {Journey.RuinCapacity}", body);
            Paint(new Rect(x + 16, 121, 237, 3), Opacity(GlassBlue, .18f));
            Paint(new Rect(x + 16, 121, 237 * (journey.RuinInventory / (float)Journey.RuinCapacity), 3), GlassBlue);
            RuinCount(x + 17, 134, "大脑", journey.RuinBrains, MemoryViolet);
            RuinCount(x + 139, 134, "心脏", journey.RuinHearts, PulseRose);
            RuinCount(x + 17, 162, "手机", journey.RuinPhones, GlassBlue);
            RuinCount(x + 139, 162, "电脑", journey.RuinComputers, Mint);
            GUI.Label(new Rect(x + 14, 193, 239, 25), $"已发现宝箱  {journey.RuinChests} / 10", body);

            string level = journey.RuinLayer == 0 ? "中层" : journey.RuinLayer > 0
                ? $"上层 +{journey.RuinLayer}" : $"下层 {journey.RuinLayer}";
            Paint(new Rect(26, 130, 332, 35), InkGlass);
            GUI.Label(new Rect(43, 134, 310, 27), $"{level}   ·   {journey.RuinRoomName}", body);
            if (journey.RuinGuide != null && journey.RuinGateCharge > 0)
            {
                Paint(new Rect(44, 116, 186, 3), Opacity(GlassBlue, .19f));
                Paint(new Rect(44, 116, 186 * Mathf.Clamp01(journey.RuinGateCharge), 3), GlassBlue);
            }
        }

        private void RuinCount(float x, float y, string label, int count, Color color)
        {
            Orb(x + 5, y + 13, 10, color, count > 0 ? .85f : .35f);
            GUI.Label(new Rect(x + 17, y, 99, 27), $"{label}  {count}", body);
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
