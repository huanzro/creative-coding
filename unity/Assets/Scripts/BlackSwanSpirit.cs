/* ARTISTIC RATIONALE
 * The swan holds a dark silhouette against the independent projection's black
 * field. Cool feathers indicate the direction and opening of each shared gesture.
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    internal sealed class BlackSwanSpirit : IDisposable
    {
        private static readonly Color Coal = new Color(.094f, .103f, .144f);
        private static readonly Color Raven = new Color(.128f, .151f, .209f);
        private static readonly Color Steel = new Color(.236f, .294f, .381f);
        private static readonly Color NeckPlumage = new Color(.122f, .141f, .187f);
        private static readonly Color FeatherWhite = new Color(.63f, .69f, .72f);
        private static readonly Color Wine = new Color(.52f, .10f, .12f);
        private static readonly Color Vermilion = new Color(.88f, .19f, .17f);
        private static readonly Color Ivory = new Color(.91f, .83f, .73f);
        private static readonly int Tint = Shader.PropertyToID("_Tint");

        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly MaterialPropertyBlock paint = new MaterialPropertyBlock();
        private readonly Material material;
        private readonly Mesh sphere, feather, webbing, upperBill, lowerBill, billBand;
        private readonly SwanNeck neck;
        private Matrix4x4 root;
        private CommandBuffer commands;

        public BlackSwanSpirit(Material material)
        {
            this.material = material;
            var oval = new WorldMesh(meshes);
            oval.Rock(Vector3.zero, Vector3.one, Color.white, 0, 20, 28, 0);
            sphere = oval.Finish("Black swan curved forms");

            var vane = new WorldMesh(meshes);
            vane.Surface(14, 14, FeatherVertex, p => WorldMesh.Shade(Color.white, .80f + .16f * p.y - .15f * Mathf.Abs(p.x)), null);
            feather = vane.Finish("Black swan feather vane");

            var foot = new WorldMesh(meshes);
            foot.Surface(13, 10, WebVertex, p => WorldMesh.Shade(Color.white, .84f + .065f * Mathf.Cos(30 * p.x)), null);
            webbing = foot.Finish("Black swan webbed foot");

            upperBill = MakeBill("Scarlet upper bill", .00f, 1, 0, 1);
            lowerBill = MakeBill("Wine-colored lower bill", -.018f, .48f, 0, 1);
            billBand = MakeBill("Ivory bill band", 0, 1.051f, .78f, .91f);
            neck = new SwanNeck();
        }

        public void Draw(SpiritPose pose, Color accent, Vector3[] anchors, Matrix4x4 basis, CommandBuffer target)
        {
            root = basis * SpiritModel.PoseTransform(pose);
            commands = target;
            float breath = 1 + .022f * pose.breath;
            Ellipsoid(new Vector3(0, .951f, .096f), new Vector3(.426f, .495f * breath, .687f),
                Raven, Quaternion.Euler(-7, 0, 0));
            DrawTail(pose);
            for (int index = 0; index < 2; index++)
            {
                int side = index == 0 ? -1 : 1;
                DrawFlank(side);
                DrawFoot(pose, side, anchors, index + 2);
                DrawWing(pose, side, index == 0 ? pose.left : pose.right, accent, anchors, index);
            }
            DrawHead(pose);
        }

        private void DrawTail(SpiritPose pose)
        {
            for (int index = -2; index <= 2; index++)
            {
                float fan = 1 + .18f * pose.flare;
                var start = new Vector3(index * .069f, .824f, .60f);
                var end = new Vector3(index * .113f * fan, .902f + .14f * pose.ribbon - .034f * Mathf.Abs(index),
                    1.018f - .036f * Mathf.Abs(index));
                Feather(start, end, Vector3.up, .085f, .071f, Color.Lerp(Raven, Steel, .10f * (2 - Mathf.Abs(index))));
            }
        }

        private void DrawFlank(int side)
        {
            for (int i = 0; i < 5; i++)
            {
                var laid = new Vector3(side * (.318f + .024f * i), 1.241f - .068f * i, -.174f + .049f * i);
                var tip = new Vector3(side * (.278f + .032f * i), 1.002f - .081f * i, .571f - .019f * i);
                Feather(laid, tip, new Vector3(side, .52f, -.29f), .073f, .061f,
                    Color.Lerp(Raven, Steel, .15f + .10f * Mathf.Sin(.8f * i)));
            }
        }

        private void DrawWing(SpiritPose pose, int side, SpiritArm arm, Color accent, Vector3[] anchors, int index)
        {
            SpiritModel.ArmGeometry(side, arm, out Vector3 shoulder, out Vector3 elbow,
                out Vector3 wrist, out Vector3 spread, out Vector3 forward);
            Vector3 focus = SpiritModel.HandContact(wrist, spread, forward, arm);
            Vector3 lead = focus - wrist;
            Vector3 sweep = new Vector3(-side * .12f, -.92f, .69f);
            Vector3 fan = Vector3.ProjectOnPlane(sweep, forward).normalized;
            if (fan.sqrMagnitude < .01f) fan = Vector3.Cross(forward, spread).normalized;
            Vector3 normal = Vector3.Cross(forward, fan).normalized;
            Vector3 coverNormal = normal.z > 0 ? -normal : normal;
            float fold = Mathf.Clamp01(arm.curl);
            float fanWidth = Mathf.Lerp(.26f, .10f, fold) * (1 + .065f * pose.flare);

            Vector3 inner = shoulder + new Vector3(-side * .043f, -.085f, .15f);
            Ellipsoid(Vector3.Lerp(inner, elbow, .49f), new Vector3(.12f, .204f, .10f),
                Raven, Quaternion.FromToRotation(Vector3.up, elbow - inner));
            Ellipsoid(Vector3.Lerp(elbow, wrist, .45f), new Vector3(.083f, .174f, .075f),
                Coal, Quaternion.FromToRotation(Vector3.up, wrist - elbow));

            // Every move is measured from the common shoulder, elbow and wrist.
            // The leading feather actually ends where the projection emits its light.
            Feather(wrist - forward * .061f, focus, coverNormal, Mathf.Lerp(.052f, .032f, arm.point), .046f,
                Color.Lerp(Raven, Steel, .68f));
            for (int primary = 0; primary < 6; primary++)
            {
                float row = primary / 5f;
                Vector3 from = Vector3.Lerp(wrist, elbow, row * .84f) + fan * (.012f + row * .06f);
                Vector3 to = Vector3.Lerp(focus, elbow, .09f + .69f * row) + fan * (fanWidth * (.54f + row * .60f))
                    - coverNormal * (.029f + row * .024f) + lead * (.06f * (1 - row));
                float colorMix = primary % 2 == 0 ? .27f : .12f;
                Feather(from, to, coverNormal, .049f + row * .024f, .058f, Color.Lerp(Coal, Steel, .44f + colorMix));
            }
            for (int covert = 0; covert < 5; covert++)
            {
                float row = covert / 4f;
                Vector3 from = Vector3.Lerp(inner, wrist, .09f + row * .68f) + coverNormal * .082f;
                Vector3 end = Vector3.Lerp(inner, wrist, .14f + row * .65f) + fan * (.096f + fanWidth * .67f)
                    + coverNormal * .038f;
                Feather(from, end, coverNormal, .054f + .027f * (1 - row), .054f,
                    Color.Lerp(Raven, Steel, .32f + .10f * (covert % 2)));
            }

            float spreadOut = Mathf.InverseLerp(.53f, .85f, Mathf.Abs(wrist.x)) * (1 - fold);
            for (int i = 0; i < 3; i++)
            {
                Vector3 from = Vector3.Lerp(elbow, wrist, .44f + .17f * i) - coverNormal * .043f;
                Vector3 tip = Vector3.Lerp(elbow, wrist, .49f + .19f * i) + fan * (fanWidth * (.82f - .10f * i))
                    - coverNormal * .053f;
                Feather(from, tip, -coverNormal, .026f + .006f * spreadOut, .036f,
                    Color.Lerp(Steel, FeatherWhite, .28f + .52f * spreadOut));
            }

            if (arm.light > .05f)
            {
                Vector3 glowAt = focus + coverNormal * .037f;
                float radius = Mathf.Lerp(.012f, .027f, arm.light);
                Ellipsoid(glowAt, Vector3.one * radius, accent, Quaternion.identity, arm.light * .57f);
            }
            anchors[index] = root.MultiplyPoint3x4(focus);
        }

        private void DrawFoot(SpiritPose pose, int side, Vector3[] anchors, int index)
        {
            SpiritModel.FootGeometry(pose, side, out Vector3 hip, out Vector3 knee, out Vector3 ankle, out Quaternion shoe);
            Vector3 joint = Vector3.Lerp(knee, hip, .13f) + new Vector3(0, 0, .028f);
            Rod(hip, joint, .078f, Coal);
            Rod(joint, ankle, .036f, Raven);
            Ellipsoid(ankle, new Vector3(.056f, .049f, .047f), Steel, shoe);
            Piece(webbing, Matrix4x4.TRS(ankle, shoe, Vector3.one), Color.Lerp(Raven, Steel, .24f));
            for (int toe = -1; toe <= 1; toe++)
            {
                Vector3 from = ankle + shoe * new Vector3(toe * .014f, -.022f, -.018f);
                Vector3 tip = ankle + shoe * new Vector3(toe * .072f, -.025f, -.145f + .031f * Mathf.Abs(toe));
                Rod(from, tip, .012f, Steel);
            }
            anchors[index] = root.MultiplyPoint3x4(SpiritModel.FootContact(ankle, shoe));
        }

        private void DrawHead(SpiritPose pose)
        {
            float retract = Mathf.Clamp01(.72f * pose.tuck + pose.settle * .30f);
            float stretch = Mathf.Clamp01(pose.extension) * (1 - .40f * retract);
            float turn = Mathf.Clamp(pose.look / 43, -1, 1);
            float nod = Mathf.Clamp(pose.nod / 36, -1, 1);
            float breathe = .014f * pose.breath;
            Vector3 skull = new Vector3(.084f * turn, 2.009f - .082f * retract - .035f * nod + breathe + .277f * stretch,
                -.514f + .068f * retract - .042f * nod + .12f * stretch);
            Quaternion face = Quaternion.Euler(-pose.nod * .69f, pose.look * .91f, pose.tilt * .66f);
            Vector3 jaw = skull + face * new Vector3(0, -.019f, .031f);
            neck.Bend(new Vector3(0, .969f, -.267f),
                new Vector3(.032f * turn, 1.279f, -.462f),
                new Vector3(.079f * turn, 1.557f - .069f * retract + .115f * stretch, -.435f + .029f * retract),
                new Vector3(.070f * turn, 1.855f - .079f * retract + breathe + .225f * stretch, -.222f - .028f * nod),
                jaw);
            Piece(neck.Mesh, Matrix4x4.identity, NeckPlumage);

            Matrix4x4 faceMatrix = Matrix4x4.TRS(skull, face, Vector3.one);
            Piece(sphere, faceMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(4, 0, 0),
                new Vector3(.133f, .146f, .193f)), NeckPlumage);

            for (int side = -1; side <= 1; side += 2)
            {
                var eye = new Vector3(side * .111f, .034f, -.113f);
                Quaternion set = Quaternion.Euler(0, side * -48, -side * 8);
                Piece(sphere, faceMatrix * Matrix4x4.TRS(eye, set, new Vector3(.047f, .029f, .015f)), Coal);
                float lid = .32f + .68f * Mathf.Clamp01(pose.blink);
                Vector3 iris = eye + set * new Vector3(.002f * turn, 0, -.013f);
                Piece(sphere, faceMatrix * Matrix4x4.TRS(iris, set, new Vector3(.024f, .021f * lid, .012f)), Wine);
                Vector3 pupil = iris + set * (Vector3.back * .009f);
                Piece(sphere, faceMatrix * Matrix4x4.TRS(pupil, set, new Vector3(.011f, .015f * lid, .006f)), Coal);
                Vector3 glint = pupil + set * new Vector3(-.003f, .005f * lid, -.004f);
                Piece(sphere, faceMatrix * Matrix4x4.TRS(glint, set, Vector3.one * (.0046f * lid)), Ivory, .09f);
            }

            Matrix4x4 beak = faceMatrix * Matrix4x4.Translate(new Vector3(0, -.038f, -.114f));
            Piece(upperBill, beak, Vermilion);
            Piece(billBand, beak, Ivory);
            float open = Mathf.Clamp01(pose.expression + .4f * pose.glow) * 12;
            Piece(lowerBill, beak * Matrix4x4.Rotate(Quaternion.Euler(-open, 0, 0)), Wine);
            for (int side = -1; side <= 1; side += 2)
                Piece(sphere, beak * Matrix4x4.TRS(new Vector3(side*.056f,.021f,-.098f), Quaternion.Euler(-13,0,side*23),
                    new Vector3(.008f,.006f,.024f)), Wine);
        }

        private Mesh MakeBill(string name, float offset, float height, float start, float end)
        {
            var geometry = new WorldMesh(meshes);
            geometry.Surface(22, 12, (column, row) =>
            {
                float t = Mathf.Lerp(start, end, row / 12f);
                float angle = column * (2 * Mathf.PI / 22);
                float round = Mathf.Pow(Mathf.Max(.001f, 1 - t*t), .48f);
                float arch = -.018f * t * t + offset;
                return new Vector3(.079f * round * Mathf.Cos(angle) * (1 + (height - 1) * .18f),
                    arch + Mathf.Sin(angle) * .049f * height * round,
                    -.305f * t - (start > 0 ? .0017f : 0));
            }, p => Color.white, null);
            return geometry.Finish(name);
        }

        private static Vector3 FeatherVertex(int column, int row)
        {
            float v = row / 14f;
            float theta = column * (2 * Mathf.PI / 14);
            float curve = Mathf.Sin(Mathf.PI * v);
            float width = Mathf.Pow(Mathf.Max(.001f, curve), .68f) * (1.09f - .31f * v);
            float camber = (.72f - .36f * v) * curve;
            return new Vector3(Mathf.Cos(theta) * width, v, camber + .31f * width * Mathf.Sin(theta));
        }

        private static Vector3 WebVertex(int column, int row)
        {
            float t = row / 10f;
            float u = column / 13f * 2 - 1;
            float span = Mathf.Abs(u);
            float edge = Mathf.Lerp(1, .762f, span) + .036f * Mathf.Sin(9.42478f * span);
            return new Vector3(u * (.019f + t * .058f), -.036f + .022f * Mathf.Sin(t * Mathf.PI) * (1 - span * .6f),
                -.153f * t * edge);
        }

        private void Ellipsoid(Vector3 center, Vector3 radius, Color color, Quaternion orientation, float glow = 0) =>
            Piece(sphere, Matrix4x4.TRS(center, orientation, radius), color, glow);

        private void Rod(Vector3 from, Vector3 to, float radius, Color color) =>
            Ellipsoid((from + to) * .5f, new Vector3(radius, (to - from).magnitude * .59f, radius), color,
                Quaternion.FromToRotation(Vector3.up, to - from));

        private void Feather(Vector3 start, Vector3 end, Vector3 face, float width, float depth, Color tint)
        {
            Vector3 axis = end - start;
            if (axis.sqrMagnitude < .0001f) return;
            Vector3 forward = Vector3.ProjectOnPlane(face, axis).normalized;
            if (forward.sqrMagnitude < .01f)
                forward = Vector3.ProjectOnPlane(Mathf.Abs(axis.normalized.y) > .85f ? Vector3.forward : Vector3.up, axis).normalized;
            Piece(feather, Matrix4x4.TRS(start, Quaternion.LookRotation(forward, axis),
                new Vector3(width, axis.magnitude, depth)), tint);
        }

        private void Piece(Mesh mesh, Matrix4x4 placement, Color color, float glow = 0)
        {
            paint.SetVector(Tint, new Vector4(color.r, color.g, color.b, glow));
            commands.DrawMesh(mesh, root * placement, material, 0, 0, paint);
        }

        public void Dispose()
        {
            foreach (Mesh mesh in meshes) UnityEngine.Object.Destroy(mesh);
            UnityEngine.Object.Destroy(neck.Mesh);
        }

        private sealed class SwanNeck
        {
            private const int Rings = 31;
            private const int Sides = 14;
            private static readonly float[] Radii = { .204f, .151f, .105f, .091f, .091f };
            private readonly Vector3[] controls = new Vector3[Radii.Length];
            private readonly Vector3[] vertices = new Vector3[Rings * Sides];
            private readonly Vector3[] normals = new Vector3[Rings * Sides];
            private readonly Vector2[] circle = new Vector2[Sides];
            public readonly Mesh Mesh;

            public SwanNeck()
            {
                var colors = new Color[vertices.Length];
                var triangles = new int[(Rings - 1) * Sides * 6];
                int next = 0;
                for (int col = 0; col < Sides; col++)
                {
                    float angle = (2 * Mathf.PI / Sides) * col;
                    circle[col] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
                for (int row = 0; row < Rings; row++)
                for (int col = 0; col < Sides; col++)
                {
                    int a = row * Sides + col;
                    float gain = .92f + .07f * Mathf.Sin(col * .4488f);
                    colors[a] = WorldMesh.Shade(Color.white, gain);
                    if (row == Rings - 1) continue;
                    int b = row * Sides + (col + 1) % Sides;
                    triangles[next++] = a; triangles[next++] = a + Sides; triangles[next++] = b;
                    triangles[next++] = b; triangles[next++] = a + Sides; triangles[next++] = b + Sides;
                }
                Mesh = new Mesh { name = "Black swan breathing S-neck", vertices = vertices, normals = normals,
                    colors = colors, triangles = triangles };
                Mesh.MarkDynamic();
                Mesh.bounds = new Bounds(new Vector3(0, 1.6f, -.43f), new Vector3(1.3f, 1.6f, 1.2f));
            }

            public void Bend(Vector3 breast, Vector3 lower, Vector3 middle, Vector3 upper, Vector3 skull)
            {
                controls[0] = breast;
                controls[1] = lower;
                controls[2] = middle;
                controls[3] = upper;
                controls[4] = skull;
                for (int row = 0; row < Rings; row++)
                {
                    float t = 4f * row / (Rings - 1);
                    Vector3 point = Center(t);
                    Vector3 tangent = (Center(Mathf.Min(4, t + .015f)) - Center(Mathf.Max(0, t - .015f))).normalized;
                    Vector3 across = Vector3.ProjectOnPlane(Vector3.right, tangent).normalized;
                    Vector3 normal = Vector3.Cross(tangent, across).normalized;
                    int interval = Mathf.Min((int)t, 3);
                    float radius = Mathf.SmoothStep(Radii[interval], Radii[interval + 1], t - interval);
                    for (int col = 0; col < Sides; col++)
                    {
                        Vector2 c = circle[col];
                        int index = row * Sides + col;
                        vertices[index] = point + radius * (c.x * across + c.y * .89f * normal);
                        normals[index] = (c.x * across + c.y / .89f * normal).normalized;
                    }
                }
                Mesh.SetVertices(vertices, 0, vertices.Length, MeshUpdateFlags.DontRecalculateBounds);
                Mesh.SetNormals(normals);
            }

            private Vector3 Center(float t)
            {
                int i = Mathf.Min((int)t, 3);
                float u = t - i;
                Vector3 a = controls[Mathf.Max(0, i - 1)], b = controls[i];
                Vector3 c = controls[i + 1], d = controls[Mathf.Min(4, i + 2)];
                return .5f * ((2 * b) + (c - a) * u + (2 * a - 5 * b + 4 * c - d) * (u * u)
                    + (-a + 3 * b - 3 * c + d) * (u * u * u));
            }
        }
    }
}
