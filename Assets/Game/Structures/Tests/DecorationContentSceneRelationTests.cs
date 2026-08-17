using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationContentSceneRelationTests
    {
        [Test]
        public void TavernBarStaysInFrontOfKegServiceWallAcrossSeeds()
        {
            AssertRequiredInFront(DecorationContentSceneKind.TavernBar, 1u, 2u, 32u, 110, 90);
        }

        [Test]
        public void StableTroughStaysInFrontOfMangerAcrossSeeds()
        {
            AssertRequiredInFront(DecorationContentSceneKind.Stable, 1u, 2u, 32u, 100, 80);
        }

        [Test]
        public void MarketProduceStandClustersAroundPrimaryStallAcrossSeeds()
        {
            AssertRequiredAround(DecorationContentSceneKind.Market, 1u, 2u, 32u, 120);
        }

        [Test]
        public void CryptSecondaryFloorContentClustersAroundSarcophagusWhenSelected()
        {
            AssertOptionalAround(DecorationContentSceneKind.Crypt, 1u, new[] { 3u, 4u, 5u }, 32u, 120);
        }

        [Test]
        public void PrisonSecondaryFloorContentClustersAroundCageWhenSelected()
        {
            AssertOptionalAround(DecorationContentSceneKind.Prison, 1u, new[] { 3u, 5u, 6u }, 32u, 110);
        }

        [Test]
        public void CivicSecondaryFloorContentClustersAroundFountainWhenSelected()
        {
            AssertOptionalAround(DecorationContentSceneKind.CivicCorner, 1u, new[] { 3u, 4u, 5u }, 32u, 135);
        }

        private static void AssertRequiredInFront(
            DecorationContentSceneKind scene,
            uint anchorSlot,
            uint childSlot,
            uint seedCount,
            int maxForward,
            int maxLateral)
        {
            DecorationSpace space = Space();
            for (uint seed = 1; seed <= seedCount; seed++)
            {
                DecorationContext context = Context(seed);
                Assert.IsTrue(DecorationContentSceneResolver.TryResolve(
                    scene, in space, in context, null, out DecorationPlacement[] placements),
                    $"{scene} failed for seed {seed}.");

                DecorationPlacement anchor = Find(placements, anchorSlot);
                DecorationPlacement child = Find(placements, childSlot);
                Assert.Multiple(() =>
                {
                    Assert.IsTrue(anchor.IsWellFormed, $"{scene} anchor missing for seed {seed}.");
                    Assert.IsTrue(child.IsWellFormed, $"{scene} child missing for seed {seed}.");
                });

                int2 delta = CenterDelta(in anchor, in child);
                int forward = delta.x * anchor.Facing.x + delta.y * anchor.Facing.z;
                int lateral = math.abs(delta.x * anchor.Facing.z - delta.y * anchor.Facing.x);
                Assert.Multiple(() =>
                {
                    Assert.Greater(forward, 0, $"{scene} child was not in front for seed {seed}.");
                    Assert.LessOrEqual(forward, maxForward, $"{scene} child was too far forward for seed {seed}.");
                    Assert.LessOrEqual(lateral, maxLateral, $"{scene} child was too far lateral for seed {seed}.");
                });
            }
        }

        private static void AssertRequiredAround(
            DecorationContentSceneKind scene,
            uint anchorSlot,
            uint childSlot,
            uint seedCount,
            int maxDistance)
        {
            DecorationSpace space = Space();
            for (uint seed = 1; seed <= seedCount; seed++)
            {
                DecorationContext context = Context(seed);
                Assert.IsTrue(DecorationContentSceneResolver.TryResolve(
                    scene, in space, in context, null, out DecorationPlacement[] placements),
                    $"{scene} failed for seed {seed}.");

                DecorationPlacement anchor = Find(placements, anchorSlot);
                DecorationPlacement child = Find(placements, childSlot);
                Assert.Multiple(() =>
                {
                    Assert.IsTrue(anchor.IsWellFormed, $"{scene} anchor missing for seed {seed}.");
                    Assert.IsTrue(child.IsWellFormed, $"{scene} child missing for seed {seed}.");
                });
                AssertWithinRadius(in anchor, in child, maxDistance, scene.ToString(), seed);
            }
        }

        private static void AssertOptionalAround(
            DecorationContentSceneKind scene,
            uint anchorSlot,
            uint[] childSlots,
            uint seedCount,
            int maxDistance)
        {
            DecorationSpace space = Space();
            int observed = 0;
            for (uint seed = 1; seed <= seedCount; seed++)
            {
                DecorationContext context = Context(seed);
                Assert.IsTrue(DecorationContentSceneResolver.TryResolve(
                    scene, in space, in context, null, out DecorationPlacement[] placements),
                    $"{scene} failed for seed {seed}.");

                DecorationPlacement anchor = Find(placements, anchorSlot);
                Assert.IsTrue(anchor.IsWellFormed, $"{scene} anchor missing for seed {seed}.");
                for (int i = 0; i < childSlots.Length; i++)
                {
                    DecorationPlacement child = Find(placements, childSlots[i]);
                    if (!child.IsWellFormed)
                        continue;
                    observed++;
                    AssertWithinRadius(in anchor, in child, maxDistance, scene.ToString(), seed);
                }
            }
            Assert.Greater(observed, 0, $"{scene} never selected a relational optional floor detail.");
        }

        private static void AssertWithinRadius(
            in DecorationPlacement anchor,
            in DecorationPlacement child,
            int maxDistance,
            string label,
            uint seed)
        {
            int2 delta = CenterDelta(in anchor, in child);
            int manhattan = math.abs(delta.x) + math.abs(delta.y);
            Assert.LessOrEqual(manhattan, maxDistance,
                $"{label} child escaped relational cluster for seed {seed}: distance={manhattan}.");
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
            StructureId = 0xCE11A001u,
            SpaceId = 0xCE11A002u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Rustic, seed),
            StructureKind = DecorationStructureKind.House,
            SpaceKind = DecorationSpaceKind.Storage,
            Wealth = DecorationWealthTier.Comfortable,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior,
        };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0xCE11A002u,
            Kind = DecorationSpaceKind.Storage,
            Bounds = new DecorationBounds
            {
                Min = new int3(-180, 0, -160),
                MaxExclusive = new int3(180, 84, 160),
            },
        };
    }
}
