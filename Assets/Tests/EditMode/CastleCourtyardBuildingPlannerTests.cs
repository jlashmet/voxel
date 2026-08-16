using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCourtyardBuildingPlannerTests
    {
        [Test]
        public void PlannerIsDeterministicAndProducesValidWallRelativeFootprints()
        {
            int totalBuildings = 0;
            int stableCount = 0;
            int barracksCount = 0;
            int storesCount = 0;

            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.KeepPlacement = CastleKeepPlacement.Central;
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

                CastleCourtyardBuildingSpec[] first =
                    CastleCourtyardBuildingPlanner.Create(in dimensions, spatial);
                CastleCourtyardBuildingSpec[] second =
                    CastleCourtyardBuildingPlanner.Create(in dimensions, spatial);

                Assert.AreEqual(first.Length, second.Length,
                    $"seed {seed}: building count changed between identical planning passes");

                for (int i = 0; i < first.Length; i++)
                {
                    CastleCourtyardBuildingSpec a = first[i];
                    CastleCourtyardBuildingSpec b = second[i];
                    Assert.AreEqual(i, a.Id, $"seed {seed}: unstable building id");
                    Assert.AreEqual(a.Id, b.Id, $"seed {seed}, building {i}: id changed");
                    Assert.AreEqual(a.Purpose, b.Purpose, $"seed {seed}, building {i}: purpose changed");
                    Assert.AreEqual(a.WallEdgeIndex, b.WallEdgeIndex,
                        $"seed {seed}, building {i}: wall edge changed");
                    Assert.AreEqual(a.Centre, b.Centre, $"seed {seed}, building {i}: centre changed");
                    Assert.AreEqual(a.Tangent, b.Tangent, $"seed {seed}, building {i}: tangent changed");
                    Assert.AreEqual(a.Inward, b.Inward, $"seed {seed}, building {i}: inward changed");
                    Assert.AreEqual(a.Width, b.Width, $"seed {seed}, building {i}: width changed");
                    Assert.AreEqual(a.Depth, b.Depth, $"seed {seed}, building {i}: depth changed");
                    Assert.AreEqual(a.Height, b.Height, $"seed {seed}, building {i}: height changed");

                    Assert.Greater(a.Width, 0, $"seed {seed}, building {i}: invalid width");
                    Assert.Greater(a.Depth, 0, $"seed {seed}, building {i}: invalid depth");
                    Assert.Greater(a.Height, 0, $"seed {seed}, building {i}: invalid height");
                    Assert.That(math.length(a.Tangent), Is.EqualTo(1f).Within(0.001f),
                        $"seed {seed}, building {i}: tangent not normalized");
                    Assert.That(math.length(a.Inward), Is.EqualTo(1f).Within(0.001f),
                        $"seed {seed}, building {i}: inward not normalized");
                    Assert.That(math.abs(math.dot(a.Tangent, a.Inward)), Is.LessThan(0.001f),
                        $"seed {seed}, building {i}: footprint basis not orthogonal");

                    Assert.AreNotEqual(spatial.PrimaryGate.EdgeIndex, a.WallEdgeIndex,
                        $"seed {seed}, building {i}: building occupies primary gate edge");
                    if (spatial.HasPosternGate)
                    {
                        Assert.AreNotEqual(spatial.PosternGate.EdgeIndex, a.WallEdgeIndex,
                            $"seed {seed}, building {i}: building occupies postern edge");
                    }

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
                    switch (a.Purpose)
                    {
                        case CastleCourtyardBuildingPurpose.Stables: stableCount++; break;
                        case CastleCourtyardBuildingPurpose.Barracks: barracksCount++; break;
                        case CastleCourtyardBuildingPurpose.Stores: storesCount++; break;
                    }
                }
            }

            Assert.Greater(totalBuildings, 0, "Planner never found any courtyard building footprint.");
            Assert.Greater(stableCount, 0, "Planner never produced stables across the seed corpus.");
            Assert.Greater(barracksCount, 0, "Planner never produced barracks across the seed corpus.");
            Assert.Greater(storesCount, 0, "Planner never produced stores across the seed corpus.");
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
            Assert.AreEqual(0,
                CastleCourtyardBuildingPlanner.Create(in dimensions, unresolved).Length,
                "Courtyard buildings must not be placed against an unresolved keep footprint.");

            CastleSpatialPlan resolved = CastleSpatialPlanner.ResolveHighestGroundKeep(
                in dimensions, unresolved, int2.zero);
            Assert.IsFalse(resolved.KeepRequiresTerrainResolution);

            Assert.DoesNotThrow(() =>
                CastleCourtyardBuildingPlanner.Create(in dimensions, resolved));
        }
    }
}
