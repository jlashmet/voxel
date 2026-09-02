using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeTerraceSurfaceCorrectionTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void MarketMainUrbanShoulderUsesRoadSurfaceInsteadOfMoss()
        {
            FeatureCatalogue catalogue = KentridgeTerraceSurfaceCorrectionCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                int definitionIndex = FindDefinition(catalogue, "kentridge-terrace-surface-market-main");
                Assert.That(definitionIndex, Is.GreaterThanOrEqualTo(0),
                    "The market-main correction fixture must remain part of the Kentridge catalogue.");

                FeatureDefinition definition = catalogue.Definitions[definitionIndex];
                int pc = definition.ProgramOffset;

                Assert.AreEqual(ShapeOp.EmitBox, (ShapeOp)catalogue.Program[pc]);
                Assert.AreEqual(PrimitiveMode.PaintSolid, (PrimitiveMode)catalogue.Program[pc + 11]);
                pc += ShapeOps.InstructionLength(ShapeOp.EmitBox);

                Assert.AreEqual(ShapeOp.EmitBox, (ShapeOp)catalogue.Program[pc]);
                Assert.AreEqual(PrimitiveMode.PaintSurface, (PrimitiveMode)catalogue.Program[pc + 11]);
                Assert.AreEqual(13, (byte)catalogue.Program[pc + 8],
                    "The full market-main terrace footprint includes the broad stepped shoulder; " +
                    "its correction must preserve the authored urban RoadSurface material rather than repainting it Moss.");

                pc += ShapeOps.InstructionLength(ShapeOp.EmitBox);
                Assert.AreEqual(ShapeOp.EmitBox, (ShapeOp)catalogue.Program[pc]);
                Assert.AreEqual(PrimitiveMode.PaintSurface, (PrimitiveMode)catalogue.Program[pc + 11]);
                Assert.AreEqual(6, (byte)catalogue.Program[pc + 8],
                    "The market-main core should still be reasserted as DarkMasonry after the shoulder correction.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static int FindDefinition(FeatureCatalogue catalogue, string name)
        {
            for (int i = 0; i < catalogue.Definitions.Length; i++)
            {
                if (catalogue.Definitions[i].Name.ToString() == name)
                    return i;
            }
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
