using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationExpansion200Tests
    {
        [Test]
        public void StableArchetypes115Through200AreWellFormedAndRoundTripIdentity()
        {
            DecorationContext context = Context(17u);
            for (ushort id = DecorationExpansion200Catalog.FirstId; id <= DecorationExpansion200Catalog.LastId; id++)
            {
                DecorationExpandedContentKind kind = (DecorationExpandedContentKind)id;
                DecorationExpandedContentRecipe recipe = DecorationExpansion200Catalog.Recipe(kind);
                DecorationPropDescriptor descriptor = DecorationExpansion200Catalog.Describe(
                    in context, 0xE2CA7001u, id, kind);

                Assert.Multiple(() =>
                {
                    Assert.IsTrue(recipe.IsWellFormed, $"Recipe {kind} malformed.");
                    Assert.IsTrue(descriptor.IsWellFormed, $"Descriptor {kind} malformed.");
                    Assert.IsTrue(DecorationExpandedContentVariants.IsExpanded(descriptor.Variant), $"{kind} lost expanded marker.");
                    Assert.AreEqual(id, DecorationExpandedContentVariants.StableIdOf(descriptor.Variant), $"{kind} stable ID did not round trip.");
                    Assert.AreEqual(kind, DecorationExpandedContentVariants.KindOf(descriptor.Variant), $"{kind} enum identity did not round trip.");
                    Assert.AreEqual(recipe.ProxyFamily, descriptor.Family, $"{kind} proxy family mismatch.");
                });
            }

            Assert.AreEqual(200, DecorationContentCatalog.KindCount + DecorationExpansion200Catalog.Count);
        }

        [Test]
        public void ExpandedSceneSlotsRequestSocketsAcceptedByTheirRecipes()
        {
            for (int raw = 0; raw <= (int)DecorationExpansion200SceneKind.CivicStreet; raw++)
            {
                DecorationExpansion200SceneKind scene = (DecorationExpansion200SceneKind)raw;
                DecorationExpansion200SceneSlot[] slots = DecorationExpansion200SceneCatalog.Slots(scene);
                Assert.Greater(slots.Length, 0);
                for (int i = 0; i < slots.Length; i++)
                {
                    DecorationExpandedContentRecipe recipe = DecorationExpansion200Catalog.Recipe(slots[i].Kind);
                    Assert.IsTrue(recipe.IsWellFormed, $"{scene}/{slots[i].Kind} missing recipe.");
                    Assert.AreNotEqual(DecorationSocketKind.None, recipe.AcceptedSockets & slots[i].Socket,
                        $"{scene}/{slots[i].Kind} requests unsupported {slots[i].Socket} socket.");
                }
            }
        }

        [Test]
        public void EightExpandedScenesResolveRequiredContentAcrossRepresentativeSeeds()
        {
            DecorationSpace space = Space();
            for (int raw = 0; raw <= (int)DecorationExpansion200SceneKind.CivicStreet; raw++)
            {
                DecorationExpansion200SceneKind scene = (DecorationExpansion200SceneKind)raw;
                DecorationExpansion200SceneSlot[] slots = DecorationExpansion200SceneCatalog.Slots(scene);
                for (uint seed = 1; seed <= 12; seed++)
                {
                    DecorationContext context = Context(seed);
                    Assert.IsTrue(DecorationExpansion200SceneResolver.TryResolve(
                        scene, in space, in context, null, out DecorationPlacement[] placements),
                        $"{scene} failed seed {seed}.");
                    AssertRequired(slots, placements, scene, seed);
                    for (int i = 0; i < placements.Length; i++)
                    {
                        Assert.IsTrue(space.Bounds.Contains(in placements[i].Bounds), $"{scene} escaped bounds seed {seed}.");
                        Assert.IsTrue(DecorationExpandedContentVariants.IsExpanded(placements[i].Variant),
                            $"{scene} lost expanded identity seed {seed}.");
                        for (int j = i + 1; j < placements.Length; j++)
                            Assert.IsFalse(placements[i].Bounds.Overlaps(in placements[j].Bounds),
                                $"{scene} placements {i}/{j} overlapped seed {seed}.");
                    }
                }
            }
        }

        [Test]
        public void NewPacksHaveBackendAndInteractionDiversity()
        {
            DecorationContext context = Context(77u);
            bool mesh = false, thin = false, voxel = false, container = false, light = false, movable = false;
            for (ushort id = DecorationExpansion200Catalog.FirstId; id <= DecorationExpansion200Catalog.LastId; id++)
            {
                DecorationPropDescriptor d = DecorationExpansion200Catalog.Describe(
                    in context, 0xE2D17001u, id, (DecorationExpandedContentKind)id);
                mesh |= d.Backend == DecorationRenderBackend.ProceduralMesh;
                thin |= d.Backend == DecorationRenderBackend.ThinSurface;
                voxel |= d.Backend == DecorationRenderBackend.VoxelStamp;
                container |= (d.Interaction & DecorationInteractionFlags.Container) != 0;
                light |= (d.Interaction & DecorationInteractionFlags.EmitsLight) != 0;
                movable |= (d.Interaction & DecorationInteractionFlags.Movable) != 0;
            }
            Assert.Multiple(() =>
            {
                Assert.IsTrue(mesh);
                Assert.IsTrue(thin);
                Assert.IsTrue(voxel);
                Assert.IsTrue(container);
                Assert.IsTrue(light);
                Assert.IsTrue(movable);
            });
        }

        private static void AssertRequired(
            DecorationExpansion200SceneSlot[] slots,
            DecorationPlacement[] placements,
            DecorationExpansion200SceneKind scene,
            uint seed)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].Required) continue;
                bool found = false;
                for (int p = 0; p < placements.Length; p++)
                    if (placements[p].SlotId == slots[i].SlotId) found = true;
                Assert.IsTrue(found, $"{scene} missing required {slots[i].Kind} seed {seed}.");
            }
        }

        private static DecorationContext Context(uint seed) => new DecorationContext
        {
            WorldSeed = seed,
            StructureId = 0xE2001001u,
            SpaceId = 0xE2001002u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Frontier, seed),
            StructureKind = DecorationStructureKind.House,
            SpaceKind = DecorationSpaceKind.Storage,
            Wealth = DecorationWealthTier.Comfortable,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior,
        };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0xE2001002u,
            Kind = DecorationSpaceKind.Storage,
            Bounds = new DecorationBounds
            {
                Min = new int3(-260, 0, -220),
                MaxExclusive = new int3(260, 90, 220),
            },
        };
    }
}
