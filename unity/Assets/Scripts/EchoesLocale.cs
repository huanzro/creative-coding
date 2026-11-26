using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    public static class EchoesLocale
    {
        public static bool English { get; set; }
        public static string GameTitle => T("光的回响", "Echoes of Light");
        public static string T(string chinese, string english) => English ? english : chinese;
        private static Dictionary<string, string> rows;
        private static List<KeyValuePair<string, string>> fragments;

        public static string Text(string value)
        {
            if (!English || string.IsNullOrEmpty(value) || !HasChinese(value)) return value;
            if (rows == null) ReadTable();
            if (rows.TryGetValue(value, out string exact)) return exact;
            foreach (var entry in fragments)
                if (value.Contains(entry.Key)) value = value.Replace(entry.Key, entry.Value);
            return rows.TryGetValue(value, out exact) ? exact : value;
        }

        private static bool HasChinese(string value)
        {
            foreach (char c in value)
                if (c >= '\u3400' && c <= '\u9fff') return true;
            return false;
        }

        private static void ReadTable()
        {
            rows = new Dictionary<string, string>(StringComparer.Ordinal);
            fragments = new List<KeyValuePair<string, string>>();
            var table = Resources.Load<TextAsset>("English");
            if (table == null) return;
            foreach (string line in table.text.Split('\n'))
            {
                int split = line.IndexOf('|');
                if (split <= 0) continue;
                string key = line.Substring(0, split);
                string translation = line.Substring(split + 1).TrimEnd('\r');
                if (key[0] == '*')
                {
                    key = key.Substring(1);
                    fragments.Add(new KeyValuePair<string, string>(key, translation));
                }
                rows[key] = translation;
            }
            fragments.Sort((a,b) => b.Key.Length.CompareTo(a.Key.Length));
        }
    }

    internal static class LocaleGui
    {
        public static void Label(Rect area, string value, GUIStyle style) => GUI.Label(area, EchoesLocale.Text(value), style);
        public static bool Button(Rect area, string value, GUIStyle style) => GUI.Button(area, EchoesLocale.Text(value), style);
        public static bool Toggle(Rect area, bool enabled, string value) => GUI.Toggle(area, enabled, EchoesLocale.Text(value));
        public static bool RepeatButton(Rect area, string value, GUIStyle style) => GUI.RepeatButton(area, EchoesLocale.Text(value), style);
        public static int SelectionGrid(Rect area, int selection, string[] labels, int columns, GUIStyle style) =>
            GUI.SelectionGrid(area, selection, EchoesLocale.English ? Array.ConvertAll(labels, EchoesLocale.Text) : labels, columns, style);
    }
}
