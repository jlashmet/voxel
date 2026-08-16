using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleWallPlanningTests
    {
        [Test]
        public void HistoricalRecipePreservesLegacyCurtainWallValues()
        {
            CastleWallPlan walls = CastleWallRecipe.Historical();

            Assert.AreEqual(CastleWallStyle.Historical, walls.Style);
            Assert.AreEqual(12, walls.PrimaryGateExtraClearWidth);
            Assert.AreEqual(2, walls.PrimaryGateMinimumThicknessMultiple);
            Assert.AreEqual(22, walls.MaxPlinthHeight);
            Assert.AreEqual(0.66f, walls.CourseHeightFraction);
            Assert.AreEqual(4, walls.CourseMinimumWallHeight);
            Assert.AreEqual(2, walls.CourseThickness);
            Assert.AreEqual(1, walls.WallWalkThickness);
            Assert.AreEqual(70, walls.ArrowSlitMinimumWallHeight);
            Assert.AreEqual(40, walls.ArrowSlitFirstDistance);
            Assert.AreEqual(20, walls.ArrowSlitEndInset);
            Assert.AreEqual(90, walls.ArrowSlitSpacing);
            Assert.AreEqual(40, walls.ArrowSlitYOffset);
            Assert.AreEqual(28, walls.ArrowSlitMaxHeight);
            Assert.AreEqual(2, walls.ArrowSlitThickness);
            Assert.AreEqual(0.65f, walls.ArrowSlitDepthScale);
            Assert.AreEqual(26, walls.CrenellationMerlonLength);
            Assert.AreEqual(18, walls.CrenellationGapLength);
            Assert.AreEqual(20, walls.CrenellationHeight);
            Assert.AreEqual(2, walls.CrenellationMinimumThickness);
            Assert.AreEqual(8, walls.CrenellationMaximumThickness);
        }

        [Test]
        public void SeededWallPlanningIsDeterministicAndValid()
        {
            for (uint seed = 0; seed <= 512; seed++)
            {
                CastleWallPlan first = CastleWallPlanner.Create(seed);
                CastleWallPlan second = CastleWallPlanner.Create(seed);

                Assert.AreEqual(first.Style, second.Style, $"seed {seed}: style");
                Assert.AreEqual(first.MaxPlinthHeight, second.MaxPlinthHeight,
                    $"seed {seed}: plinth");
                Assert.AreEqual(first.CourseHeightFraction, second.CourseHeightFraction,
                    $"seed {seed}: course height");
                Assert.AreEqual(first.ArrowSlitSpacing, second.ArrowSlitSpacing,
                    $"seed {seed}: slit spacing");
                Assert.AreEqual(first.CrenellationMerlonLength,
                                second.CrenellationMerlonLength,
                    $"seed {seed}: merlon length");
                Assert.IsTrue(
                    CastleWallPlanValidator.TryValidate(
                        in first, out CastleWallPlanIssue issue),
                    $"seed {seed}: invalid wall plan: {issue}");
                Assert.AreNotEqual(CastleWallStyle.Historical, first.Style,
                    $"seed {seed}: production planner fell back to compatibility style");
            }
        }

        [Test]
        public void SeedSpaceReachesEveryGeneratedWallStyle()
        {
            bool regular = false;
            bool heavy = false;
            bool austere = false;
            bool ceremonial = false;

            for (uint seed = 0; seed < 2048; seed++)
            {
                CastleWallPlan walls = CastleWallPlanner.Create(seed);
                regular |= walls.Style == CastleWallStyle.Regular;
                heavy |= walls.Style == CastleWallStyle.Heavy;
                austere |= walls.Style == CastleWallStyle.Austere;
                ceremonial |= walls.Style == CastleWallStyle.Ceremonial;
            }

            Assert.IsTrue(regular, "No regular wall style was reachable.");
            Assert.IsTrue(heavy, "No heavy wall style was reachable.");
            Assert.IsTrue(austere, "No austere wall style was reachable.");
            Assert.IsTrue(ceremonial, "No ceremonial wall style was reachable.");
        }

        [Test]
        public void LayoutPlannerCarriesSeededWallStyleIntoTopology()
        {
            bool sawNonRegular = false;
            for (uint seed = 0; seed < 256; seed++)
            {
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleWallPlan expected = CastleWallPlanner.Create(seed);

                Assert.AreEqual(expected.Style, topology.Walls.Style,
                    $"seed {seed}: topology wall style");
                Assert.AreEqual(expected.ArrowSlitSpacing, topology.Walls.ArrowSlitSpacing,
                    $"seed {seed}: topology slit spacing");
                sawNonRegular |= topology.Walls.Style != CastleWallStyle.Regular;
            }

            Assert.IsTrue(sawNonRegular,
                "Topology planning never exposed seeded curtain-wall variation.");
        }

        [Test]
        public void ValidatorRejectsUnknownWallStyle()
        {
            CastleWallPlan walls = CastleWallRecipe.Historical();
            walls.Style = (CastleWallStyle)255;

            Assert.IsFalse(CastleWallPlanValidator.TryValidate(
                in walls, out CastleWallPlanIssue issue));
            Assert.AreEqual(CastleWallPlanIssue.InvalidStyle, issue);
        }
    }
}
