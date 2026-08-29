using System;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Structures.Runtime.Emitters;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgePlotSurfaceSceneIssueRegressionTests
    {
        private const uint VoxelShowcaseSeed = 1592594996u;
        private const byte RoadMaterial = 13;

        // Corrected saved-camera ray envelope from experiment 015, in authored decimetres.
        // The upper immutable circle contains the conspicuous rectangular grass tongue.
        private const int UpperMarkedMinX = 910;
        private const int UpperMarkedMaxX = 938;
        private const int UpperMarkedMinZ = 286;
        private const int UpperMarkedMaxZ = 304;

        [Test]
        public void SceneIssue20260826132234356OrganicRoadWinsAfterPlotGrading()
        {
            SettlementPlan plan = KentridgeDefinition.Build(VoxelShowcaseSeed);
            Assert.Greater(plan.Routes.Count, 0,
                "The VoxelShowcase Kentridge plan must exercise the authored organic-route path.");

            VoxelWorldGenSettings settings = BuildSettings(plan);
            FeatureCatalogue roads = KentridgeDirectedTownSurfaceCatalogue.Build(
                VoxelShowcaseSeed, settings, Allocator.Persistent);
            FeatureCatalogue combined = KentridgeCombinedVoxelCatalogue.Build(
                VoxelShowcaseSeed, settings, Allocator.Persistent);
            var primitives = new NativeList<Primitive>(4, Allocator.Persistent);
            var anchors = new NativeList<ResolvedAnchor>(1, Allocator.Persistent);

            try
            {
                int organicDefinitions = 0;
                bool markedEnvelopeCovered = false;
                for (int definitionId = 0; definitionId < roads.Definitions.Length; definitionId++)
                {
                    FeatureDefinition definition = roads.Definitions[definitionId];
                    string name = definition.Name.ToString();
                    if (!name.StartsWith("kentridge-organic-route-", StringComparison.Ordinal))
                        continue;

                    organicDefinitions++;
                    PlacementRule rule = roads.Rules[definitionId];
                    Assert.Greater(rule.ExplicitCount, 0,
                        "Every organic road-width definition must have production route stamps.");

                    int firstOp = definition.ProgramOffset;
                    int secondOp = firstOp + ShapeOps.InstructionLength(ShapeOp.EmitCylinder);
                    Assert.AreEqual(ShapeOp.EmitCylinder, (ShapeOp)roads.Program[firstOp],
                        "Road clearance still uses an axis-aligned square stamp.");
                    Assert.AreEqual(ShapeOp.EmitCylinder, (ShapeOp)roads.Program[secondOp],
                        "Road surface still uses an axis-aligned square stamp, which creates right-angle Dirt/grass bites once the road owns the final corridor.");

                    ExplicitPlacement firstPlacement = roads.ExplicitPlacements[rule.ExplicitOffset];
                    ParameterSet parameters = FeatureGeneration.ResolveParameters(
                        in roads, in definition, in firstPlacement,
                        definitionId, firstPlacement.Position, VoxelShowcaseSeed);
                    ulong instanceSeed = FeatureGeneration.InstanceSeed(
                        VoxelShowcaseSeed, definitionId, firstPlacement.Position);

                    primitives.Clear();
                    anchors.Clear();
                    EvaluationResult evaluation = ShapeProgram.Evaluate(
                        in roads, definitionId, in parameters,
                        firstPlacement.Position, firstPlacement.Orientation,
                        VoxelShowcaseSeed, instanceSeed, primitives, anchors);
                    Assert.AreEqual(EvaluationResult.Ok, evaluation);
                    Assert.AreEqual(2, primitives.Length,
                        "Organic road stamps should remain a bounded clear+surface pair.");

                    Primitive road = FindRoadSurface(primitives);
                    Assert.AreEqual(PrimitiveShape.Cylinder, road.Shape,
                        "The authored Showcase road surface must have a radial horizontal boundary.");
                    Assert.AreEqual(1, road.Axis,
                        "The road stamp must be a vertical cylinder so its X/Z edge is radial.");

                    int3 centre = new int3(
                        road.A.x + road.Radius,
                        road.A.y,
                        road.A.z + road.Radius);
                    int3 squareCorner = new int3(road.A.x, road.A.y, road.A.z);
                    Assert.IsTrue(CylinderEmitter.Contains(in road, centre),
                        "The rounded stamp must still own the route centre.");
                    Assert.IsFalse(CylinderEmitter.Contains(in road, squareCorner),
                        "The old square corner is still part of the road footprint.");

                    int radius = definition.Footprint.x / 2;
                    for (int p = 0; p < rule.ExplicitCount; p++)
                    {
                        ExplicitPlacement placement = roads.ExplicitPlacements[rule.ExplicitOffset + p];
                        int centreX = placement.Position.x + radius;
                        int centreZ = placement.Position.z + radius;
                        if (CircleIntersectsMarkedEnvelope(centreX, centreZ, radius))
                            markedEnvelopeCovered = true;
                    }
                }

                Assert.Greater(organicDefinitions, 0,
                    "The regression did not inspect the production organic circulation definitions used by VoxelShowcase.");
                Assert.IsTrue(markedEnvelopeCovered,
                    "No production organic route stamp reaches the corrected upper marked envelope; the ownership hypothesis must be re-localized instead of changing composition order.");

                int lastPlot = -1;
                int firstRoad = int.MaxValue;
                int lastRoad = -1;
                int piazza = -1;
                for (int i = 0; i < combined.Definitions.Length; i++)
                {
                    string name = combined.Definitions[i].Name.ToString();
                    if (name.StartsWith("kentridge-plot-", StringComparison.Ordinal))
                        lastPlot = i;
                    else if (name.StartsWith("kentridge-organic-route-", StringComparison.Ordinal))
                    {
                        firstRoad = Math.Min(firstRoad, i);
                        lastRoad = i;
                    }
                    else if (string.Equals(name, "kentridge-market-piazza-hard-surface", StringComparison.Ordinal))
                        piazza = i;
                }

                Assert.GreaterOrEqual(lastPlot, 0, "Combined production catalogue contains no plot grading stage.");
                Assert.AreNotEqual(int.MaxValue, firstRoad, "Combined production catalogue contains no organic road stage.");
                Assert.Greater(firstRoad, lastPlot,
                    "Plot grading still runs after organic circulation and repaints the public road with rectangular Moss at the captured parcel edge.");
                Assert.Greater(piazza, lastRoad,
                    "Moving organic roads after plot grading must not let them overwrite the later authored market piazza.");

                TestContext.WriteLine(
                    $"SCENEISSUE_ROAD_OWNERSHIP seed={VoxelShowcaseSeed} routes={plan.Routes.Count} " +
                    $"definitions={organicDefinitions} lastPlot={lastPlot} firstRoad={firstRoad} piazza={piazza} " +
                    $"markedEnvelope=({UpperMarkedMinX}..{UpperMarkedMaxX},{UpperMarkedMinZ}..{UpperMarkedMaxZ})");
            }
            finally
            {
                anchors.Dispose();
                primitives.Dispose();
                combined.Dispose();
                roads.Dispose();
            }
        }

        private static bool CircleIntersectsMarkedEnvelope(int centreX, int centreZ, int radius)
        {
            int nearestX = math.clamp(centreX, UpperMarkedMinX, UpperMarkedMaxX);
            int nearestZ = math.clamp(centreZ, UpperMarkedMinZ, UpperMarkedMaxZ);
            int dx = centreX - nearestX;
            int dz = centreZ - nearestZ;
            return dx * dx + dz * dz <= radius * radius;
        }

        private static Primitive FindRoadSurface(NativeList<Primitive> primitives)
        {
            for (int i = 0; i < primitives.Length; i++)
            {
                Primitive primitive = primitives[i];
                if (primitive.Mode == PrimitiveMode.Fill && primitive.Material == RoadMaterial)
                    return primitive;
            }

            Assert.Fail("Organic route stamp emitted no production road-surface primitive.");
            return default;
        }

        private static VoxelWorldGenSettings BuildSettings(SettlementPlan plan)
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1, masonry: 1, darkMasonry: 6,
                timber: 2, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: RoadMaterial);
            return new VoxelWorldGenSettings(1, materials, plan);
        }
    }
}
