using System;
using UnityEngine;

namespace Echoes
{
    internal sealed class OpeningSound : IDisposable
    {
        private const int Rate = 24000;
        private const double FullTurn = Math.PI * 2;
        private readonly AudioSource source;
        private AudioClip clip;
        private float lastTime = -1;

        public OpeningSound(GameObject owner)
        {
            source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.volume = .74f;
        }

        public void Follow(float time, bool muted)
        {
            if (time < 0)
            {
                if (lastTime >= 0) source.Stop();
                lastTime = time;
                return;
            }
            if (muted)
            {
                if (source.isPlaying) source.Pause();
                return;
            }
            if (clip == null)
            {
                clip = Compose();
                source.clip = clip;
            }
            if (lastTime < 0 || time + .05f < lastTime || !source.isPlaying)
            {
                source.time = Mathf.Clamp(time, 0, clip.length - .08f);
                source.Play();
            }
            if (Math.Abs(source.time - time) > .57f)
                source.time = Mathf.Clamp(time, 0, clip.length - .08f);
            lastTime = time;
        }

        // Four warm notes travel with the same insect. The red answer arrives
        // a semitone away. A short earth sound closes the fall without a flash.
        private static AudioClip Compose()
        {
            int length = (int)(OpeningTiming.Duration * Rate);
            float[] samples = new float[length * 2];
            var random = new System.Random(53);
            double wind = 0, rumble = 0, leafNoise = 0;
            for (int i = 0; i < length; i++)
            {
                double t = (double)i / Rate;
                double noise = random.NextDouble() * 2 - 1;
                wind += (noise - wind) * .016;
                rumble += (noise - rumble) * .005;
                leafNoise += (noise - leafNoise) * .086;
                double air = Bell(t, OpeningTiming.FirstFall + 1.3, 1.24) +
                             .84 * Bell(t, OpeningTiming.SecondFall + .92, 1.05);
                double canopy = .043 * Tone(t, 130.813) + .020 * Tone(t, 196) + .014 * Tone(t, 261.626);
                double notes = Pluck(t, 3.3, 523.25, .18) + Pluck(t, 5.3, 659.25, .22) +
                    Pluck(t, OpeningTiming.Catch + .75, 783.99, .31) + Pluck(t, 16.55, 523.25, .22) +
                    Pluck(t, 18.9, 659.25, .24) + Pluck(t, 20.2, 783.99, .21) +
                    Pluck(t, 24.0, 698.46, .12) + Pluck(t, OpeningTiming.RedContact, 554.37, .25) +
                    Pluck(t, 35.0, 392.0, .14);
                double red = Bell(t, OpeningTiming.RedContact + .55, .84) * .085 * (Tone(t, 146.83) + Tone(t, 155.56));
                double land = Bell(t, OpeningTiming.Impact + .017, .078) * (rumble * 1.19 + Tone(t, 54) * .23);
                double rolls = .71 * Bell(t, 31.74, .103) + .44 * Bell(t, 32.20, .103) + .23 * Bell(t, 32.79, .16);
                double brush = Bell(t, 1.66, .23) + .78 * Bell(t, 2.65, .27) + .42 * Bell(t, 3.07, .22)
                    + 1.42 * Bell(t, 31.43, .21) + rolls;
                double pebbles = .72 * Bell(t, 8.41, .14) + .49 * Bell(t, 9.08, .096);
                double close = .19 * Bell(t, 7.00, .34) + .36 * Bell(t, 26.78, .31);
                double quiet = 1 - Math.Max(0, close);
                double fade = Math.Min(1, t / 1.45) * Math.Min(1, (OpeningTiming.Duration - t) / 1.48);
                double ground = land + rumble * rolls * .56 + (leafNoise - wind) * (brush * .42 + pebbles * .32);
                double airBed = (canopy * (1 - .45 * Math.Min(1, air)) + wind * (.16 + .79 * air)) * quiet;
                double pan = .27 * Math.Sin(t * .41) + .19 * Bell(t, 26.69, 1.42);
                double focal = notes + red;
                samples[i * 2] = (float)Math.Max(-1, Math.Min(1, fade * (ground + airBed + focal * (1 - pan))));
                samples[i * 2 + 1] = (float)Math.Max(-1, Math.Min(1, fade * (ground + airBed + focal * (1 + pan))));
            }
            var rendered = AudioClip.Create("Echoes opening score", length, 2, Rate, false);
            rendered.SetData(samples, 0);
            return rendered;
        }

        private static double Tone(double time, double hz) => Math.Sin(FullTurn * hz * time);
        private static double Bell(double time, double center, double width)
        {
            double x = (time - center) / width;
            return Math.Exp(-x * x);
        }

        private static double Pluck(double time, double at, double hz, double gain)
        {
            double x = time - at;
            if (x <= 0 || x > 2.8) return 0;
            return gain * (1 - Math.Exp(-x * 28)) * Math.Exp(-x * 2.15) *
                (.74 * Tone(x, hz) + .16 * Tone(x, hz * 2.01) + .10 * Tone(x, hz * 3.09));
        }

        public void Dispose()
        {
            if (source != null)
            {
                source.Stop();
                UnityEngine.Object.Destroy(source);
            }
            if (clip != null) UnityEngine.Object.Destroy(clip);
        }
    }
}
