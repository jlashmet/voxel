using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Focused world-space voxel/surface query for placement and world-generation clients.
    /// Physical region/brick/pool representation remains owned by Storage.Runtime.
    /// </summary>
    public interface IVoxelSurfaceQuery
    {
        bool TryRead(int3 worldVoxel, out VoxelCell cell);

        /// <summary>Finds the highest non-empty voxel in the inclusive Y range.</summary>
        bool TryFindTopSolid(int x, int z, int minY, int maxY,
                             out int y, out VoxelCell cell);

        /// <summary>
        /// Finds the highest non-empty voxel while ignoring two caller-supplied material IDs.
        /// Storage owns the scan; the caller owns domain-specific material classification.
        /// </summary>
        bool TryFindTopSolidExcluding(int x, int z, int minY, int maxY,
                                      byte excludedMaterialA, byte excludedMaterialB,
                                      out int y, out VoxelCell cell);
    }
}
