using System;
using UnityEngine;

namespace Echoes
{
    internal sealed class StarTrailArt
    {
        [Serializable] private sealed class Library { public Entry[] trails; }
        [Serializable] private sealed class Palette { public string start, middle, end; }
        [Serializable] private sealed class Entry
        {
            public string id, group;
            public float lengthPixels;
            public Palette colors;
            public Vector2[] points;
        }

        private static StarTrailArt[] library;
        private static bool loaded;
        private readonly Vector2[] points;
        private readonly Color start, middle, end;
        public readonly float Duration;
        public readonly int Segments;
        public readonly float LateralBias;

        private StarTrailArt(Entry entry)
        {
            points = entry.points;
            ColorUtility.TryParseHtmlString(entry.colors.start, out start);
            ColorUtility.TryParseHtmlString(entry.colors.middle, out middle);
            ColorUtility.TryParseHtmlString(entry.colors.end, out end);
            Duration = Mathf.Clamp(1.05f + entry.lengthPixels * .00065f, 1.7f, 2.9f);
            Segments = Mathf.Clamp(points.Length / 3, 96, 256);
            float low = 0, high = 0;
            foreach (Vector2 point in points)
            {
                float offset = point.x - points[0].x;
                low = Mathf.Min(low, offset);
                high = Mathf.Max(high, offset);
            }
            LateralBias = low + high < 0 ? -1 : 1;
        }

        public static StarTrailArt Find(int id)
        {
            if (!StarMotifs.Valid(id)) return null;
            if (!loaded) Load();
            return library?[id - 1];
        }

        private static void Load()
        {
            loaded = true;
            var source = Resources.Load<TextAsset>("StarSea/LightTrails");
            var data = source == null ? null : JsonUtility.FromJson<Library>(source.text);
            if (data?.trails?.Length != StarMotifs.Count)
            {
                Debug.LogError("Star Sea requires all eight original light trails.");
                return;
            }
            var paths = new StarTrailArt[StarMotifs.Count];
            for (int i = 0; i < paths.Length; i++)
            {
                var path = data.trails[i];
                if (path?.points == null || path.points.Length < 2 || path.colors == null ||
                    path.group != (i < StarMotifs.PerGroup ? "A" : "B"))
                {
                    Debug.LogError("Star Sea found an invalid light trail at index " + i);
                    return;
                }
                paths[i] = new StarTrailArt(path);
            }
            library = paths;
        }

        public Color Pigment(float progress) => progress < .52f ? Color.Lerp(start, middle, progress / .52f)
            : Color.Lerp(middle, end, (progress - .52f) / .48f);

        public Vector3 Offset(float progress)
        {
            float index = Mathf.Clamp01(progress) * (points.Length - 1);
            int first = Mathf.Min(points.Length - 2, (int)index);
            Vector2 point = Vector2.LerpUnclamped(points[first], points[first + 1], index - first) - points[0];
            return new Vector3(point.x, -point.y * (640f / 1200f), .09f * Mathf.Sin(progress * Mathf.PI) + .16f * progress);
        }
    }
}
