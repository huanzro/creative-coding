using System;

namespace Echoes
{
    internal sealed class ChapterLampShow
    {
        private static readonly LampRgb Gold = new LampRgb(64,47,0), Amber = new LampRgb(48,31,0);
        private static readonly LampRgb Red = new LampRgb(56,0,3), Violet = new LampRgb(37,2,62);
        private int cue = -1, chapter = -1;
        private float collectUntil = -1, tradeUntil = -1, leftUntil = -1, rightUntil = -1;

        public void Reset(int cueId)
        {
            cue = cueId;
            collectUntil = tradeUntil = leftUntil = rightUntil = -1;
        }

        public void Sample(Journey journey, float now, out LampRgb left, out LampRgb right)
        {
            left = right = default;
            if (chapter != journey.Chapter)
            {
                Reset(cue);
                chapter = journey.Chapter;
            }
            if (cue != journey.CueId)
            {
                cue = journey.CueId;
                if (journey.InRuins && journey.Cue == JourneyCue.RuinCollect) collectUntil = now + .63f;
                if (journey.InRuins && journey.Cue == JourneyCue.RuinTradeAccepted) tradeUntil = now + 2.6f;
                if (journey.InStars && journey.Cue == JourneyCue.Star)
                {
                    if (journey.StarLastStamp <= StarMotifs.PerGroup) leftUntil = now + .60f;
                    else rightUntil = now + .60f;
                }
            }
            if (journey.InRuins)
            {
                float tradeRemaining = tradeUntil - now;
                if (tradeRemaining > 0)
                {
                    float elapsed = 2.6f - tradeRemaining;
                    left = Red.Dim(Peak(elapsed, .36f, .34f));
                    right = Red.Dim(Peak(elapsed, 1.39f, .41f));
                }
                else left = right = Amber.Dim(Envelope(collectUntil - now, .63f));
            }
            if (!journey.InStars) return;
            if (!journey.StarFinaleActive)
            {
                left = Violet.Dim(Envelope(leftUntil - now, .60f));
                right = Amber.Dim(Envelope(rightUntil - now, .60f));
                return;
            }
            float age = journey.StarFinaleAge;
            if (age < 9.5f) return;
            if (age < 11.5f)
            {
                float t = age - 9.5f;
                float beat = Math.Max(Peak(t,.19f,.21f), Math.Max(Peak(t,.79f,.25f), Peak(t,1.57f,.48f)));
                left = right = Gold.Dim(beat);
                return;
            }
            float white = Smooth((age - 11.5f) / 2.5f);
            left = right = new LampRgb(64, 47 + (int)(17 * white), (int)(64 * white));
        }

        private static float Peak(float time, float center, float width) =>
            Smooth(1 - Math.Abs(time - center) / width);

        private static float Envelope(float remaining, float length) =>
            Math.Min(Smooth((length - remaining) / .055f), Smooth(remaining / (length - .055f)));

        private static float Smooth(float t)
        {
            t = Math.Max(0,Math.Min(1,t));
            return t*t*(3-2*t);
        }
    }
}
