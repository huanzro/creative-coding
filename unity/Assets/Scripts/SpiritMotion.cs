using UnityEngine;

namespace Echoes
{
    internal struct SpiritArm
    {
        public Vector3 upper, lower;
        public float curl, light, wristFlex, point, wristRoll, fan;

        public SpiritArm(Vector3 upper, Vector3 lower, float curl = 0, float light = 0, float wristFlex = 0, float point = 0,
            float wristRoll = 0, float fan = 0)
        {
            this.upper = upper;
            this.lower = lower;
            this.curl = curl;
            this.light = light;
            this.wristFlex = wristFlex;
            this.point = point;
            this.wristRoll = wristRoll;
            this.fan = fan;
        }

        public static SpiritArm Blend(SpiritArm a, SpiritArm b, float weight) =>
            new SpiritArm(Vector3.Lerp(a.upper, b.upper, weight), Vector3.Lerp(a.lower, b.lower, weight),
                Mathf.Lerp(a.curl, b.curl, weight), Mathf.Lerp(a.light, b.light, weight),
                Mathf.Lerp(a.wristFlex, b.wristFlex, weight), Mathf.Lerp(a.point, b.point, weight),
                Mathf.Lerp(a.wristRoll, b.wristRoll, weight), Mathf.Lerp(a.fan, b.fan, weight));
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
        private static readonly SpiritArm GuardHeart = new SpiritArm(new Vector3(.42f, -.68f, -.43f),
            new Vector3(-.74f, .49f, -.47f), .27f, .17f, 8);
        private static readonly SpiritArm MawUpperPalm = new SpiritArm(new Vector3(.54f,.43f,-.68f),
            new Vector3(.16f,.71f,-.69f),.09f,.32f,-14);
        private static readonly SpiritArm MawLowerPalm = new SpiritArm(new Vector3(.58f,-.43f,-.69f),
            new Vector3(.13f,-.27f,-.95f),.12f,.18f,20);

        public static SpiritPose Sample(SpiritFrame frame, float tracked, float closed, float voice, float[] dance,
            float pointing = 0, Vector2 writingSpeed = default)
        {
            if (frame.openingTime >= 0) return ActOpening(frame.openingTime);
            SpiritDance.Sample(frame.phase, dance);
            float time = frame.motionTime;
            float draw = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.105f, .30f, voice));
            float flight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.37f, .72f, voice));
            if (frame.chapter == 2)
            {
                draw = pointing;
                flight = frame.starThrust * .83f;
            }
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
                    SampleStarWriting(ref pose, frame, touch, pointing, writingSpeed);
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
            float turnYaw = 360 * Mathf.SmoothStep(0, 1, turn) - 25 * Envelope(frame.turnAge, .10f, .17f, .32f);
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
            if (frame.forest) ActForest(ref pose, frame, closed, reachUp, glide, spinBody);
            else if (frame.ruins) ActRuins(ref pose, frame, closed, glide);
            // Chapter poses establish the travel heading before the full turn is applied.
            pose.yaw += turnYaw;
            return pose;
        }

        private static void SampleStarWriting(ref SpiritPose pose, SpiritFrame frame, float touch, float pointing, Vector2 motion)
        {
            float aimX = (frame.starTipX - .5f) * 2 * pointing;
            float aimY = (.5f - frame.starTipY) * 2 * pointing;
            float flow = Mathf.Clamp01(motion.magnitude);
            Vector3 wrist = new Vector3(.21f + .155f * aimX + .047f * motion.x,
                -.057f + .202f * aimY - .044f * motion.y + .014f * Mathf.Sin(pose.time * 2.2f), -.33f - .036f * flow);
            Vector3 reach = Vector3.ClampMagnitude(wrist, .493f);
            float length = reach.magnitude;
            Vector3 axis = reach / length;
            Vector3 bend = Vector3.ProjectOnPlane(new Vector3(.65f, -.92f, .11f), axis).normalized;
            float upper = SpiritModel.UpperArmLength, lower = SpiritModel.ForearmLength;
            float along = (upper * upper - lower * lower + length * length) / (2 * length);
            Vector3 elbow = axis * along + bend * Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
            pose.right = new SpiritArm(elbow, reach - elbow, .68f + .15f * flow - .13f * touch, .13f + touch * .19f,
                -6 + 18 * aimY - 21 * motion.y, 1, -14 + 31 * motion.x + 13 * aimX, .28f - .46f * flow + .22f * touch);
            pose.left = SpiritArm.Blend(Rest, Wing, Mathf.Clamp01(.33f + frame.starThrust * .36f + flow * .13f));
            pose.left.wristFlex = 9 * aimY + motion.y * 14;
            pose.yaw -= 7 + aimX * 12 + frame.starThrust * 10;
            pose.lean -= aimX * 7 + motion.x * 6;
            pose.look -= 12 + aimX * 14 + motion.x * 6;
            pose.nod += 8 - aimY * 16 + motion.y * 5;
            pose.pitch += frame.starThrust * 17;
            pose.lift += frame.starThrust * .063f;
            pose.extension = frame.starThrust * .67f;
        }

        private static void ActRuins(ref SpiritPose pose, SpiritFrame frame, float closed, float glide)
        {
            float steer = frame.ruinSteering;
            float rest = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.016f, .16f, frame.altitude));
            float openGate = Mathf.SmoothStep(0, 1, frame.ruinUnlock) * (1 - closed);
            pose.yaw = 180 + pose.yaw * .38f + 21 * steer;
            pose.lean = pose.lean * (1 - .72f * rest) - steer * 14;
            pose.lift *= 1 - .9f * rest;
            pose.step = Mathf.Lerp(pose.step, Mathf.Sin(pose.time * 12) * Mathf.Abs(steer) * .39f, rest);
            pose.look += 22 * steer;
            pose.left = SpiritArm.Blend(pose.left, Rest, .80f * rest * (1 - closed));
            pose.right = SpiritArm.Blend(pose.right, Wing, glide * .36f);
            pose.right = SpiritArm.Blend(pose.right, Push, openGate);

            if (frame.ruinRetryPhase != RuinRetryPhase.Inactive)
            {
                ActRuinRetry(ref pose, frame);
                return;
            }
            if (frame.ruinHeartAge < 0) return;
            float jolt = OpeningStoryboard.Window(frame.ruinHeartAge, 0, .085f, .17f, .55f);
            float recover = OpeningStoryboard.Window(frame.ruinHeartAge, .23f, .52f, .64f, 1.38f);
            pose.left = SpiritArm.Blend(pose.left, CoverHead, .82f * jolt);
            pose.right = SpiritArm.Blend(pose.right, Brace, jolt);
            pose.left = SpiritArm.Blend(pose.left, Hold, .81f * recover);
            pose.pitch -= 21 * jolt;
            pose.lean -= 16 * jolt;
            pose.lift += .19f * jolt;
            pose.tuck += .38f * jolt;
            pose.nod += 12 * recover - 16 * jolt;
            pose.expression += .9f * jolt;
            pose.ribbon += .65f * jolt;
        }

        private static void ActRuinRetry(ref SpiritPose pose, SpiritFrame frame)
        {
            float t = frame.ruinRetryAge;
            switch (frame.ruinRetryPhase)
            {
                case RuinRetryPhase.Impact:
                    float brace = OpeningStoryboard.Window(t,.21f,.39f,.48f,.77f);
                    float shock = OpeningStoryboard.Window(t,.385f,.425f,.47f,.65f);
                    float catchStep = OpeningStoryboard.Window(t,.48f,.69f,.84f,1.17f);
                    float glance = OpeningStoryboard.Window(t,.79f,1.06f,1.30f,1.62f);
                    pose.left = SpiritArm.Blend(pose.left, Push, .95f*brace);
                    pose.right = SpiritArm.Blend(pose.right, Push, brace);
                    pose.right = SpiritArm.Blend(pose.right, Brace, .72f*catchStep);
                    pose.pitch += 27*brace - 35*shock - 12*catchStep;
                    pose.lean -= 14*shock + 10*catchStep;
                    pose.yaw += 23*glance - 12*shock;
                    pose.look += 42*glance;
                    pose.nod += 12*brace - 16*shock;
                    pose.step -= .69f*catchStep;
                    pose.lift += .10f*shock - .075f*catchStep;
                    pose.settle += .57f*catchStep;
                    pose.tuck += .42f*shock;
                    pose.ribbon -= .49f*shock;
                    pose.expression += .82f*shock;
                    break;
                default:
                    ActRuinChamber(ref pose, frame);
                    break;
            }
        }

        private static void ActRuinChamber(ref SpiritPose pose, SpiritFrame frame)
        {
            float age = frame.ruinRetryAge;
            pose.left = Rest;
            pose.right = Rest;
            pose.glow = .23f;
            pose.nod = 0;
            pose.look = 0;
            pose.step = 0;
            pose.lift = Mathf.Clamp(pose.lift, .003f, .012f);
            pose.pitch = pose.lean = pose.tuck = pose.ribbon = pose.expression = 0;
            pose.yaw = 180;
            switch (frame.ruinRetryPhase)
            {
                case RuinRetryPhase.Gate:
                    float checking = OpeningStoryboard.Window(age, .04f, .71f, 1.04f, 1.56f);
                    float crossing = OpeningStoryboard.Window(age, .80f, 1.55f, 2.41f, 3.16f);
                    float gateStride = Mathf.Sin((age - .8f) * 10);
                    pose.look = 19 * checking;
                    pose.nod = -11 * checking;
                    pose.pitch = 6 * crossing;
                    pose.glow += .29f * checking;
                    pose.left = SpiritArm.Blend(Rest, Reach, crossing * Mathf.Max(.14f, 1 - gateStride) * .43f);
                    pose.right = SpiritArm.Blend(Rest, Brace, crossing * Mathf.Max(.14f, 1 + gateStride) * .41f);
                    pose.step = .68f * gateStride * crossing;
                    pose.lift += .045f * Mathf.Abs(gateStride) * crossing;
                    break;
                case RuinRetryPhase.Survey:
                    // The head tracks the panoramic camera without translating the actor.
                    float yaw = Mathf.Sin(frame.ruinLookYaw * Mathf.Deg2Rad);
                    pose.yaw += 18 * yaw;
                    pose.look = 32 * yaw;
                    pose.nod = Mathf.Clamp(-frame.ruinLookPitch * .38f, -28, 22);
                    pose.nod -= 17 * OpeningStoryboard.Window(age, 0, .50f, 1.07f, 2.25f);
                    break;
                case RuinRetryPhase.Arrival:
                    float surprise = OpeningStoryboard.Window(age, .31f, 1.0f, 1.80f, 3.65f);
                    float settle = OpeningStoryboard.Window(age, 2.30f, 2.65f, 2.88f, 3.53f);
                    pose.nod = -39 * surprise + 6 * settle;
                    pose.pitch = -8 * surprise;
                    pose.left = SpiritArm.Blend(Rest, Brace, surprise * .45f);
                    pose.right = SpiritArm.Blend(Rest, CoverHead, surprise * .45f);
                    pose.settle = .36f * settle;
                    pose.expression = .68f * surprise;
                    break;
                case RuinRetryPhase.Valuation:
                    float palm = OpeningStoryboard.Window(age, .20f, .83f, 1.38f, 2.26f);
                    float recognition = OpeningStoryboard.Ease(2.13f, 3.06f, age);
                    pose.right = SpiritArm.Blend(Rest, Offer, palm * .63f);
                    pose.nod = 14 * palm - 11 * recognition;
                    pose.pitch = 6 * palm;
                    pose.lean = -4 * palm;
                    pose.look = 16 * palm;
                    pose.expression = .29f * recognition;
                    break;
                case RuinRetryPhase.Bargain:
                    float weigh = OpeningStoryboard.Ease(.1f, .75f, age);
                    pose.left = SpiritArm.Blend(Rest, GuardHeart, .66f * weigh);
                    pose.nod = 11 * weigh;
                    pose.tilt = 7 * weigh;
                    break;
                case RuinRetryPhase.Accepted:
                case RuinRetryPhase.Declined:
                    ActRuinExit(ref pose, age, frame.ruinRetryPhase == RuinRetryPhase.Accepted);
                    break;
            }
        }

        private static void ActRuinExit(ref SpiritPose pose, float age, bool accepted)
        {
            if (accepted)
            {
                float consent = OpeningStoryboard.Window(age, 0, .39f, .76f, 1.10f);
                float heart = OpeningStoryboard.Window(age, .83f, 1.16f, 1.32f, 1.75f);
                float brain = OpeningStoryboard.Window(age, 1.60f, 1.95f, 2.24f, 2.68f);
                float recover = OpeningStoryboard.Window(age, 1.52f, 2.36f, 2.67f, 3.28f);
                pose.right = SpiritArm.Blend(Rest, Offer, .52f * consent);
                pose.right = SpiritArm.Blend(pose.right, Brace, .79f * heart);
                pose.left = SpiritArm.Blend(Rest, GuardHeart, .82f * heart + .22f * recover);
                pose.pitch = -13 * heart - 8 * brain + 9 * recover;
                pose.nod = 10 * consent - 17 * brain + 25 * recover;
                pose.lean = -9 * heart + 6 * brain;
                pose.lift -= .09f * recover;
                pose.tuck = .42f * recover;
                pose.settle = .22f * recover;
                pose.expression = Mathf.Max(heart, brain) * .79f;
                pose.glow = Mathf.Lerp(.31f, .06f, OpeningStoryboard.Ease(1.04f, 2.67f, age));
            }
            else
            {
                float shake = OpeningStoryboard.Window(age, 0, .47f, 1.0f, 1.58f);
                float resolve = OpeningStoryboard.Window(age, .38f, 1.11f, 2.62f, 3.42f);
                pose.left = SpiritArm.Blend(Rest, GuardHeart, .86f * resolve);
                pose.right = SpiritArm.Blend(Rest, Brace, .34f * resolve);
                pose.look = Mathf.Sin(age * 13) * 19 * shake - 17 * resolve;
                pose.nod = -13 * resolve;
                pose.glow = .31f + .23f * resolve;
            }

            float start = accepted ? 3.05f : 2.53f;
            float takeoff = OpeningStoryboard.Ease(accepted ? 3.80f : 3.18f, 4.53f, age);
            float strides = OpeningStoryboard.Window(age, start, start + .30f, 3.96f, 4.52f);
            float stride = Mathf.Sin((age - start) * 11.4f) * strides;
            float sidestep = accepted ? 0 : OpeningStoryboard.Window(age, 2.38f, 2.94f, 3.42f, 4.19f);
            pose.yaw -= 52 * sidestep;
            pose.lean += 14 * sidestep;
            pose.step += .78f * stride;
            pose.lift += .046f * Mathf.Abs(stride);
            pose.pitch += 13 * takeoff;
            pose.nod -= 9 * takeoff;
            pose.left = SpiritArm.Blend(pose.left, Wing, takeoff * .90f);
            pose.right = SpiritArm.Blend(pose.right, Reach, takeoff * .82f);
            pose.ribbon += .42f * takeoff;
            pose.extension += .47f * takeoff;
        }

        private static void ActForest(ref SpiritPose pose, SpiritFrame frame, float closed,
            float rise, float glide, float spin)
        {
            float steer = frame.forestSteering;
            float sweep = Envelope(frame.touchAge, .11f, .22f, .69f);
            float resting = 1 - Mathf.Clamp01((frame.altitude + Mathf.Abs(frame.verticalSpeed) * .1f) * 13);
            pose.yaw = 180 + pose.yaw * .50f + steer * 24;
            pose.lean = pose.lean * (1 - resting * .85f) - steer * 17;
            pose.lift *= 1 - resting;
            pose.step *= 1 - resting;
            var ready = SpiritArm.Blend(Rest, Hold, closed);
            pose.left = SpiritArm.Blend(pose.left, ready, resting * (1 - sweep));
            pose.right = SpiritArm.Blend(pose.right, ready, resting * (1 - sweep));
            float travelStep = Mathf.Clamp01(frame.forestMazeSpeed / 2.5f);
            float stride = Mathf.Sin(frame.motionTime*Mathf.Lerp(8,13,travelStep)) * Mathf.Max(Mathf.Abs(steer),travelStep) * resting;
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

            if (frame.forestMachineShaft)
            {
                float p=frame.forestShaftProgress;
                float glideFrom=OpeningStoryboard.Window(p,0,.06f,.86f,.99f);
                float dive=OpeningStoryboard.Window(p,.07f,.24f,.69f,.89f);
                float watch=OpeningStoryboard.Window(p,.14f,.32f,.48f,.66f);
                float braking=OpeningStoryboard.Window(p,.80f,.94f,.98f,1);
                pose.left=SpiritArm.Blend(pose.left,Wing,glideFrom*.95f);
                pose.right=SpiritArm.Blend(pose.right,Wing,glideFrom*.95f);
                pose.right=SpiritArm.Blend(pose.right,High,braking*.66f);
                pose.pitch+=28*dive-12*braking;
                pose.lean+=Mathf.Sin(frame.motionTime*4.8f)*5.2f*dive;
                pose.look+=33*watch;
                pose.nod+=17*dive-13*braking;
                pose.tuck+=.50f*braking;
                pose.ribbon+=.76f*dive;
            }

            float sow = Envelope(frame.forestPlant, .20f, .57f, 1.28f);
            pose.left = SpiritArm.Blend(pose.left, Offer, sow * .64f);
            pose.right = SpiritArm.Blend(pose.right, Sow, sow);
            pose.settle += sow * .42f;
            pose.nod += 21 * sow;
            pose.pitch += 19 * sow;
            pose.glow = Mathf.Max(pose.glow, .78f * sow);

            if(frame.forestMachineHeart && frame.forestDescent>=0)
            {
                float marvel=OpeningStoryboard.Window(frame.forestDescent,.13f,.24f,.47f,.56f);
                pose.nod-=22*marvel;
                pose.look+=27*marvel;
                pose.left=SpiritArm.Blend(pose.left,Offer,.76f*marvel);
                pose.right=SpiritArm.Blend(pose.right,Rest,.71f*marvel);
            }

            if (frame.forestRescue && frame.forestSnareAge >= 0)
                ActForestRescue(ref pose, frame);
            else if (frame.forestSnareAge >= 0 || frame.forestDescent >= 0 || frame.forestFall >= 0)
                ActForestCapture(ref pose, frame);
            if (frame.forestDoorAge >= 0) ActMachineDoor(ref pose, frame);
        }

        private static void ActForestRescue(ref SpiritPose pose, SpiritFrame frame)
        {
            float t = frame.forestSnareAge;
            bool web = frame.forestTrap == ForestTrapKind.Web;
            var shot=ForestRescueStoryboard.Sample(t,web);
            if (!web)
            {
                ActMawRescue(ref pose,t,shot);
                return;
            }
            float brace=shot.tight;
            float looking=OpeningStoryboard.Window(t,2.15f,2.81f,6.92f,8.23f);
            float wrench=shot.pull*(.92f+.08f*Mathf.Sin(t*13));
            pose.yaw += -29*brace + 17*wrench - 24*shot.returnLook;
            pose.lean += -19*brace + shot.effort*Mathf.Sin(t*8.0f)*9 + 13*shot.balance;
            pose.pitch += -13*brace - 9*wrench + 17*shot.balance;
            pose.nod += 19*looking - 6*shot.effort;
            pose.look = Mathf.Lerp(pose.look,31,looking*.91f) - 30*shot.returnLook;
            pose.left = SpiritArm.Blend(pose.left,HoldAbove,brace);
            pose.right = SpiritArm.Blend(pose.right,Hold,brace);
            pose.left = SpiritArm.Blend(pose.left,Hold,wrench);
            pose.right = SpiritArm.Blend(pose.right,Sweep,wrench);
            pose.left = SpiritArm.Blend(pose.left,Wing,shot.balance*.78f);
            pose.right = SpiritArm.Blend(pose.right,Wing,shot.balance*.65f);
            pose.step += .46f*shot.effort*Mathf.Sin(t*9)+.60f*shot.balance;
            pose.tuck = Mathf.Max(pose.tuck,brace*.45f);
            pose.settle += shot.balance*.79f;
            pose.ribbon += .43f*brace + .38f*shot.effort;
            pose.expression += .78f*brace;
        }

        private static void ActMawRescue(ref SpiritPose pose,float t,ForestRescueStoryboard.Performance shot)
        {
            float duck=OpeningStoryboard.Window(t,.18f,.63f,1.30f,2.56f);
            float press=shot.pry*(1-OpeningStoryboard.Ease(6.95f,7.64f,t));
            float retreat=OpeningStoryboard.Window(t,6.93f,7.47f,8.02f,9.25f);
            float landing=shot.balance;

            pose.left=SpiritArm.Blend(pose.left,CoverHead,duck*.88f);
            pose.right=SpiritArm.Blend(pose.right,MawLowerPalm,Mathf.Max(duck*.55f,shot.tight*.85f));
            pose.left=SpiritArm.Blend(pose.left,MawUpperPalm,Mathf.Max(.38f*shot.watch,press));
            pose.right=SpiritArm.Blend(pose.right,MawLowerPalm,press);
            pose.left=SpiritArm.Blend(pose.left,Wing,retreat*.77f);
            pose.right=SpiritArm.Blend(pose.right,Reach,retreat*.77f);
            pose.right=SpiritArm.Blend(pose.right,Brace,landing*.76f);

            pose.yaw+=26*shot.tight-43*retreat+17*shot.returnLook;
            pose.look=Mathf.Lerp(pose.look,44,shot.watch*.94f)-24*retreat+25*shot.returnLook;
            pose.nod+=28*duck+17*shot.watch-22*shot.pop+15*landing;
            pose.pitch+=25*duck+15*press-29*shot.pop+24*landing;
            pose.lean+=15*duck+10*shot.mouthPulse*Mathf.Sin(t*19)-23*retreat+14*landing;
            pose.shift-=.21f*retreat;
            pose.lift+=.32f*shot.pop;
            pose.step+=.38f*shot.lowerJaw*(1-retreat)+.63f*retreat;
            pose.tuck=Mathf.Max(pose.tuck,.83f*duck+.46f*press+.38f*shot.pop);
            pose.settle+=.43f*shot.tight+.58f*landing;
            pose.ribbon+=.36f*shot.tight+.73f*shot.pop+.24f*retreat;
            pose.expression+=.72f*duck+.40f*press;
        }

        private static void ActForestCapture(ref SpiritPose pose, SpiritFrame frame)
        {
            bool web = frame.forestTrap == ForestTrapKind.Web;
            bool cable = frame.forestTrap == ForestTrapKind.Cable;
            bool descent = frame.forestDescent >= 0;
            bool falling = frame.forestFall >= 0;
            float t = descent ? frame.forestDescent * (cable ? 8.8f : Journey.ForestTransitionDuration) :
                falling ? Journey.ForestFallDuration * frame.forestFall : frame.forestSnareAge;
            float grasp = falling ? OpeningStoryboard.Ease(.92f,1.4f,t) :
                descent ? OpeningStoryboard.Ease(cable?4.97f:.75f,cable?6.3f:1.76f,t) : OpeningStoryboard.Ease(0,.32f,t);
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
            float t = frame.forestDoorAge / Journey.ForestDoorDuration;
            float fall = OpeningStoryboard.Window(t,.06f,.13f,.19f,.34f);
            float regard = OpeningStoryboard.Window(t,.19f,.34f,.46f,.58f);
            float panic = OpeningStoryboard.Ease(.46f,.67f,t);
            float contact = OpeningStoryboard.Ease(.65f,.83f,t);
            pose.left=SpiritArm.Blend(Rest,Wing,fall);
            pose.right=SpiritArm.Blend(Rest,Reach,fall);
            pose.settle+=.42f*fall;
            pose.look+=21*regard;
            pose.pitch+=25*fall;
            switch(frame.forestDoor)
            {
                case 0:
                    pose.left=SpiritArm.Blend(pose.left,Brace,panic);
                    pose.right=SpiritArm.Blend(pose.right,Push,panic);
                    pose.yaw-=38*regard-27*contact;
                    pose.lean-=24*panic;
                    pose.nod+=27*contact;
                    pose.tuck+=.81f*contact;
                    break;
                case 1:
                    float recoil=OpeningStoryboard.Window(t,.29f,.46f,.56f,.74f);
                    pose.yaw+=57*recoil-30*contact;
                    pose.look-=37*recoil;
                    pose.left=SpiritArm.Blend(pose.left,Sweep,recoil);
                    pose.right=SpiritArm.Blend(pose.right,High,contact);
                    pose.lean+=22*panic*Mathf.Sin(frame.forestDoorAge*4.2f);
                    pose.tuck+=.53f*panic;
                    break;
                default:
                    pose.left=SpiritArm.Blend(pose.left,Push,regard + .31f*panic);
                    pose.right=SpiritArm.Blend(pose.right,Reach,panic);
                    pose.nod+=30*regard-25*contact;
                    pose.pitch-=17*contact;
                    pose.extension+=.54f*contact;
                    pose.glow*=1-.88f*OpeningStoryboard.Ease(.64f,.92f,t);
                    break;
            }
            pose.expression+=panic*.72f;
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
