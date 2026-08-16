using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleWallPlanTests
    {
        [Test]
        public void HistoricalWallRecipePreservesLegacyAuthoredValues()
        {
            CastleWallPlan walls = CastleWallRecipe.Historical();

            Assert.IsTrue(
                CastleWallPlanValidator.TryValidate(in walls, out CastleWallPlanIssue issue),
                issue.ToString());
            Assert.AreEqual(12, walls.PrimaryGateExtraClearWidth);
            Assert.AreEqual(2, walls.PrimaryGateMinimumThicknessMultiple);
            Assert.AreEqual(22, walls.MaxPlinthHeight);
            Assert.AreEqual(0.66f, walls.CourseHeightFraction);
            Assert.AreEqual(40, walls.ArrowSlitFirstDistance);
            Assert.AreEqual(90, walls.ArrowSlitSpacing);
            Assert.AreEqual(26, walls.CrenellationMerlonLength);
            Assert.AreEqual(18, walls.CrenellationGapLength);
            Assert.AreEqual(20, walls.CrenellationHeight);
        }

        [Test]
        public void LayoutPlannerFreezesValidWallRecipeIntoTopology()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleWallPlan walls = topology.Walls;
                Assert.IsTrue(
                    CastleWallPlanValidator.TryValidate(in walls, out CastleWallPlanIssue issue),
                    $"seed {seed}: {issue}");
            }
        }

        [Test]
        public void PlannedWallRealizationConsumesFrozenStyleParameters()
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
                    Centre = new int3(80, 2, 80),
                    PlateauHeight = 4,
                    WallHeight = 90,
                    WallThickness = 8,
                };
                int2[] perimeter =
                {
                    new int2(-30, -30),
                    new int2(30, -30),
                    new int2(30, 30),
                    new int2(-30, 30),
                };

                CastleWallPlan walls = CastleWallRecipe.Historical();
                walls.MaxPlinthHeight = 5;
                walls.CourseHeightFraction = 0.5f;
                walls.CourseThickness = 3;
                walls.WallWalkThickness = 2;
                walls.ArrowSlitMinimumWallHeight = 999;
                walls.CrenellationHeight = 7;
                CastleWallPlanValidator.RequireValid(in walls);

                CastlePerimeterRealizer.Walls(ref brush, in plan, perimeter, in walls);

                int baseY = plan.Centre.y + plan.PlateauHeight;
                int midpointX = plan.Centre.x;
                int edgeZ = plan.Centre.z - 30;
                Assert.AreEqual(Mat.DarkStone, brush.Get(midpointX, baseY + 4, edgeZ));
                Assert.AreEqual(Mat.Stone, brush.Get(midpointX, baseY + 10, edgeZ),
                    "Plinth height should come from CastleWallPlan.");
                Assert.AreEqual(Mat.DarkStone, brush.Get(midpointX, baseY + 45, edgeZ),
                    "Course height should come from CastleWallPlan.");
                Assert.AreEqual(Mat.Stone, brush.Get(midpointX, baseY + 48, edgeZ));
                Assert.AreEqual(Mat.Stone, brush.Get(midpointX, baseY + 91, edgeZ),
                    "Wall-walk thickness should come from CastleWallPlan.");

                int merlonX = plan.Centre.x - 20;
                Assert.AreEqual(Mat.Stone, brush.Get(merlonX, baseY + 95, edgeZ),
                    "Crenellation height/profile should come from CastleWallPlan.");
                Assert.AreEqual(Mat.Empty, brush.Get(merlonX, baseY + 99, edgeZ),
                    "Customized seven-voxel crenellation should stop before this row.");

                Assert.AreEqual(0, brush.VoxelsWritten);
                Assert.Greater(brush.BulkVoxelsWritten, 0);
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
