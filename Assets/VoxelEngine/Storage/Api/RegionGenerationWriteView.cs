using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Borrowed bulk-generation writer for one resident region.
    ///
    /// The current contract deliberately supports only empty/uniform logical read blocks. That is
    /// exactly what Terrain generation emits today and keeps physical pool allocation out of the
    /// Terrain boundary. Mixed voxel mutation belongs to the Edits/write API, not this fast path.
    /// </summary>
    public struct RegionGenerationWriteView
    {
        private NativeArray<int> _encodedBlockRefs;
        private NativeArray<ulong> _occupiedBlockWords;
        private NativeArray<ulong> _fullySolidBlockWords;

        public int3 RegionCoord { get; }
        public bool IsCreated => _encodedBlockRefs.IsCreated;

        internal RegionGenerationWriteView(int3 regionCoord,
                                           NativeArray<int> encodedBlockRefs,
                                           NativeArray<ulong> occupiedBlockWords,
                                           NativeArray<ulong> fullySolidBlockWords)
        {
            RegionCoord = regionCoord;
            _encodedBlockRefs = encodedBlockRefs;
            _occupiedBlockWords = occupiedBlockWords;
            _fullySolidBlockWords = fullySolidBlockWords;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniformBlock(int x, int y, int z, byte material)
        {
            int edge = VoxelReadGrid.BlocksPerRegionEdge;
            if ((uint)x >= edge || (uint)y >= edge || (uint)z >= edge)
                return;

            int index = x | (y << VoxelReadGrid.BlocksPerRegionEdgeLog2)
                          | (z << (VoxelReadGrid.BlocksPerRegionEdgeLog2 * 2));
            _encodedBlockRefs[index] = material == VoxelGrid.MaterialEmpty
                ? -1
                : -material - 1;

            bool solid = material != VoxelGrid.MaterialEmpty;
            SetSummaryBit(_occupiedBlockWords, index, solid);
            SetSummaryBit(_fullySolidBlockWords, index, solid);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetSummaryBit(NativeArray<ulong> words, int index, bool value)
        {
            if (!words.IsCreated) return;
            int wordIndex = index >> 6;
            ulong mask = 1UL << (index & 63);
            ulong word = words[wordIndex];
            words[wordIndex] = value ? word | mask : word & ~mask;
        }
    }
}
