using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    // The two cameras share the same centerline. The shader handles eye-facing width and projection.
    internal sealed class StarRibbonRenderer : IDisposable
    {
        private static readonly int Eye = Shader.PropertyToID("_InkEye");
        private static readonly int InkTime = Shader.PropertyToID("_InkTime");
        private static readonly int Canvas = Shader.PropertyToID("_InkCanvas");
        private static readonly int Axes = Shader.PropertyToID("_InkAxes");
        private static readonly Color Cool = new Color(.37f, .80f, 1, .84f);
        private static readonly Color Warm = new Color(1, .73f, .48f, .84f);
        private readonly Material material;
        private readonly int worldPass, portraitPass;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly Mesh history = NewMesh("Star Sea finished strokes");
        private readonly Mesh live = NewMesh("Star Sea current stroke");
        private readonly List<Vector3> samples = new List<Vector3>(1024);
        private readonly List<Vector3> vertices = new List<Vector3>(1024);
        private readonly List<Vector3> tangents = new List<Vector3>(1024);
        private readonly List<Color32> colors = new List<Color32>(1024);
        private readonly List<Vector2> sides = new List<Vector2>(1024);
        private readonly List<Vector3> reveal = new List<Vector3>(1024);
        private readonly List<int> indices = new List<int>(2048);
        private int canvasId, historyVersion = -1, tick = -1;

        public StarRibbonRenderer(Material material)
        {
            this.material = material;
            worldPass = material.FindPass("WorldRibbon");
            portraitPass = material.FindPass("PortraitRibbon");
            live.MarkDynamic();
        }

        private static Mesh NewMesh(string name) =>
            new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };

        public void Draw(StarInkFrame frame, CommandBuffer commands, Matrix4x4 basis, StarInkSpace.PortraitProjection portrait)
        {
            if (frame.marks == null) return;
            if (canvasId != frame.canvasId || historyVersion != frame.historyVersion)
            {
                canvasId = frame.canvasId;
                historyVersion = frame.historyVersion;
                tick = -1;
                Begin();
                for (int i = 0; i < frame.marks.Length; i++)
                {
                    StarInkMark mark = frame.marks[i];
                    if (mark.branchSide != 0) AddBranch(mark);
                    else if (i != frame.penMark && mark.motif == 0 && mark.points?.Count > 1) AddStroke(mark.points, null);
                }
                Upload(history);
            }
            if (tick != frame.tick)
            {
                tick = frame.tick;
                Begin();
                if ((uint)frame.penMark < (uint)frame.marks.Length)
                    AddStroke(frame.marks[frame.penMark].points, frame.cursor);
                Upload(live);
            }
            properties.SetVector(Eye, frame.Eye);
            properties.SetFloat(InkTime, frame.time);
            int pass = portrait == null ? worldPass : portraitPass;
            if (portrait != null)
            {
                properties.SetVector(Canvas, portrait.RibbonCanvas);
                properties.SetVector(Axes, portrait.RibbonAxes);
            }
            if (history.vertexCount != 0) commands.DrawMesh(history, basis, material, 0, pass, properties);
            if (live.vertexCount != 0) commands.DrawMesh(live, basis, material, 0, pass, properties);
        }

        private void Begin()
        {
            vertices.Clear();
            tangents.Clear();
            colors.Clear();
            sides.Clear();
            reveal.Clear();
            indices.Clear();
        }

        private void AddStroke(List<Vector3> points, Vector3? cursor)
        {
            Vector3 end = points[points.Count - 1];
            bool extend = cursor.HasValue && (cursor.Value - end).sqrMagnitude > .0005f * .0005f;
            if (extend) end = cursor.Value;
            int count = points.Count + (extend ? 1 : 0);
            if (count < 2) return;

            samples.Clear();
            samples.Add(points[0]);
            for (int i = 1; i < count - 1; i++)
            {
                Vector3 finish = points[i];
                AddCurve(points[Mathf.Max(0, i - 2)], points[i - 1], finish, i + 1 == count - 1 ? end : points[i + 1]);
            }
            AddCurve(points[Mathf.Max(0, count - 3)], points[count - 2], end, end);
            AppendRibbon(end.z - StarInkSpace.PerformerDepth, null, null, 0);
        }

        // The curve goes through every control point, so a pattern stays attached after the pen moves on.
        private void AddCurve(Vector3 previous, Vector3 start, Vector3 finish, Vector3 following)
        {
            Vector3 chord = finish - start;
            float length = chord.magnitude;
            if (length < .0001f) return;
            Vector3 direction = chord / length;
            Vector3 incoming = (finish - previous).normalized, outgoing = (following - start).normalized;
            float turn = Mathf.Min(Vector3.Dot(direction, incoming), Vector3.Dot(direction, outgoing));
            if (turn < .94f)
            {
                Vector3 a = start + incoming * (length / 3), b = finish - outgoing * (length / 3);
                if (finish.z >= start.z)
                {
                    a.z = Mathf.Clamp(a.z, start.z, finish.z);
                    b.z = Mathf.Clamp(b.z, a.z, finish.z);
                }
                int steps = turn < .53f ? 4 : 2;
                for (int part = 1; part < steps; part++)
                {
                    float t = part / (float)steps, u = 1 - t;
                    samples.Add(u * u * u * start + 3 * u * u * t * a + 3 * u * t * t * b + t * t * t * finish);
                }
            }
            samples.Add(finish);
        }

        private void AddBranch(StarInkMark mark)
        {
            StarTrailArt art = StarTrailArt.Find(mark.motif);
            if (art == null) return;
            const int stem = 9;
            float side = mark.branchSide;
            float lift = mark.origin.y > .40f ? -.24f : .24f;
            var stemEnd = new Vector3(side * .89f, lift, .59f);
            var bend = new Vector3(side * .28f, lift * .18f, .10f);
            Vector3 join = stemEnd - BranchArm(art, side, 2f / art.Segments).normalized * .26f;
            samples.Clear();
            for (int i = 0; i <= stem; i++)
            {
                float t = i / (float)stem, u = 1 - t;
                samples.Add(mark.origin + 3 * u * u * t * bend + 3 * u * t * t * join + t * t * t * stemEnd);
            }
            for (int i = 1; i <= art.Segments; i++)
                samples.Add(mark.origin + stemEnd + BranchArm(art, side, i / (float)art.Segments));
            AppendRibbon(mark.origin.z - StarInkSpace.PerformerDepth, mark, art, stem);
        }

        private static Vector3 BranchArm(StarTrailArt art, float side, float progress)
        {
            Vector3 offset = art.Offset(progress);
            return new Vector3(offset.x * side * art.LateralBias * 2.80f, offset.y * 2.67f, offset.z * 5.9f);
        }

        private void AppendRibbon(float eyeDepth, StarInkMark branch, StarTrailArt art, int stem)
        {
            if (samples.Count < 2) return;
            int start = vertices.Count;
            Vector2 offset = StarInkSpace.TravelOffset(eyeDepth);
            var eye = new Vector3(offset.x, offset.y, eyeDepth);
            Vector3 previousSide = Vector3.zero;
            for (int i = 0; i < samples.Count; i++)
            {
                Vector3 point = samples[i];
                Vector3 tangent = (samples[Mathf.Min(i + 1, samples.Count - 1)] - samples[Mathf.Max(0, i - 1)]).normalized;
                Vector3 across = Vector3.Cross(point - eye, tangent);
                // A sharp reversal should keep the two edges of the ribbon from crossing.
                if (Vector3.Dot(across, previousSide) < 0)
                {
                    across = -across;
                    tangent = -tangent;
                }
                if (across.sqrMagnitude > .000001f) previousSide = across;
                float fraction = i / (float)(samples.Count - 1);
                Color32 color = Color.Lerp(Cool, Warm, fraction * .60f);
                Vector3 timing = Vector3.zero;
                if (branch != null)
                {
                    float artProgress = Mathf.Max(0, i - stem) / (float)art.Segments;
                    float progress = i < stem ? i * .17f / stem : .17f + artProgress * .83f;
                    Color hue = art.Pigment(artProgress);
                    if (i < stem) hue = Color.Lerp(Cool, hue, i / (float)stem);
                    hue.a = .88f;
                    color = hue;
                    // Finish the reveal while its base is still visible at high travel speeds.
                    timing = new Vector3(branch.born + progress * art.Duration,
                        branch.origin.z - StarInkSpace.PerformerDepth + progress * 3.12f, 1);
                }
                vertices.Add(point);
                vertices.Add(point);
                tangents.Add(tangent);
                tangents.Add(tangent);
                colors.Add(color);
                colors.Add(color);
                sides.Add(new Vector2(0, .5f));
                sides.Add(new Vector2(1, .5f));
                reveal.Add(timing);
                reveal.Add(timing);
                if (i == 0) continue;
                int a = start + (i - 1) * 2;
                indices.Add(a);
                indices.Add(a + 1);
                indices.Add(a + 2);
                indices.Add(a + 2);
                indices.Add(a + 1);
                indices.Add(a + 3);
            }
        }

        private void Upload(Mesh mesh)
        {
            if (mesh.vertexCount == 0 && vertices.Count == 0) return;
            mesh.Clear(false);
            mesh.SetVertices(vertices);
            mesh.SetNormals(tangents);
            mesh.SetColors(colors);
            mesh.SetUVs(0, sides);
            mesh.SetUVs(1, reveal);
            mesh.SetTriangles(indices, 0);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(history);
            UnityEngine.Object.Destroy(live);
        }
    }
}
