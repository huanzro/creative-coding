using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesView
    {
        private readonly StarInkSpace starInk = new StarInkSpace();

        internal int StarInkRevision => starInk.Revision;
        internal StarInkFrame StarArtwork => starInk.Frame;

        internal void RestoreStarArtwork(Journey journey, JourneyCheckpoint checkpoint) =>
            starInk.Restore(journey, checkpoint);

        private void DrawStarStage(Journey j, SpiritFrame frame, bool spatial, bool split)
        {
            Vector2 center = spatial ? Vector2.Scale(world.ActorScreen, new Vector2(Width, Height)) :
                new Vector2(Width * .5f, Height * .56f);
            float effects = (split ? .36f : .62f) * ProjectionOverlayGain;
            if (!spatial && !split)
                DrawCharacter(frame, center, center, 142, false, 0, 1, true, frame.releaseAge >= 0 ? effects : 0);
            else DrawReleaseRings(frame.releaseAge, center, center, 1, palette[2], effects);
            PrepareInterface();
            var plate = new Rect(917, 91, 300, 43);
            Paint(plate, new Color(.025f, .041f, .087f, .86f));
            Color color = GUI.contentColor;
            GUI.contentColor = j.StarPointer.Valid ? new Color(.70f, .97f, 1) : new Color(.80f, .83f, .98f);
            string steering = j.StarPointer.Valid ? "食指引导" : "持续巡航";
            GUI.Label(new Rect(931, 99, 273, 25), steering + "  ·  " + j.StarSpeed.ToString("0.00") + " m/s", body);
            GUI.contentColor = color;
        }

        private void DrawPortraitStars(StarInkFrame remoteInk, Rect area, Vector2 center, float size,
            bool mirror, bool sideways, float extent)
        {
            Texture ink = starInk.DrawPortrait(remoteInk ?? starInk.Frame, area, center, size, mirror, sideways, extent);
            if (ink != null) Paint(area, Color.white, ink);
        }
    }
}
