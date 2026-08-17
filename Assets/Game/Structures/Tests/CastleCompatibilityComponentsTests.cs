using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CastleCompatibilityComponentsTests
    {
        [Test]
        public void PlannerOutputMapsToWellFormedSharedComponents()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(320, 24, -180), 0xC0FFEEu);
            CastleCompatibilityComponents components =
                CastleCompatibilityPreset.FromPlan(in plan);

            Assert.IsTrue(components.IsWellFormed);

            Assert.AreEqual(plan.BaileyHalfX * 2, components.CurtainWallX.Length);
            Assert.AreEqual(plan.BaileyHalfZ * 2, components.CurtainWallZ.Length);
            Assert.AreEqual(plan.WallHeight, components.CurtainWallX.Height);
            Assert.AreEqual(plan.WallThickness, components.CurtainWallX.Thickness);

            Assert.AreEqual(StructureTowerShape.Round, components.CornerTowers.Shape);
            Assert.AreEqual(StructureTowerPlacement.Corners, components.CornerTowers.Placement);
            Assert.AreEqual(4, components.CornerTowers.Count);
            Assert.AreEqual(plan.TowerRadius, components.CornerTowers.Radius);
            Assert.AreEqual(plan.TowerHeight, components.CornerTowers.Height);

            Assert.AreEqual(2, components.GateTowers.Count);
            Assert.AreEqual(plan.GateTowerRadius, components.GateTowers.Radius);
            Assert.AreEqual(plan.GateTowerHeight, components.GateTowers.Height);

            Assert.AreEqual(StructureOpeningKind.Door, components.MainGate.Kind);
            Assert.AreEqual(CastleLayout.FrontGateWidth, components.MainGate.Width);
            Assert.AreEqual(CastleLayout.FrontGateHeight, components.MainGate.Height);

            Assert.AreEqual(26, components.CurtainBattlements.MerlonWidth);
            Assert.AreEqual(18, components.CurtainBattlements.GapWidth);
            Assert.AreEqual(20, components.CurtainBattlements.MerlonHeight);
        }

        [Test]
        public void KeepCompatibilityFoundationPreservesHistoricalSupportBounds()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 17u);
            CastleCompatibilityComponents components =
                CastleCompatibilityPreset.FromPlan(in plan);

            StructureFootprintRect footprint = components.KeepFoundation.Primary;
            Assert.AreEqual(new int2(-6, -6), footprint.Min);
            Assert.AreEqual(
                new int2(plan.KeepHalfX * 2 + 12, plan.KeepHalfZ * 2 + 12),
                footprint.Size);
            Assert.AreEqual(StructureFoundationStyle.Slab,
                components.KeepFoundation.FoundationStyle);
            Assert.AreEqual(30, components.KeepFoundation.FoundationDepth);
            Assert.AreEqual(8, components.KeepWallX.Thickness);
            Assert.AreEqual(8, components.KeepWallZ.Thickness);
        }

        [Test]
        public void SamePlanProducesIdenticalSharedComponentPolicy()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(-100, 12, 400), 99u);
            CastleCompatibilityComponents first = CastleCompatibilityPreset.FromPlan(in plan);
            CastleCompatibilityComponents second = CastleCompatibilityPreset.FromPlan(in plan);

            Assert.AreEqual(first.KeepFoundation.Primary.Min, second.KeepFoundation.Primary.Min);
            Assert.AreEqual(first.KeepFoundation.Primary.Size, second.KeepFoundation.Primary.Size);
            Assert.AreEqual(first.CurtainWallX.Length, second.CurtainWallX.Length);
            Assert.AreEqual(first.CurtainWallZ.Length, second.CurtainWallZ.Length);
            Assert.AreEqual(first.CornerTowers.Radius, second.CornerTowers.Radius);
            Assert.AreEqual(first.GateTowers.Radius, second.GateTowers.Radius);
            Assert.AreEqual(first.MainGate.Width, second.MainGate.Width);
            Assert.AreEqual(first.CurtainBattlements.MerlonWidth,
                second.CurtainBattlements.MerlonWidth);
        }
    }
}
