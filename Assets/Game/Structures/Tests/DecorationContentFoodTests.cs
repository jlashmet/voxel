using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationContentFoodTests
    {
        [Test]
        public void FoodProductionPackCoversStableIdsEightyFiveThroughOneHundredFourteen()
        {
            DecorationContext context = Context(85u);
            for (int raw = (int)DecorationContentKind.PrepTable;
                 raw <= (int)DecorationContentKind.CellarCaskStack;
                 raw++)
            {
                DecorationContentKind kind = (DecorationContentKind)raw;
                DecorationContentRecipe recipe = DecorationContentCatalog.Recipe(kind);
                DecorationPropDescriptor descriptor = DecorationContentCatalog.Describe(
                    in context, 0x464F4431u, (uint)raw, kind);

                Assert.Multiple(() =>
                {
                    Assert.IsTrue(recipe.IsWellFormed, $"{kind} recipe malformed.");
                    Assert.AreEqual(DecorationContentCategory.FoodProduction, recipe.Category, $"{kind} wrong category.");
                    Assert.IsTrue(descriptor.IsWellFormed, $"{kind} descriptor malformed.");
                    Assert.AreEqual(kind, DecorationContentVariants.KindOf(descriptor.Variant), $"{kind} identity lost.");
                });
            }
        }

        [Test]
        public void FiveFoodScenesResolveRequiredContentAcrossSeeds()
        {
            DecorationSpace space = Space();
            for (int raw = 0; raw <= (int)DecorationContentFoodSceneKind.Pantry; raw++)
            {
                DecorationContentFoodSceneKind scene = (DecorationContentFoodSceneKind)raw;
                DecorationContentSceneSlot[] slots = DecorationContentFoodScenes.Slots(scene);
                for (uint seed = 1; seed <= 16; seed++)
                {
                    DecorationContext context = Context(seed);
                    Assert.IsTrue(DecorationContentFoodScenes.TryResolve(
                        scene, in space, in context, null, out DecorationPlacement[] placements),
                        $"Food scene {scene} failed for seed {seed}.");

                    for (int s = 0; s < slots.Length; s++)
                    {
                        if (!slots[s].Required)
                            continue;
                        Assert.IsTrue(Find(placements, slots[s].SlotId).IsWellFormed,
                            $"Food scene {scene} lost required slot {slots[s].SlotId} for seed {seed}.");
                    }

                    for (int i = 0; i < placements.Length; i++)
                    {
                        Assert.IsTrue(space.Bounds.Contains(in placements[i].Bounds));
                        for (int j = i + 1; j < placements.Length; j++)
                            Assert.IsFalse(placements[i].Bounds.Overlaps(in placements[j].Bounds),
                                $"Food scene {scene} placements {i}/{j} overlapped for seed {seed}.");
                    }
                }
            }
        }

        [Test]
        public void BakeryPrepTableStaysInFrontOfBreadOven()
        {
            AssertInFront(DecorationContentFoodSceneKind.Bakery, 1u, 2u, 32u);
        }

        [Test]
        public void PantryFlourBinStaysInFrontOfPantryCabinet()
        {
            AssertInFront(DecorationContentFoodSceneKind.Pantry, 1u, 2u, 32u);
        }

        [Test]
        public void BreweryAndWineryPrimaryVesselsStayClustered()
        {
            AssertAround(DecorationContentFoodSceneKind.Brewery, 1u, 2u, 32u, 120);
            AssertAround(DecorationContentFoodSceneKind.Brewery, 1u, 3u, 32u, 120);
            AssertAround(DecorationContentFoodSceneKind.Winery, 1u, 2u, 32u, 120);
        }

        private static void AssertInFront(
            DecorationContentFoodSceneKind scene,
            uint anchorSlot,
            uint childSlot,
            uint seedCount)
        {
            DecorationSpace space = Space();
            for (uint seed = 1; seed <= seedCount; seed++)
            {
                DecorationContext context = Context(seed);
                Assert.IsTrue(DecorationContentFoodScenes.TryResolve(
                    scene, in space, in context, null, out DecorationPlacement[] placements));
                DecorationPlacement anchor = Find(placements, anchorSlot);
                DecorationPlacement child = Find(placements, childSlot);
                Assert.Multiple(() =>
                {
                    Assert.IsTrue(anchor.IsWellFormed);
                    Assert.IsTrue(child.IsWellFormed);
                });
                int2 delta = Delta(in anchor, in child);
                int forward = delta.x * anchor.Facing.x + delta.y * anchor.Facing.z;
                Assert.Greater(forward, 0, $"{scene} child was not in front for seed {seed}.");
                Assert.LessOrEqual(forward, 110, $"{scene} child drifted too far for seed {seed}.");
            }
        }

        private static void AssertAround(
            DecorationContentFoodSceneKind scene,
            uint anchorSlot,
            uint childSlot,
            uint seedCount,
            int maxDistance)
        {
            DecorationSpace space = Space();
            for (uint seed = 1; seed <= seedCount; seed++)
            {
                DecorationContext context = Context(seed);
                Assert.IsTrue(DecorationContentFoodScenes.TryResolve(
                    scene, in space, in context, null, out DecorationPlacement[] placements));
                DecorationPlacement anchor = Find(placements, anchorSlot);
                DecorationPlacement child = Find(placements, childSlot);
                Assert.Multiple(() =>
                {
                    Assert.IsTrue(anchor.IsWellFormed);
                    Assert.IsTrue(child.IsWellFormed);
                });
                int2 delta = Delta(in anchor, in child);
                Assert.LessOrEqual(math.abs(delta.x) + math.abs(delta.y), maxDistance,
                    $"{scene} child escaped cluster for seed {seed}.");
            }
        }

        private static int2 Delta(in DecorationPlacement a, in DecorationPlacement b)
        {
            int ax = (a.Bounds.Min.x + a.Bounds.MaxExclusive.x) / 2;
            int az = (a.Bounds.Min.z + a.Bounds.MaxExclusive.z) / 2;
            int bx = (b.Bounds.Min.x + b.Bounds.MaxExclusive.x) / 2;
            int bz = (b.Bounds.Min.z + b.Bounds.MaxExclusive.z) / 2;
            return new int2(bx - ax, bz - az);
        }

        private static DecorationPlacement Find(DecorationPlacement[] placements, uint slotId)
        {
            if (placements != null)
                for (int i = 0; i < placements.Length; i++)
                    if (placements[i].SlotId == slotId)
                        return placements[i];
            return default;
        }

        private static DecorationContext Context(uint seed) => new DecorationContext
        {
            WorldSeed = seed,
            StructureId = 0xF00D0001u,
            SpaceId = 0xF00D0002u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Rustic, seed),
            StructureKind = DecorationStructureKind.House,
            SpaceKind = DecorationSpaceKind.Storage,
            Wealth = DecorationWealthTier.Comfortable,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior,
        };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0xF00D0002u,
            Kind = DecorationSpaceKind.Storage,
            Bounds = new DecorationBounds
            {
                Min = new int3(-200, 0, -180),
                MaxExclusive = new int3(200, 92, 180),
            },
        };
    }
}
