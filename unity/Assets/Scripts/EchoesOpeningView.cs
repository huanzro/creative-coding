using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesView
    {
        private readonly Texture2D openingForest = Resources.Load<Texture2D>("Opening/Forest");
        private readonly Texture2D openingRuins = Resources.Load<Texture2D>("Opening/Ruins");
        private readonly Texture2D openingSky = Resources.Load<Texture2D>("Opening/Stars");
        private readonly Texture2D openingFerns = Resources.Load<Texture2D>("Opening/ForegroundFerns");
        private readonly Texture2D openingRim = Resources.Load<Texture2D>("Opening/SinkholeRim");
        private readonly Texture2D openingVignette, grassBlade;
        private Material plateMaterial;
        private RenderTexture compositePlate;
        private float plateTime = -1;

        public void UpdateWorld(Journey journey, SpiritFrame frame, bool split, bool previewWorld = false)
        {
            if (frame.openingTime < 0 && !frame.forest && !frame.ruins && !journey.InStars && !UseWorld && !previewWorld) return;
            if (world == null) world = new EchoesWorld();
            Vector2 amber = Vector2.zero, red = Vector2.zero;
            if (frame.openingTime >= 0)
            {
                float time = frame.openingTime;
                var shot = OpeningStoryboard.Sample(time);
                var center = new Vector2(shot.Actor.x*Width, shot.Actor.y*Height);
                var scale = new Vector2(1/Width,1/Height);
                amber = Vector2.Scale(center+OpeningFlyOffset(time,shot,false)*(162*shot.Actor.z),scale);
                red = Vector2.Scale(center+OpeningFlyOffset(time,shot,true)*(162*shot.Actor.z),scale);
            }
            world.Render(journey,frame,spirit,split,TimeOfDay,ScrollSpeed,amber,red,
                ScreenBrightness,ProjectionFalloff,ProjectionGradient,BackgroundBrightness,
                ForestSpiderBrightness,ForestMawBrightness,starInk);
            if (frame.openingTime >= 0 && !world.Ready) UpdatePlates(frame.openingTime);
        }

        private void DrawSpatialOpening(SpiritFrame frame,bool split)
        {
            float t=frame.openingTime;
            var shot=OpeningStoryboard.Sample(t);
            Paint(new Rect(0,0,Width,Height),Color.white,world.Texture);
            Vector2 actor=new Vector2(shot.Actor.x*Width,shot.Actor.y*Height);
            float size=162*shot.Actor.z;
            Vector2 fingertips=actor+HandPosition*size;
            Vector2 fly=actor+OpeningFlyOffset(t,shot,false)*size;
            float overlayGain=ProjectionOverlayGain;
            float strength=(split?.34f:1)*overlayGain;
            float catchLight=OpeningStoryboard.Window(t,11.52f,12.4f,15.3f,17.0f);
            DrawLiftRibbon(fly+new Vector2(0,-18),fingertips,t,catchLight*strength*.67f);
            float scarlet=OpeningStoryboard.Window(t,26.83f,27.62f,29.96f,30.54f);
            DrawScarletSpiral(actor,t,scarlet*strength*.34f);
            DrawLandingDebris(t,overlayGain);
            float start=1-OpeningStoryboard.Ease(.03f,1.18f,t);
            Paint(new Rect(0,0,Width,Height),new Color(.006f*overlayGain,.012f*overlayGain,.017f*overlayGain,start));
            float bar=28*OpeningStoryboard.Ease(0,.8f,t)*(1-OpeningStoryboard.Ease(35.2f,37.5f,t));
            Paint(new Rect(0,0,Width,bar),Color.black);
            Paint(new Rect(0,Height-bar,Width,bar),Color.black);
        }

        private void PreparePlates()
        {
            var shader = Resources.Load<Shader>("OpeningPlates");
            if (shader == null || !shader.isSupported) return;
            plateMaterial = new Material(shader);
            compositePlate = new RenderTexture((int)Width, (int)Height, 0, RenderTextureFormat.ARGB32) {
                name = "Echoes graded opening plates", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
        }

        private void UpdatePlates(float time)
        {
            if (plateMaterial == null || openingForest == null || openingRuins == null || openingSky == null ||
                (time == plateTime && compositePlate.IsCreated())) return;
            if (!compositePlate.IsCreated() && !compositePlate.Create()) return;
            var shot = OpeningStoryboard.Sample(time);
            bool sky = shot.Sky > 0;
            Texture2D arriving = sky ? openingSky : openingRuins;
            float reveal = sky ? shot.Sky : shot.Ruin;
            Vector3 opticalDrift = shot.Camera + new Vector3(shot.Camera.x * .13f, 0, -.023f);
            plateMaterial.SetTexture("_NextPlate", arriving);
            plateMaterial.SetVector("_ForestUV", PlateUV(openingForest, shot.Camera));
            plateMaterial.SetVector("_NextUV", PlateUV(arriving, opticalDrift));
            plateMaterial.SetVector("_Boundary", new Vector4(reveal, sky ? 1 : 0, time * .72f, sky ? .63f : .78f));
            plateMaterial.SetVector("_MistedTint", sky ? new Vector4(.18f, .244f, .32f, 1) : new Vector4(.018f, .040f, .044f, 1));
            RenderTexture before = RenderTexture.active;
            Graphics.Blit(openingForest, compositePlate, plateMaterial);
            RenderTexture.active = before;
            plateTime = time;
        }

        private static Vector4 PlateUV(Texture2D texture, Vector3 camera)
        {
            float y = 1 / (1 + Mathf.Max(.044f, camera.z));
            float x = y * Mathf.Min(1, (Width / Height) / (texture.width / (float)texture.height));
            return new Vector4(x, y, (1 - x) * (.5f + .46f * Mathf.Clamp(camera.x, -1, 1)),
                (1 - y) * (.5f - .46f * Mathf.Clamp(camera.y, -1, 1)));
        }

        private void DrawOpening(SpiritFrame frame, bool split)
        {
            float time = frame.openingTime;
            var shot = OpeningStoryboard.Sample(time);
            var actor = new Vector2(shot.Actor.x * Width, shot.Actor.y * Height);
            float gain = split ? .43f : 1;
            Paint(new Rect(0, 0, Width, Height), BackgroundTint(new Color(.011f, .022f, .030f)));
            if (compositePlate != null && time == plateTime && compositePlate.IsCreated())
                Paint(new Rect(0, 0, Width, Height), BackgroundTint(Color.white), compositePlate);
            else
            {
                DrawFilmPlate(openingForest ?? arches, shot.Camera, 1);
                if (shot.Ruin > .001f) DrawFilmPlate(openingRuins ?? ruins, shot.Camera, shot.Ruin);
                if (shot.Sky > .001f) DrawFilmPlate(openingSky ?? stars, shot.Camera, shot.Sky);
            }
            if (shot.Sky > .001f) DrawDistantStars(time, shot.Sky * BackgroundBrightness, shot.Rush);

            Color air = Color.Lerp(new Color(.10f,.13f,.084f), new Color(.027f,.062f,.106f),
                Mathf.Max(shot.Ruin, shot.Sky));
            Paint(new Rect(0, 0, Width, Height), BackgroundTint(Opacity(air, .055f + .08f * shot.Rush)));
            DrawFilmAir(time, shot, actor);
            Paint(new Rect(0, 0, Width, Height), new Color(0, 0, 0, .48f), openingVignette);
            DrawGroundShadow(actor, shot, time, split);

            float size = 162 * shot.Actor.z;
            Vector2 wrist = actor + HandPosition * size;
            Vector2 yellowPoint = actor + OpeningFlyOffset(time, shot, false) * size;
            Vector2 redPoint = actor + OpeningFlyOffset(time, shot, true) * size;
            float amberCast = OpeningStoryboard.Contact(time, false);
            float redCast = OpeningStoryboard.Contact(time, true);
            SoftLight(yellowPoint.x, yellowPoint.y + 23, 127, Amber, amberCast * gain * .17f);
            SoftLight(redPoint.x, redPoint.y + 14, 142, Scarlet, redCast * gain * .16f);
            float lifted = OpeningStoryboard.Window(time, 11.52f, 12.4f, 15.3f, 17f);
            float danger = OpeningStoryboard.Window(time, 26.77f, 27.47f, 30.15f, 31.48f);
            DrawLiftRibbon(yellowPoint + new Vector2(0, -48), wrist, time, lifted * gain);
            DrawScarletSpiral(actor, time, danger * gain);
            DrawFlyTrail(time, false, gain * (1 - amberCast * .87f));
            DrawFlyTrail(time, true, gain * (1 - redCast * .91f));
            DrawCharacter(frame, actor, actor, size, false, 0, .84f + .2f * shot.Actor.z,
                !split, gain * .25f);
            DrawOpeningFireflies(time, shot, actor, size, false, 0, false, split, 1);

            DrawLensLayers(time, shot, wrist);
            DrawFilmGrass(time, shot);
            DrawLandingDebris(time);
            float start = 1 - OpeningStoryboard.Ease(.07f, 1.18f, time);
            Paint(new Rect(0, 0, Width, Height), new Color(.008f,.018f,.022f, start * .88f));
            float bar = 29 * OpeningStoryboard.Ease(0, .8f, time) * (1 - OpeningStoryboard.Ease(35.2f, 37.5f, time));
            Paint(new Rect(0, 0, Width, bar), Color.black);
            Paint(new Rect(0, Height - bar, Width, bar), Color.black);
        }

        private void DrawOpeningArrival(float opacity)
        {
            var shot = OpeningStoryboard.Sample(OpeningTiming.Duration);
            DrawFilmPlate(openingForest ?? arches, shot.Camera, opacity);
            Paint(new Rect(0, 0, Width, Height), new Color(0, 0, 0, .48f * opacity), openingVignette);
        }

        private void DrawFilmPlate(Texture2D texture, Vector3 camera, float opacity)
        {
            if (texture == null || opacity <= .002f) return;
            float crop = 1 + Mathf.Max(.044f, camera.z);
            float height = Height * crop;
            float width = Mathf.Max(Width * crop, height * texture.width / texture.height);
            float x = (Width - width) * (.5f + Mathf.Clamp(camera.x, -1, 1) * .46f);
            float y = (Height - height) * (.5f + Mathf.Clamp(camera.y, -1, 1) * .46f);
            Paint(new Rect(x, y, width, height), BackgroundTint(Opacity(Color.white,opacity)), texture);
        }

        private void DrawFilmAir(float time, OpeningStoryboard.Shot shot, Vector2 actor)
        {
            Color tint = BackgroundTint(Color.Lerp(new Color(.88f,.83f,.61f), new Color(.53f,.72f,.84f),
                Mathf.Max(shot.Ruin, shot.Sky)));
            float travel = -145 * OpeningStoryboard.Ease(7.6f, 10.7f, time)
                + 210 * OpeningStoryboard.Ease(12.8f, 16.31f, time)
                + 95 * OpeningStoryboard.Ease(18.54f, 22f, time)
                - 225 * OpeningStoryboard.Ease(27.6f, 31.15f, time);
            for (int i = 0; i < 34; i++)
            {
                float x = Mathf.Repeat(65 * i + 263 * Mathf.Sin(i * 1.7f) + time * (4 + (i % 4) * 2), Width);
                float y = Mathf.Repeat(i * 97.37f - time * 6 + travel * (.48f + i % 4 * .27f) +
                    Mathf.Sin(i + time * .65f) * 16, Height);
                float alpha = .06f + .12f * Mathf.Sin(i * 2.17f + time * .6f) * Mathf.Sin(i * 2.17f + time * .6f);
                SoftLight(x, y, 4 + i % 5, tint, alpha);
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
                    BackgroundTint(new Color(.009f,.024f,.027f, .80f * occlusion)), glow);
            }
        }

        private void DrawLensLayers(float time, OpeningStoryboard.Shot shot, Vector2 hand)
        {
            float dryGround = (1 - OpeningStoryboard.Ease(7.94f, 8.95f, time)) +
                OpeningStoryboard.Window(time, 15.55f, 16.42f, 18.72f, 20.36f) +
                OpeningStoryboard.Window(time, 29.50f, 31.20f, 35.45f, 37.45f);
            if (openingFerns != null && dryGround > .01f)
            {
                // Each side of the fern curtain dips away from the leading hand on its own beat.
                float touch = OpeningStoryboard.Window(time, .82f, 2.25f, 4.40f, 7.38f);
                float landing = OpeningStoryboard.Window(time, 30.76f, 31.50f, 32.24f, 33.46f);
                float x = -108 - shot.Camera.x * 125;
                float y = 66 + 76 * shot.Camera.y - 86 * shot.Camera.z;
                float height = 724, half = 752;
                Color mist = Opacity(new Color(.76f, .78f, .70f), dryGround * .91f);
                float leaf = Mathf.Clamp01(1 - Mathf.Abs(hand.x / Width - .52f) * 3.7f);
                var left = new Rect(x - touch * 56, y + touch * (54 + 45 * leaf) + landing * 31, half + touch * 56, height);
                var right = new Rect(x + half, y + touch * (19 + 37 * leaf) + landing * 23, half + touch * 23, height);
                GUI.color = mist;
                GUI.DrawTextureWithTexCoords(left, openingFerns, new Rect(0, 0, .5f, 1), true);
                GUI.DrawTextureWithTexCoords(right, openingFerns, new Rect(.5f, 0, .5f, 1), true);
                GUI.color = Color.white;
            }

            float rim = OpeningStoryboard.Window(time, 7.66f, 8.13f, 9.63f, 10.31f) +
                .81f * OpeningStoryboard.Window(time, 14.02f, 14.91f, 15.96f, 16.64f);
            DrawRim(rim, shot.Ruin, shot.Camera.x);

            float canopy = OpeningStoryboard.Window(time, 18.28f, 19.31f, 20.61f, 22.04f);
            float cloud = OpeningStoryboard.Window(time, 27.94f, 29.15f, 29.92f, 31.14f);
            if (canopy + cloud > .005f)
            {
                float y = -130 + OpeningStoryboard.Ease(18.43f, 22.12f, time) * 405 - cloud * 132;
                Paint(new Rect(-260, y, 1740, 420), new Color(.42f,.50f,.66f, canopy * .32f), glow);
                Paint(new Rect(-170, y + 120, 1630, 398), new Color(.080f,.14f,.21f, cloud * .67f), glow);
            }
        }

        private void DrawRim(float opacity, float fall, float cameraX)
        {
            if (openingRim == null || opacity <= .005f) return;
            float y = Height * Mathf.Lerp(-.042f, -.70f, fall);
            float zoom = 1.39f + .34f * Mathf.Sin(fall * Mathf.PI);
            float width = Width * zoom;
            Paint(new Rect((Width - width) * .5f - cameraX * 132, y, width, Height * zoom),
                new Color(.80f, .85f, .90f, opacity), openingRim);
        }

        private void ReleasePlates()
        {
            if (compositePlate != null)
            {
                compositePlate.Release();
                Object.Destroy(compositePlate);
            }
            if (plateMaterial != null) Object.Destroy(plateMaterial);
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

        private void DrawGroundShadow(Vector2 actor, OpeningStoryboard.Shot shot, float t, bool split)
        {
            float weight = OpeningStoryboard.Window(t, 0, .7f, 7.2f, 7.92f) +
                OpeningStoryboard.Ease(OpeningTiming.Impact - .22f, OpeningTiming.Impact + .4f, t);
            float y = Mathf.Clamp(actor.y + 69 * shot.Actor.z, Height * .74f, Height * .945f);
            float alpha = weight * shot.Ground * (split ? .41f : .83f);
            Paint(new Rect(actor.x - 67 * shot.Actor.z, y - 24, 134 * shot.Actor.z, 45),
                new Color(.007f,.016f,.016f, alpha), glow);
            if (!spirit.Articulated || alpha < .01f) return;
            for (int i = 2; i <= 3; i++)
            {
                Vector2 foot = actor + spirit.Landmark(i) * (162 * shot.Actor.z);
                float ground = Mathf.Min(Height * .94f, Mathf.Max(y, foot.y + 4));
                Paint(new Rect(foot.x - 21, ground - 6, 42, 13),
                    new Color(.008f,.015f,.012f, alpha * (1 - Mathf.Clamp01((ground - foot.y) / 78))), glow);
            }
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
            for (int i = 0; i < 28; i++)
            {
                float baseX = Width * (i / 27f) + Mathf.Sin(i * 4.7f) * 23;
                float wave = Mathf.Sin(time * 1.8f + i * .65f);
                bool left = baseX < Width * .5f;
                float bow = sweep * (1 - Mathf.Abs(baseX - Width * .45f) / Width);
                float nearCenter = Mathf.Clamp01(1 - Mathf.Abs(baseX / Width - .45f) * 5f);
                float size = 34 + 68 * Mathf.Abs(Mathf.Sin(i * 8.92f)) + 36 * nearCenter * (1 - bow);
                float ground = Height + 29 - nearCenter * 57 * (1 - bow);
                float angle = (left ? -1 : 1) * bow * (20 + 35 * nearCenter) + wave * 9 + Mathf.Sin(i * 15) * 28;
                Vector2 root = new Vector2(baseX, ground);
                Matrix4x4 before = GUI.matrix;
                GUI.matrix = RotateAbout(before, root, angle);
                float stain = .32f + .68f * Mathf.Abs(Mathf.Sin(i * 2.1f));
                Paint(new Rect(root.x - 8, root.y - size, 16 + (i % 4) * 2.3f, size),
                    new Color(.055f * stain, .13f * stain, .073f * stain, front * .42f), grassBlade);
                GUI.matrix = before;
            }
        }

        private void DrawLandingDebris(float time, float gain = 1)
        {
            float age = time - OpeningTiming.Impact;
            if (age < 0 || age > 2.25f || gain <= 0) return;
            var actor = OpeningStoryboard.Sample(OpeningTiming.Impact).Actor;
            for (int i = 0; i < 30; i++)
            {
                float direction = Mathf.Sin(i * 3.888f);
                float x = Width * actor.x + Mathf.Sqrt(age) * (32 + 161 * Mathf.Abs(direction)) * Mathf.Sign(direction);
                float y = Height * .89f - Mathf.Sin(Mathf.Min(age, 1.73f) * 1.68f) * (12 + i % 7 * 10);
                float opacity = gain * .47f * (1 - age / 2.25f) * (i % 3 == 0 ? .9f : .46f);
                Color ochre = new Color(.58f,.52f,.33f);
                if (i % 3 == 0)
                    Stroke(new Vector2(x, y), new Vector2(x + 11 * direction, y + 2 - 8 * Mathf.Cos(age * 4 + i)), 3.6f,
                        ochre, opacity, grassBlade);
                else SoftLight(x, y, 6 + i % 5 + age * 9, ochre, opacity);
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
