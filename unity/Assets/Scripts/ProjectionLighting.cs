using UnityEngine;

namespace Echoes
{
    internal static class ProjectionLighting
    {
        public const float MinimumBrightness = .50f, MaximumBrightness = 1.35f;
        public const float MinimumFalloff = .55f, MaximumFalloff = 2f;
        public const float DefaultValue = 1f, CoreGain = .44f;

        public static float ClampBrightness(float value) =>
            float.IsNaN(value) ? DefaultValue : Mathf.Clamp(value,MinimumBrightness,MaximumBrightness);

        public static float ClampFalloff(float value) =>
            float.IsNaN(value) ? DefaultValue : Mathf.Clamp(value,MinimumFalloff,MaximumFalloff);

        public static float CoreRadius(float actorPixels) => Mathf.Clamp(actorPixels * .32f / 720, .075f, .22f);
        public static float Spread(float falloff) => .56f / falloff;

        // Neutral gray input for the brightness curve shown in the calibration panel.
        public static float Tone(float input, float brightness)
        {
            float headroom = 1-input;
            return input * (Mathf.Min(1,brightness) + Mathf.Max(0,brightness-1)*headroom*headroom);
        }

        public static float RadialGain(float distance, float core, float falloff) =>
            Mathf.SmoothStep(CoreGain, 1, Mathf.InverseLerp(core, core + Spread(falloff), distance));
    }
}
