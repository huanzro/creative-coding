using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesView : IDisposable
    {
        private const float Width = 1280, Height = 720, CenterX = 640;
        private readonly Texture2D far, arches, flowers, ruins, stars, glow, ring;
        private readonly SpiritRig spirit;
        private readonly List<Vector3> traces = new List<Vector3>(140);
        private readonly List<Vector4> flightTrace = new List<Vector4>(70);
        private float flightAt = float.NegativeInfinity;
        private int trailScene = -1;
        private readonly AtmosphereMotion atmosphere = new AtmosphereMotion();
        private readonly Color[] palette = {
            new Color(1f, .76f, .38f), new Color(.58f, .78f, 1f), new Color(.84f, .74f, 1f)
        };
        private float traceAt, lastDistance, lastJourneyDistance;
        private float lastTime;
        private float verticalPan;
        private int lastChapter = -1;
        private GUIStyle title, body, button;
        private Font interfaceFont;
        public int TimeOfDay { get; set; }
        public float ScrollSpeed { get; set; } = 1;
        private static readonly Color[] Sky = {
            new Color32(35,26,46,255), new Color32(18,33,62,255),
            new Color32(8,14,37,255), new Color32(70,40,52,255)
        };
        private static readonly float[] Tint = { .12f, .16f, .23f, .10f };
        public EchoesView()
        {
            far = Load("chapter1-1");
            arches = Load("chapter1-0");
            flowers = Load("chapter1-2");
            ruins = Load("chapter2-0");
            stars = Load("chapter3-0");
            spirit = new SpiritRig(Load("forest-sylph-long-dress"));
            glow = MakeMask(false);
            ring = MakeMask(true);
            openingVignette = MakeVignette();
            grassBlade = MakeBlade();
            PreparePlates();
        }

        private static Texture2D Load(string name) => Resources.Load<Texture2D>("OriginalArt/" + name);

        private static Texture2D MakeMask(bool outline)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            var colors = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x - (size - 1) * .5f, y - (size - 1) * .5f).magnitude / (size * .5f);
                float alpha = outline ? Mathf.Clamp01(1 - Mathf.Abs(distance - .83f) * 34) :
                    Mathf.Pow(Mathf.Clamp01(1 - distance), 2.3f);
                colors[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
            texture.SetPixels32(colors);
            texture.Apply(false, true);
            return texture;
        }

        private static Color Opacity(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static void Paint(Rect rect, Color color, Texture texture = null)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.color = color;
            GUI.DrawTexture(rect, texture == null ? Texture2D.whiteTexture : texture);
            GUI.color = Color.white;
        }

        private static Matrix4x4 BeginFrame(Rect viewport)
        {
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(viewport.width / Width, viewport.height / Height);
            GUI.matrix = previous * Matrix4x4.TRS(new Vector3(viewport.x + (viewport.width - Width * scale) / 2,
                viewport.y + (viewport.height - Height * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            GUI.BeginGroup(new Rect(0, 0, Width, Height));
            return previous;
        }

        private static void EndFrame(Matrix4x4 previous)
        {
            GUI.EndGroup();
            GUI.matrix = previous;
        }

        private static Rect ScreenViewport => new Rect(0, 0, Screen.width, Screen.height);

        public void Advance(Journey journey)
        {
            if (journey.Chapter != lastChapter || journey.Time < lastTime)
            {
                traces.Clear();
                atmosphere.Reset();
                lastTime = journey.Time;
                lastDistance = 0;
                lastJourneyDistance = journey.Distance;
                traceAt = 0;
                lastChapter = journey.Chapter;
                verticalPan = 0;
            }
            float dt = journey.Time - lastTime;
            atmosphere.Step(dt, journey.Gesture, journey.Voice, journey.Started, ScrollSpeed);
            lastDistance += (journey.Distance - lastJourneyDistance) * ScrollSpeed;
            lastJourneyDistance = journey.Distance;
            lastTime = journey.Time;
            verticalPan = Mathf.Lerp(verticalPan, Mathf.Lerp(-.085f, .10f, journey.Flight.Height),
                1 - Mathf.Exp(-Mathf.Max(0, dt) * 3));
            if (journey.Time - traceAt < .05f) return;
            traceAt = journey.Time;
            traces.Add(new Vector3(lastDistance, CharacterY(journey.Flight.Height), journey.Voice));
            if (traces.Count > 120) traces.RemoveAt(0);
        }

        private static float CharacterY(float altitude) => Height * SpiritFlight.ScreenY(altitude);

        public void DrawStage(Journey journey, SpiritFrame frame, bool splitProjection)
        {
            if (Event.current.type != EventType.Repaint) return;
            Matrix4x4 matrix = BeginFrame(ScreenViewport);
            if (journey.IsOpening)
            {
                DrawOpening(frame, splitProjection);
                EndFrame(matrix);
                return;
            }
            var color = palette[journey.Chapter];
            float distance = lastDistance + atmosphere.Drift;
            Paint(new Rect(0, 0, Width, Height), new Color(.025f, .045f, .08f));
            if (journey.Chapter == 0)
            {
                Tiles(far, distance, .20f, 1.55f, -.26f + verticalPan * .35f, 1);
                Tiles(arches, distance, .48f, 1.42f, -.19f + verticalPan * .60f, .86f);
                Tiles(flowers, distance, .90f, 1.37f, -.14f + verticalPan, .91f);
            }
            else Tiles(journey.Chapter == 1 ? ruins : stars, distance, .60f, 1.48f, -.22f + verticalPan, 1);
            float period = TimeOfDay > 0 ? TimeOfDay - 1 : Mathf.Clamp01(journey.Round * .8f / 3 + journey.Time / 150) * 3;
            int index = Mathf.Min(2, Mathf.FloorToInt(period));
            float mix = Mathf.SmoothStep(0, 1, period - index);
            Paint(new Rect(0, 0, Width, Height), Opacity(Color.Lerp(Sky[index], Sky[index + 1], mix),
                Mathf.Lerp(Tint[index], Tint[index + 1], mix)));
            Paint(new Rect(0, 0, Width, Height), new Color(.012f, .039f, .094f, .33f * (1 - atmosphere.Wake)));
            Paint(new Rect(-300, -30, 1200, 980), Opacity(color, .04f + atmosphere.Wake * .28f), glow);
            Paint(new Rect(540, -330, 1040, 1060), Opacity(color, atmosphere.Wake * .22f + atmosphere.Energy * .15f), glow);
            for (int i = 0; i < 27; i++)
            {
                float x = Mathf.Repeat(i * 157.7f - distance * Height * (.2f + i % 3 * .15f), Width);
                float y = Mathf.Repeat(i * 91.3f + Mathf.Sin(journey.Time * .6f + i) * (13 + atmosphere.Energy * 15), Height);
                float alpha = (.12f + atmosphere.Wake * .49f) * (.48f + .52f * Mathf.Pow(Mathf.Sin(i + journey.Time * .7f), 2));
                Orb(x, y, 4 + i % 3 + atmosphere.Wake * 2, Color.Lerp(color, new Color(.64f, .77f, .83f), .72f),
                    alpha * .58f);
            }
            if (journey.EnteredFromOpening && frame.motionTime < 2.4f)
                DrawOpeningArrival(1 - OpeningStoryboard.Ease(.15f, 2.4f, frame.motionTime));
            DrawChallenge(journey, color);
            if (splitProjection)
                Paint(new Rect(100, -100, 1080, 940), new Color(0, 0, 0, .53f), glow);
            DrawTrace(journey, color, splitProjection ? .28f : 1);
            float strength = splitProjection ? .40f : 1;
            DrawFlightTrail(frame, color, strength, 1, new Rect(0, 0, Width, Height));
            DrawActiveFirefly(journey, splitProjection);
            var center = new Vector2(CenterX, CharacterY(frame.altitude));
            var origin = new Vector2(CenterX, CharacterY(frame.releaseAltitude));
            DrawCharacter(frame, center, origin, 162, false, 0, 1, !splitProjection, strength);
            DrawLiveHandoff(frame, center, 162, false, 0, 1, splitProjection ? .24f : 1);
            EndFrame(matrix);
        }

        private static void Tiles(Texture2D texture, float distance, float speed, float zoom, float y, float opacity)
        {
            if (texture == null || Event.current.type != EventType.Repaint) return;
            float height = Height * zoom;
            float width = height * texture.width / texture.height;
            float offset = distance * Height * speed + width * .22f;
            int start = Mathf.FloorToInt(offset / width);
            GUI.color = new Color(1, 1, 1, opacity);
            for (int i = start; i * width - offset < Width; i++)
            {
                Rect uv = i % 2 == 0 ? new Rect(.0006f, 0, .9988f, 1) : new Rect(.9994f, 0, -.9988f, 1);
                GUI.DrawTextureWithTexCoords(new Rect(i * width - offset - .96f, y * Height, width + 1.92f, height),
                    texture, uv, true);
            }
            GUI.color = Color.white;
        }

        private void DrawTrace(Journey journey, Color color, float strength)
        {
            for (int i = 0; i < traces.Count; i++)
            {
                var point = traces[i];
                float alpha = (i + 1f) / traces.Count * (.14f + point.z * .5f) * strength;
                float x = CenterX - (lastDistance - point.x) * Width;
                Orb(x, point.y + Mathf.Sin(i * .5f + journey.Time) * 10, 7 + point.z * 20, color, alpha);
            }
        }

        private void DrawChallenge(Journey j, Color color)
        {
            if (j.Chapter == 0 && j.Lesson < 0)
            {
                for (int i = 0; i < j.Flock; i++)
                {
                    float x = CenterX - 92 - i * 23 + Mathf.Sin(j.Time * 2 + i) * 16;
                    float y = CharacterY(j.Flight.Height) + Mathf.Sin(j.Time * 2.2f + i * 1.7f) * 33;
                    Orb(x, y, 12, color, .56f);
                }
                if (j.Phase == Stage.Travel && j.Choice == 1)
                    for (int i = 0; i <= j.Round; i++)
                    {
                        float x = CenterX + ((i + 1f) / (j.Round + 2) - j.Progress) * Width;
                        Orb(x, Height * .53f, 180, new Color(.8f, .9f, 1), .28f);
                        Ripple(x, Height * .53f, 70, color, .37f);
                    }
                if (j.Phase == Stage.Nest) Ripple(Width * .73f, Height * .61f, 40 + 40 * j.Progress, color, .65f);
            }
            else if (j.Chapter == 1)
            {
                for (int i = 0; i < j.SlotCount; i++)
                {
                    float x = CenterX + (i - (j.SlotCount - 1) * .5f) * 109;
                    float y = Height * .72f;
                    Color cue = i < j.Recording.Count && j.Recording[i] == Hand.Open
                        ? new Color(1, .77f, .48f) : color;
                    float alpha = i == j.Slot ? .55f + .35f * j.Progress : .31f;
                    Ripple(x, y, 27 + (i == j.Slot ? j.Progress * 7 : 0), cue, alpha);
                    Orb(x, y, 25, cue, alpha);
                }
            }
            else if (j.Phase == Stage.Wave || j.Phase == Stage.WaveResult)
            {
                var incomingColor = j.Storm ? new Color(.88f, .48f, 1f) : new Color(.46f, .89f, 1f);
                Ripple(j.WaveX * Width, CharacterY(j.Flight.Height), 68 + j.IncomingAmplitude * 105, incomingColor, .75f);
                if (j.HasResponse) Ripple(j.ResponseX * Width, CharacterY(j.Flight.Height), 82, color, .72f);
                if (j.Phase == Stage.Wave && j.Gesture == Hand.Fist)
                {
                    float x = CenterX - 160;
                    float y = Height * .73f;
                    Paint(new Rect(x, y, 320, 4), new Color(1, 1, 1, .22f));
                    Paint(new Rect(x + Mathf.Clamp01(j.Target * .5f + .25f) * 320 - 25, y - 4, 50, 12), Opacity(color, .43f));
                    Orb(x + Mathf.Clamp01(j.Preview * .5f + .25f) * 320, y + 2, 12,
                        j.Aligned ? new Color(.59f, 1, .77f) : Color.white, .9f);
                }
            }
            else if (j.Chapter == 2 && j.Phrase.Count > 0)
            {
                if (j.Phase == Stage.Answer || j.Phase == Stage.Complete) DrawConstellation(j, color);
                for (int i = 0; i < j.Phrase.Count; i++)
                {
                    float x = CenterX + (i - 1) * 145;
                    float radius = 22 + Mathf.Abs(j.Phrase[i]) * 54;
                    Ripple(x, Height * .67f, radius, color, .78f);
                    if (j.Phase == Stage.Answer)
                        Ripple(x, Height * .67f, radius + 16 + Mathf.Sin(j.Time * 2 + i) * 12,
                            new Color(.48f, .86f, 1), .7f);
                }
            }
        }

        private void Orb(float x, float y, float radius, Color color, float alpha)
        {
            Paint(new Rect(x - radius, y - radius, radius * 2, radius * 2), Opacity(color, alpha), glow);
            if (radius < 45) Paint(new Rect(x - 1, y - 1, 2, 2), Opacity(color, alpha));
        }

        private void Ripple(float x, float y, float radius, Color color, float alpha) =>
            Paint(new Rect(x - radius, y - radius, radius * 2, radius * 2), Opacity(color, alpha), ring);

        public void UpdateSpirit(SpiritFrame frame)
        {
            spirit.Render(frame);
            if (frame.openingTime >= 0) UpdatePlates(frame.openingTime);
            int scene = frame.openingTime >= 0 ? 3 : frame.chapter;
            if (scene != trailScene || frame.motionTime < flightAt)
            {
                flightTrace.Clear();
                flightAt = float.NegativeInfinity;
                trailScene = scene;
            }
            if (scene == 3 || frame.motionTime - flightAt < .035f) return;
            flightAt = frame.motionTime;
            float power = Mathf.Clamp01(frame.glide * .78f + Mathf.Abs(frame.verticalSpeed) * .41f);
            flightTrace.Add(new Vector4(frame.altitude, frame.motionTime, power, frame.voice));
            while (flightTrace.Count > 0 && frame.motionTime - flightTrace[0].y > 2.05f)
                flightTrace.RemoveAt(0);
        }

        private static Matrix4x4 RotateAbout(Matrix4x4 parent, Vector2 center, float degrees) =>
            parent * Matrix4x4.Translate(center) * Matrix4x4.Rotate(Quaternion.Euler(0, 0, degrees))
                * Matrix4x4.Translate(-center);

        private void DrawCharacter(SpiritFrame frame, Vector2 center, Vector2 ringOrigin, float size,
            bool mirror, float rotation, float extent,
            bool showPortrait, float effects)
        {
            var color = palette[Mathf.Clamp(frame.chapter, 0, 2)];
            Orb(center.x, center.y, (128 + frame.light * 74) * extent, color,
                (.13f + frame.charge * .14f + frame.light * .20f) * effects);
            if (frame.releaseAge >= 0 && frame.releaseAge < 2.2f)
            {
                float progress = frame.releaseAge / 2.2f;
                float strength = Mathf.Pow(1 - progress, 1.45f) * effects;
                Vector2 lifted = Vector2.Lerp(ringOrigin, center, .62f);
                Ripple(lifted.x, lifted.y, (76 + progress * 230) * extent, color, strength * .56f);
                Ripple(ringOrigin.x, ringOrigin.y, (124 + progress * 660) * extent, color, strength * .27f);
            }
            if (frame.closed) Ripple(center.x, center.y, (75 + frame.charge * 27) * extent,
                color, (.20f + frame.charge * .26f) * effects);
            Matrix4x4 before = GUI.matrix;
            if (rotation != 0) GUI.matrix = RotateAbout(before, center, rotation);
            if (spirit.Articulated && frame.openingTime < 0)
                DrawGestureTrails(frame.motionTime, center, size, color, mirror, effects);
            if (showPortrait)
            {
                Texture texture = spirit.Texture;
                if (texture == null) Orb(center.x, center.y, 42, color, .65f);
                else
                {
                    float width = size * (spirit.Articulated ? SpiritRig.WidthScale : 2f / 3);
                    float height = size * (spirit.Articulated ? SpiritRig.HeightScale : 1);
                    GUI.DrawTextureWithTexCoords(new Rect(center.x - width / 2, center.y - height / 2, width, height),
                        texture, mirror ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1), true);
                }
            }
            GUI.matrix = before;
        }

        private void DrawGestureTrails(float time, Vector2 center, float size, Color color, bool mirror, float strength)
        {
            Vector2 scale = new Vector2(mirror ? -size : size, size);
            for (int anchor = 0; anchor < 4; anchor++)
            {
                var trace = spirit.Trace(anchor);
                Color tint = anchor % 2 == 0 ? color : Color.Lerp(color, new Color(.64f, .87f, .74f), .5f);
                for (int index = 1; index < trace.Count; index++)
                {
                    var point = trace[index];
                    float fade = Mathf.Pow(Mathf.Clamp01(1 - (time - point.Time) / .48f), 1.5f)
                        * point.Energy * strength;
                    if (fade < .015f) continue;
                    Vector2 a = center + Vector2.Scale(trace[index - 1].Position, scale);
                    Vector2 b = center + Vector2.Scale(point.Position, scale);
                    Vector2 direction = b - a;
                    if (direction.sqrMagnitude < .09f) continue;
                    Matrix4x4 previous = GUI.matrix;
                    GUI.matrix = RotateAbout(previous, a, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                    float thickness = Mathf.Clamp(size / (anchor < 2 ? 92 : 145), .65f, 3.2f);
                    Paint(new Rect(a.x, a.y - thickness / 2, direction.magnitude, thickness),
                        Opacity(tint, fade * .55f));
                    GUI.matrix = previous;
                }
            }
        }

        public void DrawControls(string heading, string message, string left, string right, float volume,
            Action<int> choose, Action<string> select, Action retry, Action restore, Action pause, bool paused,
            bool characterOnly, Action toggleSettings, Action<GUIStyle, GUIStyle> settings)
        {
            PrepareInterface();
            Matrix4x4 matrix = BeginFrame(ScreenViewport);
            if (characterOnly && settings == null) { EndFrame(matrix); return; }
            if (GUI.Button(new Rect(1065, 27, 186, 36), settings == null ? "游戏设置 · F1" : "关闭设置 · F1", button))
                toggleSettings();
            Paint(new Rect(26, 22, 689, 100), new Color(.02f, .05f, .09f, .85f));
            GUI.Label(new Rect(44, 30, 630, 39), heading, title);
            GUI.Label(new Rect(44, 74, 650, 41), message, body);
            Paint(new Rect(26, 650, 1228, 50), new Color(.02f, .05f, .09f, .86f));
            if (GUI.Button(new Rect(42, 658, 164, 34), left, button)) choose(0);
            if (GUI.Button(new Rect(213, 658, 164, 34), right, button)) choose(1);
            GUI.Label(new Rect(393, 651, 310, 25), "空格握拳 · 松开张手 · Z轻声 · X加速", body);
            Paint(new Rect(396, 680, 270, 4), new Color(1, 1, 1, .2f));
            Paint(new Rect(396, 680, volume * 270, 4), new Color(.81f, .85f, 1));
            if (GUI.Button(new Rect(687, 658, 76, 34), paused ? "继续" : "暂停", button)) pause();
            if (GUI.Button(new Rect(770, 658, 76, 34), "重试", button)) retry();
            if (GUI.Button(new Rect(853, 658, 76, 34), "恢复", button)) restore();
            string[] names = { "试玩", "森林", "遗迹", "星海" };
            for (int i = 0; i < names.Length; i++)
                if (GUI.Button(new Rect(935 + i * 78, 658, 72, 34), names[i], button)) select((i - 1).ToString());
            if (settings != null)
            {
                var panel = new Rect(724, 72, 527, 410);
                Paint(panel, new Color(.02f, .05f, .09f, .96f));
                GUI.BeginGroup(panel);
                settings(body, button);
                GUI.EndGroup();
            }
            EndFrame(matrix);
        }

        private void PrepareInterface()
        {
            if (title != null) return;
            interfaceFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "PingFang SC", "Arial" }, 22);
            title = new GUIStyle(GUI.skin.label) { font = interfaceFont, fontSize = 27 };
            body = new GUIStyle(GUI.skin.label) { font = interfaceFont, fontSize = 17, wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { font = interfaceFont, fontSize = 16 };
        }

        public void DrawOpeningControls(float time, Action skip)
        {
            PrepareInterface();
            Matrix4x4 matrix = BeginFrame(ScreenViewport);
            if (time > .75f && GUI.Button(new Rect(1100, 660, 140, 29), "跳过 · Esc", button)) skip();
            EndFrame(matrix);
        }

        public void Dispose()
        {
            spirit.Dispose();
            ReleasePlates();
            UnityEngine.Object.Destroy(glow);
            UnityEngine.Object.Destroy(ring);
            UnityEngine.Object.Destroy(openingVignette);
            UnityEngine.Object.Destroy(grassBlade);
            if (interfaceFont != null) UnityEngine.Object.Destroy(interfaceFont);
        }
    }
}
