using System;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Structures.Runtime.Emitters;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeRoadShoulderRegressionTests
    {
        private const uint Seed = 0x4B454E54u;
        private const int LegacyTerraceStepCount = 6;

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
        public void SceneIssue20260826132234356DistrictTerraceShouldersRasterizeWithoutSixBandPlateaus()
        {
            FeatureCatalogue terraces = KentridgeDistrictTerraceCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                FeatureDefinition target = default;
                bool foundTarget = false;
                for (int i = 0; i < terraces.Definitions.Length; i++)
                {
                    FeatureDefinition definition = terraces.Definitions[i];
                    if (definition.Name.ToString() != "kentridge-district-terrace-upper-shoulder")
                        continue;

                    target = definition;
                    foundTarget = true;
                    break;
                }

                Assert.IsTrue(foundTarget,
                    "The live district-terrace catalogue did not emit the captured upper-shoulder feature.");

                int earthFillBoxes = 0;
                int shoulderRamps = 0;
                int meaningfullySlopedRamps = 0;
                int pc = target.ProgramOffset;
                int end = pc + target.ProgramLength;

                while (pc < end)
                {
                    ShapeOp op = (ShapeOp)terraces.Program[pc];
                    if (op == ShapeOp.EmitBox)
                    {
                        byte material = (byte)terraces.Program[pc + 8];
                        PrimitiveMode mode = (PrimitiveMode)terraces.Program[pc + 11];
                        if (mode == PrimitiveMode.Fill && material == 13)
                            earthFillBoxes++;
                    }
                    else if (op == ShapeOp.EmitRamp)
                    {
                        int x = terraces.Program[pc + 2];
                        int y = terraces.Program[pc + 3];
                        int z = terraces.Program[pc + 4];
                        int sx = terraces.Program[pc + 5];
                        int sy = terraces.Program[pc + 6];
                        int sz = terraces.Program[pc + 7];
                        byte axis = (byte)terraces.Program[pc + 8];
                        byte material = (byte)terraces.Program[pc + 9];
                        PrimitiveMode mode = (PrimitiveMode)terraces.Program[pc + 12];

                        if (mode == PrimitiveMode.Fill && material == 13)
                        {
                            shoulderRamps++;
                            int rampAxis = axis & ShapeOps.RampAxisMask;
                            Assert.IsTrue(rampAxis == 0 || rampAxis == 2,
                                "District terrace shoulder ramps must run along X or Z.");

                            Primitive ramp = BoxEmitter.Ramp(
                                new int3(x, y, z), new int3(sx, sy, sz),
                                axis, material, mode, order: 0);
                            int axisLength = rampAxis == 0 ? sx : sz;
                            int distinctHeights = 0;
                            int longestPlateau = 0;
                            int currentPlateau = 0;
                            int previousTop = int.MinValue;

                            for (int along = 0; along < axisLength; along++)
                            {
                                int top = RasterizedTop(in ramp, rampAxis, along, x, y, z, sx, sy, sz);
                                if (top != previousTop)
                                {
                                    distinctHeights++;
                                    currentPlateau = 1;
                                    previousTop = top;
                                }
                                else
                                {
                                    currentPlateau++;
                                }

                                longestPlateau = Math.Max(longestPlateau, currentPlateau);
                            }

                            if (sy > LegacyTerraceStepCount)
                            {
                                meaningfullySlopedRamps++;
                                Assert.Greater(distinctHeights, LegacyTerraceStepCount,
                                    "A captured terrace shoulder with more than six voxels of rise must rasterize to more than six surface levels.");

                                int expectedLinearPlateau = Math.Max(1, (axisLength + sy - 1) / sy);
                                Assert.LessOrEqual(longestPlateau, expectedLinearPlateau + 1,
                                    "The rasterized shoulder contains a plateau wider than a linear voxel ramp permits.");
                            }
                        }
                    }

                    pc += ShapeOps.InstructionLength(op);
                    if (op == ShapeOp.End)
                        break;
                }

                Assert.AreEqual(1, earthFillBoxes,
                    "The terrace program should retain only its Dirt core box; shoulders must not return to six broad Dirt boxes.");
                Assert.Greater(shoulderRamps, 0,
                    "The captured upper-shoulder feature emitted no authoritative shoulder ramps.");
                Assert.Greater(meaningfullySlopedRamps, 0,
                    "The regression fixture must exercise a shoulder rise large enough to distinguish a ramp from the legacy six-band profile.");
            }
            finally
            {
                terraces.Dispose();
            }
        }

        private static int RasterizedTop(in Primitive ramp, int axis, int along,
                                         int x, int y, int z, int sx, int sy, int sz)
        {
            int vx = axis == 0 ? x + along : x + sx / 2;
            int vz = axis == 2 ? z + along : z + sz / 2;

            for (int vy = y + sy - 1; vy >= y; vy--)
            {
                if (BoxEmitter.RampContains(in ramp, new int3(vx, vy, vz)))
                    return vy;
            }

            return y - 1;
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
