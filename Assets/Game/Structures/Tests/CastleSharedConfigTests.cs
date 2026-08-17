using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CastleSharedConfigTests
    {
        [Test]
        public void CompatibilityProjectionPreservesLegacyPlanDimensions()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(120, 40, -80), 0x12345678u);
            CastleComponentConfig config = Resolve(in plan);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(config.IsWellFormed);

                Assert.AreEqual(plan.BaileyHalfX * 2, config.BaileyFootprint.Primary.Size.x);
                Assert.AreEqual(plan.BaileyHalfZ * 2, config.BaileyFootprint.Primary.Size.y);
                Assert.AreEqual(plan.KeepHalfX * 2 + 12, config.KeepFoundation.Primary.Size.x);
                Assert.AreEqual(plan.KeepHalfZ * 2 + 12, config.KeepFoundation.Primary.Size.y);
                Assert.AreEqual(30, config.KeepFoundation.FoundationDepth);
                Assert.AreEqual(4, config.KeepFoundationTopOffset);

                Assert.AreEqual(plan.KeepHalfX * 2, config.KeepWidth);
                Assert.AreEqual(plan.KeepHalfZ * 2, config.KeepDepth);
                Assert.AreEqual(plan.KeepHeight, config.KeepHeight);
                Assert.AreEqual(8, config.KeepWallThickness);
                Assert.AreEqual(plan.Floors, config.KeepLevelCount);
                Assert.AreEqual(plan.FloorHeight, config.KeepFloors.LevelHeight);
                Assert.AreEqual(3, config.KeepFloors.SlabThickness);
                Assert.AreEqual(RoofStyle.Gable, config.KeepRoof.Style);
                Assert.AreEqual(RoofAxis.X, config.KeepRoof.RidgeAxis);
                Assert.AreEqual(70, config.KeepRoof.PitchRise);
                Assert.AreEqual(StructureMaterialRole.Roof, config.KeepRoof.MaterialRole);
                Assert.AreEqual(24, config.KeepParapet.MerlonWidth);
                Assert.AreEqual(20, config.KeepParapet.MerlonHeight);
                Assert.AreEqual(20, config.KeepParapet.GapWidth);
                Assert.AreEqual(StructureOpeningKind.Arch, config.KeepEntrance.Kind);
                Assert.AreEqual(30, config.KeepEntrance.Width);
                Assert.AreEqual(34, config.KeepEntrance.Height);
                Assert.AreEqual(StructureOpeningKind.Window, config.KeepWindow.Kind);
                Assert.AreEqual(16, config.KeepWindow.Width);
                Assert.AreEqual(12, config.KeepWindow.BottomOffset);
                Assert.AreEqual(4, config.KeepWindow.HeightVariation);

                Assert.AreEqual(plan.BaileyHalfX * 2, config.CurtainWallX.Length);
                Assert.AreEqual(plan.BaileyHalfZ * 2, config.CurtainWallZ.Length);
                Assert.AreEqual(plan.WallHeight, config.CurtainWallX.Height);
                Assert.AreEqual(plan.WallThickness, config.CurtainWallX.Thickness);
                Assert.AreEqual(90, config.CurtainWallX.RepetitionSpacing);
                Assert.AreEqual(40, config.CurtainWallX.RepetitionOffset);

                Assert.AreEqual(StructureTowerShape.Round, config.CornerTowers.Shape);
                Assert.AreEqual(StructureTowerPlacement.Corners, config.CornerTowers.Placement);
                Assert.AreEqual(4, config.CornerTowers.Count);
                Assert.AreEqual(plan.TowerRadius, config.CornerTowers.Radius);
                Assert.AreEqual(plan.TowerHeight, config.CornerTowers.Height);
                Assert.AreEqual(StructureTowerPlacement.Explicit, config.GateTowers.Placement);
                Assert.AreEqual(2, config.GateTowers.Count);
                Assert.AreEqual(plan.GateTowerRadius, config.GateTowers.Radius);
                Assert.AreEqual(plan.GateTowerHeight, config.GateTowers.Height);

                Assert.AreEqual(StructureOpeningKind.Arch, config.MainGate.Kind);
                Assert.AreEqual(CastleLayout.FrontGateWidth, config.MainGate.Width);
                Assert.AreEqual(CastleLayout.FrontGateHeight, config.MainGate.Height);
                Assert.AreEqual(26, config.CurtainBattlements.MerlonWidth);
                Assert.AreEqual(18, config.CurtainBattlements.GapWidth);
                Assert.AreEqual(20, config.CurtainBattlements.MerlonHeight);
                Assert.AreEqual(18, config.GatehouseBattlements.MerlonWidth);
                Assert.AreEqual(18, config.GatehouseBattlements.MerlonHeight);
                Assert.AreEqual(12, config.GatehouseBattlements.GapWidth);

                Assert.AreEqual(GameMaterialIds.DarkStone,
                    config.Palette.Resolve(StructureMaterialRole.Foundation));
                Assert.AreEqual(GameMaterialIds.Stone,
                    config.Palette.Resolve(StructureMaterialRole.PrimaryWall));
                Assert.AreEqual(GameMaterialIds.Slate,
                    config.Palette.Resolve(StructureMaterialRole.Roof));
                Assert.AreEqual(GameMaterialIds.Empty,
                    config.Palette.Resolve(StructureMaterialRole.Opening));
            });
        }

        [Test]
        public void KeepControlsAreIndependentlyOverrideable()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 123u);
            CastleComponentConfig config = Resolve(in plan);

            config.KeepWalls.Length += 24;
            config.KeepWalls.Height += 16;
            config.KeepWalls.Thickness = 10;
            config.KeepDepth += 18;
            config.KeepFloors.FloorCount = 4;
            config.KeepFloors.LevelHeight = 44;
            config.KeepRoof.PitchRise = 48;
            config.KeepRoof.PitchRun = 72;
            config.KeepParapet.MerlonWidth = 20;
            config.KeepParapet.GapWidth = 14;
            config.KeepEntrance.Width = 34;
            config.KeepWindow.Width = 18;
            config.Palette.PrimaryWall = 19;

            Assert.Multiple(() =>
            {
                Assert.IsTrue(config.IsWellFormed);
                Assert.AreEqual(plan.KeepHalfX * 2 + 24, config.KeepWidth);
                Assert.AreEqual(plan.KeepHalfZ * 2 + 18, config.KeepDepth);
                Assert.AreEqual(plan.KeepHeight + 16, config.KeepHeight);
                Assert.AreEqual(10, config.KeepWallThickness);
                Assert.AreEqual(4, config.KeepLevelCount);
                Assert.AreEqual(44, config.KeepFloors.LevelHeight);
                Assert.AreEqual(48, config.KeepRoof.PitchRise);
                Assert.AreEqual(20, config.KeepParapet.MerlonWidth);
                Assert.AreEqual(34, config.KeepEntrance.Width);
                Assert.AreEqual(18, config.KeepWindow.Width);
                Assert.AreEqual(19, config.Palette.PrimaryWall);
            });
        }

        [Test]
        public void CompatibilityProjectionIsPureForSamePlan()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 99u);
            CastleComponentConfig a = Resolve(in plan);
            CastleComponentConfig b = Resolve(in plan);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(a.KeepFoundation.Primary.Min, b.KeepFoundation.Primary.Min);
                Assert.AreEqual(a.KeepFoundation.Primary.Size, b.KeepFoundation.Primary.Size);
                Assert.AreEqual(a.KeepWalls.Length, b.KeepWalls.Length);
                Assert.AreEqual(a.KeepDepth, b.KeepDepth);
                Assert.AreEqual(a.KeepWalls.Height, b.KeepWalls.Height);
                Assert.AreEqual(a.KeepFloors.FloorCount, b.KeepFloors.FloorCount);
                Assert.AreEqual(a.KeepRoof.Style, b.KeepRoof.Style);
                Assert.AreEqual(a.KeepRoof.PitchRise, b.KeepRoof.PitchRise);
                Assert.AreEqual(a.KeepParapet.MerlonWidth, b.KeepParapet.MerlonWidth);
                Assert.AreEqual(a.KeepEntrance.Width, b.KeepEntrance.Width);
                Assert.AreEqual(a.KeepWindow.Width, b.KeepWindow.Width);
                Assert.AreEqual(a.CurtainWallX.Length, b.CurtainWallX.Length);
                Assert.AreEqual(a.CurtainWallX.Height, b.CurtainWallX.Height);
                Assert.AreEqual(a.CornerTowers.Radius, b.CornerTowers.Radius);
                Assert.AreEqual(a.CornerTowers.Height, b.CornerTowers.Height);
                Assert.AreEqual(a.GateTowers.Radius, b.GateTowers.Radius);
                Assert.AreEqual(a.MainGate.Width, b.MainGate.Width);
                Assert.AreEqual(a.CurtainBattlements.MerlonWidth,
                    b.CurtainBattlements.MerlonWidth);
                Assert.AreEqual(a.Palette.PrimaryWall, b.Palette.PrimaryWall);
            });
        }

        private static CastleComponentConfig Resolve(in CastlePlan plan)
        {
            var palette = new StructureMaterialPalette
            {
                Foundation = GameMaterialIds.DarkStone,
                PrimaryWall = GameMaterialIds.Stone,
                SecondaryWall = GameMaterialIds.DarkStone,
                Trim = GameMaterialIds.DarkStone,
                Roof = GameMaterialIds.Slate,
                Floor = GameMaterialIds.Wood,
                Column = GameMaterialIds.Stone,
                Accent = GameMaterialIds.Gold,
                Underground = GameMaterialIds.DarkStone,
                Opening = GameMaterialIds.Empty,
                Glass = GameMaterialIds.LitWindow,
                Detail = GameMaterialIds.Cloth,
            };
            return CastleComponentPresets.Compatibility(in plan, in palette);
        }
    }
}
