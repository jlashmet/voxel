using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ExactSnapshotRegionCoverageTests
    {
        [Test]
        public void MissingRequiredCoreRegionCannotBeTreatedAsAuthoritativeEmpty()
        {
            ExactSnapshotRegionCoverage coverage = default;
            coverage.Reset();
            coverage.RecordRegion(required: true, pinned: true);
            coverage.RecordRegion(required: true, pinned: false);
            coverage.RecordRegion(required: false, pinned: true);

            Assert.AreEqual(2, coverage.RequiredRegions);
            Assert.AreEqual(1, coverage.PinnedRegions);
            Assert.AreEqual(1, coverage.OptionalRegions);
            Assert.AreEqual(1, coverage.PinnedOptionalRegions);
            Assert.False(coverage.IsComplete,
                "A failed core-region pin must make the exact snapshot unavailable, not empty.");
        }

        [Test]
        public void MissingOptionalHaloRegionDoesNotBlockCoreClassification()
        {
            ExactSnapshotRegionCoverage coverage = default;
            coverage.RecordRegion(required: true, pinned: true);
            coverage.RecordRegion(required: false, pinned: false);
            coverage.RecordRegion(required: false, pinned: true);

            Assert.AreEqual(1, coverage.RequiredRegions);
            Assert.AreEqual(1, coverage.PinnedRegions);
            Assert.AreEqual(2, coverage.OptionalRegions);
            Assert.AreEqual(1, coverage.PinnedOptionalRegions);
            Assert.True(coverage.IsComplete,
                "A non-resident extraction halo must not permanently retry an otherwise coherent core snapshot.");
        }

        [Test]
        public void CompleteRequiredCoreRegionSetMayProceedToExactClassification()
        {
            ExactSnapshotRegionCoverage coverage = default;
            coverage.RecordRegion(required: true, pinned: true);
            coverage.RecordRegion(required: true, pinned: true);

            Assert.True(coverage.IsComplete);
        }
    }
}
