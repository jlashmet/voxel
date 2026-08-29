using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class MountainDragonNaturalSupportProgramTests
    {
        private const uint Seed = 0x5EED1234;
        private const byte MountainMaterial = 1;
        private const byte PathMaterial = 13;
        private const byte DragonMaterial = 9;

        [Test]
        public void MountainPathSupportUsesTaperedMassesWithoutTallRetainingWallBoxes()
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
                int frustumCount = 0;
                int tallGroundSupportBoxes = 0;

                while (pc < end)
                {
                    ShapeOp op = (ShapeOp)catalogue.Program[pc];
                    if (op == ShapeOp.End) break;

                    int instructionLength = ShapeOps.InstructionLength(op);
                    Assert.That(instructionLength, Is.GreaterThan(0),
                        "Mountain landform program contains an unknown shape opcode.");
                    Assert.That(pc + instructionLength, Is.LessThanOrEqualTo(end),
                        "Mountain landform program contains a truncated shape instruction.");

                    if (op == ShapeOp.EmitFrustum)
                        frustumCount++;
                    else if (op == ShapeOp.EmitBox)
                    {
                        int y = catalogue.Program[pc + 3];
                        int sizeY = catalogue.Program[pc + 6];
                        int material = catalogue.Program[pc + 8];
                        if (y == 0 && sizeY > 1 && material == MountainMaterial)
                            tallGroundSupportBoxes++;
                    }

                    pc += instructionLength;
                }

                Assert.That(tallGroundSupportBoxes, Is.Zero,
                    "Switchback support must not regress to tall ground-to-path rectangular retaining walls.");
                Assert.That(frustumCount, Is.GreaterThanOrEqualTo(20),
                    "The path and silhouette must be supported by multiple tapered landform masses.");
                Assert.That(landform.MaxPrimitives, Is.LessThanOrEqualTo(80),
                    "Naturalized Mountain Dragon support must stay within the feature's measured primitive envelope.");
                Assert.That(landform.MaxPrimitives, Is.LessThanOrEqualTo(FeatureBudget.MaxPrimitivesPerInstance),
                    "Mountain Dragon realization must remain inside the shared per-instance primitive budget.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }
    }
}
