using System;

namespace Echoes
{
    [Serializable]
    public struct SpiritFrame
    {
        public int chapter;
        public float time, phase, voice, light, charge, releaseAge, turnAge;
        public bool closed;

        public static SpiritFrame Read(Journey journey) => new SpiritFrame {
            chapter = journey.Chapter, time = journey.Time, phase = journey.DancePhase,
            voice = journey.Voice, light = journey.Light, charge = journey.Charge,
            closed = journey.Gesture == Hand.Fist,
            releaseAge = Age(journey.Time, journey.LastRelease, 2.2f),
            turnAge = Age(journey.Time, journey.LastSpin, 1.35f)
        };

        private static float Age(float now, float start, float lifetime)
        {
            float age = now - start;
            return age >= 0 && age < lifetime ? age : -1;
        }
    }
}
