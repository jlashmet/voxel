using Game.Structures.Api;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CastleUndergroundAttachmentTests
    {
        [Test]
        public void CompatibilityAnchorsMatchExistingCellarAndDungeonCoordinates()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(140, 32, -90), 0x55112233u);
            CastleUndergroundAttachmentConfig underground =
                CastleUndergroundAttachmentPresets.Compatibility(in plan);
            int3 trapdoor = CastleLayout.TrapdoorCentre(in plan);

            var expectedBasement = new int3(
                trapdoor.x,
                plan.Centre.y + plan.PlateauHeight - 46,
                trapdoor.z);
            var expectedDungeon = new int3(
                trapdoor.x,
                plan.Centre.y + plan.PlateauHeight - 46 - 120,
                trapdoor.z);
            var expectedCrypt = new int3(
                plan.Centre.x,
                plan.Centre.y + plan.PlateauHeight - 64,
                plan.Centre.z - plan.BaileyHalfZ);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(underground.IsWellFormed);
                Assert.AreEqual(StructureAttachmentKind.Basement, underground.KeepBasement.Kind);
                Assert.AreEqual(StructureAttachmentKind.Dungeon, underground.Dungeon.Kind);
                Assert.AreEqual(StructureAttachmentKind.Crypt, underground.GatehouseCrypt.Kind);
                Assert.AreEqual(expectedBasement, underground.ResolveKeepBasement(in plan));
                Assert.AreEqual(expectedDungeon, underground.ResolveDungeon(in plan));
                Assert.AreEqual(expectedCrypt, underground.ResolveGatehouseCrypt(in plan));
                Assert.AreEqual(Facing.Down, underground.KeepBasement.Facing);
                Assert.AreEqual(Facing.South, underground.Dungeon.Facing);
                Assert.AreEqual(Facing.Down, underground.GatehouseCrypt.Facing);
            });
        }

        [Test]
        public void UndergroundAttachmentSemanticsHaveStableNamesAndAppendedDungeonId()
        {
            Assert.Multiple(() =>
            {
                Assert.AreEqual(6, (byte)StructureAttachmentKind.Extension);
                Assert.AreEqual(7, (byte)StructureAttachmentKind.Dungeon);
                Assert.AreEqual("Basement", StructureAttachmentNames.Resolve(
                    StructureAttachmentKind.Basement).ToString());
                Assert.AreEqual("Dungeon", StructureAttachmentNames.Resolve(
                    StructureAttachmentKind.Dungeon).ToString());
                Assert.AreEqual("Crypt", StructureAttachmentNames.Resolve(
                    StructureAttachmentKind.Crypt).ToString());
            });
        }

        [Test]
        public void UndergroundAnchorsCanBeMovedWithoutChangingTheirSemanticKinds()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 0x2233u);
            CastleUndergroundAttachmentConfig underground =
                CastleUndergroundAttachmentPresets.Compatibility(in plan);

            underground.KeepBasement.LocalPosition += new int3(12, -8, 4);
            underground.Dungeon.LocalPosition += new int3(-20, -30, 15);
            underground.GatehouseCrypt.LocalPosition += new int3(0, -24, 18);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(underground.IsWellFormed);
                Assert.AreEqual(
                    plan.Centre + underground.KeepBasement.LocalPosition,
                    underground.ResolveKeepBasement(in plan));
                Assert.AreEqual(
                    plan.Centre + underground.Dungeon.LocalPosition,
                    underground.ResolveDungeon(in plan));
                Assert.AreEqual(
                    plan.Centre + underground.GatehouseCrypt.LocalPosition,
                    underground.ResolveGatehouseCrypt(in plan));
            });
        }

        [Test]
        public void ValidationRejectsSemanticKindMixups()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 0x7788u);
            CastleUndergroundAttachmentConfig valid =
                CastleUndergroundAttachmentPresets.Compatibility(in plan);

            CastleUndergroundAttachmentConfig wrongBasement = valid;
            wrongBasement.KeepBasement.Kind = StructureAttachmentKind.Crypt;

            CastleUndergroundAttachmentConfig wrongDungeon = valid;
            wrongDungeon.Dungeon.Kind = StructureAttachmentKind.Extension;

            CastleUndergroundAttachmentConfig wrongCrypt = valid;
            wrongCrypt.GatehouseCrypt.Kind = StructureAttachmentKind.Basement;

            Assert.Multiple(() =>
            {
                Assert.IsTrue(valid.IsWellFormed);
                Assert.IsFalse(wrongBasement.IsWellFormed);
                Assert.IsFalse(wrongDungeon.IsWellFormed);
                Assert.IsFalse(wrongCrypt.IsWellFormed);
            });
        }
    }
}
