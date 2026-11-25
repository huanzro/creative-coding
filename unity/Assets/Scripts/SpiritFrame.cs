using System;
using System.Collections.Generic;

namespace Echoes
{
    [Serializable]
    public struct ForestFlyView
    {
        public int id;
        public bool red;
        public float lane, depth, height, visibility;

        public static ForestFlyView Read(ForestFeature feature, float time)
        {
            return new ForestFlyView {
                id = feature.Id, red = feature.Kind == ForestFeatureKind.Scarlet,
                lane = feature.LaneAt(time), depth = feature.DepthAt(time), height = feature.AltitudeAt(time)
            };
        }

        public static float Approach(float depthGap, float laneGap, float heightGap, float velocity)
        {
            float far = Math.Max(2.3f, velocity * 1.23f);
            if (depthGap < -2.5f || depthGap > far || Math.Abs(laneGap) > 1.7f || Math.Abs(heightGap) > .63f) return 0;
            float edge = Math.Min(1, Math.Min((far - depthGap) / Math.Max(1.5f, far * .33f), (depthGap + 2.5f) / 1.5f));
            return edge * edge * (3 - 2 * edge);
        }

        public float ScarletPulse(float time)
        {
            float cycle = time + id * .11f;
            cycle -= (float)Math.Floor(cycle);
            return Flash(cycle,.085f,.083f) + Flash(cycle,.31f,.083f);
        }

        private static float Flash(float cycle,float center,float width)
        {
            float shape = Math.Max(0,1-Math.Abs(cycle-center)/width);
            return shape*shape*(3-2*shape);
        }
    }

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
        public int forestReserve, forestEvent, forestDoor;
        public float forestEventAge, forestFall, forestDoorAge;
        public float forestContactLane, forestContactDepth, forestContactHeight;
        public ForestFlyView[] forestFlies;
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
            forestReserve = journey.InForest ? journey.ForestLights : 0, forestEvent = (int)journey.Cue,
            forestEventAge = Math.Min(journey.CueAge, 10),
            forestFall = journey.Phase == Stage.ForestFalling ? journey.Progress : -1,
            forestDoor = journey.Phase == Stage.ForestDoor ? journey.ForestDoor : -1,
            forestDoorAge = journey.Phase == Stage.ForestDoor ? journey.Age : -1,
            forestContactLane = journey.ForestContactLane - journey.ForestLane,
            forestContactDepth = journey.ForestContactDepth - journey.Distance,
            forestContactHeight = journey.ForestContactAltitude - journey.Flight.Height,
            forestFlies = NearbyFlies(journey),
            releaseAge = Age(journey.PoseTime, journey.LastRelease, 2.2f),
            turnAge = Age(journey.PoseTime, journey.LastSpin, 1.35f),
            touchAge = Age(journey.PoseTime, journey.LastTouch, .95f)
        };

        private static ForestFlyView[] NearbyFlies(Journey journey)
        {
            if (!journey.InForest) return null;
            List<ForestFlyView> nearby = null;
            foreach (ForestFeature feature in journey.ForestFeatures)
            {
                if (feature.Collected || (feature.Kind != ForestFeatureKind.Amber && feature.Kind != ForestFeatureKind.Scarlet)) continue;
                if (journey.Section == ForestSection.Vestibule && journey.SectionAt(feature.Depth)==ForestSection.Brain) continue;
                ForestFlyView fly = ForestFlyView.Read(feature, journey.Time);
                fly.depth -= journey.Distance;
                fly.lane -= journey.ForestLane;
                fly.height -= journey.Flight.Height;
                fly.visibility = ForestFlyView.Approach(fly.depth, fly.lane, fly.height, journey.ForestFlightSpeed);
                if (fly.visibility <= 0) continue;
                if (nearby == null) nearby = new List<ForestFlyView>(3);
                nearby.Add(fly);
            }
            return nearby?.ToArray();
        }

        private static float PlantAge(float time) => time >= 0 && time < 1.35f ? time + .001f : 0;

        private static float Age(float now, float start, float lifetime)
        {
            float age = now - start;
            return age >= 0 && age < lifetime ? age : -1;
        }
    }
}
