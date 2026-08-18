using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationContentWorkshopSceneTests
    {
        [Test]
        public void FourWorkshopScenesResolveRequiredContentAcrossSeeds()
        {
            DecorationSpace space = Space();
            for (int sceneRaw = 0; sceneRaw <= (int)DecorationContentWorkshopSceneKind.Pottery; sceneRaw++)
            {
                DecorationContentWorkshopSceneKind scene = (DecorationContentWorkshopSceneKind)sceneRaw;
                DecorationContentSceneSlot[] slots = DecorationContentWorkshopScenes.Slots(scene);
                for (uint seed = 1; seed <= 16; seed++)
                {
                    DecorationContext context = Context(seed);
                    Assert.IsTrue(DecorationContentWorkshopScenes.TryResolve(
                        scene, in space, in context, null, out DecorationPlacement[] placements),
                        $"Workshop {scene} failed for seed {seed}.");

                    for (int s = 0; s < slots.Length; s++)
                    {
                        if (!slots[s].Required)
                            continue;
                        Assert.IsTrue(Find(placements, slots[s].SlotId).IsWellFormed,
                            $"Workshop {scene} lost required slot {slots[s].SlotId} for seed {seed}.");
                    }

                    for (int i = 0; i < placements.Length; i++)
                    {
                        Assert.Multiple(() =>
                        {
                            Assert.IsTrue(placements[i].IsWellFormed);
                            Assert.IsTrue(space.Bounds.Contains(in placements[i].Bounds));
                            Assert.IsTrue(DecorationContentVariants.IsContent(placements[i].Variant));
                        });
                        for (int j = i + 1; j < placements.Length; j++)
                            Assert.IsFalse(placements[i].Bounds.Overlaps(in placements[j].Bounds),
                                $"Workshop {scene} placements {i}/{j} overlapped for seed {seed}.");
                    }
                }
            }
        }

        [Test]
        public void PotteryWheelStaysInFrontOfKilnAcrossSeeds()
        {
            DecorationSpace space = Space();
            for (uint seed = 1; seed <= 32; seed++)
            {
                DecorationContext context = Context(seed);
                Assert.IsTrue(DecorationContentWorkshopScenes.TryResolve(
                    DecorationContentWorkshopSceneKind.Pottery,
                    in space, in context, null, out DecorationPlacement[] placements));

                DecorationPlacement kiln = Find(placements, 1u);
                DecorationPlacement wheel = Find(placements, 2u);
                Assert.Multiple(() =>
                {
                    Assert.IsTrue(kiln.IsWellFormed);
                    Assert.IsTrue(wheel.IsWellFormed);
                });

                int2 delta = CenterDelta(in kiln, in wheel);
                int forward = delta.x * kiln.Facing.x + delta.y * kiln.Facing.z;
                Assert.Greater(forward, 0, $"Pottery wheel was not in front of kiln for seed {seed}.");
                Assert.LessOrEqual(forward, 100, $"Pottery wheel drifted too far from kiln for seed {seed}.");
            }
        }

        [Test]
        public void RequiredWorkStationsStayClusteredAroundPrimaryAnchor()
        {
            AssertRequiredAround(DecorationContentWorkshopSceneKind.Carpentry, 1u, 3u, 32u, 115);
            AssertRequiredAround(DecorationContentWorkshopSceneKind.Textile, 1u, 3u, 32u, 115);
            AssertRequiredAround(DecorationContentWorkshopSceneKind.Leather, 1u, 3u, 32u, 115);
        }

        private static void AssertRequiredAround(
            DecorationContentWorkshopSceneKind scene,
            uint anchorSlot,
            uint childSlot,
            uint seedCount,
            int maxDistance)
        {
            DecorationSpace space = Space();
            for (uint seed = 1; seed <= seedCount; seed++)
            {
                DecorationContext context = Context(seed);
                Assert.IsTrue(DecorationContentWorkshopScenes.TryResolve(
                    scene, in space, in context, null, out DecorationPlacement[] placements),
                    $"Workshop {scene} failed for seed {seed}.");

                DecorationPlacement anchor = Find(placements, anchorSlot);
                DecorationPlacement child = Find(placements, childSlot);
                Assert.Multiple(() =>
                {
                    Assert.IsTrue(anchor.IsWellFormed);
                    Assert.IsTrue(child.IsWellFormed);
                });
                int2 delta = CenterDelta(in anchor, in child);
                Assert.LessOrEqual(math.abs(delta.x) + math.abs(delta.y), maxDistance,
                    $"Workshop {scene} child escaped cluster for seed {seed}.");
            }
        }

        private static int2 CenterDelta(in DecorationPlacement a, in DecorationPlacement b)
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
            StructureId = 0xB0A70001u,
            SpaceId = 0xB0A70002u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Rustic, seed),
            StructureKind = DecorationStructureKind.House,
            SpaceKind = DecorationSpaceKind.Storage,
            Wealth = DecorationWealthTier.Comfortable,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior,
        };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0xB0A70002u,
            Kind = DecorationSpaceKind.Storage,
            Bounds = new DecorationBounds
            {
                Min = new int3(-180, 0, -160),
                MaxExclusive = new int3(180, 88, 160),
            },
        };
    }
}
