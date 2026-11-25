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
            public string id, name, group;
            public float lengthPixels;
            public Palette colors;
            public Vector2[] points;
        }

        public static readonly Rect ActiveBounds = new Rect(80, 30, 1120, 1120 * 640f / 1200);
        public static readonly Color SystemInk = new Color(.31f, .76f, 1);
        public static readonly Vector2 IdleActor = new Vector2(640, 505);
        private static StarTrailArt[] library;
        private static bool loaded;
        public readonly string Name;
        public readonly Vector2[] Points;
        private readonly Color start, middle, end;

        private StarTrailArt(Entry entry)
        {
            Name = entry.name;
            Points = entry.points;
            ColorUtility.TryParseHtmlString(entry.colors.start, out start);
            ColorUtility.TryParseHtmlString(entry.colors.middle, out middle);
            ColorUtility.TryParseHtmlString(entry.colors.end, out end);
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
#if UNITY_EDITOR
                int upper = Array.FindAll(path.points, point => point.y < StarMotifs.UpperEdge).Length;
                Debug.Assert(Mathf.Abs(path.lengthPixels - StarMotifs.Length(i + 1)) < .03f &&
                    Mathf.RoundToInt(100f * upper / path.points.Length) == StarMotifs.UpperShare(i + 1),
                    "Star Sea estimates must match the measured geometry: " + path.id);
#endif
            }
            library = paths;
        }

        public Color Pigment(float progress, bool machine)
        {
            if (machine) return SystemInk;
            return progress < .52f ? Color.Lerp(start, middle, progress / .52f)
                : Color.Lerp(middle, end, (progress - .52f) / .48f);
        }

        public Vector2 NormalizedPoint(float progress)
        {
            float index = Mathf.Clamp01(progress) * (Points.Length - 1);
            int first = Mathf.Min(Points.Length - 2, (int)index);
            return Vector2.LerpUnclamped(Points[first], Points[first + 1], index - first);
        }

        public Vector2 Point(Rect bounds, float progress) => bounds.position + Vector2.Scale(NormalizedPoint(progress), bounds.size);

        public Vector2 Direction(float progress)
        {
            Vector2 direction = NormalizedPoint(progress + .007f) - NormalizedPoint(progress - .007f);
            return new Vector2(direction.x * 1200, direction.y * 640).normalized;
        }

        public static Vector2 Actor(Journey journey)
        {
            var active = Find(journey.StarMotif);
            if (active != null)
            {
                Vector2 head = active.Point(ActiveBounds, journey.StarProgress);
                Vector2 tangent = active.Direction(journey.StarProgress);
                Vector2 body = head - tangent * 21 + new Vector2(0, 19);
                return Vector2.Lerp(IdleActor, body, OpeningStoryboard.Ease(0, Journey.StarArrivalSeconds, journey.Age));
            }
            if (journey.Phase != Stage.StarsRest || journey.StarCount == 0) return IdleActor;
            var last = Find(journey.StarMotifAt(journey.StarCount - 1));
            return last == null ? IdleActor : Vector2.Lerp(last.Point(ActiveBounds, 1) + new Vector2(0, 19),
                IdleActor, OpeningStoryboard.Ease(.19f, Journey.StarSettleSeconds, journey.Age));
        }
    }
}
