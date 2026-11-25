using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesView
    {
        private static readonly Color Amber = new Color(1f, .68f, .26f);
        private static readonly Color Scarlet = new Color(1f, .24f, .22f);

        public void DrawProjection(SpiritFrame frame, bool mirror, bool sideways, Rect? bounds = null,
            float characterScale = 1, float lightReach = 1)
        {
            if (Event.current.type != EventType.Repaint) return;
            Rect viewport = bounds ?? ScreenViewport;
            Paint(viewport, Color.black);
            GUI.BeginGroup(viewport);
            var area = new Rect(0, 0, viewport.width, viewport.height);
            float bodySize = sideways ? Mathf.Min(area.width * .50f, area.height * .92f)
                                      : Mathf.Min(area.height * .67f, area.width * .93f);
            if (frame.forest || frame.ruins) bodySize *= .78f;
            bodySize *= Mathf.Clamp(characterScale, .35f, 1.40f);
            bodySize = Mathf.Min(bodySize, Mathf.Min(area.width, area.height) * (sideways ? 1.34f : .88f));
            float unit = Mathf.Min(area.height / Height, area.width / 880);
            float extent = Mathf.Max(.20f, unit) * Mathf.Clamp(lightReach, .65f, 2.45f);
            var center = FrameProjectionPosition(frame, frame.altitude, area, bodySize, sideways);
            var source = FrameProjectionPosition(frame, frame.releaseAltitude, area, bodySize, sideways);
            if (frame.openingTime >= 0)
            {
                var shot = OpeningStoryboard.Sample(frame.openingTime);
                Vector3 actor = shot.Actor;
                float shotSize = bodySize * Mathf.Lerp(.86f, 1.13f, Mathf.InverseLerp(1, 1.8f, actor.z));
                center = ProjectionPosition(actor.y, area, shotSize, sideways);
                Vector2 crossAxis = sideways ? Vector2.up * area.height : Vector2.right * area.width;
                center += crossAxis * (actor.x - .5f) * (mirror ? -.42f : .42f);
                DrawCharacter(frame, center, center, shotSize, mirror, sideways ? -90 : 0, extent, true, .78f);
                DrawOpeningFireflies(frame.openingTime, shot, center, shotSize, mirror, sideways ? -90 : 0,
                    true, false, extent);
            }
            else
            {
                float visibility = 1;
                if (frame.forest)
                {
                    Vector2 axis = sideways ? Vector2.down : Vector2.right;
                    Vector2 lane = axis * (sideways ? area.height : area.width) * frame.forestLane * (mirror ? -.063f : .063f);
                    center += lane;
                    source += lane;
                    float swallowed = Mathf.Max(frame.forestFall, frame.forestDoorAge / 3f);
                    if (swallowed >= 0)
                    {
                        Vector2 down = sideways ? Vector2.right * area.width : Vector2.up * area.height;
                        center += down * (.20f * OpeningStoryboard.Ease(.08f,.81f,swallowed));
                        visibility = 1 - OpeningStoryboard.Ease(.64f,.96f,swallowed);
                        if (frame.forestDoor == 0 && frame.forestDoorAge >= 0)
                            bodySize *= Mathf.Lerp(1,.08f,OpeningStoryboard.Ease(.62f,.86f,swallowed));
                    }
                    if (frame.forestDescent >= 0)
                    {
                        float descent = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.44f, 1, frame.forestDescent));
                        Vector2 down = sideways ? Vector2.right * area.width : Vector2.up * area.height;
                        center += down * (.24f * descent);
                        visibility = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.68f, .98f, frame.forestDescent));
                    }
                }
                if (frame.ruins)
                {
                    Vector2 lateral = sideways ? Vector2.down : Vector2.right;
                    Vector2 lane = lateral * (sideways ? area.height : area.width) * frame.ruinLane * (mirror ? -.13f : .13f);
                    center += lane;
                    source += lane;
                    if (frame.ruinTransfer >= 0)
                        visibility -= .40f * Mathf.Sin(Mathf.Clamp01(frame.ruinTransfer) * Mathf.PI);
                    DrawRuinFinds(frame, center, bodySize, mirror, sideways);
                }
                if (!frame.forest || frame.forestDescent < 0)
                    DrawFlightTrail(frame, palette[Mathf.Clamp(frame.chapter, 0, 2)], 1, extent, area, bodySize, sideways);
                DrawCharacter(frame, center, source, bodySize, mirror, sideways ? -90 : 0, extent, true, visibility, visibility);
                if (frame.forest) DrawForestSwarm(frame, center, bodySize, mirror, sideways ? -90 : 0, visibility, true);
                else if (!frame.ruins) DrawLiveHandoff(frame, center, bodySize, mirror, sideways ? -90 : 0, extent, visibility);
            }
            GUI.EndGroup();
        }

        private static Vector2 FrameProjectionPosition(SpiritFrame frame, float altitude, Rect area, float size, bool sideways)
        {
            if (!frame.forest && !frame.ruins) return ProjectionPosition(SpiritFlight.ScreenY(altitude), area, size, sideways);
            float axis = sideways ? area.width : area.height;
            float edge = Mathf.Min(size * .52f + 7, axis * .43f);
            float height = Mathf.Lerp(axis - edge, edge, Mathf.Clamp01(altitude / (frame.ruins ? Journey.RuinHeightLimit : 1.34f)));
            return sideways ? new Vector2(height, area.height * .54f) : new Vector2(area.width * .5f, height);
        }

        private static Vector2 ProjectionPosition(float normalizedY, Rect area, float size, bool sideways)
        {
            float flight = Mathf.Clamp01(Mathf.InverseLerp(SpiritFlight.Ceiling - .02f, SpiritFlight.Floor + .04f, normalizedY));
            float axis = sideways ? area.width : area.height;
            float edge = Mathf.Min(size * .55f, axis * .46f);
            float from = Mathf.Min(axis * .49f, Mathf.Max(axis * .20f, edge + (sideways ? 7 : 22)));
            float to = Mathf.Max(axis * .51f, Mathf.Min(axis * .91f, axis - edge - 5));
            float height = Mathf.Lerp(from, to, flight);
            return sideways ? new Vector2(height, area.height * .54f) : new Vector2(area.width * .5f, height);
        }

        private void DrawFlightTrail(SpiritFrame frame, Color color, float strength, float extent, Rect viewport,
            float bodySize = 0, bool sideways = false)
        {
            Vector2 spreadAxis = sideways ? Vector2.up : Vector2.right;
            for (int index = 1; index < flightTrace.Count; index++)
            {
                Vector4 point = flightTrace[index];
                Vector4 before = flightTrace[index - 1];
                float age = frame.motionTime - point.y;
                float speed = point.w;
                float fade = Mathf.Clamp01(1 - age / (.75f + speed * 1.70f));
                fade *= fade * point.z * strength;
                if (fade < .018f) continue;
                Vector2 p = bodySize == 0
                    ? new Vector2(CenterX, CharacterY(point.x))
                    : FrameProjectionPosition(frame, point.x, viewport, bodySize, sideways);
                Vector2 q = bodySize == 0
                    ? new Vector2(CenterX, CharacterY(before.x))
                    : FrameProjectionPosition(frame, before.x, viewport, bodySize, sideways);
                float drift = (5 + age * 28f + speed * 24) * extent;
                float oldDrift = drift + 1.5f * extent;
                float thickness = Mathf.Max(.85f, (7 + speed * 13) * extent);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 a = q + spreadAxis * side * oldDrift;
                    Vector2 b = p + spreadAxis * side * drift;
                    Stroke(a, b, thickness, color, fade * (.23f + speed * .37f), glow);
                    if (index % 4 == 0 && speed > .32f)
                        Orb(b.x, b.y, (4 + speed * 6) * extent, color, fade * .48f);
                }
            }
        }

        private void Stroke(Vector2 a, Vector2 b, float thickness, Color color, float alpha, Texture2D texture = null)
        {
            Vector2 direction = b - a;
            if (direction.sqrMagnitude < .12f || alpha <= .004f) return;
            Matrix4x4 before = GUI.matrix;
            GUI.matrix = RotateAbout(before, a, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            Paint(new Rect(a.x, a.y - thickness * .5f, direction.magnitude, thickness), Opacity(color, alpha), texture);
            GUI.matrix = before;
        }

        private void DrawActiveFirefly(Journey journey, bool split)
        {
            var fly = journey.Fireflies;
            float gain = split ? .78f : 1;
            if (fly.Kind != FireflyKind.None)
            {
                float t = journey.PoseTime;
                bool red = fly.Kind == FireflyKind.Scarlet;
                float flutter = (red ? 11 : 6) * Mathf.Sin(t * (red ? 7.6f : 2.8f));
                float x = Width * fly.X;
                float y = CharacterY(fly.Altitude) + flutter;
                if (journey.IsObserving)
                {
                    Color cue = red ? Scarlet : Amber;
                    for (int i = 1; i <= 5; i++)
                        Orb(x - i * 19, y + 10 * Mathf.Sin(t * 5 - i * .48f), 5, cue, gain * (1 - i / 6f) * .46f);
                    Ripple(x, y, 27 + 3 * Mathf.Sin(t * 4.6f), cue, gain * .45f);
                }
                DrawFirefly(new Vector2(x, y), t, red, 1, gain);
            }
            if (fly.Pulse <= 0) return;
            float p = 1 - fly.Pulse / FireflyCourse.ContactDuration;
            Color color = fly.Response == FireflyKind.Scarlet ? Scarlet : Amber;
            Ripple(Width * fly.X, CharacterY(fly.Altitude), 22 + 62 * p, color, (1 - p) * gain * (split ? .38f : .55f));
        }

        private Vector2 OpeningFlyOffset(float time, OpeningStoryboard.Shot shot, bool red)
        {
            Vector3 fly = OpeningStoryboard.Firefly(time, red);
            var path = new Vector2((fly.x - shot.Actor.x) * Width, (fly.y - shot.Actor.y) * Height);
            path /= 162 * shot.Actor.z;
            Vector2 fingers = HandPosition;
            float attach = OpeningStoryboard.Contact(time, red);
            var air = new Vector2(.016f + .056f * (1 - attach),
                -.026f + .013f * Mathf.Sin(time * (red ? 7.0f : 4.7f))) * (1 - .78f * attach);
            return Vector2.Lerp(path, fingers + air, attach);
        }

        private Vector2 HandPosition => spirit.Articulated ? spirit.Landmark(1) : new Vector2(.23f, -.07f);

        private void DrawOpeningFireflies(float time, OpeningStoryboard.Shot shot, Vector2 center, float size,
            bool mirror, float rotation, bool projection, bool split, float extent)
        {
            for (int i = 0; i < 2; i++)
            {
                bool red = i == 1;
                float near = OpeningStoryboard.NearHand(time, red);
                float opacity = Mathf.Clamp01(OpeningStoryboard.Firefly(time, red).z) *
                    (projection ? near : split ? .57f - near * .40f : 1);
                if (opacity <= .008f) continue;
                Vector2 offset = OpeningFlyOffset(time, shot, red);
                if (projection) offset = Vector2.Lerp(Vector2.ClampMagnitude(offset, .63f), offset, near);
                Vector2 at = center + PortraitOffset(offset, mirror, rotation) * size;
                float contact = OpeningStoryboard.Contact(time, red);
                float asLight = red ? OpeningStoryboard.Ease(27.05f, 27.6f, time) :
                    OpeningStoryboard.Ease(17.6f, 19.5f, time);
                float lampScale = projection ? Mathf.Clamp(size / 275, .62f, 1.65f) : 1;
                DrawFirefly(at, time, red, lampScale * (red ? 1.23f : 1.48f), opacity, asLight);
                Color color = red ? Scarlet : Amber;
                float reach = Mathf.Max(.28f, extent);
                float radius = 25 + 10 * (1 + Mathf.Sin(time * 4.2f));
                SoftLight(at.x, at.y, radius * reach, color, opacity * contact * .22f);
            }
        }

        private void DrawLiveHandoff(SpiritFrame frame, Vector2 center, float size, bool mirror, float rotation,
            float extent, float opacity)
        {
            if (frame.fireflyPulse <= 0 || frame.fireflyResponse == FireflyKind.None) return;
            float age = FireflyCourse.ContactDuration - frame.fireflyPulse;
            float visible = OpeningStoryboard.Window(age, 0, .12f, .34f, FireflyCourse.ContactDuration);
            bool red = frame.fireflyResponse == FireflyKind.Scarlet;
            Vector2 departure = new Vector2(.06f, -.27f) * Mathf.Pow(Mathf.Clamp01((age - .25f) / .53f), 1.38f);
            Vector2 at = center + PortraitOffset(HandPosition + new Vector2(.014f, -.021f) + departure, mirror, rotation) * size;
            float lampScale = Mathf.Clamp(size / 210, .65f, 1.60f);
            DrawFirefly(at, frame.motionTime, red, lampScale * (1 - age * .33f), visible * opacity,
                OpeningStoryboard.Ease(.16f, .46f, age));
            float radius = (17 + age * 65) * Mathf.Max(.24f, extent);
            Ripple(at.x, at.y, radius, red ? Scarlet : Amber, visible * opacity * .46f);
        }

        private void DrawForestSwarm(SpiritFrame frame, Vector2 center, float body, bool mirror, float rotation,
            float gain, bool approach)
        {
            Vector2 Place(Vector2 offset) => center + PortraitOffset(offset, mirror, rotation) * body;
            float scale = Mathf.Clamp(body / 180, .27f, 3.3f);
            if (approach && frame.forestFlies != null)
            {
                foreach (ForestFlyView fly in frame.forestFlies)
                {
                    float far = Mathf.Clamp01(Mathf.Abs(fly.depth) / 8);
                    Vector2 offset = new Vector2(.22f * fly.lane + .044f, -.93f * fly.height - .28f * far - .13f);
                    float pulse = fly.red ? .68f+.32f*fly.ScarletPulse(frame.time) : 1;
                    DrawFirefly(Place(offset), frame.motionTime + fly.id * .071f, fly.red,
                        scale * (1 - .47f * far), fly.visibility * gain * pulse);
                }
            }

            JourneyCue cue = (JourneyCue)frame.forestEvent;
            float age = frame.forestEventAge;
            bool settling = cue == JourneyCue.ForestAmber && age < .65f;
            for (int i = 0; i < frame.forestReserve; i++)
            {
                Vector2 offset = ForestOrbit(i, frame.motionTime);
                if (settling && i == frame.forestReserve - 1)
                {
                    Vector2 source = new Vector2(.22f*frame.forestContactLane+.044f,
                        -.93f*frame.forestContactHeight-.13f-.035f*Mathf.Abs(frame.forestContactDepth));
                    offset = Vector2.Lerp(HandPosition, offset, OpeningStoryboard.Ease(0, .65f, age));
                    offset += (source-HandPosition)*(1-OpeningStoryboard.Ease(0,.19f,age));
                }
                DrawFirefly(Place(offset), frame.motionTime + .13f * i, false, scale * .34f, gain * .79f);
            }
            if (cue == JourneyCue.ForestScarlet && age < .67f)
            {
                float drift = OpeningStoryboard.Ease(0,.67f,age);
                Vector2 departing = ForestOrbit(frame.forestReserve, frame.motionTime) + new Vector2(.19f, .45f) * drift;
                DrawFirefly(Place(departing), frame.motionTime, false, scale * .34f * (1 - .44f * drift), gain * (1 - drift));
                Ripple(Place(departing).x, Place(departing).y, body * (.075f + .19f * drift), Scarlet, (1 - drift) * gain * .5f);
            }
            if (frame.forestRescue && frame.forestSnareAge >= .72f)
            {
                float rescue = OpeningStoryboard.Ease(.72f, 1.85f, frame.forestSnareAge);
                Vector2 departure = frame.forestTrap == ForestTrapKind.Web ? new Vector2(.24f,-.44f) : new Vector2(.10f,.53f);
                DrawFirefly(Place(Vector2.Lerp(ForestOrbit(frame.forestReserve, frame.motionTime),departure,rescue)),
                    frame.motionTime,false,scale * .47f,gain * Mathf.Sin(Mathf.PI * rescue));
            }
            if (cue == JourneyCue.Plant && age < 1.34f)
            {
                float seed = OpeningStoryboard.Ease(0,1.34f,age);
                Vector2 target = new Vector2(.24f * frame.forestContactLane,
                    -.93f * frame.forestContactHeight+.26f);
                Vector2 offset = Vector2.Lerp(ForestOrbit(frame.forestReserve,frame.motionTime),target,seed);
                DrawFirefly(Place(offset),frame.motionTime,false,scale*(.34f+.34f*seed),gain*(1-seed));
            }
            if (frame.forestTrap == ForestTrapKind.Cable && frame.forestDescent >= 0 && approach)
            {
                float coil = OpeningStoryboard.Ease(.30f,.71f,frame.forestDescent);
                for (int turn = 0; turn < 3; turn++)
                {
                    Vector2 from = Place(new Vector2(-.35f, .49f-turn*.23f));
                    for (int segment = 1; segment < 10; segment++)
                    {
                        float fraction = segment / 9f;
                        Vector2 next = Place(new Vector2(-.35f+fraction*.68f*coil,
                            .49f-turn*.23f+.055f*Mathf.Sin(fraction*6.28f+turn)));
                        if (coil > .02f) Stroke(from, next, 2.8f*scale, Mint, gain * coil * .70f, glow);
                        from = next;
                    }
                }
            }
        }

        private static Vector2 ForestOrbit(int index, float time)
        {
            float phase = time * (index < 6 ? 1.24f : -1.06f) + 2.399963f * index;
            float ring = index < 6 ? 1 : 1.43f;
            return new Vector2(Mathf.Sin(phase)*.31f*ring, -.22f + Mathf.Cos(phase)*.19f*ring + .043f*(index%3));
        }

        private static Vector2 PortraitOffset(Vector2 offset, bool mirror, float rotation)
        {
            if (mirror) offset.x = -offset.x;
            Vector3 point = Quaternion.Euler(0, 0, rotation) * offset;
            return new Vector2(point.x, point.y);
        }

        private void SoftLight(float x, float y, float radius, Color color, float opacity) =>
            Paint(new Rect(x - radius, y - radius, radius * 2, radius * 2), Opacity(color, opacity), glow);

        private void DrawFirefly(Vector2 at, float t, bool red, float size, float opacity, float asLight = 0)
        {
            if (opacity <= .008f) return;
            Color hue = red ? Scarlet : Amber;
            float beat = Mathf.Sin(t * (red ? 13.4f : 4.5f));
            float glowSize = ((red ? 30 : 37) + beat * (red ? 9 : 5)) * size;
            SoftLight(at.x, at.y, glowSize, hue, opacity * (.48f + beat * .095f));
            float wing = (4.6f + 5.7f * Mathf.Abs(Mathf.Sin(t * (red ? 36 : 25)))) * size * (1 - asLight);
            if (wing > 1)
            {
                var tint = Color.Lerp(hue, new Color(.96f, .99f, .92f), .43f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 joint = at + new Vector2(side * 2.2f, -.9f) * size;
                    float angle = Mathf.Sin(t * (red ? 6.0f : 4.5f)) * 2.4f;
                    Stroke(joint, joint + new Vector2(side * wing, (-5.5f + side * angle) * size), 4.3f * size,
                        tint, opacity * .79f, glow);
                }
            }
            float center = (red ? 5.5f : 6.7f) * size;
            Orb(at.x, at.y, center, Color.Lerp(hue, new Color(1, .97f, .78f), red ? .3f : .68f), opacity);
            if (red) Ripple(at.x, at.y, (13 + Mathf.Max(0, beat) * 8) * size, hue, opacity * .32f);
        }

    }
}
