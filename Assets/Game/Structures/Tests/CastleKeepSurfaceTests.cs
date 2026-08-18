using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CastleKeepSurfaceTests
    {
        [Test]
        public void CompatibilityPresetExposesCompleteKeepSurface()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(17, 9, -23), 0xC451E123u);
            CastleComponentConfig config = CastleCompatibilityComponents.Resolve(in plan);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(config.IsWellFormed);
                Assert.AreEqual(plan.KeepHalfX * 2, config.KeepWidth);
                Assert.AreEqual(plan.KeepHalfZ * 2, config.KeepDepth);
                Assert.AreEqual(plan.KeepHeight, config.KeepHeight);
                Assert.AreEqual(8, config.KeepWallThickness);
                Assert.AreEqual(plan.Floors, config.KeepLevelCount);
                Assert.AreEqual(plan.FloorHeight, config.KeepFloors.LevelHeight);

                Assert.AreEqual(RoofStyle.Gable, config.KeepRoof.Style);
                Assert.Greater(config.KeepRoof.PitchRise, 0);
                Assert.Greater(config.KeepRoof.PitchRun, 0);
                Assert.Greater(config.KeepParapet.ParapetHeight, 0);

                Assert.AreEqual(StructureOpeningKind.Arch, config.KeepEntrance.Kind);
                Assert.Greater(config.KeepEntrance.Width, 0);
                Assert.Greater(config.KeepEntrance.Height, 0);
                Assert.AreEqual(StructureOpeningKind.Window, config.KeepWindow.Kind);
                Assert.Greater(config.KeepWindow.Width, 0);
                Assert.Greater(config.KeepWindow.Height, 0);

                Assert.AreNotEqual(0, config.Palette.PrimaryWall);
                Assert.AreNotEqual(0, config.Palette.Roof);
            });
        }

        [Test]
        public void KeepControlsCanChangeWithoutMutatingUnrelatedCastleControls()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 0x42u);
            CastleComponentConfig baseline = CastleCompatibilityComponents.Resolve(in plan);

            CastleComponentConfig changed = baseline;
            changed.KeepWalls.Length += 16;
            changed.KeepDepth += 12;
            changed.KeepWalls.Height += 8;
            changed.KeepFloors.FloorCount += 1;
            changed.KeepRoof.PitchRise += 1;
            changed.KeepParapet.MerlonWidth += 2;
            changed.KeepEntrance.Width += 2;
            changed.KeepWindow.Height += 1;
            changed.Palette.PrimaryWall = (byte)(baseline.Palette.PrimaryWall + 1);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(baseline.CurtainWallX.Length, changed.CurtainWallX.Length);
                Assert.AreEqual(baseline.CurtainWallZ.Length, changed.CurtainWallZ.Length);
                Assert.AreEqual(baseline.CornerTowers.Radius, changed.CornerTowers.Radius);
                Assert.AreEqual(baseline.GateTowers.Radius, changed.GateTowers.Radius);
                Assert.AreEqual(baseline.MainGate.Width, changed.MainGate.Width);
                Assert.AreEqual(baseline.CurtainBattlements.MerlonWidth,
                    changed.CurtainBattlements.MerlonWidth);

                Assert.AreNotEqual(baseline.KeepWidth, changed.KeepWidth);
                Assert.AreNotEqual(baseline.KeepDepth, changed.KeepDepth);
                Assert.AreNotEqual(baseline.KeepHeight, changed.KeepHeight);
                Assert.AreNotEqual(baseline.KeepLevelCount, changed.KeepLevelCount);
                Assert.AreNotEqual(baseline.KeepRoof.PitchRise, changed.KeepRoof.PitchRise);
                Assert.AreNotEqual(baseline.KeepParapet.MerlonWidth, changed.KeepParapet.MerlonWidth);
                Assert.AreNotEqual(baseline.KeepEntrance.Width, changed.KeepEntrance.Width);
                Assert.AreNotEqual(baseline.KeepWindow.Height, changed.KeepWindow.Height);
                Assert.AreNotEqual(baseline.Palette.PrimaryWall, changed.Palette.PrimaryWall);
            });
        }
    }
}
