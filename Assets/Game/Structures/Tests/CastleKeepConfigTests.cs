using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CastleKeepConfigTests
    {
        [Test]
        public void CompatibilityPresetExposesLegacyKeepDimensionsAndMaterials()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(80, 12, -140), 0xBEEFu);
            CastleKeepConfig keep = CastleKeepPresets.Compatibility(in plan);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(keep.IsWellFormed);
                Assert.AreEqual(plan.KeepHalfX * 2, keep.Width);
                Assert.AreEqual(plan.KeepHalfZ * 2, keep.Depth);
                Assert.AreEqual(plan.KeepHeight, keep.Height);
                Assert.AreEqual(8, keep.WallThickness);
                Assert.AreEqual(plan.Floors, keep.FloorCount);
                Assert.AreEqual(plan.FloorHeight, keep.FloorHeight);

                Assert.AreEqual(RoofStyle.Gable, keep.Roof.Style);
                Assert.AreEqual(30, keep.MainEntrance.Width);
                Assert.AreEqual(34, keep.MainEntrance.Height);
                Assert.AreEqual(16, keep.Windows.Width);
                Assert.AreEqual(12, keep.Windows.BottomOffset);

                Assert.AreEqual(GameMaterialIds.DarkStone,
                    keep.Palette.Resolve(StructureMaterialRole.Foundation));
                Assert.AreEqual(GameMaterialIds.Stone,
                    keep.Palette.Resolve(StructureMaterialRole.PrimaryWall));
                Assert.AreEqual(GameMaterialIds.Wood,
                    keep.Palette.Resolve(StructureMaterialRole.Floor));
                Assert.AreEqual(GameMaterialIds.Tile,
                    keep.Palette.Resolve(StructureMaterialRole.Roof));
                Assert.AreEqual(GameMaterialIds.LitWindow,
                    keep.Palette.Resolve(StructureMaterialRole.Glass));
            });
        }

        [Test]
        public void SharedKeepControlsCanBeOverriddenWithoutChangingConfigType()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 73u);
            CastleKeepConfig keep = CastleKeepPresets.Compatibility(in plan);

            keep.WallX.Length = 240;
            keep.WallZ.Length = 168;
            keep.WallX.Height = 176;
            keep.WallZ.Height = 176;
            keep.WallX.Thickness = 10;
            keep.WallZ.Thickness = 10;
            keep.Levels.FloorCount = 4;
            keep.Levels.LevelHeight = 44;
            keep.Roof.Style = RoofStyle.Hip;
            keep.Roof.PitchRise = 2;
            keep.Roof.PitchRun = 3;
            keep.MainEntrance.Width = 34;
            keep.Windows.Width = 18;
            keep.Palette.PrimaryWall = GameMaterialIds.MasonryLarge;

            Assert.Multiple(() =>
            {
                Assert.IsTrue(keep.IsWellFormed);
                Assert.AreEqual(240, keep.Width);
                Assert.AreEqual(168, keep.Depth);
                Assert.AreEqual(176, keep.Height);
                Assert.AreEqual(10, keep.WallThickness);
                Assert.AreEqual(4, keep.FloorCount);
                Assert.AreEqual(44, keep.FloorHeight);
                Assert.AreEqual(RoofStyle.Hip, keep.Roof.Style);
                Assert.AreEqual(34, keep.MainEntrance.Width);
                Assert.AreEqual(18, keep.Windows.Width);
                Assert.AreEqual(GameMaterialIds.MasonryLarge,
                    keep.Palette.Resolve(StructureMaterialRole.PrimaryWall));
            });
        }
    }
}
