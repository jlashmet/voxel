using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleRearKeepPlacementTests
    {
        [TestCase(CastlePerimeterKind.Rectangular, 4)]
        [TestCase(CastlePerimeterKind.IrregularQuadrilateral, 4)]
        [TestCase(CastlePerimeterKind.IrregularPolygon, 6)]
        [TestCase(CastlePerimeterKind.Concentric, 6)]
        public void RearKeepProducesSemanticallyValidPlanAcrossPerimeters(
            CastlePerimeterKind perimeter,
            int towerCount)
        {
            for (uint seed = 1; seed <= 32; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.Perimeter = perimeter;
                topology.DesiredTowerCount = towerCount;
                topology.KeepPlacement = CastleKeepPlacement.Rear;
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
        public void ValidatorRejectsRearKeepThatNoLongerMatchesAuthoredDepth()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 613u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(613u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.DesiredTowerCount = 4;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Rear;
            topology.HasPosternGate = false;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            Assert.IsTrue(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, spatial, out CastleSpatialPlanIssue validIssue),
                validIssue.ToString());

            // The old centre still fits a smaller keep, but the semantic Rear placement should now
            // sit farther toward the rear ward. Containment-only validation would incorrectly pass.
            CastlePlan smallerKeep = dimensions;
            smallerKeep.KeepHalfZ = math.max(1, dimensions.KeepHalfZ - 12);

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(
                    in smallerKeep, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.RearKeepPlacementMismatch, issue);
        }
    }
}
