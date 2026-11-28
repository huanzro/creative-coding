/* ARTISTIC RATIONALE
 * The visitor steers a continuous line with their index finger. A touch grows a pattern from the line.
 * The brush travels forward at its cruising speed and picks up speed with the visitor's voice.
 */
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Echoes
{
    public readonly struct StarTip
    {
        public readonly float X, Y;
        public readonly bool Valid;

        public StarTip(float x, float y)
        {
            X = x;
            Y = y;
            Valid = x >= 0 && x <= 1 && y >= 0 && y <= 1;
        }

        // From either camera bridge. Integer coordinates are in the mirrored camera frame.
        internal static bool Decode(string packet, out string state, out StarTip tip)
        {
            tip = default;
            state = packet;
            if (string.IsNullOrEmpty(packet) || packet.Length > 40) return false;
            int separator = packet.IndexOf('/');
            if (separator < 0) return true;
            int endX = packet.IndexOf('/', separator + 1);
            if (endX <= separator + 1 ||
                !int.TryParse(packet.Substring(separator + 1, endX - separator - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int x) ||
                !int.TryParse(packet.Substring(endX + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int y) ||
                (uint)x > 10000 || (uint)y > 10000) return false;
            state = packet.Substring(0, separator);
            tip = new StarTip(x / 10000f, y / 10000f);
            return true;
        }
    }

    internal static class StarMotifs
    {
        public const int Count = 8, PerGroup = 4;
        public static bool Valid(int id) => id >= 1 && id <= Count;
    }

    public sealed partial class Journey
    {
        public const float MinimumStarCruiseSpeed = .25f, DefaultStarCruiseSpeed = .72f, MaximumStarCruiseSpeed = 1.65f;
        private const float StarVoiceFloor = .075f, StarAudioBoost = 1.50f;
        public const float StarMaxSpeed = MaximumStarCruiseSpeed + StarAudioBoost;
        public const float StarFinaleDistance = StarMaxSpeed * 30f;
        public const float StarFinaleDuration = 14f;
        private readonly Queue<int> starStamps = new Queue<int>(12);
        private readonly int[] lastStarMotif = { -1, -1 };
        private readonly float[] lastStampAt = new float[2];
        private float starCruiseSpeed = DefaultStarCruiseSpeed;
        private float starFinaleAge = -1;

        public bool InStars => Chapter == 2 && Lesson < 0;
        public int StarPaper { get; private set; }
        public StarTip StarPointer { get; private set; }
        public float StarSpeed { get; private set; }
        public bool StarFinaleActive => InStars && starFinaleAge >= 0;
        public float StarFinaleAge => StarFinaleActive ? starFinaleAge : -1;
        public float StarCruiseSpeed
        {
            get => starCruiseSpeed;
            set => starCruiseSpeed = TuningValue(value, MinimumStarCruiseSpeed, MaximumStarCruiseSpeed, DefaultStarCruiseSpeed);
        }
        public int StarLastStamp { get; private set; }
        public string StarInstruction
        {
            get
            {
                if (StarLastStamp > 0 && PoseTime - LastTouch < 1.35f)
                    return StarLastStamp <= StarMotifs.PerGroup ? "A · 流线正在展开" : "B · 星纹正在展开";
                return StarPointer.Valid ? "指尖引导光迹，发声加快飞行。按 A/B 让图案沿光迹长出。"
                    : "点按 A/B 放出图案。伸出食指可改变光迹走向。";
            }
        }

        public void SetStarPointer(StarTip pointer)
        {
            if (!StarFinaleActive) StarPointer = InStars ? pointer : default;
        }

        internal bool TryTakeStarStamp(out int motif)
        {
            motif = starStamps.Count > 0 ? starStamps.Dequeue() : 0;
            return motif != 0;
        }

        private void StartStar()
        {
            StarPaper++;
            starFinaleAge = -1;
            StarSpeed = starCruiseSpeed;
            StarLastStamp = 0;
            StarPointer = default;
            LastSpin = LastRelease = float.NegativeInfinity;
            starStamps.Clear();
            for (int i = 0; i < lastStarMotif.Length; i++)
            {
                lastStampAt[i] = float.NegativeInfinity;
                lastStarMotif[i] = -1;
            }
            Change(Stage.StarsCanvas, "");
            Started = true;
        }

        private bool StarsTouch(int option)
        {
            if (StarFinaleActive || starStamps.Count >= 12 || Time - lastStampAt[option] < .115f) return false;
            int previous = lastStarMotif[option];
            int chosen = forestRandom.Next(previous < 0 ? StarMotifs.PerGroup : StarMotifs.PerGroup - 1);
            if (previous >= 0 && chosen >= previous) chosen++;
            lastStarMotif[option] = chosen;
            lastStampAt[option] = Time;
            StarLastStamp = chosen + option * StarMotifs.PerGroup + 1;
            starStamps.Enqueue(StarLastStamp);
            Notify(JourneyCue.Star);
            return true;
        }

        private void UpdateStars(float dt)
        {
            if (StarFinaleActive)
            {
                starFinaleAge = Math.Min(StarFinaleDuration, starFinaleAge + dt);
                return;
            }
            float pressure = Clamp((Voice - StarVoiceFloor) / (1 - StarVoiceFloor));
            float target = starCruiseSpeed + StarAudioBoost * (float)Math.Pow(pressure, .80);
            float difference = target - StarSpeed;
            float step = dt * (difference >= 0 ? 4.8f : 7.6f);
            StarSpeed += Math.Max(-step, Math.Min(step, difference));
            Distance += StarSpeed * dt;
            if (Distance >= StarFinaleDistance)
            {
                starFinaleAge = (Distance - StarFinaleDistance) / Math.Max(.01f, StarSpeed);
                Distance = StarFinaleDistance;
                StarSpeed = 0;
                Revision++;
            }
            Progress = Distance / StarFinaleDistance;
        }

        private void RetryStar()
        {
            Distance = 0;
            LastTouch = float.NegativeInfinity;
            Voice = 0;
            StartStar();
        }

        private void RestoreStars(JourneyCheckpoint checkpoint)
        {
            if (checkpoint.version < 11) return;
            Distance = Math.Min(checkpoint.starDepth, StarFinaleDistance);
            Time = PoseTime = checkpoint.starTime;
            if (Distance >= StarFinaleDistance)
            {
                starFinaleAge = 0;
                StarSpeed = 0;
            }
            Progress = Distance / StarFinaleDistance;
        }
    }
}
