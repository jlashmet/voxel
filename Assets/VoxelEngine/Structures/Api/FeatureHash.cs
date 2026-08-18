using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Integer hashing for placement and identity.
    ///
    /// Every placement decision in the world comes out of these functions, and two clients agree
    /// about where a castle is only because they agree here. That makes the mixing quality a
    /// correctness property rather than an aesthetic one: a hash that correlates across
    /// neighbouring cells produces villages in rows, and one that correlates across definition ids
    /// produces every definition choosing the same cells.
    ///
    /// 64-bit output for identity, because identity must stay distinct across the whole world
    /// rather than within a region.
    /// </summary>
    public static class FeatureHash
    {
        /// <summary>SplitMix64 finaliser. Well-mixed, cheap, and identical on every platform.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Mix(ulong v)
        {
            v ^= v >> 30; v *= 0xbf58476d1ce4e5b9ul;
            v ^= v >> 27; v *= 0x94d049bb133111ebul;
            v ^= v >> 31;
            return v;
        }

        /// <summary>Hashes a cell, a definition, and the world seed into an independent stream.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Cell(uint seed, int definitionId, int3 cell)
        {
            // Large odd multipliers keep the axes from aliasing onto each other, which would make
            // the diagonal of the world behave differently from the axes.
            ulong h = seed;
            h = Mix(h ^ ((ulong)(uint)definitionId * 0x9E3779B97F4A7C15ul));
            h = Mix(h ^ ((ulong)(uint)cell.x * 0xC2B2AE3D27D4EB4Ful));
            h = Mix(h ^ ((ulong)(uint)cell.y * 0x165667B19E3779F9ul));
            h = Mix(h ^ ((ulong)(uint)cell.z * 0x27D4EB2F165667C5ul));
            return h;
        }

        /// <summary>
        /// Hash of an unordered pair of cells, used where two neighbours must agree without
        /// talking — cave portals in particular. Ordering the pair canonically is what makes both
        /// sides derive the same answer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong CellPair(uint seed, int3 a, int3 b)
        {
            bool aFirst = a.x != b.x ? a.x < b.x
                        : a.y != b.y ? a.y < b.y
                        : a.z <= b.z;

            int3 lo = aFirst ? a : b;
            int3 hi = aFirst ? b : a;

            ulong h = Mix(seed ^ 0x51_75_A1_7Eul);
            h = Mix(h ^ ((ulong)(uint)lo.x * 0xC2B2AE3D27D4EB4Ful));
            h = Mix(h ^ ((ulong)(uint)lo.y * 0x165667B19E3779F9ul));
            h = Mix(h ^ ((ulong)(uint)lo.z * 0x27D4EB2F165667C5ul));
            h = Mix(h ^ ((ulong)(uint)hi.x * 0x9E3779B97F4A7C15ul));
            h = Mix(h ^ ((ulong)(uint)hi.y * 0xBF58476D1CE4E5B9ul));
            h = Mix(h ^ ((ulong)(uint)hi.z * 0x94D049BB133111EBul));
            return h;
        }

        /// <summary>
        /// Derives an independent deterministic stream from a parent instance seed and a stable
        /// semantic key. Callers should use meaning-based keys such as "roof", "windows.north",
        /// or "crypt" rather than allocation/order indices so adding an unrelated component does
        /// not perturb existing generated details.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Semantic(ulong parentSeed, in FixedString64Bytes semanticKey)
        {
            ulong h = Mix(parentSeed ^ 0x53_45_4D_41_4E_54_49_43ul);
            for (var i = 0; i < semanticKey.Length; i++)
            {
                h = Mix(h ^ ((ulong)semanticKey[i] + ((ulong)(uint)i << 32)));
            }

            // Mix the length separately so a trailing zero byte cannot alias a shorter key.
            return Mix(h ^ (uint)semanticKey.Length);
        }

        /// <summary>Advances a stream, so successive draws from one candidate are independent.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Next(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15ul;
            return Mix(state);
        }

        /// <summary>
        /// Uniform integer in [min, max] inclusive.
        ///
        /// Uses the multiply-shift reduction rather than a modulo: modulo of a power-of-two-biased
        /// stream leaves a visible bias in the low bits, which shows up as placement favouring
        /// particular offsets within a cell.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Range(ref ulong state, int min, int max)
        {
            if (max <= min) return min;

            ulong span = (ulong)(max - min + 1);
            ulong draw = Next(ref state);
            return min + (int)(((draw >> 32) * span) >> 32);
        }

        /// <summary>True with probability <paramref name="chanceOutOf65536"/> / 65536.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Chance(ref ulong state, int chanceOutOf65536)
        {
            if (chanceOutOf65536 <= 0) return false;
            if (chanceOutOf65536 >= 65536) return true;

            return (int)((Next(ref state) >> 40) & 0xFFFF) < chanceOutOf65536;
        }
    }
}
