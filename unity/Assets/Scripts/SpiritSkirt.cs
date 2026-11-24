using System;
using UnityEngine;

namespace Echoes
{
    internal sealed class SpiritSkirt : IDisposable
    {
        public const int Layers = 3;
        private const int Pleats = 80, Along = 19;
        private const int LayerVertices = (Pleats + 1) * (Along + 1);
        public readonly Mesh Mesh = new Mesh { name = "Layered pleated silk and embroidered hems" };
        private readonly Vector3[] points = new Vector3[Layers * LayerVertices];

        public SpiritSkirt()
        {
            Mesh.MarkDynamic();
            var uv = new Vector2[points.Length];
            Mesh.vertices = points;
            for (int layer = 0; layer < Layers; layer++)
            for (int row = 0; row <= Along; row++)
            for (int column = 0; column <= Pleats; column++)
                uv[layer * LayerVertices + row * (Pleats + 1) + column] =
                    new Vector2(column / (float)Pleats, row / (float)Along);
            Mesh.uv = uv;
            Mesh.subMeshCount = Layers;
            for (int layer = 0; layer < Layers; layer++)
            {
                var indices = new int[Pleats * Along * 6];
                int index = 0;
                for (int row = 0; row < Along; row++)
                for (int column = 0; column < Pleats; column++)
                {
                    int a = layer * LayerVertices + row * (Pleats + 1) + column;
                    int b = a + Pleats + 1;
                    indices[index++] = a;
                    indices[index++] = b;
                    indices[index++] = a + 1;
                    indices[index++] = b;
                    indices[index++] = b + 1;
                    indices[index++] = a + 1;
                }
                Mesh.SetTriangles(indices, layer, false);
            }
            Mesh.bounds = new Bounds(new Vector3(0, .73f, 0), new Vector3(2, 1.5f, 2));
        }

        public void Drape(SpiritPose pose)
        {
            for (int layer = 0; layer < Layers; layer++)
            for (int row = 0; row <= Along; row++)
            {
                float down = row / (float)Along;
                float hem = down * down;
                float lag = pose.time * 3.02f - down * 2.4f - layer * .28f;
                for (int column = 0; column <= Pleats; column++)
                {
                    float angle = column * (2 * Mathf.PI / Pleats);
                    float scallop = Mathf.Cos(angle * (layer == 1 ? 9 : 8) + layer * .31f);
                    float wake = Mathf.Sin(angle * 2 + lag);
                    float depth = down * (.615f + .089f * layer) + hem * (.031f * scallop + .017f * wake);
                    depth -= hem * (.12f * pose.flare + .035f * pose.tuck * Mathf.Cos(angle));
                    float radius = .156f + (2 - layer) * .030f + .566f * Mathf.Pow(Mathf.Max(0, depth), .89f);
                    float pleat = Mathf.Cos(angle * 10 + down * .88f + layer * .35f);
                    radius += down * (.027f * pleat + hem * (.012f * wake + pose.flare * .105f));
                    // The front of the gown lifts with the knees before the hem catches up.
                    radius += .218f * pose.tuck * Mathf.Sin(Mathf.PI * down) * Mathf.Max(0, -Mathf.Cos(angle));
                    float swing = angle + .51f * pose.ribbon * hem;
                    float x = Mathf.Sin(swing) * radius + .055f * hem * Mathf.Sin(lag);
                    float z = Mathf.Cos(swing) * radius * .83f + hem * pose.extension * .14f;
                    points[layer * LayerVertices + row * (Pleats + 1) + column] =
                        new Vector3(x, 1.144f - depth, z);
                }
            }
            Mesh.vertices = points;
            Mesh.RecalculateNormals();
        }

        public void Dispose() => UnityEngine.Object.Destroy(Mesh);
    }
}
