using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePosternPlanningTests
    {
        [Test]
        public void PosternSemanticChoiceProducesDistinctValidatedPerimeterGate()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 77u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(77u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 6;
            topology.HasPosternGate = true;

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            Assert.IsTrue(spatial.HasPosternGate);
            Assert.AreNotEqual(spatial.PrimaryGate.EdgeIndex, spatial.PosternGate.EdgeIndex);
            Assert.Greater(
                math.dot(spatial.PosternGate.Outward, -spatial.PrimaryGate.Outward),
                0.5f,
                "Rectangular postern should face broadly away from the primary gate.");
            Assert.IsTrue(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, spatial, out CastleSpatialPlanIssue issue),
                issue.ToString());

            for (int i = 0; i < spatial.Towers.Length; i++)
            {
                if (spatial.Towers[i].Role != CastleTowerPlacementRole.Wall) continue;
                Assert.AreNotEqual(spatial.PosternGate.Centre, spatial.Towers[i].Centre,
                    "Optional wall tower occupied the postern opening.");
            }
        }

        [Test]
        public void DisabledPosternDoesNotInventSpatialGate()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 79u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(79u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            Assert.IsFalse(spatial.HasPosternGate);
            Assert.IsTrue(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, spatial, out CastleSpatialPlanIssue issue),
                issue.ToString());
        }

        [Test]
        public void ValidatorRejectsPosternDetachedFromChosenEdge()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 83u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(83u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = true;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            int edge = spatial.PosternGate.EdgeIndex;
            spatial.OuterWardVertices[edge] += new int2(3, 0);

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.PosternGateDetachedFromPerimeter, issue);
        }
    }
}
