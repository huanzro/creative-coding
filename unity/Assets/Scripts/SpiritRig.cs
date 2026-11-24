/* ARTISTIC RATIONALE
 * The sleeve, toes and fabric preserve the small movements of the original
 * projection portrait. A shared deformed image lets the gesture remain the
 * same in both windows without drawing the spirit onto the backdrop.
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    internal sealed class SpiritRig : IDisposable
    {
        private const float ArtWidth = 1024, ArtHeight = 1536, Margin = 150;
        private const float FrameWidth = ArtWidth + Margin * 2, FrameHeight = ArtHeight + Margin * 2;
        private const float ArtToScene = 105f / ArtHeight;
        private const float TurnDuration = 1.25f;
        private const int Columns = 38, Rows = 50;
        private const int AnchorCount = 4;

        public const float WidthScale = FrameWidth / ArtHeight;
        public const float HeightScale = FrameHeight / ArtHeight;
        public Texture Texture => rendered ? target : (Texture)portrait;
        public bool Articulated => rendered;

        private static readonly Vector2[] Shoulders = { new Vector2(427, 450), new Vector2(627, 446) };
        private static readonly Vector2[] Elbows = { new Vector2(335, 547), new Vector2(711, 554) };
        private static readonly Vector2[] Wrists = { new Vector2(248, 592), new Vector2(783, 605) };
        private static readonly Vector2[] Ankles = { new Vector2(460, 1376), new Vector2(531, 1371) };
        private static readonly Vector2[] Anchors = {
            new Vector2(188, 620), new Vector2(844, 637), new Vector2(463, 1486), new Vector2(529, 1448)
        };

        private readonly Texture2D portrait;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly RenderTexture target;
        private readonly CommandBuffer commands;
        private readonly BindPoint[] bindPoints;
        private readonly BindPoint[] anchorPoints = new BindPoint[AnchorCount];
        private readonly Vector3[] vertices;
        private readonly List<TracePoint>[] traces = new List<TracePoint>[AnchorCount];
        private readonly float[] dance = new float[SpiritDance.TrackCount];
        private readonly float[] cloth = new float[SpiritDance.TrackCount];
        private readonly Bone[] shoulder = new Bone[2], elbow = new Bone[2], wrist = new Bone[2], toe = new Bone[2];
        private readonly Vector2[] footOffset = new Vector2[2];
        private Bone head, chest, lean;
        private float facing, clothLean, clothFacing, lagX, lagY, flutterTime, turnAngle, turnTail;
        private Vector2 root;
        private float observedTime = float.NegativeInfinity, observedPhase, observedTurn, observedCast, observedEnergy;
        private float lastTrail = float.NegativeInfinity;
        private bool rendered, observedClosed;

        internal readonly struct TracePoint
        {
            public readonly Vector2 Position;
            public readonly float Time, Energy;
            public TracePoint(Vector2 position, float time, float energy)
            { Position = position; Time = time; Energy = energy; }
        }

        private readonly struct Bone
        {
            private readonly Vector2 center;
            private readonly float sine, cosine;
            public Bone(Vector2 center, float angle)
            { this.center = center; sine = Mathf.Sin(angle); cosine = Mathf.Cos(angle); }
            public Vector2 Turn(Vector2 position, float weight)
            {
                Vector2 p = position - center;
                return position + (new Vector2(p.x * cosine - p.y * sine, p.x * sine + p.y * cosine) - p) * weight;
            }
        }

        private struct BindPoint
        {
            public Vector2 Position;
            public int Arm;
            public float ArmWeight, Elbow, Wrist, Head, Torso, Chest, Skirt, Ribbon, Leg, Foot;
        }

        public SpiritRig(Texture2D portrait)
        {
            this.portrait = portrait;
            Shader shader = Resources.Load<Shader>("SpiritPortrait");
            if (shader == null || portrait == null || !shader.isSupported) return;
            material = new Material(shader) { mainTexture = portrait };
            target = new RenderTexture(768, Mathf.RoundToInt(768 * FrameHeight / FrameWidth), 0, RenderTextureFormat.ARGB32)
            { name = "Echoes articulated character", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            commands = new CommandBuffer { name = "Echoes shared portrait" };
            mesh = new Mesh { name = "Echoes portrait deformation" };
            mesh.MarkDynamic();
            bindPoints = new BindPoint[(Columns + 1) * (Rows + 1)];
            vertices = new Vector3[bindPoints.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[Columns * Rows * 6];
            int triangle = 0;
            for (int row = 0; row <= Rows; row++)
            for (int column = 0; column <= Columns; column++)
            {
                int a = row * (Columns + 1) + column;
                float u = (float)column / Columns;
                float v = (float)row / Rows;
                bindPoints[a] = Bind(u * ArtWidth, v * ArtHeight);
                uv[a] = new Vector2(u, 1 - v);
                if (row == Rows || column == Columns) continue;
                int b = a + Columns + 1;
                triangles[triangle++] = a;
                triangles[triangle++] = a + 1;
                triangles[triangle++] = b;
                triangles[triangle++] = b;
                triangles[triangle++] = a + 1;
                triangles[triangle++] = b + 1;
            }
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(4, 4, 1));
            for (int index = 0; index < AnchorCount; index++)
            {
                anchorPoints[index] = Bind(Anchors[index].x, Anchors[index].y);
                traces[index] = new List<TracePoint>(20);
            }
        }

        public IReadOnlyList<TracePoint> Trace(int index) => traces[index];

        public void Render(float time, float phase, float energy, float cast, float turnAge, bool closed)
        {
            if (mesh == null) return;
            if (rendered && target.IsCreated() && observedTime == time && observedPhase == phase &&
                observedTurn == turnAge && observedCast == cast && observedEnergy == energy && observedClosed == closed) return;
            observedTime = time;
            observedPhase = phase;
            observedTurn = turnAge;
            observedCast = cast;
            observedEnergy = energy;
            observedClosed = closed;
            PoseAt(time, phase, energy, cast, turnAge, closed);
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector2 point = Deform(bindPoints[index]);
                vertices[index] = new Vector3(point.x * 2 / FrameWidth, -point.y * 2 / FrameHeight);
            }
            mesh.vertices = vertices;
            if (!target.IsCreated() && !target.Create()) return;
            material.SetFloat("_BackShade", Mathf.Lerp(.59f, 1, Mathf.SmoothStep(0, 1, (1 + Mathf.Cos(turnAngle)) / 2)));
            commands.Clear();
            commands.SetRenderTarget(target);
            commands.SetViewport(new Rect(0, 0, target.width, target.height));
            commands.ClearRenderTarget(false, true, Color.clear);
            commands.DrawMesh(mesh, Matrix4x4.identity, material, 0, 0);
            Graphics.ExecuteCommandBuffer(commands);
            rendered = true;
            SampleTrails(time);
        }

        private void PoseAt(float time, float phase, float energy, float cast, float age, bool closed)
        {
            SpiritDance.Sample(phase, dance);
            SpiritDance.Sample(phase - .058f, cloth);
            float movement = .82f + .18f * energy;
            float spin = age < 0 ? 0 : Mathf.Clamp01(age / TurnDuration);
            float fabricSpin = age < 0 ? 0 : Mathf.Clamp01((age - .095f) / TurnDuration);
            turnAngle = Mathf.SmoothStep(0, 1, spin) * Mathf.PI * 2;
            turnTail = Mathf.Sin(Mathf.SmoothStep(0, 1, fabricSpin) * Mathf.PI * 2);
            float lift = Mathf.Sin(spin * Mathf.PI);
            Vector2 shift = new Vector2(dance[0], dance[1]) * movement;
            root = new Vector2(shift.x, shift.y - 10 * lift + Mathf.Sin(time * 2.15f) * .7f) / ArtToScene;
            float bodyLean = dance[2] * movement;
            facing = dance[3] * movement * (1 - .6f * lift);
            lean = new Bone(Vector2.zero, bodyLean * (1 - .4f * lift) + Mathf.Sin(turnAngle) * .13f);
            chest = new Bone(new Vector2(526, 600), .085f * (facing - cloth[3]) - .17f * bodyLean);
            head = new Bone(new Vector2(522, 363), dance[4] - .28f * dance[2] - Mathf.Sin(turnAngle) * .13f);
            clothLean = cloth[2];
            clothFacing = cloth[3];
            lagX = (cloth[0] - dance[0]) * 10;
            lagY = (cloth[1] - dance[1]) * 7;
            flutterTime = time * 3.4f;
            for (int index = 0; index < 2; index++)
            {
                int sign = index == 0 ? -1 : 1;
                int arm = 5 + index * 3;
                float gathered = closed ? .28f : 0;
                shoulder[index] = new Bone(Shoulders[index], -sign * Mathf.Min(.82f, dance[arm] * movement +
                    cast * (index == 0 ? .25f : .12f) + .24f * lift - gathered));
                elbow[index] = new Bone(Elbows[index], -sign * Mathf.Min(.88f, dance[arm + 1] + cast * .08f + gathered));
                wrist[index] = new Bone(Wrists[index], -sign * (dance[arm + 2] - cast * .17f));

                int foot = 11 + index * 4;
                float support = dance[foot + 3] * (1 - lift);
                float drift = root.x - bodyLean * (Ankles[index].y - ArtHeight / 2);
                toe[index] = new Bone(Ankles[index], dance[foot + 2] - bodyLean * support);
                footOffset[index] = new Vector2(dance[foot] + Mathf.Clamp(-drift, -44, 44) * support,
                    -dance[foot + 1] - root.y * support * .30f - lift * (index == 0 ? 22 : 46));
            }
        }

        private static BindPoint Bind(float x, float y)
        {
            int arm = x < 512 ? 0 : 1;
            int side = arm == 0 ? -1 : 1;
            Vector2 shoulder = Shoulders[arm], elbow = Elbows[arm], wrist = Wrists[arm];
            float span = (x - shoulder.x) * side;
            float lower = (elbow.x - shoulder.x) * side;
            float upper = (wrist.x - shoulder.x) * side;
            float center = shoulder.y + (elbow.y - shoulder.y) * Mathf.Max(0, span) / lower;
            if (span > upper) center = wrist.y + (span - upper) * .37f;
            else if (span > lower) center = Mathf.Lerp(elbow.y, wrist.y, (span - lower) / (upper - lower));
            float armBottom = arm == 0 ? 644 + .39f * (248 - x) : 653 + .37f * (x - 783) + 29 * Smooth(799, 835, x);
            float palm = Smooth(16, 70, (x - elbow.x) * side) * Smooth(510, 560, y) * (1 - Smooth(700, 730, y));
            float sleeve = Smooth(-25, 53, span) * (1 - Smooth(43, 86, Mathf.Abs(y - center))) * (1 - Smooth(675, 719, y));
            return new BindPoint {
                Position = new Vector2(x, y), Arm = arm,
                ArmWeight = Mathf.Max(sleeve, palm) * (1 - Smooth(armBottom, armBottom + 26, y)),
                Elbow = Smooth(-45, 51, (x - elbow.x) * side + (y - elbow.y) * .28f),
                Wrist = Smooth(-28, 13, (x - wrist.x) * side + (y - wrist.y) * .19f),
                Head = 1 - Smooth(305, 400, y),
                Torso = Smooth(325, 470, y) * (1 - Smooth(580, 970, y)),
                Chest = Smooth(250, 390, y) * (1 - Smooth(575, 905, y)),
                Skirt = Smooth(582, 1295, y) * (1 - Smooth(1315, 1470, y)),
                Ribbon = Smooth(645, 925, y) * (1 - Smooth(1080, 1190, y)) * Smooth(227, 465, Mathf.Abs(x - 512)),
                Leg = Smooth(1273, 1402, y) * (1 - Smooth(91, 128, Mathf.Abs(x - 490))),
                Foot = Smooth(478, 502, x)
            };
        }

        private Vector2 Deform(BindPoint v)
        {
            Vector2 original = v.Position;
            Vector2 moved = head.Turn(original, v.Head);
            if (v.ArmWeight > 0)
            {
                Vector2 hand = wrist[v.Arm].Turn(moved, v.Wrist);
                hand = elbow[v.Arm].Turn(hand, v.Elbow);
                moved = Vector2.Lerp(original, shoulder[v.Arm].Turn(hand, 1), v.ArmWeight);
            }
            if (v.Skirt > 0)
            {
                float fabric = Mathf.Sin(flutterTime + original.x * .006f - original.y * .004f);
                moved.x += v.Skirt * (fabric * 20 + lagX + clothLean * 195 + turnTail * 112);
                moved.y += v.Skirt * (fabric * 13 + lagY + (original.x - 512) * clothFacing * .12f
                    - Mathf.Abs(turnTail) * 38);
            }
            if (v.Ribbon > 0)
            {
                float flutter = Mathf.Sin(flutterTime * 1.25f + original.x * .009f - original.y * .008f);
                moved.x += v.Ribbon * (flutter * 24 + lagX * .65f + turnTail * 60);
                moved.y += v.Ribbon * (flutter * 31 + lagY * .6f);
            }
            if (v.Leg > 0)
            {
                Vector2 foot = toe[0].Turn(original, 1 - v.Foot);
                foot = toe[1].Turn(foot, v.Foot);
                moved = Vector2.Lerp(moved, foot + Vector2.Lerp(footOffset[0], footOffset[1], v.Foot), v.Leg);
            }
            float across = moved.x - 512;
            moved.x += facing * (v.Head * 27 - v.Torso * 19) - across * Mathf.Abs(facing) * .17f;
            moved.y += across * v.Torso * facing * .115f;
            moved = chest.Turn(moved, v.Chest) - new Vector2(ArtWidth / 2, ArtHeight / 2);
            // Turn around the body axis. Cloth follows the rotation after a beat.
            float projectedX = moved.x * Mathf.Cos(turnAngle);
            float depth = moved.x * Mathf.Sin(turnAngle);
            float perspective = 1 + depth * .00033f;
            moved = new Vector2(projectedX * perspective + turnTail * (v.Skirt * 42 + v.Ribbon * 36),
                moved.y * perspective - depth * .095f);
            return lean.Turn(moved, 1) + root;
        }

        private void SampleTrails(float time)
        {
            float interval = time - lastTrail;
            if (interval >= 0 && interval < 1f / 32) return;
            for (int i = 0; i < AnchorCount; i++)
            {
                List<TracePoint> trail = traces[i];
                if (interval > .25f || interval < 0) trail.Clear();
                Vector2 point = Deform(anchorPoints[i]) / ArtHeight;
                float movement = trail.Count == 0 ? 0 : Vector2.Distance(point, trail[trail.Count - 1].Position) / interval;
                trail.Add(new TracePoint(point, time, Smooth(.038f, .62f, movement)));
                while (trail.Count > 0 && time - trail[0].Time > .48f) trail.RemoveAt(0);
            }
            lastTrail = time;
        }

        private static float Smooth(float low, float high, float value) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(low, high, value));

        public void Dispose()
        {
            commands?.Dispose();
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            if (material != null) UnityEngine.Object.Destroy(material);
            if (mesh != null) UnityEngine.Object.Destroy(mesh);
        }
    }
}
