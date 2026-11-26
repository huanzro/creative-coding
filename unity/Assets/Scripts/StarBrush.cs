using UnityEngine;

namespace Echoes
{
    internal struct StarBrush
    {
        private const float Deadband = .0011f;
        private Vector2 latest, display, velocity;
        private double sampleAt;
        private float cadence;
        private bool active;

        public StarTip Follow(StarTip input, double captureTime, double time, float dt)
        {
            if (!input.Valid)
            {
                active = false;
                return default;
            }
            var point = new Vector2(input.X, input.Y);
            if (!active)
            {
                active = true;
                latest = display = point;
                sampleAt = captureTime;
                cadence = 0;
                velocity = Vector2.zero;
                return input;
            }

            if (captureTime > sampleAt)
            {
                var delta = point - latest;
                float interval = (float)(captureTime - sampleAt);
                cadence = interval >= .005f && interval <= .13f && delta.sqrMagnitude < .17f * .17f ? interval : 0;
                velocity = cadence > 0 && delta.sqrMagnitude > Deadband * Deadband
                    ? Vector2.ClampMagnitude(delta / interval, 3.1f) : Vector2.zero;
                latest = point;
                sampleAt = captureTime;
            }

            // Use the latest capture immediately and predict only between nearby frames.
            float age = Mathf.Max(0, (float)(time - sampleAt));
            float lead = Mathf.Min(age + .007f, Mathf.Min(.048f, cadence * .90f));
            lead *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(cadence + .015f, cadence + .09f, age));
            Vector2 target = latest + Vector2.ClampMagnitude(velocity * lead, .052f);
            target.x = Mathf.Clamp01(target.x);
            target.y = Mathf.Clamp01(target.y);
            Vector2 error = target - display;
            if (error.sqrMagnitude > Deadband * Deadband)
            {
                float response = Mathf.Lerp(57f, 147f, Mathf.Clamp01(velocity.magnitude * 2f + error.sqrMagnitude * 400f));
                display += error * (1 - Mathf.Exp(-dt * response));
            }
            return new StarTip(display.x, display.y);
        }
    }
}
