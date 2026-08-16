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
                CastleSpatialProjection.KeepMinimum(in projection.KeepPlan),
                projection.KeepMinimumWorld);
            Assert.AreEqual(
                CastleLayout.TrapdoorCentre(in projection.KeepPlan),
                projection.TrapdoorCentre);
            Assert.AreEqual(
                CastleLayout.ChapelBellTowerCentre(in projection.KeepPlan),
                projection.ChapelBellTowerCentre);
        }

        [Test]
        public void KeepProjectionHelpersRoundTripSemanticCentreAndBounds()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(180, 210, 410), 47u);
            var localKeepCentre = new int2(73, -54);

            CastlePlan projected = CastleSpatialProjection.ProjectKeepPlan(
                in plan, localKeepCentre);
            int2 actual = CastleSpatialProjection.ActualKeepCentre(in projected);
            int3 minimum = CastleSpatialProjection.KeepMinimum(in projected);

            Assert.AreEqual(
                new int2(plan.Centre.x + localKeepCentre.x,
                         plan.Centre.z + localKeepCentre.y),
                actual);
            Assert.AreEqual(
                new int3(
                    actual.x - plan.KeepHalfX,
                    plan.Centre.y + plan.PlateauHeight,
                    actual.y - plan.KeepHalfZ),
                minimum);
            Assert.AreEqual(plan.Centre.y, projected.Centre.y);
        }

        [Test]
        public void KeepMinimumPreservesHistoricalCompatibilityPlacement()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(210, 205, 390), 49u);

            int3 minimum = CastleSpatialProjection.KeepMinimum(in plan);

            Assert.AreEqual(
                new int3(
                    plan.Centre.x - plan.KeepHalfX,
                    plan.Centre.y + plan.PlateauHeight,
                    plan.Centre.z - plan.KeepHalfZ + CastleLayout.LegacyKeepCentreZOffset),
                minimum);
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

        [Test]
        public void CompletedDungeonPlanValidatesAndProjectsWithoutRecursiveValidation()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(256, 220, 376), 223u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(223u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan raw = CastleSpatialPlanner.Create(in plan, in topology);
            CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                in plan, raw);

            Assert.NotNull(completed.Dungeon);
            Assert.IsTrue(
                CastleSpatialPlanValidator.TryValidate(
                    in plan, completed, out CastleSpatialPlanIssue issue),
                issue.ToString());

            CastleSpatialProjection projection = CastleSpatialProjection.Create(in plan, completed);
            Assert.AreEqual(completed.Dungeon.Entrance, projection.TrapdoorCentre);
        }
    }
}
