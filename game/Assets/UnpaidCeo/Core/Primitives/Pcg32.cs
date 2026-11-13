using System;

namespace UnpaidCeo.Core.Primitives
{
    public struct PcgState
    {
        public ulong S;
        public ulong Inc;
    }

    /// <summary>PCG32 (XSH-RR 64/32), the only RNG allowed in the core. Deterministic on every platform.</summary>
    public sealed class Pcg32
    {
        const ulong Multiplier = 6364136223846793005UL;
        ulong _state;
        ulong _inc;

        Pcg32() { }

        public Pcg32(ulong seed, ulong seq)
        {
            _state = 0UL;
            _inc = (seq << 1) | 1UL;
            NextUInt();
            unchecked { _state += seed; }
            NextUInt();
        }

        public PcgState State => new PcgState { S = _state, Inc = _inc };

        public static Pcg32 FromState(PcgState s) => new Pcg32 { _state = s.S, _inc = s.Inc };

        public uint NextUInt()
        {
            unchecked
            {
                ulong old = _state;
                _state = old * Multiplier + _inc;
                uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
                int rot = (int)(old >> 59);
                return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
            }
        }

        /// <summary>Uniform integer in [0, maxExclusive) without modulo bias.</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            uint bound = (uint)maxExclusive;
            uint threshold = (uint)((0x100000000UL - bound) % bound);
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold) return (int)(r % bound);
            }
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble() => NextUInt() / 4294967296.0;
    }
}
