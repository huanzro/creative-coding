using UnityEngine;

namespace Echoes
{
    internal struct StarBrush
    {
        private const float Deadband = .0013f;
        private Vector2 previous, latest, display;
        private float sampleAt, interval, delay;

        public void Reset(Vector2 point, float time)
        {
            previous = latest = display = point;
            sampleAt = time;
            interval = delay = 0;
        }

        public Vector2 Follow(Vector2 sample, float time, float dt)
        {
            float displacement = Vector2.Distance(sample, latest);
            if (displacement > Deadband)
            {
                float gap = time - sampleAt;
                previous = latest;
                latest = sample;
                sampleAt = time;
                // A large correction or the first frame after a stop should follow directly.
                interval = gap >= .008f && gap <= .115f && displacement < .105f ? gap : 0;
                delay = Mathf.Min(.038f, interval * .76f);
            }

            Vector2 target = latest;
            if (interval > 0)
            {
                float phase = time - sampleAt - delay;
                // Reconstruct motion between camera samples at the display frame rate.
                float lead = Mathf.Min(.017f, Mathf.Max(0, phase));
                lead *= 1 - Mathf.InverseLerp(.018f, .060f, phase);
                target = phase < 0 ? Vector2.Lerp(previous, latest, 1 + phase / interval) :
                    latest + Vector2.ClampMagnitude((latest - previous) * (lead / interval), .009f);
                target.x = Mathf.Clamp01(target.x);
                target.y = Mathf.Clamp01(target.y);
            }
            Vector2 delta = target - display;
            float distance = delta.magnitude;
            if (distance <= Deadband) return display;
            float response = Mathf.Lerp(42, 68, Mathf.Clamp01(distance * 20));
            display += delta * ((1 - Mathf.Exp(-dt * response)) * (1 - Deadband / distance));
            return display;
        }
    }
}
