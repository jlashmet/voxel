using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeCivicUpperSurfaceOwnershipPlayModeRegressionTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void SceneIssue20260826132234356CivicSouthWestShoulderFollowsLocalTerrainProfile()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            FeatureCatalogue production = KentridgeCombinedVoxelCatalogue.Build(
                Seed, settings, Allocator.Temp);

            try
            {
                int civicIndex = FindIndex(
                    production, "kentridge-terrace-surface-civic-summit");
                int upperIndex = FindIndex(
                    production, "kentridge-terrace-surface-upper-shoulder");
                int courtIndex = FindIndex(
                    production, "kentridge-civic-west-block-court");
                FeatureDefinition civic = production.Definitions[civicIndex];
                FeatureDefinition upper = production.Definitions[upperIndex];
                FeatureDefinition court = production.Definitions[courtIndex];

                Assert.AreEqual(18, civic.MaxPrimitives,
                    "The localized civic corner repair must stay within its bounded generation budget.");

                // Saved-camera ray projection at local natural height puts the marked upper circle
                // across roughly X=91.0..93.8m, Z=28.6..30.4m. The first six samples cover the
                // original west shoulder; the final two prove the production repair continues east
                // of X=92.0m through the marked part of the civic south edge.
                int[] sampleWorldX = { 854, 866, 878, 890, 902, 914, 926, 938 };
                const int outerSouthWorldZ = 312;
                const int localSouthZ = 272;
                const int stripWidth = 12;
                const int shoulderDepth = 72;

                int rampCount = 0;
                for (int i = 0; i < sampleWorldX.Length; i++)
                {
                    int expectedEdgeY = TerrainQuery.HeightAt(
                        sampleWorldX[i], outerSouthWorldZ, Seed);
                    int actualEdgeY = CivicRampOuterEdgeYAtWorldX(
                        production, civicIndex, civic,
                        sampleWorldX[i], localSouthZ, stripWidth, shoulderDepth,
                        ref rampCount);

                    Assert.AreEqual(expectedEdgeY, actualEdgeY,
                        "Each marked-corner strip must meet locally sampled natural terrain instead of reusing the civic centreline edge height.");
                }

                Assert.AreEqual(sampleWorldX.Length, rampCount,
                    "The 9.6m marked envelope should compile to eight 1.2m locally sampled ramp strips.");

                // The surviving upper mark intersects the civic-west court at X=92.8..93.8m,
                // Z=28.6..29.8m. The court is later (precedence 85) than the terrace repair, so a
                // solid court primitive would recreate the rectangular shelf even when the ramp is
                // correct. The final production catalogue must keep court material ownership while
                // leaving solid height to the district terrace beneath it.
                Assert.AreEqual(KentridgeUrbanCourtCatalogue.CourtPrecedence, court.Precedence);
                Assert.AreEqual(2, court.MaxPrimitives,
                    "Changing court ownership must not increase its generation budget.");
                AssertSurfaceOnlyAtWorld(production, courtIndex, court, 934, 290);

                Assert.AreEqual(0, SurfaceMaterialAtWorld(
                    production, upperIndex, upper, 840, 260),
                    "The superseded upper-patch material repaint must not remain after the geometric repair.");
                Assert.AreEqual(6, SurfaceMaterialAtWorld(
                    production, civicIndex, civic, 950, 150),
                    "The civic correction must continue to preserve the built core paving.");
            }
            finally
            {
                production.Dispose();
            }
        }

        private static int CivicRampOuterEdgeYAtWorldX(
            FeatureCatalogue catalogue, int definitionIndex, FeatureDefinition target,
            int worldX, int expectedLocalZ, int expectedStripWidth, int expectedDepth,
            ref int rampCount)
        {
            ExplicitPlacement placement = PlacementFor(catalogue, definitionIndex);
            int localX = worldX - placement.Position.x;
            int pc = target.ProgramOffset;
            int end = pc + target.ProgramLength;
            while (pc < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                if (op == ShapeOp.EmitRamp)
                {
                    int x = catalogue.Program[pc + 2];
                    int y = catalogue.Program[pc + 3];
                    int z = catalogue.Program[pc + 4];
                    int sx = catalogue.Program[pc + 5];
                    int sy = catalogue.Program[pc + 6];
                    int sz = catalogue.Program[pc + 7];
                    byte axis = (byte)catalogue.Program[pc + 8];
                    byte material = (byte)catalogue.Program[pc + 9];
                    PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 12];

                    if (z == expectedLocalZ
                        && sx == expectedStripWidth
                        && sz == expectedDepth
                        && localX >= x && localX < x + sx)
                    {
                        Assert.AreEqual(13, material,
                            "The repaired shoulder ramp must remain Dirt rather than synthesize Moss.");
                        Assert.AreEqual(PrimitiveMode.Fill, mode);
                        Assert.AreEqual(2, axis & 0x03,
                            "The localized repair must slope along the civic south Z axis.");
                        rampCount++;

                        bool highAtNegative = (axis & ShapeOps.ReverseRampBit) != 0;
                        int localOuterY = highAtNegative ? y : y + sy;
                        return placement.Position.y + localOuterY;
                    }
                }

                pc += ShapeOps.InstructionLength(op);
                if (op == ShapeOp.End)
                    break;
            }

            Assert.Fail("No localized civic south-west ramp covered world X=" + worldX + ".");
            return int.MinValue;
        }

        private static void AssertSurfaceOnlyAtWorld(
            FeatureCatalogue catalogue, int definitionIndex, FeatureDefinition target,
            int worldX, int worldZ)
        {
            ExplicitPlacement placement = PlacementFor(catalogue, definitionIndex);
            int x = worldX - placement.Position.x;
            int z = worldZ - placement.Position.z;
            Assert.That(x, Is.GreaterThanOrEqualTo(0).And.LessThan(target.Footprint.x));
            Assert.That(z, Is.GreaterThanOrEqualTo(0).And.LessThan(target.Footprint.z));

            bool paintsMarkedSurface = false;
            int pc = target.ProgramOffset;
            int end = pc + target.ProgramLength;
            while (pc < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                if (op == ShapeOp.EmitBox)
                {
                    int bx = catalogue.Program[pc + 2];
                    int bz = catalogue.Program[pc + 4];
                    int sx = catalogue.Program[pc + 5];
                    int sz = catalogue.Program[pc + 7];
                    PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 11];
                    if (x >= bx && x < bx + sx && z >= bz && z < bz + sz)
                    {
                        Assert.AreNotEqual(PrimitiveMode.Fill, mode,
                            "A late solid court primitive would recreate the raised rectangular tongue over the repaired shoulder.");
                        if (mode == PrimitiveMode.PaintSurface)
                            paintsMarkedSurface = true;
                    }
                }

                pc += ShapeOps.InstructionLength(op);
                if (op == ShapeOp.End)
                    break;
            }

            Assert.IsTrue(paintsMarkedSurface,
                "The civic-west court should still own the marked surface material without owning its height.");
        }

        private static byte SurfaceMaterialAtWorld(
            FeatureCatalogue catalogue, int definitionIndex, FeatureDefinition target,
            int worldX, int worldZ)
        {
            ExplicitPlacement placement = PlacementFor(catalogue, definitionIndex);
            int x = worldX - placement.Position.x;
            int z = worldZ - placement.Position.z;
            if (x < 0 || z < 0 || x >= target.Footprint.x || z >= target.Footprint.z)
                return 0;

            byte material = 0;
            int pc = target.ProgramOffset;
            int end = pc + target.ProgramLength;
            while (pc < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                if (op == ShapeOp.EmitBox)
                {
                    int bx = catalogue.Program[pc + 2];
                    int bz = catalogue.Program[pc + 4];
                    int sx = catalogue.Program[pc + 5];
                    int sz = catalogue.Program[pc + 7];
                    byte candidate = (byte)catalogue.Program[pc + 8];
                    PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 11];
                    if (mode == PrimitiveMode.PaintSurface
                        && x >= bx && x < bx + sx
                        && z >= bz && z < bz + sz)
                        material = candidate;
                }

                pc += ShapeOps.InstructionLength(op);
                if (op == ShapeOp.End)
                    break;
            }

            return material;
        }

        private static ExplicitPlacement PlacementFor(
            FeatureCatalogue catalogue, int definitionIndex)
        {
            for (int i = 0; i < catalogue.Rules.Length; i++)
            {
                PlacementRule rule = catalogue.Rules[i];
                if (rule.DefinitionId != definitionIndex || rule.ExplicitCount <= 0)
                    continue;
                Assert.That(rule.ExplicitOffset,
                    Is.GreaterThanOrEqualTo(0).And.LessThan(catalogue.ExplicitPlacements.Length));
                return catalogue.ExplicitPlacements[rule.ExplicitOffset];
            }

            Assert.Fail("No explicit placement mapped to definition index " + definitionIndex + ".");
            return default;
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
