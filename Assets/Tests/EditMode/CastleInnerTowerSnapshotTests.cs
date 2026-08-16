using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleInnerTowerSnapshotTests
    {
        [Test]
        public void InnerTowerSnapshotDoesNotReplanAfterWardMutation()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 211u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(211u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.InnerAndOuterWards;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
            Assert.Greater(spatial.InnerTowers.Length, 2);
            CastleTowerPlacementSpec plannedTower = spatial.InnerTowers[2];

            spatial.InnerWardVertices[2] += new int2(-3, -2);

            Assert.AreEqual(plannedTower.Centre, spatial.InnerTowers[2].Centre,
                "Completed inner-tower geometry must not be replanned from mutable ward vertices.");
            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.InvalidInnerTowerPlacement, issue,
                "Ward/tower drift must be rejected before Runtime snapshots the completed plan.");
        }
    }
}
