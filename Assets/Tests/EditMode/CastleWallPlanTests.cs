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
            CastleWallPlan compatibility = CastleWallPlanner.Create();

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

            AssertWallStyleEquals(in walls, in compatibility, "compatibility planner drifted");
        }

        [Test]
        public void SeededWallPlannerIsDeterministicValidAndVaried()
        {
            CastleWallPlan firstStyle = CastleWallPlanner.Create(1u);
            bool sawPlinthVariation = false;
            bool sawCourseVariation = false;
            bool sawSlitVariation = false;
            bool sawCrenellationVariation = false;

            for (uint seed = 1; seed <= 512; seed++)
            {
                CastleWallPlan first = CastleWallPlanner.Create(seed);
                CastleWallPlan second = CastleWallPlanner.Create(seed);
                AssertWallStyleEquals(in first, in second, $"seed {seed}: nondeterministic wall style");
                Assert.IsTrue(
                    CastleWallPlanValidator.TryValidate(in first, out CastleWallPlanIssue issue),
                    $"seed {seed}: {issue}");

                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleWallPlan topologyWalls = topology.Walls;
                AssertWallStyleEquals(
                    in first, in topologyWalls, $"seed {seed}: topology did not freeze seeded walls");

                sawPlinthVariation |= first.MaxPlinthHeight != firstStyle.MaxPlinthHeight;
                sawCourseVariation |= first.CourseHeightFraction != firstStyle.CourseHeightFraction
                                   || first.CourseThickness != firstStyle.CourseThickness
                                   || first.WallWalkThickness != firstStyle.WallWalkThickness;
                sawSlitVariation |= first.ArrowSlitFirstDistance != firstStyle.ArrowSlitFirstDistance
                                 || first.ArrowSlitSpacing != firstStyle.ArrowSlitSpacing
                                 || first.ArrowSlitYOffset != firstStyle.ArrowSlitYOffset;
                sawCrenellationVariation |=
                    first.CrenellationMerlonLength != firstStyle.CrenellationMerlonLength
                 || first.CrenellationGapLength != firstStyle.CrenellationGapLength
                 || first.CrenellationHeight != firstStyle.CrenellationHeight;
            }

            Assert.IsTrue(sawPlinthVariation, "Seeded walls never varied their plinth profile.");
            Assert.IsTrue(sawCourseVariation, "Seeded walls never varied their masonry course profile.");
            Assert.IsTrue(sawSlitVariation, "Seeded walls never varied their arrow-slit profile.");
            Assert.IsTrue(sawCrenellationVariation, "Seeded walls never varied their crenellations.");
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

        private static void AssertWallStyleEquals(
            in CastleWallPlan expected,
            in CastleWallPlan actual,
            string message)
        {
            Assert.AreEqual(expected.PrimaryGateExtraClearWidth, actual.PrimaryGateExtraClearWidth, message);
            Assert.AreEqual(expected.PrimaryGateMinimumThicknessMultiple, actual.PrimaryGateMinimumThicknessMultiple, message);
            Assert.AreEqual(expected.MaxPlinthHeight, actual.MaxPlinthHeight, message);
            Assert.AreEqual(expected.CourseHeightFraction, actual.CourseHeightFraction, message);
            Assert.AreEqual(expected.CourseMinimumWallHeight, actual.CourseMinimumWallHeight, message);
            Assert.AreEqual(expected.CourseThickness, actual.CourseThickness, message);
            Assert.AreEqual(expected.WallWalkThickness, actual.WallWalkThickness, message);
            Assert.AreEqual(expected.ArrowSlitMinimumWallHeight, actual.ArrowSlitMinimumWallHeight, message);
            Assert.AreEqual(expected.ArrowSlitFirstDistance, actual.ArrowSlitFirstDistance, message);
            Assert.AreEqual(expected.ArrowSlitEndInset, actual.ArrowSlitEndInset, message);
            Assert.AreEqual(expected.ArrowSlitSpacing, actual.ArrowSlitSpacing, message);
            Assert.AreEqual(expected.ArrowSlitYOffset, actual.ArrowSlitYOffset, message);
            Assert.AreEqual(expected.ArrowSlitMaxHeight, actual.ArrowSlitMaxHeight, message);
            Assert.AreEqual(expected.ArrowSlitThickness, actual.ArrowSlitThickness, message);
            Assert.AreEqual(expected.ArrowSlitDepthScale, actual.ArrowSlitDepthScale, message);
            Assert.AreEqual(expected.CrenellationMerlonLength, actual.CrenellationMerlonLength, message);
            Assert.AreEqual(expected.CrenellationGapLength, actual.CrenellationGapLength, message);
            Assert.AreEqual(expected.CrenellationHeight, actual.CrenellationHeight, message);
            Assert.AreEqual(expected.CrenellationMinimumThickness, actual.CrenellationMinimumThickness, message);
            Assert.AreEqual(expected.CrenellationMaximumThickness, actual.CrenellationMaximumThickness, message);
        }
    }
}
