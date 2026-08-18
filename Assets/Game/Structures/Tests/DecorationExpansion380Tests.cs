using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationExpansion380Tests
    {
        [Test]
        public void WizardSchoolIdsAndScenesAreDeterministicAndRegionAware()
        {
            DecorationContext context = Context(11u);
            for (int id = 361; id <= 380; id++)
            {
                DecorationExpansion380Kind kind = (DecorationExpansion380Kind)id;
                DecorationExpansion380Recipe recipe = DecorationExpansion380Catalog.Recipe(kind);
                DecorationPropDescriptor descriptor = DecorationExpansion380Catalog.Describe(in context, 0xE3801001u, (uint)id, kind);
                Assert.IsTrue(recipe.IsWellFormed, $"Malformed {kind}.");
                Assert.IsTrue(descriptor.IsWellFormed);
                Assert.AreEqual(id, DecorationExpansion380Variants.StableIdOf(descriptor.Variant));
            }

            DecorationSpace space = Space();
            DecorationContext high = DecorationRegionProfiles.ApplyDefaults(in context, DecorationRegionTheme.Hightown, 11u);
            DecorationContext moor = DecorationRegionProfiles.ApplyDefaults(in context, DecorationRegionTheme.Moordell, 11u);
            for (int raw = 0; raw <= (int)DecorationExpansion380SceneKind.ForbiddenArchive; raw++)
            {
                DecorationExpansion380SceneKind scene = (DecorationExpansion380SceneKind)raw;
                Assert.IsTrue(DecorationExpansion380SceneResolver.TryResolve(scene, DecorationRegionTheme.Hightown, in space, in high, null, out DecorationPlacement[] a));
                Assert.IsTrue(DecorationExpansion380SceneResolver.TryResolve(scene, DecorationRegionTheme.Hightown, in space, in high, null, out DecorationPlacement[] b));
                Assert.AreEqual(a.Length, b.Length);
                for (int i = 0; i < a.Length; i++) Assert.AreEqual(a[i].Id, b[i].Id);
                Assert.IsTrue(DecorationExpansion380SceneResolver.TryResolve(scene, DecorationRegionTheme.Moordell, in space, in moor, null, out _));
            }
        }

        [Test]
        public void ScholarRegionsReceiveLargerOptionalBudgets()
        {
            DecorationContext source = Context(5u);
            DecorationContext high = DecorationRegionProfiles.ApplyDefaults(in source, DecorationRegionTheme.Hightown, 5u);
            DecorationContext kent = DecorationRegionProfiles.ApplyDefaults(in source, DecorationRegionTheme.Kentridge, 5u);
            Assert.Greater(
                DecorationExpansion380SceneCatalog.OptionalBudget(DecorationExpansion380SceneKind.WizardLibrary, DecorationRegionTheme.Hightown, in high),
                DecorationExpansion380SceneCatalog.OptionalBudget(DecorationExpansion380SceneKind.WizardLibrary, DecorationRegionTheme.Kentridge, in kent));
        }

        private static DecorationContext Context(uint seed) => new DecorationContext
        {
            WorldSeed = seed, StructureId = 0xE380001u, SpaceId = 0xE380002u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Sacred, seed),
            StructureKind = DecorationStructureKind.House, SpaceKind = DecorationSpaceKind.Study,
            Wealth = DecorationWealthTier.Comfortable, Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior,
        };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0xE380002u, Kind = DecorationSpaceKind.Study,
            Bounds = new DecorationBounds { Min = new int3(-180, 0, -160), MaxExclusive = new int3(180, 100, 160) },
        };
    }
}
