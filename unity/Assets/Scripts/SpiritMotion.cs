using UnityEngine;

namespace Echoes
{
    internal struct SpiritArm
    {
        public Vector3 upper, lower;
        public float curl, light;

        public SpiritArm(Vector3 upper, Vector3 lower, float curl = 0, float light = 0)
        {
            this.upper = upper;
            this.lower = lower;
            this.curl = curl;
            this.light = light;
        }

        public static SpiritArm Blend(SpiritArm a, SpiritArm b, float weight) =>
            new SpiritArm(Vector3.Lerp(a.upper, b.upper, weight), Vector3.Lerp(a.lower, b.lower, weight),
                Mathf.Lerp(a.curl, b.curl, weight), Mathf.Lerp(a.light, b.light, weight));
    }

    internal struct SpiritPose
    {
        public SpiritArm left, right;
        public float time, shift, lift, pitch, yaw, lean, nod, look, tilt, flare, ribbon, step, blink, glow, pivot;
    }

    internal static class SpiritMotion
    {
        private static readonly SpiritArm Reach = new SpiritArm(new Vector3(.82f, -.33f, -.45f),
            new Vector3(.63f, .20f, -.56f), 0, .30f);
        private static readonly SpiritArm Hold = new SpiritArm(new Vector3(.45f, -.57f, -.38f),
            new Vector3(-.58f, .47f, -.54f), .92f, .69f);
        private static readonly SpiritArm Measure = new SpiritArm(new Vector3(.78f, -.20f, -.37f),
            new Vector3(-.12f, .87f, -.64f), .3f, .62f);
        private static readonly SpiritArm Offer = new SpiritArm(new Vector3(.24f, -.46f, -.87f),
            new Vector3(.30f, .58f, -.84f), .08f, .87f);
        private static readonly SpiritArm High = new SpiritArm(new Vector3(.71f, .47f, -.39f),
            new Vector3(.28f, .88f, -.25f), .16f, .84f);
        private static readonly SpiritArm Rest = new SpiritArm(new Vector3(.42f, -.88f, -.12f),
            new Vector3(.35f, -.72f, -.26f), .42f);
        private static readonly SpiritArm Sow = new SpiritArm(new Vector3(.48f, -.63f, -.64f),
            new Vector3(.31f, -.46f, -.72f), .10f, .91f);
        private static readonly SpiritArm Listen = new SpiritArm(new Vector3(.89f, -.20f, -.29f),
            new Vector3(-.64f, .94f, -.35f), .18f, .36f);
        private static readonly SpiritArm Push = new SpiritArm(new Vector3(.50f, -.10f, -.85f),
            new Vector3(.22f, .12f, -.92f), .06f, .64f);

        public static SpiritPose Sample(SpiritFrame frame, float tracked, float closed, float voice, float[] dance)
        {
            SpiritDance.Sample(frame.phase, dance);
            float time = frame.motionTime;
            float draw = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.105f, .30f, voice));
            float flight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.37f, .72f, voice));
            float touch = Envelope(frame.touchAge, .14f, .27f, .95f);
            float cast = Envelope(frame.releaseAge, .12f, .20f, .70f);
            float fly = flight * (1 - closed) * (1 - touch);
            float turn = frame.turnAge < 0 ? 0 : Mathf.Clamp01(frame.turnAge / 1.25f);
            float spinBody = Mathf.Sin(turn * Mathf.PI);
            float trailing = frame.turnAge < 0 ? 0 : Mathf.Clamp01((frame.turnAge - .095f) / 1.23f);
            float idle = Mathf.Sin(time * 2.15f);
            var pose = new SpiritPose {
                time = time, yaw = -8 + dance[3] * 14, lean = dance[2] * 21, step = (dance[12] - dance[16]) * .004f,
                lift = .048f + idle * .022f, blink = 1 - Envelope(Mathf.Repeat(time + .4f, 4.3f), .055f, .075f, .15f) * .89f,
                look = dance[4] * 24, shift = dance[0] * .009f, glow = frame.light * .78f + draw * .22f
            };

            // Each gesture uses a different task vocabulary in each chapter.
            switch (frame.chapter)
            {
                case 1:
                    pose.left = SpiritArm.Blend(Rest, Measure, tracked);
                    pose.right = SpiritArm.Blend(Rest, Reach, tracked);
                    pose.left = SpiritArm.Blend(pose.left, Hold, closed);
                    pose.right = SpiritArm.Blend(pose.right, Measure, closed);
                    pose.left = SpiritArm.Blend(pose.left, Listen, draw * (1 - closed));
                    pose.right = SpiritArm.Blend(pose.right, Offer, cast);
                    pose.nod = 7 * closed - 10 * draw;
                    pose.look += 18 * draw;
                    pose.pitch += 9 * flight;
                    pose.left = SpiritArm.Blend(pose.left, Push, fly);
                    pose.right = SpiritArm.Blend(pose.right, Push, fly * .78f);
                    ApplyTouch(ref pose, frame.touch, touch, Measure, Offer, -28, 33, 3);
                    break;
                case 2:
                    pose.left = SpiritArm.Blend(Rest, Reach, tracked);
                    pose.right = SpiritArm.Blend(Rest, High, .85f * tracked);
                    pose.left = SpiritArm.Blend(pose.left, Hold, closed);
                    pose.right = SpiritArm.Blend(pose.right, Hold, closed);
                    pose.left = SpiritArm.Blend(pose.left, Measure, draw * (1 - closed));
                    pose.right = SpiritArm.Blend(pose.right, High, cast);
                    pose.nod = 12 * closed - 16 * draw;
                    pose.pitch -= 9 * flight;
                    pose.lift += .10f * flight;
                    pose.left = SpiritArm.Blend(pose.left, High, fly * .85f);
                    pose.right = SpiritArm.Blend(pose.right, High, fly);
                    ApplyTouch(ref pose, frame.touch, touch, Measure, High, -21, 37, -12);
                    break;
                default:
                    pose.left = SpiritArm.Blend(Rest, Reach, tracked);
                    pose.right = SpiritArm.Blend(Rest, Offer, tracked);
                    pose.left = SpiritArm.Blend(pose.left, Hold, closed);
                    pose.right = SpiritArm.Blend(pose.right, Hold, closed);
                    pose.right = SpiritArm.Blend(pose.right, Listen, draw * (1 - closed));
                    pose.left = SpiritArm.Blend(pose.left, High, cast);
                    pose.nod = 15 * closed - 11 * draw;
                    pose.tilt = -10 * draw;
                    pose.pitch += 7 * flight;
                    pose.left = SpiritArm.Blend(pose.left, Reach, fly * .61f);
                    pose.right = SpiritArm.Blend(pose.right, Reach, fly * .92f);
                    ApplyTouch(ref pose, frame.touch, touch, Hold, Sow, -22, 27, 23);
                    break;
            }

            pose.nod += 12 * (1 - tracked) - 8 * spinBody;
            pose.lean += 6 * (1 - tracked) * idle;
            pose.step += .22f * fly * idle + spinBody * .72f;
            pose.lift += .09f * spinBody;
            pose.flare = .14f * draw + .23f * cast + .69f * Mathf.Sin(trailing * Mathf.PI);
            pose.ribbon = .08f * idle + .28f * flight + .87f * Mathf.Sin(trailing * Mathf.PI);
            pose.yaw += 360 * Mathf.SmoothStep(0, 1, turn) - 25 * Envelope(frame.turnAge, .10f, .17f, .32f);
            pose.pivot = Mathf.Sin(turn * Mathf.PI);
            pose.look += -21 * Mathf.Sin(turn * Mathf.PI * 2);
            pose.lean += 6 * spinBody;
            pose.left = SpiritArm.Blend(pose.left, Hold, spinBody * .7f);
            pose.right = SpiritArm.Blend(pose.right, High, spinBody);
            return pose;
        }

        private static void ApplyTouch(ref SpiritPose pose, int side, float cue,
            SpiritArm left, SpiritArm right, float turnLeft, float turnRight, float dip)
        {
            if (side == 0)
            {
                pose.left = SpiritArm.Blend(pose.left, left, cue);
                pose.right = SpiritArm.Blend(pose.right, Rest, cue * .62f);
                pose.yaw += turnLeft * cue;
                pose.look -= 17 * cue;
            }
            else
            {
                pose.right = SpiritArm.Blend(pose.right, right, cue);
                pose.left = SpiritArm.Blend(pose.left, Reach, cue * .70f);
                pose.yaw += turnRight * cue;
                pose.pitch += dip * cue;
                pose.nod += dip * cue * .52f;
                pose.lift -= Mathf.Max(0, dip) * .0022f * cue;
                pose.look += 18 * cue;
            }
            pose.lean += (side == 0 ? 6 : -6) * cue;
            pose.shift += (side == 0 ? -.11f : .11f) * cue;
        }

        private static float Envelope(float age, float rise, float hold, float fall)
        {
            if (age < 0) return 0;
            return Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / rise))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(hold, fall, age)));
        }
    }
}
