using System.Linq;
using NUnit.Framework;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.Features
{
    public sealed class HouseStylePresetTests
    {
        [Test]
        public void PresetsUseOneConfigTypeButChooseMateriallyDifferentGeometry()
        {
            HouseConfig cottage = HousePresets.CottageCompatibility(3, 7);
            HouseConfig cabin = HouseStylePresets.CompactCabin(3, 7);
            HouseConfig farmhouse = HouseStylePresets.Farmhouse(3, 7);

            Assert.AreEqual(64, cottage.Width);
            Assert.AreEqual(48, cabin.Width);
            Assert.AreEqual(96, farmhouse.Width);
            Assert.AreEqual(64, cottage.Depth);
            Assert.AreEqual(40, cabin.Depth);
            Assert.AreEqual(72, farmhouse.Depth);
            Assert.AreEqual(1, cabin.FloorCount);
            Assert.AreEqual(2, farmhouse.FloorCount);
            Assert.AreEqual(2, cabin.FrontWindows.Count);
            Assert.AreEqual(4, farmhouse.FrontWindows.Count);
            Assert.AreEqual(1, farmhouse.RearDoors.Count);

            int[] cottageProgram = HouseProgramCompiler.BuildCompatibilityProgram(in cottage, 0, 1);
            int[] cabinProgram = HouseProgramCompiler.BuildCompatibilityProgram(in cabin, 0, 1);
            int[] farmhouseProgram = HouseProgramCompiler.BuildCompatibilityProgram(in farmhouse, 0, 1);

            Assert.IsFalse(cottageProgram.SequenceEqual(cabinProgram));
            Assert.IsFalse(cottageProgram.SequenceEqual(farmhouseProgram));
            Assert.IsFalse(cabinProgram.SequenceEqual(farmhouseProgram));
        }

        [Test]
        public void SamePresetInputsCompileDeterministically()
        {
            HouseConfig first = HouseStylePresets.Farmhouse(5, 9);
            HouseConfig second = HouseStylePresets.Farmhouse(5, 9);

            int[] firstProgram = HouseProgramCompiler.BuildCompatibilityProgram(in first, 2, 3);
            int[] secondProgram = HouseProgramCompiler.BuildCompatibilityProgram(in second, 2, 3);

            CollectionAssert.AreEqual(firstProgram, secondProgram);
        }
    }
}
