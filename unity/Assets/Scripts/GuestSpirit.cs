using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Echoes
{
    // Retargets the same projection poses used by the original sylph to KayKit's bind skeleton.
    // Bake into camera-local draw commands so the model never leaks into another camera or display.
    internal sealed class GuestSpirit : IDisposable
    {
        private static readonly int Atlas = Shader.PropertyToID("_PaintedAtlas");
        private static readonly int UseAtlas = Shader.PropertyToID("_UsePaintedAtlas");
        private static readonly int Tint = Shader.PropertyToID("_Tint");
        private readonly GameObject instance;
        private readonly SkinnedMeshRenderer[] renderers;
        private readonly Mesh[] meshes;
        private readonly MeshFilter cape;
        private readonly Joint capeJoint;
        private readonly Joint head;
        private readonly Joint[] shoulders, elbows, wrists, hips, knees, feet;
        private readonly Transform[] toes, hands;
        private readonly Matrix4x4 normalization, denormalization;
        private readonly float handReach;
        private readonly Vector3[] handContacts=new Vector3[2];
        private readonly MaterialPropertyBlock paint = new MaterialPropertyBlock();

        private readonly struct Joint
        {
            private readonly Transform bone;
            private readonly Quaternion bindRotation;
            private readonly Vector3 axis;
            public Vector3 Position => bone.position;
            public float Length { get; }

            public Joint(Transform bone, Transform child = null)
            {
                this.bone = bone;
                bindRotation = bone.localRotation;
                Length = child == null ? 0 : Vector3.Distance(bone.position, child.position);
                axis = child == null ? Vector3.up :
                    bone.InverseTransformDirection((child.position - bone.position).normalized);
            }

            public void Aim(Vector3 direction)
            {
                bone.localRotation = bindRotation;
                Quaternion turn = Quaternion.FromToRotation(bone.TransformDirection(axis), direction.normalized);
                bone.rotation = turn * bone.rotation;
            }

            public void Rotate(Quaternion turn)
            {
                bone.localRotation = bindRotation;
                bone.rotation = turn * bone.rotation;
            }
        }

        private GuestSpirit(GameObject instance, Texture texture, Dictionary<string, Transform> bones,
            SkinnedMeshRenderer[] renderers, MeshFilter cape, Color dye)
        {
            this.instance = instance;
            this.renderers = renderers;
            this.cape = cape;
            Vector3 facing = (bones["toes.l"].position - bones["foot.l"].position) +
                (bones["toes.r"].position - bones["foot.r"].position);
            float heading = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            instance.transform.rotation = Quaternion.AngleAxis(180 - heading, Vector3.up) *
                instance.transform.rotation;

            head = new Joint(bones["head"]);
            if (cape != null) capeJoint = new Joint(cape.transform);
            shoulders = new Joint[2];
            elbows = new Joint[2];
            wrists = new Joint[2];
            hips = new Joint[2];
            knees = new Joint[2];
            feet = new Joint[2];
            toes = new Transform[2];
            hands = new Transform[2];
            bool leftIsNegative = bones["upperarm.l"].position.x < bones["upperarm.r"].position.x;
            for (int i = 0; i < 2; i++)
            {
                string side = (i == 0) == leftIsNegative ? ".l" : ".r";
                Transform upperArm = bones["upperarm" + side], lowerArm = bones["lowerarm" + side];
                Transform wrist = bones["hand" + side], upperLeg = bones["upperleg" + side];
                Transform lowerLeg = bones["lowerleg" + side], foot = bones["foot" + side];
                toes[i] = bones["toes" + side];
                hands[i] = wrist;
                shoulders[i] = new Joint(upperArm, lowerArm);
                elbows[i] = new Joint(lowerArm, wrist);
                wrists[i] = new Joint(wrist);
                hips[i] = new Joint(upperLeg, lowerLeg);
                knees[i] = new Joint(lowerLeg, foot);
                feet[i] = new Joint(foot, toes[i]);
            }

            Bounds bounds = renderers[0].bounds;
            if (cape != null) bounds.Encapsulate(cape.GetComponent<Renderer>().bounds);
            meshes = new Mesh[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
                renderers[i].enabled = false;
                meshes[i] = new Mesh { name = "Echoes guest pose", hideFlags = HideFlags.HideAndDontSave };
                meshes[i].MarkDynamic();
            }

            var ground = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            // The imported bind soles sit below the lowest toes reached by the retargeted pose.
            float scale = 2.24f / Mathf.Max(.1f, bounds.size.y);
            normalization = Matrix4x4.TRS(new Vector3(0,-.10f,0), Quaternion.identity, Vector3.one * scale) *
                Matrix4x4.Translate(-ground);
            denormalization = normalization.inverse;
            handReach = .10f / scale;
            paint.SetTexture(Atlas, texture);
            paint.SetFloat(UseAtlas, 1);
            paint.SetVector(Tint, new Vector4(dye.r, dye.g, dye.b, 0));
        }

        public static GuestSpirit Load(string asset, string texture, Color dye)
        {
            string path = "GuestSpirits/";
            var prefab = Resources.Load<GameObject>(path + asset);
            var atlas = Resources.Load<Texture2D>(path + texture);
            if (prefab == null || atlas == null) return null;

            var instance = Object.Instantiate(prefab);
            instance.name = "Echoes guest spirit " + asset;
            instance.hideFlags = HideFlags.HideAndDontSave;
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
            MeshFilter cape = null;
            foreach (var rigidPart in instance.GetComponentsInChildren<MeshRenderer>())
            {
                rigidPart.enabled = false;
                if (rigidPart.name.EndsWith("_Cape", StringComparison.Ordinal))
                    cape = rigidPart.GetComponent<MeshFilter>();
            }
            var bones = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in instance.GetComponentsInChildren<Transform>())
                bones[part.name] = part;

            if (renderers.Length != 0 && bones.ContainsKey("head") && HasLimbs(bones, ".l") && HasLimbs(bones, ".r"))
                return new GuestSpirit(instance, atlas, bones, renderers, cape, dye);

            Debug.LogWarning("Echoes guest spirit could not bind the model " + asset);
            Object.Destroy(instance);
            return null;
        }

        private static bool HasLimbs(Dictionary<string, Transform> bones, string side) =>
            bones.ContainsKey("upperarm" + side) && bones.ContainsKey("lowerarm" + side) &&
            bones.ContainsKey("hand" + side) && bones.ContainsKey("upperleg" + side) &&
            bones.ContainsKey("lowerleg" + side) && bones.ContainsKey("foot" + side) &&
            bones.ContainsKey("toes" + side);

        public void Pose(SpiritPose pose)
        {
            head.Rotate(Quaternion.Euler(pose.nod, pose.look, pose.tilt));
            if (cape != null)
                capeJoint.Rotate(Quaternion.Euler(-24 * pose.ribbon + 3 * pose.breath, 0, -.17f * pose.lean));
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                SpiritArm arm = i == 0 ? pose.left : pose.right;
                SpiritModel.ArmGeometry(side, arm, out _, out Vector3 elbow, out Vector3 wrist,
                    out Vector3 spread, out Vector3 forward);
                Vector3 contact = denormalization.MultiplyPoint3x4(SpiritModel.HandContact(wrist, spread, forward, arm));
                Reach(shoulders[i], elbows[i], contact - forward * handReach, denormalization.MultiplyPoint3x4(elbow));
                wrists[i].Rotate(Quaternion.Euler(arm.wristFlex + 18 * arm.curl, 0, side * 8 * arm.point));
                handContacts[i] = hands[i].position + forward * handReach;

                SpiritModel.FootGeometry(pose, side, out _, out Vector3 knee, out Vector3 ankle, out Quaternion shoe);
                Vector3 toe = denormalization.MultiplyPoint3x4(SpiritModel.FootContact(ankle, shoe));
                Vector3 shoeDirection = shoe * new Vector3(0,-.025f,-.145f).normalized;
                Reach(hips[i], knees[i], toe - shoeDirection * feet[i].Length, denormalization.MultiplyPoint3x4(knee));
                feet[i].Aim(shoeDirection);
            }

            for (int i = 0; i < renderers.Length; i++) renderers[i].BakeMesh(meshes[i]);
        }

        // Follow the shared tip and toe targets while preserving the imported bone proportions.
        private static void Reach(Joint upper, Joint lower, Vector3 target, Vector3 bendToward)
        {
            Vector3 origin = upper.Position;
            Vector3 delta = target - origin;
            float distance = delta.magnitude;
            if (distance < .0001f || upper.Length < .0001f || lower.Length < .0001f) return;
            Vector3 axis = delta / distance;
            float bendDistance = Mathf.Clamp(distance, Mathf.Abs(upper.Length - lower.Length) + .0001f,
                upper.Length + lower.Length - .0001f);
            Vector3 bend = Vector3.ProjectOnPlane(bendToward - origin, axis);
            if (bend.sqrMagnitude < .000001f)
                bend = Vector3.Cross(axis, Mathf.Abs(axis.y) < .91f ? Vector3.up : Vector3.forward);
            float along = (upper.Length * upper.Length - lower.Length * lower.Length + bendDistance * bendDistance) /
                (2 * bendDistance);
            float away = Mathf.Sqrt(Mathf.Max(0, upper.Length * upper.Length - along * along));
            upper.Aim(axis * along + bend.normalized * away);
            lower.Aim(origin + axis * bendDistance - lower.Position);
        }

        public void Draw(SpiritPose pose, Vector3[] anchors, Matrix4x4 basis, CommandBuffer target, Material material)
        {
            Matrix4x4 model = basis * SpiritModel.PoseTransform(pose) * normalization;
            if (cape != null && cape.sharedMesh != null)
                DrawMesh(cape.sharedMesh, model * cape.transform.localToWorldMatrix, target, material);
            for (int i = 0; i < renderers.Length; i++)
            {
                Matrix4x4 transform = model * renderers[i].transform.localToWorldMatrix;
                DrawMesh(meshes[i], transform, target, material);
            }
            for (int i = 0; i < 2; i++)
            {
                anchors[i] = model.MultiplyPoint3x4(handContacts[i]);
                anchors[i + 2] = model.MultiplyPoint3x4(toes[i].position);
            }
        }

        private void DrawMesh(Mesh mesh, Matrix4x4 transform, CommandBuffer target, Material material)
        {
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                target.DrawMesh(mesh, transform, material, submesh, 0, paint);
        }

        public void Dispose()
        {
            foreach (var mesh in meshes) Object.Destroy(mesh);
            Object.Destroy(instance);
        }
    }
}
