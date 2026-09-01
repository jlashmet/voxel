using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ArchitectureShapeProgramBuilderTests
    {
        [Test]
        public void PrimitiveOverridesCanDepartFromCityGeometryDefaults()
        {
            var profile = new StructureGeometryProfile(
                foundationCornerRadiusDm: 1,
                shellCornerRadiusDm: 4,
                openingCornerRadiusDm: 2,
                detailCornerRadiusDm: 3,
                detailSurface: StructureSurfaceTreatment.Beveled);
            var builder = new ArchitectureShapeProgramBuilder(
                profile,
                voxelsPerDecimetre: 1);

            builder.DetailBox(
                0, 0, 0,
                12, 18, 2,
                material: 4,
                cornerRadiusDm: 0,
                surface: StructureSurfaceTreatment.Planar);
            builder.DetailBox(
                20, 0, 0,
                12, 18, 6,
                material: 2,
                cornerRadiusDm: 2,
                surface: StructureSurfaceTreatment.ArchitecturalRounded);
            int[] code = builder.Finish();

            Assert.AreEqual(ShapeOp.EmitBox, (ShapeOp)code[0]);
            Assert.AreEqual(SurfaceStyles.Planar, (ushort)code[9],
                "A local glazing-like primitive should be able to suppress default detail rounding.");

            int second = ShapeOps.InstructionLength(ShapeOp.EmitBox);
            Assert.AreEqual(ShapeOp.EmitRoundedBox, (ShapeOp)code[second]);
            Assert.AreEqual(2, code[second + 8]);
            Assert.AreEqual(SurfaceStyles.ArchitecturalRounded, (ushort)code[second + 10]);
        }

        [Test]
        public void PrimitiveOverridesRejectNegativeCornerRadius()
        {
            var builder = new ArchitectureShapeProgramBuilder(
                StructureGeometryProfile.Sharp,
                voxelsPerDecimetre: 1);

            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                builder.DetailBox(
                    0, 0, 0,
                    8, 8, 8,
                    material: 1,
                    cornerRadiusDm: -1));
        }

        [Test]
        public void OpeningArchUsesIntegerArchPrismInCarveMode()
        {
            var profile = new StructureGeometryProfile(
                0, 0, 0, 0,
                openingSurface: StructureSurfaceTreatment.ArchitecturalRounded);
            var builder = new ArchitectureShapeProgramBuilder(profile, 1);

            builder.OpeningArchCarve(2, 18, 0, 13, 7, 5);
            int[] code = builder.Finish();

            Assert.AreEqual(ShapeOp.EmitPrism, (ShapeOp)code[0]);
            Assert.AreEqual(PrismProfile.Arch, (PrismProfile)code[8]);
            Assert.AreEqual((byte)0, (byte)code[9]);
            Assert.AreEqual(SurfaceStyles.ArchitecturalRounded, (ushort)code[10]);
            Assert.AreEqual(PrimitiveMode.Carve, (PrimitiveMode)code[12]);
        }
    }
}
