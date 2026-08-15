using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace VoxelEngine.Storage.Runtime.Occupancy
{
    /// <summary>
    /// Bit-per-voxel occupancy for one brick: 512 bits across 8 x 64-bit words.
    ///
    /// This is the most-consumed structure in the engine. It is built once and read
    /// five ways — empty-space skipping in extraction, streaming detail level,
    /// far-field replication payload, connectivity flood-fill, and support
    /// propagation. Its layout is effectively a public interface; changing it
    /// ripples everywhere.
    ///
    /// All operations are integer and bitwise (Constitution Principle I).
    /// </summary>
    public static class OccupancyMask
    {
        public const int WordCount = global::VoxelEngine.Storage.Runtime.VoxelDimensions.OccupancyWordsPerBrick; // 8

        /// <summary>
        /// Linear voxel index within a brick. Layout is x-major within a row, then y,
        /// then z, so consecutive x steps are consecutive bits — traversal along x
        /// most often, and this keeps that walk inside one word wherever possible.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int VoxelIndex(int x, int y, int z) =>
            x | (y << global::VoxelEngine.Storage.Runtime.VoxelDimensions.BrickEdgeLog2)
              | (z << (global::VoxelEngine.Storage.Runtime.VoxelDimensions.BrickEdgeLog2 * 2));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Get(in NativeArray<ulong> words, int wordOffset, int voxelIndex)
        {
            var w = words[wordOffset + (voxelIndex >> 6)];
            return (w & (1UL << (voxelIndex & 63))) != 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Set(ref NativeArray<ulong> words, int wordOffset, int voxelIndex, bool occupied)
        {
            var i = wordOffset + (voxelIndex >> 6);
            var bit = 1UL << (voxelIndex & 63);
            var w = words[i];
            words[i] = occupied ? (w | bit) : (w & ~bit);
        }

        /// <summary>
        /// True when no voxel in the brick is occupied. One of the two hot early-outs
        /// in extraction/traversal — an empty brick is skipped without touching voxel data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEmpty(in NativeArray<ulong> words, int wordOffset)
        {
            ulong acc = 0UL;
            for (var i = 0; i < WordCount; i++) acc |= words[wordOffset + i];
            return acc == 0UL;
        }

        /// <summary>True when every voxel in the brick is occupied.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFull(in NativeArray<ulong> words, int wordOffset)
        {
            ulong acc = ulong.MaxValue;
            for (var i = 0; i < WordCount; i++) acc &= words[wordOffset + i];
            return acc == ulong.MaxValue;
        }

        /// <summary>Population count across the brick. Used by density caps (FR-019).</summary>
        public static int CountOccupied(in NativeArray<ulong> words, int wordOffset)
        {
            var total = 0;
            for (var i = 0; i < WordCount; i++)
                total += math_countbits(words[wordOffset + i]);
            return total;
        }

        public static void Clear(ref NativeArray<ulong> words, int wordOffset)
        {
            for (var i = 0; i < WordCount; i++) words[wordOffset + i] = 0UL;
        }

        public static void Fill(ref NativeArray<ulong> words, int wordOffset)
        {
            for (var i = 0; i < WordCount; i++) words[wordOffset + i] = ulong.MaxValue;
        }

        // Burst-compatible countbits without depending on Unity.Mathematics.math (no using needed).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int math_countbits(ulong value)
        {
            value = value - ((value >> 1) & 0x5555555555555555UL);
            value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
            value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((value * 0x0101010101010101UL) >> 56);
        }
    }
}
