using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CastleGatehouseConfigTests
    {
        [Test]
        public void CompatibilityPresetPreservesLegacyGatehouseAndRoadAnchor()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(120, 40, -80), 0x12345678u);
            CastleGatehouseConfig gatehouse = Resolve(in plan).Gatehouse;

            int3 expectedRoad = new(
                plan.Centre.x,
                plan.Centre.y + plan.PlateauHeight,
                plan.Centre.z - plan.BaileyHalfZ - plan.WallThickness - 149);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(gatehouse.IsWellFormed);
                Assert.AreEqual(108, gatehouse.Width);
                Assert.AreEqual(plan.WallThickness * 2, gatehouse.Depth);
                Assert.AreEqual(plan.WallHeight + 22, gatehouse.Height);
                Assert.AreEqual(54, gatehouse.TowerCentreOffset);
                Assert.AreEqual(38, gatehouse.LeftTowerHeightOffset);
                Assert.AreEqual(12, gatehouse.RightTowerHeightOffset);
                Assert.AreEqual(CastleLayout.FrontGateDepth, gatehouse.GateLeafDepth);
                Assert.AreEqual(2, gatehouse.GateLeafInset);

                Assert.AreEqual(StructureTowerPlacement.Explicit, gatehouse.FlankingTowers.Placement);
                Assert.AreEqual(2, gatehouse.FlankingTowers.Count);
                Assert.AreEqual(plan.GateTowerRadius, gatehouse.FlankingTowers.Radius);
                Assert.AreEqual(plan.GateTowerHeight, gatehouse.FlankingTowers.Height);

                Assert.AreEqual(StructureOpeningKind.Arch, gatehouse.GateOpening.Kind);
                Assert.AreEqual(CastleLayout.FrontGateWidth, gatehouse.GateOpening.Width);
                Assert.AreEqual(CastleLayout.FrontGateHeight, gatehouse.GateOpening.Height);
                Assert.AreEqual(1, gatehouse.GateOpening.BottomOffset);

                Assert.AreEqual(StructureOpeningKind.Arch, gatehouse.PortcullisOpening.Kind);
                Assert.AreEqual(CastleLayout.FrontGateWidth + 4, gatehouse.PortcullisOpening.Width);
                Assert.AreEqual(CastleLayout.FrontGateHeight + 14, gatehouse.PortcullisOpening.Height);
                Assert.AreEqual(0, gatehouse.PortcullisOpening.BottomOffset);

                Assert.AreEqual(8, gatehouse.Battlements.ParapetThickness);
                Assert.AreEqual(18, gatehouse.Battlements.MerlonWidth);
                Assert.AreEqual(18, gatehouse.Battlements.MerlonHeight);
                Assert.AreEqual(12, gatehouse.Battlements.GapWidth);

                Assert.AreEqual(StructureAttachmentKind.Road, gatehouse.RoadAnchor.Kind);
                Assert.AreEqual(Facing.South, gatehouse.RoadAnchor.Facing);
                Assert.IsFalse(gatehouse.RoadAnchor.SnapToGround);
                Assert.AreEqual(expectedRoad, gatehouse.ResolveRoadAnchor(in plan));
            });
        }

        [Test]
        public void DetailedGatehouseControlsAreIndependentlyOverrideable()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 0x91u);
            CastleGatehouseConfig gatehouse = Resolve(in plan).Gatehouse;

            gatehouse.Width = 140;
            gatehouse.Depth = 48;
            gatehouse.Height = 128;
            gatehouse.TowerCentreOffset = 62;
            gatehouse.LeftTowerHeightOffset = 20;
            gatehouse.RightTowerHeightOffset = -8;
            gatehouse.GateLeafDepth = 6;
            gatehouse.GateLeafInset = 4;
            gatehouse.FlankingTowers.Radius += 3;
            gatehouse.FlankingTowers.Height += 11;
            gatehouse.GateOpening.Width = 52;
            gatehouse.GateOpening.Height = 64;
            gatehouse.PortcullisOpening.Width = 58;
            gatehouse.PortcullisOpening.Height = 80;
            gatehouse.Battlements.MerlonWidth = 20;
            gatehouse.Battlements.GapWidth = 14;
            gatehouse.RoadAnchor.LocalPosition = new int3(6, plan.PlateauHeight, -260);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(gatehouse.IsWellFormed);
                Assert.AreEqual(140, gatehouse.Width);
                Assert.AreEqual(48, gatehouse.Depth);
                Assert.AreEqual(128, gatehouse.Height);
                Assert.AreEqual(62, gatehouse.TowerCentreOffset);
                Assert.AreEqual(20, gatehouse.LeftTowerHeightOffset);
                Assert.AreEqual(-8, gatehouse.RightTowerHeightOffset);
                Assert.AreEqual(6, gatehouse.GateLeafDepth);
                Assert.AreEqual(4, gatehouse.GateLeafInset);
                Assert.AreEqual(plan.GateTowerRadius + 3, gatehouse.FlankingTowers.Radius);
                Assert.AreEqual(plan.GateTowerHeight + 11, gatehouse.FlankingTowers.Height);
                Assert.AreEqual(52, gatehouse.GateOpening.Width);
                Assert.AreEqual(58, gatehouse.PortcullisOpening.Width);
                Assert.AreEqual(20, gatehouse.Battlements.MerlonWidth);
                Assert.AreEqual(plan.Centre + gatehouse.RoadAnchor.LocalPosition,
                    gatehouse.ResolveRoadAnchor(in plan));
            });
        }

        [Test]
        public void ValidationRejectsInvalidGatehouseComposition()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 0x33u);
            CastleGatehouseConfig valid = Resolve(in plan).Gatehouse;

            CastleGatehouseConfig invalidPortcullis = valid;
            invalidPortcullis.PortcullisOpening.Width = valid.GateOpening.Width - 1;

            CastleGatehouseConfig invalidRoad = valid;
            invalidRoad.RoadAnchor.Kind = StructureAttachmentKind.Extension;

            CastleGatehouseConfig invalidTowers = valid;
            invalidTowers.FlankingTowers.Count = 1;

            CastleGatehouseConfig invalidLeaf = valid;
            invalidLeaf.GateLeafInset = invalidLeaf.Depth;

            CastleGatehouseConfig invalidTowerHeight = valid;
            invalidTowerHeight.LeftTowerHeightOffset = -invalidTowerHeight.FlankingTowers.Height;

            Assert.Multiple(() =>
            {
                Assert.IsFalse(invalidPortcullis.IsWellFormed);
                Assert.IsFalse(invalidRoad.IsWellFormed);
                Assert.IsFalse(invalidTowers.IsWellFormed);
                Assert.IsFalse(invalidLeaf.IsWellFormed);
                Assert.IsFalse(invalidTowerHeight.IsWellFormed);
            });
        }

        private static CastleComponentConfig Resolve(in CastlePlan plan)
        {
            StructureMaterialPalette palette = default;
            return CastleComponentPresets.Compatibility(in plan, in palette);
        }
    }
}
