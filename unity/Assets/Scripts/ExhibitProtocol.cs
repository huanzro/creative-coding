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

        public static string Output(int sequence, float first, float second, int fan, bool fanSupported,
            bool forestPalette = false)
        {
            string fields = string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} 0 {3} {4} {5} -1",
                sequence & 65535, Brightness(first, 12), Brightness(first, forestPalette ? 9 : 4),
                forestPalette ? Brightness(second, 12) : 0,
                forestPalette ? 0 : Brightness(second, 3), forestPalette ? 0 : Brightness(second, 12));
            if (!fanSupported) return "EOL3 SET " + fields + "\n";
            return "EOL3 IO " + fields + " " + Math.Max(0, Math.Min(166, fan)).ToString(CultureInfo.InvariantCulture) + "\n";
        }

        private static int Brightness(float value, int maximum) =>
            (int)Math.Round(Math.Max(0, Math.Min(1, value)) * maximum);
    }
}
