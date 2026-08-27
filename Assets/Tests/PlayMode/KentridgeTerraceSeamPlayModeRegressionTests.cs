using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeTerraceSeamPlayModeRegressionTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void SceneIssue20260826132234356UpperWestShoulderFollowsLocalTerrain()
        {
            FeatureCatalogue terraces = KentridgeDistrictTerraceCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                int upperIndex = FindIndex(terraces, "kentridge-district-terrace-upper-shoulder");
                int marketIndex = FindIndex(terraces, "kentridge-district-terrace-market-main");
                FeatureDefinition upper = terraces.Definitions[upperIndex];
                FeatureDefinition market = terraces.Definitions[marketIndex];
                int originY = terraces.ExplicitPlacements[upperIndex].Position.y;

                const int shoulder = 72;
                const int coreDepth = 200;
                const int stripDepth = 5;
                const int expectedStrips = coreDepth / stripDepth;
                const int westWorldX = 900 - shoulder;
                const int coreWorldZ = 240;
                int strips = 0;

                int pc = upper.ProgramOffset;
                int end = pc + upper.ProgramLength;
                while (pc < end)
                {
                    ShapeOp op = (ShapeOp)terraces.Program[pc];
                    if (op == ShapeOp.EmitBox)
                    {
                        int x = terraces.Program[pc + 2];
                        int z = terraces.Program[pc + 4];
                        int sx = terraces.Program[pc + 5];
                        int sz = terraces.Program[pc + 7];
                        PrimitiveMode mode = (PrimitiveMode)terraces.Program[pc + 11];
                        if (mode == PrimitiveMode.Carve
                            && x == 0 && sx == shoulder
                            && z >= shoulder && z < shoulder + coreDepth
                            && sz <= stripDepth)
                        {
                            Assert.AreEqual(shoulder + strips * stripDepth, z,
                                "West-edge strips must cover the transition contiguously.");
                            Assert.AreEqual(stripDepth, sz,
                                "No west-edge ownership step may exceed 0.5 m along z.");

                            int sampleWorldZ = coreWorldZ + (z - shoulder) + sz / 2;
                            int expectedOuterY = TerrainQuery.HeightAt(
                                westWorldX, sampleWorldZ, Seed);
                            AssertRampOuterHeight(
                                terraces, upper, z, sz, originY, expectedOuterY);
                            strips++;
                        }
                    }

                    pc += ShapeOps.InstructionLength(op);
                    if (op == ShapeOp.End)
                        break;
                }

                Assert.AreEqual(expectedStrips, strips,
                    "upper-shoulder west edge must follow local terrain across all 20 m.");
                Assert.AreEqual(96, upper.MaxPrimitives,
                    "Only the profiled terrace needs the expanded bounded budget.");
                Assert.AreEqual(40, market.MaxPrimitives,
                    "Unrelated terraces must retain the standard primitive budget.");
            }
            finally
            {
                terraces.Dispose();
            }
        }

        private static void AssertRampOuterHeight(
            FeatureCatalogue catalogue, FeatureDefinition target,
            int z, int depth, int originY, int expectedOuterY)
        {
            int pc = target.ProgramOffset;
            int end = pc + target.ProgramLength;
            while (pc < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                if (op == ShapeOp.EmitRamp)
                {
                    int x = catalogue.Program[pc + 2];
                    int y = catalogue.Program[pc + 3];
                    int rampZ = catalogue.Program[pc + 4];
                    int sx = catalogue.Program[pc + 5];
                    int sy = catalogue.Program[pc + 6];
                    int sz = catalogue.Program[pc + 7];
                    byte axis = (byte)catalogue.Program[pc + 8];
                    byte material = (byte)catalogue.Program[pc + 9];
                    PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 12];
                    if (x == 0 && sx == 72 && rampZ == z && sz == depth
                        && (axis & ~ShapeOps.ReverseRampBit) == 0
                        && material == 13 && mode == PrimitiveMode.Fill)
                    {
                        int localOuterY = (axis & ShapeOps.ReverseRampBit) != 0
                            ? y + sy
                            : y;
                        Assert.AreEqual(expectedOuterY, originY + localOuterY,
                            "Each west ramp must meet TerrainQuery at its local sample.");
                        return;
                    }
                }

                pc += ShapeOps.InstructionLength(op);
                if (op == ShapeOp.End)
                    break;
            }

            Assert.Fail("Expected a west-edge ramp for z=" + z + ".");
        }

        private static int FindIndex(FeatureCatalogue catalogue, string name)
        {
            for (int i = 0; i < catalogue.Definitions.Length; i++)
            {
                if (catalogue.Definitions[i].Name.ToString() == name)
                    return i;
            }

            Assert.Fail("Catalogue did not emit " + name + ".");
            return -1;
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
