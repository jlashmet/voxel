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
            Assert.AreEqual(3, code[7], "The reveal carve must retain the full wall depth.");
            Assert.AreEqual(SurfaceStyles.ArchitecturalRounded, (ushort)code[10]);
            Assert.AreEqual(PrimitiveMode.Carve, (PrimitiveMode)code[12]);

            int pane = ShapeOps.InstructionLength(ShapeOp.EmitRoundedBox);
            Assert.AreEqual(ShapeOp.EmitBox, (ShapeOp)code[pane]);
            Assert.AreEqual(1, code[pane + 4], "The Z-normal pane should be centered inside the reveal.");
            Assert.AreEqual(1, code[pane + 7], "The pane must be thinner than the wall reveal.");
            Assert.AreEqual((byte)4, (byte)code[pane + 8]);
            Assert.AreEqual(SurfaceStyles.Planar, (ushort)code[pane + 9]);
        }

        [Test]
        public void GlazedOpeningUsesThinCenteredPaneAcrossFacadeOrientations()
        {
            var builder = new ArchitectureShapeProgramBuilder(
                StructureGeometryProfile.Sharp, 1);

            ArchitectureVoxelPatterns.GlazedOpening(
                builder,
                20, 5, 30,
                3, 12, 10,
                glazingMaterial: 15);
            int[] code = builder.Finish();

            Assert.AreEqual(ShapeOp.EmitBox, (ShapeOp)code[0]);
            Assert.AreEqual(31, code[4], "Large X-normal windows should retain a one-voxel facade border.");
            Assert.AreEqual(3, code[5], "The X-normal reveal must retain the full wall depth.");
            Assert.AreEqual(PrimitiveMode.Carve, (PrimitiveMode)code[11]);

            int pane = ShapeOps.InstructionLength(ShapeOp.EmitBox);
            Assert.AreEqual(ShapeOp.EmitBox, (ShapeOp)code[pane]);
            Assert.AreEqual(21, code[pane + 2], "The X-normal pane should be centered inside the reveal.");
            Assert.AreEqual(1, code[pane + 5], "The pane must be thinner than the wall reveal.");
            Assert.AreEqual(3, code[pane + 7], "A mullion should split the facade span instead of leaving one slab.");
            Assert.AreEqual((byte)15, (byte)code[pane + 8]);
            Assert.AreEqual(SurfaceStyles.Planar, (ushort)code[pane + 9]);
        }

        [Test]
        public void GlazedOpeningFramesAndSubdividesLargeFacadePane()
        {
            var builder = new ArchitectureShapeProgramBuilder(
                StructureGeometryProfile.Sharp, 1);

            ArchitectureVoxelPatterns.GlazedOpening(
                builder,
                10, 4, 30,
                12, 14, 3,
                glazingMaterial: 15);
            int[] code = builder.Finish();

            int carveLength = ShapeOps.InstructionLength(ShapeOp.EmitBox);
            int firstPane = carveLength;
            int secondCarve = firstPane + ShapeOps.InstructionLength(ShapeOp.EmitBox);
            int secondPane = secondCarve + carveLength;

            Assert.AreEqual(ShapeOp.EmitBox, (ShapeOp)code[0]);
            Assert.AreEqual(12, code[2], "The outer two voxels should remain facade material as a left frame.");
            Assert.AreEqual(6, code[3], "The top and bottom two voxels should remain facade material as frame.");
            Assert.AreEqual(3, code[5], "The first glazed cell should occupy half of the inset facade span.");
            Assert.AreEqual(10, code[6], "The framed opening should be vertically inset by two voxels.");
            Assert.AreEqual(3, code[7], "The carve must still cross the complete wall depth.");
            Assert.AreEqual(PrimitiveMode.Carve, (PrimitiveMode)code[11]);

            Assert.AreEqual(ShapeOp.EmitBox, (ShapeOp)code[firstPane]);
            Assert.AreEqual(31, code[firstPane + 4], "The first pane should remain centered in wall depth.");
            Assert.AreEqual(1, code[firstPane + 7], "The pane should stay thin after facade framing.");
            Assert.AreEqual((byte)15, (byte)code[firstPane + 8]);

            Assert.AreEqual(ShapeOp.EmitBox, (ShapeOp)code[secondCarve]);
            Assert.AreEqual(17, code[secondCarve + 2], "A two-voxel masonry mullion should separate the glazed cells.");
            Assert.AreEqual(3, code[secondCarve + 5]);
            Assert.AreEqual(PrimitiveMode.Carve, (PrimitiveMode)code[secondCarve + 11]);

            Assert.AreEqual(ShapeOp.EmitBox, (ShapeOp)code[secondPane]);
            Assert.AreEqual(17, code[secondPane + 2]);
            Assert.AreEqual(31, code[secondPane + 4]);
            Assert.AreEqual(1, code[secondPane + 7]);
            Assert.AreEqual((byte)15, (byte)code[secondPane + 8]);
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

        [Test]
        public void FramedArchedOpeningKeepsBodyClearanceAndCurvesTheHead()
        {
            var builder = new ArchitectureShapeProgramBuilder(
                StructureGeometryProfile.Sharp, 1);

            ArchitectureVoxelPatterns.FramedArchedOpening(
                builder,
                10, 4, 0,
                width: 13,
                clearHeight: 24,
                archRise: 7,
                depth: 5,
                frameThickness: 2,
                frameMaterial: 6);
            int[] code = builder.Finish();

            Assert.AreEqual(ShapeOp.EmitPrism, (ShapeOp)code[0]);
            Assert.AreEqual(PrismProfile.Arch, (PrismProfile)code[8]);
            Assert.AreEqual(PrimitiveMode.Fill, (PrimitiveMode)code[12]);

            int body = ShapeOps.InstructionLength(ShapeOp.EmitPrism);
            Assert.AreEqual(PrimitiveMode.Carve, (PrimitiveMode)code[body + 11]);
            Assert.AreEqual(24, code[body + 6]);

            int head = body + ShapeOps.InstructionLength((ShapeOp)code[body]);
            Assert.AreEqual(ShapeOp.EmitPrism, (ShapeOp)code[head]);
            Assert.AreEqual(PrismProfile.Arch, (PrismProfile)code[head + 8]);
            Assert.AreEqual(PrimitiveMode.Carve, (PrimitiveMode)code[head + 12]);
        }

        [Test]
        public void ArchedGlazingRestoresPlanarPaneAfterCurvedCarve()
        {
            var builder = new ArchitectureShapeProgramBuilder(
                StructureGeometryProfile.Sharp, 1);

            ArchitectureVoxelPatterns.FramedArchedGlazedOpening(
                builder,
                4, 10, 0,
                15, 17, 7, 5,
                2, 6, 15);
            int[] code = builder.Finish();

            bool carvedArch = false;
            bool planarGlassBody = false;
            bool planarGlassHead = false;
            for (int pc = 0; pc < code.Length;)
            {
                ShapeOp op = (ShapeOp)code[pc];
                if (op == ShapeOp.EmitPrism
                    && (PrismProfile)code[pc + 8] == PrismProfile.Arch)
                {
                    PrimitiveMode mode = (PrimitiveMode)code[pc + 12];
                    if (mode == PrimitiveMode.Carve) carvedArch = true;
                    if (mode == PrimitiveMode.Fill
                        && (byte)code[pc + 9] == 15
                        && (ushort)code[pc + 10] == SurfaceStyles.Planar)
                        planarGlassHead = true;
                }
                else if (op == ShapeOp.EmitBox
                         && (byte)code[pc + 8] == 15
                         && (ushort)code[pc + 9] == SurfaceStyles.Planar)
                    planarGlassBody = true;

                int length = ShapeOps.InstructionLength(op);
                Assert.GreaterOrEqual(length, 2);
                pc += length;
                if (op == ShapeOp.End) break;
            }

            Assert.IsTrue(carvedArch);
            Assert.IsTrue(planarGlassBody);
            Assert.IsTrue(planarGlassHead);
        }
    }
}
