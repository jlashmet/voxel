using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepTurretPreflightTests
    {
        [Test]
        public void PlannedKeepTurretsIncreaseSpatialEstimate()
        {
            const uint seed = 127u;
            CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;

            topology.KeepTurrets = null;
            CastleSpatialPlan withoutTurrets = CastleSpatialPlanner.Create(in plan, in topology);
            long withoutEstimate = CastleBuildPreflight.EstimateWrites(in plan, withoutTurrets);

            topology.KeepTurrets = CastleKeepTurretPlanner.Create(seed);
            CastleSpatialPlan withTurrets = CastleSpatialPlanner.Create(in plan, in topology);
            long withEstimate = CastleBuildPreflight.EstimateWrites(in plan, withTurrets);

            Assert.Greater(withEstimate, withoutEstimate,
                "Spatial preflight must budget the keep turrets that Runtime realizes.");
        }
    }
}
