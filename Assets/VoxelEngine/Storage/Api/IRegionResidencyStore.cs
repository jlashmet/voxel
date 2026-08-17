using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Storage-owned mechanics for region residency and mixed-voxel memory pressure.
    ///
    /// Streaming decides which regions should exist and when to evict them. Storage performs
    /// those mutations and owns the allocator details required to release region memory.
    /// </summary>
    public interface IRegionResidencyStore
    {
        StoragePressure Pressure { get; }

        bool IsRegionResident(int3 regionCoord);

        /// <summary>
        /// Returns an allocator-owned snapshot of the regions that are actually resident now.
        /// Streaming uses this authoritative set for distance eviction; synthesising candidate
        /// coordinates around the current player cannot find regions left behind after traversal.
        /// </summary>
        NativeArray<int3> GetResidentRegionCoords(Allocator allocator);

        /// <summary>Makes the requested region resident. Idempotent.</summary>
        void EnsureRegionResident(int3 regionCoord);

        /// <summary>Evicts a resident region and releases its Storage-owned memory.</summary>
        bool EvictRegion(int3 regionCoord);
    }
}