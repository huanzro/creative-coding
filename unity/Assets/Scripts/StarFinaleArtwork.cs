using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    internal sealed class StarFinaleArtwork : IDisposable
    {
        private const int Width = 1600, Height = 900;
        private static readonly Quaternion PrintAngle = Quaternion.Euler(8, -69, -6);
        private readonly Vector3 printRight = PrintAngle * Vector3.right;
        private readonly Vector3 printUp = PrintAngle * Vector3.up;
        private readonly Vector3 printForward = PrintAngle * Vector3.forward;
        private readonly Material material;
        private readonly Mesh paths;
        private readonly CommandBuffer render;
        private readonly RenderTexture canvas;
        private Vector3 printCenter;
        private Vector2 printScale;
        private float wideDistance;
        private float lastSeconds = -1, lastBrightness;
        private bool lastPrint;

        public StarFinaleArtwork(StarInkFrame frame)
        {
            Shader shader = Resources.Load<Shader>("StarSea/FinaleArtwork");
            if (shader == null || !shader.isSupported) return;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            render = new CommandBuffer { name = "Star Sea finale artwork" };
            canvas = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32) {
                name = "Star Sea celestial print", filterMode = FilterMode.Bilinear
            };
            paths = Build(frame);
        }

        public bool Ready => paths != null;

        private Mesh Build(StarInkFrame frame)
        {
            var locations = new List<Vector3>(4096);
            var tangents = new List<Vector3>(4096);
            var colors = new List<Color32>(4096);
            var widths = new List<Vector2>(4096);
            var indices = new List<int>(8192);
            var samples = new List<Vector3>(4096);
            var bounds = new Bounds(PrintBasis(frame.cursor), Vector3.one * .08f);

            void AddPath(StarTrailArt brush, bool primary)
            {
                if (samples.Count < 2) return;
                int start = locations.Count;
                for (int i = 0; i < samples.Count; i++)
                {
                    Vector3 point = samples[i];
                    bounds.Encapsulate(PrintBasis(point));
                    Vector3 tangent = (samples[Mathf.Min(i + 1, samples.Count - 1)] - samples[Mathf.Max(0, i - 1)]).normalized;
                    float progress = i / (float)(samples.Count - 1);
                    Color tint = primary ? Color.Lerp(new Color(.38f,.80f,1), new Color(1,.78f,.59f), progress) : brush.Pigment(progress);
                    float weight = primary ? 1 : .68f;
                    for (int side = 0; side < 2; side++)
                    {
                        locations.Add(point);
                        tangents.Add(tangent);
                        colors.Add(tint);
                        widths.Add(new Vector2(side, weight));
                    }
                    if (i == 0) continue;
                    int edge = start + (i - 1) * 2;
                    indices.Add(edge); indices.Add(edge + 1); indices.Add(edge + 2);
                    indices.Add(edge + 2); indices.Add(edge + 1); indices.Add(edge + 3);
                }
            }

            if (frame.archive != null)
            {
                StarRibbonRenderer.SampleStroke(frame.archive, frame.cursor, samples);
                if (samples.Count < 2)
                {
                    samples.Clear();
                    samples.Add(frame.cursor - new Vector3(.12f,.016f,.06f));
                    samples.Add(frame.cursor);
                }
                AddPath(null, true);
            }
            if (frame.archiveMarks != null)
                foreach (StarInkMark mark in frame.archiveMarks)
                {
                    StarTrailArt brush = StarTrailArt.Find(mark.motif);
                    if (brush == null) continue;
                    int detail = frame.archiveMarks.Count > 250 ? 44 : 96;
                    StarRibbonRenderer.SampleBranch(mark, brush, samples, detail);
                    AddPath(brush, false);
                }

            printCenter = PrintAngle * bounds.center;
            float width = Mathf.Max(bounds.size.x, 3);
            float height = Mathf.Max(bounds.size.y, .65f);
            printScale = new Vector2(1.72f / width, Mathf.Min(1.40f / height, 11f / width));
            wideDistance = bounds.extents.z + 1.31f * Mathf.Max(bounds.extents.x / (StarInkSpace.Aspect / (2 * StarProjection.Focal)),
                bounds.extents.y * 2 * StarProjection.Focal);

            var mesh = new Mesh { name = "Complete Star Sea ink", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(locations);
            mesh.SetNormals(tangents);
            mesh.SetColors(colors);
            mesh.SetUVs(0, widths);
            mesh.SetTriangles(indices, 0);
            return mesh;
        }

        private Vector3 PrintBasis(Vector3 position) => new Vector3(Vector3.Dot(position, printRight),
            Vector3.Dot(position, printUp), Vector3.Dot(position, printForward));

        public Texture Draw(StarInkFrame frame, float seconds, float brightness, bool print = false)
        {
            if (!Ready) return null;
            if (seconds == lastSeconds && brightness == lastBrightness && print == lastPrint && canvas.IsCreated())
                return canvas;
            lastSeconds = seconds;
            lastBrightness = brightness;
            lastPrint = print;
            float orbit = print ? 1 : OpeningStoryboard.Ease(1.75f, 7.4f, seconds);
            float paper = print ? 1 : OpeningStoryboard.Ease(7.05f, 9.40f, seconds);
            Quaternion rotation = Quaternion.Slerp(Quaternion.identity, PrintAngle, orbit);
            Vector3 target = Vector3.Lerp(frame.cursor, printCenter, orbit);
            float radius = Mathf.Lerp(StarInkSpace.PerformerDepth, wideDistance, orbit);
            Vector3 forward = rotation * Vector3.forward;
            Vector3 eye = target - forward * radius;
            float gain = Mathf.Lerp(.66f, brightness, OpeningStoryboard.Ease(9.10f, 12f, seconds));
            material.SetColor("_Mood", new Color(paper, gain, orbit, print ? 1 : 0));
            material.SetVector("_Eye", eye);
            material.SetVector("_ViewRight", rotation * Vector3.right);
            material.SetVector("_ViewUp", rotation * Vector3.up);
            material.SetVector("_ViewForward", forward);
            material.SetVector("_PrintCenter", printCenter);
            material.SetVector("_PrintRight", printRight * printScale.x);
            material.SetVector("_PrintUp", printUp * printScale.y);
            material.SetVector("_Ink", new Vector4(StarProjection.Focal * 2 / StarInkSpace.Aspect, StarProjection.Focal * 2,
                StarInkSpace.Aspect, Mathf.Lerp(.012f, .0078f, paper)));
            var previous = RenderTexture.active;
            Graphics.Blit(Texture2D.blackTexture, canvas, material, 0);
            if (paths.vertexCount > 0)
            {
                render.Clear();
                render.SetRenderTarget(canvas);
                render.DrawMesh(paths, Matrix4x4.identity, material, 0, 1);
                Graphics.ExecuteCommandBuffer(render);
            }
            RenderTexture.active = previous;
            return canvas;
        }

        public byte[] MakePng(StarInkFrame frame)
        {
            if (!Ready) return null;
            Draw(frame, Journey.StarFinaleDuration, 1.08f, true);
            var before = RenderTexture.active;
            var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture.active = canvas;
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            pixels.Apply();
            RenderTexture.active = before;
            byte[] png = pixels.EncodeToPNG();
            UnityEngine.Object.Destroy(pixels);
            return png;
        }

        public void Dispose()
        {
            render?.Dispose();
            if (canvas != null) { canvas.Release(); UnityEngine.Object.Destroy(canvas); }
            if (paths != null) UnityEngine.Object.Destroy(paths);
            if (material != null) UnityEngine.Object.Destroy(material);
        }
    }
}
