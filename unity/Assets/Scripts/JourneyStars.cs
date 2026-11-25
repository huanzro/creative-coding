/* ARTISTIC RATIONALE
 * A touch opens one of the machine's patterns. An index finger leaves the visitor's own line.
 * Their voice carries the brush into the sky, so both kinds of mark occupy the same space.
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
        public const float StarVoiceFloor = .075f, StarMaxSpeed = 1.36f;
        private readonly Queue<int> starStamps = new Queue<int>(12);
        private readonly int[] lastStarMotif = { -1, -1 };
        private readonly float[] lastStampAt = new float[2];

        public bool InStars => Chapter == 2 && Lesson < 0;
        public int StarPaper { get; private set; }
        public StarTip StarPointer { get; private set; }
        public float StarSpeed { get; private set; }
        public int StarLastStamp { get; private set; }
        public string StarInstruction => StarPointer.Valid
            ? "食指绘制中。发声向星海深处飞，收起食指即可提笔。"
            : "只伸出食指指向镜头，移动指尖画光。A/B 可从精灵指尖放出图案。";

        public void SetStarPointer(StarTip pointer) => StarPointer = InStars ? pointer : default;

        internal bool TryTakeStarStamp(out int motif)
        {
            motif = starStamps.Count > 0 ? starStamps.Dequeue() : 0;
            return motif != 0;
        }

        private void StartStar()
        {
            StarPaper++;
            StarSpeed = 0;
            StarLastStamp = 0;
            StarPointer = default;
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
            if (starStamps.Count >= 12 || Time - lastStampAt[option] < .115f) return false;
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
            float pressure = Clamp((Voice - StarVoiceFloor) / (1 - StarVoiceFloor));
            float target = StarMaxSpeed * (float)Math.Pow(pressure, .80);
            float difference = target - StarSpeed;
            float step = dt * (difference >= 0 ? 4.8f : 7.6f);
            StarSpeed += Math.Max(-step, Math.Min(step, difference));
            Distance += StarSpeed * dt;
            Progress = StarSpeed / StarMaxSpeed;
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
            Distance = checkpoint.starDepth;
            Time = PoseTime = checkpoint.starTime;
        }
    }
}
