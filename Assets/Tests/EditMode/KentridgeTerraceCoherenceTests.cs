using System;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeTerraceCoherenceTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void LowerTownTerraceShouldersUseContinuousGrades()
        {
            FeatureCatalogue catalogue = KentridgeDistrictTerraceCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                AssertDefinitionContainsRamp(catalogue,
                    "kentridge-district-terrace-lower-residential-main");
                AssertDefinitionContainsRamp(catalogue,
                    "kentridge-district-terrace-lower-residential-east");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static void AssertDefinitionContainsRamp(
            FeatureCatalogue catalogue,
            string expectedName)
        {
            for (int i = 0; i < catalogue.Definitions.Length; i++)
            {
                FeatureDefinition definition = catalogue.Definitions[i];
                if (definition.Name.ToString() != expectedName) continue;

                int ramps = CountRamps(catalogue, definition);
                Assert.Greater(
                    ramps,
                    0,
                    expectedName + " has no continuous shoulder grade. District transitions must "
                    + "not be compiled entirely from discrete box bands that later sidewalk paint "
                    + "can turn into false stair flights.");
                return;
            }

            Assert.Fail("Missing expected Kentridge terrace definition: " + expectedName);
        }

        private static int CountRamps(
            FeatureCatalogue catalogue,
            FeatureDefinition definition)
        {
            int pc = definition.ProgramOffset;
            int end = pc + definition.ProgramLength;
            int ramps = 0;
            while (pc < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                switch (op)
                {
                    case ShapeOp.EmitBox:
                    case ShapeOp.EmitRoundedBox:
                        pc += 12; // op + mask + 10 operands
                        break;
                    case ShapeOp.EmitRamp:
                        ramps++;
                        pc += 13; // op + mask + 11 operands
                        break;
                    case ShapeOp.End:
                        pc += 2;
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Unexpected op while parsing terrace program: " + op);
                }
            }

            Assert.AreEqual(end, pc, definition.Name + " has malformed bytecode length.");
            return ramps;
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
