using System;

namespace Echoes
{
    [Serializable]
    public struct SpiritFrame
    {
        public int chapter, touch, cast, lesson;
        public Hand hand;
        public FireflyKind fireflyResponse;
        public float fireflyPulse;
        public float time, motionTime, phase, voice, light, charge, releaseAge, turnAge, touchAge;
        public float altitude, verticalSpeed, glide, openingTime, releaseAltitude;
        public bool forest, forestAirborne, forestRescue;
        public ForestTrapKind forestTrap;
        public float forestLane, forestSteering, forestSnareAge, forestDescent, forestPlant;
        public bool closed;

        public static SpiritFrame Read(Journey journey, int cast = 0) => new SpiritFrame {
            chapter = journey.Chapter, time = journey.Time, motionTime = journey.PoseTime, phase = journey.DancePhase,
            cast = cast, lesson = journey.Lesson, fireflyResponse = journey.FireflyResponse, fireflyPulse = journey.FireflyPulse,
            voice = journey.PoseVoice, light = journey.Light, charge = journey.Charge,
            touch = journey.TouchOption, hand = journey.Gesture,
            altitude = journey.Flight.Height, verticalSpeed = journey.Flight.Speed, glide = journey.Flight.Glide,
            releaseAltitude = journey.ReleaseAltitude,
            openingTime = journey.IsOpening ? journey.OpeningTime : -1,
            closed = journey.Gesture == Hand.Fist,
            forest = journey.InForest, forestAirborne = journey.ForestAirborne,
            forestRescue = journey.ForestHasRescue,
            forestLane = journey.ForestLane, forestSteering = journey.ForestSteering, forestTrap = journey.ForestTrap,
            forestSnareAge = journey.Phase == Stage.ForestTrapped ? journey.Age : -1,
            forestDescent = journey.Phase == Stage.ForestTransition ? journey.Progress : -1,
            forestPlant = journey.Cue == JourneyCue.Plant && journey.InForest
                ? PlantAge(journey.CueAge) : 0,
            releaseAge = Age(journey.PoseTime, journey.LastRelease, 2.2f),
            turnAge = Age(journey.PoseTime, journey.LastSpin, 1.35f),
            touchAge = Age(journey.PoseTime, journey.LastTouch, .95f)
        };

        private static float PlantAge(float time) => time >= 0 && time < 1.35f ? time + .001f : 0;

        private static float Age(float now, float start, float lifetime)
        {
            float age = now - start;
            return age >= 0 && age < lifetime ? age : -1;
        }
    }
}
