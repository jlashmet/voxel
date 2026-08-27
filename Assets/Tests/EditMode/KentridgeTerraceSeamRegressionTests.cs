using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

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

        private static FeatureDefinition Find(FeatureCatalogue catalogue, string name)
        {
            for (int i = 0; i < catalogue.Definitions.Length; i++)
            {
                FeatureDefinition definition = catalogue.Definitions[i];
                if (definition.Name.ToString() == name)
                    return definition;
            }

            Assert.Fail("Surface-correction catalogue did not emit " + name + ".");
            return default;
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
