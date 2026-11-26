using UnityEngine;

namespace Echoes
{
    internal static class StarProjection
    {
        private static readonly Rect FullStage = new Rect(0, 0, 1, 1);
        private static readonly Vector2 DrawingCenter = new Vector2(.51f, .49f);
        private static readonly Vector2 WindowCenter = new Vector2(.50f, .53f);
        public static readonly float Focal = .5f / Mathf.Tan(StarInkSpace.FieldOfView * .5f * Mathf.Deg2Rad);
        public static readonly Rect DefaultWindow = Window(new Vector2(960, 540), true, 1.45f);

        // The physical aperture lives in normalized stage coordinates, with its origin at the top left.
        public static Rect Window(Vector2 size, bool sideways, float lightReach)
        {
            if (size.x < 2 || size.y < 2) return FullStage;
            float aspect = sideways ? size.y / size.x : size.x / size.y;
            float height = .44f + .104f * Mathf.Clamp(lightReach, .65f, 2.45f);
            float ratio = Mathf.Max(.16f, aspect / StarInkSpace.Aspect);
            height = Mathf.Min(height, .86f / ratio);
            var footprint = new Vector2(height * ratio, height);
            return new Rect(WindowCenter - footprint * .5f, footprint);
        }

        public static Vector2 EyeOffset(Vector3 tip, Vector2 steering, Rect window)
        {
            bool split = window.width > 0;
            Vector2 travel = split ? window.size * .37f : new Vector2(.34f, .32f);
            Vector2 onScreen = (split ? window.center : DrawingCenter) + Vector2.Scale(steering - Vector2.one * .5f, travel);
            return (Vector2)tip - Vector2.Scale(onScreen - Vector2.one * .5f,
                new Vector2(StarInkSpace.Aspect, -1)) * (StarInkSpace.PerformerDepth / Focal);
        }

        public static Vector2 StagePoint(Vector3 relative)
        {
            float scale = Focal / Mathf.Max(.05f, relative.z);
            return new Vector2(.5f + relative.x * scale / StarInkSpace.Aspect, .5f - relative.y * scale);
        }

        public static Vector2 PortraitPoint(Vector3 relative, Rect window, Rect area, bool mirror, bool sideways)
        {
            Rect region = WindowOrFull(window);
            Vector2 stage = StagePoint(relative) - region.center;
            var local = new Vector2(stage.x / region.width, stage.y / region.height);
            if (mirror) local.x = -local.x;
            if (sideways) local = new Vector2(local.y, -local.x);
            return area.center + Vector2.Scale(local, area.size);
        }

        public static float FitWriter(float size, Vector2 hand, Vector2 tip, Rect area, Vector2 half)
        {
            const float margin = 4;
            float left = (tip.x - area.xMin - margin) / Mathf.Max(.04f, half.x + hand.x);
            float right = (area.xMax - tip.x - margin) / Mathf.Max(.04f, half.x - hand.x);
            float top = (tip.y - area.yMin - margin) / Mathf.Max(.04f, half.y + hand.y);
            float bottom = (area.yMax - tip.y - margin) / Mathf.Max(.04f, half.y - hand.y);
            return Mathf.Max(1, Mathf.Min(size, Mathf.Min(Mathf.Min(left, right), Mathf.Min(top, bottom))));
        }

        public static Rect WindowOrFull(Rect window) => window.width > 0 ? window : FullStage;
    }
}
