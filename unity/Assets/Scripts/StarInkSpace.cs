using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    [Serializable]
    internal sealed class StarInkMark
    {
        public int motif;
        public float born;
        public Vector3 origin;
        public Vector2 portraitTip;
        public List<Vector3> points;
    }

    [Serializable]
    internal sealed class StarInkFrame
    {
        public float time, depth;
        public StarInkMark[] marks;
        public bool pointing;
        public Vector3 cursor, hand;
        public Vector2 portraitHand;
    }

    internal sealed class StarInkSpace : IDisposable
    {
        public const float PerformerDepth = 5.8f, FieldOfView = 49f;
        public const float Aspect = 16f / 9;
        private const int MaxMarks = 24, MaxSamples = 300;
        private readonly List<StarInkMark> marks = new List<StarInkMark>(MaxMarks);
        private StarInkFrame frame = new StarInkFrame();
        private StarInkMark pen;
        private Vector2 filtered;
        private Vector2 previousTip;
        private float prevTime, penTime;
        private int paper = -1;
        private StarInkMesh worldInk, portraitInk;
        private CommandBuffer portraitCommands;
        private Material material;
        private int worldPass, portraitPass, alphaPass;
        private RenderTexture inkLayer, portraitLayer;

        public int Revision { get; private set; }
        public StarInkFrame Frame => frame;

        public void Update(Journey journey, Camera camera, Vector3 atFinger, Vector2 portraitTip)
        {
            if (paper != journey.StarPaper) Clear(journey.StarPaper);
            frame.depth = journey.Distance;
            frame.time = journey.Time;
            Vector3 origin = camera.transform.InverseTransformPoint(atFinger);
            origin.z += frame.depth;
            frame.hand = origin;
            frame.portraitHand = portraitTip;
            bool changed = false;
            while (journey.TryTakeStarStamp(out int motif))
            {
                Add(new StarInkMark { motif = motif, born = frame.time, origin = origin, portraitTip = portraitTip });
                changed = true;
            }

            StarTip input = journey.StarPointer;
            frame.pointing = input.Valid;
            if (!input.Valid)
            {
                pen = null;
            }
            else
            {
                Vector2 sample = new Vector2(input.X, input.Y);
                float dt = Mathf.Clamp(frame.time - prevTime, 0, .09f);
                if (pen != null && (sample - previousTip).sqrMagnitude > .33f * .33f)
                    pen = null;
                if (pen == null)
                {
                    filtered = sample;
                    Vector3 seed = PointInSky(filtered, frame.depth);
                    pen = new StarInkMark { born = frame.time, origin = seed, points = new List<Vector3>(96) { seed } };
                    Add(pen);
                    changed = true;
                    penTime = frame.time;
                }
                else if (dt > 0)
                {
                    float rate = Mathf.Lerp(12, 27, Mathf.Clamp01((sample - filtered).magnitude * 5));
                    filtered = Vector2.Lerp(filtered, sample, 1 - Mathf.Exp(-dt * rate));
                    Vector3 point = PointInSky(filtered, frame.depth);
                    Vector3 last = pen.points[pen.points.Count - 1];
                    float distance = (point - last).sqrMagnitude;
                    if (distance >= .026f * .026f || distance > .006f * .006f && frame.time - penTime > .09f)
                    {
                        if (pen.points.Count >= MaxSamples) pen.points.RemoveRange(0, 20);
                        pen.points.Add(point);
                        penTime = frame.time;
                        changed = true;
                    }
                }
                frame.cursor = PointInSky(filtered, frame.depth);
                previousTip = sample;
            }
            prevTime = frame.time;

            // Leave finished marks suspended in world space. Prune only work already behind the viewer.
            for (int i = marks.Count - 1; i >= 0; i--)
            {
                var mark = marks[i];
                if (mark == pen || LastDepth(mark) >= frame.depth + .15f) continue;
                marks.RemoveAt(i);
                RefreshMarks();
                changed = true;
            }
            if (changed) Revision++;
        }

        private static Vector3 PointInSky(Vector2 screen, float depth)
        {
            float halfY = PerformerDepth * Mathf.Tan(FieldOfView * .5f * Mathf.Deg2Rad);
            return new Vector3((screen.x - .5f) * (2 * halfY * Aspect * .82f),
                (.5f - screen.y) * (2 * halfY * .71f), depth + PerformerDepth);
        }

        private static float LastDepth(StarInkMark mark) => mark.motif != 0 ? mark.origin.z + 1.1f :
            mark.points[mark.points.Count - 1].z;

        private void Add(StarInkMark mark)
        {
            if (marks.Count == MaxMarks)
            {
                if (marks[0] == pen) pen = null;
                marks.RemoveAt(0);
            }
            marks.Add(mark);
            RefreshMarks();
        }

        private void RefreshMarks() => frame.marks = marks.ToArray();

        private void Clear(int next)
        {
            paper = next;
            marks.Clear();
            frame = new StarInkFrame();
            pen = null;
            prevTime = 0;
        }

        public void Restore(Journey journey, JourneyCheckpoint checkpoint)
        {
            Clear(journey.StarPaper);
            frame.depth = journey.Distance;
            frame.time = journey.Time;
            if (checkpoint.version >= 11)
            {
                string saved = checkpoint.starArtwork;
                if (string.IsNullOrEmpty(saved) || saved.Length > 700000) return;
                StarInkFrame old;
                try { old = JsonUtility.FromJson<StarInkFrame>(saved); }
                catch (ArgumentException) { return; }
                if (old?.marks == null || old.marks.Length > MaxMarks) return;
                foreach (StarInkMark mark in old.marks)
                {
                    if (mark == null || !SafePoint(mark.origin) || !(mark.born >= -3 && mark.born <= frame.time + .04f)) continue;
                    if (StarMotifs.Valid(mark.motif) && Mathf.Abs(mark.portraitTip.x) < 1 && Mathf.Abs(mark.portraitTip.y) < 1 ||
                        mark.motif == 0 && mark.points != null && mark.points.Count > 0 && mark.points.Count <= MaxSamples &&
                        mark.points.TrueForAll(SafePoint))
                        marks.Add(mark);
                }
                RefreshMarks();
                return;
            }
            for (int i = 0; i < 3 && ((checkpoint.starPaths >> (i * 2)) & 3) != 0; i++)
            {
                int motif = (checkpoint.starMotifs >> (i * 4)) & 15;
                Add(new StarInkMark { motif = StarMotifs.Valid(motif) ? motif : 1 + i * 2, born = -3,
                    origin = new Vector3(-2.6f + i * 2.0f, .38f - .49f * i, 8 + .68f * i),
                    portraitTip = new Vector2(-.29f + i * .20f, -.20f + i * .16f) });
            }
        }

        private static bool SafePoint(Vector3 p) => Mathf.Abs(p.x) < 30 && Mathf.Abs(p.y) < 30 && p.z >= 0 && p.z <= 1000020;

        private bool Prepare()
        {
            if (material != null) return true;
            Shader shader = Resources.Load<Shader>("StarSea/StarTrailInk");
            if (shader == null || !shader.isSupported) return false;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            worldPass = material.FindPass("WorldInk");
            portraitPass = material.FindPass("PortraitInk");
            alphaPass = material.FindPass("StraightAlpha");
            worldInk = new StarInkMesh("Star Sea world-space ribbons");
            portraitInk = new StarInkMesh("Star Sea portrait ribbons");
            portraitCommands = new CommandBuffer { name = "Star Sea portrait paint" };
            return true;
        }

        public void DrawWorld(Camera camera, CommandBuffer commands)
        {
            if (!Prepare()) return;
            worldInk.Build(frame, null);
            Transform eye = camera.transform;
            Vector3 origin = eye.position - eye.forward * frame.depth;
            commands.DrawMesh(worldInk.Mesh, Matrix4x4.TRS(origin, eye.rotation, Vector3.one), material, 0, worldPass);
        }

        public Texture DrawPortrait(StarInkFrame source, Rect area, Vector2 actor, float size, bool mirror, bool sideways, float extent)
        {
            if (source?.marks == null || area.width < 2 || area.height < 2 || !Prepare()) return null;
            var portrait = new PortraitProjection(area, actor, size, mirror, sideways, extent);
            portraitInk.Build(source, portrait);
            int width = Mathf.Clamp(Mathf.CeilToInt(area.width), 32, 1280);
            int height = Mathf.Clamp(Mathf.CeilToInt(area.height), 32, 1280);
            if (inkLayer == null || inkLayer.width != width || inkLayer.height != height)
            {
                ReleaseTexture(inkLayer);
                ReleaseTexture(portraitLayer);
                inkLayer = CanvasTexture(width, height, "Star Sea portrait ink");
                portraitLayer = CanvasTexture(width, height, "Star Sea portrait overlay");
            }
            var before = RenderTexture.active;
            if (!inkLayer.IsCreated()) inkLayer.Create();
            material.SetVector("_InkViewport", new Vector4(2 / area.width, -2 / area.height, -1, 1));
            portraitCommands.Clear();
            portraitCommands.SetRenderTarget(inkLayer);
            portraitCommands.ClearRenderTarget(false, true, Color.clear);
            portraitCommands.DrawMesh(portraitInk.Mesh, Matrix4x4.identity, material, 0, portraitPass);
            Graphics.ExecuteCommandBuffer(portraitCommands);
            Graphics.Blit(inkLayer, portraitLayer, material, alphaPass);
            RenderTexture.active = before;
            return portraitLayer;
        }

        private static RenderTexture CanvasTexture(int width, int height, string name) =>
            new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) {
                name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };

        private static void ReleaseTexture(RenderTexture target)
        {
            if (target == null) return;
            target.Release();
            UnityEngine.Object.Destroy(target);
        }

        public void Dispose()
        {
            worldInk?.Dispose();
            portraitInk?.Dispose();
            portraitCommands?.Dispose();
            ReleaseTexture(inkLayer);
            ReleaseTexture(portraitLayer);
            if (material != null) UnityEngine.Object.Destroy(material);
        }

        internal sealed class PortraitProjection
        {
            private readonly Rect area;
            private readonly Vector2 actor;
            private readonly float size, extent;
            private readonly bool mirror, sideways;

            public PortraitProjection(Rect area, Vector2 actor, float size, bool mirror, bool sideways, float extent)
            {
                this.area = area; this.actor = actor; this.size = size; this.mirror = mirror;
                this.sideways = sideways; this.extent = extent;
            }

            private Vector2 Rotate(Vector2 value)
            {
                if (mirror) value.x = -value.x;
                return sideways ? new Vector2(value.y, -value.x) : value;
            }

            public Vector2 Anchor(Vector2 tip) => actor + Rotate(tip) * size;

            public float Fit(StarInkMark mark, StarTrailArt art)
            {
                Vector2 anchor = Anchor(mark.portraitTip);
                float scale = Mathf.Min(area.width, area.height) * Mathf.Clamp(.60f + extent * .32f, .64f, 1.10f);
                for (int i = 0; i <= 32; i++)
                {
                    Vector3 p = art.Offset(i / 32f);
                    Vector2 d = Rotate(new Vector2(p.x, -p.y));
                    if (Mathf.Abs(d.x) > .0001f) scale = Mathf.Min(scale, SideRoom(anchor.x, area.width, d.x));
                    if (Mathf.Abs(d.y) > .0001f) scale = Mathf.Min(scale, SideRoom(anchor.y, area.height, d.y));
                }
                return Mathf.Max(7, scale);
            }

            private static float SideRoom(float anchor, float limit, float delta) =>
                (delta > 0 ? limit - anchor - 12 : anchor - 12) / Mathf.Abs(delta);

            public Vector3 Motif(Vector2 anchor, Vector3 offset, float scale)
            {
                Vector2 at = anchor + Rotate(new Vector2(offset.x, -offset.y)) * (scale / (1 + offset.z * .40f));
                return new Vector3(at.x, at.y, 0);
            }

            public Vector3 Gesture(Vector3 point, float cameraDepth)
            {
                float depth = Mathf.Max(2.5f, point.z - cameraDepth);
                float unit = Mathf.Min(area.width, area.height) * .67f;
                Vector2 at = actor + Rotate(new Vector2(point.x, -point.y)) * (unit / depth);
                return new Vector3(at.x, at.y, 0);
            }

            public float Halo => Mathf.Clamp(Mathf.Min(area.width, area.height) * .014f + extent * 2, 4.5f, 16);
        }
    }

    internal sealed class StarInkMesh : IDisposable
    {
        private static readonly Color InkCool = new Color(.37f, .80f, 1);
        private static readonly Color InkWarm = new Color(1, .73f, .48f);
        private readonly List<Vector3> vertices = new List<Vector3>(16000);
        private readonly List<Color32> colors = new List<Color32>(16000);
        private readonly List<Vector2> uv = new List<Vector2>(16000);
        private readonly List<int> triangles = new List<int>(28000);
        private readonly List<Vector3> samples = new List<Vector3>(1000);
        public readonly Mesh Mesh;

        public StarInkMesh(string name)
        {
            Mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            Mesh.MarkDynamic();
        }

        public void Build(StarInkFrame frame, StarInkSpace.PortraitProjection portrait)
        {
            vertices.Clear(); colors.Clear(); uv.Clear(); triangles.Clear();
            bool screen = portrait != null;
            Vector3 eye = Vector3.forward * frame.depth;
            float halo = screen ? portrait.Halo : .078f;
            if (frame.marks != null)
            {
                int newest = Array.FindLastIndex(frame.marks, item => item.motif != 0);
                for (int i = 0; i < frame.marks.Length; i++)
                {
                    StarInkMark mark = frame.marks[i];
                    var art = StarTrailArt.Find(mark.motif);
                    if (art != null) AddPattern(mark, art, frame, portrait, eye, halo, i == newest);
                    else if (mark.points?.Count > 1) AddGesture(mark, frame.depth, portrait, eye, halo);
                }
            }
            if (frame.pointing)
            {
                float shimmer = .72f + .11f * Mathf.Sin(frame.time * 6f);
                Vector3 at = screen ? portrait.Gesture(frame.cursor, frame.depth) : frame.cursor;
                AddSpark(at, halo * 1.45f, new Color(.61f, .94f, 1, shimmer));
            }
            Mesh.Clear(false);
            Mesh.SetVertices(vertices);
            Mesh.SetUVs(0, uv);
            Mesh.SetColors(colors);
            Mesh.SetTriangles(triangles, 0, false);
            Mesh.bounds = new Bounds(eye, Vector3.one * 3000);
        }

        private void AddPattern(StarInkMark mark, StarTrailArt art, StarInkFrame frame, StarInkSpace.PortraitProjection portrait,
            Vector3 eye, float halo, bool latest)
        {
            bool screen = portrait != null;
            float fade = NearFade(mark.origin.z - frame.depth);
            if (fade <= .001f) return;
            float age = Mathf.Max(0, frame.time - mark.born);
            float progress = Mathf.Clamp01(age / art.Duration);
            progress = .62f * progress + .38f * (progress * progress * (3 - 2 * progress));
            Vector2 anchor = screen ? portrait.Anchor(mark.portraitTip) : default;
            float scale = screen ? portrait.Fit(mark, art) : 4.7f;
            samples.Clear();
            int count = Mathf.CeilToInt(art.Segments * progress);
            for (int i = 0; i <= count; i++)
            {
                float t = Mathf.Min(progress, i / (float)art.Segments);
                Vector3 offset = art.Offset(t);
                samples.Add(screen ? portrait.Motif(anchor, offset, scale) : mark.origin + offset * scale);
            }
            AddRibbon(screen, eye, halo, .86f * fade, art, progress);
            if (age < art.Duration + .32f)
            {
                float glow = .30f + .59f * (1 - Mathf.SmoothStep(0, 1, (age - art.Duration) / .32f));
                float atTip = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .11f));
                AddSpark(samples[samples.Count - 1], halo * (1.12f + atTip * .42f),
                    WithAlpha(art.Pigment(progress), glow * fade));
            }
            if (latest && age < art.Duration)
            {
                Vector3 hand = screen ? (Vector3)portrait.Anchor(frame.portraitHand) : frame.hand;
                AddLaunch(hand, samples[0], art, age, screen, eye, halo, fade);
            }
        }

        private void AddLaunch(Vector3 hand, Vector3 origin, StarTrailArt art, float age, bool screen, Vector3 eye,
            float halo, float fade)
        {
            float gap = Vector3.Distance(hand, origin);
            float reach = screen ? halo * 7 : .55f;
            float release = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(art.Duration * .60f, art.Duration, age));
            float visible = release * fade * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(reach * .44f, reach, gap)));
            if (visible < .02f || gap < halo * .22f) return;
            Vector3 control = Vector3.Lerp(hand, origin, .46f) + Vector3.up * (gap * (screen ? -.13f : .13f));
            samples.Clear();
            for (int i = 0; i <= 6; i++)
            {
                float t = i / 6f;
                samples.Add(Vector3.Lerp(Vector3.Lerp(hand, control, t), Vector3.Lerp(control, origin, t), t));
            }
            AddRibbon(screen, eye, halo * .70f, visible * .69f, art, 0);
        }

        private void AddGesture(StarInkMark mark, float depth, StarInkSpace.PortraitProjection portrait, Vector3 eye, float halo)
        {
            bool screen = portrait != null;
            List<Vector3> points = mark.points;
            int first = 0;
            while (first < points.Count && points[first].z < depth + .45f) first++;
            if (first + 1 >= points.Count) return;
            samples.Clear();
            samples.Add(points[first]);
            for (int i = first + 1; i < points.Count - 1; i++)
            {
                Vector3 control = points[i];
                Vector3 a = (points[i - 1] + control) * .5f;
                Vector3 b = (control + points[i + 1]) * .5f;
                for (int j = 0; j <= 2; j++)
                {
                    float t = j / 3f;
                    samples.Add(Vector3.Lerp(Vector3.Lerp(a, control, t), Vector3.Lerp(control, b, t), t));
                }
                if (i == points.Count - 2) samples.Add(b);
            }
            samples.Add(points[points.Count - 1]);
            if (screen)
                for (int i = 0; i < samples.Count; i++) samples[i] = portrait.Gesture(samples[i], depth);
            AddRibbon(screen, eye, halo, .84f, null, 1);
        }

        private void AddRibbon(bool screen, Vector3 eye, float radius, float alpha, StarTrailArt art, float progress)
        {
            int start = vertices.Count;
            int count = samples.Count;
            if (count < 2) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = samples[i];
                Vector3 tangent = (samples[Mathf.Min(i + 1, count - 1)] - samples[Mathf.Max(0, i - 1)]).normalized;
                Vector3 along = screen ? Vector3.forward : p - eye;
                Vector3 side = Vector3.Cross(along, tangent).normalized;
                if (side.sqrMagnitude < .001f) side = Vector3.up;
                float t = i / (float)(count - 1);
                Color tint = art == null ? Color.Lerp(InkCool, InkWarm, t * .60f) : art.Pigment(t * progress);
                float gain = screen ? 1 : NearFade(p.z - eye.z);
                AddVertex(p - side * radius, WithAlpha(tint, alpha * gain), new Vector2(0, .5f));
                AddVertex(p + side * radius, WithAlpha(tint, alpha * gain), new Vector2(1, .5f));
                if (i > 0) AddQuad(start + 2 * (i - 1), start + 2 * i);
            }
        }

        private void AddSpark(Vector3 p, float radius, Color tint)
        {
            int i = vertices.Count;
            AddVertex(p + new Vector3(-radius, -radius, 0), tint, new Vector2(0, 0));
            AddVertex(p + new Vector3(radius, -radius, 0), tint, new Vector2(1, 0));
            AddVertex(p + new Vector3(-radius, radius, 0), tint, new Vector2(0, 1));
            AddVertex(p + new Vector3(radius, radius, 0), tint, new Vector2(1, 1));
            AddQuad(i, i + 2);
        }

        private void AddVertex(Vector3 value, Color tint, Vector2 texture)
        {
            vertices.Add(value);
            colors.Add(tint);
            uv.Add(texture);
        }

        private void AddQuad(int a, int b)
        {
            triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
            triangles.Add(b); triangles.Add(a + 1); triangles.Add(b + 1);
        }

        private static float NearFade(float depth) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, 2.4f, depth));
        private static Color WithAlpha(Color value, float alpha) => new Color(value.r, value.g, value.b, alpha);
        public void Dispose() => UnityEngine.Object.Destroy(Mesh);
    }
}
