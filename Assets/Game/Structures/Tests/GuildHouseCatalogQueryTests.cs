using System.Collections.Generic;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;

namespace Game.Structures.Tests
{
    public sealed class GuildHouseCatalogQueryTests
    {
        private static readonly GuildHouseKind[] ExpectedKinds =
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
        public void HouseEnumerationHasExplicitParityWithProductionPrograms()
        {
            GuildHouseDescriptor[] houses = GuildHouseCatalogQuery.Houses();
            Assert.That(houses, Has.Length.EqualTo(ExpectedKinds.Length));

            var seen = new HashSet<GuildHouseKind>();
            for (int i = 0; i < houses.Length; i++)
            {
                GuildHouseDescriptor house = houses[i];
                Assert.That(house.IsWellFormed, Is.True);
                Assert.That(house.Kind, Is.EqualTo(ExpectedKinds[i]));
                Assert.That(seen.Add(house.Kind), Is.True);
                Assert.That(GuildHouseProgramCatalog.Get(house.Kind).Kind, Is.EqualTo(house.Kind));
            }

            for (int i = 0; i < ExpectedKinds.Length; i++)
                Assert.That(seen.Contains(ExpectedKinds[i]), Is.True);
            Assert.That(GuildHouseCatalogQuery.TryGetHouse((GuildHouseKind)0, out _), Is.False);
            Assert.That(GuildHouseCatalogQuery.TryGetHouse((GuildHouseKind)11, out _), Is.False);
        }

        [Test]
        public void ApplicableFurnishingsAreCanonicalUniqueAndMountCompatible()
        {
            for (int houseIndex = 0; houseIndex < ExpectedKinds.Length; houseIndex++)
            {
                GuildHouseKind kind = ExpectedKinds[houseIndex];
                Assert.That(
                    GuildHouseCatalogQuery.TryGetFurnishings(kind, out GuildHouseFurnishingOption[] options),
                    Is.True,
                    kind.ToString());
                Assert.That(options.Length, Is.GreaterThan(0), kind.ToString());

                var seen = new HashSet<ushort>();
                for (int i = 0; i < options.Length; i++)
                {
                    GuildHouseFurnishingOption option = options[i];
                    Assert.That(option.Decoration.IsWellFormed, Is.True, $"{kind} id={option.Decoration.StableId}");
                    Assert.That(seen.Add(option.Decoration.StableId), Is.True, $"{kind} id={option.Decoration.StableId}");
                    Assert.That(
                        DecorationValidation.IsWellFormed(option.Decoration.ToPropDescriptor()),
                        Is.True,
                        $"{kind} id={option.Decoration.StableId}");
                    Assert.That(option.Decoration.AcceptedSockets, Is.Not.EqualTo(DecorationSocketKind.None));
                    Assert.That(option.Decoration.Family, Is.Not.EqualTo(DecorationPropFamily.Unknown));
                }
            }
        }

        [Test]
        public void RequiredFixtureClassificationOnlyLocksUnavoidableRequiredRoomContent()
        {
            Assert.That(
                GuildHouseCatalogQuery.TryGetFurnishings(
                    GuildHouseKind.Adventurers,
                    out GuildHouseFurnishingOption[] options),
                Is.True);

            GuildHouseFurnishingOption questBoard = Find(options, 281);
            GuildHouseFurnishingOption bountyBoard = Find(options, 282);
            GuildHouseFurnishingOption bedrollRack = Find(options, 287);

            Assert.That(questBoard.RequiredFixture, Is.True, "required content in a required room is integrated");
            Assert.That(questBoard.Selectable, Is.False);
            Assert.That(bountyBoard.RequiredFixture, Is.False, "optional content stays user-selectable");
            Assert.That(bountyBoard.Selectable, Is.True);
            Assert.That(
                bedrollRack.RequiredFixture,
                Is.False,
                "required content in an optional room is not unavoidable at house scope");
        }

        [Test]
        public void DifferentHouseProgramsExposeDifferentApplicablePalettes()
        {
            Assert.That(
                GuildHouseCatalogQuery.TryGetFurnishings(
                    GuildHouseKind.Adventurers,
                    out GuildHouseFurnishingOption[] adventurers),
                Is.True);
            Assert.That(
                GuildHouseCatalogQuery.TryGetFurnishings(
                    GuildHouseKind.Druids,
                    out GuildHouseFurnishingOption[] druids),
                Is.True);

            Assert.That(Contains(adventurers, 281), Is.True);
            Assert.That(Contains(druids, 281), Is.False);
            Assert.That(Contains(druids, 316), Is.True);
            Assert.That(Contains(adventurers, 316), Is.False);
        }

        private static GuildHouseFurnishingOption Find(
            GuildHouseFurnishingOption[] options,
            ushort stableId)
        {
            for (int i = 0; i < options.Length; i++)
                if (options[i].Decoration.StableId == stableId)
                    return options[i];

            Assert.Fail($"Missing furnishing id {stableId}");
            return default;
        }

        private static bool Contains(
            GuildHouseFurnishingOption[] options,
            ushort stableId)
        {
            for (int i = 0; i < options.Length; i++)
                if (options[i].Decoration.StableId == stableId)
                    return true;
            return false;
        }
    }
}
