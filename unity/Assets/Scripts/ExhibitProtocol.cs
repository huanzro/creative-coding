using System;
using System.Globalization;

namespace Echoes
{
    public static class ExhibitProtocol
    {
        public static bool ReadInputs(string line, out int input, out int enabled)
        {
            input = enabled = 0;
            if (line.Length != 11 || !line.StartsWith("EOL3 IN ", StringComparison.Ordinal) || line[9] != ' ')
                return false;
            input = line[8] - '0';
            enabled = line[10] - '0';
            return input >= 0 && input <= 7 && enabled >= 0 && enabled <= 7;
        }

        public static string Output(int sequence, float warm, float blue, int fan, bool fanSupported)
        {
            string fields = string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} 0 0 {3} {4} -1",
                sequence & 65535, Brightness(warm, 12), Brightness(warm, 4), Brightness(blue, 3), Brightness(blue, 12));
            if (!fanSupported) return "EOL3 SET " + fields + "\n";
            return "EOL3 IO " + fields + " " + Math.Max(0, Math.Min(166, fan)).ToString(CultureInfo.InvariantCulture) + "\n";
        }

        private static int Brightness(float value, int maximum) =>
            (int)Math.Round(Math.Max(0, Math.Min(1, value)) * maximum);
    }
}
