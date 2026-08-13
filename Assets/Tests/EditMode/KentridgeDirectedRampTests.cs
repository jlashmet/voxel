using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Features.Emitters;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeDirectedRampTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void ReverseRampBitRaisesTheNegativeAxisEnd()
        {
            Primitive ramp = BoxEmitter.Ramp(
                int3.zero,
                new int3(4, 4, 8),
                (byte)(2 | BoxEmitter.ReverseRampBit),
                material: 1,
                PrimitiveMode.Fill,
                order: 0);

            Assert.IsTrue(BoxEmitter.RampContains(in ramp, new int3(1, 3, 0)),
                "A reversed Z ramp should be tallest at its minimum-Z end.");
            Assert.IsFalse(BoxEmitter.RampContains(in ramp, new int3(1, 3, 7)),
                "A reversed Z ramp should be low at its maximum-Z end.");
        }

        [Test]
        public void KentridgePublicRoadsEncodeDirectionWithoutHalfTurnPlacements()
        {
            FeatureCatalogue catalogue = KentridgeDirectedTownSurfaceCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                int ramps = 0;
                int reversedRamps = 0;

                for (int i = 0; i < catalogue.ExplicitPlacements.Length; i++)
                    Assert.AreEqual(0, catalogue.ExplicitPlacements[i].Orientation,
                        "Directed Kentridge roads should not rely on half-turn placement rotation.");

                for (int d = 0; d < catalogue.Definitions.Length; d++)
                {
                    FeatureDefinition definition = catalogue.Definitions[d];
                    int pc = definition.ProgramOffset;
                    int end = pc + definition.ProgramLength;

                    while (pc < end)
                    {
                        ShapeOp op = (ShapeOp)catalogue.Program[pc];
                        int length = ShapeOps.InstructionLength(op);
                        Assert.Greater(length, 0);
                        Assert.LessOrEqual(pc + length, end);
                        if (op == ShapeOp.End) break;

                        if (op == ShapeOp.EmitRamp)
                        {
                            ramps++;
                            int axis = catalogue.Program[pc + 2 + 6];
                            if ((axis & BoxEmitter.ReverseRampBit) != 0)
                                reversedRamps++;
                        }

                        pc += length;
                    }
                }

                Assert.Greater(ramps, 0,
                    "Macro-vertical Kentridge should contain actual road ramps.");
                Assert.Greater(reversedRamps, 0,
                    "At least the northbound climbs must use explicit reverse ramp direction.");
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
