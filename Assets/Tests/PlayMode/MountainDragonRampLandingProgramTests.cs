using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime.Emitters;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class MountainDragonRampLandingProgramTests
    {
        private const uint Seed = 0x5EED1234;
        private const byte MountainMaterial = 1;
        private const byte PathMaterial = 13;
        private const byte DragonMaterial = 9;

        [Test]
        public void AlternatingRampsKeepLowTurnLandingFlatWithoutBreakingInteriorContinuity()
        {
            MountainLandmarkSpec spec = ShowcaseMountainDragonLayout.CreateLandmark(Seed);
            FeatureCatalogue catalogue = WorldBuilderMountainLandmarkCatalogue.Build(
                in spec,
                MountainMaterial,
                PathMaterial,
                DragonMaterial,
                Allocator.Temp);

            try
            {
                FeatureDefinition landform = catalogue.Definitions[0];
                int pc = landform.ProgramOffset;
                int end = pc + landform.ProgramLength;
                int xRampCount = 0;

                while (pc < end)
                {
                    ShapeOp op = (ShapeOp)catalogue.Program[pc];
                    if (op == ShapeOp.End) break;

                    int instructionLength = ShapeOps.InstructionLength(op);
                    Assert.That(instructionLength, Is.GreaterThan(0));
                    Assert.That(pc + instructionLength, Is.LessThanOrEqualTo(end));

                    if (op == ShapeOp.EmitRamp)
                    {
                        int axisWithDirection = catalogue.Program[pc + 8];
                        int axis = axisWithDirection & ShapeOps.RampAxisMask;
                        if (axis == 0)
                        {
                            int x = catalogue.Program[pc + 2];
                            int y = catalogue.Program[pc + 3];
                            int z = catalogue.Program[pc + 4];
                            int sizeX = catalogue.Program[pc + 5];
                            int sizeY = catalogue.Program[pc + 6];
                            int sizeZ = catalogue.Program[pc + 7];
                            byte material = (byte)catalogue.Program[pc + 9];
                            PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 12];
                            Primitive ramp = BoxEmitter.Ramp(
                                new int3(x, y, z),
                                new int3(sizeX, sizeY, sizeZ),
                                (byte)axisWithDirection,
                                material,
                                mode,
                                xRampCount);

                            bool reverse = (axisWithDirection & ShapeOps.ReverseRampBit) != 0;
                            int lowLandingCentreX = reverse
                                ? spec.PathMinLocalX + spec.PathRun - spec.PathWidth / 2
                                : spec.PathMinLocalX + spec.PathWidth / 2;
                            int firstInteriorX = reverse
                                ? spec.PathMinLocalX + spec.PathRun - spec.PathWidth - 1
                                : spec.PathMinLocalX + spec.PathWidth;
                            int highInteriorX = reverse
                                ? spec.PathMinLocalX + spec.PathWidth
                                : spec.PathMinLocalX + spec.PathRun - spec.PathWidth - 1;
                            int centreZ = z + spec.PathWidth / 2;

                            Assert.That(
                                BoxEmitter.RampContains(
                                    in ramp,
                                    new int3(lowLandingCentreX, y + 1, centreZ)),
                                Is.False,
                                "The next ramp must not rise into the low turn landing's headroom.");
                            Assert.That(
                                BoxEmitter.RampContains(
                                    in ramp,
                                    new int3(firstInteriorX, y, centreZ)),
                                Is.True,
                                "The first interior ramp column must retain a walking floor adjacent to the flat landing.");
                            Assert.That(
                                BoxEmitter.RampContains(
                                    in ramp,
                                    new int3(highInteriorX, y + spec.PathRise, centreZ)),
                                Is.True,
                                "The ramp must still reach the full tier elevation before the high landing begins.");

                            xRampCount++;
                        }
                    }

                    pc += instructionLength;
                }

                Assert.That(xRampCount, Is.EqualTo(spec.SwitchbackCount),
                    "Every alternating switchback tier must be covered by the flat-landing ramp contract.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }
    }
}
