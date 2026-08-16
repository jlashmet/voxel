using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCaveDecorationEstimateTests
    {
        [Test]
        public void MorePlannedChambersIncreaseDecorationAdmissionCost()
        {
            CavePlan small = Plan(17u, 1);
            CavePlan large = Plan(17u, 5);
            CastleCaveDecorationPlan smallDecoration = CastleCaveDecorationPlanner.Create(small);
            CastleCaveDecorationPlan largeDecoration = CastleCaveDecorationPlanner.Create(large);

            long smallCost = CastleCaveDecorationEstimate.Estimate(small, smallDecoration);
            long largeCost = CastleCaveDecorationEstimate.Estimate(large, largeDecoration);

            Assert.Greater(smallCost, 0);
            Assert.Greater(largeCost, smallCost,
                "Additional planned chambers must increase slow-path cave decoration cost.");
            Assert.AreEqual(smallCost, CastleCaveDecorationEstimate.Estimate(small),
                "Compatibility estimator must match explicit planned decoration.");
        }

        private static CavePlan Plan(uint seed, int secondaryChambers)
        {
            var constraints = new CavePlanningConstraints
            {
                Entrance = new int3(100, 60, 100),
                EntranceToMainOffset = new int3(0, 24, 0),
                MainRadii = new int3(64, 34, 72),
                SecondaryChamberCount = secondaryChambers,
                SecondaryMinRadii = new int3(24, 18, 26),
                SecondaryMaxRadii = new int3(42, 28, 46),
                MinimumHorizontalSpread = 48,
                MaximumHorizontalSpread = 88,
                VerticalSpread = 12,
                PassageWidth = 18,
                PassageHeight = 28,
            };
            return CavePlanner.Create(seed, in constraints);
        }
    }
}
