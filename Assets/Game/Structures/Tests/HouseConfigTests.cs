using Game.Structures.Api;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class HouseConfigTests
    {
        [Test]
        public void HouseConfigExposesSharedShellDimensionsFoundationAndPalette()
        {
            var config = CottageShell();

            Assert.IsTrue(config.IsWellFormed);
            Assert.AreEqual(64, config.Width);
            Assert.AreEqual(64, config.Depth);
            Assert.AreEqual(1, config.FloorCount);
            Assert.AreEqual(32, config.FloorHeight);
            Assert.AreEqual(4, config.WallThickness);
            Assert.AreEqual(8, config.FoundationDepth);
            Assert.AreEqual(StructureFoundationStyle.Slab, config.FoundationStyle);
            Assert.AreEqual(32, config.TotalWallHeight);
            Assert.AreEqual(11, config.Palette.Resolve(StructureMaterialRole.Foundation));
            Assert.AreEqual(12, config.Palette.Resolve(StructureMaterialRole.Roof));
        }

        [Test]
        public void ExteriorWallHeightMustSpanConfiguredLevels()
        {
            var config = CottageShell();
            config.Levels.FloorCount = 2;

            Assert.IsFalse(config.IsWellFormed,
                "wall height must be updated when the number of house levels changes");

            config.ExteriorWall.Height = 64;
            Assert.IsTrue(config.IsWellFormed);
        }

        [Test]
        public void ExteriorWallLengthTracksPrimaryHouseWidth()
        {
            var config = CottageShell();
            config.ExteriorWall.Length = 60;

            Assert.IsFalse(config.IsWellFormed);
        }

        private static HouseConfig CottageShell()
        {
            return new HouseConfig
            {
                Footprint = new StructureFootprintConfig
                {
                    Primary = new StructureFootprintRect(int2.zero, new int2(64, 64)),
                    BasePlane = BasePlaneRule.LowestGround,
                    FoundationStyle = StructureFoundationStyle.Slab,
                    FoundationDepth = 8,
                    FoundationMaterial = StructureMaterialRole.Foundation,
                },
                ExteriorWall = new StructureWallRunConfig
                {
                    Length = 64,
                    Height = 32,
                    Thickness = 4,
                    PrimaryMaterial = StructureMaterialRole.PrimaryWall,
                    CornerBehavior = StructureWallCornerBehavior.Overlap,
                },
                Levels = new FloorLevelConfig
                {
                    FloorCount = 1,
                    LevelHeight = 32,
                    SlabThickness = 2,
                    MinimumLevelHeightDelta = 0,
                    MaximumLevelHeightDelta = 0,
                    SlabMaterialRole = StructureMaterialRole.Floor,
                },
                Palette = new StructureMaterialPalette
                {
                    Foundation = 11,
                    PrimaryWall = 11,
                    Roof = 12,
                    Floor = 13,
                    Trim = 14,
                },
            };
        }
    }
}
