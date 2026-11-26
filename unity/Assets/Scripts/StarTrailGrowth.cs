using System;
using UnityEngine;

namespace Echoes
{
    [Serializable]
    internal struct StarTrailGrowth
    {
        public Vector3 origin;
        public float born;
        public int seed;
        public bool flower;

        public float Variation => (seed & 1023) / 1023f;
        public float Lifetime => 3.4f + 1.45f * Variation;
    }
}
