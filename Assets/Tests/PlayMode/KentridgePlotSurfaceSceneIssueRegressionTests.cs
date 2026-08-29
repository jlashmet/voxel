using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
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
        private const int ParcelEdgeWorldX = 910;
        private const int FormerPadWorldX = 920;
        private const int MarkedWorldZ = 295;

        [Test]
        public void SceneIssue20260826132234356CapturedDirtGrassEdgesAvoidRectangularOwners()
        {
            AssertMayorHouseGradingMatchesGeneratedFoundation();
            AssertOrganicRouteEdgesUseRoundSurfaceStamps();
        }

        private static void AssertMayorHouseGradingMatchesGeneratedFoundation()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            SettlementPlan plan = KentridgeDefinition.Build(VoxelShowcaseSeed);
            BuildingPlot mayorPlot = default;
            bool foundMayorPlot = false;
            for (int i = 0; i < plan.Plots.Count; i++)
            {
                if (plan.Plots[i].RoleId == (int)KentridgeRole.MayorHouse)
                {
                    mayorPlot = plan.Plots[i];
                    foundMayorPlot = true;
                    break;
                }
            }
            Assert.IsTrue(foundMayorPlot, "The exact showcase seed must retain MayorHouse.");

            StructureForm mayorForm = ArchitectureCompiler.Resolve(
                KentridgeDefinition.StructureIntent(mayorPlot), plan.Theme, VoxelShowcaseSeed);
            Assert.IsTrue(mayorForm.IsGenerated);
            Int3 envelope = KentridgeDefinition.FootprintDm(mayorPlot.Archetype);
            int expectedX = (envelope.X - mayorForm.WidthDm) / 2;
            const int expectedZ = 10;

            FeatureCatalogue plots = KentridgePlotSurfaceCatalogue.Build(
                VoxelShowcaseSeed, settings, Allocator.Temp);

            try
            {
                for (int i = 0; i < plots.Definitions.Length; i++)
                    Assert.AreEqual(3, plots.Definitions[i].MaxPrimitives,
                        plots.Definitions[i].Name + " must use one bounded carve/fill/surface pad.");

                int definitionId = FindDefinition(plots, "kentridge-plot-mayorhouse");
                FeatureDefinition definition = plots.Definitions[definitionId];
                PlacementRule rule = FindRule(plots, definitionId);
                Assert.AreEqual(40, definition.Precedence,
                    "Foundation grading must remain in its established generation stage.");

                ExplicitPlacement mayor = plots.ExplicitPlacements[rule.ExplicitOffset];
                Assert.AreEqual(1, rule.ExplicitCount,
                    "Organic generated-house pads must be role-specific so their geometry cannot expand to another house's size.");
                Assert.AreEqual(910, mayor.Position.x);
                Assert.AreEqual(250, mayor.Position.z);

                int emittedBoxes = 0;
                int mossLayers = 0;
                bool parcelEdgeTouched = false;
                bool formerPadEdgeTouched = false;
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

                        Assert.AreEqual(expectedX, x,
                            "MayorHouse grading must begin at its generated foundation, not an archetype-wide pad.");
                        Assert.AreEqual(expectedZ, z);
                        Assert.AreEqual(mayorForm.WidthDm, sx);
                        Assert.AreEqual(mayorForm.DepthDm, sz);

                        if (material == 14 && mode == PrimitiveMode.Fill)
                            mossLayers++;

                        if (Contains(ParcelEdgeWorldX - mayor.Position.x,
                                     MarkedWorldZ - mayor.Position.z, x, z, sx, sz))
                            parcelEdgeTouched = true;
                        if (Contains(FormerPadWorldX - mayor.Position.x,
                                     MarkedWorldZ - mayor.Position.z, x, z, sx, sz))
                            formerPadEdgeTouched = true;
                    }

                    pc += ShapeOps.InstructionLength(op);
                    if (op == ShapeOp.End) break;
                }

                Assert.AreEqual(17, expectedX,
                    "The exact MayorHouse form should keep its foundation 1.7m inside the captured parcel west edge.");
                Assert.AreEqual(98, mayorForm.WidthDm);
                Assert.AreEqual(86, mayorForm.DepthDm);
                Assert.AreEqual(3, emittedBoxes);
                Assert.AreEqual(1, mossLayers);
                Assert.IsFalse(parcelEdgeTouched,
                    "Plot grading must leave the captured parcel edge to deterministic natural terrain.");
                Assert.IsFalse(formerPadEdgeTouched,
                    "The saved upper mark at x=92.0m was inside the old archetype pad but must remain natural until the actual foundation begins.");

                int targetSurface = mayor.Position.y + 12;
                int naturalSurface = TerrainQuery.HeightAt(
                    ParcelEdgeWorldX, MarkedWorldZ, VoxelShowcaseSeed);
                Assert.AreEqual(221, targetSurface);
                Assert.AreEqual(223, naturalSurface);
                Assert.Greater(naturalSurface, targetSurface,
                    "Natural ground already meets the generated foundation without a visible rectangular apron.");
            }
            finally
            {
                plots.Dispose();
            }
        }

        private static bool Contains(int px, int pz, int x, int z, int sx, int sz) =>
            px >= x && px < x + sx && pz >= z && pz < z + sz;

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
