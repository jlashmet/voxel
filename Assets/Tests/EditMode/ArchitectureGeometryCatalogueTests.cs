using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ArchitectureGeometryCatalogueTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void StructureGeometryProfileKeepsMassingOpeningsDetailsRoofsAndSurfacesIndependent()
        {
            var profile = new StructureGeometryProfile(
                foundationCornerRadiusDm: 1,
                shellCornerRadiusDm: 2,
                openingCornerRadiusDm: 3,
                detailCornerRadiusDm: 4,
                foundationSurface: StructureSurfaceTreatment.Beveled,
                shellSurface: StructureSurfaceTreatment.ArchitecturalRounded,
                openingSurface: StructureSurfaceTreatment.Smooth,
                detailSurface: StructureSurfaceTreatment.Planar,
                roofSurface: StructureSurfaceTreatment.MasonryJoint);

            Assert.AreEqual(1, profile.FoundationCornerRadiusDm);
            Assert.AreEqual(2, profile.ShellCornerRadiusDm);
            Assert.AreEqual(3, profile.OpeningCornerRadiusDm);
            Assert.AreEqual(4, profile.DetailCornerRadiusDm);
            Assert.AreEqual(StructureSurfaceTreatment.Beveled, profile.FoundationSurface);
            Assert.AreEqual(StructureSurfaceTreatment.ArchitecturalRounded, profile.ShellSurface);
            Assert.AreEqual(StructureSurfaceTreatment.Smooth, profile.OpeningSurface);
            Assert.AreEqual(StructureSurfaceTreatment.Planar, profile.DetailSurface);
            Assert.AreEqual(StructureSurfaceTreatment.MasonryJoint, profile.RoofSurface);
            Assert.IsTrue(profile.HasRoundedGeometry);
            Assert.IsTrue(profile.HasSurfaceOverrides);
            Assert.IsTrue(profile.RequiresRealization);
            Assert.IsFalse(StructureGeometryProfile.Sharp.RequiresRealization);
        }

        [Test]
        public void ArchitecturalRoundedSurfaceIsSofterThanGeneralRoundedSurface()
        {
            SurfaceCatalogueView surfaces = SurfaceCatalogueView.CreateBuiltIns();
            SurfaceStyleReadDefinition rounded = surfaces.Get(SurfaceStyles.Rounded);
            SurfaceStyleReadDefinition architectural =
                surfaces.Get(SurfaceStyles.ArchitecturalRounded);

            Assert.AreEqual(SurfaceReconstruction.Rounded, architectural.Reconstruction);
            Assert.Greater(architectural.Curvature, rounded.Curvature,
                "Architecture should be able to request stronger reconstruction curvature.");

            SurfaceJoinReadRule roundedJoin = surfaces.GetJoin(
                rounded.JoinGroup, rounded.JoinGroup);
            SurfaceJoinReadRule architecturalJoin = surfaces.GetJoin(
                architectural.JoinGroup, architectural.JoinGroup);
            Assert.AreEqual(SurfaceContinuity.Smooth, architecturalJoin.Continuity);
            Assert.Greater(architecturalJoin.BlendWidth, roundedJoin.BlendWidth,
                "Architecture should have a wider smooth join than the general rounded style.");
        }

        [Test]
        public void ArchitectureShapeBuilderAppliesSemanticOpeningAndRoofPolicies()
        {
            var profile = new StructureGeometryProfile(
                foundationCornerRadiusDm: 0,
                shellCornerRadiusDm: 0,
                openingCornerRadiusDm: 2,
                detailCornerRadiusDm: 0,
                openingSurface: StructureSurfaceTreatment.ArchitecturalRounded,
                roofSurface: StructureSurfaceTreatment.Smooth);
            var builder = new ArchitectureShapeProgramBuilder(profile, voxelsPerDecimetre: 1);

            builder.OpeningCarve(2, 3, 4, 8, 12, 4);
            builder.Prism(0, 20, 0, 30, 8, 24, PrismProfile.Gable, material: 7);
            int[] code = builder.Finish();

            Assert.AreEqual(ShapeOp.EmitRoundedBox, (ShapeOp)code[0]);
            Assert.AreEqual(2, code[8], "Opening rounding should come from OpeningCornerRadiusDm.");
            Assert.AreEqual(SurfaceStyles.ArchitecturalRounded, (ushort)code[10]);
            Assert.AreEqual(PrimitiveMode.Carve, (PrimitiveMode)code[12]);

            int prism = ShapeOps.InstructionLength(ShapeOp.EmitRoundedBox);
            Assert.AreEqual(ShapeOp.EmitPrism, (ShapeOp)code[prism]);
            Assert.AreEqual(SurfaceStyles.Smooth, (ushort)code[prism + 10],
                "Roof prisms should use the profile's RoofSurface by default.");
        }

        [Test]
        public void KentridgeGrammarAuthorsGeometryRolesDirectly()
        {
            FeatureCatalogue catalogue = KentridgeGrammarVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                int architecturalRounded = 0;
                int beveled = 0;
                int roundedOpenings = 0;
                int smoothRoofs = 0;

                for (int definitionIndex = 0;
                     definitionIndex < catalogue.Definitions.Length;
                     definitionIndex++)
                {
                    FeatureDefinition definition = catalogue.Definitions[definitionIndex];
                    if (!definition.Name.ToString().StartsWith("kentridge-role-"))
                        continue;

                    int pc = definition.ProgramOffset;
                    int end = definition.ProgramOffset + definition.ProgramLength;
                    while (pc < end)
                    {
                        ShapeOp op = (ShapeOp)catalogue.Program[pc];
                        int length = ShapeOps.InstructionLength(op);
                        Assert.GreaterOrEqual(length, 2, definition.Name.ToString());

                        if (op == ShapeOp.EmitRoundedBox)
                        {
                            ushort surface = (ushort)catalogue.Program[pc + 10];
                            PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 12];
                            if (surface == SurfaceStyles.ArchitecturalRounded)
                                architecturalRounded++;
                            if (surface == SurfaceStyles.Beveled)
                                beveled++;
                            if (mode == PrimitiveMode.Carve
                                && surface == SurfaceStyles.ArchitecturalRounded)
                                roundedOpenings++;
                        }
                        else if (op == ShapeOp.EmitPrism)
                        {
                            ushort surface = (ushort)catalogue.Program[pc + 10];
                            if (surface == SurfaceStyles.Smooth)
                                smoothRoofs++;
                        }

                        pc += length;
                        if (op == ShapeOp.End) break;
                    }
                }

                Assert.Greater(architecturalRounded, 0,
                    "Kentridge shells must author architecture-specific smoothing directly.");
                Assert.Greater(beveled, 0,
                    "Kentridge foundations/details must author their reconstruction policy directly.");
                Assert.Greater(roundedOpenings, 0,
                    "Kentridge door/window cuts must author opening geometry directly.");
                Assert.Greater(smoothRoofs, 0,
                    "Kentridge roof prisms must author roof reconstruction directly.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void KentridgeCombinedCataloguePubHasRearCounterAndOpenFrontAisle()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            SettlementPlan plan = SettlementVoxelPlan.Resolve(Seed, in settings);
            BuildingPlot pubPlot = default;
            bool foundPubPlot = false;
            for (int i = 0; i < plan.Plots.Count; i++)
            {
                if (plan.Plots[i].RoleId != (int)KentridgeRole.Pub) continue;
                pubPlot = plan.Plots[i];
                foundPubPlot = true;
                break;
            }
            Assert.IsTrue(foundPubPlot, "Kentridge settlement must contain its stable Pub role.");

            StructureIntent intent = KentridgeDefinition.StructureIntent(pubPlot);
            StructureForm form = ArchitectureCompiler.Resolve(intent, plan.Theme, Seed);
            int scale = settings.VoxelsPerDecimetre;
            Int3 envelopeDm = SettlementFootprints.For(plan, pubPlot.Archetype);
            int x0 = (envelopeDm.X * scale - form.WidthDm * scale) / 2;
            int z0 = 10 * scale;
            int foundation = plan.Theme.FoundationHeightDm * scale;
            int wall = plan.Theme.WallThicknessDm * scale;
            int counterWidth = System.Math.Min(
                64 * scale,
                form.WidthDm * scale - 2 * (8 * scale + wall));
            int counterDepth = 6 * scale;
            int counterHeight = 9 * scale;
            int usableInteriorDepth = form.DepthDm * scale - wall;
            int gatheringDepth = (usableInteriorDepth * 2) / 3;
            int counterX = x0 + (form.WidthDm * scale - counterWidth) / 2;
            int counterZ = z0 + gatheringDepth + 6 * scale;
            byte timber = settings.Materials.Resolve(plan.Theme.Frame);

            FeatureCatalogue catalogue = KentridgeCombinedVoxelCatalogue.Build(
                Seed, settings, Allocator.Temp);
            try
            {
                bool foundBase = false;
                bool foundTop = false;
                for (int definitionIndex = 0;
                     definitionIndex < catalogue.Definitions.Length;
                     definitionIndex++)
                {
                    FeatureDefinition definition = catalogue.Definitions[definitionIndex];
                    if (definition.Name.ToString() != "kentridge-role-pub") continue;

                    int pc = definition.ProgramOffset;
                    int end = definition.ProgramOffset + definition.ProgramLength;
                    while (pc < end)
                    {
                        ShapeOp op = (ShapeOp)catalogue.Program[pc];
                        int length = ShapeOps.InstructionLength(op);
                        Assert.GreaterOrEqual(length, 2, definition.Name.ToString());

                        if (op == ShapeOp.EmitBox
                            && (PrimitiveMode)catalogue.Program[pc + 11] == PrimitiveMode.Fill
                            && catalogue.Program[pc + 8] == timber)
                        {
                            int x = catalogue.Program[pc + 2];
                            int y = catalogue.Program[pc + 3];
                            int z = catalogue.Program[pc + 4];
                            int sx = catalogue.Program[pc + 5];
                            int sy = catalogue.Program[pc + 6];
                            int sz = catalogue.Program[pc + 7];

                            if (x == counterX
                                && y == foundation
                                && z == counterZ
                                && sx == counterWidth
                                && sy == counterHeight
                                && sz == counterDepth)
                            {
                                foundBase = true;
                                Assert.Greater(z, z0 + gatheringDepth,
                                    "Pub counter must sit just beyond the semantic gathering strip, not obstruct its entrance approach.");
                                Assert.LessOrEqual(z + sz + 2 * scale, z0 + usableInteriorDepth,
                                    "Pub counter must preserve usable bartender circulation behind the bar.");
                            }

                            if (x == counterX - 2 * scale
                                && y == foundation + counterHeight
                                && z == counterZ - 2 * scale
                                && sx == counterWidth + 4 * scale
                                && sy == 2 * scale
                                && sz == counterDepth + 4 * scale)
                                foundTop = true;
                        }

                        pc += length;
                        if (op == ShapeOp.End) break;
                    }
                    break;
                }

                Assert.IsTrue(foundBase,
                    "Active generated Pub geometry must contain the deterministic timber counter base at the gathering area.");
                Assert.IsTrue(foundTop,
                    "Active generated Pub geometry must contain the counter top rather than a bare wall-side block.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void KentridgeCombinedCatalogueContainsRoundedNamedAndAnonymousArchitecture()
        {
            FeatureCatalogue catalogue = KentridgeCombinedVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                int roundedFill = 0;
                int roundedCarve = 0;
                int architecturalRoundedSurface = 0;
                int beveledSurface = 0;
                int smoothRoofPrism = 0;
                int roundedStructureDefinitions = 0;

                for (int definitionIndex = 0;
                     definitionIndex < catalogue.Definitions.Length;
                     definitionIndex++)
                {
                    FeatureDefinition definition = catalogue.Definitions[definitionIndex];
                    if (!definition.Name.ToString().StartsWith("kentridge-role-"))
                        continue;

                    bool definitionRounded = false;
                    int pc = definition.ProgramOffset;
                    int end = definition.ProgramOffset + definition.ProgramLength;
                    while (pc < end)
                    {
                        ShapeOp op = (ShapeOp)catalogue.Program[pc];
                        int length = ShapeOps.InstructionLength(op);
                        Assert.GreaterOrEqual(length, 2, definition.Name.ToString());

                        if (op == ShapeOp.EmitRoundedBox)
                        {
                            definitionRounded = true;
                            int radius = catalogue.Program[pc + 8];
                            ushort surface = (ushort)catalogue.Program[pc + 10];
                            PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 12];
                            Assert.Greater(radius, 0,
                                $"{definition.Name} emitted a rounded box with no radius.");

                            if (surface == SurfaceStyles.ArchitecturalRounded)
                                architecturalRoundedSurface++;
                            if (surface == SurfaceStyles.Beveled) beveledSurface++;

                            if (mode == PrimitiveMode.Carve) roundedCarve++;
                            else if (mode == PrimitiveMode.Fill || mode == PrimitiveMode.FillIfEmpty)
                                roundedFill++;
                        }
                        else if (op == ShapeOp.EmitPrism)
                        {
                            ushort surface = (ushort)catalogue.Program[pc + 10];
                            if (surface == SurfaceStyles.Smooth) smoothRoofPrism++;
                        }

                        pc += length;
                        if (op == ShapeOp.End) break;
                    }

                    if (definitionRounded) roundedStructureDefinitions++;
                }

                Assert.Greater(roundedStructureDefinitions, 0,
                    "Kentridge's active structure stage must consume smooth geometry profiles.");
                Assert.Greater(roundedFill, 0,
                    "Primary/detail structure solids should realise as rounded geometry.");
                Assert.Greater(roundedCarve, 0,
                    "Door/window openings should consume their independent rounding control.");
                Assert.Greater(architecturalRoundedSurface, 0,
                    "Shell/opening geometry should request the architecture-specific smooth reconstruction.");
                Assert.Greater(beveledSurface, 0,
                    "Foundation/detail geometry should explicitly request beveled reconstruction.");
                Assert.Greater(smoothRoofPrism, 0,
                    "Active Kentridge roof prisms should consume the style's roof reconstruction policy.");

                int roundedFabricDefinitions = 0;
                int fabricArchitecturalRounded = 0;
                int fabricRoundedOpenings = 0;
                int fabricSmoothRoofs = 0;
                for (int definitionIndex = 0;
                     definitionIndex < catalogue.Definitions.Length;
                     definitionIndex++)
                {
                    FeatureDefinition definition = catalogue.Definitions[definitionIndex];
                    if (!definition.Name.ToString().StartsWith("kentridge-fabric-"))
                        continue;

                    bool definitionRounded = false;
                    int pc = definition.ProgramOffset;
                    int end = definition.ProgramOffset + definition.ProgramLength;
                    while (pc < end)
                    {
                        ShapeOp op = (ShapeOp)catalogue.Program[pc];
                        int length = ShapeOps.InstructionLength(op);
                        Assert.GreaterOrEqual(length, 2, definition.Name.ToString());

                        if (op == ShapeOp.EmitRoundedBox)
                        {
                            definitionRounded = true;
                            ushort surface = (ushort)catalogue.Program[pc + 10];
                            PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 12];
                            if (surface == SurfaceStyles.ArchitecturalRounded)
                                fabricArchitecturalRounded++;
                            if (mode == PrimitiveMode.Carve
                                && surface == SurfaceStyles.ArchitecturalRounded)
                                fabricRoundedOpenings++;
                        }
                        else if (op == ShapeOp.EmitPrism)
                        {
                            ushort surface = (ushort)catalogue.Program[pc + 10];
                            if (surface == SurfaceStyles.Smooth) fabricSmoothRoofs++;
                        }

                        pc += length;
                        if (op == ShapeOp.End) break;
                    }

                    if (definitionRounded) roundedFabricDefinitions++;
                }

                Assert.Greater(roundedFabricDefinitions, 0,
                    "Anonymous Kentridge frontage must consume low-level geometry profiles too.");
                Assert.Greater(fabricArchitecturalRounded, 0,
                    "Anonymous shells should request architecture-specific smooth reconstruction.");
                Assert.Greater(fabricRoundedOpenings, 0,
                    "Anonymous door/window cuts should receive independent opening rounding.");
                Assert.Greater(fabricSmoothRoofs, 0,
                    "Anonymous roof prisms should receive the city style's roof treatment.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            // Match the showcase alias: foundation stone and wall masonry are allowed to share one
            // palette slot. Semantic authoring must keep their geometry profiles independent anyway.
            var materials = new VoxelMaterialMap(
                foundationStone: 1, masonry: 1, darkMasonry: 6,
                timber: 2, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
