using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class HouseConfigTests
    {
        [Test]
        public void CottageCompatibilityExposesCoreHouseControls()
        {
            HouseConfig config = HousePresets.CottageCompatibility(
                stoneMaterial: 7,
                woodMaterial: 11);

            Assert.AreEqual(64, config.Width);
            Assert.AreEqual(64, config.Depth);
            Assert.AreEqual(1, config.FloorCount);
            Assert.AreEqual(32, config.FloorHeight);
            Assert.AreEqual(4, config.WallThickness);
            Assert.AreEqual(StructureFoundationStyle.Slab, config.FoundationStyle);
            Assert.AreEqual(8, config.FoundationDepth);

            Assert.AreEqual(7, config.Palette.Resolve(StructureMaterialRole.Foundation));
            Assert.AreEqual(7, config.Palette.Resolve(StructureMaterialRole.PrimaryWall));
            Assert.AreEqual(7, config.Palette.Resolve(StructureMaterialRole.Floor));
            Assert.AreEqual(11, config.Palette.Resolve(StructureMaterialRole.Roof));
        }

        [Test]
        public void CoreHouseControlsRemainOverrideableThroughSharedComponents()
        {
            HouseConfig config = HousePresets.CottageCompatibility(1, 2);

            config.Footprint.Primary = new StructureFootprintRect(
                new Unity.Mathematics.int2(0, 0),
                new Unity.Mathematics.int2(80, 72));
            config.Floors.FloorCount = 3;
            config.Floors.LevelHeight = 36;
            config.Walls.Thickness = 6;
            config.Footprint.FoundationStyle = StructureFoundationStyle.Terraced;
            config.Footprint.FoundationDepth = 10;
            config.Footprint.MaxTerraceStep = 3;
            config.Palette.PrimaryWall = 9;

            Assert.AreEqual(80, config.Width);
            Assert.AreEqual(72, config.Depth);
            Assert.AreEqual(3, config.FloorCount);
            Assert.AreEqual(36, config.FloorHeight);
            Assert.AreEqual(6, config.WallThickness);
            Assert.AreEqual(StructureFoundationStyle.Terraced, config.FoundationStyle);
            Assert.AreEqual(10, config.FoundationDepth);
            Assert.AreEqual(9, config.Palette.Resolve(StructureMaterialRole.PrimaryWall));
        }
    }
}
