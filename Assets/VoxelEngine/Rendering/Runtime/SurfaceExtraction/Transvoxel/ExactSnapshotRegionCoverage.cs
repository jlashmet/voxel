namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel
{
    /// <summary>
    /// Tracks whether an exact COW snapshot acquired every resident-region metadata slice that
    /// intersects its padded brick cache. A failed region pin means the snapshot is unavailable,
    /// not authoritatively empty; callers must retry rather than classify the cleared cache range.
    /// </summary>
    internal struct ExactSnapshotRegionCoverage
    {
        public int RequiredRegions { get; private set; }
        public int PinnedRegions { get; private set; }

        public bool IsComplete => RequiredRegions == PinnedRegions;

        public void Reset()
        {
            RequiredRegions = 0;
            PinnedRegions = 0;
        }

        public void RecordRequiredRegion(bool pinned)
        {
            RequiredRegions++;
            if (pinned) PinnedRegions++;
        }
    }
}
