using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ExactSnapshotRegionCoverageTests
    {
        [Test]
        public void MissingRequiredRegionCannotBeTreatedAsAuthoritativeEmpty()
        {
            ExactSnapshotRegionCoverage coverage = default;
            coverage.Reset();
            coverage.RecordRequiredRegion(pinned: true);
            coverage.RecordRequiredRegion(pinned: false);
            coverage.RecordRequiredRegion(pinned: true);

            Assert.AreEqual(3, coverage.RequiredRegions);
            Assert.AreEqual(2, coverage.PinnedRegions);
            Assert.False(coverage.IsComplete,
                "A failed exact region pin must make the snapshot unavailable, not empty.");
        }

        [Test]
        public void CompleteRequiredRegionSetMayProceedToExactClassification()
        {
            ExactSnapshotRegionCoverage coverage = default;
            coverage.RecordRequiredRegion(pinned: true);
            coverage.RecordRequiredRegion(pinned: true);

            Assert.True(coverage.IsComplete);
        }
    }
}
