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

        /// <summary>Makes the requested region resident. Idempotent.</summary>
        void EnsureRegionResident(int3 regionCoord);

        /// <summary>Evicts a resident region and releases its Storage-owned memory.</summary>
        bool EvictRegion(int3 regionCoord);

        /// <summary>
        /// Advances an opaque resident-slot cursor to the next currently resident coordinate.
        /// Returns false when the current pass reaches the end; callers then reset the cursor to
        /// zero before starting a later bounded pass. No allocation is required.
        /// </summary>
        bool TryGetNextResidentCoord(ref int cursor, out int3 regionCoord);
    }
}
