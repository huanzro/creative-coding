using System;

namespace Echoes
{
    public sealed class ForestLampShow
    {
        private enum Pulse { None, Amber, Red, Swallowed }

        private static readonly LampRgb WarmYellow = new LampRgb(27, 17, 0);
        private static readonly LampRgb SoftRed = new LampRgb(25, 1, 0);
        private static readonly LampRgb[] PlantColors =
        {
            new LampRgb(25, 15, 2), new LampRgb(3, 22, 15), new LampRgb(3, 12, 27),
            new LampRgb(20, 5, 25), new LampRgb(27, 5, 12)
        };
        private const float ColorBeat = .24f;
        private Pulse pulse;
        private bool swallowing;
        private int cue = -1;
        private float pulseAt, plantAt = -1, plantUntil = -1;

        public void Reset(int currentCue = -1, bool alreadySwallowing = false)
        {
            cue = currentCue;
            pulse = Pulse.None;
            swallowing = alreadySwallowing;
            plantAt = plantUntil = -1;
        }

        public LampRgb Sample(Journey journey, float now)
        {
            if (!journey.InForest)
            {
                Reset(journey.CueId);
                return default;
            }
            if (now - pulseAt >= Duration(pulse)) pulse = Pulse.None;
            if (cue != journey.CueId)
            {
                cue = journey.CueId;
                if (journey.CueAge < .20f)
                {
                    if (journey.Cue == JourneyCue.ForestAmber && pulse != Pulse.Red && pulse != Pulse.Swallowed)
                        BeginPulse(Pulse.Amber, now);
                    if (journey.Cue == JourneyCue.ForestScarlet) BeginPulse(Pulse.Red, now);
                    if (journey.Cue == JourneyCue.Plant) plantUntil = now + .74f;
                }
            }
            if (journey.ForestSwallowing && !swallowing) BeginPulse(Pulse.Swallowed, now);
            swallowing = journey.ForestSwallowing;

            bool charging = journey.Phase == Stage.ForestPlanting && journey.ForestPlantCharge > 0;
            bool planting = charging || now < plantUntil;
            if (!planting) plantAt = -1;
            else if (plantAt < 0) plantAt = now;

            float age = now - pulseAt;
            if (pulse == Pulse.Amber) return WarmYellow.Dim(Glow(age, .55f));
            if (pulse == Pulse.Red) return SoftRed.Dim(Glow(age, .52f));
            if (pulse == Pulse.Swallowed)
                return SoftRed.Dim(Math.Max(Glow(age, .36f), .70f * Glow(age - .37f, .38f)));
            if (!planting) return default;

            float beat = (now - plantAt) / ColorBeat;
            int color = (int)beat;
            float shimmer = .36f + .64f * (float)Math.Sin((beat - color) * Math.PI);
            float fade = charging ? 1 : Smooth((plantUntil - now) / .23f);
            return PlantColors[color % PlantColors.Length].Dim(shimmer * fade);
        }

        private void BeginPulse(Pulse effect, float now)
        {
            pulse = effect;
            pulseAt = now;
        }

        private static float Duration(Pulse effect) => effect == Pulse.Amber ? .55f :
            effect == Pulse.Red ? .52f : effect == Pulse.Swallowed ? .75f : 0;

        private static float Glow(float age, float duration) =>
            Smooth(age / .075f) * Smooth((duration - age) / (duration - .12f));

        private static float Smooth(float value)
        {
            float t = Math.Max(0, Math.Min(1, value));
            return t * t * (3 - 2 * t);
        }
    }
}
