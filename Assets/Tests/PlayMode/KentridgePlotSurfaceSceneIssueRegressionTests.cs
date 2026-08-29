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

        [Test]
        public void SceneIssue20260826132234356AuthoredShowcaseRoadStampsHaveRadialEdges()
        {
            SettlementPlan plan = KentridgeDefinition.Build(VoxelShowcaseSeed);
            Assert.Greater(plan.Routes.Count, 0,
                "The VoxelShowcase Kentridge plan must exercise the authored organic-route path.");

            VoxelWorldGenSettings settings = BuildSettings(plan);
            FeatureCatalogue roads = KentridgeDirectedTownSurfaceCatalogue.Build(
                VoxelShowcaseSeed, settings, Allocator.Persistent);
            var primitives = new NativeList<Primitive>(4, Allocator.Persistent);
            var anchors = new NativeList<ResolvedAnchor>(1, Allocator.Persistent);

            try
            {
                int organicDefinitions = 0;
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
                        "Road surface still uses an axis-aligned square stamp, which creates the captured right-angle dirt/grass bites.");

                    ExplicitPlacement placement = roads.ExplicitPlacements[rule.ExplicitOffset];
                    ParameterSet parameters = FeatureGeneration.ResolveParameters(
                        in roads, in definition, in placement,
                        definitionId, placement.Position, VoxelShowcaseSeed);
                    ulong instanceSeed = FeatureGeneration.InstanceSeed(
                        VoxelShowcaseSeed, definitionId, placement.Position);

                    primitives.Clear();
                    anchors.Clear();
                    EvaluationResult evaluation = ShapeProgram.Evaluate(
                        in roads, definitionId, in parameters,
                        placement.Position, placement.Orientation,
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
                        "The old square corner is still part of the road footprint; that corner is the captured jagged dirt/grass artifact.");
                }

                Assert.Greater(organicDefinitions, 0,
                    "The regression did not inspect the production organic circulation definitions used by VoxelShowcase.");
                TestContext.WriteLine(
                    $"SCENEISSUE_ROUNDED_ROADS seed={VoxelShowcaseSeed} routes={plan.Routes.Count} definitions={organicDefinitions}");
            }
            finally
            {
                anchors.Dispose();
                primitives.Dispose();
                roads.Dispose();
            }
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
