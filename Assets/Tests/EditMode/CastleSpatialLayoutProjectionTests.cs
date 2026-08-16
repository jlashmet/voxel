using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialLayoutProjectionTests
    {
        [Test]
        public void ProjectionKeepsGateInteractionAndKeepLocalGeometryInOneCoordinateContract()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(700, 120, 900), 41u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(41u);
            topology.Perimeter = CastlePerimeterKind.IrregularQuadrilateral;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Rear;
            topology.HasPosternGate = false;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            CastleSpatialLayoutProjection projection =
                CastleSpatialLayoutProjection.Resolve(in plan, spatial);

            Assert.AreEqual(
                new int2(plan.Centre.x + spatial.KeepCentre.x,
                         plan.Centre.z + spatial.KeepCentre.y),
                projection.KeepCentreWorld);
            Assert.AreEqual(projection.KeepCentreWorld.x, projection.KeepPlan.Centre.x);
            Assert.AreEqual(projection.KeepCentreWorld.y - 60, projection.KeepPlan.Centre.z,
                "Only the projection should carry the historical +60 Z keep authoring offset.");
            Assert.AreEqual(
                CastleLayout.TrapdoorCentre(in projection.KeepPlan),
                projection.TrapdoorCentre);
            Assert.AreEqual(
                CastleLayout.ChapelBellTowerCentre(in projection.KeepPlan),
                projection.ChapelBellTowerCentre);

            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleGateGeometry expectedGate = CastleGateGeometryResolver.Resolve(
                in plan, in primaryGate);
            Assert.AreEqual(expectedGate.Origin, projection.PrimaryGate.Origin);
            Assert.AreEqual(expectedGate.InteractionPointVoxels,
                            projection.PrimaryGate.InteractionPointVoxels);
        }

        [Test]
        public void ProjectionRejectsUnresolvedHighestGroundKeep()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 53u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(53u);
            topology.KeepPlacement = CastleKeepPlacement.HighestGround;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            Assert.IsTrue(spatial.KeepRequiresTerrainResolution);
            Assert.Throws<InvalidOperationException>(() =>
                CastleSpatialLayoutProjection.Resolve(plan, spatial));
        }
    }
}
