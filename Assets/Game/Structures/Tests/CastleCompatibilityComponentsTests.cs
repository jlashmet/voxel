using Game.Materials.Api;
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
            CastleComponentConfig components = CastleCompatibilityComponents.Resolve(in plan);

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

            Assert.AreEqual(StructureOpeningKind.Arch, components.MainGate.Kind);
            Assert.AreEqual(CastleLayout.FrontGateWidth, components.MainGate.Width);
            Assert.AreEqual(CastleLayout.FrontGateHeight, components.MainGate.Height);

            Assert.AreEqual(26, components.CurtainBattlements.MerlonWidth);
            Assert.AreEqual(18, components.CurtainBattlements.GapWidth);
            Assert.AreEqual(20, components.CurtainBattlements.MerlonHeight);
        }

        [Test]
        public void CompatibilityFootprintPreservesBaileyBoundsAndFoundationPolicy()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 17u);
            CastleComponentConfig components = CastleCompatibilityComponents.Resolve(in plan);

            StructureFootprintRect footprint = components.BaileyFootprint.Primary;
            Assert.AreEqual(new int2(-plan.BaileyHalfX, -plan.BaileyHalfZ), footprint.Min);
            Assert.AreEqual(
                new int2(plan.BaileyHalfX * 2, plan.BaileyHalfZ * 2),
                footprint.Size);
            Assert.AreEqual(BasePlaneRule.FixedAltitude, components.BaileyFootprint.BasePlane);
            Assert.AreEqual(StructureFoundationStyle.TerrainFill,
                components.BaileyFootprint.FoundationStyle);
            Assert.AreEqual(plan.CliffDrop, components.BaileyFootprint.FoundationDepth);
        }

        [Test]
        public void CompatibilityPalettePreservesLegacyCastleMaterials()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 27u);
            CastleComponentConfig components = CastleCompatibilityComponents.Resolve(in plan);

            Assert.AreEqual(GameMaterialIds.DarkStone,
                components.Palette.Resolve(StructureMaterialRole.Foundation));
            Assert.AreEqual(GameMaterialIds.Stone,
                components.Palette.Resolve(StructureMaterialRole.PrimaryWall));
            Assert.AreEqual(GameMaterialIds.DarkStone,
                components.Palette.Resolve(StructureMaterialRole.Trim));
            Assert.AreEqual(GameMaterialIds.Slate,
                components.Palette.Resolve(StructureMaterialRole.Roof));
            Assert.AreEqual(GameMaterialIds.Empty,
                components.Palette.Resolve(StructureMaterialRole.Opening));
        }

        [Test]
        public void SamePlanProducesIdenticalSharedComponentPolicy()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(-100, 12, 400), 99u);
            CastleComponentConfig first = CastleCompatibilityComponents.Resolve(in plan);
            CastleComponentConfig second = CastleCompatibilityComponents.Resolve(in plan);

            Assert.AreEqual(first.BaileyFootprint.Primary.Min, second.BaileyFootprint.Primary.Min);
            Assert.AreEqual(first.BaileyFootprint.Primary.Size, second.BaileyFootprint.Primary.Size);
            Assert.AreEqual(first.CurtainWallX.Length, second.CurtainWallX.Length);
            Assert.AreEqual(first.CurtainWallZ.Length, second.CurtainWallZ.Length);
            Assert.AreEqual(first.CornerTowers.Radius, second.CornerTowers.Radius);
            Assert.AreEqual(first.MainGate.Width, second.MainGate.Width);
            Assert.AreEqual(first.CurtainBattlements.MerlonWidth,
                second.CurtainBattlements.MerlonWidth);
        }
    }
}
