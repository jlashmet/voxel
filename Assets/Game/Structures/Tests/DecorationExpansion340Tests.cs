using Game.Structures.Api;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationExpansion340Tests
    {
        [Test]
        public void AllTwentyStableIdsHaveValidRecipesAndRoundTrip()
        {
            DecorationContext context = Context(19u);
            for (int id = 321; id <= 340; id++)
            {
                DecorationExpansion340Kind kind = (DecorationExpansion340Kind)id;
                DecorationExpansion340Recipe recipe = DecorationExpansion340Catalog.Recipe(kind);
                DecorationPropDescriptor descriptor = DecorationExpansion340Catalog.Describe(
                    in context, 0xE3401001u, (uint)id, kind);

                Assert.Multiple(() =>
                {
                    Assert.IsTrue(recipe.IsWellFormed, $"Malformed recipe {kind}.");
                    Assert.IsTrue(descriptor.IsWellFormed, $"Malformed descriptor {kind}.");
                    Assert.IsTrue(DecorationExpansion340Variants.IsExpansion340(descriptor.Variant));
                    Assert.AreEqual(kind, DecorationExpansion340Variants.KindOf(descriptor.Variant));
                    Assert.AreEqual(id, DecorationExpansion340Variants.StableIdOf(descriptor.Variant));
                });
            }
        }

        [Test]
        public void TrapPuzzleAndVaultScenesKeepRequiredContentAcrossSeeds()
        {
            DecorationSpace space = Space();
            for (int raw = 0; raw <= (int)DecorationExpansion340SceneKind.TreasureVault; raw++)
            {
                DecorationExpansion340SceneKind scene = (DecorationExpansion340SceneKind)raw;
                DecorationExpansion340SceneSlot[] slots = DecorationExpansion340SceneCatalog.Slots(scene);
                for (uint seed = 1; seed <= 24; seed++)
                {
                    DecorationContext context = Context(seed);
                    Assert.IsTrue(DecorationExpansion340SceneResolver.TryResolve(
                        scene, in space, in context, null, out DecorationPlacement[] placements),
                        $"{scene} failed for seed {seed}.");

                    for (int s = 0; s < slots.Length; s++)
                    {
                        DecorationExpansion340Recipe recipe = DecorationExpansion340Catalog.Recipe(slots[s].Kind);
                        Assert.IsTrue((recipe.Sockets & slots[s].Socket) != 0,
                            $"{slots[s].Kind} does not support {slots[s].Socket}.");
                        if (slots[s].Required)
                            Assert.IsTrue(ContainsSlot(placements, slots[s].SlotId),
                                $"{scene} missing required slot {slots[s].SlotId} for seed {seed}.");
                    }

                    for (int i = 0; i < placements.Length; i++)
                    {
                        Assert.IsTrue(space.Bounds.Contains(in placements[i].Bounds));
                        for (int j = i + 1; j < placements.Length; j++)
                            Assert.AreNotEqual(placements[i].Id, placements[j].Id);
                    }
                }
            }
        }

        [Test]
        public void TrapPuzzleBlockExercisesPresentationAndGameplayClasses()
        {
            bool box = false, mesh = false, thin = false, light = false, container = false;
            for (int id = 321; id <= 340; id++)
            {
                DecorationExpansion340Recipe recipe = DecorationExpansion340Catalog.Recipe((DecorationExpansion340Kind)id);
                box |= recipe.Backend == DecorationRenderBackend.BoxAssembly;
                mesh |= recipe.Backend == DecorationRenderBackend.ProceduralMesh;
                thin |= recipe.Backend == DecorationRenderBackend.ThinSurface;
                light |= (recipe.Interaction & DecorationInteractionFlags.EmitsLight) != 0;
                container |= (recipe.Interaction & DecorationInteractionFlags.Container) != 0;
            }

            Assert.Multiple(() =>
            {
                Assert.IsTrue(box);
                Assert.IsTrue(mesh);
                Assert.IsTrue(thin);
                Assert.IsTrue(light);
                Assert.IsTrue(container);
                Assert.IsTrue((DecorationExpansion340Catalog.Recipe(DecorationExpansion340Kind.TreasureTrapChest).Interaction &
                    DecorationInteractionFlags.Lootable) != 0);
            });
        }

        private static bool ContainsSlot(DecorationPlacement[] placements, uint slotId)
        {
            for (int i = 0; i < placements.Length; i++)
                if (placements[i].SlotId == slotId) return true;
            return false;
        }

        private static DecorationContext Context(uint seed) => new DecorationContext
        {
            WorldSeed = seed,
            StructureId = 0xE340001u,
            SpaceId = 0xE340002u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Sacred, seed),
            StructureKind = DecorationStructureKind.Dungeon,
            SpaceKind = DecorationSpaceKind.Storage,
            Wealth = DecorationWealthTier.Wealthy,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior | DecorationEnvironmentTags.Underground,
        };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0xE340002u,
            Kind = DecorationSpaceKind.Storage,
            Bounds = new DecorationBounds
            {
                Min = new int3(-180, 0, -160),
                MaxExclusive = new int3(180, 100, 160),
            },
        };
    }
}
