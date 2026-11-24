/* ARTISTIC RATIONALE
 * The sylph's porcelain face and the seed inside her dress keep a soft material
 * language around the machine's responses. From the back, the same figure shows
 * her collar, layered petals and ribbons. The audience can read each gesture
 * from the body and the direction of the light it passes to the world.
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    internal sealed class SpiritModel : IDisposable
    {
        private static readonly Color Skin = new Color(.94f, .70f, .58f);
        private static readonly Color Hair = new Color(.92f, .86f, .80f);
        private static readonly Color Gold = new Color(.93f, .72f, .35f);
        private static readonly Color Ink = new Color(.17f, .12f, .22f);
        private static readonly Color Silk = new Color(.97f, .87f, .62f);
        private static readonly Color Moss = new Color(.39f, .70f, .53f);
        private static readonly Vector3 Neck = new Vector3(0, 1.73f, -.015f);
        private readonly Mesh sphere = MakeSphere(), petal = MakePetal();
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly Material material;
        private readonly CommandBuffer commands;
        private Matrix4x4 root, joint;
        private static readonly int Tint = Shader.PropertyToID("_Tint");

        public SpiritModel(Material material, CommandBuffer commands)
        {
            this.material = material;
            this.commands = commands;
        }

        public void Draw(SpiritPose pose, Color accent, Vector3[] anchors)
        {
            var pivot = new Vector3(.16f, 0, .015f);
            Quaternion yaw = Quaternion.Euler(0, pose.yaw, 0);
            Vector3 translation = new Vector3(pose.shift, pose.lift, 0) + (pivot - yaw * pivot) * pose.pivot;
            root = Matrix4x4.TRS(translation, Quaternion.Euler(-pose.pitch, pose.yaw, pose.lean), Vector3.one);
            joint = root;
            Ball(new Vector3(0, 1.27f, 0), new Vector3(.23f, .34f, .155f), Moss);
            Ball(new Vector3(0, 1.49f, -.025f), new Vector3(.225f, .16f, .145f), Silk);
            Ball(new Vector3(0, 1.13f, 0), new Vector3(.19f, .19f, .141f), Gold);
            Ball(new Vector3(0, 1.065f, 0), new Vector3(.24f, .115f, .157f), Moss);
            Ball(new Vector3(0, 1.65f, 0), new Vector3(.092f, .17f, .087f), Skin);
            Ball(new Vector3(0, 1.49f, -.163f), new Vector3(.072f, .083f, .035f), Gold);
            Ball(new Vector3(0, 1.495f, -.192f), new Vector3(.046f, .057f, .026f), accent, .32f + .33f * pose.glow);
            Ball(new Vector3(0, 1.49f, .156f), new Vector3(.064f, .075f, .026f), Gold);

            for (int index = 0; index < 2; index++)
            {
                int side = index == 0 ? -1 : 1;
                DrawFoot(pose, side, anchors, index + 2);
                DrawArm(side, index == 0 ? pose.left : pose.right, accent, anchors, index);
            }
            DrawDress(pose, accent);
            DrawHead(pose, accent);
        }

        private void DrawArm(int side, SpiritArm arm, Color accent, Vector3[] anchors, int index)
        {
            Vector3 shoulder = new Vector3(side * .212f, 1.51f, -.015f);
            Vector3 elbow = shoulder + new Vector3(side * arm.upper.x, arm.upper.y, arm.upper.z).normalized * .247f;
            Vector3 wrist = elbow + new Vector3(side * arm.lower.x, arm.lower.y, arm.lower.z).normalized * .235f;
            Vector3 hand = wrist + (wrist - elbow).normalized * .065f;
            Limb(shoulder, Vector3.Lerp(shoulder, elbow, .68f), .106f, Silk);
            Ball(shoulder, new Vector3(.11f, .117f, .105f), Silk);
            Limb(Vector3.Lerp(shoulder, elbow, .49f), elbow, .064f, Skin);
            Ball(elbow, new Vector3(.061f, .061f, .059f), Skin);
            Limb(elbow, wrist, .053f, Skin);
            Ball(wrist, new Vector3(.058f, .062f, .054f), Gold);
            Ball(hand, new Vector3(.054f, .057f, .04f), Skin);

            Vector3 forward = (wrist - elbow).normalized;
            Vector3 spread = Vector3.Cross(forward, Vector3.forward).normalized;
            for (int finger = 0; finger < 3; finger++)
            {
                Vector3 baseOfFinger = hand + spread * (.025f * (finger - 1));
                Vector3 tip = baseOfFinger + forward * Mathf.Lerp(.06f, .026f, arm.curl)
                    + Vector3.forward * Mathf.Lerp(-.011f, .03f, arm.curl);
                Limb(baseOfFinger, tip, .015f, Skin);
            }
            Limb(hand + spread * .02f, hand + spread * .069f + forward * .016f, .019f, Skin);
            if (arm.light > .01f)
            {
                Vector3 light = hand + forward * .12f + new Vector3(0, .048f, -.045f);
                Ball(light, Vector3.one * Mathf.Lerp(.019f, .052f, arm.light), accent, .66f);
            }
            anchors[index] = root.MultiplyPoint3x4(hand + forward * .055f);
        }

        private void DrawFoot(SpiritPose pose, int side, Vector3[] anchors, int index)
        {
            float step = side < 0 ? pose.step : -pose.step;
            Vector3 hip = new Vector3(side * .10f, 1.01f, .012f);
            float toe = Mathf.Max(0, step);
            Vector3 knee = new Vector3(side * (.135f + toe * .075f), .61f + toe * .12f, -.04f + step * .23f);
            Vector3 ankle = new Vector3(side * (.16f + toe * .13f), .225f + toe * .23f, .005f + step * .41f);
            Limb(hip, knee, .086f, Skin);
            Limb(knee, ankle, .061f, Skin);
            Quaternion shoe = Quaternion.Euler(-9 - toe * 28, side * 12, -side * (5 + toe * 19));
            Ball(ankle, new Vector3(.066f, .089f, .135f), Moss, 0, shoe);
            Ball(ankle + shoe * new Vector3(0, -.025f, -.08f), new Vector3(.063f, .048f, .067f), Gold, 0, shoe);
            anchors[index] = root.MultiplyPoint3x4(ankle + shoe * new Vector3(0, -.025f, -.145f));
        }

        private void DrawDress(SpiritPose pose, Color accent)
        {
            Ball(new Vector3(0, .88f, .01f), new Vector3(.332f, .34f, .236f), Color.Lerp(Moss, Silk, .55f));
            for (int tier = 0; tier < 3; tier++)
            {
                int count = tier == 2 ? 8 : 10;
                for (int index = 0; index < count; index++)
                {
                    float angle = (index + tier * .46f) * 2 * Mathf.PI / count;
                    float flutter = Mathf.Sin(pose.time * 3.25f + angle * 2 - tier * .8f);
                    Vector3 radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                    Vector3 tangent = new Vector3(Mathf.Cos(angle), 0, -Mathf.Sin(angle));
                    Vector3 start = radial * (.165f + tier * .007f) + Vector3.up * (1.12f - tier * .04f);
                    float radius = .43f + tier * .047f + pose.flare * .18f;
                    float height = .56f - tier * .095f + flutter * .034f + pose.flare * .11f;
                    Vector3 end = radial * radius + tangent * (pose.ribbon * (.11f + tier * .031f))
                        + Vector3.up * height;
                    Color cloth = Color.Lerp(Silk, tier == 1 ? Gold : Moss, .13f + .16f * tier + .10f * flutter);
                    if (index % 3 == 0) cloth = Color.Lerp(cloth, accent, .22f);
                    Leaf(start, end, tangent, .171f - tier * .013f, .132f, cloth);
                    float stitch = .33f + (index % 3) * .09f;
                    Ball(Vector3.Lerp(start, end, stitch) + radial * .082f, new Vector3(.015f, .029f, .017f), Gold);
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 tail = new Vector3(side * .245f, 1.16f, .075f);
                for (int link = 0; link < 3; link++)
                {
                    float wave = Mathf.Sin(pose.time * 3.9f - .93f * link + side);
                    Vector3 next = tail + new Vector3(side * (.13f - .04f * link + .035f * wave),
                        -.18f + .065f * pose.flare, .055f + .066f * wave + .055f * pose.ribbon);
                    Leaf(tail, next, Vector3.right, .063f - .012f * link, .056f, Color.Lerp(Moss, accent, .36f));
                    tail = Vector3.Lerp(next, tail, .24f);
                }
            }
            Leaf(new Vector3(0, 1.15f, .152f), new Vector3(-.17f, 1.32f, .26f), Vector3.up, .10f, .053f, Moss);
            Leaf(new Vector3(0, 1.15f, .152f), new Vector3(.17f, 1.32f, .26f), Vector3.up, .10f, .053f, Moss);
        }

        private void DrawHead(SpiritPose pose, Color accent)
        {
            joint = root * Matrix4x4.TRS(Neck, Quaternion.Euler(-pose.nod, pose.look, pose.tilt), Vector3.one);
            Ball(new Vector3(0, .22f, .079f), new Vector3(.338f, .355f, .292f), Color.Lerp(Hair, Moss, .19f));
            Ball(new Vector3(0, .16f, -.066f), new Vector3(.279f, .289f, .234f), Skin);
            for (int side = -1; side <= 1; side += 2)
            {
                Leaf(new Vector3(side * .218f, .10f, -.03f), new Vector3(side * .437f, .28f, .04f),
                    Vector3.up, .068f, .035f, Skin);
                Ball(new Vector3(side * .315f, .145f, -.045f), Vector3.one * .029f, Gold);
                Ball(new Vector3(side * .139f, .149f, -.263f), new Vector3(.077f, .056f * pose.blink, .044f), Ink);
                Ball(new Vector3(side * .136f, .136f, -.285f), new Vector3(.063f, .041f * pose.blink, .025f), new Color(.99f,.93f,.83f));
                Ball(new Vector3(side * .129f + pose.look * .0007f, .144f, -.305f),
                    new Vector3(.035f, .043f * pose.blink, .02f), new Color(.26f,.28f,.39f));
                Ball(new Vector3(side * .129f + pose.look * .0007f, .144f, -.319f),
                    new Vector3(.021f, .032f * pose.blink, .012f), Ink);
                if (pose.blink > .4f)
                    Ball(new Vector3(side * .126f + .012f, .158f, -.329f), Vector3.one * .01f, Color.white, .25f);
                Ball(new Vector3(side * .189f, .066f, -.244f), new Vector3(.049f,.028f,.011f), new Color(.92f,.51f,.47f));
                Limb(new Vector3(side * .086f, .219f, -.285f), new Vector3(side * .183f, .211f, -.252f), .012f, Ink);
                for (int strand = 0; strand < 4; strand++)
                {
                    var start = new Vector3(side * (.07f + strand * .061f), .482f - strand * .035f, -.07f + strand * .048f);
                    var end = new Vector3(side * (.097f + strand * .066f), .257f - strand * .115f,
                        -.287f + strand * .069f);
                    Leaf(start, end, Vector3.right, .073f + strand * .012f, .112f,
                        Color.Lerp(Hair, strand < 2 ? Gold : Moss, .08f + strand * .095f));
                }
            }
            Ball(new Vector3(0, .087f, -.298f), new Vector3(.038f,.022f,.03f), Skin);
            Ball(new Vector3(0, .028f, -.28f), new Vector3(.043f,.011f + pose.glow * .011f,.011f), new Color(.62f,.29f,.29f));
            Ball(new Vector3(-.13f, .481f, .025f), new Vector3(.225f, .104f, .213f), Hair, 0, Quaternion.Euler(0,0,-13));
            for (int i = 0; i < 9; i++)
            {
                float angle = (i - 4) * .285f;
                Vector3 radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                var start = radial * .070f + new Vector3(0,.485f,.070f);
                var end = radial * .268f + new Vector3(-.02f,.025f + (i % 2) * .053f,.12f);
                Leaf(start, end, Vector3.Cross(Vector3.up, radial), .096f, .156f,
                    Color.Lerp(Hair, Moss, .11f + .14f * Mathf.Abs(radial.x)));
            }
            Vector3 flower = new Vector3(-.254f, .39f, -.165f);
            for (int index = 0; index < 8; index++)
            {
                float angle = index * Mathf.PI * .25f;
                Leaf(flower, flower + new Vector3(Mathf.Sin(angle) * .12f, Mathf.Cos(angle) * .12f, -.036f),
                    new Vector3(Mathf.Cos(angle),-Mathf.Sin(angle),0), .032f, .04f,
                    Color.Lerp(Silk, accent, .46f));
            }
            Ball(flower + new Vector3(0,0,-.047f), Vector3.one * .039f, Gold, .22f);
        }

        private void Ball(Vector3 center, Vector3 radius, Color color, float glow = 0, Quaternion? rotation = null) =>
            Piece(sphere, center, rotation ?? Quaternion.identity, radius, color, glow);

        private void Limb(Vector3 from, Vector3 to, float radius, Color color)
        {
            Vector3 axis = to - from;
            Ball((from + to) * .5f, new Vector3(radius, axis.magnitude * .66f, radius),
                color, 0, Quaternion.FromToRotation(Vector3.up, axis));
        }

        private void Leaf(Vector3 from, Vector3 to, Vector3 tangent, float width, float depth, Color color)
        {
            Vector3 axis = to - from;
            Vector3 face = Vector3.Cross(tangent, axis);
            if (face.sqrMagnitude < .0001f) face = Vector3.Cross(Vector3.forward, axis);
            Piece(petal, from, Quaternion.LookRotation(face, axis), new Vector3(width, axis.magnitude, depth), color, 0);
        }

        private void Piece(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Color color, float glow)
        {
            properties.SetVector(Tint, new Vector4(color.r, color.g, color.b, glow));
            commands.DrawMesh(mesh, joint * Matrix4x4.TRS(position, rotation, scale), material, 0, 0, properties);
        }

        private static Mesh MakeSphere() => Surface("Spirit ovoid", 16, 12, (u, v) =>
        {
            float latitude = Mathf.PI * v;
            float longitude = Mathf.PI * 2 * u;
            return new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), -Mathf.Cos(latitude),
                Mathf.Sin(latitude) * Mathf.Sin(longitude));
        }, false);

        private static Mesh MakePetal() => Surface("Spirit curved petal", 12, 12, (u, v) =>
        {
            float shell = Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * v)), .68f);
            float angle = 2 * Mathf.PI * u;
            return new Vector3(Mathf.Cos(angle) * shell * (.85f + .26f * v), v,
                shell * (.67f + .32f * Mathf.Sin(angle)) + .10f * Mathf.Sin(angle) * shell * shell);
        }, true);

        private static Mesh Surface(string name, int around, int length, Func<float, float, Vector3> point, bool petals)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var faces = new List<int>();
            for (int row = 0; row <= length; row++)
            for (int column = 0; column <= around; column++)
            {
                float u = (float)column / around;
                float v = (float)row / length;
                Vector3 p = point(u, v);
                positions.Add(p);
                float center = Mathf.Clamp(v, .001f, .999f);
                Vector3 normal = Vector3.Cross(point(u, center + .0005f) - point(u, center - .0005f),
                    point(u + .001f, center) - point(u - .001f, center)).normalized;
                normals.Add(normal);
                float value = petals ? Mathf.Lerp(.68f, 1, v) + .11f * Mathf.Cos(u * 2 * Mathf.PI) : 1;
                colors.Add(new Color(value, value, value, 1));
                if (row == length || column == around) continue;
                int a = row * (around + 1) + column;
                int b = a + around + 1;
                faces.Add(a); faces.Add(a + 1); faces.Add(b);
                faces.Add(a + 1); faces.Add(b + 1); faces.Add(b);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(faces, 0);
            return mesh;
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(sphere);
            UnityEngine.Object.Destroy(petal);
        }
    }
}
