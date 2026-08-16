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
        /// Copies a bounded slice of the resident-region table into caller-owned memory.
        /// <paramref name="cursor"/> is an opaque scan cursor owned by the consumer. At most
        /// destination.Length internal region slots are inspected per call, so sparse/free slots
        /// cannot turn recovery into an unbounded scan. Returns true when the current table scan
        /// has reached its end; later residency changes are delivered through the change feed.
        /// </summary>
        bool CopyResidentRegionCoords(ref int cursor, NativeArray<int3> destination,
                                      out int count);

        /// <summary>
        /// Acquires a borrowed read view for a currently resident region. The returned view is
        /// valid only while that region remains resident and until the next mutation/publish
        /// boundary represented by <see cref="Version"/>.
        /// </summary>
        bool TryAcquireRegion(int3 regionCoord, out RegionReadView view);

        /// <summary>
        /// Acquires one stable logical read block. Mixed payloads are pinned copy-on-write
        /// versions that may safely outlive later world edits/region eviction and be read by jobs.
        /// Empty and uniform blocks carry no pin. A valid pin must be released after the final
        /// dependent job; the backing arrays are Storage-owned and must never be disposed/written.
        /// </summary>
        bool TryPinWorldBlock(int3 worldBlockCoord, out PinnedVoxelReadBlock block);

        /// <summary>Releases a mixed-block pin previously returned by this source.</summary>
        void ReleasePinnedWorldBlock(in VoxelReadPinToken token);

        /// <summary>
        /// Pins the physical lifetime of one resident region's compact block-reference array for
        /// an optimistic job read. The job output is valid only if the token revision still passes
        /// <see cref="IsPinnedRegionCurrent"/> afterward. The backing array is Storage-owned.
        /// </summary>
        bool TryPinRegionBlockRefs(int3 regionCoord, out PinnedRegionBlockRefs region);

        /// <summary>Checks generation, logical residency and content revision for a pinned region.</summary>
        bool IsPinnedRegionCurrent(in VoxelRegionPinToken token);

        /// <summary>Releases a region metadata pin and completes deferred physical eviction if needed.</summary>
        void ReleasePinnedRegion(in VoxelRegionPinToken token);

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
