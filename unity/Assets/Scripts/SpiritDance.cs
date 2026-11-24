/* ARTISTIC RATIONALE
 * The original dance phrases alternate weight between the toes before each
 * reach. The body leads, each arm finishes the reach, and the cloth arrives late.
 */
using UnityEngine;

namespace Echoes
{
    internal static class SpiritDance
    {
        public const int TrackCount = 19;
        private static readonly float[] At = { 0, .11f, .19f, .29f, .38f, .48f, .57f, .70f, .80f, .91f };

        // From dist/spirit-dance.js: body x/y, lean, facing, gaze; elbow chains;
        // then left and right feet (stride, lift, toe, support).
        private static readonly float[,] Keys = {
            { 0, 0, .03f, -.04f, .02f, .03f, .18f, -.16f, .10f, .17f, -.05f, -8, 7, -.05f, .54f, 8, 18, .08f, .28f },
            { -9, 5, -.14f, -.44f, .10f, -.55f, .54f, -.33f, -.30f, .28f, .14f, -11, 4, -.10f, .78f, 20, 55, .18f, 0 },
            { -11, 3, -.20f, -.70f, .17f, -.28f, .84f, -.24f, -.42f, .67f, .17f, -18, 4, -.13f, .75f, 36, 70, .24f, 0 },
            { -7, -9, -.16f, -.82f, .22f, .78f, .72f, .20f, -.34f, .51f, -.15f, -16, 18, -.16f, .28f, 40, 81, .24f, 0 },
            { 1, -8, -.035f, -.18f, .06f, .61f, .26f, .27f, .48f, .32f, .14f, -38, 51, -.17f, 0, 23, 35, .05f, .22f },
            { 8, 1, .13f, .52f, -.10f, .05f, .04f, .02f, .40f, .16f, -.04f, -43, 62, -.15f, 0, 18, 5, .04f, .70f },
            { 11, 5, .20f, .76f, -.16f, -.48f, .30f, .14f, -.34f, .84f, -.26f, -36, 69, -.23f, 0, 15, 4, .09f, .79f },
            { 7, -9, .17f, .84f, -.22f, -.34f, .58f, -.10f, .77f, .74f, .25f, -37, 78, -.25f, 0, 23, 21, .12f, .28f },
            { -1, -7, .06f, .26f, -.12f, .28f, .22f, .20f, .59f, .24f, .19f, -26, 32, -.08f, .25f, 35, 45, .19f, 0 },
            { -5, 2, -.09f, -.36f, .10f, .24f, .39f, -.05f, -.37f, .50f, -.15f, -15, 6, -.06f, .64f, 16, 46, .18f, 0 }
        };

        public static void Sample(float phase, float[] sample)
        {
            phase = Mathf.Repeat(phase, 1);
            int count = At.Length;
            int index = count - 1;
            while (index > 0 && phase < At[index]) index--;
            int previous = (index + count - 1) % count;
            int next = (index + 1) % count;
            int after = (index + 2) % count;
            float span = Span(index, next);
            float before = Span(previous, index);
            float following = Span(next, after);
            float t = (phase - At[index]) / span;
            float square = t * t;
            float cube = square * t;
            for (int axis = 0; axis < TrackCount; axis++)
            {
                float start = Keys[index, axis];
                float end = Keys[next, axis];
                float speed = (end - start) / span;
                float m0 = span * Tangent((start - Keys[previous, axis]) / before, speed);
                float m1 = span * Tangent(speed, (Keys[after, axis] - end) / following);
                sample[axis] = start * (2 * cube - 3 * square + 1) + m0 * (cube - 2 * square + t)
                    + end * (-2 * cube + 3 * square) + m1 * (cube - square);
            }
        }

        private static float Span(int start, int end) =>
            end > start ? At[end] - At[start] : 1 + At[end] - At[start];

        private static float Tangent(float previous, float next) =>
            previous * next <= 0 ? 0 : 2 * previous * next / (previous + next);
    }
}
