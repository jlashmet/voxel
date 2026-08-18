using NUnit.Framework;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.Features
{
    public sealed class HousePresetTests
    {
        [Test]
        public void NamedPresetsUseSameConfigAndCompilerButProduceDifferentPrograms()
        {
            HouseConfig cottage = HousePresets.CottageCompatibility(7, 11);
            HouseConfig farmhouse = HousePresets.Farmhouse(7, 11);
            HouseConfig townhouse = HousePresets.TallTownhouse(9, 13);

            int[] cottageProgram = HouseProgramCompiler.BuildProgram(
                in cottage,
                mainDoorAnchorIndex: 0,
                hearthAnchorIndex: 1);
            int[] farmhouseProgram = HouseProgramCompiler.BuildProgram(
                in farmhouse,
                mainDoorAnchorIndex: 0,
                hearthAnchorIndex: 1);
            int[] townhouseProgram = HouseProgramCompiler.BuildProgram(
                in townhouse,
                mainDoorAnchorIndex: 0,
                hearthAnchorIndex: 1);

            Assert.AreEqual(64, cottage.Width);
            Assert.AreEqual(64, cottage.Depth);
            Assert.AreEqual(1, cottage.FloorCount);

            Assert.AreEqual(96, farmhouse.Width);
            Assert.AreEqual(72, farmhouse.Depth);
            Assert.AreEqual(2, farmhouse.FloorCount);
            Assert.Greater(farmhouse.FrontWindows.Count, 0,
                "the farmhouse preset should exercise detailed facade configuration");

            Assert.AreEqual(48, townhouse.Width);
            Assert.AreEqual(64, townhouse.Depth);
            Assert.AreEqual(3, townhouse.FloorCount);
            Assert.AreEqual(9, townhouse.Palette.Resolve(StructureMaterialRole.PrimaryWall));

            CollectionAssert.AreNotEqual(cottageProgram, farmhouseProgram,
                "materially different house presets must not collapse to the same shape program");
            CollectionAssert.AreNotEqual(cottageProgram, townhouseProgram,
                "materially different house presets must not collapse to the same shape program");
            CollectionAssert.AreNotEqual(farmhouseProgram, townhouseProgram,
                "materially different house presets must not collapse to the same shape program");
        }
    }
}
