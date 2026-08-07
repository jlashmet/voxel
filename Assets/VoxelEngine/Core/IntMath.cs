using System.Runtime.CompilerServices;

namespace VoxelEngine.Core
{
    /// <summary>
    /// Integer-only math primitives for code that determines world state.
    ///
    /// Constitution Principle I: no authoritative state may derive from floating-point
    /// arithmetic. The specific hazard these replace is <c>(int)math.sqrt((float)n)</c> in
    /// expansion geometry: float sqrt is correctly rounded per IEEE-754, but the surrounding
    /// int-to-float conversion, the compiler's freedom to contract or reassociate, and
    /// differing FMA availability across CPU targets all mean two machines can land on
    /// either side of a boundary voxel. One voxel of disagreement is a permanent desync.
    ///
    /// Everything here is exact: same inputs give the same outputs on every target, and the
    /// results are Burst-friendly (no branches on float state, no library calls).
    /// </summary>
    public static class IntMath
    {
        /// <summary>
        /// Exact integer square root: the largest <c>r</c> with <c>r*r &lt;= value</c>.
        ///
        /// Uses the classic restoring bit-by-bit method, which touches only shifts,
        /// comparisons, and subtraction. Runs in a fixed 16 iterations for a 32-bit input.
        /// </summary>
        /// <param name="value">Non-negative radicand. Negative input returns 0.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Isqrt(int value)
        {
            if (value <= 0) return 0;

            uint n = (uint)value;
            uint result = 0;

            // Highest power of four <= 2^30, the largest that fits a positive int.
            uint bit = 1u << 30;
            while (bit > n) bit >>= 2;

            while (bit != 0)
            {
                if (n >= result + bit)
                {
                    n -= result + bit;
                    result = (result >> 1) + bit;
                }
                else
                {
                    result >>= 1;
                }

                bit >>= 2;
            }

            return (int)result;
        }

        /// <summary>
        /// Floor division for a power-of-two divisor, correct for negative dividends.
        ///
        /// Plain <c>/</c> truncates toward zero, which puts negative world coordinates in
        /// the wrong region. An arithmetic shift floors, which is what spatial decomposition
        /// requires.
        /// </summary>
        /// <param name="value">Dividend, may be negative.</param>
        /// <param name="log2Divisor">Base-2 logarithm of the divisor.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FloorDivPow2(int value, int log2Divisor) => value >> log2Divisor;

        /// <summary>
        /// Scales <paramref name="value"/> by the rational <paramref name="numerator"/> /
        /// <paramref name="denominator"/> without leaving integer arithmetic.
        ///
        /// This is the integer replacement for multiplying by a float ratio (density caps,
        /// budget fractions). Uses a 64-bit intermediate so the product cannot overflow.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MulDiv(int value, int numerator, int denominator)
        {
            if (denominator == 0) return 0;
            return (int)((long)value * numerator / denominator);
        }
    }
}
