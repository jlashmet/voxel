using Game.Structures.Api;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationExpansion300Tests
    {
        [Test]
        public void AllFortyStableIdsHaveRecipesAndRoundTripIdentity()
        {
            DecorationContext context = Context(17u);
            for (int id = 261; id <= 300; id++)
            {
                DecorationExpansion300Kind kind = (DecorationExpansion300Kind)id;
                DecorationExpansion300Recipe recipe = DecorationExpansion300Catalog.Recipe(kind);
                DecorationPropDescriptor descriptor = DecorationExpansion300Catalog.Describe(
                    in context, 0xE3001001u, (uint)id, kind);

                Assert.Multiple(() =>
                {
                    Assert.IsTrue(recipe.IsWellFormed, $"Recipe {kind} malformed.");
                    Assert.IsTrue(descriptor.IsWellFormed, $"Descriptor {kind} malformed.");
                    Assert.IsTrue(DecorationExpansion300Variants.IsExpansion300(descriptor.Variant));
                    Assert.AreEqual(kind, DecorationExpansion300Variants.KindOf(descriptor.Variant));
                    Assert.AreEqual(id, DecorationExpansion300Variants.StableIdOf(descriptor.Variant));
                });
            }
        }

        [Test]
        public void FourFantasyScenesResolveRequiredContentAcrossSeeds()
        {
            DecorationSpace space = Space();
            for (int raw = 0; raw <= (int)DecorationExpansion300SceneKind.CaravanStaging; raw++)
            {
                DecorationExpansion300SceneKind scene = (DecorationExpansion300SceneKind)raw;
                DecorationExpansion300SceneSlot[] slots = DecorationExpansion300SceneCatalog.Slots(scene);

                for (uint seed = 1; seed <= 24; seed++)
                {
                    DecorationContext context = Context(seed);
                    Assert.IsTrue(DecorationExpansion300SceneResolver.TryResolve(
                        scene, in space, in context, null, out DecorationPlacement[] placements),
                        $"Scene {scene} failed for seed {seed}.");

                    for (int s = 0; s < slots.Length; s++)
                    {
                        DecorationExpansion300Recipe recipe = DecorationExpansion300Catalog.Recipe(slots[s].Kind);
                        Assert.IsTrue((recipe.Sockets & slots[s].Socket) != 0,
                            $"Scene {scene} asks {slots[s].Kind} for unsupported socket {slots[s].Socket}.");
                        if (!slots[s].Required)
                            continue;
                        Assert.IsTrue(ContainsSlot(placements, slots[s].SlotId),
                            $"Scene {scene} missing required slot {slots[s].SlotId} for seed {seed}.");
                    }

                    for (int i = 0; i < placements.Length; i++)
                    {
                        Assert.IsTrue(space.Bounds.Contains(in placements[i].Bounds));
                        Assert.IsTrue(DecorationExpansion300Variants.IsExpansion300(placements[i].Variant));
                        for (int j = i + 1; j < placements.Length; j++)
                            Assert.AreNotEqual(placements[i].Id, placements[j].Id);
                    }
                }
            }
        }

        [Test]
        public void LairAndGuildPacksExerciseBoxMeshAndThinBackends()
        {
            bool box = false;
            bool mesh = false;
            bool thin = false;
            bool container = false;
            bool light = false;

            for (int id = 261; id <= 300; id++)
            {
                DecorationExpansion300Recipe recipe =
                    DecorationExpansion300Catalog.Recipe((DecorationExpansion300Kind)id);
                box |= recipe.Backend == DecorationRenderBackend.BoxAssembly;
                mesh |= recipe.Backend == DecorationRenderBackend.ProceduralMesh;
                thin |= recipe.Backend == DecorationRenderBackend.ThinSurface;
                container |= (recipe.Interaction & DecorationInteractionFlags.Container) != 0;
                light |= (recipe.Interaction & DecorationInteractionFlags.EmitsLight) != 0;
            }

            Assert.Multiple(() =>
            {
                Assert.IsTrue(box);
                Assert.IsTrue(mesh);
                Assert.IsTrue(thin);
                Assert.IsTrue(container);
                Assert.IsTrue(light);
            });
        }

        private static bool ContainsSlot(DecorationPlacement[] placements, uint slotId)
        {
            if (placements == null) return false;
            for (int i = 0; i < placements.Length; i++)
                if (placements[i].SlotId == slotId)
                    return true;
            return false;
        }

        private static DecorationContext Context(uint seed) => new DecorationContext
        {
            WorldSeed = seed,
            StructureId = 0xE300001u,
            SpaceId = 0xE300002u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Rustic, seed),
            StructureKind = DecorationStructureKind.House,
            SpaceKind = DecorationSpaceKind.Storage,
            Wealth = DecorationWealthTier.Comfortable,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior,
        };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0xE300002u,
            Kind = DecorationSpaceKind.Storage,
            Bounds = new DecorationBounds
            {
                Min = new int3(-180, 0, -160),
                MaxExclusive = new int3(180, 90, 160),
            },
        };
    }
}
