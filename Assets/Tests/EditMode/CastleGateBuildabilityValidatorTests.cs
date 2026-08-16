using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleGateBuildabilityValidatorTests
    {
        [Test]
        public void ValidatorRejectsPosternEdgeTooShortForOpening()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 71u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(71u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = true;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            Assert.IsTrue(spatial.HasPosternGate);
            Assert.AreNotEqual(spatial.PrimaryGate.EdgeIndex, spatial.PosternGate.EdgeIndex);

            ShortenEdgeAroundGate(
                spatial.OuterWardVertices,
                spatial.PosternGate,
                CastleGatePlanningRules.PosternMinimumEdgeLength(in dimensions));

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.PosternGateEdgeTooShort, issue);
        }

        [Test]
        public void ValidatorRejectsInnerGateEdgeTooShortForOpening()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 73u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(73u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.InnerAndOuterWards;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            Assert.IsTrue(spatial.HasInnerGate);

            ShortenEdgeAroundGate(
                spatial.InnerWardVertices,
                spatial.InnerGate,
                CastleGatePlanningRules.InnerMinimumEdgeLength(in dimensions));

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.InnerGateEdgeTooShort, issue);
        }

        private static void ShortenEdgeAroundGate(
            int2[] perimeter,
            CastleGatePlacementSpec gate,
            int minimumLength)
        {
            int edge = gate.EdgeIndex;
            int next = (edge + 1) % perimeter.Length;
            int2 start = perimeter[edge];
            int2 end = perimeter[next];
            float2 tangent = math.normalize(new float2(end.x - start.x, end.y - start.y));
            int halfShortEdge = math.max(1, minimumLength / 2 - 2);

            perimeter[edge] = new int2(
                (int)math.round(gate.Centre.x - tangent.x * halfShortEdge),
                (int)math.round(gate.Centre.y - tangent.y * halfShortEdge));
            perimeter[next] = new int2(
                (int)math.round(gate.Centre.x + tangent.x * halfShortEdge),
                (int)math.round(gate.Centre.y + tangent.y * halfShortEdge));
        }
    }
}
