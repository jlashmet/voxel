using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleGatehouseBuildBoundsTests
    {
        [Test]
        public void CastleBoundsIncludeFrozenGatehouseEnvelope()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(256, 220, 376), 163u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(163u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            spatial = CastleGatehousePlanCompletion.Attach(in plan, spatial);
            spatial = CastleSpatialPlanCompletion.CompleteResolved(in plan, spatial);

            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleTopologyPlan completedTopology = spatial.Topology;
            CastleGatehousePlan gatehouse = completedTopology.Gatehouse;
            CastleGatehouseBuildBounds gatehouseBounds =
                CastleGatehouseBuildBoundsResolver.Resolve(
                    in plan, in primaryGate, in gatehouse);
            CastleBuildBounds castleBounds = CastleBuildBoundsResolver.Resolve(in plan, spatial);

            Assert.IsTrue(castleBounds.Contains(gatehouseBounds.Min));
            Assert.IsTrue(castleBounds.Contains(gatehouseBounds.MaxExclusive - 1));
        }

        [Test]
        public void GatehouseBoundsFollowExtendedPlannedBridgeLength()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(256, 220, 376), 167u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(167u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleGatehousePlan gatehouse = CastleGatehousePlanner.Create(
                in plan, in primaryGate);
            gatehouse.BridgeLength = 1400;

            Assert.IsTrue(
                CastleGatehousePlanValidator.TryValidate(
                    in gatehouse, out CastleGatehousePlanIssue issue),
                issue.ToString());
            Assert.IsTrue(
                CastleGatehousePlanValidator.TryValidateTowerDetails(
                    in gatehouse, plan.FloorHeight, out issue),
                issue.ToString());

            CastleGatehouseBuildBounds bounds = CastleGatehouseBuildBoundsResolver.Resolve(
                in plan, in primaryGate, in gatehouse);
            CastleGateGeometry geometry = CastleGateGeometryResolver.Resolve(
                in plan, in primaryGate);
            float farDistance = gatehouse.BridgeNearDistance + gatehouse.BridgeLength;
            float2 farXZ = geometry.PerimeterCentre + geometry.Outward * farDistance;
            int baseY = plan.Centre.y + plan.PlateauHeight;
            var farDeck = new int3(
                (int)math.round(farXZ.x),
                baseY + gatehouse.BridgeDeckYOffset,
                (int)math.round(farXZ.y));

            Assert.IsTrue(bounds.Contains(farDeck),
                "Gatehouse bounds stopped at a historical bridge-length assumption.");
        }
    }
}
