using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialProjectionTests
    {
        [Test]
        public void ProjectionPreservesSemanticKeepCentreThroughLegacyKeepAnchor()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(140, 220, 360), 41u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(41u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Rear;
            topology.HasPosternGate = false;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            CastleSpatialProjection projection = CastleSpatialProjection.Create(in plan, spatial);

            Assert.AreEqual(
                new int2(plan.Centre.x + spatial.KeepCentre.x,
                         plan.Centre.z + spatial.KeepCentre.y),
                projection.KeepCentreWorld);
            Assert.AreEqual(
                CastleLayout.TrapdoorCentre(in projection.KeepPlan),
                projection.TrapdoorCentre);
            Assert.AreEqual(
                CastleLayout.ChapelBellTowerCentre(in projection.KeepPlan),
                projection.ChapelBellTowerCentre);
        }

        [Test]
        public void ProjectionUsesAuthoritativePrimaryGateGeometry()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(90, 180, 270), 52u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(52u);
            topology.Perimeter = CastlePerimeterKind.IrregularQuadrilateral;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            CastleSpatialProjection projection = CastleSpatialProjection.Create(in plan, spatial);
            CastleGatePlacementSpec primary = spatial.PrimaryGate;
            CastleGateGeometry expected = CastleGateGeometryResolver.Resolve(in plan, in primary);

            Assert.AreEqual(expected.Origin, projection.PrimaryGateGeometry.Origin);
            Assert.AreEqual(expected.InteractionPointVoxels,
                            projection.PrimaryGateGeometry.InteractionPointVoxels);
            Assert.AreEqual(CastleApproachFrame.FromGate(in primary).Outward,
                            projection.Approach.Outward);
        }

        [Test]
        public void ProjectionRejectsUnresolvedHighestGroundKeep()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 97u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(97u);
            topology.KeepPlacement = CastleKeepPlacement.HighestGround;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            Assert.IsTrue(spatial.KeepRequiresTerrainResolution);
            Assert.Throws<InvalidOperationException>(() =>
                CastleSpatialProjection.Create(in plan, spatial));
        }
    }
}
