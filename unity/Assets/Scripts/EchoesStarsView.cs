using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesView
    {
        private readonly StarInkSpace starInk = new StarInkSpace();

        internal int StarInkRevision => starInk.Revision;
        internal StarInkFrame StarArtwork => starInk.Frame;
        internal Rect StarWindow { get; set; } = StarProjection.DefaultWindow;

        internal void RestoreStarArtwork(Journey journey, JourneyCheckpoint checkpoint) =>
            starInk.Restore(journey, checkpoint);

        private void DrawStarStage(Journey j, SpiritFrame frame, bool spatial, bool split, bool calibrating)
        {
            Vector2 center = spatial ? Vector2.Scale(world.ActorScreen, new Vector2(Width, Height)) :
                new Vector2(Width * .5f, Height * .56f);
            float effects = (split ? .36f : .62f) * ProjectionOverlayGain;
            if (!spatial && !split)
                DrawCharacter(frame, center, center, 142, false, 0, 1, true, frame.releaseAge >= 0 ? effects : 0);
            else DrawReleaseRings(frame.releaseAge, center, center, 1, palette[2], effects);
            if (split && calibrating && starInk.Frame.window.width > 0)
                DrawStarWindowGuide(starInk.Frame.window);
            PrepareInterface();
            var plate = new Rect(917, 91, 300, 43);
            Paint(plate, new Color(.025f, .041f, .087f, .86f));
            Color color = GUI.contentColor;
            GUI.contentColor = j.StarPointer.Valid ? new Color(.70f, .97f, 1) : new Color(.80f, .83f, .98f);
            string steering = j.StarPointer.Valid ? "食指引导" : "持续巡航";
            GUI.Label(new Rect(931, 99, 273, 25), steering + "  ·  " + j.StarSpeed.ToString("0.00") + " m/s", body);
            GUI.contentColor = color;
        }

        private static void DrawStarWindowGuide(Rect window)
        {
            var area = new Rect(window.x * Width, window.y * Height, window.width * Width, window.height * Height);
            var color = new Color(.57f, .84f, .98f, .58f);
            Paint(new Rect(area.xMin, area.yMin, area.width, 1), color);
            Paint(new Rect(area.xMin, area.yMax, area.width, 1), color);
            Paint(new Rect(area.xMin, area.yMin, 1, area.height), color);
            Paint(new Rect(area.xMax, area.yMin, 1, area.height), color);
        }

        private void DrawStarProjection(SpiritFrame frame, StarInkFrame artwork, Rect area, Vector2 center,
            float size, bool mirror, bool sideways, float extent)
        {
            float rotation = sideways ? -90 : 0;
            if (artwork.penMark >= 0 && artwork.marks != null)
            {
                Vector2 tip = StarProjection.PortraitPoint(artwork.cursor - artwork.Eye, artwork.window, area, mirror, sideways);
                Vector2 hand = PortraitOffset(HandPosition, mirror, rotation);
                var half = sideways ? new Vector2(SpiritRig.HeightScale, SpiritRig.WidthScale) * .5f
                    : new Vector2(SpiritRig.WidthScale, SpiritRig.HeightScale) * .5f;
                size = StarProjection.FitWriter(size, hand, tip, area, half);
                center = tip - hand * size;
            }
            Texture ink = starInk.DrawPortrait(artwork, area, mirror, sideways);
            if (ink != null) Paint(area, Color.white, ink);
            DrawCharacter(frame, center, center, size, mirror, rotation, extent, true, .72f);
        }
    }
}
