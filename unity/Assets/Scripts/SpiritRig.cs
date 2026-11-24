using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    internal sealed class SpiritRig : IDisposable
    {
        public const float WidthScale = .9f, HeightScale = 1.2f;
        private static readonly Color[] Accent = {
            new Color(.95f,.66f,.29f), new Color(.39f,.79f,.93f), new Color(.75f,.59f,1f)
        };
        private static readonly int SceneLight = Shader.PropertyToID("_SceneLight");
        private static readonly int ContactLight = Shader.PropertyToID("_ContactLight");
        private readonly Texture2D fallback;
        private readonly Material material;
        private readonly RenderTexture target;
        private readonly CommandBuffer commands;
        private readonly SpiritModel model;
        private readonly IllustratedSpirit painted;
        private readonly Camera portraitCamera;
        private readonly float[] dance = new float[SpiritDance.TrackCount];
        private readonly Vector3[] anchors = new Vector3[4];
        private readonly List<TracePoint>[] traces = new List<TracePoint>[4];
        private float lastSample = float.NegativeInfinity, lastTrail = float.NegativeInfinity;
        private float tracked, closed, voice;
        private SpiritFrame previous;
        private bool rendered;

        public bool Articulated => rendered;
        public Texture Texture => rendered ? target : (Texture)fallback;

        internal readonly struct TracePoint
        {
            public readonly Vector2 Position;
            public readonly float Time, Energy;
            public TracePoint(Vector2 position, float time, float energy)
            { Position = position; Time = time; Energy = energy; }
        }

        public SpiritRig(Texture2D fallback)
        {
            this.fallback = fallback;
            var shader = Resources.Load<Shader>("SpiritPortrait");
            if (shader == null || !shader.isSupported) return;
            var descriptor = new RenderTextureDescriptor(672, 896, RenderTextureFormat.ARGB32, 24) {
                msaaSamples = 2
            };
            descriptor.msaaSamples = Mathf.Max(1, SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor));
            target = new RenderTexture(descriptor) {
                name = "Echoes shared 3D character", filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            commands = new CommandBuffer { name = "Echoes 3D character" };
            material = new Material(shader);
            model = new SpiritModel(material, commands);
            var paintedShader = Resources.Load<Shader>("IllustratedSpirit");
            if (fallback != null && paintedShader != null && paintedShader.isSupported)
                painted = new IllustratedSpirit(fallback, paintedShader);
            var cameraObject = new GameObject("Echoes portrait camera") { hideFlags = HideFlags.HideAndDontSave };
            cameraObject.transform.position = new Vector3(0, 1.16f, -5.6f);
            portraitCamera = cameraObject.AddComponent<Camera>();
            portraitCamera.enabled = false;
            portraitCamera.cullingMask = 0;
            portraitCamera.clearFlags = CameraClearFlags.SolidColor;
            portraitCamera.backgroundColor = Color.clear;
            portraitCamera.allowHDR = false;
            portraitCamera.allowMSAA = descriptor.msaaSamples > 1;
            portraitCamera.useOcclusionCulling = false;
            portraitCamera.renderingPath = RenderingPath.Forward;
            portraitCamera.nearClipPlane = .1f;
            portraitCamera.farClipPlane = 25;
            portraitCamera.fieldOfView = 30.5f;
            portraitCamera.aspect = .75f;
            portraitCamera.targetTexture = target;
            portraitCamera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, commands);
            for (int i = 0; i < traces.Length; i++) traces[i] = new List<TracePoint>(18);
        }

        public IReadOnlyList<TracePoint> Trace(int index) => traces[index];
        public Vector2 Landmark(int index)
        {
            Vector3 viewport = portraitCamera.WorldToViewportPoint(anchors[index]);
            return new Vector2((viewport.x - .5f) * WidthScale, (.5f - viewport.y) * HeightScale);
        }

        public void Render(SpiritFrame frame)
        {
            if (model == null) return;
            bool opening = frame.openingTime >= 0;
            float time = opening ? frame.openingTime : frame.motionTime;
            if (rendered && target.IsCreated() && time == lastSample && frame.hand == previous.hand &&
                frame.touchAge == previous.touchAge && frame.touch == previous.touch &&
                frame.turnAge == previous.turnAge && frame.releaseAge == previous.releaseAge &&
                frame.chapter == previous.chapter && frame.openingTime == previous.openingTime &&
                frame.cast == previous.cast && frame.fireflyPulse == previous.fireflyPulse) return;
            if (!target.IsCreated() && !target.Create()) return;

            float elapsed = time - lastSample;
            float blend = elapsed <= 0 || elapsed > .4f ? 1 : 1 - Mathf.Exp(-elapsed / .145f);
            tracked = Mathf.Lerp(tracked, frame.hand == Hand.Missing ? 0 : 1, blend);
            closed = Mathf.Lerp(closed, frame.hand == Hand.Fist ? 1 : 0, blend);
            voice = Mathf.Lerp(voice, frame.voice, blend);
            lastSample = time;
            bool changedCast = previous.cast != frame.cast;
            previous = frame;
            SpiritPose pose = SpiritMotion.Sample(frame, tracked, closed, voice, dance);
            commands.Clear();
            Color tint = Accent[Mathf.Clamp(frame.chapter, 0, Accent.Length - 1)];
            float contact = frame.fireflyPulse * .80f;
            if (frame.fireflyResponse == FireflyKind.Scarlet && contact > 0)
                tint = Color.Lerp(tint, new Color(1,.30f,.19f), contact);
            if (opening)
            {
                var shot = OpeningStoryboard.Sample(time);
                tint = Color.Lerp(tint, new Color(.52f,.80f,1), Mathf.Max(shot.Ruin, shot.Sky) * .75f);
                float scarlet = OpeningStoryboard.Contact(time, true);
                float warm = OpeningStoryboard.Contact(time, false);
                tint = Color.Lerp(tint, new Color(1,.73f,.34f), warm * .60f);
                tint = Color.Lerp(tint, new Color(1,.33f,.26f), scarlet * .90f);
                contact = Mathf.Max(scarlet, warm) * .86f;
            }
            material.SetVector(SceneLight, new Vector4(tint.r, tint.g, tint.b, opening ? .31f : .17f));
            if (frame.cast == 1 && painted != null) painted.Draw(pose, tint, contact, anchors, commands);
            else model.Draw(pose, tint, anchors);
            Vector3 light = anchors[1] + new Vector3(.08f, .045f, -.135f);
            material.SetVector(ContactLight, new Vector4(light.x, light.y, light.z, contact));
            portraitCamera.Render();
            rendered = true;
            if (changedCast)
            {
                foreach (var trail in traces) trail.Clear();
                lastTrail = float.NegativeInfinity;
            }
            if (!opening)
                UpdateTrails(time, frame.glide > .10f || frame.turnAge >= 0 || frame.touchAge >= 0 || frame.releaseAge >= 0);
        }

        private void UpdateTrails(float time, bool active)
        {
            float elapsed = time - lastTrail;
            if (elapsed >= 0 && elapsed < 1f / 32) return;
            for (int i = 0; i < traces.Length; i++)
            {
                var trail = traces[i];
                if (elapsed > .25f || elapsed < 0) trail.Clear();
                Vector2 point = Landmark(i);
                float speed = trail.Count == 0 ? 0 : Vector2.Distance(point, trail[trail.Count - 1].Position) / elapsed;
                float energy = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.04f, .83f, speed)) * (active ? 1 : .35f);
                trail.Add(new TracePoint(point, time, energy));
                while (trail.Count > 0 && time - trail[0].Time > .48f) trail.RemoveAt(0);
            }
            lastTrail = time;
        }

        public void Dispose()
        {
            if (portraitCamera != null)
            {
                portraitCamera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, commands);
                portraitCamera.targetTexture = null;
                UnityEngine.Object.Destroy(portraitCamera.gameObject);
            }
            model?.Dispose();
            painted?.Dispose();
            commands?.Dispose();
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            if (material != null) UnityEngine.Object.Destroy(material);
        }
    }
}
