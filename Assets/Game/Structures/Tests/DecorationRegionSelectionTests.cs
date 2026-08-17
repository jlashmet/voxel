using Game.Structures.Api;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationRegionSelectionTests
    {
        [Test]
        public void SameGuildSceneUsesRegionDensityWithoutChangingRequiredIdentity()
        {
            DecorationSpace space = Space();
            DecorationContext context = Context(41u);

            Assert.IsTrue(DecorationExpansion300RegionResolver.TryResolve(
                DecorationExpansion300SceneKind.AdventurerGuildHall,
                DecorationRegionTheme.Kentridge,
                in space, in context, null,
                out DecorationPlacement[] kentridge));
            Assert.IsTrue(DecorationExpansion300RegionResolver.TryResolve(
                DecorationExpansion300SceneKind.AdventurerGuildHall,
                DecorationRegionTheme.Rossdam,
                in space, in context, null,
                out DecorationPlacement[] rossdam));

            Assert.Multiple(() =>
            {
                Assert.IsTrue(ContainsSlot(kentridge, 1));
                Assert.IsTrue(ContainsSlot(kentridge, 2));
                Assert.IsTrue(ContainsSlot(kentridge, 3));
                Assert.IsTrue(ContainsSlot(rossdam, 1));
                Assert.IsTrue(ContainsSlot(rossdam, 2));
                Assert.IsTrue(ContainsSlot(rossdam, 3));
                Assert.Greater(kentridge.Length, rossdam.Length,
                    "Kentridge's lived-in clutter bias should allow a denser optional guild dressing than formal Rossdam.");
            });
        }

        [Test]
        public void RegionPresentationChangesMaterialsWithoutChangingSemanticObject()
        {
            DecorationContext context = Context(9u);
            DecorationPresentationProfile kentridge = DecorationRegionContentPolicy.Presentation(
                in context, DecorationRegionTheme.Kentridge);
            DecorationPresentationProfile moordell = DecorationRegionContentPolicy.Presentation(
                in context, DecorationRegionTheme.Moordell);
            DecorationPresentationProfile fairy = DecorationRegionContentPolicy.Presentation(
                in context, DecorationRegionTheme.FairyVillage);
            DecorationPresentationProfile orc = DecorationRegionContentPolicy.Presentation(
                in context, DecorationRegionTheme.OrcVillage);

            Assert.Multiple(() =>
            {
                Assert.AreNotEqual(kentridge.PrimaryMaterial, moordell.PrimaryMaterial);
                Assert.AreNotEqual(fairy.SecondaryMaterial, orc.SecondaryMaterial);
                Assert.AreNotEqual(moordell.AccentMaterial, orc.AccentMaterial);
                Assert.Greater(moordell.Ornamentation, kentridge.Ornamentation);
            });
        }

        [Test]
        public void RegionWeightingFavorsMatchingFantasyDetails()
        {
            ushort fairyCharm = DecorationRegionContentPolicy.Weight(
                DecorationRegionTheme.FairyVillage,
                DecorationExpansion300Kind.TravelCharmDisplay,
                3);
            ushort kentridgeCharm = DecorationRegionContentPolicy.Weight(
                DecorationRegionTheme.Kentridge,
                DecorationExpansion300Kind.TravelCharmDisplay,
                3);
            ushort orcTrophy = DecorationRegionContentPolicy.Weight(
                DecorationRegionTheme.OrcVillage,
                DecorationExpansion300Kind.GuildTrophyWall,
                3);
            ushort hightownTrophy = DecorationRegionContentPolicy.Weight(
                DecorationRegionTheme.Hightown,
                DecorationExpansion300Kind.GuildTrophyWall,
                3);

            Assert.Multiple(() =>
            {
                Assert.Greater(fairyCharm, kentridgeCharm);
                Assert.Greater(orcTrophy, hightownTrophy);
            });
        }

        private static bool ContainsSlot(DecorationPlacement[] placements, uint slotId)
        {
            for (int i = 0; i < placements.Length; i++)
                if (placements[i].SlotId == slotId)
                    return true;
            return false;
        }

        private static DecorationContext Context(uint seed) => new DecorationContext
        {
            WorldSeed = seed,
            StructureId = 0xD201u,
            SpaceId = 0xD202u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Rustic, seed),
            StructureKind = DecorationStructureKind.House,
            SpaceKind = DecorationSpaceKind.Storage,
            Wealth = DecorationWealthTier.Comfortable,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior,
        };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0xD202u,
            Kind = DecorationSpaceKind.Storage,
            Bounds = new DecorationBounds
            {
                Min = new int3(-180, 0, -160),
                MaxExclusive = new int3(180, 90, 160),
            },
        };
    }
}
