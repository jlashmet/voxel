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
        public void SurfaceDiscoveryDoesNotCreateHaloOnlyStep4ChunksWithNonResidentCore()
        {
            // Both corners are authoritative bricks owned by the same step-4 chunk. They exercise
            // both low and high chunk borders. Discovery must learn the owner only; extraction
            // halo dependency does not create authoritative ownership in any adjacent chunk.
            // In particular, admitting a low-Y neighbour creates step-4 chunk y=-1 even though
            // showcase residency deliberately has no y=-1 core region; exact snapshot admission
            // then retries its required core pin forever. Mutation invalidation may still touch
            // already-known neighbours because their existing geometry can depend on a border.
            using var cache = new CpuTransvoxelChunkCache(sourceStep: 4);
            int admitted = cache.DiscoverSurfaceBricks(new List<int3>
            {
                int3.zero,
                new int3(31, 31, 31),
            });

            Assert.AreEqual(1, admitted,
                "Initial discovery should admit only the chunk that owns authoritative surface bricks; halo-only neighbours must wait for their own core surface content.");
            Assert.AreEqual(1, cache.KnownCount,
                "Low/high boundary surface bricks must not create halo-only coarse chunks, including the nonresident negative-Y core that caused persistent pin rejection.");
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
