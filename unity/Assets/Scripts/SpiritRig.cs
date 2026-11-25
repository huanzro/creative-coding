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
        private static readonly int StageKey = Shader.PropertyToID("_StageKey");
        private static readonly int StageCoverage = Shader.PropertyToID("_StageCoverage");
        private readonly Texture2D fallback;
        private readonly Material material;
        private readonly RenderTexture target;
        private readonly CommandBuffer commands;
        private readonly SpiritModel model;
        private ForestPredatorRig predator;
        private readonly GuestSpirit[] guests = new GuestSpirit[2];
        private readonly bool[] guestsLoaded = new bool[2];
        private GuestSpirit guest;
        private readonly Camera portraitCamera;
        private readonly float[] dance = new float[SpiritDance.TrackCount];
        private readonly Vector3[] anchors = new Vector3[4];
        private readonly Vector3[] sceneAnchors = new Vector3[4];
        private readonly List<TracePoint>[] traces = new List<TracePoint>[4];
        private float lastSample = float.NegativeInfinity, lastTrail = float.NegativeInfinity;
        private float tracked, closed, voice;
        private SpiritFrame previous;
        private SpiritPose pose;
        private Color poseTint;
        private float poseContact;
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
            model = new SpiritModel(material);
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
                frame.forest == previous.forest && frame.forestTrap == previous.forestTrap &&
                frame.forestSnareAge == previous.forestSnareAge && frame.forestFall == previous.forestFall &&
                frame.forestDescent == previous.forestDescent &&
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
            pose = SpiritMotion.Sample(frame, tracked, closed, voice, dance);
            guest = Guest(frame.cast);
            guest?.Pose(pose);
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
            poseTint = tint;
            poseContact = contact;
            ForestPredatorShot encounter=ForestPredatorShot.Read(frame);
            var center=new Vector3(0,1.16f,0);
            Matrix4x4 portraitPose = encounter.Prey(center);
            if (frame.forestMaze && !encounter.Visible)
            {
                portraitPose = Matrix4x4.Translate(center) * Matrix4x4.Rotate(Quaternion.Euler(0,frame.forestMazeHeading,0)) *
                    Matrix4x4.Translate(-center) * portraitPose;
            }
            RenderModel(portraitPose, Vector3.zero, anchors, commands, opening ? .31f : .17f);
            if(encounter.Visible)
            {
                if(predator==null) predator=new ForestPredatorRig();
                predator.DrawPortrait(encounter,center,commands);
            }
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

        private void RenderModel(Matrix4x4 basis, Vector3 key, Vector3[] joints, CommandBuffer buffer, float ambient, bool maskBackground = false)
        {
            material.SetVector(StageKey, new Vector4(key.x,key.y,key.z,key.sqrMagnitude>.1f?1:0));
            material.SetVector(SceneLight, new Vector4(poseTint.r,poseTint.g,poseTint.b,ambient));
            material.SetFloat(StageCoverage,maskBackground ? 0 : 1);
            if (guest != null) guest.Draw(pose, joints, basis, buffer, material);
            else model.Draw(pose,poseTint,joints,previous.cast == 1,basis,buffer);
            Vector3 point = joints[1] + basis.MultiplyVector(new Vector3(.08f,.045f,-.135f));
            material.SetVector(ContactLight,new Vector4(point.x,point.y,point.z,poseContact));
        }

        private GuestSpirit Guest(int cast)
        {
            int index = cast - 2;
            if (index < 0 || index >= guests.Length) return null;
            if (!guestsLoaded[index])
            {
                guests[index] = index == 0
                    ? GuestSpirit.Load("RogueHooded", "rogue_texture", new Color(.91f, 1f, .94f))
                    : GuestSpirit.Load("Mage", "mage_texture", new Color(.99f, .97f, 1f));
                guestsLoaded[index] = true;
            }
            return guests[index];
        }

        public void DrawInWorld(Camera camera, Vector2 screen, float bodyPixels, float depth, Vector3 key,
            CommandBuffer buffer, bool upright = false, float? facing = null, bool maskBackground = false)
        {
            if (!rendered) return;
            Vector3 point = camera.ViewportToWorldPoint(new Vector3(screen.x,1-screen.y,depth));
            float scale = bodyPixels * HeightScale / 720 *
                (depth*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad))/(5.6f*Mathf.Tan(15.25f*Mathf.Deg2Rad));
            Quaternion orientation = facing.HasValue ? Quaternion.Euler(0, facing.Value, 0) :
                upright ? Quaternion.Euler(0, camera.transform.eulerAngles.y, 0) : camera.transform.rotation;
            Matrix4x4 transform = Matrix4x4.TRS(point,orientation,Vector3.one*scale)
                * Matrix4x4.Translate(new Vector3(0,-1.16f,0))
                * ForestPredatorShot.Read(previous).Prey(new Vector3(0,1.16f,0));
            RenderModel(transform,key,sceneAnchors,buffer,.20f,maskBackground);
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
            predator?.Dispose();
            foreach (var guestModel in guests) guestModel?.Dispose();
            commands?.Dispose();
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            if (material != null) UnityEngine.Object.Destroy(material);
        }
    }
}
