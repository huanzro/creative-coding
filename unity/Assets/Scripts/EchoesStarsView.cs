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
            if (!spatial && !split)
            {
                var center = new Vector2(Width * .5f, Height * .56f);
                DrawCharacter(frame, center, center, 142, false, 0, 1, true, 0);
            }
            PrepareInterface();
            var plate = new Rect(917, 91, 300, 43);
            Paint(plate, new Color(.025f, .041f, .087f, .86f));
            Color color = GUI.contentColor;
            GUI.contentColor = j.StarPointer.Valid ? new Color(.70f, .97f, 1) : new Color(.80f, .83f, .98f);
            GUI.Label(new Rect(931, 99, 273, 25), j.StarPointer.Valid ? "食指已识别 · 正在画光" : "伸出食指开始画光 · 发声向里飞", body);
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
