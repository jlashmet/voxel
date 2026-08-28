using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeCivicUpperSurfaceOwnershipPlayModeRegressionTests
    {
        private const uint Seed = 0x4B454E54u;
        private const int CivicWestEdgeXDm = 848;
        private const int CivicCoreStartZDm = 40;
        private const int CivicShoulderDm = 72;
        private const int ProfileStepDm = 5;

        [Test]
        public void SceneIssue20260826132234356CivicWestShoulderFollowsLocalTerrainAlongBothMarkedRegions()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            FeatureCatalogue production = KentridgeCombinedVoxelCatalogue.Build(
                Seed, settings, Allocator.Temp);

            try
            {
                int civicIndex = FindIndex(
                    production, "kentridge-district-terrace-civic-summit");
                FeatureDefinition civic = production.Definitions[civicIndex];
                ExplicitPlacement placement = PlacementFor(production, civicIndex);

                Assert.AreEqual(96, civic.MaxPrimitives,
                    "Profiling the civic west shoulder must stay inside the existing bounded profiled-terrace budget.");

                // The immutable capture records two world hits on the same civic-summit west transition:
                // upper ~= (X 92.15m, Z 11.57m), lower ~= (X 89.03m, Z 20.91m).
                // The first lies immediately inside the civic core edge; the second lies within the
                // 7.2m west shoulder. Both must therefore be backed by a west-shoulder strip whose
                // outer elevation was sampled near that Z, rather than by the old single Z=14m sample.
                AssertProfiledWestStrip(
                    production, civic, placement,
                    capturedWorldZDm: 116);
                AssertProfiledWestStrip(
                    production, civic, placement,
                    capturedWorldZDm: 209);
            }
            finally
            {
                production.Dispose();
            }
        }

        private static void AssertProfiledWestStrip(
            FeatureCatalogue catalogue,
            FeatureDefinition civic,
            ExplicitPlacement placement,
            int capturedWorldZDm)
        {
            int stripIndex = (capturedWorldZDm - CivicCoreStartZDm) / ProfileStepDm;
            int stripStartWorldZ = CivicCoreStartZDm + stripIndex * ProfileStepDm;
            int stripDepthDm = System.Math.Min(
                ProfileStepDm,
                200 - stripIndex * ProfileStepDm);
            int sampledWorldZ = stripStartWorldZ + stripDepthDm / 2;
            int expectedOuterY = TerrainQuery.HeightAt(
                CivicWestEdgeXDm,
                sampledWorldZ,
                Seed);
            int expectedLocalZ = CivicShoulderDm + stripIndex * ProfileStepDm;

            int pc = civic.ProgramOffset;
            int end = pc + civic.ProgramLength;
            while (pc < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                if (op == ShapeOp.EmitRamp)
                {
                    int y = catalogue.Program[pc + 3];
                    int z = catalogue.Program[pc + 4];
                    int sx = catalogue.Program[pc + 5];
                    int sy = catalogue.Program[pc + 6];
                    int sz = catalogue.Program[pc + 7];
                    byte axis = (byte)catalogue.Program[pc + 8];
                    PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 12];

                    if (z == expectedLocalZ
                        && sx == CivicShoulderDm
                        && sz == stripDepthDm
                        && (axis & 0x03) == 0)
                    {
                        Assert.AreEqual(PrimitiveMode.Fill, mode);
                        bool highAtNegative = (axis & ShapeOps.ReverseRampBit) != 0;
                        int localOuterY = highAtNegative ? y + sy : y;
                        int actualOuterY = placement.Position.y + localOuterY;
                        Assert.AreEqual(expectedOuterY, actualOuterY,
                            "The civic west shoulder strip at captured Z=" + capturedWorldZDm +
                            "dm must meet the locally sampled terrain edge, not a centreline sample reused across the full 20m edge.");
                        return;
                    }
                }

                pc += ShapeOps.InstructionLength(op);
                if (op == ShapeOp.End)
                    break;
            }

            Assert.Fail(
                "No profiled civic west-shoulder ramp covered captured Z=" +
                capturedWorldZDm + "dm.");
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
