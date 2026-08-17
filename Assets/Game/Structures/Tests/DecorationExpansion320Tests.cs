using Game.Structures.Api;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationExpansion320Tests
    {
        [Test]
        public void AllTwentyMagicalNatureIdsHaveValidRecipesAndRoundTripIdentity()
        {
            DecorationContext context = Context(17u);
            for (int id = 301; id <= 320; id++)
            {
                DecorationExpansion320Kind kind = (DecorationExpansion320Kind)id;
                DecorationExpansion320Recipe recipe = DecorationExpansion320Catalog.Recipe(kind);
                DecorationPropDescriptor descriptor = DecorationExpansion320Catalog.Describe(in context, 0xE3200010u, (uint)id, kind);
                Assert.Multiple(() =>
                {
                    Assert.IsTrue(recipe.IsWellFormed, $"Malformed recipe {kind}");
                    Assert.IsTrue(descriptor.IsWellFormed, $"Malformed descriptor {kind}");
                    Assert.IsTrue(DecorationExpansion320Variants.IsExpansion320(descriptor.Variant));
                    Assert.AreEqual(kind, DecorationExpansion320Variants.KindOf(descriptor.Variant));
                });
            }
        }

        [Test]
        public void ThreeMagicalNatureScenesResolveAcrossSeeds()
        {
            DecorationSpace space = Space();
            for (int raw = 0; raw <= (int)DecorationExpansion320SceneKind.DruidShrine; raw++)
            {
                DecorationExpansion320SceneKind scene = (DecorationExpansion320SceneKind)raw;
                for (uint seed = 1; seed <= 24; seed++)
                {
                    DecorationContext context = Context(seed);
                    Assert.IsTrue(DecorationExpansion320SceneResolver.TryResolve(
                        scene, DecorationRegionTheme.FairyVillage, in space, in context, null,
                        out DecorationPlacement[] placements), $"{scene} failed seed {seed}");
                    Assert.Greater(placements.Length, 0);
                    for (int i = 0; i < placements.Length; i++)
                    {
                        Assert.IsTrue(space.Bounds.Contains(in placements[i].Bounds));
                        Assert.IsTrue(DecorationExpansion320Variants.IsExpansion320(placements[i].Variant));
                    }
                }
            }
        }

        [Test]
        public void FairyVillageGetsDenserEnchantedGroveThanFormalRossdam()
        {
            DecorationSpace space = Space();
            DecorationContext context = Context(811u);
            Assert.IsTrue(DecorationExpansion320SceneResolver.TryResolve(
                DecorationExpansion320SceneKind.EnchantedGrove, DecorationRegionTheme.FairyVillage,
                in space, in context, null, out DecorationPlacement[] fairy));
            Assert.IsTrue(DecorationExpansion320SceneResolver.TryResolve(
                DecorationExpansion320SceneKind.EnchantedGrove, DecorationRegionTheme.Rossdam,
                in space, in context, null, out DecorationPlacement[] rossdam));
            Assert.Greater(fairy.Length, rossdam.Length);
        }

        private static DecorationContext Context(uint seed) => new DecorationContext
        {
            WorldSeed = seed,
            StructureId = 0xE320001u,
            SpaceId = 0xE320002u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Sacred, seed),
            StructureKind = DecorationStructureKind.Camp,
            SpaceKind = DecorationSpaceKind.Shrine,
            Wealth = DecorationWealthTier.Comfortable,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Exterior,
        };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0xE320002u,
            Kind = DecorationSpaceKind.Shrine,
            Bounds = new DecorationBounds
            {
                Min = new int3(-200, 0, -180),
                MaxExclusive = new int3(200, 100, 180),
            },
        };
    }
}
