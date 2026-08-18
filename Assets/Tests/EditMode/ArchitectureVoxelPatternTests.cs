using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ArchitectureVoxelPatternTests
    {
        [Test]
        public void GlazedOpeningSeparatesRevealAndPaneGeometry()
        {
            var profile = new StructureGeometryProfile(
                foundationCornerRadiusDm: 1,
                shellCornerRadiusDm: 3,
                openingCornerRadiusDm: 2,
                detailCornerRadiusDm: 2,
                openingSurface: StructureSurfaceTreatment.ArchitecturalRounded,
                detailSurface: StructureSurfaceTreatment.Beveled);
            var builder = new ArchitectureShapeProgramBuilder(profile, 1);

            ArchitectureVoxelPatterns.GlazedOpening(
                builder,
                2, 4, 0,
                10, 14, 3,
                glazingMaterial: 4);
            int[] code = builder.Finish();

            Assert.AreEqual(ShapeOp.EmitRoundedBox, (ShapeOp)code[0]);
            Assert.AreEqual(SurfaceStyles.ArchitecturalRounded, (ushort)code[10]);
            Assert.AreEqual(PrimitiveMode.Carve, (PrimitiveMode)code[12]);

            int pane = ShapeOps.InstructionLength(ShapeOp.EmitRoundedBox);
            Assert.AreEqual(ShapeOp.EmitBox, (ShapeOp)code[pane]);
            Assert.AreEqual((byte)4, (byte)code[pane + 8]);
            Assert.AreEqual(SurfaceStyles.Planar, (ushort)code[pane + 9]);
        }

        [Test]
        public void HollowShellKeepsInteriorClearanceSharp()
        {
            var profile = new StructureGeometryProfile(
                foundationCornerRadiusDm: 1,
                shellCornerRadiusDm: 3,
                openingCornerRadiusDm: 2,
                detailCornerRadiusDm: 2,
                shellSurface: StructureSurfaceTreatment.ArchitecturalRounded);
            var builder = new ArchitectureShapeProgramBuilder(profile, 1);

            ArchitectureVoxelPatterns.HollowShell(
                builder,
                0, 0, 0,
                30, 20, 24,
                thickness: 3,
                material: 1);
            int[] code = builder.Finish();

            Assert.AreEqual(ShapeOp.EmitRoundedBox, (ShapeOp)code[0]);
            Assert.AreEqual(PrimitiveMode.Fill, (PrimitiveMode)code[12]);

            int interior = ShapeOps.InstructionLength(ShapeOp.EmitRoundedBox);
            Assert.AreEqual(ShapeOp.EmitBox, (ShapeOp)code[interior]);
            Assert.AreEqual(PrimitiveMode.Carve, (PrimitiveMode)code[interior + 11]);
            Assert.AreEqual(SurfaceStyles.MaterialDefault, (ushort)code[interior + 9]);
        }

        [Test]
        public void TwinGableRoofUsesProfileRoofTreatmentForBothHalves()
        {
            var profile = new StructureGeometryProfile(
                0, 0, 0, 0,
                roofSurface: StructureSurfaceTreatment.Smooth);
            var builder = new ArchitectureShapeProgramBuilder(profile, 1);

            ArchitectureVoxelPatterns.TwinGableRoof(
                builder,
                0, 20, 0,
                40, 12, 30,
                overlap: 3,
                material: 7);
            int[] code = builder.Finish();

            Assert.AreEqual(ShapeOp.EmitPrism, (ShapeOp)code[0]);
            Assert.AreEqual(SurfaceStyles.Smooth, (ushort)code[10]);
            int second = ShapeOps.InstructionLength(ShapeOp.EmitPrism);
            Assert.AreEqual(ShapeOp.EmitPrism, (ShapeOp)code[second]);
            Assert.AreEqual(SurfaceStyles.Smooth, (ushort)code[second + 10]);
        }
    }
}
