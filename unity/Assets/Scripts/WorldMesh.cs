using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    internal sealed class WorldMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> faces = new List<int>();
        private readonly List<Mesh> owned;

        public WorldMesh(List<Mesh> owned) { this.owned = owned; }

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            int index = vertices.Count;
            Vertex(a, normal, color);
            Vertex(b, normal, color);
            Vertex(c, normal, color);
            faces.Add(index);
            faces.Add(index + 1);
            faces.Add(index + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            Triangle(a, b, c, color);
            Triangle(a, c, d, color);
        }

        public void Box(Vector3 center, Vector3 dimensions, Quaternion rotation, Color color)
        {
            Vector3 x = rotation * Vector3.right * (dimensions.x * .5f);
            Vector3 y = rotation * Vector3.up * (dimensions.y * .5f);
            Vector3 z = rotation * Vector3.forward * (dimensions.z * .5f);
            Vector3 p0 = center - x - y - z, p1 = center + x - y - z;
            Vector3 p2 = center + x + y - z, p3 = center - x + y - z;
            Vector3 p4 = center - x - y + z, p5 = center + x - y + z;
            Vector3 p6 = center + x + y + z, p7 = center - x + y + z;
            Quad(p0, p3, p2, p1, color);
            Quad(p4, p5, p6, p7, color);
            Quad(p0, p4, p7, p3, color);
            Quad(p1, p2, p6, p5, color);
            Quad(p3, p7, p6, p2, color);
            Quad(p0, p1, p5, p4, color);
        }

        public void Tube(Vector3[] spine, float[] radii, int sides, Color color, float elliptic = 1)
        {
            int first = vertices.Count;
            for (int i = 0; i < spine.Length; i++)
            {
                Vector3 tangent = (spine[Mathf.Min(spine.Length - 1, i + 1)] - spine[Mathf.Max(0, i - 1)]).normalized;
                Vector3 side = Vector3.Cross(tangent, Mathf.Abs(tangent.y) > .98f ? Vector3.forward : Vector3.up).normalized;
                Vector3 other = Vector3.Cross(tangent, side).normalized;
                for (int j = 0; j <= sides; j++)
                {
                    float theta = j * Mathf.PI * 2 / sides;
                    Vector3 radial = (side * Mathf.Cos(theta) + other * (Mathf.Sin(theta) * elliptic));
                    float crimp = 1 + .11f * Mathf.Sin(theta * 3 + i * .61f) + .07f * Mathf.Cos(theta * 5 - i * .35f);
                    Vertex(spine[i] + radial * radii[i] * crimp, radial.normalized,
                        Shade(color, .88f + .12f * Mathf.Sin(theta * 3 + .8f) + i * .14f / spine.Length));
                    if (i == spine.Length - 1 || j == sides) continue;
                    int a = first + i * (sides + 1) + j, b = a + sides + 1;
                    faces.Add(a); faces.Add(a + 1); faces.Add(b);
                    faces.Add(b); faces.Add(a + 1); faces.Add(b + 1);
                }
            }
        }

        public void Rock(Vector3 center, Vector3 size, Color color, float seed, int rings = 5, int sides = 9, float rough = .23f)
        {
            int first = vertices.Count;
            Quaternion twist = Quaternion.Euler(0, seed * 53.6f, seed * 12.3f);
            for (int y = 0; y <= rings; y++)
            for (int x = 0; x <= sides; x++)
            {
                float phi = Mathf.Lerp(-Mathf.PI * .5f, Mathf.PI * .5f, y / (float)rings);
                float theta = x * Mathf.PI * 2 / sides;
                Vector3 n = new Vector3(Mathf.Cos(theta) * Mathf.Cos(phi), Mathf.Sin(phi), Mathf.Sin(theta) * Mathf.Cos(phi));
                float var = 1 + rough * Mathf.Sin(n.x * 8.4f + n.z * 5.7f + seed * 19.8f) *
                    Mathf.Cos(n.y * 11.8f - seed);
                Vector3 p = center + twist * (Vector3.Scale(n, size) * var);
                Vertex(p, twist * new Vector3(n.x / size.x, n.y / size.y, n.z / size.z).normalized,
                    Shade(color, Mathf.Lerp(.67f, 1.07f, n.y * .5f + .5f)));
                if (y == rings || x == sides) continue;
                int a = first + y * (sides + 1) + x, b = a + sides + 1;
                faces.Add(a); faces.Add(b); faces.Add(a + 1);
                faces.Add(a + 1); faces.Add(b); faces.Add(b + 1);
            }
        }

        public void Leaf(Vector3 root, Vector3 tip, Vector3 across, float width, Color color)
        {
            Vector3 side = across.normalized * width;
            Vector3 join = Vector3.Lerp(root, tip, .42f);
            Vector3 vein = join + Vector3.Cross(tip - root, side).normalized * (width * .22f);
            Triangle(root, join - side, vein, Shade(color, .74f));
            Triangle(vein, join - side, tip, Shade(color, .83f));
            Triangle(root, vein, join + side, color);
            Triangle(vein, tip, join + side, Shade(color, 1.09f));
        }

        public void Surface(int columns, int rows, System.Func<int, int, Vector3> position,
            System.Func<Vector3, Color> color, System.Predicate<Vector3> omit)
        {
            int first = vertices.Count;
            for (int z = 0; z <= rows; z++)
            for (int x = 0; x <= columns; x++)
            {
                Vector3 point = position(x, z);
                Vector3 across = position(Mathf.Min(x + 1, columns), z) - position(Mathf.Max(0, x - 1), z);
                Vector3 along = position(x, Mathf.Min(z + 1, rows)) - position(x, Mathf.Max(0, z - 1));
                Vertex(point, Vector3.Cross(along, across).normalized, color(point));
                if (x >= columns || z >= rows || (omit != null && omit((point + position(x + 1, z + 1)) * .5f))) continue;
                int a = first + z * (columns + 1) + x, b = a + columns + 1;
                faces.Add(a); faces.Add(b); faces.Add(a + 1);
                faces.Add(b); faces.Add(b + 1); faces.Add(a + 1);
            }
        }

        private void Vertex(Vector3 point, Vector3 normal, Color color)
        {
            vertices.Add(point);
            normals.Add(normal);
            colors.Add(color);
        }

        public Mesh Finish(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetIndices(faces, MeshTopology.Triangles, 0);
            mesh.UploadMeshData(true);
            owned.Add(mesh);
            return mesh;
        }

        public static Color Shade(Color color, float gain) => new Color(color.r * gain, color.g * gain, color.b * gain, color.a);
    }
}
