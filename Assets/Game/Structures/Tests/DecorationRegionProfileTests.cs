using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;

namespace Game.Structures.Tests
{
    public sealed class DecorationRegionProfileTests
    {
        [Test]
        public void AllNamedRegionsHaveWellFormedDistinctProfiles()
        {
            for (int raw = (int)DecorationRegionTheme.Kentridge; raw <= (int)DecorationRegionTheme.OrcVillage; raw++)
            {
                DecorationRegionTheme region = (DecorationRegionTheme)raw;
                DecorationRegionProfile profile = DecorationRegionProfiles.Resolve(region);
                Assert.IsTrue(profile.IsWellFormed, $"Region {region} has malformed profile.");
            }

            Assert.AreEqual(DecorationStyleFamily.Rustic,
                DecorationRegionProfiles.Resolve(DecorationRegionTheme.Kentridge).StyleFamily);
            Assert.AreEqual(DecorationWealthTier.Wealthy,
                DecorationRegionProfiles.Resolve(DecorationRegionTheme.Moordell).DefaultWealth);
            Assert.AreEqual(DecorationWealthTier.Noble,
                DecorationRegionProfiles.Resolve(DecorationRegionTheme.Rossdam).DefaultWealth);
            Assert.IsTrue(DecorationRegionProfiles.Resolve(DecorationRegionTheme.FairyVillage)
                .Prefers(DecorationRegionContentTags.Organic));
            Assert.IsTrue(DecorationRegionProfiles.Resolve(DecorationRegionTheme.OrcVillage)
                .Prefers(DecorationRegionContentTags.Trophy));
        }

        [Test]
        public void ApplyingRegionDefaultsDoesNotPreventBuildingSpecificWealthOverride()
        {
            DecorationContext source = new DecorationContext
            {
                WorldSeed = 7,
                StructureId = 11,
                SpaceId = 12,
                StructureKind = DecorationStructureKind.House,
                SpaceKind = DecorationSpaceKind.Bedroom,
                Wealth = DecorationWealthTier.Poor,
                Condition = DecorationConditionTier.Maintained,
                Environment = DecorationEnvironmentTags.Interior,
            };

            DecorationContext moordellDefault = DecorationRegionProfiles.ApplyDefaults(
                in source, DecorationRegionTheme.Moordell, 99u, applyWealth: true);
            DecorationContext preserveSpecificWealth = DecorationRegionProfiles.ApplyDefaults(
                in source, DecorationRegionTheme.Moordell, 99u, applyWealth: false);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(DecorationStyleFamily.Courtly,
                    DecorationStyleIds.FamilyOf(moordellDefault.StyleId));
                Assert.AreEqual(DecorationWealthTier.Wealthy, moordellDefault.Wealth);
                Assert.AreEqual(DecorationWealthTier.Poor, preserveSpecificWealth.Wealth);
            });
        }

        [Test]
        public void RegionContentWeightsFavorDifferentFantasyContent()
        {
            int fairyOrganic = DecorationRegionProfiles.ContentWeight(
                DecorationRegionTheme.FairyVillage, DecorationRegionContentTags.Organic);
            int kentridgeOrganic = DecorationRegionProfiles.ContentWeight(
                DecorationRegionTheme.Kentridge, DecorationRegionContentTags.Organic);
            int moordellNoble = DecorationRegionProfiles.ContentWeight(
                DecorationRegionTheme.Moordell, DecorationRegionContentTags.Noble);
            int orcTrophy = DecorationRegionProfiles.ContentWeight(
                DecorationRegionTheme.OrcVillage, DecorationRegionContentTags.Trophy);

            Assert.Multiple(() =>
            {
                Assert.Greater(fairyOrganic, kentridgeOrganic);
                Assert.Greater(moordellNoble, 10);
                Assert.Greater(orcTrophy, 10);
            });
        }
    }
}
