using System;

namespace Echoes
{
    // Height is shared by gameplay, hit tests, the camera and the projection.
    public sealed class SpiritFlight
    {
        public const float Floor = .79f;
        public const float Ceiling = .245f;
        public float Height { get; private set; } = .12f;
        public float Speed { get; private set; }
        public float Glide { get; private set; }
        public static float ScreenY(float height) => Floor + (Ceiling - Floor) * Clamp(height, 0, 1);

        public void Reset(float height = .12f, float ceiling = 1)
        {
            Height = Clamp(height, 0, ceiling);
            Speed = Glide = 0;
        }

        public void Release(float voice)
        {
            // Push from the current altitude so a second opening joins the same arc.
            Speed = Math.Max(Speed, 1.10f + .37f * Clamp(voice, 0, 1));
        }

        public void Deflect()
        {
            Speed = Math.Min(Speed, -.11f);
        }

        public void Assist() => Speed = Math.Max(Speed, .69f);

        public void Step(float dt, Hand hand)
        {
            if (dt <= 0) return;
            bool floating = hand == Hand.Open;
            float heightBrake = Clamp(Height * 7f, 0, 1);
            float terminal = (.067f + .17f * heightBrake) * (floating ? 1 : 1.42f);
            Speed = Math.Max(-terminal, Speed - dt * (Speed > 0 ? 1.30f : .74f));
            Height = Clamp(Height + Speed * dt, 0, 1);
            if ((Height == 0 && Speed < 0) || (Height == 1 && Speed > 0)) Speed = 0;
            float wanted = floating && Speed < -.037f ? Clamp(-Speed / .18f, 0, 1) * heightBrake : 0;
            Glide += (wanted - Glide) * (1 - (float)Math.Exp(-dt / .17f));
        }

        public void ReleaseForest(float voice)
        {
            float boost = Height < .022f ? 1.13f : 1.18f;
            Speed = Math.Max(Speed, boost + .27f * Clamp(voice, 0, 1));
        }

        public void StepForest(float dt, Hand hand, float ceiling)
        {
            bool glide = hand == Hand.Open;
            float support = Clamp(Height * 12, 0, 1);
            float fallSpeed = (.08f + .17f * support) * (glide ? 1 : 1.38f);
            Speed = Math.Max(-fallSpeed, Speed - dt * (Speed > 0 ? 1.43f : .68f));
            Height = Clamp(Height + Speed * dt, 0, ceiling);
            if (Height == 0 || Height == ceiling && Speed > 0) Speed = 0;
            float wanted = glide && Speed < -.035f ? Clamp(-Speed / .18f, 0, 1) * support : 0;
            Glide += (wanted - Glide) * (1 - (float)Math.Exp(-dt / .17f));
        }

        public void FollowForestCourse(float altitude, float dt)
        {
            Speed = (altitude - Height) / dt;
            Height = altitude;
            Glide += (Clamp(-Speed / .16f, 0, 1) - Glide) * (1 - (float)Math.Exp(-dt / .13f));
        }

        public void FallForest(float dt)
        {
            Speed = Math.Max(-1.62f, Math.Min(Speed, 0) - 1.55f * dt);
            Height = Math.Max(0, Height + Speed * dt);
            Glide = 0;
        }

        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}
