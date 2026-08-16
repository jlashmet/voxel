using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialProjectionCoverageTests
    {
        [Test]
        public void ProjectionMatchesResolvedSpatialGeometryAcrossSeededLayouts()
        {
            for (uint seed = 1; seed <= 512; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(
                    new int3(1000 + (int)seed, 220, 2000 - (int)seed), seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

                if (spatial.KeepRequiresTerrainResolution)
                {
                    spatial = CastleSpatialPlanner.ResolveHighestGroundKeep(
                        in plan, spatial, int2.zero);
                }

                Assert.IsTrue(
                    CastleSpatialPlanValidator.TryValidate(
                        in plan, spatial, out CastleSpatialPlanIssue issue),
                    $"seed {seed}: spatial plan invalid before projection: {issue}");

                CastleSpatialProjection projection = CastleSpatialProjection.Create(in plan, spatial);
                int2 expectedKeepCentre = new int2(
                    plan.Centre.x + spatial.KeepCentre.x,
                    plan.Centre.z + spatial.KeepCentre.y);

                Assert.AreEqual(expectedKeepCentre, projection.KeepCentreWorld,
                    $"seed {seed}: projected keep centre drifted");
                Assert.AreEqual(expectedKeepCentre.x, projection.KeepPlan.Centre.x,
                    $"seed {seed}: legacy keep X anchor drifted");
                Assert.AreEqual(expectedKeepCentre.y, projection.KeepPlan.Centre.z + 60,
                    $"seed {seed}: legacy keep Z anchor drifted");
                Assert.AreEqual(plan.Centre.y, projection.KeepPlan.Centre.y,
                    $"seed {seed}: keep projection changed vertical site anchor");
                Assert.AreEqual(plan.Seed, projection.KeepPlan.Seed,
                    $"seed {seed}: projection changed deterministic seed identity");

                CastleGatePlacementSpec primary = spatial.PrimaryGate;
                CastleGateGeometry expectedGate = CastleGateGeometryResolver.Resolve(
                    in plan, in primary);
                CastleApproachFrame expectedApproach = CastleApproachFrame.FromGate(in primary);

                Assert.AreEqual(expectedGate.Origin, projection.PrimaryGateGeometry.Origin,
                    $"seed {seed}: primary gate origin drifted");
                Assert.AreEqual(expectedGate.InteractionPointVoxels,
                    projection.PrimaryGateGeometry.InteractionPointVoxels,
                    $"seed {seed}: primary gate interaction point drifted");
                Assert.AreEqual(expectedApproach.Outward, projection.Approach.Outward,
                    $"seed {seed}: approach outward basis drifted");
                Assert.AreEqual(expectedApproach.Tangent, projection.Approach.Tangent,
                    $"seed {seed}: approach tangent basis drifted");

                Assert.AreEqual(
                    CastleLayout.TrapdoorCentre(in projection.KeepPlan),
                    projection.TrapdoorCentre,
                    $"seed {seed}: trapdoor drifted from realized keep recipe");
                Assert.AreEqual(
                    CastleLayout.ChapelBellTowerCentre(in projection.KeepPlan),
                    projection.ChapelBellTowerCentre,
                    $"seed {seed}: chapel bell tower drifted from realized keep recipe");
            }
        }

        [Test]
        public void ProjectionRejectsUnresolvedHighestGroundKeep()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 9001u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(9001u);
            topology.KeepPlacement = CastleKeepPlacement.HighestGround;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            Assert.IsTrue(spatial.KeepRequiresTerrainResolution);
            Assert.Throws<InvalidOperationException>(() =>
                CastleSpatialProjection.Create(plan, spatial));
        }

        [Test]
        public void ProjectionRejectsSpatialGeometryCorruptedAfterPlanning()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 9002u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(9002u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            spatial.OuterWardVertices[0] = new int2(plan.PlateauRadius + 1000, 0);

            Assert.Throws<InvalidOperationException>(() =>
                CastleSpatialProjection.Create(plan, spatial));
        }
    }
}
