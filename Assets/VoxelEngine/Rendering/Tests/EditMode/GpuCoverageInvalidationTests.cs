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
