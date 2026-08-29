using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgePlotSurfaceSceneIssueRegressionTests
    {
        private const uint VoxelShowcaseSeed = 1592594996u;

        [Test]
        public void SceneIssue20260826132234356CapturedDirtGrassEdgesAvoidRectangularOwners()
        {
            AssertMayorHouseVisibleCapCurvesThroughCapturedEnvelopeWithoutChangingSupport();
            AssertOrganicRouteEdgesUseRoundSurfaceStamps();
        }

        private static void AssertMayorHouseVisibleCapCurvesThroughCapturedEnvelopeWithoutChangingSupport()
        {
            FeatureCatalogue plots = KentridgePlotSurfaceCatalogue.Build(
                VoxelShowcaseSeed, BuildSettings(), Allocator.Temp);
            var primitives = new NativeList<Primitive>(5, Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(1, Allocator.Temp);

            try
            {
                int definitionId = FindDefinition(plots, "kentridge-plot-mayorhouse");
                FeatureDefinition definition = plots.Definitions[definitionId];
                PlacementRule rule = FindRule(plots, definitionId);
                Assert.AreEqual(1, rule.ExplicitCount,
                    "Organic generated-house pads must remain role-specific.");
                Assert.AreEqual(5, definition.MaxPrimitives,
                    "The organic pad fix must remain bounded to carve/support plus three surface-paint primitives.");

                ExplicitPlacement placement = plots.ExplicitPlacements[rule.ExplicitOffset];
                Assert.AreEqual(910, placement.Position.x);
                Assert.AreEqual(250, placement.Position.z);
                Assert.AreEqual(2, placement.Orientation,
                    "This regression must exercise MayorHouse's production half-turn; testing the unrotated local program caused the prior false pass.");

                ParameterSet parameters = FeatureGeneration.ResolveParameters(
                    in plots, in definition, in placement,
                    definitionId, placement.Position, VoxelShowcaseSeed);
                ulong instanceSeed = FeatureGeneration.InstanceSeed(
                    VoxelShowcaseSeed, definitionId, placement.Position);
                EvaluationResult evaluation = ShapeProgram.Evaluate(
                    in plots, definitionId, in parameters,
                    placement.Position, placement.Orientation,
                    VoxelShowcaseSeed, instanceSeed, primitives, anchors);

                Assert.AreEqual(EvaluationResult.Ok, evaluation);
                Assert.AreEqual(5, primitives.Length,
                    "MayorHouse grading should emit one clearance carve, one support fill, and a three-primitive visible surface cap.");

                Primitive carve = default;
                Primitive support = default;
                bool foundCarve = false;
                bool foundSupport = false;
                int paintCount = 0;
                int paintBoxes = 0;
                int paintCylinders = 0;
                int paintRadius = -1;
                for (int i = 0; i < primitives.Length; i++)
                {
                    Primitive primitive = primitives[i];
                    if (primitive.Mode == PrimitiveMode.Carve)
                    {
                        carve = primitive;
                        foundCarve = true;
                    }
                    else if (primitive.Mode == PrimitiveMode.Fill && primitive.Material == 13)
                    {
                        support = primitive;
                        foundSupport = true;
                    }
                    else if (primitive.Mode == PrimitiveMode.PaintSurface && primitive.Material == 14)
                    {
                        paintCount++;
                        if (primitive.Shape == PrimitiveShape.Box)
                        {
                            paintBoxes++;
                        }
                        else if (primitive.Shape == PrimitiveShape.Cylinder)
                        {
                            paintCylinders++;
                            Assert.AreEqual(1, primitive.Axis,
                                "Organic plot end-caps must be vertical so their radius exists only in plan view.");
                            if (paintRadius < 0) paintRadius = primitive.Radius;
                            Assert.AreEqual(paintRadius, primitive.Radius,
                                "Both organic plot end-caps must use the same plan radius.");
                        }
                        else
                        {
                            Assert.Fail("Unexpected organic Moss surface primitive: " + primitive.Shape);
                        }
                    }
                }

                Assert.IsTrue(foundCarve, "Missing MayorHouse clearance carve.");
                Assert.IsTrue(foundSupport, "Missing MayorHouse Dirt support.");
                Assert.AreEqual(3, paintCount,
                    "Generated-house Moss ownership must be one bridge plus two round end-caps.");
                Assert.AreEqual(1, paintBoxes);
                Assert.AreEqual(2, paintCylinders);
                Assert.AreEqual(42, paintRadius,
                    "The 9.8m x 8.6m MayorHouse pad must use the largest contained integer plan radius, not the rejected 1.2m corner radius.");

                support.Bounds(out int3 supportMin, out int3 supportMax);
                carve.Bounds(out int3 carveMin, out _);

                Assert.AreEqual(new int3(927, placement.Position.y, 286), supportMin,
                    "Production orientation must place the generated foundation support at the captured upper-mark corner.");
                Assert.AreEqual(1024, supportMax.x);
                Assert.AreEqual(371, supportMax.z);
                int surfaceY = placement.Position.y + 12;
                Assert.AreEqual(221, surfaceY,
                    "The exact captured MayorHouse surface elevation is part of the behavioral fixture.");
                Assert.AreEqual(surfaceY, supportMax.y,
                    "Changing visible Moss ownership must not lower or remove structural support.");
                Assert.AreEqual(surfaceY + 1, carveMin.y,
                    "Clearance must still begin immediately above the unchanged support surface.");

                var capturedCorner = new int3(927, surfaceY, 286);
                var oldStraightWestEdge = new int3(927, surfaceY, 300);
                var curvedMarkInterior = new int3(938, surfaceY, 304);
                var safeInterior = new int3(950, surfaceY, 310);

                AssertInsideBox(support, capturedCorner,
                    "The Dirt support must remain beneath the captured corner; this fix changes material ownership, not occupancy.");
                AssertInsideBox(support, oldStraightWestEdge,
                    "The captured west-edge probe must remain structurally supported.");
                AssertInsideBox(support, curvedMarkInterior,
                    "The captured interior probe must remain structurally supported.");

                Assert.IsFalse(PaintOwns(primitives, capturedCorner),
                    "Moss must release the exact old 90-degree corner in the upper marked region.");
                Assert.IsFalse(PaintOwns(primitives, oldStraightWestEdge),
                    "The rejected 1.2m cap became a straight vertical grass edge inside the upper mark by Z=30.0m; the production cap must still be curving there.");
                Assert.IsTrue(PaintOwns(primitives, curvedMarkInterior),
                    "The upper marked envelope must contain a curved Dirt/Moss transition rather than removing the yard wholesale.");
                Assert.IsTrue(PaintOwns(primitives, safeInterior),
                    "The rounded transition must retain Moss on the generated yard interior.");
            }
            finally
            {
                anchors.Dispose();
                primitives.Dispose();
                plots.Dispose();
            }
        }

        private static bool PaintOwns(NativeList<Primitive> primitives, int3 point)
        {
            for (int i = 0; i < primitives.Length; i++)
            {
                Primitive primitive = primitives[i];
                if (primitive.Mode == PrimitiveMode.PaintSurface
                    && primitive.Material == 14
                    && PrimitiveRasteriser.Contains(in primitive, point))
                    return true;
            }
            return false;
        }

        private static void AssertInsideBox(Primitive primitive, int3 point, string message)
        {
            primitive.Bounds(out int3 min, out int3 max);
            Assert.IsTrue(
                math.all(point >= min) && math.all(point <= max),
                message + " Bounds=" + min + ".." + max + ", point=" + point);
        }

        private static int FindDefinition(FeatureCatalogue catalogue, string name)
        {
            for (int i = 0; i < catalogue.Definitions.Length; i++)
                if (catalogue.Definitions[i].Name.ToString() == name)
                    return i;
            Assert.Fail("Missing production definition: " + name);
            return -1;
        }

        private static PlacementRule FindRule(FeatureCatalogue catalogue, int definitionId)
        {
            for (int i = 0; i < catalogue.Rules.Length; i++)
                if (catalogue.Rules[i].DefinitionId == definitionId)
                    return catalogue.Rules[i];
            Assert.Fail("Missing placement rule for definition " + definitionId);
            return default;
        }

        private static void AssertOrganicRouteEdgesUseRoundSurfaceStamps()
        {
            FeatureCatalogue routes = KentridgeDirectedTownSurfaceCatalogue.Build(
                VoxelShowcaseSeed, BuildSettings(), Allocator.Temp);

            try
            {
                int organicDefinitions = 0;
                for (int i = 0; i < routes.Definitions.Length; i++)
                {
                    FeatureDefinition definition = routes.Definitions[i];
                    string name = definition.Name.ToString();
                    Assert.IsTrue(name.StartsWith("kentridge-organic-route-"),
                        "The exact VoxelShowcase seed must exercise the organic circulation backend.");
                    organicDefinitions++;

                    Assert.AreEqual(20, definition.Precedence);
                    Assert.AreEqual(2, definition.MaxPrimitives,
                        name + " must retain the bounded two-primitive route stamp budget.");
                    Assert.AreEqual(definition.Footprint.x, definition.Footprint.z,
                        name + " must retain the authored route width as its bounding footprint.");

                    int width = definition.Footprint.x;
                    int radius = width / 2;
                    int pc = definition.ProgramOffset;
                    AssertCylinder(routes, pc, radius, 4, radius, radius, 24,
                        1, 0, PrimitiveMode.Carve, name + " clearance");
                    pc += ShapeOps.InstructionLength((ShapeOp)routes.Program[pc]);
                    AssertCylinder(routes, pc, radius, 0, radius, radius, 4,
                        1, 13, PrimitiveMode.Fill, name + " Dirt surface");
                    pc += ShapeOps.InstructionLength((ShapeOp)routes.Program[pc]);
                    Assert.AreEqual(ShapeOp.End, (ShapeOp)routes.Program[pc],
                        name + " must end after its bounded round carve/fill pair.");
                }

                Assert.Greater(organicDefinitions, 0,
                    "The exact VoxelShowcase seed emitted no organic route definitions.");
            }
            finally
            {
                routes.Dispose();
            }
        }

        private static void AssertCylinder(
            FeatureCatalogue catalogue,
            int pc,
            int cx,
            int y,
            int cz,
            int radius,
            int height,
            int axis,
            byte material,
            PrimitiveMode mode,
            string label)
        {
            Assert.AreEqual(ShapeOp.EmitCylinder, (ShapeOp)catalogue.Program[pc],
                label + " must use a round stamp so diagonal route edges cannot expose square corners.");
            Assert.AreEqual(cx, catalogue.Program[pc + 2], label + " center X");
            Assert.AreEqual(y, catalogue.Program[pc + 3], label + " Y");
            Assert.AreEqual(cz, catalogue.Program[pc + 4], label + " center Z");
            Assert.AreEqual(radius, catalogue.Program[pc + 5], label + " radius");
            Assert.AreEqual(height, catalogue.Program[pc + 6], label + " height");
            Assert.AreEqual(axis, catalogue.Program[pc + 7], label + " axis");
            Assert.AreEqual(material, (byte)catalogue.Program[pc + 8], label + " material");
            Assert.AreEqual(mode, (PrimitiveMode)catalogue.Program[pc + 11], label + " mode");
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1, masonry: 1, darkMasonry: 6,
                timber: 2, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
