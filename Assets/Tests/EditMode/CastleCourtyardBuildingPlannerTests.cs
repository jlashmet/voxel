using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCourtyardBuildingPlannerTests
    {
        [Test]
        public void PublicPlannerMatchesSpatialPlanAndProducesValidFootprints()
        {
            int totalBuildings = 0;

            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.KeepPlacement = CastleKeepPlacement.Central;
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

                CastleCourtyardBuildingSpec[] planned =
                    CastleCourtyardBuildingPlanner.Create(in dimensions, spatial);
                Assert.AreEqual(spatial.CourtyardBuildings.Length, planned.Length,
                    $"seed {seed}: public planner diverged from CastleSpatialPlan");

                for (int i = 0; i < planned.Length; i++)
                {
                    CastleCourtyardBuildingSpec a = planned[i];
                    CastleCourtyardBuildingSpec b = spatial.CourtyardBuildings[i];
                    Assert.AreEqual(i, a.Id, $"seed {seed}: unstable building id");
                    Assert.AreEqual(a.Id, b.Id, $"seed {seed}, building {i}: id changed");
                    Assert.AreEqual(a.Role, b.Role, $"seed {seed}, building {i}: role changed");
                    Assert.AreEqual(a.Centre, b.Centre, $"seed {seed}, building {i}: centre changed");
                    Assert.AreEqual(a.HalfExtents, b.HalfExtents,
                        $"seed {seed}, building {i}: footprint changed");
                    Assert.AreEqual(a.Height, b.Height, $"seed {seed}, building {i}: height changed");
                    Assert.AreEqual(a.EntranceDirection, b.EntranceDirection,
                        $"seed {seed}, building {i}: entrance changed");
                    Assert.AreEqual(a.RoofRidgeAlongX, b.RoofRidgeAlongX,
                        $"seed {seed}, building {i}: roof axis changed");

                    Assert.AreEqual(CastleCourtyardBuildingRole.Service, a.Role);
                    Assert.Greater(a.HalfExtents.x, 0, $"seed {seed}, building {i}: invalid half-width");
                    Assert.Greater(a.HalfExtents.y, 0, $"seed {seed}, building {i}: invalid half-depth");
                    Assert.Greater(a.Height, 0, $"seed {seed}, building {i}: invalid height");
                    Assert.AreEqual(1, math.abs(a.EntranceDirection.x) + math.abs(a.EntranceDirection.y),
                        $"seed {seed}, building {i}: entrance direction must be cardinal");

                    for (int corner = 0; corner < 4; corner++)
                    {
                        int2 point = a.FootprintCorner(corner);
                        Assert.IsTrue(CastlePolygonGeometry.ContainsPoint(
                                point, spatial.OuterWardVertices),
                            $"seed {seed}, building {i}: corner {corner} escaped outer ward");
                        if (spatial.InnerWardVertices != null && spatial.InnerWardVertices.Length >= 3)
                        {
                            Assert.IsFalse(CastlePolygonGeometry.ContainsPoint(
                                    point, spatial.InnerWardVertices),
                                $"seed {seed}, building {i}: corner {corner} intruded into inner ward");
                        }
                    }

                    totalBuildings++;
                }
            }

            Assert.Greater(totalBuildings, 0,
                "Planner never found any valid courtyard service-building footprint.");
        }

        [Test]
        public void HighestGroundDefersBuildingsUntilKeepIsResolved()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 401u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(401u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.HighestGround;
            CastleSpatialPlan unresolved = CastleSpatialPlanner.Create(in dimensions, in topology);

            Assert.IsTrue(unresolved.KeepRequiresTerrainResolution);
            Assert.AreEqual(0, unresolved.CourtyardBuildings.Length);
            Assert.AreEqual(0,
                CastleCourtyardBuildingPlanner.Create(in dimensions, unresolved).Length,
                "Courtyard buildings must not be placed against an unresolved keep footprint.");

            CastleSpatialPlan resolved = CastleSpatialPlanner.ResolveHighestGroundKeep(
                in dimensions, unresolved, int2.zero);
            Assert.IsFalse(resolved.KeepRequiresTerrainResolution);

            CastleCourtyardBuildingSpec[] planned =
                CastleCourtyardBuildingPlanner.Create(in dimensions, resolved);
            Assert.AreEqual(resolved.CourtyardBuildings.Length, planned.Length);
        }
    }
}
