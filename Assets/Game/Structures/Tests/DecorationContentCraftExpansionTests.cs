using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;

namespace Game.Structures.Tests
{
    public sealed class DecorationContentCraftExpansionTests
    {
        [Test]
        public void CarpentryPackCoversStableIdsFortyThreeThroughSixty()
        {
            DecorationContext context = Context(43u);
            for (int raw = (int)DecorationContentKind.CarpenterBench;
                 raw <= (int)DecorationContentKind.ShavingPile;
                 raw++)
            {
                DecorationContentKind kind = (DecorationContentKind)raw;
                DecorationContentRecipe recipe = DecorationContentCatalog.Recipe(kind);
                DecorationPropDescriptor descriptor = DecorationContentCatalog.Describe(
                    in context, 0x43525031u, (uint)raw, kind);

                Assert.Multiple(() =>
                {
                    Assert.IsTrue(recipe.IsWellFormed, $"{kind} recipe malformed.");
                    Assert.AreEqual(DecorationContentCategory.Carpentry, recipe.Category, $"{kind} wrong category.");
                    Assert.IsTrue(descriptor.IsWellFormed, $"{kind} descriptor malformed.");
                    Assert.AreEqual(kind, DecorationContentVariants.KindOf(descriptor.Variant), $"{kind} identity lost.");
                });
            }
        }

        [Test]
        public void CraftPackCoversStableIdsSixtyOneThroughEightyFour()
        {
            DecorationContext context = Context(61u);
            for (int raw = (int)DecorationContentKind.Loom;
                 raw <= (int)DecorationContentKind.LeatherToolBoard;
                 raw++)
            {
                DecorationContentKind kind = (DecorationContentKind)raw;
                DecorationContentRecipe recipe = DecorationContentCatalog.Recipe(kind);
                DecorationPropDescriptor descriptor = DecorationContentCatalog.Describe(
                    in context, 0x43524631u, (uint)raw, kind);

                Assert.Multiple(() =>
                {
                    Assert.IsTrue(recipe.IsWellFormed, $"{kind} recipe malformed.");
                    Assert.AreEqual(DecorationContentCategory.Craft, recipe.Category, $"{kind} wrong category.");
                    Assert.IsTrue(descriptor.IsWellFormed, $"{kind} descriptor malformed.");
                    Assert.AreEqual(kind, DecorationContentVariants.KindOf(descriptor.Variant), $"{kind} identity lost.");
                });
            }
        }

        [Test]
        public void ExpansionExercisesStaticMovableIntegratedAndMeshBackends()
        {
            DecorationContentRecipe bench = DecorationContentCatalog.Recipe(DecorationContentKind.CarpenterBench);
            DecorationContentRecipe chest = DecorationContentCatalog.Recipe(DecorationContentKind.ToolChest);
            DecorationContentRecipe kiln = DecorationContentCatalog.Recipe(DecorationContentKind.Kiln);
            DecorationContentRecipe shavings = DecorationContentCatalog.Recipe(DecorationContentKind.ShavingPile);
            DecorationContentRecipe dryingLine = DecorationContentCatalog.Recipe(DecorationContentKind.DryingLine);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(DecorationRenderBackend.BoxAssembly, bench.Backend);
                Assert.AreNotEqual(DecorationInteractionFlags.None,
                    chest.Interaction & DecorationInteractionFlags.Container);
                Assert.AreNotEqual(DecorationInteractionFlags.None,
                    chest.Interaction & DecorationInteractionFlags.Movable);
                Assert.AreEqual(DecorationRenderBackend.VoxelStamp, kiln.Backend);
                Assert.AreNotEqual(DecorationInteractionFlags.None,
                    kiln.Interaction & DecorationInteractionFlags.EmitsLight);
                Assert.AreEqual(DecorationRenderBackend.ProceduralMesh, shavings.Backend);
                Assert.AreEqual(DecorationRenderBackend.ProceduralMesh, dryingLine.Backend);
            });
        }

        private static DecorationContext Context(uint seed) => new DecorationContext
        {
            WorldSeed = seed,
            StructureId = 0xC2AF7001u,
            SpaceId = 0xC2AF7002u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Rustic, seed),
            StructureKind = DecorationStructureKind.House,
            SpaceKind = DecorationSpaceKind.Storage,
            Wealth = DecorationWealthTier.Modest,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior,
        };
    }
}
