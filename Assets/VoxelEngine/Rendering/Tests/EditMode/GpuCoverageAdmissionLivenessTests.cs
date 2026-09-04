using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuCoverageAdmissionLivenessTests
    {
        [Test]
        public void RegionBlockBitsetHandlesNegativeCoordinatesAndWholeRows()
        {
            var index = new GpuRegionBlockBitset();
            int regionEdge = VoxelReadGrid.BlocksPerRegionEdge;

            Assert.That(index.Add(new int3(-1, 0, 0)), Is.True);
            Assert.That(index.Add(new int3(0, 0, 0)), Is.True);
            Assert.That(index.Add(new int3(regionEdge - 1, 2, 3)), Is.True);
            Assert.That(index.Add(new int3(regionEdge, 2, 3)), Is.True);

            Assert.That(index.GetRowMask(new int3(-1, 0, 0), 0, 0),
                Is.EqualTo(1UL << (regionEdge - 1)));
            Assert.That(index.GetRowMask(int3.zero, 0, 0), Is.EqualTo(1UL));
            Assert.That(index.GetRowMask(int3.zero, 2, 3),
                Is.EqualTo(1UL << (regionEdge - 1)));
            Assert.That(index.GetRowMask(new int3(1, 0, 0), 2, 3), Is.EqualTo(1UL));

            Assert.That(index.Remove(new int3(-1, 0, 0)), Is.True);
            Assert.That(index.Contains(new int3(-1, 0, 0)), Is.False);
            Assert.That(index.Count, Is.EqualTo(3));
        }

        [Test]
        public void StepTwoFootprintQueuesMissingCoverageInOnePollAndConverges()
        {
            Assume.That(SystemInfo.supportsComputeShaders, Is.True,
                "Persistent GPU mirror coverage requires compute shader support.");

            const int edge = 18;
            int3 origin = int3.zero;
            int3 coreMin = int3.zero;
            int3 coreMax = new int3(edge * VoxelReadGrid.BlockEdge);
            int expectedBlocks = edge * edge * edge;

            var table = new RegionTable(expectedResident: 1, Allocator.Persistent);
            var pool = new BrickPool(capacity: 8, Allocator.Persistent);
            bool acquired = false;
            bool coverageRequested = false;

            try
            {
                table.LoadRegion(int3.zero);
                var changes = new VoxelChangeJournal();
                var storage = new RegionReadSource(in table, in pool, changes);
                ulong generation = storage.Version;

                _ = GpuSurfaceMirrorCoordinator.Acquire(requestedBudgetBytes: 1);
                acquired = true;
                GpuSurfaceMirrorCoordinator.PrepareFrame(
                    storage, changes, frame: 1, budgetMs: 50.0,
                    uploadBudgetBytes: 1024 * 1024);
                GpuSurfaceMirrorCoordinator.RequestCoverage(origin, edge, coreMin, coreMax);
                coverageRequested = true;

                ulong pollsBefore = GpuSurfaceMirrorCoordinator.CoveragePolls;
                ulong roundsBefore = GpuSurfaceMirrorCoordinator.CoverageRounds;
                int scanCursor = 0;
                bool roundIncomplete = false;
                Assert.That(GpuSurfaceMirrorCoordinator.Covers(
                        origin, edge, coreMin, coreMax, generation,
                        ref scanCursor, ref roundIncomplete),
                    Is.False);

                Assert.That(GpuSurfaceMirrorCoordinator.CoveragePolls,
                    Is.EqualTo(pollsBefore + 1),
                    "An 18^3 footprint must be evaluated by one range-indexed coverage poll.");
                Assert.That(GpuSurfaceMirrorCoordinator.CoverageRounds,
                    Is.EqualTo(roundsBefore + 1),
                    "Coverage must not need 46 rendered frames merely to finish scanning 18^3 blocks.");
                Assert.That(scanCursor, Is.Zero);
                Assert.That(roundIncomplete, Is.False);
                Assert.That(GpuSurfaceMirrorCoordinator.PendingBlockCount,
                    Is.EqualTo(expectedBlocks),
                    "The first poll must discover the complete resident missing footprint immediately.");

                int frame = 2;
                while (GpuSurfaceMirrorCoordinator.PendingBlockCount > 0 && frame < 66)
                {
                    GpuSurfaceMirrorCoordinator.PrepareFrame(
                        storage, changes, frame++, budgetMs: 50.0,
                        uploadBudgetBytes: 4 * 1024 * 1024);
                }

                Assert.That(GpuSurfaceMirrorCoordinator.PendingBlockCount, Is.Zero,
                    "Demanded coverage must converge instead of remaining permanently queued.");
                Assert.That(GpuSurfaceMirrorCoordinator.ReadyBlockCount,
                    Is.EqualTo(expectedBlocks));

                scanCursor = 0;
                roundIncomplete = false;
                Assert.That(GpuSurfaceMirrorCoordinator.Covers(
                        origin, edge, coreMin, coreMax, generation,
                        ref scanCursor, ref roundIncomplete),
                    Is.True,
                    "Recovered step-2 coverage must admit without another multi-frame full scan.");
            }
            finally
            {
                if (coverageRequested)
                    GpuSurfaceMirrorCoordinator.ReleaseCoverage(origin, edge, coreMin, coreMax);
                if (acquired)
                    GpuSurfaceMirrorCoordinator.ReleaseReference();
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}
