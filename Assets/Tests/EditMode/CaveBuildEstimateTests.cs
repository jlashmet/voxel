using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CaveBuildEstimateTests
    {
        [Test]
        public void EstimateIsDeterministicAndPositiveForValidPlan()
        {
            CavePlanningConstraints constraints = Constraints(new int3(82, 36, 104));
            CavePlan plan = CavePlanner.Create(17u, in constraints);

            long first = CaveBuildEstimate.Estimate(plan);
            long second = CaveBuildEstimate.Estimate(plan);

            Assert.Greater(first, 0L);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void LargerPlannedCaveCostsMore()
        {
            CavePlanningConstraints smallConstraints = Constraints(new int3(54, 26, 68));
            CavePlanningConstraints largeConstraints = Constraints(new int3(96, 44, 126));
            CavePlan small = CavePlanner.Create(29u, in smallConstraints);
            CavePlan large = CavePlanner.Create(29u, in largeConstraints);

            Assert.Greater(
                CaveBuildEstimate.Estimate(large),
                CaveBuildEstimate.Estimate(small));
        }

        private static CavePlanningConstraints Constraints(int3 mainRadii) =>
            new CavePlanningConstraints
            {
                Entrance = new int3(120, 80, -40),
                EntranceToMainOffset = new int3(0, 18, 0),
                MainRadii = mainRadii,
                SecondaryChamberCount = 3,
                SecondaryMinRadii = new int3(28, 20, 32),
                SecondaryMaxRadii = new int3(48, 30, 56),
                MinimumHorizontalSpread = 54,
                MaximumHorizontalSpread = 104,
                VerticalSpread = 12,
                PassageWidth = 20,
                PassageHeight = 30,
            };
    }
}
