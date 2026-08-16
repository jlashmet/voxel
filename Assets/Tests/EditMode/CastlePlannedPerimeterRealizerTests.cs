using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedPerimeterRealizerTests
    {
        [Test]
        public void HeavyWallProfileControlsPlinthCourseWalkSlitsAndCrenellations()
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
                    Centre = new int3(96, 2, 96),
                    PlateauHeight = 4,
                    WallHeight = 96,
                    WallThickness = 8,
                };
                int2[] perimeter =
                {
                    new int2(-40, -20),
                    new int2(40, -20),
                    new int2(40, 20),
                    new int2(-40, 20),
                };
                CastleWallPlan walls = CastleWallRecipe.Heavy();

                CastlePlannedPerimeterRealizer.Walls(
                    ref brush, in plan, perimeter, in walls);

                int baseY = plan.Centre.y + plan.PlateauHeight;
                int wallZ = plan.Centre.z - 20;

                Assert.AreEqual(
                    Mat.DarkStone,
                    brush.Get(plan.Centre.x - 30, baseY + 25, wallZ),
                    "Heavy walls should keep the dark plinth through their planned 30-voxel cap.");

                int courseY = baseY + (int)(plan.WallHeight * walls.CourseHeightFraction);
                Assert.AreEqual(
                    Mat.DarkStone,
                    brush.Get(plan.Centre.x - 30, courseY, wallZ),
                    "The planned course height must control the realized masonry band.");

                Assert.AreEqual(
                    Mat.Stone,
                    brush.Get(plan.Centre.x - 30, baseY + plan.WallHeight + 1, wallZ),
                    "Heavy wall walk thickness is two voxels and must be frozen by the wall plan.");

                int slitX = plan.Centre.x - 40 + walls.ArrowSlitFirstDistance;
                Assert.AreEqual(
                    Mat.Empty,
                    brush.Get(slitX, baseY + walls.ArrowSlitYOffset + 4, wallZ),
                    "Arrow-slit offset/spacing must come from the frozen wall recipe.");

                Assert.AreEqual(
                    Mat.Stone,
                    brush.Get(
                        plan.Centre.x - 38,
                        baseY + plan.WallHeight + walls.WallWalkThickness + 12,
                        wallZ),
                    "The first planned merlon should rise above the wall walk.");

                Assert.AreEqual(0, brush.VoxelsWritten,
                    "Planned perimeter realization must remain on bulk writes.");
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
        public void PrimaryGateGapUsesFrozenWallClearancePolicy()
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
                    Centre = new int3(100, 2, 100),
                    PlateauHeight = 4,
                    WallHeight = 64,
                    WallThickness = 8,
                };
                int2[] perimeter =
                {
                    new int2(-50, -30),
                    new int2(50, -30),
                    new int2(50, 30),
                    new int2(-50, 30),
                };
                CastleWallPlan walls = CastleWallRecipe.Heavy();
                var gate = new int2(0, -30);

                CastlePlannedPerimeterRealizer.Walls(
                    ref brush,
                    in plan,
                    perimeter,
                    gateEdgeIndex: 0,
                    localGateCentre: gate,
                    in walls);

                int baseY = plan.Centre.y + plan.PlateauHeight;
                int gateZ = plan.Centre.z + gate.y;
                Assert.AreEqual(Mat.Empty,
                    brush.Get(plan.Centre.x + 30, baseY + 20, gateZ),
                    "Heavy profile should keep a point 30 voxels from gate centre inside the gap.");
                Assert.AreNotEqual(Mat.Empty,
                    brush.Get(plan.Centre.x + 38, baseY + 20, gateZ),
                    "Curtain wall should resume outside the planned heavy gate clearance.");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}
