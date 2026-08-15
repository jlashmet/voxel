using System.IO;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Features;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeShapeProgramEncodingTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void CombinedCatalogueUsesCanonicalShapeInstructionBoundaries()
        {
            FeatureCatalogue catalogue = KentridgeCombinedVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                for (int definitionIndex = 0;
                     definitionIndex < catalogue.Definitions.Length;
                     definitionIndex++)
                {
                    FeatureDefinition definition = catalogue.Definitions[definitionIndex];
                    int pc = definition.ProgramOffset;
                    int end = definition.ProgramOffset + definition.ProgramLength;
                    bool ended = false;

                    while (pc < end)
                    {
                        ShapeOp op = (ShapeOp)catalogue.Program[pc];
                        int instructionLength = ShapeOps.InstructionLength(op);
                        Assert.GreaterOrEqual(
                            instructionLength, 2,
                            $"{definition.Name} has an unknown opcode at {pc}.");
                        Assert.LessOrEqual(
                            pc + instructionLength, end,
                            $"{definition.Name} overruns its declared bytecode boundary.");

                        pc += instructionLength;
                        if (op != ShapeOp.End) continue;
                        ended = true;
                        break;
                    }

                    Assert.IsTrue(ended, $"{definition.Name} contains no canonical End instruction.");
                    Assert.AreEqual(
                        end, pc,
                        $"{definition.Name} reaches End before its declared bytecode boundary.");
                }
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void FoundationSkirtsUseCanonicalBoxThenEndEncoding()
        {
            FeatureCatalogue catalogue = KentridgeCombinedVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                int expectedLength = ShapeOps.InstructionLength(ShapeOp.EmitBox)
                                   + ShapeOps.InstructionLength(ShapeOp.End);
                int found = 0;
                for (int i = 0; i < catalogue.Definitions.Length; i++)
                {
                    FeatureDefinition definition = catalogue.Definitions[i];
                    if (!definition.Name.ToString().StartsWith("kentridge-foundation-skirt-"))
                        continue;

                    found++;
                    Assert.AreEqual(expectedLength, definition.ProgramLength,
                        $"{definition.Name} must contain exactly one canonical EmitBox and End.");
                    Assert.AreEqual(ShapeOp.EmitBox,
                        (ShapeOp)catalogue.Program[definition.ProgramOffset]);
                    Assert.AreEqual(ShapeOp.End,
                        (ShapeOp)catalogue.Program[
                            definition.ProgramOffset + ShapeOps.InstructionLength(ShapeOp.EmitBox)]);
                }

                Assert.Greater(found, 0, "Expected at least one Kentridge foundation skirt definition.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void KentridgeUsesCanonicalEncodingWithoutCompatibilityNormalizer()
        {
            string root = FindRepoRoot();
            string voxelRoot = Path.Combine(
                root, "Packages", "com.mountingforce.worldgen", "Runtime", "Voxel");
            string compatibility = Path.Combine(
                voxelRoot, "KentridgeShapeProgramCompatibility.cs");
            Assert.False(File.Exists(compatibility),
                "Kentridge builders must emit canonical Structures.Api bytecode directly; " +
                "do not restore the compatibility normalizer.");

            string core = File.ReadAllText(Path.Combine(
                voxelRoot, "KentridgeCombinedVoxelCatalogueCanonical.Core.cs"));
            string merge = File.ReadAllText(Path.Combine(
                voxelRoot, "KentridgeCombinedVoxelCatalogueCanonical.Merge.cs"));
            StringAssert.DoesNotContain("KentridgeShapeProgramCompatibility", core);
            StringAssert.DoesNotContain("KentridgeShapeProgramCompatibility", merge);
            StringAssert.Contains("programs += stage.Program.Length", core,
                "Combined allocation must use already-canonical source lengths.");
            StringAssert.Contains("source.Program[definition.ProgramOffset + code]", merge,
                "Combined merge must copy canonical program bytes verbatim.");
        }

        private static string FindRepoRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Packages")))
                directory = directory.Parent;
            Assert.NotNull(directory, "Could not locate project root containing Packages/.");
            return directory.FullName;
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