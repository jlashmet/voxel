using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleWallPreflightTests
    {
        [Test]
        public void HeavierWallRecipeIncreasesSpatialPreflightEstimate()
        {
            const uint seed = 137u;
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
            CastleTopologyPlan baselineTopology = CastleLayoutPlanner.Create(seed);
            CastleTopologyPlan heavyTopology = baselineTopology;

            CastleWallPlan heavyWalls = heavyTopology.Walls;
            heavyWalls.WallWalkThickness += 3;
            heavyWalls.CrenellationHeight += 8;
            heavyWalls.ArrowSlitSpacing = math.max(1, heavyWalls.ArrowSlitSpacing / 2);
            heavyTopology.Walls = heavyWalls;

            CastleSpatialPlan baseline = CastleSpatialPlanner.Create(
                in dimensions, in baselineTopology);
            CastleSpatialPlan heavy = CastleSpatialPlanner.Create(
                in dimensions, in heavyTopology);

            Assert.IsTrue(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, baseline, out CastleSpatialPlanIssue baselineIssue),
                baselineIssue.ToString());
            Assert.IsTrue(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, heavy, out CastleSpatialPlanIssue heavyIssue),
                heavyIssue.ToString());

            long baselineEstimate = CastleBuildPreflight.EstimateWrites(in dimensions, baseline);
            long heavyEstimate = CastleBuildPreflight.EstimateWrites(in dimensions, heavy);

            Assert.Greater(
                heavyEstimate,
                baselineEstimate,
                "Preflight must charge additional work authored by the frozen CastleWallPlan.");
        }

        [Test]
        public void HistoricalWallRecipeRetainsDeterministicPreflightScale()
        {
            const uint seed = 149u;
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
            topology.Walls = CastleWallRecipe.Historical();
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            long first = CastleBuildPreflight.EstimateWrites(in dimensions, spatial);
            long second = CastleBuildPreflight.EstimateWrites(in dimensions, spatial);

            Assert.AreEqual(first, second,
                "Historical wall calibration must remain deterministic.");
        }
    }
}
