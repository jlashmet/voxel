using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class GuildHouseAllKindsTests
    {
        private static readonly GuildHouseKind[] Kinds =
        {
            GuildHouseKind.Adventurers,
            GuildHouseKind.Wizards,
            GuildHouseKind.Knights,
            GuildHouseKind.Assassins,
            GuildHouseKind.Druids,
            GuildHouseKind.Thieves,
            GuildHouseKind.Clerics,
            GuildHouseKind.Rangers,
            GuildHouseKind.Bards,
            GuildHouseKind.Alchemists,
        };

        [Test]
        public void EveryGuildKindHasAResolvableRepresentativePrototype()
        {
            for (int k = 0; k < Kinds.Length; k++)
            {
                GuildHouseKind kind = Kinds[k];
                DecorationRegionTheme region = RepresentativeRegion(kind);
                GuildHouseProgram program = GuildHouseProgramCatalog.Get(kind);
                GuildHousePrototype prototype = GuildHousePrototypeComposition.Build(
                    kind,
                    region,
                    (uint)(1000 + k),
                    (uint)(5000 + k),
                    new int3(k * 220, 0, 0),
                    160,
                    150,
                    program.PreferredRooms);

                Assert.That(prototype.IsWellFormed, Is.True, kind.ToString());
                Assert.That(
                    GuildHouseRoomDecorationResolver.TryResolvePrototype(in prototype, out GuildHouseResolvedRoom[] rooms),
                    Is.True,
                    $"{kind} failed room dispatch");
                Assert.That(rooms.Length, Is.EqualTo(prototype.Rooms.Length), kind.ToString());
                for (int i = 0; i < rooms.Length; i++)
                {
                    Assert.That(rooms[i].Source, Is.Not.EqualTo(GuildHouseDecorationSource.None), $"{kind} room {i}");
                    Assert.That(rooms[i].Placements.Length, Is.GreaterThan(0), $"{kind} room {i}");
                }
            }
        }

        [Test]
        public void SecretiveGuildsRetainHiddenTopologyInSpatialPrototype()
        {
            GuildHouseKind[] secretive = { GuildHouseKind.Assassins, GuildHouseKind.Thieves };
            for (int k = 0; k < secretive.Length; k++)
            {
                GuildHouseProgram program = GuildHouseProgramCatalog.Get(secretive[k]);
                GuildHousePrototype prototype = GuildHousePrototypeComposition.Build(
                    secretive[k], DecorationRegionTheme.Moordell,
                    77u, (uint)(8000 + k), int3.zero, 150, 140, program.PreferredRooms);

                bool foundHidden = false;
                for (int i = 0; i < prototype.Rooms.Length; i++)
                {
                    if (!prototype.Rooms[i].SpatialRoom.Node.HiddenAccess) continue;
                    foundHidden = true;
                    Assert.That(prototype.Rooms[i].SpatialRoom.Node.Depth, Is.GreaterThanOrEqualTo(3));
                }
                Assert.That(foundHidden, Is.True, secretive[k].ToString());
            }
        }

        private static DecorationRegionTheme RepresentativeRegion(GuildHouseKind kind)
        {
            switch (kind)
            {
                case GuildHouseKind.Wizards: return DecorationRegionTheme.Moordell;
                case GuildHouseKind.Knights: return DecorationRegionTheme.Rossdam;
                case GuildHouseKind.Druids: return DecorationRegionTheme.FairyVillage;
                case GuildHouseKind.Clerics: return DecorationRegionTheme.Hightown;
                case GuildHouseKind.Rangers: return DecorationRegionTheme.Kentridge;
                case GuildHouseKind.Assassins:
                case GuildHouseKind.Thieves: return DecorationRegionTheme.Moordell;
                default: return DecorationRegionTheme.Kentridge;
            }
        }
    }
}
