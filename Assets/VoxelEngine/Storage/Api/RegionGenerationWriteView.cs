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
    public readonly struct RegionGenerationWriteView
    {
        private readonly NativeArray<int> _encodedBlockRefs;

        public int3 RegionCoord { get; }
        public bool IsCreated => _encodedBlockRefs.IsCreated;

        internal RegionGenerationWriteView(int3 regionCoord, NativeArray<int> encodedBlockRefs)
        {
            RegionCoord = regionCoord;
            _encodedBlockRefs = encodedBlockRefs;
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
        }
    }
}
