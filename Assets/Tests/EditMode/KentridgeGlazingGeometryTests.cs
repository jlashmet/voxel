using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeGlazingGeometryTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void AnonymousFabricUsesRoundedRevealsWithPlanarGlass()
        {
            FeatureCatalogue catalogue = KentridgeUrbanFabricCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                int roundedReveals = 0;
                int planarGlass = 0;

                for (int definitionIndex = 0;
                     definitionIndex < catalogue.Definitions.Length;
                     definitionIndex++)
                {
                    FeatureDefinition definition = catalogue.Definitions[definitionIndex];
                    int pc = definition.ProgramOffset;
                    int end = definition.ProgramOffset + definition.ProgramLength;
                    while (pc < end)
                    {
                        ShapeOp op = (ShapeOp)catalogue.Program[pc];
                        int length = ShapeOps.InstructionLength(op);
                        Assert.GreaterOrEqual(length, 2, definition.Name.ToString());

                        if (op == ShapeOp.EmitRoundedBox)
                        {
                            ushort surface = (ushort)catalogue.Program[pc + 10];
                            PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 12];
                            if (mode == PrimitiveMode.Carve
                                && surface == SurfaceStyles.ArchitecturalRounded)
                                roundedReveals++;
                        }
                        else if (op == ShapeOp.EmitBox)
                        {
                            byte material = (byte)catalogue.Program[pc + 8];
                            ushort surface = (ushort)catalogue.Program[pc + 9];
                            PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 11];
                            if (mode == PrimitiveMode.Fill
                                && surface == SurfaceStyles.Planar
                                && (material == 4 || material == 15))
                                planarGlass++;
                        }

                        pc += length;
                        if (op == ShapeOp.End) break;
                    }
                }

                Assert.Greater(roundedReveals, 0,
                    "Window apertures should retain the city's rounded opening treatment.");
                Assert.Greater(planarGlass, 0,
                    "Glass infill should override the default detail profile to radius-0 planar geometry.");
            }
            finally
            {
                catalogue.Dispose();
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
