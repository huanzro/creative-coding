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
        public bool continuous;
        public int branchSide;
        public List<Vector3> points;
    }

    [Serializable]
    internal sealed class StarInkFrame
    {
        public float time, depth;
        public int canvasId, historyVersion, tick;
        public StarInkMark[] marks;
        public StarTrailGrowth[] growth;
        public bool pointing;
        public int penMark = -1;
        public Vector3 cursor, hand;
        public Vector2 portraitHand, eyeOffset;
        public Vector3 Eye => new Vector3(eyeOffset.x, eyeOffset.y, depth);
    }

    internal sealed class StarInkSpace : IDisposable
    {
        public const float PerformerDepth = 5.8f, FieldOfView = 49f;
        public const float Aspect = 16f / 9;
        private const int MaxMarks = 24, MaxSamples = 480;
        private static readonly float HalfHeight = PerformerDepth * Mathf.Tan(FieldOfView * .5f * Mathf.Deg2Rad);
        private static readonly float BrushWidth = 2 * HalfHeight * Aspect * .82f, BrushHeight = 2 * HalfHeight * .71f;
        private static int nextCanvasId;
        private readonly List<StarInkMark> marks = new List<StarInkMark>(MaxMarks);
        private readonly List<StarTrailGrowth> growth = new List<StarTrailGrowth>(32);
        private readonly System.Random growthRandom = new System.Random();
        private StarInkFrame frame = new StarInkFrame();
        private StarInkMark pen;
        private Vector2 penScreen;
        private bool reconnecting;
        private float prevTime, penTime;
        private float distanceToGrowth, lastGrowth = float.NegativeInfinity, flyCheckAt;
        private int fireflies;
        private int paper = -1;
        private StarInkMesh worldInk, portraitInk;
        private StarRibbonRenderer ribbons;
        private CommandBuffer portraitCommands;
        private Material material;
        private int worldPass, portraitPass, alphaPass;
        private RenderTexture inkLayer, portraitLayer;
        private StarInkFrame paintedFrame;
        private int paintedTick;
        private float paintedTime, paintedDepth;
        private Rect paintedArea;
        private Vector4 paintedPlacement;
        private bool paintedMirror, paintedSideways;

        public int Revision { get; private set; }
        public StarInkFrame Frame => frame;

        public void Update(Journey journey, Camera camera, Vector3 atFinger, Vector2 portraitTip)
        {
            if (paper != journey.StarPaper) Clear(journey.StarPaper);
            frame.tick++;
            frame.depth = journey.Distance;
            frame.time = journey.Time;
            frame.eyeOffset = TravelOffset(frame.depth);
            Vector3 origin = camera.transform.InverseTransformPoint(atFinger);
            frame.hand = origin + frame.Eye;
            frame.portraitHand = portraitTip;
            StarTip input = journey.StarPointer;
            float dt = Mathf.Clamp(frame.time - prevTime, 0, .12f);
            bool changed = pen == null;
            if (pen == null)
            {
                penScreen = input.Valid ? new Vector2(input.X, input.Y) : ScreenInSky(origin, origin.z);
                frame.cursor = PointInSky(penScreen, frame.Eye);
                pen = new StarInkMark { continuous = true, born = frame.time, origin = frame.cursor,
                    points = new List<Vector3>(128) { frame.cursor } };
                distanceToGrowth = .56f + (float)growthRandom.NextDouble() * .49f;
                penTime = frame.time;
                Add(pen);
            }
            else if (input.Valid && dt > 0)
            {
                Vector2 target = new Vector2(input.X, input.Y);
                if (!frame.pointing && (target - penScreen).sqrMagnitude > .035f * .035f) reconnecting = true;
                penScreen = reconnecting ? Vector2.Lerp(penScreen, target, 1 - Mathf.Exp(-37f * dt)) : target;
                if ((target - penScreen).sqrMagnitude < .010f * .010f) reconnecting = false;
            }
            if (!input.Valid) reconnecting = false;
            frame.pointing = input.Valid;
            frame.cursor = PointInSky(penScreen, frame.Eye);
            float moved = (pen.points[pen.points.Count - 1] - frame.cursor).sqrMagnitude;
            if (moved >= .029f * .029f || moved >= .009f * .009f && frame.time - penTime >= .075f)
                changed |= RecordCursor();

            while (journey.TryTakeStarStamp(out int motif))
            {
                RecordCursor();
                int side = penScreen.x > .76f ? -1 : penScreen.x < .24f ? 1 : motif <= StarMotifs.PerGroup ? -1 : 1;
                Add(new StarInkMark { motif = motif, branchSide = side, born = frame.time,
                    origin = pen.points[pen.points.Count - 1] });
                changed = true;
            }
            prevTime = frame.time;

            // Retain the connected portion inside the viewing volume and its leading control point.
            int passed = 0;
            while (passed + 2 < pen.points.Count && pen.points[passed + 1].z < frame.depth + .35f) passed++;
            if (passed > 0) pen.points.RemoveRange(0, passed);
            bool dropped = false;
            for (int i = marks.Count - 1; i >= 0; i--)
            {
                var mark = marks[i];
                if (mark == pen || LastDepth(mark) >= frame.depth + .15f) continue;
                RemoveAt(i);
                dropped = true;
            }
            if (dropped) RefreshMarks();
            frame.penMark = pen == null ? -1 : marks.IndexOf(pen);
            AgeFireflies();
            if (changed || dropped) Revision++;
        }

        private bool RecordCursor()
        {
            var points = pen.points;
            Vector3 last = points[points.Count - 1];
            if ((frame.cursor - last).sqrMagnitude < .0005f * .0005f) return false;
            AppendPoint(points, frame.cursor);
            GrowOnStroke(last, frame.cursor);
            penTime = frame.time;
            return true;
        }

        private void GrowOnStroke(Vector3 last, Vector3 next)
        {
            distanceToGrowth -= Vector3.Distance(last, next);
            if (distanceToGrowth > 0 || frame.time - lastGrowth < .19f) return;
            distanceToGrowth = .86f + (float)growthRandom.NextDouble() * .69f;
            lastGrowth = frame.time;
            Vector3 origin = next;
            bool flower = growthRandom.NextDouble() < .48;
            if (flower)
                for (int i = growth.Count - 1; i >= 0; i--)
                    if (growth[i].flower && (growth[i].origin - origin).sqrMagnitude < .31f * .31f)
                    {
                        flower = false;
                        break;
                    }
            if (!flower) fireflies++;
            growth.Add(new StarTrailGrowth { origin = origin, born = frame.time, seed = growthRandom.Next(), flower = flower });
            frame.growth = growth.ToArray();
        }

        private void AgeFireflies()
        {
            if (fireflies == 0 || frame.time < flyCheckAt) return;
            flyCheckAt = frame.time + .24f;
            int expired = growth.RemoveAll(item => !item.flower && frame.time - item.born >= item.Lifetime);
            if (expired == 0) return;
            fireflies -= expired;
            frame.growth = growth.ToArray();
        }

        private void AppendPoint(List<Vector3> points, Vector3 point)
        {
            if (points.Count == MaxSamples)
            {
                // Keep branch junctions and recent detail if a long gesture fills the visible sample budget.
                int early = MaxSamples / 2;
                int kept = 0;
                for (int i = 0; i < early; i++)
                    if ((i & 1) == 0 || HasBranchAt(points[i])) points[kept++] = points[i];
                points.RemoveRange(kept, early - kept);
            }
            points.Add(point);
        }

        private bool HasBranchAt(Vector3 point)
        {
            foreach (var mark in marks)
                if (mark.branchSide != 0 && mark.origin == point) return true;
            return false;
        }

        // A shallow serpentine flight reveals depth even when the tracked finger is stationary.
        public static Vector2 TravelOffset(float depth) => new Vector2(.75f * Mathf.Sin(depth * .32f) + .18f * Mathf.Sin(depth * .81f),
            .28f * (Mathf.Sin(.83f + depth * .34f) - Mathf.Sin(.83f)));

        private static Vector3 PointInSky(Vector2 screen, Vector3 eye) => eye + new Vector3((screen.x - .5f) * BrushWidth,
            (.5f - screen.y) * BrushHeight, PerformerDepth);

        private static Vector2 ScreenInSky(Vector3 position, float eyeDepth)
        {
            float scale = PerformerDepth / Mathf.Max(.4f, eyeDepth);
            return new Vector2(Mathf.Clamp01(.5f + position.x * scale / BrushWidth),
                Mathf.Clamp01(.5f - position.y * scale / BrushHeight));
        }

        private static float LastDepth(StarInkMark mark) => mark.motif != 0 ? mark.origin.z + (mark.branchSide == 0 ? 1.1f : 1.9f) :
            mark.points[mark.points.Count - 1].z;

        private void Add(StarInkMark mark)
        {
            if (marks.Count == MaxMarks)
                RemoveAt(marks[0] == pen ? 1 : 0);
            marks.Add(mark);
            if (mark.branchSide != 0) frame.historyVersion++;
            RefreshMarks();
        }

        private void RemoveAt(int index)
        {
            StarInkMark mark = marks[index];
            if (mark.branchSide != 0 || mark.motif == 0 && mark.points.Count > 1) frame.historyVersion++;
            marks.RemoveAt(index);
        }

        private void RefreshMarks() => frame.marks = marks.ToArray();

        private void Clear(int next)
        {
            paper = next;
            marks.Clear();
            growth.Clear();
            frame = new StarInkFrame { canvasId = ++nextCanvasId };
            pen = null;
            reconnecting = false;
            prevTime = penTime = 0;
            lastGrowth = float.NegativeInfinity;
            fireflies = 0;
            flyCheckAt = 0;
        }

        public void Restore(Journey journey, JourneyCheckpoint checkpoint)
        {
            Clear(journey.StarPaper);
            frame.depth = journey.Distance;
            frame.time = journey.Time;
            frame.eyeOffset = TravelOffset(frame.depth);
            if (checkpoint.version >= 11)
            {
                string saved = checkpoint.starArtwork;
                if (string.IsNullOrEmpty(saved) || saved.Length > 2000000) return;
                StarInkFrame old;
                try { old = JsonUtility.FromJson<StarInkFrame>(saved); }
                catch (ArgumentException) { return; }
                if (old == null) return;
                if (old.marks != null && old.marks.Length <= MaxMarks)
                {
                    foreach (StarInkMark mark in old.marks)
                    {
                        if (mark == null || !SafePoint(mark.origin) || !(mark.born >= -3 && mark.born <= frame.time + .04f)) continue;
                        if (StarMotifs.Valid(mark.motif) && mark.branchSide >= -1 && mark.branchSide <= 1 &&
                            Mathf.Abs(mark.portraitTip.x) < 1 && Mathf.Abs(mark.portraitTip.y) < 1 ||
                            mark.motif == 0 && mark.points != null && mark.points.Count > 0 && mark.points.Count <= MaxSamples &&
                            mark.points.TrueForAll(SafePoint))
                        {
                            marks.Add(mark);
                            if (mark.motif == 0 && mark.continuous) pen = mark;
                        }
                    }
                }
                if (pen != null)
                {
                    frame.penMark = marks.IndexOf(pen);
                    frame.cursor = pen.points[pen.points.Count - 1];
                    penScreen = ScreenInSky(frame.cursor - (Vector3)TravelOffset(frame.cursor.z - PerformerDepth), PerformerDepth);
                    prevTime = penTime = frame.time;
                    distanceToGrowth = .96f;
                }
                if (old.growth != null)
                    foreach (StarTrailGrowth item in old.growth)
                        if (item.flower && item.born >= 0 && item.born <= frame.time && SafePoint(item.origin))
                            growth.Add(item);
                frame.growth = growth.ToArray();
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
            ribbons = new StarRibbonRenderer(material);
            portraitCommands = new CommandBuffer { name = "Star Sea portrait paint" };
            return true;
        }

        public void DrawWorld(Camera camera, CommandBuffer commands)
        {
            if (!Prepare()) return;
            worldInk.Build(frame, null);
            Transform eye = camera.transform;
            Vector3 origin = eye.position - eye.rotation * frame.Eye;
            var basis = Matrix4x4.TRS(origin, eye.rotation, Vector3.one);
            ribbons.Draw(frame, commands, basis, null);
            if (worldInk.Mesh.vertexCount > 0) commands.DrawMesh(worldInk.Mesh, basis, material, 0, worldPass);
        }

        public Texture DrawPortrait(StarInkFrame source, Rect area, Vector2 actor, float size, bool mirror, bool sideways, float extent)
        {
            if (source == null || (source.marks == null && source.growth == null) ||
                area.width < 2 || area.height < 2 || !Prepare()) return null;
            var placement = new Vector4(actor.x, actor.y, size, extent);
            if (source == paintedFrame && source.tick == paintedTick && source.time == paintedTime && source.depth == paintedDepth &&
                area == paintedArea && placement == paintedPlacement && mirror == paintedMirror && sideways == paintedSideways &&
                portraitLayer != null && portraitLayer.IsCreated())
                return portraitLayer;
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
            ribbons.Draw(source, portraitCommands, Matrix4x4.identity, portrait);
            if (portraitInk.Mesh.vertexCount > 0)
                portraitCommands.DrawMesh(portraitInk.Mesh, Matrix4x4.identity, material, 0, portraitPass);
            Graphics.ExecuteCommandBuffer(portraitCommands);
            Graphics.Blit(inkLayer, portraitLayer, material, alphaPass);
            RenderTexture.active = before;
            paintedFrame = source;
            paintedTime = source.time;
            paintedDepth = source.depth;
            paintedTick = source.tick;
            paintedArea = area;
            paintedPlacement = placement;
            paintedMirror = mirror;
            paintedSideways = sideways;
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
            ribbons?.Dispose();
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

            public Vector3 Gesture(Vector3 point, Vector3 eye)
            {
                Vector3 relative = point - eye;
                Vector2 at = actor + Rotate(new Vector2(relative.x, -relative.y)) * GrowthScale(relative.z);
                return new Vector3(at.x, at.y, 0);
            }

            public Vector3 GrowthDirection(Vector2 axis) => Rotate(new Vector2(axis.x, -axis.y));
            public float Halo => Mathf.Clamp(Mathf.Min(area.width, area.height) * .014f + extent * 2, 4.5f, 16);
            public float DistanceScale => Mathf.Min(area.width, area.height) * .67f;
            public float GrowthScale(float depth) => DistanceScale / Mathf.Max(2.5f, depth);
            public Vector4 RibbonCanvas => new Vector4(actor.x, actor.y, DistanceScale, Halo);
            public Vector4 RibbonAxes
            {
                get
                {
                    float mirrorX = mirror ? -1 : 1;
                    return sideways ? new Vector4(0, -1, -mirrorX, 0) : new Vector4(mirrorX, 0, 0, -1);
                }
            }
        }
    }

    internal sealed class StarInkMesh : IDisposable
    {
        private static readonly Color Gold = new Color(1, .70f, .23f);
        private static readonly Color Honey = new Color(1, .53f, .13f);
        private static readonly Color Cream = new Color(1, .91f, .55f);
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
            Vector3 eye = frame.Eye;
            float halo = screen ? portrait.Halo : .078f;
            if (frame.marks != null)
            {
                int newest = Array.FindLastIndex(frame.marks, item => item.motif != 0 && item.branchSide == 0);
                for (int i = 0; i < frame.marks.Length; i++)
                {
                    StarInkMark mark = frame.marks[i];
                    if (mark.branchSide != 0) continue;
                    var art = StarTrailArt.Find(mark.motif);
                    if (art != null) AddPattern(mark, art, frame, portrait, eye, halo, i == newest);
                }
            }
            if (frame.growth != null)
                foreach (StarTrailGrowth item in frame.growth)
                    AddGrowth(item, frame, portrait);
            if (frame.penMark >= 0)
            {
                float shimmer = (frame.pointing ? .83f : .48f) + .11f * Mathf.Sin(frame.time * 6f);
                Vector3 at = screen ? portrait.Gesture(frame.cursor, eye) : frame.cursor;
                AddSpark(at, halo * 1.45f, new Color(.61f, .94f, 1, shimmer));
            }
            if (Mesh.vertexCount == 0 && vertices.Count == 0) return;
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

        // Both forms share a warm palette and the same 3D attachment point.
        private void AddGrowth(StarTrailGrowth item, StarInkFrame frame, StarInkSpace.PortraitProjection portrait)
        {
            float age = frame.time - item.born;
            float life = item.Lifetime;
            if (age < 0 || (!item.flower && age >= life)) return;
            Vector3 position = item.origin;
            float phase = item.Variation * 6.28318f;
            float flyT = Mathf.Clamp01(age / life);
            if (!item.flower)
            {
                float drift = Mathf.SmoothStep(0, 1, flyT);
                position += new Vector3(Mathf.Cos(phase) * .50f * drift + .09f * Mathf.Sin(flyT * 6) * flyT,
                    (.40f + .36f * item.Variation) * drift + .065f * Mathf.Sin(flyT * 8) * flyT,
                    (.16f + .23f * item.Variation) * drift);
            }
            float depth = position.z - frame.depth;
            if (depth < .45f || depth > 50f || Mathf.Abs(position.x - frame.eyeOffset.x) > depth * .83f + .45f ||
                Mathf.Abs(position.y - frame.eyeOffset.y) > depth * .49f + .45f) return;
            float fade = NearFade(depth);
            float scale = Mathf.Lerp(.82f, 1.19f, item.Variation);
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.right;
            if (portrait != null)
            {
                position = portrait.Gesture(position, frame.Eye);
                scale *= portrait.GrowthScale(depth);
                up = portrait.GrowthDirection(Vector2.up);
                right = portrait.GrowthDirection(Vector2.right);
            }
            if (item.flower) AddBloom(position, right, up, age, scale, fade, phase);
            else AddFirefly(position, right, up, age, flyT, scale, fade, phase);
        }

        private void AddBloom(Vector3 root, Vector3 right, Vector3 up, float age, float scale, float fade, float phase)
        {
            float sprout = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .83f));
            float petals = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.26f, 1.13f, age));
            float bend = Mathf.Cos(phase) * .031f;
            Vector3 stem = (up * .173f + right * bend) * (scale * sprout);
            Vector3 head = root + stem;
            float strength = fade * sprout;
            AddEllipse(root + stem * .48f, right * (scale * .025f), stem * .54f, WithAlpha(Honey, strength * .58f), true);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 leaf = right * side * .88f + up * .48f;
                Vector3 cross = right * -.48f + up * side * .88f;
                AddEllipse(root + stem * (side < 0 ? .44f : .63f) + leaf * (scale * .036f * sprout),
                    cross * (scale * .032f * sprout), leaf * (scale * .066f * sprout),
                    WithAlpha(Gold, strength * .48f), true);
            }
            if (petals <= .001f) return;
            for (int i = 0; i < 5; i++)
            {
                float angle = phase + i * 1.25664f;
                float x = Mathf.Cos(angle), y = Mathf.Sin(angle);
                Vector3 petal = right * x + up * y;
                Vector3 cross = right * -y + up * x;
                AddEllipse(head + petal * (scale * .074f * petals), cross * (scale * .063f * petals),
                    petal * (scale * .102f * petals), WithAlpha(Color.Lerp(Honey, Gold, .45f + .09f * i), fade * petals * .86f), true);
            }
            AddEllipse(head, right * (scale * .071f), up * (scale * .071f), WithAlpha(Cream, fade * petals), false);
        }

        private void AddFirefly(Vector3 position, Vector3 right, Vector3 up, float age, float flight, float scale,
            float fade, float phase)
        {
            float alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .26f)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.56f, 1, flight))) * fade;
            float beat = .74f + .26f * Mathf.Sin(age * 17f + phase);
            AddEllipse(position, right * (scale * .116f), up * (scale * .111f), WithAlpha(Gold, alpha * .69f), false);
            AddEllipse(position, right * (scale * .043f), up * (scale * .048f), WithAlpha(Cream, alpha), false);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 wing = right * side * (.90f * beat) + up * .51f;
                Vector3 cross = right * -.51f + up * (.90f * side);
                AddEllipse(position + wing * (scale * .048f), cross * (scale * .024f), wing * (scale * .058f),
                    WithAlpha(Cream, alpha * (.29f + beat * .31f)), true);
            }
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
                Color tint = art.Pigment(t * progress);
                float gain = screen ? 1 : NearFade(p.z - eye.z);
                AddVertex(p - side * radius, WithAlpha(tint, alpha * gain), new Vector2(0, .5f));
                AddVertex(p + side * radius, WithAlpha(tint, alpha * gain), new Vector2(1, .5f));
                if (i > 0) AddQuad(start + 2 * (i - 1), start + 2 * i);
            }
        }

        private void AddSpark(Vector3 p, float radius, Color tint)
        {
            AddEllipse(p, Vector3.right * radius, Vector3.up * radius, tint, false);
        }

        private void AddEllipse(Vector3 p, Vector3 across, Vector3 axis, Color tint, bool petal)
        {
            int i = vertices.Count;
            float u = petal ? 2 : 0;
            AddVertex(p - across - axis, tint, new Vector2(u, 0));
            AddVertex(p + across - axis, tint, new Vector2(u + 1, 0));
            AddVertex(p - across + axis, tint, new Vector2(u, 1));
            AddVertex(p + across + axis, tint, new Vector2(u + 1, 1));
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
