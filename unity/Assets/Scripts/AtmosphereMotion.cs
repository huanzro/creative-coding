using System;

namespace Echoes
{
    // Visual pace is independent of tutorial and encounter clocks.
    public sealed class AtmosphereMotion
    {
        public float Drift { get; private set; }
        public float Wake { get; private set; }
        public float Energy { get; private set; }

        public void Reset() { Drift = Wake = Energy = 0; }

        public void Step(float dt, Hand hand, float voice, bool active, float speed)
        {
            float targetWake = !active ? 0 : hand == Hand.Open ? .84f : hand == Hand.Fist ? .24f : .52f;
            Energy += ((active ? voice : 0) - Energy) * (1 - (float)Math.Exp(-dt / .24));
            Wake += (targetWake - Wake) * (1 - (float)Math.Exp(-dt / .5));
            if (active) Drift += dt * speed * (.035f + Energy * .235f) * (hand == Hand.Fist ? .65f : 1);
        }
    }
}
