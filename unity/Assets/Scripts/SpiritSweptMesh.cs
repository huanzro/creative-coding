using UnityEngine;

namespace Echoes
{
    // The same surface follows a bent arm, knee or length of silk. The centers
    // and their tangents are continuous across the joint.
    internal sealed class SpiritSweptMesh
    {
        private const int Rings = 13, Sides = 12;
        private readonly Vector3[] vertices = new Vector3[Rings * Sides];
        private readonly Vector3[] normals = new Vector3[Rings * Sides];
        private readonly Vector2[] circle = new Vector2[Sides];
        public readonly Mesh Mesh;

        public SpiritSweptMesh(string name)
        {
            var indices = new int[(Rings - 1) * Sides * 6];
            int face = 0;
            for (int row = 0; row < Rings - 1; row++)
            for (int column = 0; column < Sides; column++)
            {
                int a = row * Sides + column;
                int b = row * Sides + (column + 1) % Sides;
                indices[face++] = a; indices[face++] = b; indices[face++] = a + Sides;
                indices[face++] = a + Sides; indices[face++] = b; indices[face++] = b + Sides;
            }
            for (int n = 0; n < Sides; n++)
            {
                float angle = n * 2 * Mathf.PI / Sides;
                circle[n] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            var shade = new Color[vertices.Length];
            for (int i = 0; i < shade.Length; i++) shade[i] = Color.white;
            Mesh = new Mesh { name = name, vertices = vertices, colors = shade, triangles = indices };
            Mesh.MarkDynamic();
            Mesh.bounds = new Bounds(new Vector3(0, 1.1f, 0), new Vector3(4.5f, 3.5f, 3.5f));
        }

        public void Bend(Vector3 first, Vector3 middle, Vector3 last, Vector3 radii, float depth, Vector3 reference)
        {
            for (int ring = 0; ring < Rings; ring++)
            {
                float v = ring / (Rings - 1f);
                Vector3 point = At(v, first, middle, last);
                Vector3 up = (At(Mathf.Min(1, v + .008f), first, middle, last) -
                    At(Mathf.Max(0, v - .008f), first, middle, last)).normalized;
                Vector3 side = Vector3.Cross(up, reference).normalized;
                if (side.sqrMagnitude < .01f) side = Vector3.Cross(up, Vector3.right).normalized;
                Vector3 forward = Vector3.Cross(side, up).normalized;
                float radius = v <= .5f ? Mathf.Lerp(radii.x, radii.y, v * 2) :
                    Mathf.Lerp(radii.y, radii.z, v * 2 - 1);
                for (int n = 0; n < Sides; n++)
                {
                    Vector2 local = circle[n];
                    int index = ring * Sides + n;
                    vertices[index] = point + (side * local.x + forward * local.y * depth) * radius;
                    normals[index] = (side * local.x + forward * local.y / depth).normalized;
                }
            }
            Mesh.SetVertices(vertices, 0, vertices.Length, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
            Mesh.SetNormals(normals);
        }

        private static Vector3 At(float v, Vector3 first, Vector3 middle, Vector3 last)
        {
            Vector3 a, b, ta, tb;
            Vector3 tangent = (last - first) * .58f;
            float x;
            if (v <= .5f)
            {
                a = first; b = middle; ta = middle - first; tb = tangent; x = v * 2;
            }
            else
            {
                a = middle; b = last; ta = tangent; tb = last - middle; x = v * 2 - 1;
            }
            float x2 = x * x, x3 = x2 * x;
            return (2 * x3 - 3 * x2 + 1) * a + (x3 - 2 * x2 + x) * ta +
                (-2 * x3 + 3 * x2) * b + (x3 - x2) * tb;
        }
    }
}
