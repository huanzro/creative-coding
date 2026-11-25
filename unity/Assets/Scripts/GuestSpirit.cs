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
        private readonly Matrix4x4 normalization;
        private readonly MaterialPropertyBlock paint = new MaterialPropertyBlock();

        private readonly struct Joint
        {
            private readonly Transform bone;
            private readonly Quaternion bindRotation;
            private readonly Vector3 axis;
            public Vector3 Position => bone.position;

            public Joint(Transform bone, Transform child = null)
            {
                this.bone = bone;
                bindRotation = bone.localRotation;
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
            normalization = Matrix4x4.Scale(Vector3.one * (2.10f / Mathf.Max(.1f, bounds.size.y))) *
                Matrix4x4.Translate(-ground);
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
                shoulders[i].Aim(new Vector3(side * arm.upper.x, arm.upper.y, arm.upper.z));
                var lower = new Vector3(side * arm.lower.x, arm.lower.y, arm.lower.z);
                elbows[i].Aim(lower);
                wrists[i].Rotate(Quaternion.Euler(arm.wristFlex + 18 * arm.curl, 0, side * 8 * arm.point));

                float step = (i == 0 ? pose.step : -pose.step);
                float trailing = pose.extension * (i == 0 ? .76f : 1);
                float tuck = Mathf.Clamp01(pose.tuck);
                hips[i].Aim(new Vector3(side * (.08f + .16f * Mathf.Abs(step)), -.9f + .31f * tuck,
                    -.15f + .48f * step + .30f * trailing - .58f * tuck - .16f * pose.settle));
                knees[i].Aim(new Vector3(side * .056f, -.87f + .51f * tuck,
                    .18f + .41f * step + .42f * trailing + .20f * tuck));
                feet[i].Aim(new Vector3(side * .13f, -.16f - .71f * trailing - .49f * tuck +
                    .35f * pose.settle, -.97f));
            }

            for (int i = 0; i < renderers.Length; i++) renderers[i].BakeMesh(meshes[i]);
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
                Vector3 handDirection = (hands[i].position - elbows[i].Position).normalized;
                anchors[i] = model.MultiplyPoint3x4(hands[i].position + handDirection * .10f);
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
