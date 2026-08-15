namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Stable logical voxel-grid constants that are safe for cross-system use.
    /// Physical brick and pool layout belongs to Storage.Runtime and must not leak through this API.
    /// </summary>
    public static class VoxelGrid
    {
        /// <summary>Voxels per region edge.</summary>
        public const int RegionVoxelEdgeLog2 = 9;
        public const int RegionVoxelEdge = 1 << RegionVoxelEdgeLog2;

        /// <summary>Material index reserved for empty space.</summary>
        public const byte MaterialEmpty = 0;
    }
}
