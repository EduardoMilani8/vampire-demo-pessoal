namespace HollowTide.Core
{
    /// <summary>xorshift32 determinístico e sem alocação; permite testes reproduzíveis da simulação.</summary>
    public sealed class SimRandom
    {
        uint _state;

        public SimRandom(uint seed) => _state = seed == 0 ? 0x9E3779B9u : seed;

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>[0, 1)</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / (1 << 24));

        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>[0, exclusiveMax)</summary>
        public int NextInt(int exclusiveMax) => (int)(NextUInt() % (uint)exclusiveMax);
    }
}
