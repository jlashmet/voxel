using NUnit.Framework;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.Features
{
    /// <summary>WB040 completion coverage for house invariants, determinism, and footprint bounds.</summary>
    public sealed class HousePhaseCompletionTests
    {
        [Test]
        public void FarmhousePresetKeepsAllConfiguredHouseHooksWellFormed()
        {
            HouseConfig house = Farmhouse();

            Assert.IsTrue(house.Footprint.IsWellFormed);
            Assert.IsTrue(house.Walls.IsWellFormed);
            Assert.IsTrue(house.Floors.IsWellFormed);
            Assert.AreEqual(StructureOpeningKind.Door, house.MainDoor.Kind);
            Assert.IsTrue(house.MainDoor.IsWellFormed);
            Assert.IsTrue(house.FrontDoors.IsWellFormed);
            Assert.IsTrue(house.RearDoors.IsWellFormed);
            Assert.IsTrue(house.LeftDoors.IsWellFormed);
            Assert.IsTrue(house.RightDoors.IsWellFormed);
            Assert.IsTrue(house.FrontWindows.IsWellFormed);
            Assert.IsTrue(house.RearWindows.IsWellFormed);
            Assert.IsTrue(house.LeftWindows.IsWellFormed);
            Assert.IsTrue(house.RightWindows.IsWellFormed);
            Assert.IsTrue(house.Roof.IsWellFormed);
            Assert.IsTrue(house.Dormers.IsWellFormed);
            Assert.AreEqual(7, house.Palette.Resolve(StructureMaterialRole.Foundation));
            Assert.AreEqual(9, house.Palette.Resolve(StructureMaterialRole.PrimaryWall));
            Assert.AreEqual(11, house.Palette.Resolve(StructureMaterialRole.Roof));
        }

        [Test]
        public void SameHouseConfigProducesIdenticalShapeProgram()
        {
            HouseConfig house = Farmhouse();

            int[] first = HouseProgramCompiler.BuildCompatibilityProgram(
                in house, mainDoorAnchorIndex: 0, hearthAnchorIndex: 1);
            int[] second = HouseProgramCompiler.BuildCompatibilityProgram(
                in house, mainDoorAnchorIndex: 0, hearthAnchorIndex: 1);

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void CompiledHouseShellStaysInsideConfiguredHorizontalFootprint()
        {
            HouseConfig house = Farmhouse();
            int[] program = HouseProgramCompiler.BuildCompatibilityProgram(
                in house, mainDoorAnchorIndex: 0, hearthAnchorIndex: 1);

            int width = house.Footprint.Primary.Size.x;
            int depth = house.Footprint.Primary.Size.y;
            int pc = 0;
            while (pc < program.Length)
            {
                ShapeOp op = (ShapeOp)program[pc++];
                pc++; // parameter mask

                switch (op)
                {
                    case ShapeOp.EmitBox:
                    {
                        int x = program[pc];
                        int z = program[pc + 2];
                        int sizeX = program[pc + 3];
                        int sizeZ = program[pc + 5];
                        AssertHorizontalBounds(x, z, sizeX, sizeZ, width, depth, op);
                        pc += 10;
                        break;
                    }
                    case ShapeOp.EmitPrism:
                    {
                        int x = program[pc];
                        int z = program[pc + 2];
                        int sizeX = program[pc + 3];
                        int sizeZ = program[pc + 5];
                        AssertHorizontalBounds(x, z, sizeX, sizeZ, width, depth, op);
                        pc += 11;
                        break;
                    }
                    case ShapeOp.SetAnchor:
                        pc += 5;
                        break;
                    case ShapeOp.End:
                        Assert.AreEqual(program.Length, pc, "End must terminate the compiled house program.");
                        return;
                    default:
                        Assert.Fail($"Unexpected opcode {op} in compatibility house program.");
                        return;
                }
            }

            Assert.Fail("Compiled house program did not terminate with ShapeOp.End.");
        }

        private static HouseConfig Farmhouse()
        {
            HouseConfig house = HousePresets.Farmhouse(
                masonryMaterial: 9,
                timberMaterial: 11);
            house.Palette.Foundation = 7;
            house.Palette.Floor = 7;
            return house;
        }

        private static void AssertHorizontalBounds(
            int x,
            int z,
            int sizeX,
            int sizeZ,
            int footprintWidth,
            int footprintDepth,
            ShapeOp op)
        {
            Assert.GreaterOrEqual(x, 0, $"{op} begins left of the configured footprint.");
            Assert.GreaterOrEqual(z, 0, $"{op} begins behind the configured footprint.");
            Assert.Greater(sizeX, 0, $"{op} must have positive X size.");
            Assert.Greater(sizeZ, 0, $"{op} must have positive Z size.");
            Assert.LessOrEqual(x + sizeX, footprintWidth,
                $"{op} exceeds the configured footprint in X.");
            Assert.LessOrEqual(z + sizeZ, footprintDepth,
                $"{op} exceeds the configured footprint in Z.");
        }
    }
}
