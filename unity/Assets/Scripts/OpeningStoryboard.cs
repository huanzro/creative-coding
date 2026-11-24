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
            new Key(0, .37f, .705f, 1.51f),
            new Key(2.5f, .43f, .696f, 1.62f),
            new Key(4.2f, .482f, .695f, 1.73f),
            new Key(6.8f, .520f, .647f, 2.19f),
            new Key(7.4f, .516f, .680f, 1.99f),
            new Key(8.8f, .545f, .845f, 1.36f),
            new Key(10.3f, .518f, .635f, 1.11f),
            new Key(11.8f, .476f, .671f, 1.16f),
            new Key(13.8f, .510f, .559f, 1.30f),
            new Key(15.8f, .529f, .387f, 1.42f),
            new Key(17.5f, .498f, .556f, 1.45f),
            new Key(18.8f, .532f, .585f, 1.47f),
            new Key(21.2f, .465f, .561f, 1.10f),
            new Key(23.8f, .463f, .542f, 1.26f),
            new Key(26.7f, .531f, .525f, 2.24f),
            new Key(27.4f, .544f, .553f, 1.79f),
            new Key(28.9f, .568f, .66f, 1.49f),
            new Key(30.3f, .538f, .759f, 1.54f),
            new Key(31.4f, .428f, .776f, 1.59f),
            new Key(32.5f, .511f, .794f, 1.47f),
            new Key(33.5f, .559f, .783f, 1.43f),
            new Key(35.3f, .529f, .770f, 1.30f),
            new Key(37.6f, .50f, .771f, 1)
        };

        private static readonly Key[] Camera =
        {
            new Key(0, -.53f, .52f, .09f), new Key(5.5f, -.12f, .53f, .27f),
            new Key(7.4f, .04f, .53f, .19f), new Key(9.5f, .16f, -.70f, .23f),
            new Key(11.5f, -.08f, -.48f, .14f), new Key(15.5f, .21f, .70f, .24f),
            new Key(17.8f, .09f, .275f, .13f), new Key(20.75f, -.13f, .76f, .27f),
            new Key(23.0f, -.15f, .40f, .11f), new Key(26.4f, .19f, .32f, .22f),
            new Key(29.7f, .23f, -.80f, .25f), new Key(31.45f, -.25f, .53f, .21f),
            new Key(33.5f, .37f, .47f, .17f), new Key(37.6f, 0, .22f, .07f)
        };

        private static readonly Key[] Attitude =
        {
            new Key(0, 10, 26, 3), new Key(2.9f, 13, 39, -9),
            new Key(5.8f, 21, 41, -8), new Key(7.4f, 15, 38, -3),
            new Key(8.55f, -21, -16, 41), new Key(10.5f, 20, -45, -27),
            new Key(12.4f, -15, -21, 14), new Key(15.7f, -31, 16, -12),
            new Key(17.7f, -9, 10, -4), new Key(20.9f, -35, 44, 12),
            new Key(23.4f, -8, 42, -8), new Key(26.8f, 14, 40, -15),
            new Key(28.5f, 27, 9, 48), new Key(30.7f, -9, -29, 115),
            new Key(31.4f, 26, 11, 244), new Key(32.38f, -22, -6, 628),
            new Key(33.5f, 18, 4, 1069), new Key(35.7f, -3, -8, 1075),
            new Key(37.6f, 0, 8, 1080)
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

        public static Shot Sample(float t)
        {
            float ruin = Window(t, 7.95f, 9.9f, 14.1f, 16.4f);
            float sky = Window(t, 19.05f, 22.15f, 28.45f, 31.05f);
            float ground = 1 - Mathf.Max(ruin, sky);
            float rush = Mathf.Max(Window(t, 7.55f, 8.35f, 10.5f, 11.9f),
                         Mathf.Max(Window(t, 12.3f, 14.45f, 15.65f, 17.5f),
                                   Window(t, 27.15f, 28.25f, 30.43f, 31.7f)));
            return new Shot(Sample(Actor, t), Sample(Camera, t), Sample(Attitude, t), ruin, sky, ground, rush);
        }

        public static Vector3 Firefly(float t, bool red) => Sample(red ? Red : Yellow, t);
        public static float Window(float t, float from, float inBy, float outBy, float to) =>
            Ease(from, inBy, t) * (1 - Ease(outBy, to, t));

        public static float Ease(float a, float b, float v) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, v));

        private static Vector3 Sample(Key[] keys, float t)
        {
            if (t <= keys[0].Time) return keys[0].Value;
            for (int i = 1; i < keys.Length; i++)
            {
                Key a = keys[i - 1], b = keys[i];
                if (t > b.Time) continue;
                Key before = keys[Mathf.Max(0, i - 2)], after = keys[Mathf.Min(keys.Length - 1, i + 1)];
                float span = b.Time - a.Time;
                Vector3 slopeA = (b.Value - before.Value) * (.68f * span / (b.Time - before.Time));
                Vector3 slopeB = (after.Value - a.Value) * (.68f * span / (after.Time - a.Time));
                float x = (t - a.Time) / span;
                float square = x * x, cube = square * x;
                return (2 * cube - 3 * square + 1) * a.Value + (cube - 2 * square + x) * slopeA +
                       (-2 * cube + 3 * square) * b.Value + (cube - square) * slopeB;
            }
            return keys[keys.Length - 1].Value;
        }
    }
}
