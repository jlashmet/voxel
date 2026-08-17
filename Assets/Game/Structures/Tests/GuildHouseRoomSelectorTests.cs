using Game.Structures.Runtime;
using NUnit.Framework;

namespace Game.Structures.Tests
{
    public sealed class GuildHouseRoomSelectorTests
    {
        [Test]
        public void SelectionIsDeterministicAndCapacityBounded()
        {
            var program = GuildHouseProgramCatalog.Get(GuildHouseKind.Wizards);
            var a = GuildHouseRoomSelector.Select(program, 0xC0FFEEu, 7);
            var b = GuildHouseRoomSelector.Select(program, 0xC0FFEEu, 7);

            Assert.That(a.Length, Is.EqualTo(7));
            Assert.That(b.Length, Is.EqualTo(a.Length));
            for (var i = 0; i < a.Length; i++)
                Assert.That(b[i].Role, Is.EqualTo(a[i].Role));
        }

        [Test]
        public void RequiredRoomsAreSelectedBeforeOptionalRooms()
        {
            var program = GuildHouseProgramCatalog.Get(GuildHouseKind.Druids);
            var selected = GuildHouseRoomSelector.Select(program, 42u, program.MinimumRooms);

            Assert.That(selected.Length, Is.EqualTo(program.MinimumRooms));
            foreach (var room in selected)
                Assert.That(room.Required, Is.True);
        }

        [Test]
        public void LargerShellAddsOptionalIdentityRoomsWithoutChangingRequiredPrefix()
        {
            var program = GuildHouseProgramCatalog.Get(GuildHouseKind.Assassins);
            var minimum = GuildHouseRoomSelector.Select(program, 777u, program.MinimumRooms);
            var expanded = GuildHouseRoomSelector.Select(program, 777u, program.PreferredRooms);

            Assert.That(expanded.Length, Is.GreaterThanOrEqualTo(minimum.Length));
            for (var i = 0; i < minimum.Length; i++)
                Assert.That(expanded[i].Role, Is.EqualTo(minimum[i].Role));
        }
    }
}
