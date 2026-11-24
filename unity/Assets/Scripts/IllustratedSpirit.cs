/* ARTISTIC RATIONALE
 * The original forest sylph remains a painted performer. The hands, hair,
 * silk and shoes follow the same choreography as the sculpted performer.
 */
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    internal sealed class IllustratedSpirit : IDisposable
    {
        private const float ArtWidth = 1024, ArtHeight = 1536, Pixel = .00148f;
        private const int Columns = 48, Rows = 62;
        private static readonly Vector2[] Shoulders = { new Vector2(427, 450), new Vector2(627, 446) };
        private static readonly Vector2[] Elbows = { new Vector2(335, 547), new Vector2(711, 554) };
        private static readonly Vector2[] Wrists = { new Vector2(248, 592), new Vector2(783, 605) };
        private static readonly Vector2[] Ankles = { new Vector2(460, 1376), new Vector2(531, 1371) };
        private static readonly Vector2[] Tips = {
            new Vector2(133, 649), new Vector2(897, 658), new Vector2(473, 1490), new Vector2(527, 1460)
        };
        private readonly Mesh mesh = new Mesh { name = "Green and gold sylph skin" };
        private readonly Material material;
        private readonly Vector3[] vertices = new Vector3[(Columns + 1) * (Rows + 1)];
        private readonly BindPoint[] skin;
        private readonly BindPoint[] tips = new BindPoint[4];
        private readonly Bend[] shoulder = new Bend[2], elbow = new Bend[2], wrist = new Bend[2], ankle = new Bend[2];
        private readonly Vector2[] feet = new Vector2[2];
        private Bend head;
        private float heading, bank, pitch, time, cloth, breath, flare;

        private struct BindPoint
        {
            public Vector2 position;
            public int side;
            public float arm, elbow, wrist, head, torso, skirt, ribbon, hair, leg, foot;
        }

        private readonly struct Bend
        {
            private readonly Vector2 center;
            private readonly float sin, cos;

            public Bend(Vector2 center, float degrees)
            {
                this.center = center;
                sin = Mathf.Sin(degrees * Mathf.Deg2Rad);
                cos = Mathf.Cos(degrees * Mathf.Deg2Rad);
            }

            public Vector2 Apply(Vector2 point, float weight)
            {
                Vector2 d = point - center;
                return Vector2.LerpUnclamped(point, center + new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos), weight);
            }
        }

        public IllustratedSpirit(Texture2D art, Shader shader)
        {
            material = new Material(shader) { mainTexture = art };
            mesh.MarkDynamic();
            skin = new BindPoint[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[Columns * Rows * 6];
            int triangle = 0;
            for (int row = 0; row <= Rows; row++)
            for (int column = 0; column <= Columns; column++)
            {
                int index = row * (Columns + 1) + column;
                float u = (float)column / Columns, v = (float)row / Rows;
                skin[index] = Bind(new Vector2(u * ArtWidth, v * ArtHeight));
                uv[index] = new Vector2(u, 1 - v);
                if (row == Rows || column == Columns) continue;
                int next = index + Columns + 1;
                triangles[triangle++] = index;
                triangles[triangle++] = index + 1;
                triangles[triangle++] = next;
                triangles[triangle++] = next;
                triangles[triangle++] = index + 1;
                triangles[triangle++] = next + 1;
            }
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(new Vector3(0, 1.16f, 0), new Vector3(6, 6, 4));
            for (int i = 0; i < tips.Length; i++) tips[i] = Bind(Tips[i]);
        }

        public void Draw(SpiritPose pose, Color tint, float contact, Vector3[] anchors, CommandBuffer commands)
        {
            time = pose.time;
            heading = pose.yaw * Mathf.Deg2Rad;
            bank = pose.lean * Mathf.Deg2Rad;
            pitch = pose.pitch * Mathf.Deg2Rad;
            breath = pose.breath;
            flare = pose.flare;
            cloth = .72f * pose.ribbon + .28f * pose.extension;
            head = new Bend(new Vector2(524, 359), -(pose.tilt + .21f * pose.nod));
            for (int i = 0; i < 2; i++)
            {
                int sign = i == 0 ? -1 : 1;
                SpiritArm arm = i == 0 ? pose.left : pose.right;
                float upper = Mathf.Clamp(Vector2.SignedAngle(Elbows[i] - Shoulders[i],
                    new Vector2(sign * arm.upper.x, -arm.upper.y)), -89, 89);
                float lower = Mathf.Clamp(Mathf.DeltaAngle(upper, Vector2.SignedAngle(Wrists[i] - Elbows[i],
                    new Vector2(sign * arm.lower.x, -arm.lower.y))), -55, 55);
                shoulder[i] = new Bend(Shoulders[i], upper * .81f);
                elbow[i] = new Bend(Elbows[i], lower * .71f);
                wrist[i] = new Bend(Wrists[i], sign * (arm.wristFlex * .38f - arm.curl * 11));
                float stride = sign * pose.step;
                ankle[i] = new Bend(Ankles[i], -sign * (stride * 18 + pose.extension * 14));
                feet[i] = new Vector2(sign * (pose.tuck * 34 + stride * 28),
                    -pose.tuck * 167 - Mathf.Max(0, stride) * 74 - pose.settle * 28);
            }
            for (int i = 0; i < vertices.Length; i++) vertices[i] = Deform(skin[i], pose);
            for (int i = 0; i < tips.Length; i++) anchors[i] = Deform(tips[i], pose);
            mesh.vertices = vertices;
            material.SetVector("_CastLight", new Vector4(tint.r, tint.g, tint.b, contact));
            material.SetFloat("_Exposure", .87f + .13f * Mathf.InverseLerp(-1, 1, Mathf.Cos(heading)));
            material.SetFloat("_Lids", pose.blink);
            commands.DrawMesh(mesh, Matrix4x4.identity, material, 0, 0);
        }

        private static BindPoint Bind(Vector2 point)
        {
            float x = point.x, y = point.y;
            int arm = x < 512 ? 0 : 1;
            int sign = arm == 0 ? -1 : 1;
            Vector2 s = Shoulders[arm], e = Elbows[arm], w = Wrists[arm];
            float along = (x - s.x) * sign, lower = (e.x - s.x) * sign, upper = (w.x - s.x) * sign;
            float center = along < lower ? Mathf.LerpUnclamped(s.y, e.y, Mathf.Max(0, along) / lower) :
                along < upper ? Mathf.Lerp(e.y, w.y, (along - lower) / (upper - lower)) : w.y + (along - upper) * .37f;
            float bottom = arm == 0 ? 644 + .39f * (248 - x) : 653 + .37f * (x - 783) + 29 * Smooth(799, 835, x);
            float palm = Smooth(16, 70, (x - e.x) * sign) * Smooth(510, 560, y) * (1 - Smooth(700, 730, y));
            float sleeve = Smooth(-25, 53, along) * (1 - Smooth(43, 86, Mathf.Abs(y - center))) * (1 - Smooth(675, 719, y));
            return new BindPoint {
                position = point, side = arm, arm = Mathf.Max(sleeve, palm) * (1 - Smooth(bottom, bottom + 26, y)),
                elbow = Smooth(-45, 51, (x - e.x) * sign + (y - e.y) * .28f),
                wrist = Smooth(-28, 13, (x - w.x) * sign + (y - w.y) * .19f),
                head = 1 - Smooth(310, 410, y), torso = Smooth(325, 470, y) * (1 - Smooth(580, 970, y)),
                skirt = Smooth(578, 1287, y) * (1 - Smooth(1315, 1470, y)),
                ribbon = Smooth(645, 925, y) * (1 - Smooth(1080, 1190, y)) * Smooth(216, 460, Mathf.Abs(x - 512)),
                hair = Smooth(86, 296, Mathf.Abs(x - 512)) * Smooth(133, 322, y) * (1 - Smooth(334, 386, y)),
                leg = Smooth(1288, 1410, y) * (1 - Smooth(91, 128, Mathf.Abs(x - 490))), foot = Smooth(478, 502, x)
            };
        }

        private Vector3 Deform(BindPoint skin, SpiritPose pose)
        {
            Vector2 original = skin.position;
            Vector2 point = head.Apply(original, skin.head);
            if (skin.arm > 0)
            {
                int i = skin.side;
                Vector2 hand = wrist[i].Apply(original, skin.wrist);
                hand = elbow[i].Apply(hand, skin.elbow);
                point = Vector2.Lerp(point, shoulder[i].Apply(hand, 1), skin.arm);
            }
            float lag = Mathf.Sin(time * 3.2f + original.x * .006f - original.y * .004f);
            float hem = Mathf.Sin(time * 4.8f + original.x * .011f - original.y * .008f);
            float back = Mathf.Sin(heading - .18f * cloth);
            point.x += skin.hair * (hem * 10 + back * 24) + skin.skirt * (lag * 16 + cloth * 54 * back)
                + skin.ribbon * (hem * 24 + back * cloth * 91);
            point.y += skin.hair * lag * 7 + skin.skirt * (lag * 13 - flare * 38)
                + skin.ribbon * hem * 22 - skin.torso * breath * 5;
            if (skin.leg > 0)
            {
                Vector2 foot = Vector2.Lerp(ankle[0].Apply(original, 1), ankle[1].Apply(original, 1), skin.foot);
                point = Vector2.Lerp(point, foot + Vector2.Lerp(feet[0], feet[1], skin.foot), skin.leg);
            }
            point -= new Vector2(ArtWidth * .5f, ArtHeight * .5f);
            float facing = Mathf.Cos(heading), sin = Mathf.Sin(heading);
            float depth = point.x * sin * .41f + point.y * Mathf.Sin(pitch) * .24f;
            float squash = Mathf.Lerp(.48f, 1, Mathf.Abs(facing));
            float x = point.x * squash * (1 + depth * .00038f) + sin * (skin.head * 29 - skin.torso * 14);
            float y = point.y * (1 - Mathf.Abs(Mathf.Sin(pitch)) * .16f) - depth * .12f;
            return new Vector3((x * Mathf.Cos(bank) + y * Mathf.Sin(bank)) * Pixel + pose.shift,
                1.16f + (x * Mathf.Sin(bank) - y * Mathf.Cos(bank)) * Pixel + pose.lift, depth * Pixel);
        }

        private static float Smooth(float a, float b, float t) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));

        public void Dispose()
        {
            UnityEngine.Object.Destroy(mesh);
            UnityEngine.Object.Destroy(material);
        }
    }
}
