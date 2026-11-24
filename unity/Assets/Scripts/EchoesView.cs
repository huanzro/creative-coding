using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    public sealed class EchoesView
    {
        private const float Width = 1280, Height = 720, CenterX = 640;
        private readonly Texture2D far, arches, flowers, ruins, stars, spirit, glow, ring;
        private readonly List<Vector3> traces = new List<Vector3>(140);
        private readonly AtmosphereMotion atmosphere = new AtmosphereMotion();
        private readonly Color[] palette = {
            new Color(1f, .76f, .38f), new Color(.58f, .78f, 1f), new Color(.84f, .74f, 1f)
        };
        private float traceAt, lastDistance, lastJourneyDistance;
        private float lastTime;
        private int lastChapter = -1;
        private GUIStyle title, body, button;
        public int TimeOfDay { get; set; }
        public float ScrollSpeed { get; set; } = 1;
        private static readonly Color[] Sky = {
            new Color32(35,26,46,255), new Color32(18,33,62,255),
            new Color32(8,14,37,255), new Color32(70,40,52,255)
        };
        private static readonly float[] Tint = { .12f, .16f, .23f, .10f };
        private static readonly Vector3[] DanceBody = {
            new Vector3(0, 0, .03f), new Vector3(-9, 5, -.14f), new Vector3(-11, 3, -.20f),
            new Vector3(-7, -9, -.16f), new Vector3(1, -8, -.035f), new Vector3(8, 1, .13f),
            new Vector3(11, 5, .20f), new Vector3(7, -9, .17f), new Vector3(-1, -7, .06f),
            new Vector3(-5, 2, -.09f)
        };
        private static readonly float[] DanceAt = { 0, .11f, .19f, .29f, .38f, .48f, .57f, .70f, .80f, .91f };

        public EchoesView()
        {
            far = Load("chapter1-1");
            arches = Load("chapter1-0");
            flowers = Load("chapter1-2");
            ruins = Load("chapter2-0");
            stars = Load("chapter3-0");
            spirit = Load("forest-sylph-long-dress");
            glow = MakeMask(false);
            ring = MakeMask(true);
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

        private static void Paint(Rect rect, Color color, Texture2D texture = null)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.color = color;
            GUI.DrawTexture(rect, texture == null ? Texture2D.whiteTexture : texture);
            GUI.color = Color.white;
        }

        private static void BeginFrame()
        {
            float scale = Mathf.Min(Screen.width / Width, Screen.height / Height);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - Width * scale) / 2,
                (Screen.height - Height * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            GUI.BeginGroup(new Rect(0, 0, Width, Height));
        }

        private static void EndFrame()
        {
            GUI.EndGroup();
            GUI.matrix = Matrix4x4.identity;
        }

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
            }
            float dt = journey.Time - lastTime;
            atmosphere.Step(dt, journey.Gesture, journey.Voice, journey.Started, ScrollSpeed);
            lastDistance += (journey.Distance - lastJourneyDistance) * ScrollSpeed;
            lastJourneyDistance = journey.Distance;
            lastTime = journey.Time;
            if (journey.Time - traceAt < .05f) return;
            traceAt = journey.Time;
            traces.Add(new Vector3(lastDistance, CharacterY(journey.Time), journey.Voice));
            if (traces.Count > 120) traces.RemoveAt(0);
        }

        private static float CharacterY(float time) => Height * .51f + Mathf.Sin(time * 1.65f) * 8;

        public void DrawStage(Journey journey, bool splitProjection)
        {
            BeginFrame();
            var color = palette[journey.Chapter];
            float distance = lastDistance + atmosphere.Drift;
            Paint(new Rect(0, 0, Width, Height), new Color(.025f, .045f, .08f));
            if (journey.Chapter == 0)
            {
                Tiles(far, distance, .20f, 1.45f, -.22f, 1);
                Tiles(arches, distance, .48f, 1.29f, -.16f, .86f);
                Tiles(flowers, distance, .90f, 1.22f, -.08f, .94f);
            }
            else Tiles(journey.Chapter == 1 ? ruins : stars, distance, .60f, 1.4f, -.20f, 1);
            float period = TimeOfDay > 0 ? TimeOfDay - 1 : Mathf.Clamp01(journey.Round * .8f / 3 + journey.Time / 150) * 3;
            int index = Mathf.Min(2, Mathf.FloorToInt(period));
            float mix = Mathf.SmoothStep(0, 1, period - index);
            Paint(new Rect(0, 0, Width, Height), Opacity(Color.Lerp(Sky[index], Sky[index + 1], mix),
                Mathf.Lerp(Tint[index], Tint[index + 1], mix)));
            Paint(new Rect(0, 0, Width, Height), new Color(.012f, .039f, .094f, .33f * (1 - atmosphere.Wake)));
            Paint(new Rect(-300, -30, 1200, 980), Opacity(color, .04f + atmosphere.Wake * .28f), glow);
            Paint(new Rect(540, -330, 1040, 1060), Opacity(color, atmosphere.Wake * .22f + atmosphere.Energy * .15f), glow);
            float release = (journey.Time - journey.LastRelease) / 2.2f;
            if (release >= 0 && release < 1)
                Ripple(CenterX, CharacterY(journey.Time), 137 + 820 * release, color,
                    Mathf.Pow(1 - release, 1.4f) * atmosphere.Wake * .58f);
            for (int i = 0; i < 49; i++)
            {
                float x = Mathf.Repeat(i * 157.7f - distance * Height * (.2f + i % 3 * .15f), Width);
                float y = Mathf.Repeat(i * 91.3f + Mathf.Sin(journey.Time * .6f + i) * (13 + atmosphere.Energy * 15), Height);
                float alpha = (.12f + atmosphere.Wake * .49f) * (.48f + .52f * Mathf.Pow(Mathf.Sin(i + journey.Time * .7f), 2));
                Orb(x, y, 5 + i % 4 + atmosphere.Wake * 4, color, alpha);
            }
            DrawChallenge(journey, color);
            if (splitProjection)
                Paint(new Rect(100, -100, 1080, 940), new Color(0, 0, 0, .70f), glow);
            else
            {
                DrawTrace(journey, color);
                Character(journey.Chapter, journey.Time, journey.Light, journey.Charge,
                    journey.Gesture == Hand.Fist, false, false);
            }
            EndFrame();
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
                Rect uv = i % 2 == 0 ? new Rect(0, 0, 1, 1) : new Rect(1, 0, -1, 1);
                GUI.DrawTextureWithTexCoords(new Rect(i * width - offset, y * Height, width, height), texture, uv, true);
            }
            GUI.color = Color.white;
        }

        private void DrawTrace(Journey journey, Color color)
        {
            for (int i = 0; i < traces.Count; i++)
            {
                var point = traces[i];
                float alpha = (i + 1f) / traces.Count * (.14f + point.z * .5f);
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
                    float y = CharacterY(j.Time) + Mathf.Sin(j.Time * 2.2f + i * 1.7f) * 33;
                    Orb(x, y, 18, color, .85f);
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
                Ripple(j.WaveX * Width, CharacterY(j.Time), 68 + j.IncomingAmplitude * 105, incomingColor, .75f);
                if (j.HasResponse) Ripple(j.ResponseX * Width, CharacterY(j.Time), 82, color, .72f);
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

        private void Character(int chapter, float time, float light, float charge, bool closed, bool mirror, bool projection)
        {
            var color = palette[Mathf.Clamp(chapter, 0, 2)];
            float y = CharacterY(time);
            Orb(CenterX, y, 128 + light * 74, color, .29f + charge * .27f + light * .38f);
            if (light > .03f) Ripple(CenterX, y, 64 + (1 - light) * 122, color, light * .61f);
            if (closed) Ripple(CenterX, y, 75 + charge * 27, color, .32f + charge * .44f);
            float size = (closed ? 139 : 162) * (projection ? 4.1f : 1);
            float width = size * .68f;
            if (spirit == null)
            {
                Orb(CenterX, y, 42, color, 1);
                return;
            }
            if (Event.current.type != EventType.Repaint) return;
            // The Unity sprite follows the body timing of the original dance.
            float phase = Mathf.Repeat(time * .15f, 1);
            int cue = DanceAt.Length - 1;
            while (cue > 0 && phase < DanceAt[cue]) cue--;
            int next = (cue + 1) % DanceAt.Length;
            float blend = Mathf.SmoothStep(0, 1, (phase - DanceAt[cue]) / ((next == 0 ? 1 : DanceAt[next]) - DanceAt[cue]));
            Vector3 pose = Vector3.Lerp(DanceBody[cue], DanceBody[next], blend);
            float x = CenterX + (mirror ? -pose.x : pose.x) * (closed ? .25f : .7f);
            y += pose.y * .55f;
            Matrix4x4 before = GUI.matrix;
            GUIUtility.RotateAroundPivot((projection ? -90 : 0) + pose.z * (closed ? 12 : 38) * (mirror ? -1 : 1),
                new Vector2(x, y));
            GUI.color = Color.Lerp(Color.white, color, projection ? .07f : .18f);
            GUI.DrawTextureWithTexCoords(new Rect(x - width / 2, y - size / 2, width, size),
                spirit, mirror ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1), true);
            GUI.color = Color.white;
            GUI.matrix = before;
        }

        public void DrawProjection(int chapter, float time, float light, float charge, bool closed, bool mirror)
        {
            BeginFrame();
            Paint(new Rect(0, 0, Width, Height), Color.black);
            Character(chapter, time, light, charge, closed, mirror, true);
            EndFrame();
        }

        public void DrawControls(string heading, string message, string left, string right, float volume,
            Action<int> choose, Action<string> select, Action retry, Action restore, Action pause, bool paused,
            bool characterOnly, Action toggleSettings, Action<GUIStyle, GUIStyle> settings)
        {
            if (title == null)
            {
                Font font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "PingFang SC", "Arial" }, 22);
                title = new GUIStyle(GUI.skin.label) { font = font, fontSize = 27 };
                body = new GUIStyle(GUI.skin.label) { font = font, fontSize = 17 };
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 16 };
            }
            BeginFrame();
            if (characterOnly && settings == null) { EndFrame(); return; }
            if (GUI.Button(new Rect(1065, 27, 186, 36), settings == null ? "游戏设置 · F1" : "关闭设置 · F1", button))
                toggleSettings();
            Paint(new Rect(26, 22, 689, 100), new Color(.02f, .05f, .09f, .85f));
            GUI.Label(new Rect(44, 30, 630, 39), heading, title);
            GUI.Label(new Rect(44, 75, 650, 32), message, body);
            Paint(new Rect(26, 623, 1228, 70), new Color(.02f, .05f, .09f, .87f));
            if (GUI.Button(new Rect(42, 640, 164, 34), left, button)) choose(0);
            if (GUI.Button(new Rect(213, 640, 164, 34), right, button)) choose(1);
            GUI.Label(new Rect(393, 635, 348, 25), "空格握拳 · 松开张手 · Z轻声 · X舒适发声", body);
            Paint(new Rect(396, 663, 277, 5), new Color(1, 1, 1, .2f));
            Paint(new Rect(396, 663, volume * 277, 5), new Color(.81f, .85f, 1));
            if (GUI.Button(new Rect(687, 640, 76, 34), paused ? "继续" : "暂停", button)) pause();
            if (GUI.Button(new Rect(770, 640, 76, 34), "重试", button)) retry();
            if (GUI.Button(new Rect(853, 640, 76, 34), "恢复", button)) restore();
            string[] names = { "森林", "遗迹", "星海" };
            for (int i = 0; i < names.Length; i++)
                if (GUI.Button(new Rect(935 + i * 100, 640, 91, 34), names[i], button)) select(i.ToString());
            if (settings != null)
            {
                var panel = new Rect(724, 72, 527, 319);
                Paint(panel, new Color(.02f, .05f, .09f, .96f));
                GUI.BeginGroup(panel);
                settings(body, button);
                GUI.EndGroup();
            }
            EndFrame();
        }
    }
}
