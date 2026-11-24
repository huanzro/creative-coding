using System;

namespace Echoes
{
    public enum FireflyKind { None, Amber, Scarlet }

    // Only one firefly approaches at a time. Red is a warning with a readable exit.
    public sealed class FireflyCourse
    {
        private readonly Random random;
        private float untilArrival, quiet, pulseLeft;
        private bool previousWasScarlet;
        public FireflyKind Kind { get; private set; }
        public FireflyKind Response { get; private set; }
        public float X { get; private set; }
        public float Altitude { get; private set; }
        public float Pulse => pulseLeft;

        public FireflyCourse() : this(new Random()) { }
        public FireflyCourse(int seed) : this(new Random(seed)) { }
        private FireflyCourse(Random random) { this.random = random; Reset(); }

        public void Reset()
        {
            Kind = Response = FireflyKind.None;
            pulseLeft = 0;
            untilArrival = 2.6f;
            previousWasScarlet = true;
            quiet = 0;
        }

        public FireflyKind Step(float dt, float voice, float playerAltitude, Hand hand, bool observing)
        {
            pulseLeft = Math.Max(0, pulseLeft - dt);
            quiet = Math.Max(0, quiet - dt);
            if (Kind == FireflyKind.None)
            {
                if (quiet > 0) return FireflyKind.None;
                untilArrival -= dt;
                if (untilArrival > 0) return FireflyKind.None;
                bool scarlet = !previousWasScarlet && random.NextDouble() < .33;
                previousWasScarlet = scarlet;
                Kind = scarlet ? FireflyKind.Scarlet : FireflyKind.Amber;
                X = 1.07f;
                Altitude = .09f + (float)random.NextDouble() * .69f;
                return FireflyKind.None;
            }

            X -= dt * (.125f + .074f * voice) * (observing ? .62f : 1);
            if (Kind == FireflyKind.Amber && X > .43f && X < .72f && hand == Hand.Open &&
                voice >= .12f && voice <= .45f)
            {
                Altitude += (playerAltitude - Altitude) * (1 - (float)Math.Exp(-dt * 2.1));
            }
            bool near = Math.Abs(X - .5f) < .054f && Math.Abs(Altitude - playerAltitude) < .115f;
            if (near && hand == Hand.Open && Kind == FireflyKind.Amber)
                return Depart(FireflyKind.Amber);
            if (near && Kind == FireflyKind.Scarlet && hand != Hand.Missing)
                return Depart(FireflyKind.Scarlet);
            if (X < -.08f) Depart(FireflyKind.None);
            return FireflyKind.None;
        }

        private FireflyKind Depart(FireflyKind result)
        {
            Kind = FireflyKind.None;
            Response = result;
            pulseLeft = result == FireflyKind.None ? 0 : .78f;
            quiet = 1.3f;
            untilArrival = 1.9f + (float)random.NextDouble() * 2.8f;
            return result;
        }
    }
}
