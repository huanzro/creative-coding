using System;
using System.Globalization;

namespace Echoes
{
    public readonly struct LampRgb
    {
        public readonly int Red, Green, Blue;
        public bool Lit => (Red | Green | Blue) != 0;

        public LampRgb(int red, int green, int blue)
        {
            Red = Channel(red);
            Green = Channel(green);
            Blue = Channel(blue);
        }

        public LampRgb Dim(float amount)
        {
            float level = Math.Max(0, Math.Min(1, amount));
            return new LampRgb((int)Math.Round(Red * level), (int)Math.Round(Green * level), (int)Math.Round(Blue * level));
        }

        internal LampRgb LimitPeak(int maximum)
        {
            int peak = Math.Max(Red, Math.Max(Green, Blue));
            return peak <= maximum ? this : Dim(maximum / (float)peak);
        }

        private static int Channel(int value) => Math.Max(0, Math.Min(64, value));
    }

    public static class ExhibitProtocol
    {
        internal static readonly LampRgb LeftTouch = new LampRgb(12, 4, 0);
        internal static readonly LampRgb RightTouch = new LampRgb(0, 3, 12);
        private static readonly LampRgb ForestAmberLegacy = new LampRgb(12, 9, 0);
        private static readonly LampRgb ForestRedLegacy = new LampRgb(12, 0, 0);

        public static bool ReadInputs(string line, out int input, out int enabled)
        {
            input = enabled = 0;
            if (line.Length != 11 || !line.StartsWith("EOL3 IN ", StringComparison.Ordinal) || line[9] != ' ')
                return false;
            input = line[8] - '0';
            enabled = line[10] - '0';
            return input >= 0 && input <= 7 && enabled >= 0 && enabled <= 7;
        }

        public static bool ReadAck(string line, out int sequence)
        {
            sequence = 0;
            if (!line.StartsWith("EOL3 ACK ", StringComparison.Ordinal) ||
                !ushort.TryParse(line.Substring(9), NumberStyles.None, CultureInfo.InvariantCulture, out ushort value)) return false;
            sequence = value;
            return true;
        }

        public static string Output(int sequence, float first, float second, int fan, bool fanSupported,
            bool forestPalette = false) =>
            Output(sequence, (forestPalette ? ForestAmberLegacy : LeftTouch).Dim(first),
                (forestPalette ? ForestRedLegacy : RightTouch).Dim(second), fan, fanSupported);

        public static string Output(int sequence, LampRgb first, LampRgb second, int fan, bool fanSupported)
        {
            // The wired bench firmware accepts SET with a limit of 12 per channel.
            if (!fanSupported)
            {
                first = first.LimitPeak(12);
                second = second.LimitPeak(12);
            }
            string fields = string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} {6} -1",
                sequence & 65535, first.Red, first.Green, first.Blue, second.Red, second.Green, second.Blue);
            if (!fanSupported) return "EOL3 SET " + fields + "\n";
            return "EOL3 IO " + fields + " " + Math.Max(0, Math.Min(166, fan)).ToString(CultureInfo.InvariantCulture) + "\n";
        }
    }
}
