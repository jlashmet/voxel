using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuSurfaceMirrorCoordinatorRecoveryTests
    {
        [Test]
        public void RuntimeSemanticEditRejectsStaleGenerationAndRecoversCurrentCoverage()
        {
            Assume.That(SystemInfo.supportsComputeShaders, Is.True,
                "Persistent GPU mirror coverage requires compute shader support.");

            var table = new RegionTable(expectedResident: 2, Allocator.Persistent);
            var pool = new BrickPool(capacity: 8, Allocator.Persistent);
            bool acquired = false;
            bool coverageRequested = false;

            int3 block = int3.zero;
            int3 coreMin = int3.zero;
            int3 coreMax = new int3(VoxelReadGrid.BlockEdge);

            try
            {
                // Make the authoritative region resident before capability objects borrow the
                // storage handles, then author a mixed brick carrying every geometry semantic.
                table.LoadRegion(int3.zero);
                var changes = new VoxelChangeJournal();
                var mutations = new RegionMutationStore(in table, in pool);
                var storage = new RegionReadSource(in table, in pool, changes);

                var initial = new VoxelCell
                {
                    BaseMaterialId = 1,
                    Surface = new VoxelSurfaceSemantics
                    {
                        StyleId = SurfaceStyles.Smooth,
                        CoatingId = Coatings.Moss,
                        Detail = 3,
                    },
                    Boundary = VoxelBoundarySample.FromSignedQ4(6, extrusionAxis: 0),
                };
                Assert.That(mutations.SetWholeCellBlock(block, in initial, markHardSurface: false),
                    Is.True);
                ulong generation1 = changes.Publish(
                    region: int3.zero,
                    minVoxel: coreMin,
                    maxVoxelExclusive: coreMax,
                    kind: VoxelChangeKind.Occupancy | VoxelChangeKind.BaseMaterial
                        | VoxelChangeKind.SurfaceStyle | VoxelChangeKind.Coating);

                _ = GpuSurfaceMirrorCoordinator.Acquire(requestedBudgetBytes: 1);
                acquired = true;
                GpuSurfaceMirrorCoordinator.PrepareFrame(
                    storage, changes, frame: 1, budgetMs: 50.0, uploadBudgetBytes: 1024 * 1024);
                GpuSurfaceMirrorCoordinator.RequestCoverage(
                    block, brickCacheEdge: 1, coreMin, coreMax);
                coverageRequested = true;

                int scanCursor = 0;
                bool roundIncomplete = false;
                Assert.That(GpuSurfaceMirrorCoordinator.Covers(
                        block, 1, coreMin, coreMax, generation1,
                        ref scanCursor, ref roundIncomplete),
                    Is.False, "First coverage poll must queue the demanded authoritative block.");

                GpuSurfaceMirrorCoordinator.PrepareFrame(
                    storage, changes, frame: 2, budgetMs: 50.0, uploadBudgetBytes: 1024 * 1024);
                Assert.That(GpuSurfaceMirrorCoordinator.Covers(
                        block, 1, coreMin, coreMax, generation1,
                        ref scanCursor, ref roundIncomplete),
                    Is.True, "Initial authoritative generation should become GPU-coverable.");
                uint baselineEpoch = GpuSurfaceMirrorCoordinator.CoverageEpoch;

                var edited = new VoxelCell
                {
                    BaseMaterialId = 2,
                    Surface = new VoxelSurfaceSemantics
                    {
                        StyleId = SurfaceStyles.Rounded,
                        CoatingId = Coatings.Snow,
                        Detail = 11,
                    },
                    Boundary = VoxelBoundarySample.FromSignedQ4(-8, extrusionAxis: 2),
                };
                Assert.That(mutations.SetWholeCellBlock(block, in edited, markHardSurface: false),
                    Is.True);
                // Boundary changes are geometry-affecting occupancy changes in the compact feed;
                // combine that with material/surface/coating flags to exercise the full edit path.
                ulong generation2 = changes.Publish(
                    region: int3.zero,
                    minVoxel: coreMin,
                    maxVoxelExclusive: coreMax,
                    kind: VoxelChangeKind.Occupancy | VoxelChangeKind.BaseMaterial
                        | VoxelChangeKind.SurfaceStyle | VoxelChangeKind.Coating);
                Assert.That(generation2, Is.GreaterThan(generation1));

                GpuSurfaceMirrorCoordinator.PrepareFrame(
                    storage, changes, frame: 3, budgetMs: 50.0, uploadBudgetBytes: 1024 * 1024);

                Assert.That(GpuSurfaceMirrorCoordinator.CoverageEpoch,
                    Is.GreaterThan(baselineEpoch),
                    "A geometry-affecting edit must invalidate previously ready GPU coverage.");
                Assert.That(GpuSurfaceMirrorCoordinator.MirroredVersion, Is.EqualTo(generation2));
                Assert.That(GpuSurfaceMirrorCoordinator.PendingBlockCount, Is.Zero,
                    "Bounded recovery should converge for the single demanded edited block.");
                Assert.That(GpuSurfaceMirrorCoordinator.ReadyBlockCount, Is.EqualTo(1));

                scanCursor = 0;
                roundIncomplete = false;
                Assert.That(GpuSurfaceMirrorCoordinator.Covers(
                        block, 1, coreMin, coreMax, generation1,
                        ref scanCursor, ref roundIncomplete),
                    Is.False,
                    "The pre-edit generation must never be admitted after the edit was observed.");

                scanCursor = 0;
                roundIncomplete = false;
                Assert.That(GpuSurfaceMirrorCoordinator.Covers(
                        block, 1, coreMin, coreMax, generation2,
                        ref scanCursor, ref roundIncomplete),
                    Is.True,
                    "The edited generation must recover through the normal persistent mirror path.");

                Assert.That(storage.TryRead(int3.zero, out VoxelCell stored), Is.True);
                Assert.That(stored, Is.EqualTo(edited),
                    "Recovered coverage must be backed by the edited authoritative semantic cell.");
            }
            finally
            {
                if (coverageRequested)
                    GpuSurfaceMirrorCoordinator.ReleaseCoverage(block, 1, coreMin, coreMax);
                if (acquired)
                    GpuSurfaceMirrorCoordinator.ReleaseReference();
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}
