using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialLayoutProjectionTests
    {
        [Test]
        public void ProjectionPreservesActualKeepCentreAcrossLegacyRecipeAnchor()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(700, 220, 900), 41u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(41u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Rear;
            topology.HasPosternGate = false;
            topology.DesiredTowerCount = 4;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            CastleSpatialLayoutProjection projection =
                CastleSpatialLayoutProjector.Resolve(in plan, spatial);
            int2 actual = CastleSpatialLayoutProjector.ActualKeepCentre(
                in projection.KeepPlan);

            Assert.AreEqual(plan.Centre.x + spatial.KeepCentre.x, actual.x);
            Assert.AreEqual(plan.Centre.z + spatial.KeepCentre.y, actual.y);
            Assert.AreEqual(plan.Centre.y, projection.KeepPlan.Centre.y);
        }

        [Test]
        public void ProjectionUsesAuthoritativePrimaryGateGeometry()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(500, 180, 600), 73u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(73u);
            topology.Perimeter = CastlePerimeterKind.IrregularQuadrilateral;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            CastleSpatialLayoutProjection projection =
                CastleSpatialLayoutProjector.Resolve(in plan, spatial);
            CastleGatePlacementSpec primary = spatial.PrimaryGate;
            CastleGateGeometry direct = CastleGateGeometryResolver.Resolve(in plan, in primary);

            Assert.AreEqual(direct.Origin, projection.PrimaryGate.Origin);
            Assert.AreEqual(direct.Width, projection.PrimaryGate.Width);
            Assert.AreEqual(direct.Height, projection.PrimaryGate.Height);
            Assert.AreEqual(direct.Depth, projection.PrimaryGate.Depth);
            Assert.AreEqual(
                direct.WorldVoxel(17, 29, 2),
                projection.PrimaryGate.WorldVoxel(17, 29, 2));
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
                CastleSpatialLayoutProjector.Resolve(in plan, spatial));
        }
    }
}
