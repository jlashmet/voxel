using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeTerraceSeamRegressionTests
    {
        private const uint Seed = 0x4B454E54u;
        private const int MarketShoulderDm = 72;
        private const int BandDm = 2;
        private const int ExpectedBands = MarketShoulderDm / BandDm;
        private const int UpperExpandedWidthDm = 310 + 72 * 2;
        private const int MarketWestToUpperWestDm = 220;
        private const int MarketEastToUpperEastDm = 90;

        [Test]
        public void SceneIssue20260826132234356MarketToUpperDirtEdgeFeathersWithoutRectangularNotch()
        {
            FeatureCatalogue corrections = KentridgeTerraceSurfaceCorrectionCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                FeatureDefinition market = Find(
                    corrections, "kentridge-terrace-surface-market-main");

                int mossBase = 0;
                int dirtBands = 0;
                int previousX = int.MaxValue;
                int previousRightInset = int.MaxValue;
                int previousZ = -BandDm;
                int maxWestStep = 0;
                int maxEastStep = 0;

                int pc = market.ProgramOffset;
                int end = pc + market.ProgramLength;
                while (pc < end)
                {
                    ShapeOp op = (ShapeOp)corrections.Program[pc];
                    if (op == ShapeOp.EmitBox)
                    {
                        int x = corrections.Program[pc + 2];
                        int z = corrections.Program[pc + 4];
                        int sx = corrections.Program[pc + 5];
                        int sz = corrections.Program[pc + 7];
                        byte material = (byte)corrections.Program[pc + 8];
                        PrimitiveMode mode = (PrimitiveMode)corrections.Program[pc + 11];

                        if (mode == PrimitiveMode.PaintSurface
                            && material == 14
                            && x == 0 && z == 0
                            && sx == market.Footprint.x && sz == MarketShoulderDm)
                        {
                            mossBase++;
                        }
                        else if (mode == PrimitiveMode.PaintSurface
                                 && material == 13
                                 && z >= 0 && z < MarketShoulderDm)
                        {
                            int rightInset = market.Footprint.x - x - sx;
                            Assert.AreEqual(previousZ + BandDm, z,
                                "Taper bands must cover the market north shoulder contiguously.");
                            Assert.LessOrEqual(x, previousX,
                                "The Dirt seam must expand westward monotonically toward market-main.");
                            Assert.LessOrEqual(rightInset, previousRightInset,
                                "The Dirt seam must expand eastward monotonically toward market-main.");

                            if (dirtBands > 0)
                            {
                                maxWestStep = System.Math.Max(maxWestStep, previousX - x);
                                maxEastStep = System.Math.Max(
                                    maxEastStep, previousRightInset - rightInset);
                            }

                            if (dirtBands == 0)
                            {
                                Assert.AreEqual(MarketWestToUpperWestDm, x,
                                    "The outer seam must begin on upper-shoulder's west boundary.");
                                Assert.AreEqual(MarketEastToUpperEastDm, rightInset,
                                    "The outer seam must begin on upper-shoulder's east boundary.");
                                Assert.AreEqual(UpperExpandedWidthDm, sx,
                                    "The outer seam must exactly match upper-shoulder's expanded width.");
                            }

                            previousX = x;
                            previousRightInset = rightInset;
                            previousZ = z;
                            dirtBands++;
                        }
                    }

                    pc += ShapeOps.InstructionLength(op);
                    if (op == ShapeOp.End)
                        break;
                }

                Assert.AreEqual(1, mossBase,
                    "The old rectangular market north shoulder must first return to natural surface.");
                Assert.AreEqual(ExpectedBands, dirtBands,
                    "The market-to-upper seam must use the authored 2 dm taper bands.");
                Assert.AreEqual(0, previousX,
                    "The innermost taper band must reach market-main's full west edge.");
                Assert.AreEqual(0, previousRightInset,
                    "The innermost taper band must reach market-main's full east edge.");
                Assert.LessOrEqual(maxWestStep, 7,
                    "West Dirt edge moved by more than 0.7 m in one band; the visible notch is too coarse.");
                Assert.LessOrEqual(maxEastStep, 3,
                    "East Dirt edge moved by more than 0.3 m in one band; the visible notch is too coarse.");
                Assert.GreaterOrEqual(market.MaxPrimitives, 39,
                    "Primitive budget must include core repair, natural reset, and all taper bands.");
            }
            finally
            {
                corrections.Dispose();
            }
        }

        [Test]
        public void SceneIssue20260826132234356UpperWestShoulderFollowsLocalTerrainWithoutMetreScaleSteps()
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
                            int expectedZ = shoulder + strips * stripDepth;
                            Assert.AreEqual(expectedZ, z,
                                "Local west-edge strips must cover the full transition contiguously.");
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
                    "upper-shoulder west edge must follow local natural terrain across all 20 m.");
                Assert.AreEqual(96, upper.MaxPrimitives,
                    "The one profiled terrace needs a bounded budget for its local strips.");
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
                            "Each west-edge ramp must meet TerrainQuery at its local sample.");
                        return;
                    }
                }

                pc += ShapeOps.InstructionLength(op);
                if (op == ShapeOp.End)
                    break;
            }

            Assert.Fail("Expected a west-edge ramp for z=" + z + ".");
        }

        private static FeatureDefinition Find(FeatureCatalogue catalogue, string name)
        {
            return catalogue.Definitions[FindIndex(catalogue, name)];
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
