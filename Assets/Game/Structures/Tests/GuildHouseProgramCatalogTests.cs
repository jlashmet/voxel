using Game.Structures.Runtime;
using NUnit.Framework;
using System.Collections.Generic;

namespace Game.Structures.Tests
{
    public sealed class GuildHouseProgramCatalogTests
    {
        [Test]
        public void AllGuildsHaveDistinctValidPrograms()
        {
            var seen = new HashSet<GuildHouseKind>();
            for (byte raw = 1; raw <= 10; raw++)
            {
                var kind = (GuildHouseKind)raw;
                var program = GuildHouseProgramCatalog.Get(kind);

                Assert.That(program.Kind, Is.EqualTo(kind));
                Assert.That(seen.Add(program.Kind), Is.True);
                Assert.That(program.MinimumRooms, Is.GreaterThanOrEqualTo(4));
                Assert.That(program.PreferredRooms, Is.GreaterThanOrEqualTo(program.MinimumRooms));
                Assert.That(program.Rooms.Length, Is.GreaterThanOrEqualTo(program.MinimumRooms));

                var requiredRooms = 0;
                foreach (var room in program.Rooms)
                {
                    if (room.Required)
                        requiredRooms++;
                    Assert.That(room.Weight, Is.GreaterThan(0));
                    Assert.That(room.RequiredArchetypes.Length + room.OptionalArchetypes.Length, Is.GreaterThan(0));
                    AssertIdsInCanonicalRange(room.RequiredArchetypes);
                    AssertIdsInCanonicalRange(room.OptionalArchetypes);
                }

                Assert.That(requiredRooms, Is.GreaterThanOrEqualTo(3));
            }
        }

        [Test]
        public void SignatureGuildsHaveExpectedIdentityRooms()
        {
            AssertHasRequiredRoom(GuildHouseKind.Wizards, GuildHouseRoomRole.Library);
            AssertHasRequiredRoom(GuildHouseKind.Wizards, GuildHouseRoomRole.RitualRoom);
            AssertHasRequiredRoom(GuildHouseKind.Knights, GuildHouseRoomRole.TrainingRoom);
            AssertHasRequiredRoom(GuildHouseKind.Assassins, GuildHouseRoomRole.HiddenRoom);
            AssertHasRequiredRoom(GuildHouseKind.Druids, GuildHouseRoomRole.Garden);
            AssertHasRequiredRoom(GuildHouseKind.Thieves, GuildHouseRoomRole.HiddenRoom);
            AssertHasRequiredRoom(GuildHouseKind.Clerics, GuildHouseRoomRole.Shrine);
            AssertHasRequiredRoom(GuildHouseKind.Rangers, GuildHouseRoomRole.Stable);
            AssertHasRequiredRoom(GuildHouseKind.Bards, GuildHouseRoomRole.PerformanceHall);
            AssertHasRequiredRoom(GuildHouseKind.Alchemists, GuildHouseRoomRole.Workshop);
        }

        [Test]
        public void ProgramsReuseStableDecorationLibraryInsteadOfDuplicatingGuildVariants()
        {
            var wizard = GuildHouseProgramCatalog.Get(GuildHouseKind.Wizards);
            var druid = GuildHouseProgramCatalog.Get(GuildHouseKind.Druids);
            var cleric = GuildHouseProgramCatalog.Get(GuildHouseKind.Clerics);

            Assert.That(Contains(wizard, 135), Is.True, "wizard ritual room should reuse SummoningCircle");
            Assert.That(Contains(druid, 135), Is.True, "druid ritual room should reuse SummoningCircle");
            Assert.That(Contains(druid, 360), Is.True, "druid ritual can reuse DivineCrystalFocus");
            Assert.That(Contains(cleric, 360), Is.True, "cleric shrine can reuse DivineCrystalFocus");
        }

        private static void AssertHasRequiredRoom(GuildHouseKind kind, GuildHouseRoomRole role)
        {
            var program = GuildHouseProgramCatalog.Get(kind);
            foreach (var room in program.Rooms)
                if (room.Role == role && room.Required)
                    return;
            Assert.Fail($"{kind} is missing required room {role}");
        }

        private static bool Contains(GuildHouseProgram program, ushort id)
        {
            foreach (var room in program.Rooms)
            {
                foreach (var value in room.RequiredArchetypes)
                    if (value == id) return true;
                foreach (var value in room.OptionalArchetypes)
                    if (value == id) return true;
            }
            return false;
        }

        private static void AssertIdsInCanonicalRange(ushort[] ids)
        {
            foreach (var id in ids)
                Assert.That(id, Is.InRange((ushort)1, (ushort)400));
        }
    }
}
