using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    // The two cameras share the same centerline. The shader handles eye-facing width and projection.
    internal sealed class StarRibbonRenderer : IDisposable
    {
        private static readonly int Depth = Shader.PropertyToID("_InkDepth");
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
                    if (i != frame.penMark && frame.marks[i].motif == 0 && frame.marks[i].points?.Count > 1)
                        AddStroke(frame.marks[i].points, null);
                Upload(history);
            }
            if (tick != frame.tick)
            {
                tick = frame.tick;
                Begin();
                if (frame.pointing && (uint)frame.penMark < (uint)frame.marks.Length)
                    AddStroke(frame.marks[frame.penMark].points, frame.cursor);
                Upload(live);
            }
            properties.SetFloat(Depth, frame.depth);
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
                Vector3 control = points[i];
                Vector3 next = i + 1 == count - 1 ? end : points[i + 1];
                Vector3 a = (points[i - 1] + control) * .5f;
                Vector3 b = (control + next) * .5f;
                float turn = Vector3.Dot((control - a).normalized, (b - control).normalized);
                // Dense straight segments only need their midpoint; reserve more samples for corners.
                int steps = turn > .97f ? 1 : turn > .80f ? 2 : 3;
                for (int part = 0; part < steps; part++)
                {
                    float t = part / (float)steps;
                    samples.Add(Vector3.Lerp(Vector3.Lerp(a, control, t), Vector3.Lerp(control, b, t), t));
                }
                if (i == count - 2) samples.Add(b);
            }
            samples.Add(end);

            int start = vertices.Count;
            for (int i = 0; i < samples.Count; i++)
            {
                Vector3 point = samples[i];
                Vector3 tangent = (samples[Mathf.Min(i + 1, samples.Count - 1)] - samples[Mathf.Max(0, i - 1)]).normalized;
                Color32 color = Color.Lerp(Cool, Warm, i * .60f / (samples.Count - 1));
                vertices.Add(point);
                vertices.Add(point);
                tangents.Add(tangent);
                tangents.Add(tangent);
                colors.Add(color);
                colors.Add(color);
                sides.Add(new Vector2(0, .5f));
                sides.Add(new Vector2(1, .5f));
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
            mesh.SetTriangles(indices, 0);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(history);
            UnityEngine.Object.Destroy(live);
        }
    }
}
