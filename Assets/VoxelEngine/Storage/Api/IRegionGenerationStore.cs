using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Acquires Storage-owned bulk write views for deterministic region generation.
    /// One acquisition precedes the hot block-fill loop; consumers never receive Region/BrickRef.
    /// </summary>
    public interface IRegionGenerationStore
    {
        RegionGenerationWriteView AcquireRegion(int3 regionCoord);
    }
}
