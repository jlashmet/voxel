using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CastlePresetTests
    {
        [Test]
        public void KeepOnlyAndWalledCastleSelectMateriallyDifferentBuildStages()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 0x123456u);
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CastlePresetConfig keep = CastlePresets.KeepOnly(in plan, in palette);
            CastlePresetConfig walled = CastlePresets.WalledCastle(in plan, in palette);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(keep.IsWellFormed);
                Assert.IsTrue(walled.IsWellFormed);
                Assert.IsTrue(keep.Stages.Site);
                Assert.IsTrue(keep.Stages.Keep);
                Assert.IsFalse(keep.Stages.CurtainWalls);
                Assert.IsFalse(keep.Stages.CornerTowers);
                Assert.IsFalse(keep.Stages.Gatehouse);
                Assert.IsFalse(keep.Stages.Courtyard);
                Assert.IsFalse(keep.Stages.Dungeon);
                Assert.IsFalse(keep.Stages.Landscape);

                Assert.IsTrue(walled.Stages.Site);
                Assert.IsTrue(walled.Stages.CurtainWalls);
                Assert.IsTrue(walled.Stages.CornerTowers);
                Assert.IsTrue(walled.Stages.Gatehouse);
                Assert.IsTrue(walled.Stages.Courtyard);
                Assert.IsTrue(walled.Stages.Keep);
                Assert.IsFalse(walled.Stages.Dungeon);
                Assert.IsFalse(walled.Stages.Landscape);
            });
        }

        [Test]
        public void CompatibilityProjectionIsDeterministicAndKeepFootprintFitsCurtain()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(90, 20, -40), 0x778899u);
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CastlePresetConfig a = CastlePresets.Compatibility(in plan, in palette);
            CastlePresetConfig b = CastlePresets.Compatibility(in plan, in palette);

            StructureFootprintRect keep = a.Components.KeepFoundation.Primary;
            int2 half = a.Curtain.RectangularHalfExtents;
            Assert.Multiple(() =>
            {
                Assert.AreEqual(a.Components.KeepWalls.Length, b.Components.KeepWalls.Length);
                Assert.AreEqual(a.Components.Gatehouse.Width, b.Components.Gatehouse.Width);
                Assert.AreEqual(a.Components.Courtyard.OpenSpace.Area.Min,
                    b.Components.Courtyard.OpenSpace.Area.Min);
                Assert.AreEqual(a.Components.UndergroundAttachments.Cave.LocalPosition,
                    b.Components.UndergroundAttachments.Cave.LocalPosition);

                Assert.That(keep.Min.x, Is.GreaterThan(-half.x));
                Assert.That(keep.Min.y, Is.GreaterThan(-half.y));
                Assert.That(keep.Min.x + keep.Size.x, Is.LessThan(half.x));
                Assert.That(keep.Min.y + keep.Size.y, Is.LessThan(half.y));
            });
        }

        [Test]
        public void GatehouseSeamAndSemanticAttachmentsAlignWithSouthCurtain()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(120, 30, 240), 0xABC123u);
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CastlePresetConfig preset = CastlePresets.WalledCastle(in plan, in palette);
            CastleGatehouseConfig gatehouse = preset.Components.Gatehouse;
            CastleUndergroundAttachmentConfig underground = preset.Components.UndergroundAttachments;

            int southCurtainWorldZ = plan.Centre.z - preset.Curtain.RectangularHalfExtents.y;
            int gatehouseCentreWorldZ = plan.Centre.z - plan.BaileyHalfZ;
            int3 road = gatehouse.ResolveRoadAnchor(in plan);
            int3 cave = underground.ResolveCave(in plan);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(southCurtainWorldZ, gatehouseCentreWorldZ,
                    "Compatibility gatehouse must share the south curtain seam.");
                Assert.That(gatehouse.GateOpening.Width, Is.LessThan(gatehouse.Width));
                Assert.That(road.z, Is.LessThan(southCurtainWorldZ));
                Assert.AreEqual(StructureAttachmentKind.Road, gatehouse.RoadAnchor.Kind);
                Assert.AreEqual(StructureAttachmentKind.Basement, underground.KeepBasement.Kind);
                Assert.AreEqual(StructureAttachmentKind.Dungeon, underground.Dungeon.Kind);
                Assert.AreEqual(StructureAttachmentKind.Crypt, underground.GatehouseCrypt.Kind);
                Assert.AreEqual(StructureAttachmentKind.Cave, underground.Cave.Kind);
                Assert.That(cave.y, Is.LessThan(plan.Centre.y + plan.PlateauHeight));
            });
        }
    }
}
