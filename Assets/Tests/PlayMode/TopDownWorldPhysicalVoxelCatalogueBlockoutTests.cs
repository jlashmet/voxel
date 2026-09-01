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
        public void GenericBuildingBlockoutUsesBoundedFoundationAndWallShellsInsteadOfSolidVolumes()
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

            int foundationBoxCount = 0;
            long foundationVoxelVolume = 0;
            bool centreCoveredByFoundation = false;
            int timberBoxCount = 0;
            long timberVoxelVolume = 0;
            bool centreCoveredByTimber = false;
            const int centreX = width / 2 + 6;
            const int centreZ = depth / 2 + 6;
            const int foundationCentreY = 4;
            const int timberCentreY = 9;

            for (var i = 0; i < program.Length;)
            {
                ShapeOp op = (ShapeOp)program[i];
                if (op == ShapeOp.EmitBox)
                {
                    int material = program[i + 8];
                    int minX = program[i + 2];
                    int minY = program[i + 3];
                    int minZ = program[i + 4];
                    int sizeX = program[i + 5];
                    int sizeY = program[i + 6];
                    int sizeZ = program[i + 7];
                    long volume = (long)sizeX * sizeY * sizeZ;

                    if (material == foundation)
                    {
                        foundationBoxCount++;
                        foundationVoxelVolume += volume;
                        centreCoveredByFoundation |=
                            centreX >= minX && centreX < minX + sizeX
                            && foundationCentreY >= minY && foundationCentreY < minY + sizeY
                            && centreZ >= minZ && centreZ < minZ + sizeZ;
                    }
                    else if (material == timber)
                    {
                        timberBoxCount++;
                        timberVoxelVolume += volume;
                        centreCoveredByTimber |=
                            centreX >= minX && centreX < minX + sizeX
                            && timberCentreY >= minY && timberCentreY < minY + sizeY
                            && centreZ >= minZ && centreZ < minZ + sizeZ;
                    }
                }

                int instructionLength = ShapeOps.InstructionLength(op);
                Assert.That(instructionLength, Is.GreaterThan(0));
                i += instructionLength;
                if (op == ShapeOp.End) break;
            }

            long formerSolidFoundationVolume = (long)(width + 12) * 8 * (depth + 12);
            long formerSolidBodyVolume = (long)width * (height - 8) * depth;
            Assert.That(foundationBoxCount, Is.EqualTo(4),
                "A generic fallback building should use four bounded exterior foundation boxes.");
            Assert.That(centreCoveredByFoundation, Is.False,
                "The generic blockout foundation interior should stay hollow instead of publishing an unnecessary solid slab.");
            Assert.That(foundationVoxelVolume, Is.LessThan(formerSolidFoundationVolume / 2),
                "The perimeter plinth should materially reduce foundation voxel work while preserving the exterior footprint and wall support.");
            Assert.That(timberBoxCount, Is.EqualTo(4),
                "A generic fallback building should use four bounded exterior wall boxes.");
            Assert.That(centreCoveredByTimber, Is.False,
                "The generic blockout interior should stay hollow instead of publishing an unnecessary solid mass.");
            Assert.That(timberVoxelVolume, Is.LessThan(formerSolidBodyVolume / 4),
                "The wall shell should materially reduce authored body voxel work while preserving the exterior blockout.");
        }
    }
}
