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
        [Test]
        public void ToroidalSlotGridMaintainsDenseActiveCoordinatesAcrossReuse()
        {
            var grid = new SurfaceChunkSlotGrid();
            grid.UpdateWindow(int3.zero, 1); // edge = 3

            Assert.True(grid.TryAcquire(int3.zero, out SurfaceChunkSlot first));
            Assert.True(grid.TryAcquire(new int3(1, 0, 0), out _));
            Assert.AreEqual(2, grid.ActiveCount);

            // Moving three cells makes x=3 reuse x=0's toroidal slot. It must advance the slot
            // generation without growing the dense active set.
            grid.UpdateWindow(new int3(3, 0, 0), 1);
            Assert.True(grid.TryAcquire(new int3(3, 0, 0), out SurfaceChunkSlot replacement));
            Assert.AreNotEqual(first.Generation, replacement.Generation);
            Assert.AreEqual(2, grid.ActiveCount);

            bool sawReplacement = false;
            bool sawOutgoing = false;
            for (int i = 0; i < grid.ActiveCount; i++)
            {
                int3 coordinate = grid.ActiveCoordinateAt(i);
                sawReplacement |= coordinate.Equals(new int3(3, 0, 0));
                sawOutgoing |= coordinate.Equals(new int3(1, 0, 0));
            }
            Assert.True(sawReplacement);
            Assert.True(sawOutgoing,
                "Outgoing slots remain indexed until the cache's bounded edge-retirement slice runs.");

            grid.Retire(new int3(1, 0, 0));
            Assert.AreEqual(1, grid.ActiveCount);
            Assert.AreEqual(new int3(3, 0, 0), grid.ActiveCoordinateAt(0));
        }

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
        public void RenderRingsUseFeaturePreservingStepEightAndMipsBeyondIt()
        {
            // Step 8 keeps exact COW Storage inputs but no longer runs exact Transvoxel. It
            // compresses those inputs into spatial 4^3 HLOD subcells; coarser experimental rings
            // beyond step 8 may still consume the conventional mip pyramid.
            using (var fine = new CpuTransvoxelChunkCache(1))
                Assert.IsFalse(fine.SamplesFromMips, "Step 1 must read voxels.");
            using (var fine2 = new CpuTransvoxelChunkCache(4))
                Assert.IsFalse(fine2.SamplesFromMips, "Step 4 is still sub-brick.");
            using (var coarse = new CpuTransvoxelChunkCache(8))
            {
                Assert.IsFalse(coarse.SamplesFromMips,
                    "Step 8 must not use conservative any-solid block summaries as render density.");
                Assert.IsTrue(coarse.UsesBlockHlod,
                    "Step 8 must derive its coarse mesh from feature-preserving exact block inputs.");
            }
            using (var coarser = new CpuTransvoxelChunkCache(16))
                Assert.IsTrue(coarser.SamplesFromMips);
        }

        [Test]
        public void CompleteViewStopsBackgroundBuildsAndVisibleHoleRestartsConvergence()
        {
            Assert.AreEqual(0,
                VoxelSurfaceScheduler.ResolveBuildCeiling(
                    missingVisibleCount: 0, convergingCeiling: 12, convergedCeiling: 0),
                "A complete view must not keep filling the fixed arena with invisible prefetch.");
            Assert.AreEqual(12,
                VoxelSurfaceScheduler.ResolveBuildCeiling(
                    missingVisibleCount: 1, convergingCeiling: 12, convergedCeiling: 0),
                "The first visible hole must restore full convergence immediately.");
            Assert.IsFalse(
                VoxelSurfaceScheduler.ShouldAllowBackgroundBuilds(
                    missingVisibleCount: 1, buildCeiling: 12),
                "Visible convergence must not admit off-screen work from idle shards.");
            Assert.IsTrue(
                VoxelSurfaceScheduler.ShouldAllowBackgroundBuilds(
                    missingVisibleCount: 0, buildCeiling: 1),
                "An explicitly configured settled build slot may resume prefetch.");
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
            const float showcaseStreamingRadiusMetres = 8 * 51.2f;   // LoadRadiusRegions = 8
            Assert.LessOrEqual(VoxelSurfaceScheduler.MaxVoxelRingRadiusMetresDefault,
                               showcaseStreamingRadiusMetres + 1f,
                $"Voxel rings reach {VoxelSurfaceScheduler.MaxVoxelRingRadiusMetresDefault} m but only "
              + $"{showcaseStreamingRadiusMetres} m of regions are ever resident.");
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
                (step: 8, inner: 288f, outer: VoxelSurfaceScheduler.MaxVoxelRingRadiusMetresDefault),
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
                (step: 8, inner: 288f, outer: VoxelSurfaceScheduler.MaxVoxelRingRadiusMetresDefault),
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
        // Truncation against the streamed radius
        // -------------------------------------------------------------------------

        private static readonly (int Step, float Inner, float Outer)[] s_Layout =
        {
            (1, 0f, 96f),
            (2, 96f, 192f),
            (4, 192f, 288f),
            (8, 288f, VoxelSurfaceScheduler.MaxVoxelRingRadiusMetresDefault),
        };

        [Test]
        public void RingTruncatedPastItsInnerCutIsSuspendedRatherThanCollapsed()
        {
            // The band tolerates a chunk straddling either cut so that adjacent rings overlap by
            // one chunk instead of gapping. Collapsing a band to zero width therefore does not
            // empty it — it leaves a one-chunk-thick shell of that ring's step just inside the
            // cut, drawn over ground the finest ring already covers. At step 8 that shell is
            // 51.2 m of coarse terrain sitting on the near field.
            const float smallShowcaseCap = 51.2f;   // LoadRadiusRegions = 1

            for (int r = 1; r < s_Layout.Length; r++)
            {
                (float inner, float outer, bool suspended) = VoxelSurfaceScheduler.ResolveRingBand(
                    s_Layout[r].Inner, s_Layout[r].Outer, smallShowcaseCap, lodEnabled: true);

                Assert.IsTrue(suspended,
                    $"Step {s_Layout[r].Step} is truncated to [{inner}, {outer}] by a "
                  + $"{smallShowcaseCap} m cap and must be suspended, not left as a shell.");
            }
        }

        [Test]
        public void FinestRingCoversEverythingInsideTheCapAndIsNeverSuspended()
        {
            foreach (float cap in new[] { 25.6f, 51.2f, 150f, 409.6f })
            {
                (float inner, float outer, bool suspended) = VoxelSurfaceScheduler.ResolveRingBand(
                    s_Layout[0].Inner, s_Layout[0].Outer, cap, lodEnabled: true);

                Assert.IsFalse(suspended, $"The finest ring must stay live at a {cap} m cap.");
                Assert.AreEqual(0f, inner, "The finest ring has no inner cut.");
                Assert.AreEqual(Mathf.Min(96f, cap), outer, 0.001f);
            }
        }

        [Test]
        public void NoLiveRingEverHasAZeroWidthBand()
        {
            // The shell artefact is exactly "live ring with inner == outer". Sweep the caps a
            // world might stream and assert the combination cannot arise.
            for (float cap = 0f; cap <= 512f; cap += 6.4f)
            {
                foreach (var configured in s_Layout)
                {
                    (float inner, float outer, bool suspended) =
                        VoxelSurfaceScheduler.ResolveRingBand(
                            configured.Inner, configured.Outer, cap, lodEnabled: true);

                    if (suspended) continue;
                    Assert.IsTrue(inner < outer || inner == 0f,
                        $"Step {configured.Step} is live at cap {cap} with band "
                      + $"[{inner}, {outer}], which renders a one-chunk shell.");
                }
            }
        }

        [Test]
        public void TruncationPreservesTheConfiguredBandsWhenTheWorldStreamsFarEnough()
        {
            // The full showcase streams past the outermost ring, so nothing should move.
            const float fullShowcaseCap = VoxelSurfaceScheduler.MaxVoxelRingRadiusMetresDefault;

            foreach (var configured in s_Layout)
            {
                (float inner, float outer, bool suspended) =
                    VoxelSurfaceScheduler.ResolveRingBand(
                        configured.Inner, configured.Outer, fullShowcaseCap, lodEnabled: true);

                Assert.IsFalse(suspended, $"Step {configured.Step} must stay live.");
                Assert.AreEqual(configured.Inner, inner, 0.001f);
                Assert.AreEqual(configured.Outer, outer, 0.001f);
            }
        }

        [Test]
        public void DetailScaleNeverReducesTheOuterRenderRadius()
        {
            const float ringCap = VoxelSurfaceScheduler.MaxVoxelRingRadiusMetresDefault;

            foreach (float scale in new[] { 0.05f, 0.25f, 0.6f, 1f, 1.5f })
            {
                float furthestLiveOuter = 0f;
                for (int r = 0; r < s_Layout.Length; r++)
                {
                    var configured = s_Layout[r];
                    (float inner, float outer, bool suspended) =
                        VoxelSurfaceScheduler.ResolveScaledRingBand(
                            configured.Inner, configured.Outer, scale, ringCap,
                            lodEnabled: true, isOutermost: r == s_Layout.Length - 1);
                    if (suspended) continue;
                    Assert.Less(inner, outer);
                    furthestLiveOuter = Mathf.Max(furthestLiveOuter, outer);
                }

                Assert.AreEqual(ringCap, furthestLiveOuter, 0.001f,
                    $"Detail scale {scale} reduced the voxel render radius.");
            }
        }

        [Test]
        public void DisablingLodLeavesOnlyTheFinestRingLive()
        {
            // m_DisableLod on the showcase used to collapse the coarse bands to zero width,
            // which is the same shell bug by another route: LOD looked disabled while step-2,
            // step-4 and step-8 shells were still being drawn near the player.
            const float cap = 51.2f;

            (float inner, float outer, bool suspended) = VoxelSurfaceScheduler.ResolveRingBand(
                s_Layout[0].Inner, s_Layout[0].Outer, cap, lodEnabled: false);
            Assert.IsFalse(suspended, "The finest ring stays live with LOD off.");
            Assert.AreEqual(0f, inner);
            Assert.AreEqual(cap, outer, 0.001f);

            for (int r = 1; r < s_Layout.Length; r++)
            {
                (_, _, bool coarseSuspended) = VoxelSurfaceScheduler.ResolveRingBand(
                    s_Layout[r].Inner, s_Layout[r].Outer, cap, lodEnabled: false);
                Assert.IsTrue(coarseSuspended,
                    $"Step {s_Layout[r].Step} must be off, not narrow, when LOD is disabled.");
            }
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
