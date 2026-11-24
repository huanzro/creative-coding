using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesView
    {
        private readonly Texture2D openingForest = Resources.Load<Texture2D>("Opening/Forest");
        private readonly Texture2D openingRuins = Resources.Load<Texture2D>("Opening/Ruins");
        private readonly Texture2D openingSky = Resources.Load<Texture2D>("Opening/Stars");
        private readonly Texture2D openingVignette, grassBlade;

        private void DrawOpening(SpiritFrame frame, bool split)
        {
            float time = frame.openingTime;
            var shot = OpeningStoryboard.Sample(time);
            var actor = new Vector2(shot.Actor.x * Width, shot.Actor.y * Height);
            float gain = split ? .43f : 1;
            Paint(new Rect(0, 0, Width, Height), new Color(.011f, .022f, .030f));
            DrawFilmPlate(openingForest ?? arches, shot.Camera, 1);
            if (shot.Ruin > .001f)
                DrawFilmPlate(openingRuins ?? ruins, shot.Camera, shot.Ruin);
            if (shot.Sky > .001f)
            {
                DrawFilmPlate(openingSky ?? stars, shot.Camera, shot.Sky);
                DrawDistantStars(time, shot.Sky, shot.Rush);
            }

            Color air = Color.Lerp(new Color(.10f,.13f,.084f), new Color(.027f,.062f,.106f),
                Mathf.Max(shot.Ruin, shot.Sky));
            Paint(new Rect(0, 0, Width, Height), Opacity(air, .055f + .08f * shot.Rush));
            DrawFilmAir(time, shot, actor);
            Paint(new Rect(0, 0, Width, Height), new Color(0, 0, 0, .48f), openingVignette);
            DrawGroundShadow(actor, shot, time);

            Vector3 amber = OpeningStoryboard.Firefly(time, false);
            Vector3 red = OpeningStoryboard.Firefly(time, true);
            var yellowPoint = new Vector2(amber.x * Width, amber.y * Height);
            var redPoint = new Vector2(red.x * Width, red.y * Height);
            Vector2 wrist = spirit.Articulated
                ? actor + spirit.Landmark(1) * (162 * shot.Actor.z)
                : actor + new Vector2(45, -18);
            redPoint = Vector2.Lerp(redPoint, wrist + new Vector2(4, -2),
                OpeningStoryboard.Window(time, 25.98f, 26.76f, 27.09f, 27.63f));
            yellowPoint = Vector2.Lerp(yellowPoint, wrist + new Vector2(25, -7),
                OpeningStoryboard.Window(time, 5.65f, 6.9f, 7.21f, 7.51f) * .66f);
            float lifted = OpeningStoryboard.Window(time, 11.52f, 12.4f, 15.3f, 17f);
            float danger = OpeningStoryboard.Window(time, 26.77f, 27.47f, 30.15f, 31.48f);
            DrawLiftRibbon(yellowPoint, wrist, time, lifted * gain);
            DrawScarletSpiral(actor, time, danger * gain);
            DrawCharacter(frame, actor, actor, 162 * shot.Actor.z, false, 0, .84f + .2f * shot.Actor.z,
                !split, gain * .64f);
            DrawFlyTrail(time, false, gain);
            DrawFlyTrail(time, true, gain);
            DrawFirefly(yellowPoint, time, false, 1.40f, Mathf.Clamp01(amber.z) * gain,
                OpeningStoryboard.Ease(17.6f, 19.5f, time));
            DrawFirefly(redPoint, time, true, 1.32f, Mathf.Clamp01(red.z) * gain,
                OpeningStoryboard.Ease(27.05f, 27.6f, time));

            DrawFilmGrass(time, shot);
            DrawLandingDebris(time);
            float start = 1 - OpeningStoryboard.Ease(.07f, 1.18f, time);
            Paint(new Rect(0, 0, Width, Height), new Color(.008f,.018f,.022f, start * .88f));
            float bar = 27 * OpeningStoryboard.Ease(0, .8f, time) * (1 - OpeningStoryboard.Ease(35.2f, 37.5f, time));
            Paint(new Rect(0, 0, Width, bar), Color.black);
            Paint(new Rect(0, Height - bar, Width, bar), Color.black);
        }

        private void DrawOpeningArrival(float opacity)
        {
            var shot = OpeningStoryboard.Sample(OpeningTiming.Duration);
            DrawFilmPlate(openingForest ?? arches, shot.Camera, opacity);
            Paint(new Rect(0, 0, Width, Height), new Color(0, 0, 0, .48f * opacity), openingVignette);
        }

        private static void DrawFilmPlate(Texture2D texture, Vector3 camera, float opacity)
        {
            if (texture == null || opacity <= .002f) return;
            float crop = 1 + Mathf.Max(.044f, camera.z);
            float height = Height * crop;
            float width = Mathf.Max(Width * crop, height * texture.width / texture.height);
            float x = (Width - width) * (.5f + Mathf.Clamp(camera.x, -1, 1) * .46f);
            float y = (Height - height) * (.5f + Mathf.Clamp(camera.y, -1, 1) * .46f);
            Paint(new Rect(x, y, width, height), new Color(1, 1, 1, opacity), texture);
        }

        private void DrawFilmAir(float time, OpeningStoryboard.Shot shot, Vector2 actor)
        {
            Color tint = Color.Lerp(new Color(.88f,.83f,.61f), new Color(.53f,.72f,.84f),
                Mathf.Max(shot.Ruin, shot.Sky));
            for (int i = 0; i < 34; i++)
            {
                float x = Mathf.Repeat(65 * i + 263 * Mathf.Sin(i * 1.7f) + time * (4 + (i % 4) * 2), Width);
                float y = Mathf.Repeat(i * 97.37f + time * (shot.Rush * (i % 4 - 1) * 40 - 6) +
                    Mathf.Sin(i + time * .65f) * 16, Height);
                float alpha = .06f + .12f * Mathf.Sin(i * 2.17f + time * .6f) * Mathf.Sin(i * 2.17f + time * .6f);
                Orb(x, y, 4 + i % 5, tint, alpha);
                if (shot.Rush > .23f && i % 3 == 0)
                {
                    Vector2 p = new Vector2(Mathf.Lerp(x, actor.x, .16f), y);
                    float direction = time >= 12.6f && time < 17 ? 1 : -1;
                    Stroke(p, p + new Vector2(-5, (25 + i % 8 * 9) * shot.Rush * direction),
                        2.8f, tint, .23f * shot.Rush, glow);
                }
            }
            if (shot.Rush > .001f)
            {
                // Moistened roots and canopy briefly mask the change in exposure.
                float occlusion = OpeningStoryboard.Window(time, 8.0f, 8.69f, 8.84f, 9.55f) +
                    OpeningStoryboard.Window(time, 14.3f, 15.27f, 15.43f, 16.13f) * .70f +
                    OpeningStoryboard.Window(time, 28.68f, 29.48f, 29.73f, 30.77f);
                Paint(new Rect(-550, 168 - shot.Rush * 260, Width * 1.85f, Height * 1.85f),
                    new Color(.009f,.024f,.027f, .80f * occlusion), glow);
            }
        }

        private void DrawDistantStars(float time, float visible, float rush)
        {
            for (int i = 0; i < 68; i++)
            {
                float x = Mathf.Repeat(i * .58291f + .113f + time * .0017f, 1);
                float y = .10f + Mathf.Repeat(i * .71607f + .466f, 1) * .53f;
                float shimmer = .64f + .36f * Mathf.Sin(i*5.178f + time*.68f);
                float radius = i % 11 == 0 ? 4.6f : 2.6f;
                Orb(x * Width, y * Height, radius, new Color(.72f,.81f,1), visible * shimmer * .54f);
            }
            Paint(new Rect(105, Height * (.37f + rush * .09f), 1140, 290),
                new Color(.32f,.48f,.92f, visible * .21f), glow);
        }

        private void DrawGroundShadow(Vector2 actor, OpeningStoryboard.Shot shot, float t)
        {
            float weight = OpeningStoryboard.Window(t, 0, .7f, 7.2f, 7.92f) +
                OpeningStoryboard.Ease(OpeningTiming.Impact - .22f, OpeningTiming.Impact + .4f, t);
            float y = Mathf.Clamp(actor.y + 69 * shot.Actor.z, Height * .74f, Height * .945f);
            Paint(new Rect(actor.x - 66 * shot.Actor.z, y - 18, 132 * shot.Actor.z, 36),
                new Color(.012f,.024f,.023f, weight * shot.Ground * .72f), glow);
        }

        private void DrawFlyTrail(float t, bool red, float gain)
        {
            Vector3 now = OpeningStoryboard.Firefly(t, red);
            if (now.z <= .01f) return;
            for (int i = 1; i <= 9; i++)
            {
                Vector3 delay = OpeningStoryboard.Firefly(Mathf.Max(0, t - i * .053f), red);
                float fade = (1 - i / 10f) * Mathf.Clamp01(delay.z) * gain;
                Color tint = red ? Scarlet : Amber;
                float radius = Mathf.Lerp(3, 12, 1 - i / 10f);
                Orb(delay.x * Width, delay.y * Height + Mathf.Sin(t * 9 + i) * i * .30f, radius, tint, fade * .48f);
            }
        }

        private void DrawLiftRibbon(Vector2 fly, Vector2 actor, float t, float weight)
        {
            if (weight <= .006f) return;
            Vector2 last = fly;
            float angle = t * 4.4f;
            for (int i = 1; i <= 17; i++)
            {
                float p = i / 17f;
                Vector2 next = Vector2.Lerp(fly, actor, p);
                next.x += Mathf.Sin(p * 5.6f + angle) * Mathf.Sin(p * Mathf.PI) * 23;
                float alpha = weight * (.29f + .23f * Mathf.Sin(p * Mathf.PI));
                Stroke(last, next, 9, Amber, alpha, glow);
                if (i % 4 == 0) Orb(next.x, next.y, 9, Amber, alpha);
                last = next;
            }
        }

        private void DrawScarletSpiral(Vector2 actor, float t, float weight)
        {
            if (weight <= .008f) return;
            Paint(new Rect(actor.x - 97, actor.y - 116, 194, 229),
                new Color(.87f, .14f, .17f, weight * .25f), glow);
            for (int strand = 0; strand < 2; strand++)
            {
                Vector2 last = actor;
                for (int i = 0; i < 22; i++)
                {
                    float u = i / 21f;
                    float phase = t * 6.1f + strand * Mathf.PI + u * 10;
                    var next = actor + new Vector2(Mathf.Sin(phase) * (36 + u * 71),
                        Mathf.Cos(phase) * 20 + (u - .5f) * 174);
                    if (i > 0) Stroke(last, next, 7.2f, Scarlet, weight * Mathf.Sin(u * Mathf.PI) * .81f, glow);
                    last = next;
                }
            }
        }

        private void DrawFilmGrass(float time, OpeningStoryboard.Shot shot)
        {
            float front = shot.Ground * (1 - OpeningStoryboard.Window(time, 7.2f, 8f, 15.4f, 16.8f)) *
                (1 - OpeningStoryboard.Window(time, 18.2f, 19.5f, 30.5f, 31.5f)) *
                (1 - OpeningStoryboard.Ease(35.9f, 37.4f, time));
            if (front <= .008f) return;
            float sweep = OpeningStoryboard.Window(time, .8f, 2.39f, 3.5f, 6.6f);
            for (int i = 0; i < 46; i++)
            {
                float baseX = Width * (i / 45f) + Mathf.Sin(i * 4.7f) * 23;
                float wave = Mathf.Sin(time * 1.8f + i * .65f);
                bool left = baseX < Width * .5f;
                float bow = sweep * (1 - Mathf.Abs(baseX - Width * .45f) / Width);
                float nearCenter = Mathf.Clamp01(1 - Mathf.Abs(baseX / Width - .45f) * 5f);
                float size = 40 + 99 * Mathf.Abs(Mathf.Sin(i * 8.92f)) + 61 * nearCenter * (1 - bow);
                float ground = Height + 34 - nearCenter * 77 * (1 - bow);
                float angle = (left ? -1 : 1) * bow * (20 + 35 * nearCenter) + wave * 9 + Mathf.Sin(i * 15) * 28;
                Vector2 root = new Vector2(baseX, ground);
                Matrix4x4 before = GUI.matrix;
                GUI.matrix = RotateAbout(before, root, angle);
                float stain = .32f + .68f * Mathf.Abs(Mathf.Sin(i * 2.1f));
                Paint(new Rect(root.x - 8, root.y - size, 16 + (i % 4) * 2.3f, size),
                    new Color(.053f * stain, .14f * stain, .073f * stain, front * .75f), grassBlade);
                GUI.matrix = before;
            }
        }

        private void DrawLandingDebris(float time)
        {
            float age = time - OpeningTiming.Impact;
            if (age < 0 || age > 2.25f) return;
            for (int i = 0; i < 30; i++)
            {
                float direction = Mathf.Sin(i * 3.888f);
                float x = Width * .443f + Mathf.Sqrt(age) * (32 + 161 * Mathf.Abs(direction)) * Mathf.Sign(direction);
                float y = Height * .853f - Mathf.Sin(Mathf.Min(age, 1.73f) * 1.68f) * (12 + i % 7 * 10);
                Orb(x, y, 4 + i % 6 + age * 8, new Color(.67f,.57f,.35f),
                    .49f * (1 - age / 2.25f) * (i % 3 == 0 ? .9f : .46f));
            }
        }

        private static Texture2D MakeVignette()
        {
            const int side = 96;
            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[side * side];
            for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
            {
                float radial = new Vector2(x / 47.5f - 1, (y / 47.5f - 1) * .88f).magnitude;
                float fog = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.43f, 1.37f, radial));
                pixels[y * side + x] = new Color32(255, 255, 255, (byte)(fog * 255));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D MakeBlade()
        {
            const int width = 30, height = 96;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = y / (height - 1f);
                float half = Mathf.Lerp(.27f, .017f, u);
                float distance = Mathf.Abs(x / (width - 1f) - .42f - u * u * .27f);
                float alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.67f, 1, distance / half));
                byte shade = (byte)Mathf.Lerp(167, 255, 1 - Mathf.Min(1, distance / half));
                pixels[y * width + x] = new Color32(shade, 255, shade, (byte)(alpha * 255));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
