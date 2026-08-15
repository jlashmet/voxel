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
