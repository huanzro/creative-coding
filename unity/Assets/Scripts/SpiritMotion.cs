using UnityEngine;

namespace Echoes
{
    internal struct SpiritArm
    {
        public Vector3 upper, lower;
        public float curl, light, wristFlex, point;

        public SpiritArm(Vector3 upper, Vector3 lower, float curl = 0, float light = 0, float wristFlex = 0, float point = 0)
        {
            this.upper = upper;
            this.lower = lower;
            this.curl = curl;
            this.light = light;
            this.wristFlex = wristFlex;
            this.point = point;
        }

        public static SpiritArm Blend(SpiritArm a, SpiritArm b, float weight) =>
            new SpiritArm(Vector3.Lerp(a.upper, b.upper, weight), Vector3.Lerp(a.lower, b.lower, weight),
                Mathf.Lerp(a.curl, b.curl, weight), Mathf.Lerp(a.light, b.light, weight),
                Mathf.Lerp(a.wristFlex, b.wristFlex, weight), Mathf.Lerp(a.point, b.point, weight));
    }

    internal struct SpiritPose
    {
        public SpiritArm left, right;
        public float time, shift, lift, pitch, yaw, lean, nod, look, tilt, flare, ribbon, step, blink, glow, pivot;
        public float settle, tuck, extension, breath, expression, centerPivot;
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
        private static readonly SpiritArm Wing = new SpiritArm(new Vector3(.94f, -.18f, .29f),
            new Vector3(.85f, -.14f, .38f), .18f, .22f, -15);
        private static readonly SpiritArm Sweep = new SpiritArm(new Vector3(.70f, -.62f, -.48f),
            new Vector3(.84f, .05f, -.61f), .20f, 0, -28);
        private static readonly SpiritArm TouchLight = new SpiritArm(new Vector3(.63f, -.38f, -.70f),
            new Vector3(.62f, .19f, -.76f), .68f, 0, 7, .96f);
        private static readonly SpiritArm CoverHead = new SpiritArm(new Vector3(.61f, .66f, -.11f),
            new Vector3(-.66f, .92f, -.29f), .42f, 0, 17);
        private static readonly SpiritArm HoldAbove = new SpiritArm(new Vector3(.49f, .72f, -.27f),
            new Vector3(.14f, .96f, -.21f), .23f, .46f, 17);
        private static readonly SpiritArm Brace = new SpiritArm(new Vector3(.76f, -.58f, -.36f),
            new Vector3(.38f, -.72f, -.72f), .45f, 0, 27);

        public static SpiritPose Sample(SpiritFrame frame, float tracked, float closed, float voice, float[] dance)
        {
            if (frame.openingTime >= 0) return ActOpening(frame.openingTime);
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
            float idle = Mathf.Sin(time * 1.95f);
            float breath = Mathf.Sin(time * 2.16f + .35f * Mathf.Sin(time * .71f));
            var pose = new SpiritPose {
                time = time, yaw = -8 + dance[3] * 9, lean = dance[2] * 15, step = (dance[12] - dance[16]) * .0025f,
                lift = .037f + idle * .014f, blink = Blink(time),
                look = dance[4] * 17, shift = dance[0] * .0055f, glow = frame.light * .78f + draw * .22f,
                breath = breath, expression = .22f * closed, centerPivot = .24f
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
            float reachUp = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.06f, .80f, frame.verticalSpeed)) *
                (1 - closed) * (1 - spinBody * .73f);
            float glide = frame.glide * (1 - spinBody * .82f);
            float footDrop = Mathf.SmoothStep(0, 1, 1 - Mathf.Clamp01(frame.altitude / .14f)) * glide;
            pose.left = SpiritArm.Blend(pose.left, HoldAbove, reachUp * .61f);
            pose.right = SpiritArm.Blend(pose.right, Reach, reachUp * .46f);
            pose.left = SpiritArm.Blend(pose.left, Wing, glide * .91f);
            pose.right = SpiritArm.Blend(pose.right, Wing, glide * .76f);
            pose.yaw += 16 * glide + 7 * glide * idle;
            pose.lean += glide * (-6 + 4.6f * idle);
            pose.nod += -12 * reachUp + 10 * glide;
            pose.pitch += 16 * glide - 12 * reachUp;
            pose.extension = glide * .91f + .24f * reachUp;
            pose.settle = footDrop * .47f;
            pose.flare += glide * .22f + reachUp * .13f;
            pose.ribbon += glide * (.38f + .40f * flight);
            // An elbow and a wrist follow the chest with different phase delays.
            pose.left.lower.y += .065f * Mathf.Sin(time * 2.16f - .55f) * (1 - closed);
            pose.right.lower.y += .075f * Mathf.Sin(time * 2.16f - 1.12f) * (1 - closed);
            pose.left.wristFlex += 5 * Mathf.Sin(time * 2.3f - .88f);
            pose.right.wristFlex += 7 * Mathf.Sin(time * 2.3f - 1.27f);
            float contact = Mathf.SmoothStep(0, 1, frame.fireflyPulse / .57f);
            if (frame.fireflyResponse == FireflyKind.Amber)
            {
                pose.right = SpiritArm.Blend(pose.right, TouchLight, contact * .82f);
                pose.look -= 7 * contact;
                pose.nod += 9 * contact;
            }
            else if (frame.fireflyResponse == FireflyKind.Scarlet)
            {
                pose.right = SpiritArm.Blend(pose.right, Brace, contact * .85f);
                pose.left = SpiritArm.Blend(pose.left, Wing, contact * .63f);
                pose.pitch -= 7 * contact;
                pose.expression += contact * .55f;
            }
            if (frame.lesson == 0 && frame.hand != Hand.Fist && frame.releaseAge < 0)
            {
                float demonstrate = OpeningStoryboard.Window(time,.42f,.97f,1.55f,2.24f);
                float offer = OpeningStoryboard.Window(time,1.63f,2.26f,2.58f,3.51f);
                pose.right = SpiritArm.Blend(pose.right,Hold,demonstrate);
                pose.right = SpiritArm.Blend(pose.right,Offer,offer);
                pose.left = SpiritArm.Blend(pose.left,Hold,demonstrate*.81f);
                pose.nod += 8*demonstrate-11*offer;
            }
            if (frame.forest) ActForest(ref pose, frame, closed, reachUp, glide, turn, spinBody);
            return pose;
        }

        private static void ActForest(ref SpiritPose pose, SpiritFrame frame, float closed,
            float rise, float glide, float turn, float spin)
        {
            float steer = frame.forestSteering;
            float sweep = Envelope(frame.touchAge, .11f, .22f, .69f);
            float resting = 1 - Mathf.Clamp01((frame.altitude + Mathf.Abs(frame.verticalSpeed) * .1f) * 13);
            pose.yaw = 180 + (pose.yaw - 360 * Mathf.SmoothStep(0, 1, turn)) * .50f + steer * 24;
            pose.lean = pose.lean * (1 - resting * .85f) - steer * 17;
            pose.lift *= 1 - resting;
            pose.step *= 1 - resting;
            var ready = SpiritArm.Blend(Rest, Hold, closed);
            pose.left = SpiritArm.Blend(pose.left, ready, resting * (1 - sweep));
            pose.right = SpiritArm.Blend(pose.right, ready, resting * (1 - sweep));
            float stride = Mathf.Sin(frame.motionTime*15.5f) * Mathf.Abs(steer) * resting;
            pose.step += stride*.43f;
            pose.lift += .029f*Mathf.Abs(stride);
            pose.shift -= steer*.034f*resting;
            pose.look -= steer*15*resting;
            pose.left = SpiritArm.Blend(pose.left, Wing, glide * .44f * (1 - spin));
            pose.right = SpiritArm.Blend(pose.right, Wing, glide * .44f * (1 - spin));
            if (frame.touch == 0)
            {
                pose.left = SpiritArm.Blend(pose.left, Sweep, sweep * .88f);
                pose.right = SpiritArm.Blend(pose.right, Brace, sweep * .34f);
                pose.look -= 25 * sweep;
            }
            else
            {
                pose.right = SpiritArm.Blend(pose.right, Sweep, sweep * .88f);
                pose.left = SpiritArm.Blend(pose.left, Brace, sweep * .34f);
                pose.look += 25 * sweep;
            }
            pose.tuck += rise * .21f;

            float sow = Envelope(frame.forestPlant, .20f, .57f, 1.28f);
            pose.left = SpiritArm.Blend(pose.left, Offer, sow * .64f);
            pose.right = SpiritArm.Blend(pose.right, Sow, sow);
            pose.settle += sow * .42f;
            pose.nod += 21 * sow;
            pose.pitch += 19 * sow;
            pose.glow = Mathf.Max(pose.glow, .78f * sow);

            if (frame.forestSnareAge >= 0 || frame.forestDescent >= 0 || frame.forestFall >= 0)
                ActForestCapture(ref pose, frame);
            if (frame.forestDoorAge >= 0) ActMachineDoor(ref pose, frame);
        }

        private static void ActForestCapture(ref SpiritPose pose, SpiritFrame frame)
        {
            bool web = frame.forestTrap == ForestTrapKind.Web;
            bool cable = frame.forestTrap == ForestTrapKind.Cable;
            bool descent = frame.forestDescent >= 0;
            bool falling = frame.forestFall >= 0;
            float t = descent ? frame.forestDescent * Journey.ForestTransitionDuration : falling ? 3.2f * frame.forestFall : frame.forestSnareAge;
            float grasp = falling ? OpeningStoryboard.Ease(.92f,1.4f,t) :
                descent ? OpeningStoryboard.Ease(cable?1.40f:.75f,cable?2.68f:1.76f,t) : OpeningStoryboard.Ease(0,.32f,t);
            if (frame.forestRescue)
                grasp *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.68f, 2.4f, t));
            if (falling)
            {
                float flail = OpeningStoryboard.Window(t,0,.32f,1.0f,1.60f);
                pose.left = SpiritArm.Blend(pose.left,High,flail);
                pose.right = SpiritArm.Blend(pose.right,Reach,flail);
                pose.lean += Mathf.Sin(t*11)*flail*18;
                pose.pitch += flail*25;
            }
            float struggle = Mathf.Sin(t * (web ? 9.5f : 6.4f)) * Mathf.Exp(-t * .76f);
            float fold = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.20f, 1.25f, t));
            pose.yaw = Mathf.Lerp(pose.yaw, web ? 143 : 221, grasp) + 9 * struggle * grasp;
            pose.look = Mathf.Lerp(pose.look, web ? -20 : 29, grasp);
            pose.pitch = Mathf.Lerp(pose.pitch, web ? -14 : 33, grasp) + 7 * struggle * grasp;
            pose.lean = Mathf.Lerp(pose.lean, web ? -18 : 22, grasp) + 17 * struggle * grasp;
            pose.nod -= 21 * grasp;
            pose.left = SpiritArm.Blend(pose.left, web ? HoldAbove : cable ? Reach : Brace, grasp);
            pose.right = SpiritArm.Blend(pose.right, web ? High : cable ? Hold : CoverHead, grasp);
            pose.tuck = Mathf.Max(pose.tuck, grasp * (web ? .50f : .92f));
            pose.settle += (web ? .07f : .63f) * grasp * fold;
            pose.ribbon += grasp * (.48f + Mathf.Abs(struggle) * .22f);
            pose.expression += grasp * .7f;
        }

        private static void ActMachineDoor(ref SpiritPose pose, SpiritFrame frame)
        {
            float t = frame.forestDoorAge;
            float fall = OpeningStoryboard.Ease(.23f,.81f,t);
            float alarm = OpeningStoryboard.Ease(.66f,1.45f,t);
            bool pool = frame.forestDoor == 2;
            pose.left = SpiritArm.Blend(pose.left,fall < .8f ? Wing : Brace,fall);
            pose.right = SpiritArm.Blend(pose.right,pool ? Reach : CoverHead,alarm);
            pose.look += (frame.forestDoor == 1 ? 40 : -21)*alarm;
            pose.pitch += (pool ? -15 : 25)*fall;
            pose.yaw += Mathf.Sin(t*5)*alarm*(pool ? 11 : 28);
            pose.tuck += (pool ? .40f : .80f)*alarm;
            pose.extension += .49f*fall;
            pose.glow *= 1-.75f*OpeningStoryboard.Ease(1.75f,2.9f,t);
        }

        private static SpiritPose ActOpening(float t)
        {
            var shot = OpeningStoryboard.Sample(t);
            float sweep = OpeningStoryboard.Window(t, .65f, 2.08f, 3.7f, 4.9f);
            float yellow = OpeningStoryboard.Window(t, 4.35f, 6.73f, 7.19f, 7.57f);
            float rescue = OpeningStoryboard.Window(t, 10.88f, 12.83f, 15.38f, 17.4f);
            float chase = OpeningStoryboard.Window(t, 17.3f, 19.23f, 21.17f, 22.5f);
            float red = OpeningStoryboard.Window(t, 24.0f, 26.38f, 26.87f, 27.4f);
            float lookFirst = OpeningStoryboard.Window(t, 23.03f, 24.24f, 25.02f, 26.40f);
            float recoil = OpeningStoryboard.Window(t, 26.83f, 27.10f, 27.39f, 28.23f);
            float brace = OpeningStoryboard.Window(t, 29.9f, 31.28f, 31.48f, 32.14f);
            float falling = OpeningStoryboard.Window(t, 7.4f, 8.1f, 10.45f, 12.21f) +
                OpeningStoryboard.Window(t, 27.4f, 28.3f, 30.57f, 31.5f);
            float knees = OpeningStoryboard.Window(t, 30.7f, 31.65f, 32.85f, 34.58f);
            float rub = OpeningStoryboard.Window(t, 33.48f, 34.48f, 35.16f, 36.8f);
            float settle = OpeningStoryboard.Ease(34.7f, 37.4f, t);
            float alarm = Mathf.Clamp01(falling * .62f + knees * .44f + recoil * .61f);
            float stride = Mathf.Sin(t * 4.14f - .26f) * sweep;
            var pose = new SpiritPose
            {
                time = t, pitch = shot.Attitude.x, yaw = -shot.Attitude.y, lean = shot.Attitude.z,
                left = Rest, right = Rest, centerPivot = 1, blink = Blink(t),
                breath = Mathf.Sin(t * (falling > .62f ? 3.3f : 2.16f)),
                extension = .36f * chase + .42f * rescue,
                nod = 13 * sweep + 12 * yellow - 28 * rescue - 26 * chase + 16 * rub + 15 * brace,
                look = -15 * (yellow + red) - 23 * lookFirst + 8 * falling + 11 * rub,
                tilt = -10 * rub + 7 * red + 8 * lookFirst,
                expression = alarm,
                tuck = .7f * falling + .77f * knees, settle = knees * .39f + .18f * rub,
                step = .47f * stride + .38f * rescue - .27f * chase + .32f * knees,
                lift = .019f * Mathf.Abs(stride) - .10f * brace - .11f * knees,
                shift = -.10f * recoil + .026f * stride,
                flare = shot.Rush * .88f + falling * .26f,
                ribbon = shot.Rush * .86f + rescue * .26f + .08f * Mathf.Sin(t * 2.8f - .5f),
                glow = .34f * (yellow + rescue + chase) + .52f * red
            };
            pose.left = SpiritArm.Blend(pose.left, Sweep, sweep * (.58f - .29f * stride));
            pose.right = SpiritArm.Blend(pose.right, Sweep, sweep * (.68f + .26f * stride));
            pose.right = SpiritArm.Blend(pose.right, TouchLight, Mathf.Max(yellow, red));
            pose.left = SpiritArm.Blend(pose.left, Offer, .41f * (yellow + red));
            pose.left = SpiritArm.Blend(pose.left, Wing, .68f * falling);
            pose.right = SpiritArm.Blend(pose.right, High, .68f * falling);
            pose.left = SpiritArm.Blend(pose.left, HoldAbove, Mathf.Max(rescue * .55f, chase * .45f));
            pose.right = SpiritArm.Blend(pose.right, HoldAbove, Mathf.Max(rescue, chase));
            pose.right = SpiritArm.Blend(pose.right, Hold, recoil * .84f);
            pose.left = SpiritArm.Blend(pose.left, Brace, brace);
            pose.left = SpiritArm.Blend(pose.left, Hold, knees * .82f);
            pose.right = SpiritArm.Blend(pose.right, Brace, knees);
            pose.right = SpiritArm.Blend(pose.right, CoverHead, rub);
            pose.right.wristFlex += Mathf.Sin((t - 33.9f) * 10.9f) * rub * 11;
            pose.left = SpiritArm.Blend(pose.left, Brace, rub * .58f);
            pose.left = SpiritArm.Blend(pose.left, Reach, settle * .88f);
            pose.right = SpiritArm.Blend(pose.right, Offer, settle);
            pose.left.light *= .13f;
            pose.right.light *= .03f;
            pose.blink *= 1 - Mathf.Max(knees * .82f, brace * .65f);
            return pose;
        }

        private static float Blink(float time) =>
            1 - Envelope(Mathf.Repeat(time + .38f * Mathf.Sin(time * .57f) + .7f, 4.48f),
                .055f, .074f, .158f) * .94f;

        private static void ApplyTouch(ref SpiritPose pose, int side, float cue,
            SpiritArm left, SpiritArm right, float turnLeft, float turnRight, float dip)
        {
            if (side == 0)
            {
                pose.left = SpiritArm.Blend(pose.left, left, cue);
                pose.right = SpiritArm.Blend(pose.right, Rest, cue * .62f);
                pose.yaw -= turnLeft * cue;
                pose.look += 17 * cue;
            }
            else
            {
                pose.right = SpiritArm.Blend(pose.right, right, cue);
                pose.left = SpiritArm.Blend(pose.left, Reach, cue * .70f);
                pose.yaw -= turnRight * cue;
                pose.pitch += dip * cue;
                pose.nod += dip * cue * .52f;
                pose.lift -= Mathf.Max(0, dip) * .0022f * cue;
                pose.look -= 18 * cue;
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
