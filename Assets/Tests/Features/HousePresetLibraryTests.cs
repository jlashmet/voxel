using System.Linq;
using NUnit.Framework;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.Features
{
    public sealed class HousePresetLibraryTests
    {
        [Test]
        public void CompactCabinAndFarmhouseUseOneConfigTypeButProduceDifferentPrograms()
        {
            HouseConfig cabin = HouseStylePresets.CompactCabin(
                wallMaterial: 4,
                roofMaterial: 9);
            HouseConfig farmhouse = HouseStylePresets.Farmhouse(
                wallMaterial: 7,
                roofMaterial: 9);

            Assert.AreEqual(48, cabin.Width);
            Assert.AreEqual(40, cabin.Depth);
            Assert.AreEqual(1, cabin.FloorCount);

            Assert.AreEqual(96, farmhouse.Width);
            Assert.AreEqual(72, farmhouse.Depth);
            Assert.AreEqual(2, farmhouse.FloorCount);
            Assert.AreEqual(7, farmhouse.Palette.Resolve(StructureMaterialRole.PrimaryWall));

            int[] cabinProgram = HouseProgramCompiler.BuildCompatibilityProgram(
                in cabin,
                mainDoorAnchorIndex: 0,
                hearthAnchorIndex: 1);
            int[] farmhouseProgram = HouseProgramCompiler.BuildCompatibilityProgram(
                in farmhouse,
                mainDoorAnchorIndex: 0,
                hearthAnchorIndex: 1);

            Assert.IsFalse(cabinProgram.SequenceEqual(farmhouseProgram),
                "materially different house presets compiled to the same shape program");
        }

        [Test]
        public void PresetsRemainOrdinaryOverrideableHouseConfigs()
        {
            HouseConfig farmhouse = HouseStylePresets.Farmhouse(2, 3);
            farmhouse.Roof.EaveOverhang = 5;
            farmhouse.FrontDoors.Opening.FrameThickness = 2;
            farmhouse.Palette.Trim = 8;

            Assert.AreEqual(5, farmhouse.Roof.EaveOverhang);
            Assert.AreEqual(2, farmhouse.FrontDoors.Opening.FrameThickness);
            Assert.AreEqual(8, farmhouse.Palette.Resolve(StructureMaterialRole.Trim));
        }
    }
}
