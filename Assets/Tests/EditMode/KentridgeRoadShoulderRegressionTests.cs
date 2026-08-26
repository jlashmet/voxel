using System;
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
                        $"{definition.Name} must feather the hard Dirt edge with grassy shoulder bands.");
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

        [Test]
        public void SceneIssue20260826132234356RoadsUseVoxelGranularGrassTransitions()
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
                    int roadSurfaceHeight = -1;
                    int grassyShoulderStrips = 0;
                    int firstShoulderHeight = -1;
                    int previousShoulderHeight = -1;
                    int lastShoulderHeight = -1;
                    int widestShoulderStrip = 0;
                    int pc = definition.ProgramOffset;
                    int end = pc + definition.ProgramLength;

                    while (pc < end)
                    {
                        ShapeOp op = (ShapeOp)roads.Program[pc];
                        if (op == ShapeOp.EmitBox)
                        {
                            byte material = (byte)roads.Program[pc + 8];
                            PrimitiveMode mode = (PrimitiveMode)roads.Program[pc + 11];
                            if (mode == PrimitiveMode.Fill && material == 13)
                            {
                                roadSurfaceHeight = roads.Program[pc + 6];
                            }
                            else if (mode == PrimitiveMode.Fill && material == 14)
                            {
                                int stripWidth = Math.Min(
                                    roads.Program[pc + 5],
                                    roads.Program[pc + 7]);
                                int stripHeight = roads.Program[pc + 6];

                                widestShoulderStrip = Math.Max(widestShoulderStrip, stripWidth);
                                if (firstShoulderHeight < 0)
                                    firstShoulderHeight = stripHeight;
                                if (previousShoulderHeight >= 0)
                                {
                                    Assert.GreaterOrEqual(stripHeight, previousShoulderHeight,
                                        $"{definition.Name} shoulder heights must not fall while moving outward.");
                                    Assert.LessOrEqual(stripHeight - previousShoulderHeight, 1,
                                        $"{definition.Name} shoulder height changed by more than one decimetre between adjacent bands.");
                                }

                                previousShoulderHeight = stripHeight;
                                lastShoulderHeight = stripHeight;
                                grassyShoulderStrips++;
                            }
                        }

                        pc += ShapeOps.InstructionLength(op);
                        if (op == ShapeOp.End)
                            break;
                    }

                    Assert.Greater(roadSurfaceHeight, 0,
                        $"{definition.Name} did not emit a Dirt carriageway box.");
                    Assert.AreEqual(60, grassyShoulderStrips,
                        $"{definition.Name} should use thirty one-decimetre grass bands per side.");
                    Assert.AreEqual(1, widestShoulderStrip,
                        $"{definition.Name} shoulder bands must be one decimetre wide at the regression scale.");
                    Assert.AreEqual(roadSurfaceHeight, firstShoulderHeight,
                        $"{definition.Name} first grass band must start flush with the Dirt carriageway.");
                    Assert.AreEqual(20, lastShoulderHeight - roadSurfaceHeight,
                        $"{definition.Name} must preserve the existing two-metre outer shoulder rise.");
                    Assert.GreaterOrEqual(definition.MaxPrimitives, 123,
                        $"{definition.Name} primitive budget must cover the voxel-granular shoulder strips and ramps.");
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
