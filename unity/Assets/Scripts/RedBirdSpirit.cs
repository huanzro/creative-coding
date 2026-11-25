/* ARTISTIC RATIONALE
 * A small flame-like bird reinterprets the same gestures through folding wings.
 * Its individual feather tips touch the forest and trace light at the same
 * points as the sylph, so the selected performer does not change how a gesture lands.
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    internal sealed class RedBirdSpirit : IDisposable
    {
        private static readonly Color Scarlet = new Color(.91f,.106f,.145f);
        private static readonly Color Flame = new Color(1,.264f,.205f);
        private static readonly Color Garnet = new Color(.54f,.043f,.099f);
        private static readonly Color Cheek = new Color(.99f,.46f,.342f);
        private static readonly Color Cream = new Color(1,.973f,.87f);
        private static readonly Color Gold = new Color(1,.612f,.188f);
        private static readonly Color Orange = new Color(.90f,.332f,.066f);
        private static readonly Color Outline = new Color(.231f,.046f,.083f);
        private static readonly Color Indigo = new Color(.063f,.188f,.445f);
        private static readonly Color Iris = new Color(.17f,.46f,.82f);
        private static readonly Color Pupil = new Color(.035f,.062f,.154f);
        private static readonly Vector3 FacePivot = new Vector3(0,1.49f,-.045f);
        private static readonly int Tint = Shader.PropertyToID("_Tint");
        private readonly Mesh body, oval, feather, crest, beak;
        private readonly List<Mesh> meshes = new List<Mesh>(5);
        private readonly Material material;
        private readonly MaterialPropertyBlock surface = new MaterialPropertyBlock();
        private CommandBuffer commands;
        private Matrix4x4 root;

        public RedBirdSpirit(Material material)
        {
            this.material = material;
            body = BuildBody();
            oval = BuildOval();
            feather = BuildFeather(false);
            crest = BuildFeather(true);
            beak = BuildBeak();
        }

        public void Draw(SpiritPose pose, Color accent, Vector3[] anchors, Matrix4x4 basis, CommandBuffer target)
        {
            commands = target;
            root = basis * SpiritModel.PoseTransform(pose);
            float breathing = 1 + .018f * pose.breath;
            Part(body,root,Vector3.zero,Quaternion.identity,new Vector3(breathing,1,breathing),Scarlet);
            DrawBib(breathing);
            DrawTail(pose);
            for (int index = 0; index < 2; index++)
            {
                int side = index == 0 ? -1 : 1;
                DrawFoot(pose,side,anchors,index+2);
                DrawWing(side,index == 0 ? pose.left : pose.right,pose,accent,anchors,index);
            }
            DrawFace(pose);
        }

        private void DrawBib(float breathing)
        {
            var center = new Vector3(0,.813f,-.305f*breathing);
            Ball(root,center+new Vector3(0,-.021f,.018f),new Vector3(.357f,.379f,.093f),
                Color.Lerp(Garnet,Cheek,.70f));
            Ball(root,center,new Vector3(.333f,.352f,.108f),Cream);
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 3; i++)
            {
                var from = new Vector3(side*(.391f+i*.042f),.573f+i*.115f,-.160f-i*.015f);
                var to = from + new Vector3(side*.085f,.072f,.023f);
                Plume(root,from,to,Vector3.back,.022f,.012f,Color.Lerp(Scarlet,Flame,.20f-.043f*i));
            }
        }

        private void DrawFace(SpiritPose pose)
        {
            var turn = Quaternion.Euler(-pose.nod*.35f,pose.look*.44f,pose.tilt*.46f);
            Matrix4x4 face = root * Matrix4x4.TRS(FacePivot,turn,Vector3.one) *
                Matrix4x4.Translate(-FacePivot);
            float emotion = Mathf.Clamp01(pose.expression);
            float open = Mathf.Lerp(.071f,1,Mathf.Clamp01(pose.blink));
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = new Vector3(side*.170f,1.504f,-.299f);
                var eyeAngle = Quaternion.Euler(0,-side*10,side*(7+9*emotion));
                float height = (.156f+.018f*emotion)*open;
                Ball(face,eye,new Vector3(.124f,height+.010f,.064f),Outline,eyeAngle);
                Ball(face,eye+Vector3.back*.016f,new Vector3(.113f,height,.058f),Cream,eyeAngle);
                if (open > .12f)
                {
                    Vector3 iris = eye + new Vector3(Mathf.Clamp(pose.look*.001f,-.025f,.025f)+side*.006f,
                        Mathf.Clamp(pose.nod*.00065f,-.021f,.021f),-.070f);
                    Ball(face,iris,new Vector3(.068f,.105f*open,.025f),Indigo,eyeAngle);
                    Ball(face,iris+new Vector3(-.006f,-.008f,-.015f),new Vector3(.052f,.084f*open,.018f),Iris,eyeAngle);
                    Ball(face,iris+new Vector3(.002f,.007f,-.026f),new Vector3(.034f,.066f*open,.014f),Pupil,eyeAngle);
                    Ball(face,iris+new Vector3(-.019f,.034f*open,-.040f),new Vector3(.018f,.023f*open,.009f),Color.white);
                    Ball(face,iris+new Vector3(.023f,-.032f*open,-.034f),Vector3.one*.008f,Color.Lerp(Iris,Cream,.78f));
                }
                var browStart = new Vector3(side*.080f,1.689f+emotion*.033f,-.284f);
                var browEnd = new Vector3(side*.267f,1.697f-emotion*.022f,-.238f);
                Plume(face,browStart,browEnd,Vector3.back,.030f,.025f,Color.Lerp(Garnet,Scarlet,.24f));
                Ball(face,new Vector3(side*.304f,1.294f,-.253f),new Vector3(.104f,.045f,.041f),
                    Garnet,Quaternion.Euler(0,-side*29,side*9));
                Ball(face,new Vector3(side*.303f,1.299f,-.265f),new Vector3(.092f,.037f,.037f),
                    Cheek,Quaternion.Euler(0,-side*29,side*9));
                Ball(face,new Vector3(side*.341f,1.312f,-.288f),new Vector3(.024f,.009f,.005f),
                    Color.Lerp(Cheek,Cream,.42f),Quaternion.Euler(0,-side*29,side*9));
            }

            float gape = emotion*.046f + Mathf.Max(pose.left.light,pose.right.light)*.012f;
            Ball(face,new Vector3(0,1.305f,-.345f),new Vector3(.080f,.046f+gape*.56f,.052f),Outline);
            Part(beak,face,new Vector3(0,1.33f,-.299f),Quaternion.identity,new Vector3(.093f,.076f,.158f),Gold);
            Part(beak,face,new Vector3(0,1.305f-gape,-.296f),Quaternion.Euler(0,0,180),
                new Vector3(.076f,.053f,.133f),Orange);

            float sway = Mathf.Sin(pose.time*2.85f)*.027f + pose.ribbon*.044f;
            Plume(face,new Vector3(.035f,1.79f,-.006f),new Vector3(.239f+sway,2.021f,.062f),
                Vector3.back,.068f,.045f,Color.Lerp(Scarlet,Flame,.21f));
            Plume(face,new Vector3(.014f,1.80f,-.015f),new Vector3(.110f+sway,2.119f,.066f),
                Vector3.back,.087f,.053f,Scarlet);
            Plume(face,new Vector3(-.019f,1.775f,-.023f),new Vector3(-.168f+sway,2.281f,.029f),
                Vector3.back,.108f,.057f,Flame,-1,crest);
            Plume(face,new Vector3(-.043f,1.831f,-.071f),new Vector3(-.105f+sway,2.204f,-.016f),
                Vector3.back,.030f,.014f,Color.Lerp(Flame,Cheek,.34f),-1,crest);
        }

        private void DrawWing(int side, SpiritArm arm, SpiritPose pose, Color accent, Vector3[] anchors, int index)
        {
            SpiritModel.ArmGeometry(side,arm,out Vector3 shoulder,out Vector3 elbow,out Vector3 wrist,
                out Vector3 spread,out Vector3 forward);
            Vector3 contact = SpiritModel.HandContact(wrist,spread,forward,arm);
            Vector3 joint = Vector3.Lerp(shoulder,elbow,.45f)+new Vector3(side*.102f,-.020f,.047f);
            Vector3 outer = elbow+new Vector3(side*.086f,-.091f,.060f);
            Vector3 inner = shoulder + new Vector3(side*.044f,-.020f,.018f);
            Plume(root,inner,Vector3.Lerp(elbow,wrist,.61f)+new Vector3(side*.013f,-.025f,.035f),
                Vector3.back,.122f,.096f,Color.Lerp(Scarlet,Flame,.13f),side);
            Plume(root,joint,Vector3.Lerp(wrist,contact,.48f),Vector3.back,.141f,.082f,
                Color.Lerp(Garnet,Scarlet,.69f),side);
            Plume(root,joint+new Vector3(side*.027f,.041f,-.047f),outer+forward*.106f,
                Vector3.back,.123f,.068f,Scarlet,side);
            for (int i = 0; i < 3; i++)
            {
                float fan = (i+1)/3f;
                var start = Vector3.Lerp(joint,wrist,.22f+fan*.30f)+spread*(side*fan*.055f);
                var tip = outer + forward*(.102f+fan*.090f) + spread*(side*(.110f+fan*.065f));
                tip += Vector3.down*(.083f*fan*(1-arm.point)) + Vector3.forward*(.033f*fan);
                Plume(root,start,tip,Vector3.back,.068f-.013f*fan,.046f-.012f*fan,
                    Color.Lerp(Scarlet,Garnet,.20f+fan*.30f),side);
            }
            var wristTurn = Quaternion.FromToRotation(Vector3.up,forward);
            Ball(root,wrist+forward*.010f,new Vector3(.054f,.078f,.046f),
                Color.Lerp(Scarlet,Flame,.12f),wristTurn);
            Vector3 finger = wrist-forward*.010f;
            for (int i = 0; i < 3; i++)
            {
                float fan = i+1;
                Vector3 baseOfFeather = finger+spread*(side*fan*.019f);
                Vector3 feathersFanned = forward*(.121f-.013f*fan)+spread*(side*fan*(.021f-.009f*arm.curl));
                Vector3 curl = Vector3.Cross(spread,forward).normalized*(arm.curl*.039f);
                Plume(root,baseOfFeather,baseOfFeather+feathersFanned*(1-.30f*arm.point)+curl,
                    Vector3.back,.036f-.0035f*fan,.033f,Color.Lerp(Flame,Scarlet,fan*.23f),side);
            }
            Plume(root,Vector3.Lerp(wrist,contact,.08f),contact,Vector3.back,.040f,
                .036f,Color.Lerp(Scarlet,Flame,.60f),side);
            if (arm.light > .035f)
                Ball(root,contact+new Vector3(0,.024f,-.032f),Vector3.one*(.012f+.016f*arm.light),
                    accent,null,.64f);
            anchors[index] = root.MultiplyPoint3x4(contact);
        }

        private void DrawFoot(SpiritPose pose, int side, Vector3[] anchors, int index)
        {
            SpiritModel.FootGeometry(pose,side,out _,out Vector3 knee,out Vector3 ankle,out Quaternion shoe);
            Vector3 contact = SpiritModel.FootContact(ankle,shoe);
            Plume(root,knee,ankle+Vector3.down*.021f,Vector3.back,.067f,.066f,Orange);
            Ball(root,ankle,new Vector3(.077f,.065f,.096f),Gold,shoe);
            for (int toe = -1; toe <= 1; toe++)
            {
                float spread = .039f*toe;
                Vector3 from = ankle+shoe*new Vector3(spread*.55f,-.007f,-.048f);
                Vector3 end = contact+shoe*new Vector3(spread,0,.018f*Mathf.Abs(toe));
                Plume(root,from,end,Vector3.up,.038f-.006f*Mathf.Abs(toe),.034f,Gold,side);
                Ball(root,Vector3.Lerp(from,end,.84f)+shoe*Vector3.up*.012f,
                    new Vector3(.014f,.012f,.023f),Color.Lerp(Gold,Cream,.41f),shoe);
            }
            anchors[index] = root.MultiplyPoint3x4(contact);
        }

        private void DrawTail(SpiritPose pose)
        {
            float sweep = Mathf.Sin(pose.time*3.1f)*(.018f+.030f*pose.ribbon);
            for (int i = -2; i <= 2; i++)
            {
                var from = new Vector3(i*.071f,.745f+Mathf.Abs(i)*.042f,.282f);
                var to = new Vector3(i*(.135f+.028f*pose.flare)+sweep,.619f+Mathf.Abs(i)*.083f+pose.ribbon*.124f,
                    .606f+.12f*pose.ribbon-Mathf.Abs(i)*.038f);
                Plume(root,from,to,Vector3.up,.098f-.009f*Mathf.Abs(i),.054f,
                    Color.Lerp(Garnet,Scarlet,.38f+.13f*Mathf.Abs(i)),i<0 ? -1 : 1);
            }
        }

        private void Plume(Matrix4x4 basis, Vector3 from, Vector3 tip, Vector3 normal,
            float width, float thick, Color color, float bend = 1, Mesh shape = null)
        {
            Vector3 axis = tip-from;
            if (axis.sqrMagnitude < .00001f) return;
            Vector3 face = Vector3.ProjectOnPlane(normal,axis);
            if (face.sqrMagnitude < .0001f) face = Vector3.ProjectOnPlane(Vector3.right,axis);
            Part(shape ?? feather,basis,from,Quaternion.LookRotation(face,axis),new Vector3(width*bend,axis.magnitude,thick),color);
        }

        private void Ball(Matrix4x4 basis, Vector3 at, Vector3 size, Color color, Quaternion? rotation = null, float glow = 0) =>
            Part(oval,basis,at,rotation ?? Quaternion.identity,size,color,glow);

        private void Part(Mesh mesh, Matrix4x4 basis, Vector3 at, Quaternion rotation, Vector3 size, Color color, float glow = 0)
        {
            surface.SetVector(Tint,new Vector4(color.r,color.g,color.b,glow));
            commands.DrawMesh(mesh,basis*Matrix4x4.TRS(at,rotation,size),material,0,0,surface);
        }

        private Mesh BuildBody()
        {
            var shape = new WorldMesh(meshes);
            shape.Surface(40,26,(column,row) =>
            {
                float angle = column*Mathf.PI*2/40;
                float profile = Mathf.Clamp(row/26f,.0005f,.9995f)*Mathf.PI;
                float v = (1-Mathf.Cos(profile))*.5f;
                float plump = Mathf.Pow(Mathf.Sin(profile),.74f);
                float width = Mathf.Lerp(.680f,.437f,v) + .055f*Mathf.Sin(v*2*Mathf.PI)*Mathf.Sin(profile);
                float depth = Mathf.Lerp(.369f,.330f,v);
                return new Vector3(Mathf.Cos(angle)*plump*width,.332f+v*1.550f,
                    Mathf.Sin(angle)*plump*depth+.021f*(1-v));
            },point =>
            {
                float light = Mathf.Lerp(.76f,1.055f,Mathf.InverseLerp(.34f,1.84f,point.y));
                float fleck = Mathf.Sin(49*point.x+19*point.y)*Mathf.Sin(27*point.z-31*point.y)*.018f;
                return WorldMesh.Shade(Color.white,light+fleck);
            },null);
            return shape.Finish("Red bird round body");
        }

        private Mesh BuildOval()
        {
            var shape = new WorldMesh(meshes);
            shape.Surface(24,17,(column,row) =>
            {
                float theta = column*Mathf.PI*2/24;
                float phi = Mathf.Clamp(row/17f,.0005f,.9995f)*Mathf.PI;
                return new Vector3(Mathf.Sin(phi)*Mathf.Cos(theta),-Mathf.Cos(phi),Mathf.Sin(phi)*Mathf.Sin(theta));
            },_ => Color.white,null);
            return shape.Finish("Red bird small volumes");
        }

        private Mesh BuildFeather(bool flame)
        {
            var shape = new WorldMesh(meshes);
            shape.Surface(16,17,(column,row) =>
            {
                float theta = column*Mathf.PI*2/16;
                float t = Mathf.Clamp(row/17f,.0005f,.9995f);
                float envelope = Mathf.Pow(Mathf.Sin(Mathf.PI*t),.80f)*(1.12f-.48f*t);
                float bends = Mathf.Sin(t*Mathf.PI)*.35f*t;
                if (flame) bends += Mathf.Sin(t*Mathf.PI)*.65f-Mathf.Sin(t*2*Mathf.PI)*.43f;
                return new Vector3(Mathf.Cos(theta)*envelope+bends,t,
                    Mathf.Sin(theta)*envelope + Mathf.Sin(t*Mathf.PI)*.24f);
            },point => WorldMesh.Shade(Color.white,.79f+.24f*point.y+
                .09f*Mathf.Cos(point.x*6.2f)*Mathf.Sin(point.y*11f)),null);
            return shape.Finish(flame ? "Red bird flame crest" : "Red bird cambered feather");
        }

        private Mesh BuildBeak()
        {
            var shape = new WorldMesh(meshes);
            shape.Surface(22,16,(column,row) =>
            {
                float theta = column*Mathf.PI*2/22;
                float t = Mathf.Clamp(row/16f,.0005f,.9995f);
                float width = Mathf.Pow(Mathf.Sin(t*Mathf.PI),.59f)*(1.02f-.48f*t);
                return new Vector3(width*Mathf.Cos(theta),-.32f*t*t+width*Mathf.Sin(theta),-t);
            },point => WorldMesh.Shade(Color.white,.82f+.21f*point.y),null);
            return shape.Finish("Red bird carved bill");
        }

        public void Dispose()
        {
            foreach (Mesh mesh in meshes) UnityEngine.Object.Destroy(mesh);
        }
    }
}
