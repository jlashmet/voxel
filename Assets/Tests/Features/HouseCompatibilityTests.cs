using NUnit.Framework;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Tests.Features.Fixtures;

namespace VoxelEngine.Tests.Features
{
    public sealed class HouseCompatibilityTests
    {
        [Test]
        public void CottageCompatibilityPresetEmitsLegacyProgramExactly()
        {
            int[] expected = new ProgramBuilder()
                .Box(0, 0, 0, 64, 8, 64,
                    CottageFixture.MaterialStone, PrimitiveMode.Fill)
                .Box(0, 8, 0, 64, 32, 64,
                    CottageFixture.MaterialStone, PrimitiveMode.Fill)
                .Box(4, 8, 4, 56, 32, 56,
                    0, PrimitiveMode.Carve)
                .Box(26, 8, 0, 12, 20, 4,
                    0, PrimitiveMode.Carve)
                .Prism(0, 40, 0, 64, 16, 64,
                    PrismProfile.Gable, CottageFixture.MaterialWood, PrimitiveMode.Fill)
                .Anchor(CottageProgram.AnchorDoor, 32, 8, 0, Facing.South)
                .Anchor(CottageProgram.AnchorHearth, 32, 8, 32, Facing.Up)
                .End()
                .Build();

            int[] actual = CottageProgram.Build();

            CollectionAssert.AreEqual(expected, actual,
                "refactoring the compatibility cottage through shared house components changed its shape program");
        }

        [Test]
        public void DetailedHousePresetsMaintainNavigableStructuralInvariants()
        {
            AssertStructuralInvariants(HousePresets.CottageCompatibility(1, 2));
            AssertStructuralInvariants(HousePresets.Farmhouse(1, 2));
            AssertStructuralInvariants(HousePresets.TallTownhouse(1, 2));
        }

        [Test]
        public void SameDetailedHouseConfigCompilesDeterministically()
        {
            HouseConfig config = HousePresets.Farmhouse(3, 8);

            int[] first = HouseProgramCompiler.BuildCompatibilityProgram(
                in config, mainDoorAnchorIndex: 0, hearthAnchorIndex: 1);
            int[] second = HouseProgramCompiler.BuildCompatibilityProgram(
                in config, mainDoorAnchorIndex: 0, hearthAnchorIndex: 1);

            CollectionAssert.AreEqual(first, second,
                "the same house config must compile to an identical integer shape program");
        }

        [Test]
        public void DetailedHouseShellRemainsInsideDeclaredFootprintAndHeightEnvelope()
        {
            HouseConfig config = HousePresets.Farmhouse(3, 8);
            int[] program = HouseProgramCompiler.BuildCompatibilityProgram(
                in config, mainDoorAnchorIndex: 0, hearthAnchorIndex: 1);

            int roofSpan = config.Roof.RidgeAxis == RoofAxis.Z
                ? config.Width / 2
                : config.Depth / 2;
            int roofHeight = roofSpan * config.Roof.PitchRise / config.Roof.PitchRun;
            int maximumY = config.FoundationDepth + config.Walls.Height + roofHeight;

            // The compatibility compiler emits four boxes followed by one prism. Validate every
            // emitted shell/carve bound rather than merely checking the authored config dimensions.
            for (int op = 0; op < 4; op++)
                AssertPrimitiveBounds(program, op * 12, config.Width, config.Depth, maximumY);
            AssertPrimitiveBounds(program, 48, config.Width, config.Depth, maximumY);
        }

        private static void AssertStructuralInvariants(HouseConfig config)
        {
            Assert.IsTrue(config.Footprint.IsWellFormed);
            Assert.IsTrue(config.Walls.IsWellFormed);
            Assert.Greater(config.FoundationDepth, 0);
            Assert.Greater(config.Width, config.WallThickness * 2,
                "wall thickness must leave navigable interior width");
            Assert.Greater(config.Depth, config.WallThickness * 2,
                "wall thickness must leave navigable interior depth");
            Assert.AreEqual(config.Walls.Height, config.FloorCount * config.FloorHeight,
                "preset floor stack must account for the authored wall height");
            Assert.AreEqual(StructureComponentValidationIssue.None,
                StructureComponentValidation.Opening(in config.MainDoor, config.Width));
            Assert.AreEqual(StructureComponentValidationIssue.None,
                StructureComponentValidation.Roof(in config.Roof));
        }

        private static void AssertPrimitiveBounds(
            int[] program,
            int opcodeOffset,
            int width,
            int depth,
            int maximumY)
        {
            int x = program[opcodeOffset + 2];
            int y = program[opcodeOffset + 3];
            int z = program[opcodeOffset + 4];
            int sizeX = program[opcodeOffset + 5];
            int sizeY = program[opcodeOffset + 6];
            int sizeZ = program[opcodeOffset + 7];

            Assert.Greater(sizeX, 0);
            Assert.Greater(sizeY, 0);
            Assert.Greater(sizeZ, 0);
            Assert.GreaterOrEqual(x, 0);
            Assert.GreaterOrEqual(y, 0);
            Assert.GreaterOrEqual(z, 0);
            Assert.LessOrEqual(x + sizeX, width);
            Assert.LessOrEqual(z + sizeZ, depth);
            Assert.LessOrEqual(y + sizeY, maximumY);
        }
    }
}
