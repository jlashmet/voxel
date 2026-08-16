using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialLayoutProjectionTests
    {
        [Test]
        public void ProjectionPlacesLegacyKeepRecipeAtSemanticKeepCentre()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(700, 210, 900), 41u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(41u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Rear;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            CastleSpatialLayoutProjection projection =
                CastleSpatialLayoutProjection.Create(in plan, spatial);

            int expectedX = plan.Centre.x + spatial.KeepCentre.x;
            int expectedZ = plan.Centre.z + spatial.KeepCentre.y;
            Assert.AreEqual(expectedX, projection.KeepPlan.Centre.x);
            Assert.AreEqual(
                expectedZ - CastleSpatialLayoutProjection.LegacyKeepCentreZOffset,
                projection.KeepPlan.Centre.z);
            Assert.AreEqual(
                new int2(expectedX, expectedZ),
                CastleSpatialLayoutProjection.ActualKeepCentre(in projection.KeepPlan));

            int3 trapdoor = CastleLayout.TrapdoorCentre(in projection.KeepPlan);
            Assert.AreEqual(expectedX, trapdoor.x);
            Assert.AreEqual(expectedZ + 40, trapdoor.z,
                "Keep-local interaction geometry must follow the semantic keep centre.");
        }

        [Test]
        public void ProjectionUsesAuthoritativePrimaryGateGeometry()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(640, 200, 800), 43u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(43u);
            topology.Perimeter = CastlePerimeterKind.IrregularQuadrilateral;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            CastleSpatialLayoutProjection projection =
                CastleSpatialLayoutProjection.Create(in plan, spatial);
            CastleGatePlacementSpec primary = spatial.PrimaryGate;
            CastleGateGeometry expected = CastleGateGeometryResolver.Resolve(in plan, in primary);

            Assert.AreEqual(expected.Origin, projection.PrimaryGate.Origin);
            Assert.AreEqual(expected.PerimeterCentre, projection.PrimaryGate.PerimeterCentre);
            Assert.AreEqual(expected.Outward, projection.PrimaryGate.Outward);
            Assert.AreEqual(expected.Tangent, projection.PrimaryGate.Tangent);
        }

        [Test]
        public void ProjectionRejectsUnresolvedHighestGroundKeep()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 47u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(47u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.HighestGround;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            Assert.IsTrue(spatial.KeepRequiresTerrainResolution);
            Assert.Throws<InvalidOperationException>(() =>
                CastleSpatialLayoutProjection.Create(in plan, spatial));
        }
    }
}
