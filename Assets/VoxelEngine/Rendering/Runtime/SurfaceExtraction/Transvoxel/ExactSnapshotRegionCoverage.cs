namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel
{
    /// <summary>
    /// Tracks exact COW metadata coverage for one padded brick cache.
    ///
    /// The owned chunk core is authoritative: if a region intersecting that core cannot be pinned,
    /// the snapshot is unavailable and must retry. The one-brick extraction halo is different.
    /// A halo may cross into a region that is intentionally not resident (for example the
    /// showcase's y=-1 underground layer), so a missing halo pin remains cleared/empty for this
    /// optimistic build and can be refreshed when that neighbour later becomes resident.
    ///
    /// This distinction prevents both failure modes seen during the showcase repair: silently
    /// publishing a missing core as empty, and permanently retrying because an optional halo is
    /// outside the world's residency surface.
    /// </summary>
    internal struct ExactSnapshotRegionCoverage
    {
        public int RequiredRegions { get; private set; }
        public int PinnedRegions { get; private set; }
        public int OptionalRegions { get; private set; }
        public int PinnedOptionalRegions { get; private set; }

        public bool IsComplete => RequiredRegions == PinnedRegions;

        public void Reset()
        {
            RequiredRegions = 0;
            PinnedRegions = 0;
            OptionalRegions = 0;
            PinnedOptionalRegions = 0;
        }

        public void RecordRegion(bool required, bool pinned)
        {
            if (required)
            {
                RequiredRegions++;
                if (pinned) PinnedRegions++;
                return;
            }

            OptionalRegions++;
            if (pinned) PinnedOptionalRegions++;
        }

        // Kept while callers migrate to the explicit core/halo contract.
        public void RecordRequiredRegion(bool pinned) => RecordRegion(required: true, pinned);
    }
}
