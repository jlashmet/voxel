using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialPlanValidatorTests
    {
        [Test]
        public void GeneratedSpatialPlansAreStructurallyValid()
        {
            for (uint seed = 1; seed <= 512; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

                Assert.IsTrue(
                    CastleSpatialPlanValidator.TryValidate(in dimensions, spatial, out CastleSpatialPlanIssue issue),
                    $"seed {seed}: {issue}");
                Assert.AreEqual(CastleSpatialPlanIssue.None, issue, $"seed {seed}");
            }
        }

        [Test]
        public void ValidatorRejectsSelfIntersectingOuterWardWithNonzeroSignedArea()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 101u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(101u);
            topology.Perimeter = CastlePerimeterKind.IrregularPolygon;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 5;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            int2[] bowTie =
            {
                new int2(-100, -100),
                new int2(100, 100),
                new int2(100, -100),
                new int2(0, 140),
                new int2(-100, 100),
            };
            for (int i = 0; i < bowTie.Length; i++)
                spatial.OuterWardVertices[i] = bowTie[i];

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(in dimensions, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.SelfIntersectingOuterWard, issue);
        }

        [Test]
        public void ValidatorRejectsSelfIntersectingInnerWardBeforeInterpretingItsGate()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 103u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(103u);
            topology.Perimeter = CastlePerimeterKind.IrregularPolygon;
            topology.Wards = CastleWardPattern.InnerAndOuterWards;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 5;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            int2[] bowTie =
            {
                new int2(-50, -50),
                new int2(50, 50),
                new int2(50, -50),
                new int2(0, 70),
                new int2(-50, 50),
            };
            for (int i = 0; i < bowTie.Length; i++)
                spatial.InnerWardVertices[i] = bowTie[i];

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(in dimensions, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.SelfIntersectingInnerWard, issue);
        }

        [Test]
        public void ValidatorRejectsGateDetachedFromItsChosenEdge()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 17u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(17u);
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
            int gateEdge = spatial.PrimaryGate.EdgeIndex;
            spatial.OuterWardVertices[gateEdge] += new int2(7, 0);

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(in dimensions, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.GateDetachedFromPerimeter, issue);
        }

        [Test]
        public void ValidatorRejectsPrimaryGateEdgeTooShortForOpening()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 19u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(19u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            int edge = spatial.PrimaryGate.EdgeIndex;
            int next = (edge + 1) % spatial.OuterWardVertices.Length;
            int2 centre = spatial.PrimaryGate.Centre;
            int2 originalStart = spatial.OuterWardVertices[edge];
            int2 originalEnd = spatial.OuterWardVertices[next];
            float2 tangent = math.normalize(new float2(
                originalEnd.x - originalStart.x,
                originalEnd.y - originalStart.y));
            int minimum = CastleGatePlanningRules.PrimaryMinimumEdgeLength(in dimensions);
            int halfShortEdge = math.max(1, minimum / 2 - 2);
            spatial.OuterWardVertices[edge] = new int2(
                (int)math.round(centre.x - tangent.x * halfShortEdge),
                (int)math.round(centre.y - tangent.y * halfShortEdge));
            spatial.OuterWardVertices[next] = new int2(
                (int)math.round(centre.x + tangent.x * halfShortEdge),
                (int)math.round(centre.y + tangent.y * halfShortEdge));

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(in dimensions, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.GateEdgeTooShort, issue);
        }

        [Test]
        public void ValidatorRejectsTowerThatLeavesThePerimeter()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 31u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(31u);
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
            CastleTowerPlacementSpec tower = spatial.Towers[0];
            tower.Centre += new int2(3, 5);
            spatial.Towers[0] = tower;

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(in dimensions, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.TowerOffPerimeter, issue);
        }

        [Test]
        public void ValidatorRejectsInnerWardThatEscapesOuterWard()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 5u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(5u);
            topology.Wards = CastleWardPattern.InnerAndOuterWards;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
            spatial.InnerWardVertices[0] = spatial.OuterWardVertices[0] * 2;

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(in dimensions, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.InnerWardOutsideOuterWard, issue);
        }

        [Test]
        public void ValidatorRejectsInnerGateDetachedFromItsWard()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 5u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(5u);
            topology.Wards = CastleWardPattern.InnerAndOuterWards;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
            int gateEdge = spatial.InnerGate.EdgeIndex;
            spatial.InnerWardVertices[gateEdge] += new int2(1, 0);

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(in dimensions, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.InnerGateDetachedFromPerimeter, issue);
        }

        [Test]
        public void ValidatorRejectsKeepFootprintThatEscapesOuterWard()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 41u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(41u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            dimensions.KeepHalfX = dimensions.BaileyHalfX + 1;

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(in dimensions, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.KeepOutsideOuterWard, issue);
        }

        [Test]
        public void ValidatorRequiresNestedWardToContainWholeKeep()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 43u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(43u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.InnerAndOuterWards;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            dimensions.KeepHalfX = dimensions.BaileyHalfX * 7 / 10;

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(in dimensions, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.KeepOutsideInnerWard, issue);
        }
    }
}
