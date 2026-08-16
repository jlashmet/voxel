namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Stable batching geometry for Storage read views.
    ///
    /// A read block is API vocabulary, not a physical allocation promise. Implementations may
    /// store, compress, page, or pool voxel data however they choose as long as they can expose
    /// the same logical 8^3 read batches efficiently.
    /// </summary>
    public static class VoxelReadGrid
    {
        public const int BlockEdgeLog2 = 3;
        public const int BlockEdge = 1 << BlockEdgeLog2;
        public const int BlockEdgeMask = BlockEdge - 1;
        public const int VoxelsPerBlock = BlockEdge * BlockEdge * BlockEdge;
        public const int OccupancyWordsPerBlock = VoxelsPerBlock / 64;

        public const int BlocksPerRegionEdgeLog2 = VoxelGrid.RegionVoxelEdgeLog2 - BlockEdgeLog2;
        public const int BlocksPerRegionEdge = 1 << BlocksPerRegionEdgeLog2;
        public const int BlocksPerRegionEdgeMask = BlocksPerRegionEdge - 1;
        public const int BlocksPerRegion = BlocksPerRegionEdge * BlocksPerRegionEdge * BlocksPerRegionEdge;
        /// <summary>64-bit words required for one bit of metadata per logical read block.</summary>
        public const int BlockSummaryWordCount = BlocksPerRegion / 64;

        /// <summary>
        /// Mip level whose cells span <paramref name="sourceStep"/> voxels, or -1 when exact
        /// voxel sampling is required. Level zero is a conservative any-solid 8^3 block summary,
        /// so an 8-voxel render stride must stay on exact samples; otherwise thin structures
        /// expand to whole coarse cells and architectural openings disappear.
        /// </summary>
        public static int LevelForStride(int sourceStep)
        {
            if (sourceStep <= BlockEdge) return -1;
            int level = 0;
            for (int span = BlockEdge; span < sourceStep; span <<= 1) level++;
            return level;
        }

        /// <summary>Voxels spanned by one cell at a read-view mip level.</summary>
        public static int VoxelsPerCell(int level) => BlockEdge << level;
    }
}
