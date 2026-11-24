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
        private static readonly int ViewProjection = Shader.PropertyToID("_AvatarViewProjection");
        private readonly Texture2D fallback;
        private readonly Material material;
        private readonly RenderTexture target;
        private readonly CommandBuffer commands;
        private readonly SpiritModel model;
        private readonly Matrix4x4 projection, view;
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
            target = new RenderTexture(576, 768, 24, RenderTextureFormat.ARGB32) {
                name = "Echoes shared 3D character", filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            commands = new CommandBuffer { name = "Echoes 3D character" };
            material = new Material(shader);
            model = new SpiritModel(material, commands);
            view = Matrix4x4.Scale(new Vector3(1, 1, -1)) *
                Matrix4x4.Translate(new Vector3(0, -1.16f, 5.6f));
            projection = Matrix4x4.Perspective(28f, .75f, .1f, 25f);
            material.SetMatrix(ViewProjection, GL.GetGPUProjectionMatrix(projection, true) * view);
            for (int i = 0; i < traces.Length; i++) traces[i] = new List<TracePoint>(18);
        }

        public IReadOnlyList<TracePoint> Trace(int index) => traces[index];

        public void Render(SpiritFrame frame)
        {
            if (model == null) return;
            if (rendered && target.IsCreated() && frame.motionTime == lastSample && frame.hand == previous.hand &&
                frame.touchAge == previous.touchAge && frame.touch == previous.touch &&
                frame.turnAge == previous.turnAge && frame.releaseAge == previous.releaseAge &&
                frame.chapter == previous.chapter) return;
            if (!target.IsCreated() && !target.Create()) return;

            float elapsed = frame.motionTime - lastSample;
            float blend = elapsed <= 0 || elapsed > .4f ? 1 : 1 - Mathf.Exp(-elapsed / .11f);
            tracked = Mathf.Lerp(tracked, frame.hand == Hand.Missing ? 0 : 1, blend);
            closed = Mathf.Lerp(closed, frame.hand == Hand.Fist ? 1 : 0, blend);
            voice = Mathf.Lerp(voice, frame.voice, blend);
            lastSample = frame.motionTime;
            previous = frame;
            SpiritPose pose = SpiritMotion.Sample(frame, tracked, closed, voice, dance);
            commands.Clear();
            commands.SetRenderTarget(target);
            commands.DisableScissorRect();
            commands.SetViewport(new Rect(0, 0, target.width, target.height));
            commands.ClearRenderTarget(true, true, Color.clear);
            model.Draw(pose, Accent[Mathf.Clamp(frame.chapter, 0, Accent.Length - 1)], anchors);
            RenderTexture active = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                Graphics.ExecuteCommandBuffer(commands);
            }
            finally { RenderTexture.active = active; }
            rendered = true;
            UpdateTrails(frame.motionTime, frame.turnAge >= 0 || frame.touchAge >= 0 || frame.releaseAge >= 0);
        }

        private void UpdateTrails(float time, bool active)
        {
            float elapsed = time - lastTrail;
            if (elapsed >= 0 && elapsed < 1f / 32) return;
            Matrix4x4 logicalProjection = projection * view;
            for (int i = 0; i < traces.Length; i++)
            {
                var trail = traces[i];
                if (elapsed > .25f || elapsed < 0) trail.Clear();
                Vector4 viewport = logicalProjection * new Vector4(anchors[i].x, anchors[i].y, anchors[i].z, 1);
                var point = new Vector2(viewport.x * WidthScale, -viewport.y * HeightScale) / (2 * viewport.w);
                float speed = trail.Count == 0 ? 0 : Vector2.Distance(point, trail[trail.Count - 1].Position) / elapsed;
                float energy = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.04f, .83f, speed)) * (active ? 1 : .35f);
                trail.Add(new TracePoint(point, time, energy));
                while (trail.Count > 0 && time - trail[0].Time > .48f) trail.RemoveAt(0);
            }
            lastTrail = time;
        }

        public void Dispose()
        {
            model?.Dispose();
            commands?.Dispose();
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            if (material != null) UnityEngine.Object.Destroy(material);
        }
    }
}
