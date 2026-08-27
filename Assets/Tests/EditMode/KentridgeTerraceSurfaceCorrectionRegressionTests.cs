using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeTerraceSurfaceCorrectionRegressionTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void SceneIssue20260826132234356UrbanCorrectionLeavesTransitionShouldersToTerraceOwner()
        {
            FeatureCatalogue corrections = KentridgeTerraceSurfaceCorrectionCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                FeatureDefinition urban = Find(
                    corrections, "kentridge-terrace-surface-upper-shoulder");
                FeatureDefinition residential = Find(
                    corrections, "kentridge-terrace-surface-lower-residential-main");

                AssertUrbanCoreOnly(corrections, urban);
                AssertResidentialFullPatchStillCorrected(corrections, residential);
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

        private static void AssertUrbanCoreOnly(
            FeatureCatalogue catalogue, FeatureDefinition target)
        {
            const int shoulder = 72;
            const int coreWidth = 310;
            const int coreDepth = 200;

            int coreSolidPaints = 0;
            int coreSurfacePaints = 0;
            int fullFootprintPaints = 0;
            int pc = target.ProgramOffset;
            int end = pc + target.ProgramLength;

            while (pc < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                if (op == ShapeOp.EmitBox)
                {
                    int x = catalogue.Program[pc + 2];
                    int z = catalogue.Program[pc + 4];
                    int sx = catalogue.Program[pc + 5];
                    int sz = catalogue.Program[pc + 7];
                    byte material = (byte)catalogue.Program[pc + 8];
                    PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 11];

                    if (x == 0 && z == 0
                        && sx == target.Footprint.x && sz == target.Footprint.z)
                        fullFootprintPaints++;

                    bool isCore = x == shoulder && z == shoulder
                        && sx == coreWidth && sz == coreDepth;
                    if (isCore && mode == PrimitiveMode.PaintSolid && material == 1)
                        coreSolidPaints++;
                    if (isCore && mode == PrimitiveMode.PaintSurface && material == 6)
                        coreSurfacePaints++;
                }

                pc += ShapeOps.InstructionLength(op);
                if (op == ShapeOp.End)
                    break;
            }

            Assert.AreEqual(0, fullFootprintPaints,
                "Urban correction must not claim the expanded transition rectangle; "
                + "the district terrace and roads own those shoulder surfaces.");
            Assert.AreEqual(1, coreSolidPaints,
                "Urban correction must retain foundation-material repair inside the built core.");
            Assert.AreEqual(1, coreSurfacePaints,
                "Urban correction must retain the paved built-core surface.");
        }

        private static void AssertResidentialFullPatchStillCorrected(
            FeatureCatalogue catalogue, FeatureDefinition target)
        {
            int fullSolidPaints = 0;
            int fullSurfacePaints = 0;
            int pc = target.ProgramOffset;
            int end = pc + target.ProgramLength;

            while (pc < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                if (op == ShapeOp.EmitBox)
                {
                    int x = catalogue.Program[pc + 2];
                    int z = catalogue.Program[pc + 4];
                    int sx = catalogue.Program[pc + 5];
                    int sz = catalogue.Program[pc + 7];
                    byte material = (byte)catalogue.Program[pc + 8];
                    PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 11];

                    bool full = x == 0 && z == 0
                        && sx == target.Footprint.x && sz == target.Footprint.z;
                    if (full && mode == PrimitiveMode.PaintSolid && material == 1)
                        fullSolidPaints++;
                    if (full && mode == PrimitiveMode.PaintSurface && material == 14)
                        fullSurfacePaints++;
                }

                pc += ShapeOps.InstructionLength(op);
                if (op == ShapeOp.End)
                    break;
            }

            Assert.AreEqual(1, fullSolidPaints,
                "Non-urban correction must keep its full-patch solid-material repair.");
            Assert.AreEqual(1, fullSurfacePaints,
                "Non-urban correction must keep its full-patch natural surface repair.");
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
