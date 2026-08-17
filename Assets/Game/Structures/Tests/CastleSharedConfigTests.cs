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
            CastleConfig config = CastlePresets.Compatibility(in plan);
            CastlePlan resolved = config.ResolvePlan();

            Assert.Multiple(() =>
            {
                Assert.IsTrue(config.IsWellFormed);

                Assert.AreEqual(plan.KeepHalfX * 2 + 12, config.KeepFoundation.Primary.Size.x);
                Assert.AreEqual(plan.KeepHalfZ * 2 + 12, config.KeepFoundation.Primary.Size.y);
                Assert.AreEqual(30, config.KeepFoundation.FoundationDepth);
                Assert.AreEqual(4, config.KeepFoundationTopOffset);

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
                Assert.AreEqual(2, config.GateTowers.Count);
                Assert.AreEqual(plan.GateTowerRadius, config.GateTowers.Radius);
                Assert.AreEqual(plan.GateTowerHeight, config.GateTowers.Height);

                Assert.AreEqual(CastleLayout.FrontGateWidth, config.MainGate.Width);
                Assert.AreEqual(CastleLayout.FrontGateHeight, config.MainGate.Height);
                Assert.AreEqual(26, config.CurtainBattlements.MerlonWidth);
                Assert.AreEqual(18, config.CurtainBattlements.GapWidth);
                Assert.AreEqual(20, config.CurtainBattlements.MerlonHeight);

                Assert.AreEqual(plan.BaileyHalfX, resolved.BaileyHalfX);
                Assert.AreEqual(plan.BaileyHalfZ, resolved.BaileyHalfZ);
                Assert.AreEqual(plan.WallHeight, resolved.WallHeight);
                Assert.AreEqual(plan.WallThickness, resolved.WallThickness);
                Assert.AreEqual(plan.TowerRadius, resolved.TowerRadius);
                Assert.AreEqual(plan.GateTowerRadius, resolved.GateTowerRadius);
            });
        }

        [Test]
        public void CompatibilityProjectionIsPureForSamePlan()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 99u);
            CastleConfig a = CastlePresets.Compatibility(in plan);
            CastleConfig b = CastlePresets.Compatibility(in plan);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(a.KeepFoundation.Primary.Min, b.KeepFoundation.Primary.Min);
                Assert.AreEqual(a.KeepFoundation.Primary.Size, b.KeepFoundation.Primary.Size);
                Assert.AreEqual(a.CurtainWallX.Length, b.CurtainWallX.Length);
                Assert.AreEqual(a.CurtainWallX.Height, b.CurtainWallX.Height);
                Assert.AreEqual(a.CornerTowers.Radius, b.CornerTowers.Radius);
                Assert.AreEqual(a.CornerTowers.Height, b.CornerTowers.Height);
                Assert.AreEqual(a.MainGate.Width, b.MainGate.Width);
                Assert.AreEqual(a.CurtainBattlements.MerlonWidth, b.CurtainBattlements.MerlonWidth);
            });
        }
    }
}
