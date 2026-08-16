using System.IO;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
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
                shellSurface: StructureSurfaceTreatment.Rounded,
                openingSurface: StructureSurfaceTreatment.Smooth,
                detailSurface: StructureSurfaceTreatment.Planar,
                roofSurface: StructureSurfaceTreatment.MasonryJoint);

            Assert.AreEqual(1, profile.FoundationCornerRadiusDm);
            Assert.AreEqual(2, profile.ShellCornerRadiusDm);
            Assert.AreEqual(3, profile.OpeningCornerRadiusDm);
            Assert.AreEqual(4, profile.DetailCornerRadiusDm);
            Assert.AreEqual(StructureSurfaceTreatment.Beveled, profile.FoundationSurface);
            Assert.AreEqual(StructureSurfaceTreatment.Rounded, profile.ShellSurface);
            Assert.AreEqual(StructureSurfaceTreatment.Smooth, profile.OpeningSurface);
            Assert.AreEqual(StructureSurfaceTreatment.Planar, profile.DetailSurface);
            Assert.AreEqual(StructureSurfaceTreatment.MasonryJoint, profile.RoofSurface);
            Assert.IsTrue(profile.HasRoundedGeometry);
            Assert.IsTrue(profile.HasSurfaceOverrides);
            Assert.IsTrue(profile.RequiresRealization);
            Assert.IsFalse(StructureGeometryProfile.Sharp.RequiresRealization);
        }

        [Test]
        public void ArchitectureShapeBuilderAppliesSemanticOpeningAndRoofPolicies()
        {
            var profile = new StructureGeometryProfile(
                foundationCornerRadiusDm: 0,
                shellCornerRadiusDm: 0,
                openingCornerRadiusDm: 2,
                detailCornerRadiusDm: 0,
                openingSurface: StructureSurfaceTreatment.Rounded,
                roofSurface: StructureSurfaceTreatment.Smooth);
            var builder = new ArchitectureShapeProgramBuilder(profile, voxelsPerDecimetre: 1);

            builder.OpeningCarve(2, 3, 4, 8, 12, 4);
            builder.Prism(0, 20, 0, 30, 8, 24, PrismProfile.Gable, material: 7);
            int[] code = builder.Finish();

            Assert.AreEqual(ShapeOp.EmitRoundedBox, (ShapeOp)code[0]);
            Assert.AreEqual(2, code[8], "Opening rounding should come from OpeningCornerRadiusDm.");
            Assert.AreEqual(SurfaceStyles.Rounded, (ushort)code[10]);
            Assert.AreEqual(PrimitiveMode.Carve, (PrimitiveMode)code[12]);

            int prism = ShapeOps.InstructionLength(ShapeOp.EmitRoundedBox);
            Assert.AreEqual(ShapeOp.EmitPrism, (ShapeOp)code[prism]);
            Assert.AreEqual(SurfaceStyles.Smooth, (ushort)code[prism + 10],
                "Roof prisms should use the profile's RoofSurface by default.");
        }

        [Test]
        public void KentridgeCombinedCatalogueContainsRoundedMassingOpeningsAndExplicitSurfaceStyles()
        {
            FeatureCatalogue catalogue = KentridgeCombinedVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                int roundedFill = 0;
                int roundedCarve = 0;
                int roundedSurface = 0;
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

                            if (surface == SurfaceStyles.Rounded) roundedSurface++;
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
                Assert.Greater(roundedSurface, 0,
                    "Shell/opening geometry should explicitly request rounded reconstruction.");
                Assert.Greater(beveledSurface, 0,
                    "Foundation/detail geometry should explicitly request beveled reconstruction.");
                Assert.Greater(smoothRoofPrism, 0,
                    "Active Kentridge roof prisms should consume the style's roof reconstruction policy.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void GenericGeometryRealizerDoesNotReferenceKentridgeContent()
        {
            string root = FindRepoRoot();
            string path = Path.Combine(
                root,
                "Packages",
                "com.mountingforce.worldgen",
                "Runtime",
                "Voxel",
                "ArchitectureGeometryCatalogue.cs");
            string source = File.ReadAllText(path);

            StringAssert.DoesNotContain("Content.Kentridge", source);
            StringAssert.DoesNotContain("KentridgeDefinition", source);
            StringAssert.DoesNotContain("KentridgeRole", source);
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            // Match the showcase alias: foundation stone and wall masonry are allowed to share one
            // palette slot. Geometry-role resolution must still keep their profiles independent.
            var materials = new VoxelMaterialMap(
                foundationStone: 1, masonry: 1, darkMasonry: 6,
                timber: 2, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }

        private static string FindRepoRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Packages")))
                directory = directory.Parent;
            Assert.NotNull(directory, "Could not locate project root containing Packages/.");
            return directory.FullName;
        }
    }
}
