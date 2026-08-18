using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationExpansion360Tests
    {
        [Test]
        public void AllTwentySacredIdsRoundTripAndRemainRenderable()
        {
            DecorationContext context = Context(7u);
            for (int id = 341; id <= 360; id++)
            {
                DecorationExpansion360Kind kind = (DecorationExpansion360Kind)id;
                DecorationExpansion360Recipe recipe = DecorationExpansion360Catalog.Recipe(kind);
                DecorationPropDescriptor descriptor = DecorationExpansion360Catalog.Describe(in context, 0xE3601001u, (uint)id, kind);
                Assert.Multiple(() =>
                {
                    Assert.IsTrue(recipe.IsWellFormed, $"Malformed {kind}.");
                    Assert.IsTrue(descriptor.IsWellFormed, $"Malformed descriptor {kind}.");
                    Assert.AreEqual(kind, DecorationExpansion360Variants.KindOf(descriptor.Variant));
                    Assert.AreEqual(id, DecorationExpansion360Variants.StableIdOf(descriptor.Variant));
                });
            }
        }

        [Test]
        public void SacredScenesResolveRequiredContentAcrossSeedsAndRegions()
        {
            DecorationSpace space = Space();
            DecorationRegionTheme[] regions = { DecorationRegionTheme.Hightown, DecorationRegionTheme.Rossdam, DecorationRegionTheme.Kentridge };
            for (int raw = 0; raw <= (int)DecorationExpansion360SceneKind.SacredCrypt; raw++)
            {
                DecorationExpansion360SceneKind scene = (DecorationExpansion360SceneKind)raw;
                DecorationExpansion360SceneSlot[] slots = DecorationExpansion360SceneCatalog.Slots(scene);
                for (int r = 0; r < regions.Length; r++)
                {
                    for (uint seed = 1; seed <= 16; seed++)
                    {
                        DecorationContext seeded = Context(seed);
                        DecorationContext context = DecorationRegionProfiles.ApplyDefaults(in seeded, regions[r], seed);
                        Assert.IsTrue(DecorationExpansion360SceneResolver.TryResolve(scene, regions[r], in space, in context, null, out DecorationPlacement[] placements));
                        for (int s = 0; s < slots.Length; s++)
                            if (slots[s].Required) Assert.IsTrue(Contains(placements, slots[s].SlotId));
                    }
                }
            }
        }

        [Test]
        public void SacredRegionsGetDenserOptionalTempleDressing()
        {
            DecorationContext source = Context(61u);
            DecorationContext high = DecorationRegionProfiles.ApplyDefaults(in source, DecorationRegionTheme.Hightown, 61u);
            DecorationContext kent = DecorationRegionProfiles.ApplyDefaults(in source, DecorationRegionTheme.Kentridge, 61u);
            Assert.Greater(
                DecorationExpansion360SceneCatalog.OptionalBudget(DecorationExpansion360SceneKind.GrandTemple, DecorationRegionTheme.Hightown, in high),
                DecorationExpansion360SceneCatalog.OptionalBudget(DecorationExpansion360SceneKind.GrandTemple, DecorationRegionTheme.Kentridge, in kent));
        }

        private static bool Contains(DecorationPlacement[] placements, uint slotId)
        {
            for (int i = 0; i < placements.Length; i++) if (placements[i].SlotId == slotId) return true;
            return false;
        }

        private static DecorationContext Context(uint seed) => new DecorationContext
        {
            WorldSeed = seed, StructureId = 0xE360001u, SpaceId = 0xE360002u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Sacred, seed),
            StructureKind = DecorationStructureKind.Church, SpaceKind = DecorationSpaceKind.Chapel,
            Wealth = DecorationWealthTier.Comfortable, Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior | DecorationEnvironmentTags.Sacred,
        };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0xE360002u, Kind = DecorationSpaceKind.Chapel,
            Bounds = new DecorationBounds { Min = new int3(-180, 0, -160), MaxExclusive = new int3(180, 100, 160) },
        };
    }
}
