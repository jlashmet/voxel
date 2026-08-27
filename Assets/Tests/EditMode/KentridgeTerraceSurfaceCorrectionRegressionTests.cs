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
        public void SceneIssue20260826132234356UrbanTerraceCorrectionLeavesShouldersGrassy()
        {
            FeatureCatalogue corrections = KentridgeTerraceSurfaceCorrectionCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                FeatureDefinition target = default;
                bool foundTarget = false;
                for (int i = 0; i < corrections.Definitions.Length; i++)
                {
                    FeatureDefinition definition = corrections.Definitions[i];
                    if (definition.Name.ToString() != "kentridge-terrace-surface-upper-shoulder")
                        continue;

                    target = definition;
                    foundTarget = true;
                    break;
                }

                Assert.IsTrue(foundTarget,
                    "The live surface-correction catalogue did not emit the captured upper-shoulder patch.");

                int grassyFootprintPaints = 0;
                int dirtFootprintPaints = 0;
                int pavedCorePaints = 0;
                int pc = target.ProgramOffset;
                int end = pc + target.ProgramLength;

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

                        if (mode == PrimitiveMode.PaintSurface && material == 14)
                        {
                            grassyFootprintPaints++;
                            Assert.AreEqual(0, x);
                            Assert.AreEqual(0, z);
                            Assert.AreEqual(target.Footprint.x, sx);
                            Assert.AreEqual(target.Footprint.z, sz);
                        }
                        else if (mode == PrimitiveMode.PaintSurface && material == 13)
                        {
                            dirtFootprintPaints++;
                        }
                        else if (mode == PrimitiveMode.PaintSurface && material == 6)
                        {
                            pavedCorePaints++;
                        }
                    }

                    pc += ShapeOps.InstructionLength(op);
                    if (op == ShapeOp.End)
                        break;
                }

                Assert.AreEqual(1, grassyFootprintPaints,
                    "The urban terrace correction must restore grass across the transition footprint before repainting its built core.");
                Assert.AreEqual(0, dirtFootprintPaints,
                    "The correction must not turn the whole rectangular urban shoulder into a Dirt apron.");
                Assert.AreEqual(1, pavedCorePaints,
                    "The captured urban terrace must retain its paved core after the grassy shoulder correction.");
            }
            finally
            {
                corrections.Dispose();
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
