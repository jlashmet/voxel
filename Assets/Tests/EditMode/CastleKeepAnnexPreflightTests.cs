using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepAnnexPreflightTests
    {
        [Test]
        public void PlannedKeepAnnexesIncreaseSpatialEstimate()
        {
            const uint seed = 131u;
            CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;
            topology.HasKeepAnnexPlan = true;

            topology.KeepAnnexes = new CastleKeepAnnexPlan(
                hasGreatHallWing: false,
                hasChapelWing: false,
                hasBellTower: false,
                hasRearOriel: false);
            CastleSpatialPlan withoutAnnexes = CastleSpatialPlanner.Create(in plan, in topology);
            long withoutEstimate = CastleBuildPreflight.EstimateWrites(in plan, withoutAnnexes);

            topology.KeepAnnexes = CastleKeepAnnexPlanner.Create(in plan);
            CastleSpatialPlan withAnnexes = CastleSpatialPlanner.Create(in plan, in topology);
            long withEstimate = CastleBuildPreflight.EstimateWrites(in plan, withAnnexes);

            Assert.Greater(withEstimate, withoutEstimate,
                "Spatial preflight must budget the keep annexes selected by topology planning.");
        }
    }
}
