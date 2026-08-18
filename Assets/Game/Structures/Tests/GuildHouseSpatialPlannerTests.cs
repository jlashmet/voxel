using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class GuildHouseSpatialPlannerTests
    {
        [Test]
        public void WizardsUseMultiFloorTowerAndProduceValidDecorationSpaces()
        {
            GuildHousePrototype prototype = GuildHousePrototypeComposition.Build(
                GuildHouseKind.Wizards,
                DecorationRegionTheme.Moordell,
                1234u,
                77u,
                new int3(100, 20, 200),
                72,
                72,
                8);

            Assert.That(prototype.IsWellFormed, Is.True);
            Assert.That(prototype.SpatialPlan.ShellStyle, Is.EqualTo(GuildHouseShellStyle.Tower));
            Assert.That(prototype.SpatialPlan.FloorCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(prototype.Rooms.Length, Is.GreaterThanOrEqualTo(6));

            for (int i = 0; i < prototype.Rooms.Length; i++)
            {
                Assert.That(prototype.Rooms[i].Space.IsWellFormed, Is.True);
                Assert.That(prototype.Rooms[i].Context.IsWellFormed, Is.True);
                Assert.That(prototype.Rooms[i].RequiredArchetypes.Length, Is.GreaterThan(0));
            }
        }

        [Test]
        public void DruidsUseWideLodgeGrammarAndKeepGardenExterior()
        {
            GuildHousePrototype prototype = GuildHousePrototypeComposition.Build(
                GuildHouseKind.Druids,
                DecorationRegionTheme.FairyVillage,
                991u,
                88u,
                int3.zero,
                90,
                78,
                6);

            Assert.That(prototype.SpatialPlan.ShellStyle, Is.EqualTo(GuildHouseShellStyle.Lodge));
            Assert.That(prototype.SpatialPlan.Width, Is.GreaterThanOrEqualTo(84));

            bool foundGarden = false;
            for (int i = 0; i < prototype.Rooms.Length; i++)
            {
                GuildHouseRoomComposition room = prototype.Rooms[i];
                if (room.SpatialRoom.Node.Room.Role != GuildHouseRoomRole.Garden)
                    continue;
                foundGarden = true;
                Assert.That((room.Context.Environment & DecorationEnvironmentTags.Exterior) != 0, Is.True);
            }
            Assert.That(foundGarden, Is.True);
        }

        [Test]
        public void SpatialRoomsDoNotOverlapInThreeDimensions()
        {
            GuildHouseSpatialPlan plan = GuildHouseSpatialPlanner.Plan(
                GuildHouseKind.Wizards,
                0xBEEFu,
                new int3(-20, 5, -40),
                72,
                72,
                9);

            for (int i = 0; i < plan.Rooms.Length; i++)
            for (int j = i + 1; j < plan.Rooms.Length; j++)
            {
                var a = new DecorationBounds { Min = plan.Rooms[i].Min, MaxExclusive = plan.Rooms[i].MaxExclusive };
                var b = new DecorationBounds { Min = plan.Rooms[j].Min, MaxExclusive = plan.Rooms[j].MaxExclusive };
                Assert.That(a.Overlaps(in b), Is.False, $"rooms {i} and {j} overlap");
            }
        }

        [Test]
        public void SameSeedProducesSameRoomLayout()
        {
            GuildHouseSpatialPlan a = GuildHouseSpatialPlanner.Plan(
                GuildHouseKind.Assassins, 123u, int3.zero, 70, 70, 7);
            GuildHouseSpatialPlan b = GuildHouseSpatialPlanner.Plan(
                GuildHouseKind.Assassins, 123u, int3.zero, 70, 70, 7);

            Assert.That(b.Rooms.Length, Is.EqualTo(a.Rooms.Length));
            for (int i = 0; i < a.Rooms.Length; i++)
            {
                Assert.That(b.Rooms[i].Node.Room.Role, Is.EqualTo(a.Rooms[i].Node.Room.Role));
                Assert.That(b.Rooms[i].FloorIndex, Is.EqualTo(a.Rooms[i].FloorIndex));
                Assert.That(b.Rooms[i].Min, Is.EqualTo(a.Rooms[i].Min));
                Assert.That(b.Rooms[i].Size, Is.EqualTo(a.Rooms[i].Size));
            }
        }
    }
}
