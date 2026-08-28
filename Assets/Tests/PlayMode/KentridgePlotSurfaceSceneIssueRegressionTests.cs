using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgePlotSurfaceSceneIssueRegressionTests
    {
        private const uint VoxelShowcaseSeed = 1592594996u;
        private const int MarkedWorldX = 910;
        private const int MarkedWorldZ = 295;

        [Test]
        public void SceneIssue20260826132234356CapturedDirtGrassEdgesAvoidRectangularOwners()
        {
            AssertMayorHousePlotEdgePreservesNaturalTerrain();
            AssertOrganicRouteEdgesUseRoundSurfaceStamps();
        }

        private static void AssertMayorHousePlotEdgePreservesNaturalTerrain()
        {
            FeatureCatalogue plots = KentridgePlotSurfaceCatalogue.Build(
                VoxelShowcaseSeed, BuildSettings(), Allocator.Temp);

            try
            {
                for (int i = 0; i < plots.Definitions.Length; i++)
                    Assert.AreEqual(3, plots.Definitions[i].MaxPrimitives,
                        plots.Definitions[i].Name + " must use one bounded carve/fill/surface pad.");

                int definitionId = -1;
                for (int i = 0; i < plots.Definitions.Length; i++)
                {
                    if (plots.Definitions[i].Name.ToString() == "kentridge-plot-widehouse")
                    {
                        definitionId = i;
                        break;
                    }
                }
                Assert.GreaterOrEqual(definitionId, 0,
                    "The production plot catalogue must retain the WideHouse definition.");

                FeatureDefinition definition = plots.Definitions[definitionId];
                PlacementRule rule = default;
                bool foundRule = false;
                for (int i = 0; i < plots.Rules.Length; i++)
                {
                    if (plots.Rules[i].DefinitionId == definitionId)
                    {
                        rule = plots.Rules[i];
                        foundRule = true;
                        break;
                    }
                }
                Assert.IsTrue(foundRule, "The WideHouse plot definition must retain its placement rule.");
                Assert.AreEqual(40, definition.Precedence,
                    "Plot grading must remain in its established generation stage.");

                ExplicitPlacement mayor = default;
                bool foundMayor = false;
                for (int i = 0; i < rule.ExplicitCount; i++)
                {
                    ExplicitPlacement placement = plots.ExplicitPlacements[rule.ExplicitOffset + i];
                    if (placement.Position.x == 910 && placement.Position.z == 250)
                    {
                        mayor = placement;
                        foundMayor = true;
                        break;
                    }
                }

                Assert.IsTrue(foundMayor,
                    "The exact VoxelShowcase seed must retain MayorHouse at the captured organic plot.");

                int localX = MarkedWorldX - mayor.Position.x;
                int localZ = MarkedWorldZ - mayor.Position.z;
                Assert.AreEqual(0, localX,
                    "The captured upper mark should remain on the MayorHouse parcel west edge.");
                Assert.AreEqual(45, localZ);

                int emittedBoxes = 0;
                int mossLayers = 0;
                bool gradingTouchesMarkedEdge = false;
                int pc = definition.ProgramOffset;
                int end = pc + definition.ProgramLength;
                while (pc < end)
                {
                    ShapeOp op = (ShapeOp)plots.Program[pc];
                    if (op == ShapeOp.EmitBox)
                    {
                        emittedBoxes++;
                        int x = plots.Program[pc + 2];
                        int z = plots.Program[pc + 4];
                        int sx = plots.Program[pc + 5];
                        int sz = plots.Program[pc + 7];
                        byte material = (byte)plots.Program[pc + 8];
                        PrimitiveMode mode = (PrimitiveMode)plots.Program[pc + 11];

                        Assert.AreEqual(6, x,
                            "WideHouse grading must begin at the building envelope, not the parcel boundary.");
                        Assert.AreEqual(4, z);
                        Assert.AreEqual(116, sx);
                        Assert.AreEqual(100, sz);

                        if (material == 14 && mode == PrimitiveMode.Fill)
                            mossLayers++;

                        if (localX >= x && localX < x + sx
                            && localZ >= z && localZ < z + sz)
                            gradingTouchesMarkedEdge = true;
                    }

                    pc += ShapeOps.InstructionLength(op);
                    if (op == ShapeOp.End) break;
                }

                Assert.AreEqual(3, emittedBoxes,
                    "A plot pad must compile to one clearance carve plus dirt and ground-cover fills.");
                Assert.AreEqual(1, mossLayers,
                    "The building envelope needs one authored ground-cover surface, not stacked outward terraces.");
                Assert.IsFalse(gradingTouchesMarkedEdge,
                    "Plot grading must leave the captured parcel edge to deterministic natural terrain.");

                int targetSurface = mayor.Position.y + 12;
                int naturalSurface = TerrainQuery.HeightAt(
                    MarkedWorldX, MarkedWorldZ, VoxelShowcaseSeed);
                Assert.AreEqual(221, targetSurface,
                    "The exact scene seed should retain the MayorHouse frontage target used by the capture.");
                Assert.AreEqual(223, naturalSurface,
                    "The captured parcel edge should retain its deterministic natural terrain height.");
                Assert.Greater(naturalSurface, targetSurface,
                    "A rising artificial feather is unnecessary where natural ground already meets the graded core.");
            }
            finally
            {
                plots.Dispose();
            }
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
