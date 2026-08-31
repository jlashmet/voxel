using System;
using System.Reflection;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class TopDownWorldPhysicalVoxelCatalogueBlockoutTests
    {
        [Test]
        public void GenericBuildingBlockoutUsesBoundedWallShellInsteadOfSolidBodyVolume()
        {
            MethodInfo buildingProgram = typeof(TopDownWorldPhysicalVoxelCatalogue).GetMethod(
                "BuildingProgram",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(buildingProgram, Is.Not.Null);

            const int width = 100;
            const int depth = 80;
            const int height = 60;
            const int scale = 1;
            const byte foundation = 11;
            const byte timber = 22;
            const byte roof = 33;
            int[] program = (int[])buildingProgram.Invoke(
                null,
                new object[] { width, depth, height, 0, scale, foundation, timber, roof });

            int timberBoxCount = 0;
            long timberVoxelVolume = 0;
            bool centreCoveredByTimber = false;
            const int centreX = width / 2 + 6;
            const int centreZ = depth / 2 + 6;
            const int centreY = 9;

            for (var i = 0; i < program.Length;)
            {
                ShapeOp op = (ShapeOp)program[i];
                if (op == ShapeOp.EmitBox && program[i + 8] == timber)
                {
                    timberBoxCount++;
                    int minX = program[i + 2];
                    int minY = program[i + 3];
                    int minZ = program[i + 4];
                    int sizeX = program[i + 5];
                    int sizeY = program[i + 6];
                    int sizeZ = program[i + 7];
                    timberVoxelVolume += (long)sizeX * sizeY * sizeZ;
                    centreCoveredByTimber |=
                        centreX >= minX && centreX < minX + sizeX
                        && centreY >= minY && centreY < minY + sizeY
                        && centreZ >= minZ && centreZ < minZ + sizeZ;
                }

                int instructionLength = ShapeOps.InstructionLength(op);
                Assert.That(instructionLength, Is.GreaterThan(0));
                i += instructionLength;
                if (op == ShapeOp.End) break;
            }

            long formerSolidBodyVolume = (long)width * (height - 8) * depth;
            Assert.That(timberBoxCount, Is.EqualTo(4),
                "A generic fallback building should use four bounded exterior wall boxes.");
            Assert.That(centreCoveredByTimber, Is.False,
                "The generic blockout interior should stay hollow instead of publishing an unnecessary solid mass.");
            Assert.That(timberVoxelVolume, Is.LessThan(formerSolidBodyVolume / 4),
                "The wall shell should materially reduce authored body voxel work while preserving the exterior blockout.");
        }
    }
}
