using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleGatehousePreflightTests
    {
        [Test]
        public void HeavierGatehouseRecipeIncreasesSpatialPreflightEstimate()
        {
            const uint seed = 181u;
            CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
            CastleSpatialPlan probe = CastleSpatialPlanner.Create(in plan, in topology);
            CastleGatePlacementSpec primaryGate = probe.PrimaryGate;

            CastleTopologyPlan baselineTopology = topology;
            baselineTopology.HasGatehousePlan = true;
            baselineTopology.Gatehouse = CastleGatehouseRecipe.Historical(
                in plan, in primaryGate);

            CastleTopologyPlan heavyTopology = baselineTopology;
            CastleGatehousePlan heavy = baselineTopology.Gatehouse;
            heavy.BlockHeight += 18;
            heavy.BridgeLength += 48;
            heavy.BridgeWidth += 10;
            heavy.BridgeDeckHeight += 2;
            heavy.BridgeSupportHeight += 2;
            heavy.BridgeRailHeight += 2;
            heavyTopology.Gatehouse = heavy;

            CastleSpatialPlan baseline = CastleSpatialPlanner.Create(
                in plan, in baselineTopology);
            CastleSpatialPlan heavier = CastleSpatialPlanner.Create(
                in plan, in heavyTopology);

            Assert.IsTrue(
                CastleSpatialPlanValidator.TryValidate(
                    in plan, baseline, out CastleSpatialPlanIssue baselineIssue),
                baselineIssue.ToString());
            Assert.IsTrue(
                CastleSpatialPlanValidator.TryValidate(
                    in plan, heavier, out CastleSpatialPlanIssue heavierIssue),
                heavierIssue.ToString());

            long baselineEstimate = CastleBuildPreflight.EstimateWrites(in plan, baseline);
            long heavierEstimate = CastleBuildPreflight.EstimateWrites(in plan, heavier);

            Assert.Greater(
                heavierEstimate,
                baselineEstimate,
                "Preflight must charge the masonry and bridge work frozen in CastleGatehousePlan.");
        }
    }
}
