using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCourtyardRealizerTests
    {
        [Test]
        public void PlannedCourtyardBuildsWellAtSuppliedCoordinateOnly()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(4096, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 1);
                var plan = new CastlePlan
                {
                    Centre = new int3(128, 100, 128),
                    PlateauHeight = 20,
                    BaileyHalfX = 80,
                    BaileyHalfZ = 80,
                    Seed = 71u,
                };
                int2[] perimeter =
                {
                    new int2(-60, -60),
                    new int2( 60, -60),
                    new int2( 60,  60),
                    new int2(-60,  60),
                };
                var plannedWell = new int2(28, 12);

                CastleCourtyardRealizer.BuildPlanned(
                    ref brush,
                    in plan,
                    perimeter,
                    hasWell: true,
                    localWellCentre: plannedWell);

                int baseY = plan.Centre.y + plan.PlateauHeight;
                int wellX = plan.Centre.x + plannedWell.x;
                int wellZ = plan.Centre.z + plannedWell.y;

                Assert.AreEqual(Mat.Water, brush.Get(wellX, baseY - 55, wellZ),
                    "The planned well shaft should contain water at the supplied coordinate.");
                Assert.AreEqual(Mat.DarkStone, brush.Get(wellX + 13, baseY + 3, wellZ),
                    "The planned coordinate should receive the authored stone well ring.");

                int legacyX = plan.Centre.x - plan.BaileyHalfX / 2;
                int legacyZ = plan.Centre.z + plan.BaileyHalfZ / 3;
                Assert.AreNotEqual(Mat.Water, brush.Get(legacyX, baseY - 55, legacyZ),
                    "Spatial realization must not rebuild the historical guessed well location.");

                Assert.AreEqual(0, brush.VoxelsWritten,
                    "Spatial courtyard realization should remain on bulk column writes.");
                Assert.Greater(brush.BulkVoxelsWritten, 0);
                Assert.IsFalse(brush.BudgetExceeded);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void PlannedCourtyardCanExplicitlyOmitWell()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(2048, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 1);
                var plan = new CastlePlan
                {
                    Centre = new int3(128, 100, 128),
                    PlateauHeight = 20,
                    Seed = 73u,
                };
                int2[] perimeter =
                {
                    new int2(-32, -32),
                    new int2( 32, -32),
                    new int2( 32,  32),
                    new int2(-32,  32),
                };
                var unusedWell = new int2(18, 0);

                CastleCourtyardRealizer.BuildPlanned(
                    ref brush,
                    in plan,
                    perimeter,
                    hasWell: false,
                    localWellCentre: unusedWell);

                int baseY = plan.Centre.y + plan.PlateauHeight;
                Assert.AreNotEqual(
                    Mat.Water,
                    brush.Get(plan.Centre.x + unusedWell.x, baseY - 55, plan.Centre.z),
                    "hasWell=false must not be overridden by Runtime placement logic.");
                Assert.AreEqual(0, brush.VoxelsWritten);
                Assert.IsFalse(brush.BudgetExceeded);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}
