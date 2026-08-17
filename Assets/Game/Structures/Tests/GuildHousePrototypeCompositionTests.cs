using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class GuildHousePrototypeCompositionTests
    {
        [Test]
        public void WizardsGuildResolvesEverySelectedRoomThroughExistingSceneResolvers()
        {
            GuildHousePrototype prototype = GuildHousePrototypeComposition.Build(
                GuildHouseKind.Wizards,
                DecorationRegionTheme.Moordell,
                111u,
                901u,
                int3.zero,
                84,
                84,
                7);

            Assert.That(GuildHouseRoomDecorationResolver.TryResolvePrototype(in prototype, out GuildHouseResolvedRoom[] rooms), Is.True);
            Assert.That(rooms.Length, Is.EqualTo(prototype.Rooms.Length));
            for (int i = 0; i < rooms.Length; i++)
            {
                Assert.That(rooms[i].Source, Is.Not.EqualTo(GuildHouseDecorationSource.None));
                Assert.That(rooms[i].Placements.Length, Is.GreaterThan(0));
            }
        }

        [Test]
        public void DruidsLodgeResolvesEverySelectedRoomThroughExistingSceneResolvers()
        {
            GuildHousePrototype prototype = GuildHousePrototypeComposition.Build(
                GuildHouseKind.Druids,
                DecorationRegionTheme.FairyVillage,
                222u,
                902u,
                new int3(20, 0, 40),
                96,
                84,
                6);

            Assert.That(GuildHouseRoomDecorationResolver.TryResolvePrototype(in prototype, out GuildHouseResolvedRoom[] rooms), Is.True);
            Assert.That(rooms.Length, Is.EqualTo(prototype.Rooms.Length));
            for (int i = 0; i < rooms.Length; i++)
            {
                Assert.That(rooms[i].Source, Is.Not.EqualTo(GuildHouseDecorationSource.None));
                Assert.That(rooms[i].Placements.Length, Is.GreaterThan(0));
            }
        }

        [Test]
        public void SameWizardPrototypeProducesStablePlacementIdentity()
        {
            GuildHousePrototype a = GuildHousePrototypeComposition.Build(
                GuildHouseKind.Wizards, DecorationRegionTheme.Hightown,
                333u, 903u, int3.zero, 84, 84, 6);
            GuildHousePrototype b = GuildHousePrototypeComposition.Build(
                GuildHouseKind.Wizards, DecorationRegionTheme.Hightown,
                333u, 903u, int3.zero, 84, 84, 6);

            Assert.That(GuildHouseRoomDecorationResolver.TryResolvePrototype(in a, out GuildHouseResolvedRoom[] ar), Is.True);
            Assert.That(GuildHouseRoomDecorationResolver.TryResolvePrototype(in b, out GuildHouseResolvedRoom[] br), Is.True);
            Assert.That(br.Length, Is.EqualTo(ar.Length));
            for (int i = 0; i < ar.Length; i++)
            {
                Assert.That(br[i].Placements.Length, Is.EqualTo(ar[i].Placements.Length));
                for (int p = 0; p < ar[i].Placements.Length; p++)
                    Assert.That(br[i].Placements[p].Id, Is.EqualTo(ar[i].Placements[p].Id));
            }
        }
    }
}
