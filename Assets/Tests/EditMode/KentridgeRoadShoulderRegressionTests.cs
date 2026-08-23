using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeRoadShoulderRegressionTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void SceneIssue20260823013924433RoadsFeatherDirtWithGrassyShoulders()
        {
            FeatureCatalogue roads = KentridgeTownSurfaceCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                int roadDefinitions = 0;

                for (int i = 0; i < roads.Definitions.Length; i++)
                {
                    FeatureDefinition definition = roads.Definitions[i];
                    if (!definition.Name.ToString().StartsWith("kentridge-road-"))
                        continue;

                    roadDefinitions++;
                    int roadSurfaceStrips = 0;
                    int grassyShoulderStrips = 0;
                    int carveStrips = 0;
                    int pc = definition.ProgramOffset;
                    int end = pc + definition.ProgramLength;

                    while (pc < end)
                    {
                        ShapeOp op = (ShapeOp)roads.Program[pc];
                        if (op == ShapeOp.EmitBox)
                        {
                            byte material = (byte)roads.Program[pc + 8];
                            PrimitiveMode mode = (PrimitiveMode)roads.Program[pc + 11];
                            if (mode == PrimitiveMode.Carve)
                                carveStrips++;
                            else if (mode == PrimitiveMode.Fill && material == 13)
                                roadSurfaceStrips++;
                            else if (mode == PrimitiveMode.Fill && material == 14)
                                grassyShoulderStrips++;
                        }

                        pc += ShapeOps.InstructionLength(op);
                        if (op == ShapeOp.End)
                            break;
                    }

                    Assert.AreEqual(1, carveStrips,
                        $"{definition.Name} should cut one graded corridor before filling it back.");
                    Assert.AreEqual(1, roadSurfaceStrips,
                        $"{definition.Name} should retain one Dirt carriageway core.");
                    Assert.GreaterOrEqual(grassyShoulderStrips, 10,
                        $"{definition.Name} must feather the hard Dirt edge with at least five grassy bands per side.");
                    Assert.GreaterOrEqual(definition.MaxPrimitives, 12,
                        $"{definition.Name} primitive budget must account for its carriageway and shoulder strips.");
                }

                Assert.Greater(roadDefinitions, 0,
                    "The Kentridge surface catalogue emitted no road definitions.");
            }
            finally
            {
                roads.Dispose();
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
