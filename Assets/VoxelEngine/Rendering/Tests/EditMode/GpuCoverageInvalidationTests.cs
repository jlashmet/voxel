using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuCoverageInvalidationTests
    {
        private static void Change(int3 min, int3 max, VoxelChangeKind kind = VoxelChangeKind.Occupancy)
        {
            var change = new VoxelChangeRecord(1, min >> VoxelGrid.RegionVoxelEdgeLog2, min, max, kind);
            typeof(GpuSurfaceMirrorCoordinator).GetMethod("ApplyChange", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { change });
        }

        [Test]
        public void UnrelatedChangesPreserveCoarseScanButChangedHaloInvalidatesIt()
        {
            int3 coarse = new(-65, -1, -1), other = new(128, 0, 0);
            ulong world = GpuSurfaceMirrorCoordinator.RequestCoverage(coarse, 66, default, default);
            GpuSurfaceMirrorCoordinator.RequestCoverage(other, 3, default, default);
            try
            {
                uint before = GpuSurfaceMirrorCoordinator.CoverageEpochFor(coarse, 66);
                uint otherBefore = GpuSurfaceMirrorCoordinator.CoverageEpochFor(other, 3);
                Change(other * 8, other * 8 + 1);
                Assert.That(GpuSurfaceMirrorCoordinator.CoverageEpochFor(coarse, 66), Is.EqualTo(before));
                Assert.That(GpuSurfaceMirrorCoordinator.CoverageEpochFor(other, 3), Is.Not.EqualTo(otherBefore));
                // New residency/occupancy must invalidate already-scanned absent halo cells too,
                // even though no ready mirror block exists at that coordinate yet.
                Change(coarse * 8, coarse * 8 + 1);
                Assert.That(GpuSurfaceMirrorCoordinator.CoverageEpochFor(coarse, 66), Is.Not.EqualTo(before));
            }
            finally
            {
                GpuSurfaceMirrorCoordinator.ReleaseCoverage(coarse, 66, default, default, world);
                GpuSurfaceMirrorCoordinator.ReleaseCoverage(other, 3, default, default, world);
            }
        }

        [Test]
        public void WaterAndExclusiveBoundaryDoNotRestartSolidCoverage()
        {
            int3 origin = new(256, 0, 0);
            ulong world = GpuSurfaceMirrorCoordinator.RequestCoverage(origin, 3, default, default);
            try
            {
                uint before = GpuSurfaceMirrorCoordinator.CoverageEpochFor(origin, 3);
                Change(origin * 8, origin * 8 + 1, VoxelChangeKind.Water);
                Change((origin + 3) * 8, (origin + 3) * 8 + 1);
                Assert.That(GpuSurfaceMirrorCoordinator.CoverageEpochFor(origin, 3), Is.EqualTo(before));
            }
            finally { GpuSurfaceMirrorCoordinator.ReleaseCoverage(origin, 3, default, default, world); }
        }

        private static bool IsDemanded(int3 block) => (bool)typeof(GpuSurfaceMirrorCoordinator)
            .GetMethod("IsBlockDemanded", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { block });

        [Test]
        public void WholeRequestWatchSurvivesPortionReleaseWithoutRetainingItsSources()
        {
            int3 origin = new(-128, 32, -64);
            ulong world = GpuSurfaceMirrorCoordinator.RequestEditWatch(origin, 66);
            int beforeDemands = GpuSurfaceMirrorCoordinator.DemandFootprintCount;
            try
            {
                uint before = GpuSurfaceMirrorCoordinator.CoverageEpochFor(origin, 66);
                Assert.That(IsDemanded(origin), Is.False);
                GpuSurfaceMirrorCoordinator.RequestCoverage(origin, 2, default, default);
                try
                {
                    Assert.That(IsDemanded(origin), Is.True);
                    Assert.That(IsDemanded(origin + new int3(3, 0, 0)), Is.False);
                }
                finally { GpuSurfaceMirrorCoordinator.ReleaseCoverage(origin, 2, default, default, world); }
                Assert.That(IsDemanded(origin), Is.False,
                    "Completed source portions must be eligible for eviction while their summary survives.");
                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.EqualTo(beforeDemands));
                Change(origin * 8, origin * 8 + 1);
                Assert.That(GpuSurfaceMirrorCoordinator.CoverageEpochFor(origin, 66), Is.Not.EqualTo(before),
                    "An edit to a released portion must invalidate the assembled request.");
                uint changed = GpuSurfaceMirrorCoordinator.CoverageEpochFor(origin, 66);
                Change((origin + new int3(80, 0, 0)) * 8, (origin + new int3(80, 0, 0)) * 8 + 1);
                Assert.That(GpuSurfaceMirrorCoordinator.CoverageEpochFor(origin, 66), Is.EqualTo(changed));
            }
            finally { GpuSurfaceMirrorCoordinator.ReleaseEditWatch(origin, 66, world); }
        }

        [Test]
        public void SharedWatchAndCoverageReleaseIndependently()
        {
            int3 origin = new(1024, 0, 0);
            ulong world = GpuSurfaceMirrorCoordinator.RequestEditWatch(origin, 3);
            GpuSurfaceMirrorCoordinator.RequestCoverage(origin, 3, default, default);
            GpuSurfaceMirrorCoordinator.ReleaseCoverage(origin, 3, default, default, world);
            try
            {
                Assert.That(IsDemanded(origin), Is.False);
                uint before = GpuSurfaceMirrorCoordinator.CoverageEpochFor(origin, 3);
                Change(origin * 8, origin * 8 + 1);
                Assert.That(GpuSurfaceMirrorCoordinator.CoverageEpochFor(origin, 3), Is.Not.EqualTo(before));
            }
            finally { GpuSurfaceMirrorCoordinator.ReleaseEditWatch(origin, 3, world); }
        }

        [Test]
        public void RectangularPortionsProtectOnlyTheirActualBricksAndKeepOverlappingReaders()
        {
            int3 origin = new(-256, 48, -128), extent = new(64, 16, 1);
            int3 crossing = origin + new int3(63, 0, 0), crossingExtent = new(1, 1, 2);
            ulong world = GpuSurfaceMirrorCoordinator.RequestSourceRange(origin, extent);
            GpuSurfaceMirrorCoordinator.RequestSourceRange(crossing, crossingExtent);
            bool released = false;
            try
            {
                Assert.That(IsDemanded(origin + extent - 1), Is.True);
                Assert.That(IsDemanded(origin + new int3(0, 0, 1)), Is.False);
                Assert.That(IsDemanded(origin + new int3(0, 16, 0)), Is.False);
                Assert.That(IsDemanded(origin + new int3(64, 0, 0)), Is.False);
                GpuSurfaceMirrorCoordinator.ReleaseSourceRange(origin, extent, world);
                released = true;
                Assert.That(IsDemanded(origin), Is.False);
                Assert.That(IsDemanded(crossing), Is.True);
                Assert.That(IsDemanded(crossing + new int3(0, 0, 1)), Is.True);
            }
            finally
            {
                if (!released) GpuSurfaceMirrorCoordinator.ReleaseSourceRange(origin, extent, world);
                GpuSurfaceMirrorCoordinator.ReleaseSourceRange(crossing, crossingExtent, world);
            }
        }

        [TestCase(66, 16, 1)]
        [TestCase(1, 0, 1)]
        [TestCase(int.MaxValue, int.MaxValue, int.MaxValue)]
        public void SourcePortionCannotExceedOneBoundedSummaryDispatch(int x, int y, int z)
        {
            int before = GpuSurfaceMirrorCoordinator.DemandFootprintCount;
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                GpuSurfaceMirrorCoordinator.RequestSourceRange(int3.zero, new int3(x, y, z)));
            Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.EqualTo(before));
        }

        [Test]
        public void RetiredWorldWatchReleaseCannotRemoveTheNewWorldWatch()
        {
            int3 origin = new(2048, 0, 0);
            ulong oldWorld = GpuSurfaceMirrorCoordinator.RequestEditWatch(origin, 3);
            typeof(GpuSurfaceMirrorCoordinator).GetMethod("ResetWorld", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { false });
            ulong newWorld = GpuSurfaceMirrorCoordinator.RequestEditWatch(origin, 3);
            try
            {
                GpuSurfaceMirrorCoordinator.ReleaseEditWatch(origin, 3, oldWorld);
                uint before = GpuSurfaceMirrorCoordinator.CoverageEpochFor(origin, 3);
                Change(new int3(32768), new int3(32769));
                Assert.That(GpuSurfaceMirrorCoordinator.CoverageEpochFor(origin, 3), Is.EqualTo(before),
                    "A surviving watch must ignore unrelated edits rather than use the global epoch.");
                Change(origin * 8, origin * 8 + 1);
                Assert.That(GpuSurfaceMirrorCoordinator.CoverageEpochFor(origin, 3), Is.Not.EqualTo(before));
            }
            finally { GpuSurfaceMirrorCoordinator.ReleaseEditWatch(origin, 3, newWorld); }
        }

        [Test]
        public void SharedFootprintRetainsInvalidationUntilLastReaderReleases()
        {
            int3 origin = new(512, 0, 0);
            ulong world = GpuSurfaceMirrorCoordinator.RequestCoverage(origin, 3, default, default);
            GpuSurfaceMirrorCoordinator.RequestCoverage(origin, 3, default, default);
            Change(origin * 8, origin * 8 + 1);
            uint stamp = GpuSurfaceMirrorCoordinator.CoverageEpochFor(origin, 3);
            GpuSurfaceMirrorCoordinator.ReleaseCoverage(origin, 3, default, default, world);
            try
            {
                Change(new int3(8192), new int3(8193));
                Assert.That(GpuSurfaceMirrorCoordinator.CoverageEpochFor(origin, 3), Is.EqualTo(stamp));
            }
            finally { GpuSurfaceMirrorCoordinator.ReleaseCoverage(origin, 3, default, default, world); }
        }
    }
}
