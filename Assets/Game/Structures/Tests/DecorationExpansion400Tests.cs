using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationExpansion400Tests
    {
        [Test]
        public void FinalTwentyIdsRoundTripAndSceneContentIsStable()
        {
            DecorationContext context = Context(21u, DecorationConditionTier.Abandoned);
            for (int id = 381; id <= 400; id++)
            {
                DecorationExpansion400Kind kind = (DecorationExpansion400Kind)id;
                DecorationExpansion400Recipe recipe = DecorationExpansion400Catalog.Recipe(kind);
                DecorationPropDescriptor descriptor = DecorationExpansion400Catalog.Describe(in context, 0xE4001001u, (uint)id, kind);
                Assert.IsTrue(recipe.IsWellFormed, $"Malformed {kind}.");
                Assert.IsTrue(descriptor.IsWellFormed);
                Assert.AreEqual(id, DecorationExpansion400Variants.StableIdOf(descriptor.Variant));
            }

            DecorationSpace space = Space();
            for (int raw = 0; raw <= (int)DecorationExpansion400SceneKind.CorruptedRuin; raw++)
            {
                DecorationExpansion400SceneKind scene = (DecorationExpansion400SceneKind)raw;
                Assert.IsTrue(DecorationExpansion400SceneResolver.TryResolve(scene, in space, in context, null, out DecorationPlacement[] first));
                Assert.IsTrue(DecorationExpansion400SceneResolver.TryResolve(scene, in space, in context, null, out DecorationPlacement[] second));
                Assert.AreEqual(first.Length, second.Length);
                for (int i = 0; i < first.Length; i++) Assert.AreEqual(first[i].Id, second[i].Id);
            }
        }

        [Test]
        public void RuinedSpacesReceiveMoreAftermathThanPristineSpaces()
        {
            DecorationContext ruined = Context(17u, DecorationConditionTier.Ruined);
            DecorationContext pristine = Context(17u, DecorationConditionTier.Pristine);
            Assert.Greater(
                DecorationExpansion400SceneCatalog.OptionalBudget(DecorationExpansion400SceneKind.CorruptedRuin, in ruined),
                DecorationExpansion400SceneCatalog.OptionalBudget(DecorationExpansion400SceneKind.CorruptedRuin, in pristine));
        }

        [Test]
        public void FinalBlockExercisesBoxMeshThinLightAndContainerPaths()
        {
            bool box = false, mesh = false, thin = false, light = false, container = false;
            for (int id = 381; id <= 400; id++)
            {
                DecorationExpansion400Recipe recipe = DecorationExpansion400Catalog.Recipe((DecorationExpansion400Kind)id);
                box |= recipe.Backend == DecorationRenderBackend.BoxAssembly;
                mesh |= recipe.Backend == DecorationRenderBackend.ProceduralMesh;
                thin |= recipe.Backend == DecorationRenderBackend.ThinSurface;
                light |= (recipe.Interaction & DecorationInteractionFlags.EmitsLight) != 0;
                container |= (recipe.Interaction & DecorationInteractionFlags.Container) != 0;
            }
            Assert.Multiple(() => { Assert.IsTrue(box); Assert.IsTrue(mesh); Assert.IsTrue(thin); Assert.IsTrue(light); Assert.IsTrue(container); });
        }

        private static DecorationContext Context(uint seed, DecorationConditionTier condition) => new DecorationContext
        {
            WorldSeed = seed, StructureId = 0xE400001u, SpaceId = 0xE400002u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Sacred, seed),
            StructureKind = DecorationStructureKind.Ruin, SpaceKind = DecorationSpaceKind.Study,
            Wealth = DecorationWealthTier.Comfortable, Condition = condition,
            Environment = DecorationEnvironmentTags.Interior | DecorationEnvironmentTags.Abandoned,
        };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0xE400002u, Kind = DecorationSpaceKind.Study,
            Bounds = new DecorationBounds { Min = new int3(-180, 0, -160), MaxExclusive = new int3(180, 100, 160) },
        };
    }
}
