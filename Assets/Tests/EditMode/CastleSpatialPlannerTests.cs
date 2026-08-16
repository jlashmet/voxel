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
                Assert.AreEqual(first.HasInnerGate, second.HasInnerGate,
                    $"seed {seed}: inner gate presence");
                if (first.HasInnerGate)
                {
                    Assert.AreEqual(first.InnerGate.EdgeIndex, second.InnerGate.EdgeIndex,
                        $"seed {seed}: inner gate edge");
                    Assert.AreEqual(first.InnerGate.Centre, second.InnerGate.Centre,
                        $"seed {seed}: inner gate centre");
                    Assert.AreEqual(first.InnerGate.Outward, second.InnerGate.Outward,
                        $"seed {seed}: inner gate outward");
                }
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
                {
                    Assert.AreEqual(spatial.OuterWardVertices.Length, spatial.InnerWardVertices.Length,
                        $"seed {seed}: nested ward ring");
                    Assert.IsTrue(spatial.HasInnerGate, $"seed {seed}: nested ward gate");
                }
                else
                {
                    Assert.AreEqual(0, spatial.InnerWardVertices.Length,
                        $"seed {seed}: unexpected inner ward");
                    Assert.IsFalse(spatial.HasInnerGate, $"seed {seed}: unexpected inner gate");
                }
            }
        }

        [Test]
        public void PrimaryGateVariesAcrossBuildableRectangularEdges()
        {
            var seenEdges = new bool[4];

            for (uint seed = 1; seed <= 256; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.Perimeter = CastlePerimeterKind.Rectangular;
                topology.Wards = CastleWardPattern.SingleWard;
                topology.KeepPlacement = CastleKeepPlacement.Central;
                topology.DesiredTowerCount = 4;
                topology.HasPosternGate = false;

                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
                int gateEdge = spatial.PrimaryGate.EdgeIndex;
                seenEdges[gateEdge] = true;
                int2 gateStart = spatial.OuterWardVertices[gateEdge];
                int2 gateEnd = spatial.OuterWardVertices[(gateEdge + 1) % spatial.OuterWardVertices.Length];
                int2 expectedCentre = new int2(
                    (gateStart.x + gateEnd.x) / 2,
                    (gateStart.y + gateEnd.y) / 2);

                Assert.AreEqual(expectedCentre, spatial.PrimaryGate.Centre,
                    $"seed {seed}: gate midpoint");

                long dx = (long)gateEnd.x - gateStart.x;
                long dz = (long)gateEnd.y - gateStart.y;
                int minimumLength = CastleGatePlanningRules.PrimaryMinimumEdgeLength(in dimensions);
                Assert.GreaterOrEqual(dx * dx + dz * dz,
                    (long)minimumLength * minimumLength,
                    $"seed {seed}: gate selected an edge too short for its opening");

                float2 toGate = new float2(expectedCentre.x, expectedCentre.y);
                Assert.Greater(math.dot(toGate, spatial.PrimaryGate.Outward), 0f,
                    $"seed {seed}: primary gate normal points into the castle");
            }

            int distinctEdges = 0;
            for (int i = 0; i < seenEdges.Length; i++)
                if (seenEdges[i]) distinctEdges++;
            Assert.AreEqual(4, distinctEdges,
                "Seeded primary-gate planning should exercise every rectangular approach side.");
        }

        [Test]
        public void PrimaryGateSeedStreamIsIndependentOfOtherTopologyChoices()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                var firstTopology = new CastleTopologyPlan
                {
                    Perimeter = CastlePerimeterKind.Rectangular,
                    KeepPlacement = CastleKeepPlacement.Central,
                    Wards = CastleWardPattern.SingleWard,
                    DesiredTowerCount = 4,
                    HasPosternGate = false,
                };
                CastleTopologyPlan secondTopology = firstTopology;
                secondTopology.KeepPlacement = CastleKeepPlacement.Rear;
                secondTopology.DesiredTowerCount = 6;
                secondTopology.HasPosternGate = true;

                CastleSpatialPlan first = CastleSpatialPlanner.Create(in dimensions, in firstTopology);
                CastleSpatialPlan second = CastleSpatialPlanner.Create(in dimensions, in secondTopology);

                Assert.AreEqual(first.PrimaryGate.EdgeIndex, second.PrimaryGate.EdgeIndex,
                    $"seed {seed}: unrelated topology changed primary gate edge");
                Assert.AreEqual(first.PrimaryGate.Centre, second.PrimaryGate.Centre,
                    $"seed {seed}: unrelated topology changed primary gate centre");
                Assert.AreEqual(first.PrimaryGate.Outward, second.PrimaryGate.Outward,
                    $"seed {seed}: unrelated topology changed primary gate orientation");
            }
        }

        [Test]
        public void InnerGateContinuesThePrimaryApproachThroughNestedWards()
        {
            for (uint seed = 1; seed <= 512; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                if (topology.Wards != CastleWardPattern.InnerAndOuterWards)
                    continue;

                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
                Assert.IsTrue(spatial.HasInnerGate, $"seed {seed}: missing inner gate");
                Assert.AreEqual(spatial.PrimaryGate.EdgeIndex, spatial.InnerGate.EdgeIndex,
                    $"seed {seed}: inner gate moved to a different perimeter side");

                int edge = spatial.InnerGate.EdgeIndex;
                int2 a = spatial.InnerWardVertices[edge];
                int2 b = spatial.InnerWardVertices[(edge + 1) % spatial.InnerWardVertices.Length];
                Assert.AreEqual(new int2((a.x + b.x) / 2, (a.y + b.y) / 2),
                    spatial.InnerGate.Centre, $"seed {seed}: inner gate midpoint");
                Assert.Greater(math.dot(spatial.InnerGate.Outward, spatial.PrimaryGate.Outward), 0.5f,
                    $"seed {seed}: inner gate faces away from the primary approach");
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
