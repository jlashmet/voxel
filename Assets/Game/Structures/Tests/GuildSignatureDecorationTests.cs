using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class GuildSignatureDecorationTests
    {
        [Test]
        public void AllReservedGuildSignatureIdsNowHaveValidRecipes()
        {
            for (ushort id = 401; id <= 440; id++)
            {
                GuildSignatureKind kind = (GuildSignatureKind)id;
                GuildSignatureRecipe recipe = GuildSignatureDecorationCatalog.Recipe(kind);
                Assert.That(recipe.IsWellFormed, Is.True, kind.ToString());
                uint variant = GuildSignatureVariants.Encode(kind, id);
                Assert.That(GuildSignatureVariants.IsGuildSignature(variant), Is.True);
                Assert.That(GuildSignatureVariants.KindOf(variant), Is.EqualTo(kind));
            }
        }

        [Test]
        public void RepresentativeFullGuildHousesReceiveSignatureDecoration()
        {
            GuildHouseKind[] kinds =
            {
                GuildHouseKind.Adventurers, GuildHouseKind.Wizards, GuildHouseKind.Knights,
                GuildHouseKind.Assassins, GuildHouseKind.Druids, GuildHouseKind.Thieves,
                GuildHouseKind.Clerics, GuildHouseKind.Rangers, GuildHouseKind.Bards,
                GuildHouseKind.Alchemists,
            };

            for (int k = 0; k < kinds.Length; k++)
            {
                GuildHouseProgram program = GuildHouseProgramCatalog.Get(kinds[k]);
                GuildHousePrototype prototype = GuildHousePrototypeComposition.Build(
                    kinds[k], Region(kinds[k]), (uint)(4000 + k), (uint)(9000 + k),
                    new int3(k * 200, 0, 0), 170, 160, program.PreferredRooms);
                Assert.That(GuildHouseRoomDecorationResolver.TryResolvePrototype(in prototype, out GuildHouseResolvedRoom[] rooms), Is.True);
                GuildSignatureResolvedRoom[] signatureRooms = GuildSignatureDecorationResolver.Resolve(in prototype, rooms);
                Assert.That(signatureRooms.Length, Is.GreaterThan(0), kinds[k].ToString());
                int total = 0;
                for (int i = 0; i < signatureRooms.Length; i++) total += signatureRooms[i].Placements.Length;
                Assert.That(total, Is.GreaterThan(0), kinds[k].ToString());
            }
        }

        private static DecorationRegionTheme Region(GuildHouseKind kind)
        {
            switch (kind)
            {
                case GuildHouseKind.Wizards:
                case GuildHouseKind.Assassins:
                case GuildHouseKind.Thieves: return DecorationRegionTheme.Moordell;
                case GuildHouseKind.Knights: return DecorationRegionTheme.Rossdam;
                case GuildHouseKind.Druids: return DecorationRegionTheme.FairyVillage;
                case GuildHouseKind.Clerics: return DecorationRegionTheme.Hightown;
                default: return DecorationRegionTheme.Kentridge;
            }
        }
    }
}
