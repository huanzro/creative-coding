using System;

namespace Echoes
{
    [Serializable]
    public struct SpiritFrame
    {
        public int chapter, touch;
        public Hand hand;
        public float time, motionTime, phase, voice, light, charge, releaseAge, turnAge, touchAge;
        public float altitude, verticalSpeed, glide, openingTime, releaseAltitude;
        public bool closed;

        public static SpiritFrame Read(Journey journey) => new SpiritFrame {
            chapter = journey.Chapter, time = journey.Time, motionTime = journey.PoseTime, phase = journey.DancePhase,
            voice = journey.PoseVoice, light = journey.Light, charge = journey.Charge,
            touch = journey.TouchOption, hand = journey.Gesture,
            altitude = journey.Flight.Height, verticalSpeed = journey.Flight.Speed, glide = journey.Flight.Glide,
            releaseAltitude = journey.ReleaseAltitude,
            openingTime = journey.IsOpening ? journey.OpeningTime : -1,
            closed = journey.Gesture == Hand.Fist,
            releaseAge = Age(journey.PoseTime, journey.LastRelease, 2.2f),
            turnAge = Age(journey.PoseTime, journey.LastSpin, 1.35f),
            touchAge = Age(journey.PoseTime, journey.LastTouch, .95f)
        };

        private static float Age(float now, float start, float lifetime)
        {
            float age = now - start;
            return age >= 0 && age < lifetime ? age : -1;
        }
    }
}
