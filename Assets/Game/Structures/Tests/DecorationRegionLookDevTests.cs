using Game.Structures.Api;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationRegionLookDevTests
    {
        [Test]
        public void SameGuildRoomResolvesAcrossAllSixRegionsWithDistinctPresentation()
        {
            DecorationSpace space = Space();
            DecorationContext context = Context();

            Assert.IsTrue(DecorationRegionLookDevComposition.TryResolveAdventurerGuildAcrossRegions(
                in space, in context, null, out DecorationRegionLookDevResult[] results));
            Assert.AreEqual(6, results.Length);

            for (int i = 0; i < results.Length; i++)
            {
                Assert.IsTrue(results[i].IsWellFormed, $"Malformed region look-dev result at {i}.");
                Assert.AreEqual((DecorationRegionTheme)(i + 1), results[i].Region);
                Assert.IsTrue(ContainsSlot(results[i].Placements, 1));
                Assert.IsTrue(ContainsSlot(results[i].Placements, 2));
                Assert.IsTrue(ContainsSlot(results[i].Placements, 3));
            }

            DecorationRegionLookDevResult kentridge = Find(results, DecorationRegionTheme.Kentridge);
            DecorationRegionLookDevResult hightown = Find(results, DecorationRegionTheme.Hightown);
            DecorationRegionLookDevResult moordell = Find(results, DecorationRegionTheme.Moordell);
            DecorationRegionLookDevResult rossdam = Find(results, DecorationRegionTheme.Rossdam);
            DecorationRegionLookDevResult fairy = Find(results, DecorationRegionTheme.FairyVillage);
            DecorationRegionLookDevResult orc = Find(results, DecorationRegionTheme.OrcVillage);

            Assert.Multiple(() =>
            {
                Assert.Greater(kentridge.Placements.Length, rossdam.Placements.Length);
                Assert.AreNotEqual(kentridge.Presentation.PrimaryMaterial, moordell.Presentation.PrimaryMaterial);
                Assert.AreNotEqual(hightown.Presentation.MagicMaterial, kentridge.Presentation.MagicMaterial);
                Assert.AreNotEqual(fairy.Presentation.SecondaryMaterial, orc.Presentation.SecondaryMaterial);
                Assert.Greater(rossdam.Presentation.Ornamentation, kentridge.Presentation.Ornamentation);
                Assert.Greater(moordell.Context.Wealth, kentridge.Context.Wealth);
            });
        }

        [Test]
        public void SixRegionComparisonIsDeterministicForSameSeed()
        {
            DecorationSpace space = Space();
            DecorationContext context = Context();
            Assert.IsTrue(DecorationRegionLookDevComposition.TryResolveAdventurerGuildAcrossRegions(
                in space, in context, null, out DecorationRegionLookDevResult[] first));
            Assert.IsTrue(DecorationRegionLookDevComposition.TryResolveAdventurerGuildAcrossRegions(
                in space, in context, null, out DecorationRegionLookDevResult[] second));

            Assert.AreEqual(first.Length, second.Length);
            for (int r = 0; r < first.Length; r++)
            {
                Assert.AreEqual(first[r].Placements.Length, second[r].Placements.Length);
                for (int i = 0; i < first[r].Placements.Length; i++)
                {
                    Assert.AreEqual(first[r].Placements[i].Id, second[r].Placements[i].Id);
                    Assert.AreEqual(first[r].Placements[i].Variant, second[r].Placements[i].Variant);
                    Assert.AreEqual(first[r].Placements[i].Bounds.Min, second[r].Placements[i].Bounds.Min);
                    Assert.AreEqual(first[r].Placements[i].Bounds.MaxExclusive, second[r].Placements[i].Bounds.MaxExclusive);
                }
            }
        }

        private static DecorationRegionLookDevResult Find(
            DecorationRegionLookDevResult[] results,
            DecorationRegionTheme region)
        {
            for (int i = 0; i < results.Length; i++)
                if (results[i].Region == region)
                    return results[i];
            return default;
        }

        private static bool ContainsSlot(DecorationPlacement[] placements, uint slotId)
        {
            for (int i = 0; i < placements.Length; i++)
                if (placements[i].SlotId == slotId)
                    return true;
            return false;
        }

        private static DecorationContext Context() => new DecorationContext
        {
            WorldSeed = 811u,
            StructureId = 0xD301u,
            SpaceId = 0xD302u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Rustic, 811u),
            StructureKind = DecorationStructureKind.House,
            SpaceKind = DecorationSpaceKind.Storage,
            Wealth = DecorationWealthTier.Comfortable,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior,
        };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0xD302u,
            Kind = DecorationSpaceKind.Storage,
            Bounds = new DecorationBounds
            {
                Min = new int3(-180, 0, -160),
                MaxExclusive = new int3(180, 90, 160),
            },
        };
    }
}
