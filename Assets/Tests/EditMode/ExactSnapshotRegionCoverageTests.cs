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
            // World block (0,0,0) is the concrete showcase floor case. It belongs to step-4
            // chunk (0,0,0). The extractor needs neighbouring block samples as an optional halo,
            // but that does not give chunks (-1/0,-1/0,-1/0) authoritative owned content.
            // Admitting those neighbours during initial discovery creates chunk y=-1 even though
            // showcase residency deliberately has no y=-1 core region; exact snapshot admission
            // then retries its required core pin forever. Mutation invalidation may still touch
            // already-known neighbours because their existing geometry can depend on this border.
            using var cache = new CpuTransvoxelChunkCache(sourceStep: 4);
            int admitted = cache.DiscoverSurfaceBricks(new List<int3> { int3.zero });

            Assert.AreEqual(1, admitted,
                "Initial discovery should admit only the chunk that owns the authoritative surface brick; halo-only neighbours must wait for their own core surface content.");
            Assert.AreEqual(1, cache.KnownCount,
                "A boundary surface brick must not create seven halo-only coarse chunks, including the nonresident negative-Y core that caused persistent pin rejection.");
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
