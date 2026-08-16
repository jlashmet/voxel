using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialLayoutProjectionTests
    {
        [Test]
        public void ProjectionPlacesLegacyKeepAnchorUnderSemanticKeepCentre()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(120, 30, 240), 17u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(17u);
            topology.KeepPlacement = CastleKeepPlacement.Rear;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            CastleSpatialLayoutProjection projection =
                CastleSpatialLayoutProjection.Resolve(in plan, spatial);

            Assert.AreEqual(
                plan.Centre.x + spatial.KeepCentre.x,
                projection.KeepPlan.Centre.x);
            Assert.AreEqual(plan.Centre.y, projection.KeepPlan.Centre.y);
            Assert.AreEqual(
                plan.Centre.z + spatial.KeepCentre.y
                - CastleSpatialLayoutProjection.LegacyKeepCentreZOffset,
                projection.KeepPlan.Centre.z);

            int actualKeepCentreZ = projection.KeepPlan.Centre.z
                                  + CastleSpatialLayoutProjection.LegacyKeepCentreZOffset;
            Assert.AreEqual(plan.Centre.z + spatial.KeepCentre.y, actualKeepCentreZ);
        }

        [Test]
        public void ProjectionSharesGateAndKeepLocalInteractionGeometry()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(64, 12, 96), 31u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(31u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            CastleSpatialLayoutProjection projection =
                CastleSpatialLayoutProjection.Resolve(in plan, spatial);
            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleGateGeometry expectedGate = CastleGateGeometryResolver.Resolve(
                in plan, in primaryGate);
            CastlePlan keepPlan = projection.KeepPlan;

            Assert.AreEqual(expectedGate.Origin, projection.PrimaryGate.Origin);
            Assert.AreEqual(expectedGate.InteractionPointVoxels,
                            projection.PrimaryGate.InteractionPointVoxels);
            Assert.AreEqual(CastleLayout.TrapdoorCentre(in keepPlan),
                            projection.TrapdoorCentre);
            Assert.AreEqual(CastleLayout.ChapelBellTowerCentre(in keepPlan),
                            projection.ChapelBellTowerCentre);
        }

        [Test]
        public void ProjectionRejectsUnresolvedHighestGroundKeep()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 43u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(43u);
            topology.KeepPlacement = CastleKeepPlacement.HighestGround;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            Assert.IsTrue(spatial.KeepRequiresTerrainResolution);
            Assert.Throws<InvalidOperationException>(() =>
                CastleSpatialLayoutProjection.Resolve(in plan, spatial));
        }
    }
}
