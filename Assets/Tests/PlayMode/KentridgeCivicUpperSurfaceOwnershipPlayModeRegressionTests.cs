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

        [Test]
        public void SceneIssue20260826132234356CivicUpperWestJoinReclaimsMismatchAsDirt()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            FeatureCatalogue terraces = KentridgeDistrictTerraceCatalogue.Build(
                Seed, settings, Allocator.Temp);
            FeatureCatalogue corrections = KentridgeTerraceSurfaceCorrectionCatalogue.Build(
                Seed, settings, Allocator.Temp);

            try
            {
                int upperTerraceIndex = FindIndex(
                    terraces, "kentridge-district-terrace-upper-shoulder");
                int civicTerraceIndex = FindIndex(
                    terraces, "kentridge-district-terrace-civic-summit");
                int upperCorrectionIndex = FindIndex(
                    corrections, "kentridge-terrace-surface-upper-shoulder");

                FeatureDefinition upperTerrace = terraces.Definitions[upperTerraceIndex];
                FeatureDefinition civicTerrace = terraces.Definitions[civicTerraceIndex];
                FeatureDefinition upperCorrection = corrections.Definitions[upperCorrectionIndex];

                const byte dirt = 13;
                const byte paving = 6;
                const int overlapWorldZ = 260;
                int[] mismatchWorldX = { 830, 840 };

                for (int i = 0; i < mismatchWorldX.Length; i++)
                {
                    int worldX = mismatchWorldX[i];
                    Assert.AreEqual(dirt, SurfaceMaterialAtWorld(
                        terraces, upperTerraceIndex, upperTerrace, worldX, overlapWorldZ),
                        "The underlying upper terrace must own the 82.8-84.8 m west mismatch as Dirt.");
                    Assert.AreEqual(0, SurfaceMaterialAtWorld(
                        terraces, civicTerraceIndex, civicTerrace, worldX, overlapWorldZ),
                        "The civic terrace must still begin at its authored 84.8 m west envelope.");
                    Assert.AreEqual(dirt, SurfaceMaterialAtWorld(
                        corrections, upperCorrectionIndex, upperCorrection,
                        worldX, overlapWorldZ),
                        "The higher-precedence correction must reclaim the exposed mismatch as Dirt.");
                }

                Assert.AreEqual(0, SurfaceMaterialAtWorld(
                    corrections, upperCorrectionIndex, upperCorrection, 850, overlapWorldZ),
                    "The repair must stop at the civic envelope rather than widening the whole shoulder.");
                Assert.AreEqual(3, upperCorrection.MaxPrimitives,
                    "The localized Dirt repair must fit the existing bounded correction budget.");
                Assert.AreEqual(paving, SurfaceMaterialAtWorld(
                    corrections, upperCorrectionIndex, upperCorrection, 950, 300),
                    "The correction must still repair the upper built core surface.");
            }
            finally
            {
                corrections.Dispose();
                terraces.Dispose();
            }
        }

        private static byte SurfaceMaterialAtWorld(
            FeatureCatalogue catalogue, int definitionIndex, FeatureDefinition target,
            int worldX, int worldZ)
        {
            int x = worldX - catalogue.ExplicitPlacements[definitionIndex].Position.x;
            int z = worldZ - catalogue.ExplicitPlacements[definitionIndex].Position.z;
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
