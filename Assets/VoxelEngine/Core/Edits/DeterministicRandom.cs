using System;
using System.Runtime.CompilerServices;
using VoxelEngine.Edits.Api;

namespace VoxelEngine.Core.Edits
{
    /// <summary>
    /// Seeded integer-only pseudo-random number generator using the xoshiro128** algorithm.
    ///
    /// Produces identical sequences across all platforms given the same seed — this is a
    /// hard requirement for cross-client agreement (Constitution Principle III: Determinism).
    /// The algorithm uses only 32-bit integer operations and bitwise manipulation, with no
    /// floating-point or platform-dependent arithmetic.
    ///
    /// State is four uints (128 bits total). The internal state is never exposed to callers;
    /// all values are generated through the public API which returns new instances (stateless
    /// from the caller's perspective — each call creates a new DeterministicRandom with an
    /// updated seed derived from the previous one, avoiding aliasing issues).
    ///
    /// For Burst compatibility this type has no dependencies beyond primitive integer ops.
    /// It contains no virtual calls, no allocations, and no platform-specific intrinsics.
    /// </summary>
    public struct DeterministicRandom : IEquatable<DeterministicRandom>
    {
        // -- state ---------------------------------------------------------------

        /// <summary>xoshiro128** internal state: four uints.</summary>
        // Mutable: NextUint advances the generator state in place.
        private uint s0, s1, s2, s3;

        // -- construction --------------------------------------------------------

        /// <summary>
        /// Construct from a single seed value. The seed is expanded into four state words
        /// using splitmix32, which provides good bit dispersion even for clustered seeds.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DeterministicRandom(uint seed)
        {
            uint x = seed;
            x = Mix(x);
            s0 = x;
            x = Mix(x + 0x9E3779B9u); // golden ratio constant for state separation
            s1 = x;
            x = Mix(x + 0x9E3779B9u);
            s2 = x;
            x = Mix(x + 0x9E3779B9u);
            s3 = x;
        }

        /// <summary>Construct from four seed words directly. Useful for seeding from region coordinates.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DeterministicRandom(uint w0, uint w1, uint w2, uint w3)
        {
            s0 = w0;
            s1 = w1;
            s2 = w2;
            s3 = w3;
        }

        // -- public API ----------------------------------------------------------

        /// <summary>
        /// Generate the next pseudo-random uint in the sequence.
        /// The sequence is identical across all platforms given the same initial seed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint NextUint()
        {
            uint result = RotateRight(s3, 13) * 5u + 9u; // xoshiro128** output function
            uint s = s0;
            uint t = s1 << 9;

            s2 ^= s;
            s3 ^= s;
            s2 = RotateLeft(s2, 11) ^ t;
            s3 = RotateLeft(s3, 9);
            s0 = Mix(s2 + s3);
            s1 = Mix(s3 + s);

            return result;
        }

        /// <summary>Generate a signed integer in the full int range [-2147483648 .. 2147483647].</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int NextInt() => (int)NextUint();

        /// <summary>Generate a byte value [0 .. 255].</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte NextByte() => (byte)(NextUint() & 0xFF);

        /// <summary>Generate an integer in [min, max] inclusive. Both bounds are valid outputs.</summary>
        /// <param name="min">Minimum value (inclusive).</param>
        /// <param name="max">Maximum value (inclusive). Must be >= min.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when max &lt; min.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int NextRange(int min, int max)
        {
            if (max < min)
                throw new ArgumentOutOfRangeException(nameof(max), "max must be >= min");

            // Use modular reduction on the full uint range to avoid bias.
            var range = (uint)(max - min);
            uint r = NextUint() % (range + 1);
            return min + (int)r;
        }

        /// <summary>Generate a value in [0, maxExclusive). Alias for NextRange(0, maxExclusive - 1).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int NextRangeExclusive(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Must be > 0");

            return (int)(NextUint() % (uint)maxExclusive);
        }

        /// <summary>Generate a boolean with approximately equal probability of true or false.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NextBool() => ((NextUint() >> 31) & 1u) == 1u;

        /// <summary>
        /// Create a new DeterministicRandom derived from this one's state, allowing forked
        /// random sequences without modifying the original. Useful for generating sub-sequences
        /// (e.g., per-brick debris material within an explosion).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DeterministicRandom Fork() => new DeterministicRandom(NextUint(), NextUint(), NextUint(), NextUint());

        // -- static helpers ------------------------------------------------------

        /// <summary>Splitmix32 hash function for seed expansion. Deterministic across platforms.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint x)
        {
            x ^= x >> 16;
            x *= 0x85ebca6bu;
            x ^= x >> 13;
            x *= 0xc2b2ae35u;
            x ^= x >> 16;
            return x;
        }

        /// <summary>Left-rotate a uint by the given bit count (0..31).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateLeft(uint value, int count) =>
            (value << count) | (value >> (32 - count));

        /// <summary>Right-rotate a uint by the given bit count (0..31).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateRight(uint value, int count) =>
            (value >> count) | (value << (32 - count));

        // -- equality ------------------------------------------------------------

        public bool Equals(DeterministicRandom other) =>
            s0 == other.s0 && s1 == other.s1 && s2 == other.s2 && s3 == other.s3;

        public override bool Equals(object obj) => obj is DeterministicRandom o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                var h = s0.GetHashCode();
                h = (h * 397) ^ s1.GetHashCode();
                h = (h * 397) ^ s2.GetHashCode();
                h = (h * 397) ^ s3.GetHashCode();
                return h;
            }
        }

        public static bool operator ==(DeterministicRandom a, DeterministicRandom b) => a.Equals(b);
        public static bool operator !=(DeterministicRandom a, DeterministicRandom b) => !a.Equals(b);
    }
}
