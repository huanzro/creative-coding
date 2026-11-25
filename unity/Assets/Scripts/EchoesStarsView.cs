using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesView
    {
        private static readonly Color StarPearl = new Color(.88f, .95f, 1);
        private static readonly Color StarViolet = new Color(.66f, .59f, .91f);
        private static readonly Color StarSlate = new Color(.30f, .40f, .59f);
        private static readonly Rect[] StarCards = { new Rect(94, 148, 520, 336), new Rect(666, 148, 520, 336) };
        private static readonly Rect[] StarGallery = {
            new Rect(10, 104, 686, 366), new Rect(582, 76, 686, 366), new Rect(295, 265, 690, 368)
        };
        private static readonly string[] StarActs = { "星光天平", "请等我看清", "借你一步" };
        private Material starInk;
        private RenderTexture starAccumulation, starCanvas;
        private GUIStyle starMotifTitle, starCaption, starRightCaption;

        private enum InkLayer { Preview, Guide, Borrow, Drawn }

        private void PrepareStarCanvas()
        {
            var shader = Resources.Load<Shader>("StarSea/StarTrailInk");
            if (shader == null || !shader.isSupported) return;
            starInk = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            starAccumulation = StarTexture("Star Sea layered ink");
            starCanvas = StarTexture("Star Sea projection ink");
        }

        private static RenderTexture StarTexture(string name) =>
            new RenderTexture((int)Width, (int)Height, 0, RenderTextureFormat.ARGB32) {
                name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };

        private void UpdateStarCanvas(Journey j)
        {
            if (starInk == null) PrepareStarCanvas();
            if (starInk == null) return;
            var before = RenderTexture.active;
            RenderTexture.active = starAccumulation;
            GL.Clear(false, true, Color.clear);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Width, Height, 0);
            if (starInk.SetPass(0))
            {
                GL.Begin(GL.QUADS);
                // Broad diffuse light, colored ribbon and fine silver edge.
                for (int pass = 0; pass < 3; pass++) DrawStarGeometry(j, pass);
                GL.End();
            }
            GL.PopMatrix();
            Vector2 center = StarTrailArt.Actor(j);
            starInk.SetVector("_InkProjection", new Vector4(center.x / Width, 1 - center.y / Height,
                ProjectionLighting.CoreRadius(122), ProjectionLighting.Spread(ProjectionFalloff)));
            starInk.SetVector("_InkCenterExposure", new Vector4(ProjectionGradient ? ProjectionLighting.CenterGain(ProjectionFalloff) : 1,
                ProjectionLighting.Tone(.44f, ScreenBrightness) / .44f, 0, 0));
            Graphics.Blit(starAccumulation, starCanvas, starInk, 1);
            RenderTexture.active = before;
        }

        private void DrawStarGeometry(Journey j, int pass)
        {
            for (int i = 0; i < j.StarCount; i++)
            {
                Rect bounds = CompletedStarBounds(j, i);
                DrawInk(StarTrailArt.Find(j.StarMotifAt(i)), bounds, 0, 1, InkLayer.Drawn, pass,
                    j.StarMachineStartAt(i), j.StarMachineEndAt(i), bounds.width > 500 ? .87f : .63f, j.PoseTime);
            }
            if (j.Phase == Stage.StarsRoute)
            {
                DrawInk(StarTrailArt.Find(j.StarLeft), StarPreview(0), 0, 1, InkLayer.Preview, pass, time: j.PoseTime);
                DrawInk(StarTrailArt.Find(j.StarRight), StarPreview(1), 0, 1, InkLayer.Preview, pass, time: j.PoseTime);
            }
            if (!j.StarDrawing) return;
            var trail = StarTrailArt.Find(j.StarMotif);
            float reveal = OpeningStoryboard.Ease(0, Journey.StarArrivalSeconds, j.Age);
            Rect active = StarTrailArt.ActiveBounds;
            DrawInk(trail, active, j.StarProgress, 1, InkLayer.Guide, pass, alpha: reveal);
            if (j.StarBorrowAhead || j.StarCanBorrow || j.Phase == Stage.StarsDelegated)
                DrawInk(trail, active, Mathf.Max(Journey.StarHelpStart, j.StarProgress), Journey.StarHelpEnd,
                    InkLayer.Borrow, pass, alpha: .52f + j.StarAuthorization * .48f);
            DrawInk(trail, active, 0, j.StarProgress, InkLayer.Drawn, pass, j.StarMachineStart, j.StarMachineEnd, time: j.PoseTime);
        }

        private static void DrawInk(StarTrailArt trail, Rect bounds, float from, float to, InkLayer layer, int pass,
            float machineFrom = 0, float machineTo = 0, float alpha = 1, float time = 0)
        {
            if (trail == null || to <= from || alpha < .01f || layer == InkLayer.Guide && pass != 2 ||
                layer == InkLayer.Borrow && pass == 0) return;
            bool dotted = layer == InkLayer.Guide || layer == InkLayer.Borrow;
            int samples = trail.Points.Length - 1;
            int step = dotted ? 3 : bounds.width > 550 ? 2 : 3;
            float widthScale = Mathf.Clamp(Mathf.Sqrt(bounds.width / 1000), .54f, 1.15f);
            float radius = widthScale * (pass == 0 ? 12.5f : pass == 1 ? 4.3f : 1.38f);
            float strength = alpha * (pass == 0 ? .17f : pass == 1 ? .49f : .94f);
            if (layer == InkLayer.Preview) strength *= .78f;
            if (dotted) { radius *= .87f; strength *= layer == InkLayer.Borrow ? .67f : .34f; }

            for (int i = (int)(from * samples / step) * step; i < samples; i += step)
            {
                float left = Mathf.Max(from, i / (float)samples);
                if (left >= to) break;
                if (dotted && (i / step & 1) != 0) continue;
                float right = Mathf.Min(to, (i + step) / (float)samples);
                if (right <= left) continue;
                Vector2 start = trail.Point(bounds, left);
                Vector2 end = trail.Point(bounds, right);
                float along = (left + right) * .5f;
                bool machine = layer == InkLayer.Borrow || along >= machineFrom && along < machineTo;
                Color color = trail.Pigment(along, machine);
                if (layer == InkLayer.Preview && trail.NormalizedPoint(along).y < StarMotifs.UpperEdge)
                    color = Color.Lerp(color, StarViolet, .56f);
                if (pass == 2) color = Color.Lerp(color, StarPearl, machine ? .27f : .42f);
                Vector2 segment = end - start;
                if (segment.sqrMagnitude < .00001f) continue;
                Vector2 sideAtStart = StarCross(trail, left) * radius;
                Vector2 sideAtEnd = StarCross(trail, right) * radius;
                Color center = Opacity(color, strength);
                Color edge = Opacity(color, 0);
                InkHalf(start, end, start + sideAtStart, end + sideAtEnd, center, edge);
                InkHalf(end, start, end - sideAtEnd, start - sideAtStart, center, edge);
            }
            if (pass == 2 && !dotted && bounds.width > 380)
            {
                int count = Mathf.Clamp(samples / 14, 23, 48);
                for (int i = 1; i < count; i++)
                {
                    float along = (i + .4f * Mathf.Sin(i * 7.29f)) / count;
                    if (along > to) break;
                    float drift = Mathf.Sin(i * 11.7f + time * .29f) * 4.5f;
                    float scatter = 3 + Mathf.Abs(Mathf.Sin(i * 12.31f)) * 13;
                    Vector2 point = trail.Point(bounds, along) + StarCross(trail, along)
                        * ((i & 1) == 0 ? scatter + drift : -scatter + drift) * widthScale;
                    float pulse = .23f + .28f * Mathf.Pow(.5f + .5f * Mathf.Sin(i * 8.8f + time * .8f), 2);
                    Color tint = trail.Pigment(along, along >= machineFrom && along < machineTo);
                    InkGrain(point, i % 8 == 1 ? 2.2f : 1.12f, Opacity(tint, alpha * pulse));
                }
            }
            if (pass != 2 || machineTo <= machineFrom) return;
            float spacing = 10f / samples;
            for (float along = machineFrom + spacing * .5f; along < machineTo; along += spacing)
            {
                Vector2 bead = trail.Point(bounds, along) + StarCross(trail, along) * (4.2f * widthScale);
                InkGrain(bead, 1.72f * widthScale, Opacity(StarTrailArt.SystemInk, alpha * .68f));
            }
        }

        private static Vector2 StarCross(StarTrailArt trail, float progress)
        {
            Vector2 tangent = trail.Direction(progress);
            return new Vector2(-tangent.y, tangent.x);
        }

        private static void InkGrain(Vector2 point, float radius, Color color)
        {
            Vector2 a = point - Vector2.right * radius, b = point + Vector2.right * radius;
            InkHalf(a, b, point - Vector2.up * radius, point - Vector2.up * radius, color, Opacity(color, 0));
            InkHalf(b, a, point + Vector2.up * radius, point + Vector2.up * radius, color, Opacity(color, 0));
        }

        private static void InkHalf(Vector2 a, Vector2 b, Vector2 outerA, Vector2 outerB, Color line, Color edge)
        {
            GL.Color(line);
            GL.Vertex3(a.x, a.y, 0);
            GL.Vertex3(b.x, b.y, 0);
            GL.Color(edge);
            GL.Vertex3(outerB.x, outerB.y, 0);
            GL.Vertex3(outerA.x, outerA.y, 0);
        }

        private static Rect StarPreview(int branch) =>
            new Rect(StarCards[branch].x + 10, StarCards[branch].y + 36, 500, 500 * 640f / 1200);

        private static Rect StarDock(int index) => new Rect(146 + index * 347, 519, 294, 294 * 640f / 1200);

        private static Rect CompletedStarBounds(Journey j, int index)
        {
            bool workshop = j.Phase == Stage.StarsWorkshop;
            Rect dock = StarDock(index);
            if (workshop) return StarGallery[index];
            if (j.Phase != Stage.StarsRest) return dock;
            Rect from = index == j.StarCount - 1 ? StarTrailArt.ActiveBounds : dock;
            Rect to = j.StarCount == Journey.StarTrailCount ? StarGallery[index] : dock;
            float mix = OpeningStoryboard.Ease(.18f, Journey.StarSettleSeconds, j.Age);
            return new Rect(Vector2.Lerp(from.position, to.position, mix), Vector2.Lerp(from.size, to.size, mix));
        }

        private void DrawStarStage(Journey j, SpiritFrame frame, bool spatial, bool split)
        {
            PrepareInterface();
            if (starCaption == null)
            {
                starMotifTitle = new GUIStyle(title) { fontSize = 25 };
                starCaption = new GUIStyle(body) { fontSize = 16, wordWrap = false };
                starRightCaption = new GUIStyle(starCaption) { alignment = TextAnchor.MiddleRight };
            }
            if (!spatial) Paint(new Rect(0, 0, Width, Height), BackgroundTint(new Color(.020f, .034f, .105f, .38f)));
            DrawStarDust(j);
            if (j.Phase == Stage.StarsRoute)
            {
                DrawStarCard(j.StarLeft, 0, j.StarRecommendation);
                DrawStarCard(j.StarRight, 1, j.StarRecommendation);
            }
            if (starCanvas != null) Paint(new Rect(0, 0, Width, Height), Color.white, starCanvas);
            DrawStarStamps(j);
            if (j.Phase == Stage.StarsRoute)
            {
                StarText(new Rect(98, 495, 775, 26), "评分偏好短路线与较少经过亮带的图案。", starCaption, StarPearl);
                StarText(new Rect(720, 495, 461, 26), j.StarRecommendation == 0
                    ? $"分差 {j.StarScoreMargin}  ·  交给你选择"
                    : $"分差 {j.StarScoreMargin}  ·  建议仅供参考", starRightCaption, StarViolet);
            }
            if (j.StarDrawing)
            {
                Vector2 center = StarTrailArt.Actor(j);
                DrawStarHead(j, split, center);
                if (!spatial && !split) DrawCharacter(frame, center, center, 122, false, 0, 1, true, 0);
                DrawStarStatus(j);
            }
            else if (!spatial && !split)
            {
                Vector2 center = StarTrailArt.Actor(j);
                DrawCharacter(frame, center, center, 122, false, 0, 1, true, 0);
            }
            if (j.Phase != Stage.StarsWorkshop)
                StarText(new Rect(824, 72, 364, 29), $"{StarActs[Mathf.Min(j.StarCount, 2)]}   ·   {j.StarCount} / {Journey.StarTrailCount}",
                    starRightCaption, StarPearl);
            if (j.StarCount == 3)
                StarText(new Rect(574, 603, 608, 28), "图案原色  自己描画     /     青蓝点迹  系统绘制",
                    starRightCaption, StarPearl);
        }

        private void DrawStarCard(int id, int branch, int recommendation)
        {
            var trail = StarTrailArt.Find(id);
            if (trail == null) return;
            Rect card = StarCards[branch];
            Rect plot = StarPreview(branch);
            bool suggested = id == recommendation;
            Paint(card, new Color(.032f, .054f, .116f, .83f));
            Paint(new Rect(card.x, card.y, card.width, 1), Opacity(suggested ? Amber : StarSlate, .65f));
            float windY = plot.y + plot.height * StarMotifs.UpperEdge;
            Paint(new Rect(card.x + 22, card.y + 61, card.width - 44, windY - card.y - 61), Opacity(StarViolet, .063f));
            Paint(new Rect(card.x + 22, windY, card.width - 44, 1), Opacity(StarViolet, .23f));
            StarText(new Rect(card.x + 21, card.y + 11, 375, 34), (branch == 0 ? "A   " : "B   ") + trail.Name,
                starMotifTitle, StarPearl);
            if (suggested)
                StarText(new Rect(card.x + 374, card.y + 16, 123, 24), "系统建议", starRightCaption, Amber);
            StarText(new Rect(card.x + 22, card.yMax - 36, 364, 25),
                $"路长 {StarMotifs.Length(id):0}    /    亮带 {StarMotifs.UpperShare(id)}%", starCaption, StarPearl);
            StarText(new Rect(card.x + 376, card.yMax - 36, 120, 25), $"{StarMotifs.Score(id)} 分", starRightCaption, StarPearl);
        }

        private void DrawStarStamps(Journey j)
        {
            if (j.Phase == Stage.StarsWorkshop || j.Phase == Stage.StarsRest && j.StarCount == 3) return;
            int count = j.Phase == Stage.StarsRest ? j.StarCount - 1 : j.StarCount;
            for (int i = 0; i < count; i++)
            {
                Rect dock = StarDock(i);
                var trail = StarTrailArt.Find(j.StarMotifAt(i));
                Paint(new Rect(dock.x + 25, 611, dock.width - 50, 1), Opacity(StarSlate, .56f));
                StarText(new Rect(dock.x + 27, 616, 238, 22), trail?.Name ?? "", starCaption, StarPearl);
            }
        }

        private void DrawStarStatus(Journey j)
        {
            var trail = StarTrailArt.Find(j.StarMotif);
            if (trail == null) return;
            Rect meter = new Rect(906, 570, 278, 4);
            bool system = j.Phase == Stage.StarsDelegated;
            Color color = system ? StarTrailArt.SystemInk : trail.Pigment(j.StarProgress, false);
            Paint(meter, Opacity(StarSlate, .58f));
            Paint(new Rect(meter.x, meter.y, meter.width * j.StarProgress, meter.height), Opacity(color, .92f));
            if (j.StarMachineEnd > j.StarMachineStart)
                Paint(new Rect(meter.x + meter.width * j.StarMachineStart, meter.y,
                    meter.width * (j.StarMachineEnd - j.StarMachineStart), meter.height), StarTrailArt.SystemInk);
            StarText(new Rect(meter.x, meter.y - 32, meter.width, 28),
                $"{trail.Name}   ·   {j.StarProgress:0%}", starRightCaption, StarPearl);
            if (j.StarCanBorrow || system)
            {
                string text = system ? "蓝线由系统绘制  ·  握拳收回" :
                    j.StarAuthorization >= 1 ? "张手授权蓝线" : "握拳 0.6 秒可授权蓝线";
                StarText(new Rect(794, 582, 390, 25), text, starRightCaption, StarTrailArt.SystemInk);
                if (j.StarAuthorization > 0)
                    Paint(new Rect(meter.xMax - 96 * j.StarAuthorization, 612, 96 * j.StarAuthorization, 2), StarTrailArt.SystemInk);
            }
        }

        private void DrawStarHead(Journey j, bool split, Vector2 actor)
        {
            var trail = StarTrailArt.Find(j.StarMotif);
            if (trail == null) return;
            float t = j.PoseTime;
            Vector2 head = trail.Point(StarTrailArt.ActiveBounds, j.StarProgress);
            Color pigment = trail.Pigment(j.StarProgress, j.Phase == Stage.StarsDelegated);
            float awake = OpeningStoryboard.Ease(0, Journey.StarArrivalSeconds, j.Age);
            float active = j.Gesture == Hand.Open ? .44f + j.StarFlow * .46f : .22f;
            float gain = active * awake * StarGain(head, actor);
            Orb(head.x, head.y, 29, pigment, gain);
            Orb(head.x, head.y, 6.5f, StarPearl, gain);
            float spin = (j.PoseTime - j.LastSpin) / 1.25f;
            if (spin >= 0 && spin < 1)
                Ripple(head.x, head.y, Mathf.Lerp(17, 58, spin), pigment, gain * (1 - spin) * (split ? .31f : .64f));
            if (j.StarFlow <= 0 || j.StarProgress <= 0) return;
            for (int i = 0; i < 13; i++)
            {
                float age = Mathf.Repeat(t * (.47f + i % 3 * .12f) + i * .717f, 1);
                float behind = Mathf.Max(0, j.StarProgress - age * .052f);
                Vector2 p = trail.Point(StarTrailArt.ActiveBounds, behind);
                Vector2 tangent = trail.Direction(behind);
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                p += normal * (Mathf.Sin(i * 2.4f) * (2 + age * 25)) - Vector2.up * age * 12;
                float alpha = (1 - age) * (.32f + j.StarFlow * .30f) * awake * StarGain(p, actor);
                Orb(p.x, p.y, 2.2f + i % 4, Color.Lerp(pigment, StarPearl, .3f), alpha);
            }
        }

        private void DrawStarDust(Journey j)
        {
            float t = j.PoseTime;
            Vector2 actor = StarTrailArt.Actor(j);
            for (int i = 0; i < 42; i++)
            {
                float x = Mathf.Repeat(i * 213.7f + Mathf.Sin(t * .15f + i) * 10 - j.Distance * (3 + i % 5), Width);
                float y = 126 + Mathf.Repeat(i * 173.3f + Mathf.Cos(t * .18f + i) * 8, 483);
                float shine = .17f + .20f * Mathf.Pow(.5f + .5f * Mathf.Sin(t * (.53f + i % 7 * .13f) + i * 3), 3);
                Color tint = BackgroundTint(i % 7 == 0 ? Amber : StarPearl);
                Orb(x, y, i % 13 == 0 ? 11 : 2.5f + i % 4, tint, shine * StarGain(new Vector2(x, y), actor));
            }
        }

        private float StarGain(Vector2 at, Vector2 actor)
        {
            float radial = ProjectionGradient
                ? ProjectionLighting.RadialGain(Vector2.Distance(at, actor) / Height, ProjectionLighting.CoreRadius(122), ProjectionFalloff)
                : 1;
            return radial * ProjectionLighting.Tone(.44f, ScreenBrightness) / .44f;
        }

        private void DrawPortraitStar(SpiritFrame frame, Vector2 center, float size, bool mirror, bool sideways, float extent)
        {
            var trail = StarTrailArt.Find(frame.starMotif);
            if (trail == null) return;
            float rotation = sideways ? -90 : 0;
            Vector2 tip = center + PortraitOffset(HandPosition, mirror, rotation) * size;
            Color pigment = trail.Pigment(frame.starProgress, frame.starDelegated);
            float energy = frame.hand == Hand.Open ? .51f + frame.starFlow * .39f : .16f;
            float reach = Mathf.Min(size * .65f, Mathf.Max(size * .24f, 67 * extent));
            Orb(tip.x, tip.y, Mathf.Min(size * .19f, 26 * extent), pigment, energy);
            Vector2 cut = trail.NormalizedPoint(frame.starProgress);
            for (int i = 1; i <= 16; i++)
            {
                float fraction = i / 16f;
                float past = frame.starProgress - fraction * (.044f + .037f * frame.starFlow);
                if (past < 0) break;
                Vector2 turn = trail.NormalizedPoint(past) - cut;
                turn = new Vector2(turn.x * 5.8f, turn.y * 3.5f);
                turn.y += Mathf.Sin(i * .72f - frame.motionTime * 4) * fraction * .11f;
                Vector2 point = tip + PortraitOffset(turn, mirror, rotation) * reach;
                bool machine = past >= frame.starMachineStart && past < frame.starMachineEnd;
                Orb(point.x, point.y, Mathf.Lerp(5.1f, 2f, fraction) * extent,
                    trail.Pigment(past, machine), (1 - fraction) * energy);
            }
        }

        private static void StarText(Rect where, string text, GUIStyle font, Color tint)
        {
            Color before = GUI.contentColor;
            GUI.contentColor = tint;
            GUI.Label(where, text, font);
            GUI.contentColor = before;
        }

        private void ReleaseStarCanvas()
        {
            if (starInk != null) Object.Destroy(starInk);
            if (starAccumulation != null) { starAccumulation.Release(); Object.Destroy(starAccumulation); }
            if (starCanvas != null) { starCanvas.Release(); Object.Destroy(starCanvas); }
        }
    }
}
