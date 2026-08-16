using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialCompletionGatehouseTests
    {
        [Test]
        public void CompleteResolvedAttachesMissingGatehousePlan()
        {
            const uint seed = 4103u;
            CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.HasGatehousePlan = false;
            topology.Gatehouse = default;

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            Assert.IsFalse(spatial.Topology.HasGatehousePlan,
                "Pre-terrain spatial planning should not need a frozen gatehouse recipe.");

            CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                in plan, spatial);

            Assert.IsTrue(completed.Topology.HasGatehousePlan,
                "Canonical completion must attach every recipe required by Runtime.");
            CastleGatehousePlan gatehouse = completed.Topology.Gatehouse;
            Assert.IsTrue(
                CastleGatehousePlanValidator.TryValidate(
                    in gatehouse, out CastleGatehousePlanIssue issue),
                issue.ToString());
            Assert.IsTrue(
                CastleGatehousePlanValidator.TryValidateTowerDetails(
                    in gatehouse, plan.FloorHeight, out issue),
                issue.ToString());
        }

        [Test]
        public void CompleteResolvedPreservesValidPreplannedGatehouse()
        {
            const uint seed = 4109u;
            CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.Gatehouse = CastleGatehousePlanner.Create(in plan);
            topology.Gatehouse.BridgeLength += 17;
            topology.HasGatehousePlan = true;

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                in plan, spatial);

            Assert.IsTrue(completed.Topology.HasGatehousePlan);
            Assert.AreEqual(topology.Gatehouse.BridgeLength,
                completed.Topology.Gatehouse.BridgeLength,
                "Completion must preserve an already-valid authored gatehouse recipe.");
        }
    }
}
