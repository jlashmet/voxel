using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FramedArchedOpeningProjectionTests
    {
        [Test]
        public void FramedArchedOpeningCarvesThroughProjectingSurround()
        {
            var builder = new ArchitectureShapeProgramBuilder(
                StructureGeometryProfile.Sharp, 1);

            ArchitectureVoxelPatterns.FramedArchedOpening(
                builder,
                20, 4, 30,
                width: 13,
                clearHeight: 24,
                archRise: 7,
                depth: 5,
                frameThickness: 2,
                frameMaterial: 6);
            int[] code = builder.Finish();

            Assert.AreEqual(ShapeOp.EmitPrism, (ShapeOp)code[0]);
            Assert.AreEqual(28, code[4], "The surround projects two voxels in front of the wall plane.");
            Assert.AreEqual(7, code[7], "The surround spans the projection plus the wall depth.");

            int body = ShapeOps.InstructionLength(ShapeOp.EmitPrism);
            Assert.AreEqual(ShapeOp.EmitBox, (ShapeOp)code[body]);
            Assert.AreEqual(PrimitiveMode.Carve, (PrimitiveMode)code[body + 11]);
            Assert.AreEqual(28, code[body + 4],
                "Body clearance must begin at the surround's projecting front face.");
            Assert.AreEqual(7, code[body + 7],
                "Body clearance must cross the complete projecting surround and wall depth.");

            int head = body + ShapeOps.InstructionLength(ShapeOp.EmitBox);
            Assert.AreEqual(ShapeOp.EmitPrism, (ShapeOp)code[head]);
            Assert.AreEqual(PrismProfile.Arch, (PrismProfile)code[head + 8]);
            Assert.AreEqual(PrimitiveMode.Carve, (PrimitiveMode)code[head + 12]);
            Assert.AreEqual(28, code[head + 4],
                "Arch clearance must begin at the surround's projecting front face.");
            Assert.AreEqual(7, code[head + 7],
                "Arch clearance must cross the complete projecting surround and wall depth.");
        }
    }
}
