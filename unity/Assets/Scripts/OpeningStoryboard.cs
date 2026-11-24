/* ARTISTIC RATIONALE
 * A single warm light carries the eye through changes of scale and depth.
 * Scarlet offers a superficially similar invitation and gives a different answer.
 */
using UnityEngine;

namespace Echoes
{
    internal static class OpeningStoryboard
    {
        internal readonly struct Shot
        {
            public readonly Vector3 Actor; // Screen x, screen y, size.
            public readonly Vector3 Camera; // Drift x, drift y, push.
            public readonly Vector3 Attitude; // Pitch, yaw, bank.
            public readonly float Ruin, Sky, Ground, Rush;
            public Shot(Vector3 actor, Vector3 camera, Vector3 attitude, float ruin, float sky, float ground, float rush)
            { Actor = actor; Camera = camera; Attitude = attitude; Ruin = ruin; Sky = sky; Ground = ground; Rush = rush; }
        }

        private readonly struct Key
        {
            public readonly float Time;
            public readonly Vector3 Value;
            public Key(float time, float x, float y, float z) { Time = time; Value = new Vector3(x, y, z); }
        }

        private static readonly Key[] Actor =
        {
            new Key(0, .365f, .727f, 1.58f),
            new Key(2.5f, .429f, .705f, 1.67f),
            new Key(4.2f, .478f, .696f, 1.88f),
            new Key(6.5f, .510f, .637f, 2.32f),
            new Key(7.08f, .521f, .633f, 2.37f),
            new Key(7.4f, .523f, .666f, 2.22f),
            new Key(8.8f, .545f, .845f, 1.36f),
            new Key(10.3f, .518f, .635f, 1.11f),
            new Key(11.8f, .476f, .671f, 1.16f),
            new Key(13.8f, .510f, .559f, 1.43f),
            new Key(15.8f, .529f, .387f, 1.42f),
            new Key(17.5f, .498f, .556f, 1.45f),
            new Key(18.8f, .532f, .585f, 1.47f),
            new Key(21.2f, .465f, .561f, 1.10f),
            new Key(23.8f, .463f, .542f, 1.31f),
            new Key(26.35f, .505f, .514f, 2.42f),
            new Key(26.93f, .526f, .518f, 2.48f),
            new Key(27.4f, .544f, .553f, 1.79f),
            new Key(28.9f, .568f, .66f, 1.49f),
            new Key(30.3f, .607f, .714f, 1.54f),
            new Key(31.4f, .576f, .792f, 1.61f),
            new Key(31.69f, .501f, .786f, 1.60f),
            new Key(32.1f, .449f, .786f, 1.55f),
            new Key(32.66f, .407f, .790f, 1.49f),
            new Key(33.5f, .398f, .803f, 1.42f),
            new Key(34.2f, .412f, .780f, 1.38f),
            new Key(35.3f, .448f, .766f, 1.30f),
            new Key(37.6f, .50f, .771f, 1)
        };

        private static readonly Key[] Camera =
        {
            new Key(0, -.48f, .58f, .10f), new Key(5.5f, -.14f, .61f, .32f),
            new Key(7.4f, .04f, .53f, .19f), new Key(9.5f, .16f, -.70f, .23f),
            new Key(11.5f, -.08f, -.48f, .14f), new Key(15.5f, .21f, .70f, .24f),
            new Key(17.8f, .09f, .275f, .13f), new Key(20.75f, -.13f, .76f, .27f),
            new Key(23.0f, -.15f, .40f, .11f), new Key(26.4f, .15f, .32f, .34f),
            new Key(29.7f, .23f, -.80f, .29f), new Key(31.45f, .22f, .72f, .25f),
            new Key(31.67f, .11f, .51f, .23f), new Key(33.5f, -.35f, .47f, .17f),
            new Key(37.6f, 0, .22f, .07f)
        };

        private static readonly Key[] Attitude =
        {
            new Key(0, 10, 26, 3), new Key(2.9f, 13, 39, -9),
            new Key(5.8f, 21, 41, -8), new Key(7.4f, 15, 38, -3),
            new Key(8.55f, -21, -16, 41), new Key(10.5f, 20, -45, -27),
            new Key(12.4f, -15, -21, 14), new Key(15.7f, -31, 16, -12),
            new Key(17.7f, -9, 10, -4), new Key(20.9f, -35, 44, 12),
            new Key(23.4f, -7, 28, -7), new Key(26.8f, 11, 38, -12),
            new Key(28.5f, 25, 9, 42), new Key(30.7f, -11, -34, 92),
            new Key(31.4f, 24, -20, 133), new Key(31.78f, -12, 10, 320),
            new Key(32.20f, 20, -21, 494), new Key(32.76f, -12, 5, 669),
            new Key(33.35f, 6, 10, 714), new Key(35.7f, -3, 8, 715),
            new Key(37.6f, 0, 8, 720)
        };

        private static readonly Key[] Yellow =
        {
            new Key(0, .61f, .64f, 0), new Key(2.7f, .606f, .646f, .9f),
            new Key(5.8f, .617f, .653f, 1), new Key(7.4f, .608f, .649f, 1),
            new Key(8.2f, .626f, .568f, 0), new Key(10.8f, .47f, .19f, 0),
            new Key(11.9f, .483f, .403f, 1), new Key(13.2f, .5f, .423f, 1),
            new Key(15.7f, .531f, .213f, 1), new Key(17.4f, .577f, .424f, .95f),
            new Key(18.9f, .63f, .325f, 1), new Key(20.65f, .664f, .139f, .7f),
            new Key(22.3f, .663f, .108f, 0), new Key(37.6f, .663f, .108f, 0)
        };

        private static readonly Key[] Red =
        {
            new Key(0, .67f, .45f, 0), new Key(21.4f, .684f, .414f, 0),
            new Key(23.8f, .65f, .452f, .75f), new Key(26.8f, .617f, .503f, 1),
            new Key(28.0f, .551f, .549f, .96f), new Key(29.5f, .566f, .69f, .85f),
            new Key(31.4f, .428f, .775f, .6f), new Key(32.9f, .327f, .879f, 0),
            new Key(37.6f, .327f, .879f, 0)
        };

        private static readonly FilmCurve ActorMotion = new FilmCurve(Actor), CameraMotion = new FilmCurve(Camera),
            ActorAttitude = new FilmCurve(Attitude), AmberFlight = new FilmCurve(Yellow), ScarletFlight = new FilmCurve(Red);

        public static Shot Sample(float t)
        {
            float ruin = Window(t, 7.95f, 9.9f, 14.1f, 16.4f);
            float sky = Window(t, 19.05f, 22.15f, 28.45f, 31.05f);
            float ground = 1 - Mathf.Max(ruin, sky);
            float rush = Mathf.Max(Window(t, 7.55f, 8.35f, 10.5f, 11.9f),
                         Mathf.Max(Window(t, 12.3f, 14.45f, 15.65f, 17.5f),
                                   Window(t, 27.15f, 28.25f, 30.43f, 31.7f)));
            return new Shot(ActorMotion.Evaluate(t), CameraMotion.Evaluate(t), ActorAttitude.Evaluate(t), ruin, sky, ground, rush);
        }

        public static Vector3 Firefly(float t, bool red) => (red ? ScarletFlight : AmberFlight).Evaluate(t);
        public static float Contact(float t, bool red) => red
            ? Window(t, 26.0f, OpeningTiming.RedContact, 29.18f, 30.25f)
            : Mathf.Max(Window(t, 6.24f, 7.02f, 7.27f, 7.56f),
                        Window(t, 11.9f, 12.68f, 15.1f, 16.44f) * .85f);

        public static float NearHand(float t, bool red) => red
            ? Window(t, 24.28f, 26.54f, 29.28f, 30.65f)
            : Mathf.Max(Window(t, 5.12f, 6.88f, 7.28f, 7.79f),
                        Window(t, 11.41f, 12.96f, 15.5f, 17.02f) * .77f);

        public static float Window(float t, float from, float inBy, float outBy, float to) =>
            Ease(from, inBy, t) * (1 - Ease(outBy, to, t));

        public static float Ease(float a, float b, float v) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, v));

        // Unity's animation curves keep editable, time-normalized tangent data for each track.
        private sealed class FilmCurve
        {
            private readonly AnimationCurve x, y, z;

            public FilmCurve(Key[] keys)
            {
                var xs = new Keyframe[keys.Length];
                var ys = new Keyframe[keys.Length];
                var zs = new Keyframe[keys.Length];
                for (int i = 0; i < keys.Length; i++)
                {
                    Key before = keys[Mathf.Max(0, i - 1)], key = keys[i], after = keys[Mathf.Min(keys.Length - 1, i + 1)];
                    Vector3 tangent = (after.Value - before.Value) * (.68f / (after.Time - before.Time));
                    xs[i] = new Keyframe(key.Time, key.Value.x, tangent.x, tangent.x);
                    ys[i] = new Keyframe(key.Time, key.Value.y, tangent.y, tangent.y);
                    zs[i] = new Keyframe(key.Time, key.Value.z, tangent.z, tangent.z);
                }
                x = new AnimationCurve(xs);
                y = new AnimationCurve(ys);
                z = new AnimationCurve(zs);
            }

            public Vector3 Evaluate(float t) => new Vector3(x.Evaluate(t), y.Evaluate(t), z.Evaluate(t));
        }
    }
}
