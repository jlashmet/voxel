using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleWallIntegratedKeepTests
    {
        [TestCase(CastlePerimeterKind.Rectangular, 4)]
        [TestCase(CastlePerimeterKind.IrregularQuadrilateral, 4)]
        [TestCase(CastlePerimeterKind.IrregularPolygon, 6)]
        [TestCase(CastlePerimeterKind.Concentric, 6)]
        public void WallIntegratedKeepProducesValidPlanAcrossPerimeters(
            CastlePerimeterKind perimeter,
            int towerCount)
        {
            for (uint seed = 1; seed <= 32; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.Perimeter = perimeter;
                topology.DesiredTowerCount = towerCount;
                topology.KeepPlacement = CastleKeepPlacement.WallIntegrated;
                topology.Wards = perimeter == CastlePerimeterKind.Concentric || (seed & 1u) == 0u
                    ? CastleWardPattern.InnerAndOuterWards
                    : CastleWardPattern.SingleWard;

                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

                Assert.IsFalse(spatial.KeepRequiresTerrainResolution);
                Assert.IsTrue(
                    CastleSpatialPlanValidator.TryValidate(
                        in dimensions, spatial, out CastleSpatialPlanIssue issue),
                    $"seed {seed}, {perimeter}, {topology.Wards}: {issue}");
            }
        }

        [Test]
        public void WallIntegratedKeepSitsFartherRearwardThanRearKeep()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 311u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(311u);
            topology.Perimeter = CastlePerimeterKind.IrregularPolygon;
            topology.DesiredTowerCount = 6;
            topology.Wards = CastleWardPattern.InnerAndOuterWards;
            topology.HasPosternGate = false;

            topology.KeepPlacement = CastleKeepPlacement.Rear;
            CastleSpatialPlan rear = CastleSpatialPlanner.Create(in dimensions, in topology);

            topology.KeepPlacement = CastleKeepPlacement.WallIntegrated;
            CastleSpatialPlan integrated = CastleSpatialPlanner.Create(in dimensions, in topology);

            float2 inward = -integrated.PrimaryGate.Outward;
            float rearDepth = math.dot(new float2(rear.KeepCentre.x, rear.KeepCentre.y), inward);
            float integratedDepth = math.dot(
                new float2(integrated.KeepCentre.x, integrated.KeepCentre.y), inward);

            Assert.Greater(integratedDepth, rearDepth,
                "WallIntegrated must occupy the deepest valid keep position on the gate-to-rear axis.");
        }

        [Test]
        public void ValidatorRejectsWallIntegratedKeepThatIsNoLongerFarthestValidPlacement()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 509u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(509u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.DesiredTowerCount = 4;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.WallIntegrated;
            topology.HasPosternGate = false;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            Assert.IsTrue(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, spatial, out CastleSpatialPlanIssue validIssue),
                validIssue.ToString());

            CastlePlan smallerKeep = dimensions;
            smallerKeep.KeepHalfZ = math.max(1, dimensions.KeepHalfZ - 1);

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(
                    in smallerKeep, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.WallIntegratedKeepNotAgainstWard, issue);
        }
    }
}
