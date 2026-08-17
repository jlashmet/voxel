using Game.Structures.Runtime;
using NUnit.Framework;

namespace Game.Structures.Tests
{
    public sealed class GuildHouseTopologyPlannerTests
    {
        [Test]
        public void AssassinHiddenRoomIsDeepAndConcealed()
        {
            var program = GuildHouseProgramCatalog.Get(GuildHouseKind.Assassins);
            var selected = GuildHouseRoomSelector.Select(program, 123u, program.PreferredRooms);
            var nodes = GuildHouseTopologyPlanner.Plan(program, selected);

            GuildHouseRoomNode hidden = default;
            var found = false;
            foreach (var node in nodes)
            {
                if (node.Room.Role != GuildHouseRoomRole.HiddenRoom) continue;
                hidden = node;
                found = true;
                break;
            }

            Assert.That(found, Is.True);
            Assert.That(hidden.Depth, Is.EqualTo(4));
            Assert.That(hidden.HiddenAccess, Is.True);
            Assert.That(hidden.ParentIndex, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void WizardForbiddenRoomCanBeDeepButIsNotAutomaticallySecret()
        {
            var program = GuildHouseProgramCatalog.Get(GuildHouseKind.Wizards);
            var selected = GuildHouseRoomSelector.Select(program, 456u, program.PreferredRooms);
            var nodes = GuildHouseTopologyPlanner.Plan(program, selected);

            foreach (var node in nodes)
            {
                if (node.Room.Role != GuildHouseRoomRole.HiddenRoom) continue;
                Assert.That(node.Depth, Is.EqualTo(4));
                Assert.That(node.HiddenAccess, Is.False);
                return;
            }
            Assert.Fail("expanded Wizards Guild should contain its optional forbidden/hidden room");
        }

        [Test]
        public void PublicRoomsNeverAppearDeeperThanOperationalRooms()
        {
            var program = GuildHouseProgramCatalog.Get(GuildHouseKind.Adventurers);
            var selected = GuildHouseRoomSelector.Select(program, 999u, program.PreferredRooms);
            var nodes = GuildHouseTopologyPlanner.Plan(program, selected);

            byte contractDepth = 255;
            byte dormDepth = 0;
            foreach (var node in nodes)
            {
                if (node.Room.Role == GuildHouseRoomRole.ContractHall) contractDepth = node.Depth;
                if (node.Room.Role == GuildHouseRoomRole.Dormitory) dormDepth = node.Depth;
            }

            Assert.That(contractDepth, Is.EqualTo(0));
            Assert.That(dormDepth, Is.GreaterThan(contractDepth));
        }
    }
}
