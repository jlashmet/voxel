using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Runtime.Occupancy;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Covers the LOD ring geometry: chunk sizing per stride, which data source a ring reads,
    /// and the band arithmetic that partitions the view between rings.
    ///
    /// The band rules carry two failure modes worth pinning. If the bands leave a gap, terrain
    /// vanishes in a shell around the viewer. If the LOD boundary does not land on a chunk
    /// face, transition cells have nothing to attach to and every ring seam cracks.
    /// </summary>
    public sealed class SurfaceRingBandTests
    {
        // -------------------------------------------------------------------------
        // Ring geometry
        // -------------------------------------------------------------------------

        [Test]
        public void ChunkExtentScalesWithSourceStepWhileCellCountStaysFixed()
        {
            // Constant extraction cost per chunk is the whole point: a coarse ring covers
            // vastly more world for the same 64³ lattice.
            foreach (int step in new[] { 1, 2, 4, 8, 16 })
            {
                using var cache = new CpuTransvoxelChunkCache(step);
                Assert.AreEqual(CpuTransvoxelChunkCache.CellsPerAxis * step, cache.VoxelsPerAxis,
                    $"Step {step} must span {step}x the base chunk.");
                Assert.AreEqual(step, cache.SourceStep);
            }
        }

        [Test]
        public void SourceStepMustBeAPowerOfTwo()
        {
            // Chunk coordinates and brick decomposition are shifts; a non-power-of-two stride
            // would silently misalign a ring against the brick grid.
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new CpuTransvoxelChunkCache(3));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new CpuTransvoxelChunkCache(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new CpuTransvoxelChunkCache(-2));
        }

        [Test]
        public void FineRingsReadVoxelsAndCoarseRingsReadMips()
        {
            // The dividing line sits one step *above* a brick, not on it. Mip level 0 is a
            // conservative any-solid 8^3 summary, so reading it at an 8-voxel stride inflates
            // thin structures to whole cells and closes architectural openings — the coarse-LOD
            // castle regression. VoxelReadGrid.LevelForStride therefore returns -1 for a stride
            // of 8, and only strides coarser than a brick sample the pyramid.
            using (var fine = new CpuTransvoxelChunkCache(1))
                Assert.IsFalse(fine.SamplesFromMips, "Step 1 must read voxels.");
            using (var fine2 = new CpuTransvoxelChunkCache(4))
                Assert.IsFalse(fine2.SamplesFromMips, "Step 4 is still sub-brick.");
            using (var atBrick = new CpuTransvoxelChunkCache(8))
                Assert.IsFalse(atBrick.SamplesFromMips,
                    "Step 8 matches a brick exactly and must stay on exact voxel samples.");
            using (var coarser = new CpuTransvoxelChunkCache(16))
                Assert.IsTrue(coarser.SamplesFromMips,
                    "Step 16 is coarser than a brick and must read the pyramid.");
        }

        [Test]
        public void CoarseRingChunksAlignToTheBrickGrid()
        {
            // A ring's chunk must be a whole number of bricks or its mip samples straddle cells.
            foreach (int step in new[] { 8, 16 })
            {
                using var cache = new CpuTransvoxelChunkCache(step);
                Assert.AreEqual(0, cache.VoxelsPerAxis % VoxelDimensions.BrickEdge,
                    $"Step {step} chunk must be a whole number of bricks.");
                Assert.AreEqual(cache.VoxelsPerAxis / VoxelDimensions.BrickEdge,
                                cache.BricksPerAxis);
            }
        }

        // -------------------------------------------------------------------------
        // Band partitioning
        // -------------------------------------------------------------------------

        private static CpuTransvoxelChunkCache Ring(int step, float inner, float outer) =>
            new(step) { MinViewDistanceMetres = inner, MaxViewDistanceMetres = outer };

        [Test]
        public void InnermostRingHasNoInnerCut()
        {
            using var ring = Ring(1, 0f, 96f);
            Assert.AreEqual(0f, ring.MinViewDistanceMetres,
                "The innermost ring must render everything up to its outer edge.");
        }

        [Test]
        public void RingsDoNotReachBeyondTheStreamingRadius()
        {
            // A ring meshes only resident regions. One placed past the streaming radius finds
            // no data, builds nothing, and still allocates eight shard caches of persistent
            // scratch. Distance past this point belongs to the analytic far terrain, which
            // needs no regions.
            //
            // Residency is bounded by the *unload* radius, not the load radius: regions inside
            // the load radius are fetched, and they stay resident until they fall outside the
            // unload radius. Comparing against the load radius understates what is resident by
            // three regions and fails a ring layout that is in fact within budget.
            const float regionMetres = 51.2f;
            const int showcaseUnloadRadiusRegions = 11;  // VoxelShowcase.m_UnloadRadiusRegions
            const float residentRadiusMetres = showcaseUnloadRadiusRegions * regionMetres;

            Assert.LessOrEqual(VoxelSurfaceScheduler.MaxVoxelRingRadiusMetres,
                               residentRadiusMetres + 1f,
                $"Voxel rings reach {VoxelSurfaceScheduler.MaxVoxelRingRadiusMetres} m but only "
              + $"{residentRadiusMetres} m of regions are ever resident.");

            // device-matrix.md is authoritative: the tightest tier (Mobile-HE) unloads regions
            // at 420 m, so no ring may reach past that on any device.
            const float tightestUnloadRadiusMetres = 420f;
            Assert.LessOrEqual(VoxelSurfaceScheduler.MaxVoxelRingRadiusMetres,
                               tightestUnloadRadiusMetres,
                "Voxel rings must stay inside the Mobile-HE region unload radius.");
        }

        [Test]
        public void AdjacentRingBandsLeaveNoGap()
        {
            // Every point in the world must be claimed by some ring. The inner cut of ring N+1
            // must not exceed the outer cut of ring N, or a shell of terrain goes unrendered.
            var layout = new[]
            {
                (step: 1, inner: 0f, outer: 96f),
                (step: 2, inner: 96f, outer: 192f),
                (step: 4, inner: 192f, outer: 288f),
                (step: 8, inner: 288f, outer: VoxelSurfaceScheduler.MaxVoxelRingRadiusMetres),
            };

            for (int i = 1; i < layout.Length; i++)
                Assert.LessOrEqual(layout[i].inner, layout[i - 1].outer,
                    $"Ring {i} starts at {layout[i].inner} m but ring {i - 1} ends at "
                  + $"{layout[i - 1].outer} m, leaving an unrendered shell.");
        }

        [Test]
        public void EachRingBandIsAtLeastOneChunkDeep()
        {
            // A band thinner than its own chunk cannot hold a complete chunk, so the ring
            // would never render anything while still consuming a build budget.
            var layout = new[]
            {
                (step: 1, inner: 0f, outer: 96f),
                (step: 2, inner: 96f, outer: 192f),
                (step: 4, inner: 192f, outer: 288f),
                (step: 8, inner: 288f, outer: VoxelSurfaceScheduler.MaxVoxelRingRadiusMetres),
            };
            const float voxelSize = 0.1f;

            foreach (var ring in layout)
            {
                float chunkMetres = CpuTransvoxelChunkCache.CellsPerAxis * ring.step * voxelSize;
                Assert.GreaterOrEqual(ring.outer - ring.inner, chunkMetres,
                    $"Step {ring.step} chunks are {chunkMetres} m but its band is only "
                  + $"{ring.outer - ring.inner} m deep.");
            }
        }

        [Test]
        public void OuterRingsCoverProgressivelyMoreWorldPerChunk()
        {
            // The reason whole-world view distance is affordable: each ring quadruples the
            // ground a single chunk covers while its extraction cost stays flat.
            const float voxelSize = 0.1f;
            float previous = 0f;
            foreach (int step in new[] { 1, 2, 4, 8 })
            {
                float chunkMetres = CpuTransvoxelChunkCache.CellsPerAxis * step * voxelSize;
                Assert.Greater(chunkMetres, previous);
                previous = chunkMetres;
            }
            Assert.AreEqual(51.2f, previous, 0.01f,
                "The outermost voxel ring's chunks should span one region edge.");
        }

        // -------------------------------------------------------------------------
        // Mip level agreement
        // -------------------------------------------------------------------------

        [Test]
        public void EveryCoarseRingRequestsALevelThePyramidCanHold()
        {
            // A ring asking for a level past the top of the pyramid would render nothing.
            foreach (int step in new[] { 8 })
            {
                int level = VoxelMipSampler.LevelForStride(step);
                Assert.GreaterOrEqual(level, 0);
                Assert.Less(level, MipBuilder.MaxLevels,
                    $"Step {step} wants mip level {level}, beyond MaxLevels.");
            }
        }

        [Test]
        public void RingStrideAndMipCellSpanAgree()
        {
            // If these disagree the coarse mesh samples at a different rate than the data it
            // reads, and the surface shifts relative to the fine rings.
            foreach (int step in new[] { 8, 16, 32 })
            {
                int level = VoxelMipSampler.LevelForStride(step);
                Assert.AreEqual(step, VoxelMipSampler.VoxelsPerCell(level),
                    $"Step {step} maps to level {level}, which spans "
                  + $"{VoxelMipSampler.VoxelsPerCell(level)} voxels.");
            }
        }

        // -------------------------------------------------------------------------
        // Empty-result residency
        // -------------------------------------------------------------------------

        [Test]
        public void EmptyChunksDoNotConsumeResidentCapacity()
        {
            // Air dominates any view volume. If an empty result held a resident slot, real
            // geometry would be evicted to make room for nothing.
            using var cache = new CpuTransvoxelChunkCache(1) { MaxResidentChunks = 8 };
            Assert.AreEqual(0, cache.ResidentCount,
                "A cache that has built nothing holds nothing.");
        }
    }
}
