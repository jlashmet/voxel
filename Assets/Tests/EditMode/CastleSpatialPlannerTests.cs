using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialPlannerTests
    {
        [Test]
        public void SpatialPlanIsDeterministicForSameSeed()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleSpatialPlan first = CastleSpatialPlanner.Create(in dimensions, in topology);
                CastleSpatialPlan second = CastleSpatialPlanner.Create(in dimensions, in topology);

                Assert.AreEqual(first.PrimaryGate.EdgeIndex, second.PrimaryGate.EdgeIndex,
                    $"seed {seed}: gate edge");
                Assert.AreEqual(first.PrimaryGate.Centre, second.PrimaryGate.Centre,
                    $"seed {seed}: gate centre");
                Assert.AreEqual(first.KeepCentre, second.KeepCentre, $"seed {seed}: keep centre");
                Assert.AreEqual(first.KeepRequiresTerrainResolution,
                    second.KeepRequiresTerrainResolution, $"seed {seed}: terrain resolution");
                Assert.AreEqual(first.OuterWardVertices.Length, second.OuterWardVertices.Length,
                    $"seed {seed}: outer count");
                Assert.AreEqual(first.Towers.Length, second.Towers.Length,
                    $"seed {seed}: tower count");

                for (int i = 0; i < first.OuterWardVertices.Length; i++)
                    Assert.AreEqual(first.OuterWardVertices[i], second.OuterWardVertices[i],
                        $"seed {seed}: outer vertex {i}");
                for (int i = 0; i < first.Towers.Length; i++)
                {
                    Assert.AreEqual(first.Towers[i].Centre, second.Towers[i].Centre,
                        $"seed {seed}: tower {i} centre");
                    Assert.AreEqual(first.Towers[i].Role, second.Towers[i].Role,
                        $"seed {seed}: tower {i} role");
                }
            }
        }

        [Test]
        public void SpatialPlanRespectsTopologyAndSiteBounds()
        {
            for (uint seed = 1; seed <= 512; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

                Assert.GreaterOrEqual(spatial.OuterWardVertices.Length, 4,
                    $"seed {seed}: too few perimeter vertices");
                Assert.LessOrEqual(spatial.OuterWardVertices.Length, 8,
                    $"seed {seed}: too many perimeter vertices");
                Assert.AreEqual(topology.DesiredTowerCount, spatial.Towers.Length,
                    $"seed {seed}: topology tower count was not realized in the spatial plan");

                long plateauRadiusSquared = (long)dimensions.PlateauRadius * dimensions.PlateauRadius;
                for (int i = 0; i < spatial.OuterWardVertices.Length; i++)
                {
                    int2 vertex = spatial.OuterWardVertices[i];
                    long distanceSquared = (long)vertex.x * vertex.x + (long)vertex.y * vertex.y;
                    Assert.LessOrEqual(distanceSquared, plateauRadiusSquared,
                        $"seed {seed}: perimeter vertex {i} escaped the plateau");
                }

                if (topology.Wards == CastleWardPattern.InnerAndOuterWards)
                    Assert.AreEqual(spatial.OuterWardVertices.Length, spatial.InnerWardVertices.Length,
                        $"seed {seed}: nested ward ring");
                else
                    Assert.AreEqual(0, spatial.InnerWardVertices.Length,
                        $"seed {seed}: unexpected inner ward");
            }
        }

        [Test]
        public void PrimaryGateIsPlacedOnTheFrontmostPerimeterEdge()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
                int gateEdge = spatial.PrimaryGate.EdgeIndex;
                int2 gateStart = spatial.OuterWardVertices[gateEdge];
                int2 gateEnd = spatial.OuterWardVertices[(gateEdge + 1) % spatial.OuterWardVertices.Length];
                int2 expectedCentre = new int2(
                    (gateStart.x + gateEnd.x) / 2,
                    (gateStart.y + gateEnd.y) / 2);

                Assert.AreEqual(expectedCentre, spatial.PrimaryGate.Centre,
                    $"seed {seed}: gate midpoint");

                int gateMidZTwice = gateStart.y + gateEnd.y;
                for (int edge = 0; edge < spatial.OuterWardVertices.Length; edge++)
                {
                    int2 a = spatial.OuterWardVertices[edge];
                    int2 b = spatial.OuterWardVertices[(edge + 1) % spatial.OuterWardVertices.Length];
                    Assert.LessOrEqual(gateMidZTwice, a.y + b.y,
                        $"seed {seed}: edge {edge} is farther forward than the chosen gate");
                }

                float2 toGate = new float2(expectedCentre.x, expectedCentre.y);
                Assert.Greater(math.dot(toGate, spatial.PrimaryGate.Outward), 0f,
                    $"seed {seed}: primary gate normal points into the castle");
            }
        }

        [Test]
        public void KeepPlacementMakesTerrainDependencyExplicit()
        {
            for (uint seed = 1; seed <= 512; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

                if (topology.KeepPlacement == CastleKeepPlacement.HighestGround)
                {
                    Assert.IsTrue(spatial.KeepRequiresTerrainResolution,
                        $"seed {seed}: highest-ground keep must wait for terrain");
                    Assert.AreEqual(int2.zero, spatial.KeepCentre,
                        $"seed {seed}: unresolved keep should not invent a location");
                }
                else
                {
                    Assert.IsFalse(spatial.KeepRequiresTerrainResolution,
                        $"seed {seed}: non-terrain keep unexpectedly unresolved");
                    if (topology.KeepPlacement == CastleKeepPlacement.Central)
                        Assert.AreEqual(int2.zero, spatial.KeepCentre,
                            $"seed {seed}: central keep");
                    else
                        Assert.AreNotEqual(int2.zero, spatial.KeepCentre,
                            $"seed {seed}: rear/integrated keep did not move away from centre");
                }
            }
        }
    }
}
