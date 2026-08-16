using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Region-granularity acquisition boundary for authoritative voxel reads.
    ///
    /// Implementations perform the sparse world lookup once. Hot inner loops then operate on
    /// the returned <see cref="RegionReadView"/> directly, with no per-voxel virtual dispatch.
    /// Views borrow Storage-owned native memory and must never dispose it.
    /// </summary>
    public interface IRegionReadSource
    {
        /// <summary>Current world-content version. A view should be reacquired when this changes.</summary>
        ulong Version { get; }

        bool IsRegionResident(int3 regionCoord);

        /// <summary>Caller owns and disposes the returned coordinate array.</summary>
        NativeArray<int3> GetResidentRegionCoords(Allocator allocator);

        /// <summary>
        /// Acquires a borrowed read view for a currently resident region. The returned view is
        /// valid only while that region remains resident and until the next mutation/publish
        /// boundary represented by <see cref="Version"/>.
        /// </summary>
        bool TryAcquireRegion(int3 regionCoord, out RegionReadView view);

        /// <summary>
        /// Copies compact per-block occupancy state into caller-owned memory. Unlike a borrowed
        /// <see cref="RegionReadView"/>, the copied words are immutable from Storage's point of
        /// view and may safely outlive the frame or be consumed by jobs after later world edits.
        /// Each destination requires at least <see cref="VoxelReadGrid.BlockSummaryWordCount"/>
        /// words. <paramref name="version"/> identifies the world state captured by the copy.
        /// </summary>
        bool TryCopyBlockSummary(int3 regionCoord,
                                 NativeArray<ulong> occupiedWords,
                                 NativeArray<ulong> fullySolidWords,
                                 out ulong version);

        /// <summary>
        /// Acquires the resident region containing a world-space logical read block. This keeps
        /// region/block partitioning inside Storage rather than duplicating layout math in every
        /// collision or rendering consumer.
        /// </summary>
        bool TryAcquireRegionContainingBlock(int3 worldBlockCoord, out RegionReadView view);
    }
}
