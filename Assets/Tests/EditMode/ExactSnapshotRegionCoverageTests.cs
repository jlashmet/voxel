using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
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
        public void Step4GroundChunkRequiresYZeroRegionButNotNegativeHaloRegion()
        {
            // Step 4 owns 64 cells * 4 voxels = 256 voxels = 32 eight-voxel blocks.
            // Its one-block extraction halo starts at block y=-1, which belongs to region y=-1,
            // but the owned core remains blocks [0,32) in region y=0. Showcase surface residency
            // deliberately starts at region layer zero, so treating y=-1 as required would retry
            // every ground-layer coarse chunk forever.
            const int blocksPerRegionEdge = 64;
            const int step4CoreBlocks = 32;
            int3 coreMin = int3.zero;

            Assert.True(ExactSnapshotRegionCoverage.RegionIntersectsCore(
                new int3(0, 0, 0), blocksPerRegionEdge, coreMin, step4CoreBlocks));
            Assert.False(ExactSnapshotRegionCoverage.RegionIntersectsCore(
                new int3(0, -1, 0), blocksPerRegionEdge, coreMin, step4CoreBlocks));
            Assert.False(ExactSnapshotRegionCoverage.RegionIntersectsCore(
                new int3(-1, 0, 0), blocksPerRegionEdge, coreMin, step4CoreBlocks));
        }

        [Test]
        public void SchedulerSurfaceDiscoveryCanonicalizationAdmitsOnlyOwningStep4Chunk()
        {
            // Both authoritative bricks belong to step-4 chunk (0,0,0) but sit on opposite
            // chunk borders. The scheduler must preserve that ownership while moving the solid
            // discovery feed off the border before it reaches the cache's generic halo-aware
            // admission path. Water and mutation invalidation continue to receive the original
            // coordinates.
            using var cache = new CpuTransvoxelChunkCache(sourceStep: 4);
            var canonical = new List<int3>
            {
                SurfaceDiscoveryChunkOwner.Canonicalize(int3.zero, cache.BricksPerAxis),
                SurfaceDiscoveryChunkOwner.Canonicalize(
                    new int3(31, 31, 31), cache.BricksPerAxis),
            };

            Assert.AreEqual(new int3(16, 16, 16), canonical[0]);
            Assert.AreEqual(new int3(16, 16, 16), canonical[1]);

            int admitted = cache.DiscoverSurfaceBricks(canonical);
            Assert.AreEqual(1, admitted,
                "Scheduler discovery should admit only the chunk that owns authoritative surface bricks; halo-only neighbours must wait for their own core content.");
            Assert.AreEqual(1, cache.KnownCount,
                "Boundary surface bricks must not create the nonresident negative-Y coarse core that caused persistent exact-metadata pin rejection.");
        }

        [Test]
        public void SchedulerSurfaceDiscoveryCanonicalizationPreservesNegativeChunkOwnership()
        {
            // Floor semantics matter west/below/north of the origin. A brick at -1 belongs to
            // chunk -1, not chunk 0; canonicalization must move it to that chunk's interior.
            int3 canonical = SurfaceDiscoveryChunkOwner.Canonicalize(
                new int3(-1, -1, -1), 32);
            Assert.AreEqual(new int3(-16, -16, -16), canonical);
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
