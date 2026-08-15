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
        /// Finds the highest terrain/structure surface suitable for land placement, excluding
        /// fluid/cascade presentation materials according to Storage's canonical material rules.
        /// </summary>
        bool TryFindTopLandSurface(int x, int z, int minY, int maxY,
                                   out int y, out VoxelCell cell);
    }
}
