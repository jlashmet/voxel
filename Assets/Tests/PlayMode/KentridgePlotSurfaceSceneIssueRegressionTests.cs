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

        [Test]
        public void SceneIssue20260826132234356CapturedDirtGrassEdgesAvoidRectangularOwners()
        {
            AssertMayorHouseVisibleCapRoundsCapturedCornerWithoutChangingSupport();
            AssertOrganicRouteEdgesUseRoundSurfaceStamps();
        }

        private static void AssertMayorHouseVisibleCapRoundsCapturedCornerWithoutChangingSupport()
        {
            FeatureCatalogue plots = KentridgePlotSurfaceCatalogue.Build(
                VoxelShowcaseSeed, BuildSettings(), Allocator.Temp);
            var primitives = new NativeList<Primitive>(3, Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(1, Allocator.Temp);

            try
            {
                int definitionId = FindDefinition(plots, "kentridge-plot-mayorhouse");
                FeatureDefinition definition = plots.Definitions[definitionId];
                PlacementRule rule = FindRule(plots, definitionId);
                Assert.AreEqual(1, rule.ExplicitCount,
                    "Organic generated-house pads must remain role-specific.");
                Assert.AreEqual(3, definition.MaxPrimitives,
                    "The fix must stay within the existing carve/support/surface primitive budget.");

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
                Assert.AreEqual(3, primitives.Length,
                    "MayorHouse grading should still emit one clearance carve, one support fill, and one visible surface paint.");

                Primitive carve = default;
                Primitive support = default;
                Primitive cap = default;
                bool foundCarve = false;
                bool foundSupport = false;
                bool foundCap = false;
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
                        cap = primitive;
                        foundCap = true;
                    }
                }

                Assert.IsTrue(foundCarve, "Missing MayorHouse clearance carve.");
                Assert.IsTrue(foundSupport, "Missing MayorHouse Dirt support.");
                Assert.IsTrue(foundCap, "Missing MayorHouse rounded Moss surface cap.");
                Assert.AreEqual(PrimitiveShape.Box, carve.Shape);
                Assert.AreEqual(PrimitiveShape.Box, support.Shape);
                Assert.AreEqual(PrimitiveShape.RoundedBox, cap.Shape,
                    "The visible cap must not reintroduce an axis-aligned right-angle grass corner.");
                Assert.AreEqual(12, cap.Radius,
                    "The exact showcase scale should use a 1.2m plan-view corner radius.");

                support.Bounds(out int3 supportMin, out int3 supportMax);
                cap.Bounds(out int3 capMin, out int3 capMax);
                carve.Bounds(out int3 carveMin, out _);

                Assert.AreEqual(new int3(927, placement.Position.y, 286), supportMin,
                    "Production orientation must place the generated foundation support at the captured upper-mark corner.");
                Assert.AreEqual(1024, supportMax.x);
                Assert.AreEqual(371, supportMax.z);
                int surfaceY = placement.Position.y + 12;
                Assert.AreEqual(221, surfaceY,
                    "The exact captured MayorHouse surface elevation is part of the behavioral fixture.");
                Assert.AreEqual(surfaceY, supportMax.y,
                    "Rounding the visible cap must not lower or remove structural support.");
                Assert.AreEqual(surfaceY + 1, carveMin.y,
                    "Clearance must still begin immediately above the unchanged support surface.");
                Assert.AreEqual(supportMin.x, capMin.x);
                Assert.AreEqual(supportMin.z, capMin.z);
                Assert.AreEqual(supportMax.x, capMax.x);
                Assert.AreEqual(supportMax.z, capMax.z);

                var capturedCorner = new int3(927, surfaceY, 286);
                var nearCorner = new int3(929, surfaceY, 288);
                var roundedInterior = new int3(932, surfaceY, 291);
                var southTangent = new int3(939, surfaceY, 286);
                var westTangent = new int3(927, surfaceY, 298);

                AssertInsideBox(support, capturedCorner,
                    "The Dirt support must remain beneath the captured corner; this fix changes material ownership, not occupancy.");
                Assert.IsFalse(CurvedPrimitiveEmitter.Contains(in cap, capturedCorner),
                    "The visible Moss cap must release the exact 90-degree corner seen in the upper marked region.");
                Assert.IsFalse(CurvedPrimitiveEmitter.Contains(in cap, nearCorner),
                    "The cap must remove a meaningful corner wedge rather than only one voxel.");
                Assert.IsTrue(CurvedPrimitiveEmitter.Contains(in cap, roundedInterior),
                    "The rounded transition must retain Moss immediately inside the new curve.");
                Assert.IsTrue(CurvedPrimitiveEmitter.Contains(in cap, southTangent));
                Assert.IsTrue(CurvedPrimitiveEmitter.Contains(in cap, westTangent));
            }
            finally
            {
                anchors.Dispose();
                primitives.Dispose();
                plots.Dispose();
            }
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
