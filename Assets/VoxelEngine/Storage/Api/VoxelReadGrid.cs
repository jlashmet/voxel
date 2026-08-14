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
    }
}
