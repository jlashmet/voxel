using NUnit.Framework;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.Features
{
    public sealed class HousePresetTests
    {
        [Test]
        public void CompactCottageAndFarmhouseUseSameCompilerButProduceDifferentPrograms()
        {
            HouseConfig compact = HousePresetLibrary.CompactCottage(7, 11);
            HouseConfig farmhouse = HousePresetLibrary.Farmhouse(7, 9, 11);

            int[] compactProgram = HouseProgramCompiler.BuildCompatibilityProgram(
                in compact,
                mainDoorAnchorIndex: 0,
                hearthAnchorIndex: 1);
            int[] farmhouseProgram = HouseProgramCompiler.BuildCompatibilityProgram(
                in farmhouse,
                mainDoorAnchorIndex: 0,
                hearthAnchorIndex: 1);

            Assert.AreEqual(48, compact.Width);
            Assert.AreEqual(48, compact.Depth);
            Assert.AreEqual(1, compact.FloorCount);

            Assert.AreEqual(88, farmhouse.Width);
            Assert.AreEqual(72, farmhouse.Depth);
            Assert.AreEqual(2, farmhouse.FloorCount);
            Assert.AreEqual(9, farmhouse.Palette.Resolve(StructureMaterialRole.PrimaryWall));

            CollectionAssert.AreNotEqual(compactProgram, farmhouseProgram,
                "materially different house presets must not collapse to the same shape program");
        }
    }
}
