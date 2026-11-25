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
        private static readonly Color Skin = new Color(.95f, .75f, .652f);
        private static readonly Color Hair = new Color(.93f, .901f, .828f);
        private static readonly Color Gold = new Color(.91f, .70f, .39f);
        private static readonly Color Silk = new Color(.98f, .916f, .745f);
        private static readonly Color Moss = new Color(.48f, .69f, .59f);
        private static readonly Color DeepSilk = new Color(.28f, .45f, .43f);
        private static readonly Vector3 Neck = new Vector3(0, 1.73f, -.015f);
        private readonly Mesh sphere = MakeSphere(), petal = MakePetal();
        private readonly Mesh bodice = MakeBodice(), trim = MakeTrim();
        private readonly SpiritSkirt skirt = new SpiritSkirt();
        private static readonly Color[] SkirtColors = {
            new Color(.93f, .863f, .703f), new Color(.59f, .763f, .644f), new Color(.35f, .565f, .483f)
        };
        private static readonly Color[] BellSilk = {
            new Color(.99f,.932f,.748f), new Color(.68f,.826f,.674f), new Color(.33f,.577f,.466f)
        };
        private readonly SpiritSweptMesh[] arms = MakePair("Continuous arm"),
            legs = MakePair("Continuous leg"), ribbons = MakePair("Curved silk ribbon");
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly Material material;
        private CommandBuffer commands;
        private bool longDress;
        private Matrix4x4 root, joint;
        private static readonly int Tint = Shader.PropertyToID("_Tint");
        private static readonly int Fabric = Shader.PropertyToID("_Fabric");
        private static readonly int Face = Shader.PropertyToID("_Face");
        private static readonly int Expression = Shader.PropertyToID("_Expression");

        public SpiritModel(Material material) { this.material = material; }

        public void Draw(SpiritPose pose, Color accent, Vector3[] anchors, bool longDress,
            Matrix4x4 basis, CommandBuffer target)
        {
            this.longDress = longDress;
            commands = target;
            var pivot = new Vector3(.16f, 0, .015f);
            Quaternion yaw = Quaternion.Euler(0, pose.yaw, 0);
            Quaternion attitude = Quaternion.Euler(-pose.pitch, pose.yaw, pose.lean);
            Vector3 torsoPivot = new Vector3(0, 1.13f, 0);
            Vector3 translation = new Vector3(pose.shift, pose.lift, 0) + (pivot - yaw * pivot) * pose.pivot;
            translation += (torsoPivot - attitude * torsoPivot) * pose.centerPivot;
            root = basis * Matrix4x4.TRS(translation, attitude, Vector3.one);
            joint = root;
            float breathing = 1 + .023f * pose.breath;
            Color bodiceTint = longDress ? Color.Lerp(Silk, Moss, .72f) : Moss;
            Piece(bodice, new Vector3(0, 1.35f, 0), Quaternion.identity, new Vector3(.25f, .59f, .177f * breathing), bodiceTint, 0);
            Ball(new Vector3(0, 1.64f, -.012f), new Vector3(.10f, .143f, .094f), Skin);
            Piece(trim, new Vector3(0, 1.128f, 0), Quaternion.identity, new Vector3(.182f, .021f, .145f), Gold, 0);
            Leaf(new Vector3(0, 1.64f, -.102f), new Vector3(0, 1.192f, -.166f), Vector3.right, .132f, .022f, Silk);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 from = new Vector3(side * .14f, 1.569f, -.131f * breathing);
                Vector3 to = new Vector3(side * .028f, 1.42f, -.187f * breathing);
                Leaf(from, to, Vector3.right, .041f, .019f, Gold);
                Ball(from + new Vector3(0, .005f, -.016f), Vector3.one * .018f, Silk);
                Vector3 seed = new Vector3(side * .12f, 1.26f, -.179f * breathing);
                for (int seam = 0; seam < 3; seam++)
                {
                    Vector3 stitch = seed + new Vector3(-side * seam * .028f, seam * .033f, -.003f);
                    Leaf(stitch, stitch + new Vector3(side * .022f, .037f, -.003f), Vector3.forward,
                        .007f, .008f, Color.Lerp(Gold, Silk, .43f));
                }
            }
            Ball(new Vector3(0, 1.448f, -.181f * breathing), new Vector3(.052f, .065f, .025f), Gold);
            Ball(new Vector3(0, 1.452f, -.205f * breathing), new Vector3(.033f, .043f, .023f), accent, .24f + .32f * pose.glow);
            Ball(new Vector3(0, 1.488f, .175f), new Vector3(.051f, .064f, .023f), Gold);
            if (longDress)
            {
                for (int i = -2; i <= 2; i++)
                    Leaf(new Vector3(0,1.123f,-.178f),new Vector3(i*.043f,1.245f-Mathf.Abs(i)*.018f,-.168f),
                        Vector3.right,.027f,.026f,Color.Lerp(Gold,Silk,.17f+Mathf.Abs(i)*.065f));
                Ball(new Vector3(0,1.108f,-.196f),new Vector3(.038f,.039f,.023f),Gold);
            }

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
            Vector3 shoulder = new Vector3(side * .212f, 1.51f, -.019f);
            Vector3 elbow = shoulder + new Vector3(side * arm.upper.x, arm.upper.y, arm.upper.z).normalized * .266f;
            Vector3 wrist = elbow + new Vector3(side * arm.lower.x, arm.lower.y, arm.lower.z).normalized * .247f;
            Vector3 forearm = (wrist - elbow).normalized;
            Vector3 spread = Vector3.Cross(forearm, Vector3.forward).normalized;
            if (spread.sqrMagnitude < .01f) spread = Vector3.right;
            Vector3 forward = Quaternion.AngleAxis(arm.wristFlex, spread) * forearm;
            Vector3 hand = wrist + forward * .047f;
            Quaternion sleeve = Quaternion.FromToRotation(Vector3.up, elbow - shoulder);
            Ball(Vector3.Lerp(shoulder, elbow, .25f), new Vector3(.091f, .125f, .085f), Silk, 0, sleeve);
            Leaf(shoulder + new Vector3(0, .065f, -.046f), Vector3.Lerp(shoulder, elbow, .57f) + new Vector3(0, .033f, -.068f),
                Vector3.forward, .04f, .025f, Color.Lerp(Silk, Moss, .36f));
            Sweep(arms[index], Vector3.Lerp(shoulder, elbow, .37f), elbow, wrist + forearm * .018f,
                new Vector3(.068f, .055f, .037f), .91f, Vector3.forward, Skin);
            Piece(trim, wrist - forearm * .016f, Quaternion.FromToRotation(Vector3.up, forearm),
                new Vector3(.044f,.014f,.041f), Gold, 0);
            Ball(hand, new Vector3(.042f, .050f, .030f), Skin, 0, Quaternion.FromToRotation(Vector3.up, forward));
            Vector3 knuckle = Vector3.Cross(spread, forward).normalized;
            Vector3 contact = hand + forward * .073f;
            for (int finger = 0; finger < 4; finger++)
            {
                float fingerSpread = finger - 1.5f;
                float curl = arm.curl * (finger == 0 ? 1 - arm.point : 1);
                float length = .063f - Mathf.Abs(fingerSpread - .13f) * .006f;
                Vector3 baseOfFinger = hand + forward * .030f + spread * (.018f * fingerSpread);
                Vector3 bend = baseOfFinger + forward * (length * .58f) + knuckle * (.022f * curl);
                Vector3 tip = bend + forward * (length * Mathf.Lerp(.46f, .09f, curl)) +
                    knuckle * (.021f * curl) + spread * (.003f * fingerSpread);
                Limb(baseOfFinger, bend, .011f, Skin);
                Limb(bend, tip, .0094f, Skin);
                if (finger == 0) contact = Vector3.Lerp(contact, tip, arm.point);
            }
            Limb(hand - spread * (side * .022f), hand - spread * (side * .058f) +
                forward * .031f + knuckle * (-.006f + .020f * arm.curl), .015f, Skin);
            if (arm.light > .01f)
            {
                Vector3 light = hand + forward * .12f + new Vector3(0, .048f, -.045f);
                Ball(light, Vector3.one * Mathf.Lerp(.010f, .034f, arm.light), accent, .61f);
            }
            anchors[index] = root.MultiplyPoint3x4(contact);
        }

        private void DrawFoot(SpiritPose pose, int side, Vector3[] anchors, int index)
        {
            float step = side < 0 ? pose.step : -pose.step;
            Vector3 hip = new Vector3(side * .10f, 1.01f, .012f);
            float toe = Mathf.Max(0, step), tuck = Mathf.Clamp01(pose.tuck);
            float trailing = pose.extension * (side < 0 ? .76f : 1);
            Vector3 knee = new Vector3(side * (.129f + toe * .061f), .58f + toe * .108f + tuck * .24f,
                -.047f + step * .18f + trailing * .09f - tuck * .26f - pose.settle * .10f);
            Vector3 ankle = new Vector3(side * (.149f + toe * .10f), .212f + toe * .19f + tuck * .37f + trailing * .046f,
                .008f + step * .32f + trailing * .22f + tuck * .06f);
            Sweep(legs[index - 2], hip, knee, ankle, new Vector3(.085f, .064f, .048f), .92f, Vector3.forward, Skin);
            Quaternion shoe = Quaternion.Euler(-8 - toe * 21 - trailing * 28 + pose.settle * 20, side * 10,
                -side * (4 + toe * 14));
            Ball(ankle, new Vector3(.068f, .078f, .133f), DeepSilk, 0, shoe);
            Ball(ankle + shoe * new Vector3(0, -.012f, -.071f), new Vector3(.062f, .044f, .071f), Moss, 0, shoe);
            Ball(ankle + shoe * new Vector3(0, .019f, -.089f), new Vector3(.029f, .017f, .028f), Gold, 0, shoe);
            anchors[index] = root.MultiplyPoint3x4(ankle + shoe * new Vector3(0, -.025f, -.145f));
        }

        private void DrawDress(SpiritPose pose, Color accent)
        {
            skirt.Drape(pose, longDress);
            for (int layer = 0; layer < SpiritSkirt.Layers; layer++)
            {
                Color cloth = Color.Lerp((longDress ? BellSilk : SkirtColors)[layer], accent, .045f);
                properties.SetVector(Tint, new Vector4(cloth.r, cloth.g, cloth.b, 0));
                properties.SetFloat(Fabric, 1 - layer * .19f);
                commands.DrawMesh(skirt.Mesh, joint, material, layer, 0, properties);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float wave = Mathf.Sin(pose.time * 3.1f - .93f + side);
                Vector3 knot = new Vector3(side * .191f, 1.151f, .061f);
                Vector3 curve = new Vector3(side * (.373f + wave * .038f), .914f + .054f * pose.flare,
                    .131f + .22f * pose.ribbon);
                Vector3 tip = new Vector3(side * (.410f + wave * .070f + pose.flare * .13f + (longDress?.245f:0)),
                    .53f + .14f * pose.flare + .038f * wave - (longDress?.188f:0), .24f + .30f * pose.ribbon);
                Sweep(ribbons[side < 0 ? 0 : 1], knot, curve, tip,
                    longDress ? new Vector3(.070f,.086f,.016f) : new Vector3(.049f,.058f,.010f),
                    .21f, Vector3.forward, Color.Lerp(Moss, accent, .27f));
            }
            Leaf(new Vector3(0, 1.15f, .152f), new Vector3(-.17f, 1.32f, .26f), Vector3.up, .10f, .053f, Moss);
            Leaf(new Vector3(0, 1.15f, .152f), new Vector3(.17f, 1.32f, .26f), Vector3.up, .10f, .053f, Moss);
        }

        private void DrawHead(SpiritPose pose, Color accent)
        {
            joint = root * Matrix4x4.TRS(Neck, Quaternion.Euler(-pose.nod, pose.look, pose.tilt),
                Vector3.one * (longDress ? 1.005f : .94f));
            Ball(new Vector3(0, .229f, .085f), new Vector3(.311f, .345f, .270f), Color.Lerp(Hair, Moss, .17f));
            float worry = Mathf.Clamp01(pose.expression);
            properties.SetVector(Tint, new Vector4(Skin.r, Skin.g, Skin.b, 0));
            properties.SetFloat(Fabric, 0);
            properties.SetFloat(Face, 1);
            properties.SetVector(Expression, new Vector4(pose.blink, worry, pose.look, pose.nod));
            commands.DrawMesh(sphere, joint * Matrix4x4.TRS(new Vector3(0, .164f, -.067f), Quaternion.identity,
                new Vector3(.272f, .283f, .230f)), material, 0, 0, properties);
            for (int side = -1; side <= 1; side += 2)
            {
                Leaf(new Vector3(side * .219f, .10f, -.04f), new Vector3(side * .400f, .259f, .035f),
                    Vector3.up, .060f, .034f, Skin);
                Leaf(new Vector3(side * .260f,.134f,-.038f), new Vector3(side*.365f,.221f,-.012f),
                    Vector3.up,.027f,.012f, new Color(.80f,.49f,.46f));
                Ball(new Vector3(side * .315f, .145f, -.045f), Vector3.one * .029f, Gold);
                for (int strand = 0; strand < 5; strand++)
                {
                    float breeze = Mathf.Sin(pose.time * 2.78f + strand * .81f) * (.0097f + .012f * pose.ribbon);
                    var start = new Vector3(side * (.024f + strand*.052f), .488f - strand*.029f, -.088f + strand*.038f);
                    var end = new Vector3(side * (.069f + strand*.053f) + breeze, .302f - strand*.081f,
                        -.276f + strand*.062f - breeze*.31f);
                    Leaf(start, end, Vector3.right, .057f + strand*.011f, .066f,
                        Color.Lerp(Hair, strand < 2 ? Gold : Moss, .065f + strand*.063f));
                }
                if (longDress)
                {
                    float drift=Mathf.Sin(pose.time*2.9f+side)*(.034f+.028f*pose.ribbon);
                    for (int i=0;i<3;i++)
                    {
                        var start = new Vector3(side*(.20f+i*.026f),.351f-i*.009f,.088f+i*.046f);
                        var end = new Vector3(side*(.40f+i*.029f)+drift,.057f+i*.042f,.11f+i*.079f);
                        Leaf(start,end,Vector3.right,.067f-i*.008f,.073f,Color.Lerp(Hair,Silk,.10f*i));
                    }
                }
            }
            Ball(new Vector3(0,.087f,-.283f), new Vector3(.026f,.020f,.022f), Color.Lerp(Skin, Silk, .10f));
            for (int i = 0; i < 6; i++)
            {
                float slide = i * .021f;
                Vector3 from = new Vector3(.048f - slide * .71f, .546f - slide * .23f, -.023f - slide);
                Vector3 to = new Vector3(-.238f - slide * .21f, .427f - slide, -.082f - slide * .66f);
                Leaf(from, to, Vector3.forward, .030f + slide * .14f, .055f,
                    Color.Lerp(Hair, Silk, .08f + .11f * Mathf.Sin(i * .93f)));
            }
            for (int i = 0; i < 9; i++)
            {
                float angle = (i - 4) * .285f;
                Vector3 radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                var start = radial * .111f + new Vector3(0,.451f,.107f);
                var end = radial * .278f + new Vector3(-.02f + .014f*Mathf.Sin(pose.time*2.6f+i*.73f),
                    .053f + (i % 2) * .056f,.146f);
                Leaf(start, end, Vector3.Cross(Vector3.up, radial), .066f, .079f,
                    Color.Lerp(Hair, Moss, .11f + .14f * Mathf.Abs(radial.x)));
            }
            Vector3 flower = new Vector3(-.245f, .391f, -.177f);
            for (int index = 0; index < 8; index++)
            {
                float angle = index * Mathf.PI * .25f;
                Leaf(flower, flower + new Vector3(Mathf.Sin(angle)*.105f, Mathf.Cos(angle)*.105f, -.041f),
                    new Vector3(Mathf.Cos(angle),-Mathf.Sin(angle),0), .030f, .032f,
                    Color.Lerp(Silk, accent, .36f));
            }
            Ball(flower + new Vector3(0,0,-.047f), Vector3.one * .027f, Gold, .22f);
            for (int i = 0; i < 5; i++)
                Ball(flower + new Vector3(.018f * Mathf.Sin(i * 1.26f),.018f * Mathf.Cos(i * 1.26f),-.068f),
                    Vector3.one * .0058f, Silk);
            if (longDress)
            {
                for (int i=0;i<3;i++)
                {
                    Vector3 from=flower+new Vector3(.033f-i*.036f,.022f,-.012f);
                    Vector3 end=from+new Vector3(-.034f+Mathf.Sin(pose.time*3.2f+i)*.015f,-.128f-i*.073f,-.029f);
                    Limb(from,end,.0095f,Gold);
                    Ball(end,new Vector3(.033f,.044f,.032f),Silk);
                    Piece(trim,end+Vector3.down*.022f,Quaternion.identity,new Vector3(.027f,.0062f,.027f),Gold,0);
                }
            }
        }

        private static SpiritSweptMesh[] MakePair(string name) =>
            new[] { new SpiritSweptMesh(name + " left"), new SpiritSweptMesh(name + " right") };

        private void Sweep(SpiritSweptMesh surface, Vector3 start, Vector3 middle, Vector3 tip, Vector3 radius,
            float depth, Vector3 up, Color color)
        {
            surface.Bend(start, middle, tip, radius, depth, up);
            Piece(surface.Mesh, Vector3.zero, Quaternion.identity, Vector3.one, color, 0);
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
            properties.SetFloat(Fabric, 0);
            properties.SetFloat(Face, 0);
            commands.DrawMesh(mesh, joint * Matrix4x4.TRS(position, rotation, scale), material, 0, 0, properties);
        }

        private static Mesh MakeSphere() => Surface("Spirit ovoid", 22, 16, (u, v) =>
        {
            float latitude = Mathf.PI * v;
            float longitude = Mathf.PI * 2 * u;
            return new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), -Mathf.Cos(latitude),
                Mathf.Sin(latitude) * Mathf.Sin(longitude));
        }, false);

        private static Mesh MakeBodice() => Surface("Tailored silk bodice", 26, 19, (u, v) =>
        {
            float angle = 2 * Mathf.PI * u;
            float width = .70f + .273f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.18f, .81f, v));
            width *= Mathf.Lerp(1, .19f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.81f, 1, v)));
            width *= Mathf.Lerp(.76f, 1, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .13f, v)));
            return new Vector3(Mathf.Cos(angle) * width, v - .5f, Mathf.Sin(angle) * width);
        }, true);

        private static Mesh MakeTrim() => Surface("Fine brass edging", 24, 10, (u, v) =>
        {
            float a = 2 * Mathf.PI * u, b = 2 * Mathf.PI * v;
            float radius = 1 + .077f * Mathf.Cos(b);
            return new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(b), Mathf.Sin(a) * radius);
        }, false);

        private static Mesh MakePetal() => Surface("Spirit curved petal", 16, 17, (u, v) =>
        {
            float shell = Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * v)), .68f);
            float angle = 2 * Mathf.PI * u;
            return new Vector3(Mathf.Cos(angle) * shell * (.85f + .26f * v), v,
                shell * (.54f + .25f * Mathf.Sin(angle)) + .08f * Mathf.Sin(angle) * shell * shell);
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
                float value = petals ? Mathf.Lerp(.79f, 1, v) + .073f * Mathf.Cos(u * 2 * Mathf.PI) : 1;
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
            UnityEngine.Object.Destroy(bodice);
            skirt.Dispose();
            UnityEngine.Object.Destroy(trim);
            for (int i = 0; i < 2; i++)
            {
                UnityEngine.Object.Destroy(arms[i].Mesh);
                UnityEngine.Object.Destroy(legs[i].Mesh);
                UnityEngine.Object.Destroy(ribbons[i].Mesh);
            }
        }
    }
}
