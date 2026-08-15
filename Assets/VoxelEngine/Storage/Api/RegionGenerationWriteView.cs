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
        private NativeArray<int> _blockStates;

        public int3 RegionCoord { get; }
        public bool IsCreated => _blockStates.IsCreated;

        /// <summary>
        /// Provider construction boundary. The backing array remains owned by Storage and is never
        /// exposed to consumers of this view; callers use only the logical block-writing methods.
        /// </summary>
        public RegionGenerationWriteView(int3 regionCoord, NativeArray<int> blockStates)
        {
            RegionCoord = regionCoord;
            _blockStates = blockStates;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniformBlock(int x, int y, int z, byte material)
        {
            int edge = VoxelReadGrid.BlocksPerRegionEdge;
            if ((uint)x >= edge || (uint)y >= edge || (uint)z >= edge)
                return;

            int index = x | (y << VoxelReadGrid.BlocksPerRegionEdgeLog2)
                          | (z << (VoxelReadGrid.BlocksPerRegionEdgeLog2 * 2));
            _blockStates[index] = material == VoxelGrid.MaterialEmpty
                ? -1
                : -material - 1;
        }
    }
}
