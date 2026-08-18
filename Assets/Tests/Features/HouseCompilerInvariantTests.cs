using System;
using NUnit.Framework;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.Features
{
    /// <summary>WB040 guards for deterministic house compilation and declared planar footprint.</summary>
    public sealed class HouseCompilerInvariantTests
    {
        [Test]
        public void NamedPresetsCompileDeterministicallyInsideConfiguredFootprint()
        {
            AssertDeterministicAndInsideFootprint(HousePresets.CottageCompatibility(3, 7));
            AssertDeterministicAndInsideFootprint(HousePresets.Farmhouse(3, 7));
            AssertDeterministicAndInsideFootprint(HousePresets.TallTownhouse(3, 7));
        }

        [Test]
        public void CompilerRejectsWallThicknessThatConsumesInterior()
        {
            HouseConfig config = HousePresets.Farmhouse(3, 7);
            config.Walls.Thickness = config.Width / 2;

            Assert.Throws<ArgumentException>(() => Compile(config));
        }

        [Test]
        public void CompilerRejectsLevelsThatDoNotFitInsideWallHeight()
        {
            HouseConfig config = HousePresets.Farmhouse(3, 7);
            config.Floors.FloorCount = 3;
            config.Floors.LevelHeight = 24;
            config.Walls.Height = 48;

            Assert.Throws<ArgumentException>(() => Compile(config));
        }

        private static void AssertDeterministicAndInsideFootprint(HouseConfig config)
        {
            int[] first = Compile(config);
            int[] second = Compile(config);

            CollectionAssert.AreEqual(first, second);
            AssertProgramInsidePlanarFootprint(first, config.Width, config.Depth);
        }

        private static int[] Compile(HouseConfig config)
        {
            return HouseProgramCompiler.BuildProgram(in config, 0, 1);
        }

        private static void AssertProgramInsidePlanarFootprint(int[] program, int width, int depth)
        {
            int pc = 0;
            bool ended = false;

            while (pc < program.Length)
            {
                ShapeOp op = (ShapeOp)program[pc++];
                pc++; // operand register mask

                switch (op)
                {
                    case ShapeOp.EmitBox:
                        AssertPrimitiveFootprint(program[pc], program[pc + 2],
                            program[pc + 3], program[pc + 5], width, depth, op);
                        pc += 10;
                        break;

                    case ShapeOp.EmitPrism:
                        AssertPrimitiveFootprint(program[pc], program[pc + 2],
                            program[pc + 3], program[pc + 5], width, depth, op);
                        pc += 11;
                        break;

                    case ShapeOp.SetAnchor:
                    {
                        int x = program[pc + 1];
                        int z = program[pc + 3];
                        Assert.That(x, Is.InRange(0, width), "anchor escapes the house X footprint");
                        Assert.That(z, Is.InRange(0, depth), "anchor escapes the house Z footprint");
                        pc += 5;
                        break;
                    }

                    case ShapeOp.End:
                        ended = true;
                        Assert.AreEqual(program.Length, pc, "shape program contains data after End");
                        break;

                    default:
                        Assert.Fail($"Unexpected house opcode {op}; update the footprint guard when the compiler grows a new bounded primitive.");
                        return;
                }

                if (ended)
                    break;
            }

            Assert.IsTrue(ended, "house program did not terminate with End");
        }

        private static void AssertPrimitiveFootprint(
            int x, int z, int sizeX, int sizeZ, int width, int depth, ShapeOp op)
        {
            Assert.Greater(sizeX, 0, $"{op} emitted a non-positive X size");
            Assert.Greater(sizeZ, 0, $"{op} emitted a non-positive Z size");
            Assert.GreaterOrEqual(x, 0, $"{op} escapes the house -X footprint");
            Assert.GreaterOrEqual(z, 0, $"{op} escapes the house -Z footprint");
            Assert.LessOrEqual(x + sizeX, width, $"{op} escapes the house +X footprint");
            Assert.LessOrEqual(z + sizeZ, depth, $"{op} escapes the house +Z footprint");
        }
    }
}
