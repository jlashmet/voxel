using System.Reflection;
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
        private const int ProductionMotorWidthVoxels = 6;
        private const int RequiredLateralMarginVoxels = 5;
        private const long MaximumTraversalCarveVoxels = 2_800_000;

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
                int carveBoxCount = 0;
                long carveVoxelVolume = 0;
                int expectedClearanceWidth = System.Math.Min(
                    spec.PathWidth,
                    WorldBuilderMountainLandmarkCatalogue.PathClearanceWidthVoxels);

                Assert.That(
                    WorldBuilderMountainLandmarkCatalogue.PathClearanceWidthVoxels,
                    Is.GreaterThanOrEqualTo(
                        ProductionMotorWidthVoxels + RequiredLateralMarginVoxels * 2),
                    "The centered traversal lane must preserve at least 0.5 m lateral clearance "
                    + "on both sides of the 0.6 m production motor.");

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
                        int sizeX = catalogue.Program[pc + 5];
                        int sizeY = catalogue.Program[pc + 6];
                        int sizeZ = catalogue.Program[pc + 7];
                        int material = catalogue.Program[pc + 8];
                        PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 11];
                        if (y == 0 && sizeY > 1 && material == MountainMaterial)
                            tallGroundSupportBoxes++;

                        if (mode == PrimitiveMode.Carve)
                        {
                            carveBoxCount++;
                            carveVoxelVolume += (long)sizeX * sizeY * sizeZ;
                            int horizontalNarrow = sizeX < sizeZ ? sizeX : sizeZ;
                            Assert.That(horizontalNarrow, Is.EqualTo(expectedClearanceWidth),
                                "Headroom carving must stay on the centered traversal lane instead "
                                + "of rasterizing empty space across the full decorative path width.");
                        }
                    }

                    pc += instructionLength;
                }

                Assert.That(tallGroundSupportBoxes, Is.Zero,
                    "Switchback support must not regress to tall ground-to-path rectangular retaining walls.");
                Assert.That(frustumCount, Is.GreaterThanOrEqualTo(20),
                    "The path and silhouette must be supported by multiple tapered landform masses.");
                Assert.That(carveBoxCount, Is.EqualTo(spec.SwitchbackCount * 2 + 1),
                    "Every ramp, turn, final ascent, and summit approach must retain explicit headroom carving.");
                Assert.That(carveVoxelVolume, Is.LessThanOrEqualTo(MaximumTraversalCarveVoxels),
                    "Traversal clearance must remain below the measured one-time bake-cost envelope; "
                    + "full-width carving previously rasterized more than five million voxels.");
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

        [Test]
        public void OfflineBakeFarFieldSuppressionIsScopedAndRestored()
        {
            var store = new FarFieldStructureStore();
            FieldInfo captureEnabled = typeof(FarFieldStructureStore).GetField(
                "_captureEnabled",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(captureEnabled, Is.Not.Null,
                "The far-field store must retain an instance-scoped capture gate for offline baking.");
            Assert.That(captureEnabled.GetValue(store), Is.True,
                "Runtime far-field capture must be enabled by default.");

            using (store.SuppressCapture())
            {
                Assert.That(captureEnabled.GetValue(store), Is.False,
                    "Offline bake scope must suppress non-serialized far-field scans.");

                using (store.SuppressCapture())
                    Assert.That(captureEnabled.GetValue(store), Is.False,
                        "Nested suppression must remain disabled until the outer scope exits.");

                Assert.That(captureEnabled.GetValue(store), Is.False,
                    "Disposing a nested scope must not re-enable an outer suppression scope.");
            }

            Assert.That(captureEnabled.GetValue(store), Is.True,
                "Capture must be restored after offline generation so LoadBake/runtime rebuilding works.");
        }
    }
}
