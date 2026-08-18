using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class UtilityRoomSceneStressTests
    {
        [Test]
        public void AllUtilityScenesRetainRequiredPlacementsAcrossRepresentativeSeeds()
        {
            for (int kindValue = (int)UtilityRoomSceneKind.GuardPost;
                 kindValue <= (int)UtilityRoomSceneKind.Storage;
                 kindValue++)
            {
                UtilityRoomSceneKind kind = (UtilityRoomSceneKind)kindValue;
                DecorationSpaceKind spaceKind = SpaceKind(kind);
                DecorationSpace space = Space(spaceKind, (uint)kindValue);
                int required = RequiredCount(kind);

                for (uint seed = 1; seed <= 32; seed++)
                {
                    DecorationContext context = Context(seed, spaceKind, kind);
                    Assert.IsTrue(UtilityRoomSceneResolver.TryResolve(
                        kind, in space, in context, null, out DecorationPlacement[] placements),
                        $"{kind} failed for seed {seed}.");
                    Assert.GreaterOrEqual(placements.Length, required,
                        $"{kind} lost a required slot for seed {seed}.");

                    for (int i = 0; i < placements.Length; i++)
                    {
                        Assert.IsTrue(space.Bounds.Contains(in placements[i].Bounds),
                            $"{kind} placement {i} escaped for seed {seed}.");
                        for (int j = i + 1; j < placements.Length; j++)
                            Assert.IsFalse(placements[i].Bounds.Overlaps(in placements[j].Bounds),
                                $"{kind} placements {i}/{j} overlap for seed {seed}.");
                    }
                }
            }
        }

        [Test]
        public void DiningHallRemainsCoherentAcrossRepresentativeSeeds()
        {
            DecorationSpace space = Space(DecorationSpaceKind.DiningRoom, 99u);
            for (uint seed = 1; seed <= 32; seed++)
            {
                DecorationContext context = Context(seed, DecorationSpaceKind.DiningRoom,
                    UtilityRoomSceneKind.ThroneRoom);
                Assert.IsTrue(DiningHallSceneResolver.TryResolve(
                    in space, in context, null, out DecorationPlacement[] placements),
                    $"Dining hall failed for seed {seed}.");
                Assert.AreEqual(1, Count(placements, DecorationPropFamily.Table),
                    $"Dining hall table count changed for seed {seed}.");
                Assert.AreEqual(2, Count(placements, DecorationPropFamily.Bench),
                    $"Dining hall bench count changed for seed {seed}.");
                Assert.AreEqual(1, Count(placements, DecorationPropFamily.Chandelier),
                    $"Dining hall chandelier missing for seed {seed}.");
            }
        }

        [Test]
        public void UtilitySceneIdsAreUnique()
        {
            var ids = new uint[8];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = UtilityRoomSceneCatalog.SceneId((UtilityRoomSceneKind)i);

            for (int i = 0; i < ids.Length; i++)
            {
                Assert.AreNotEqual(0u, ids[i]);
                for (int j = i + 1; j < ids.Length; j++)
                    Assert.AreNotEqual(ids[i], ids[j],
                        $"Scene IDs collided for {(UtilityRoomSceneKind)i} and {(UtilityRoomSceneKind)j}.");
            }
            for (int i = 0; i < ids.Length; i++)
                Assert.AreNotEqual(DiningHallSceneResolver.SceneId, ids[i],
                    $"Dining hall ID collides with {(UtilityRoomSceneKind)i}.");
        }

        private static int RequiredCount(UtilityRoomSceneKind kind)
        {
            DecorationSceneSlot[] slots = UtilityRoomSceneCatalog.CreateSlots(kind);
            int count = 0;
            for (int i = 0; i < slots.Length; i++) if (slots[i].Required) count++;
            return count;
        }

        private static int Count(DecorationPlacement[] placements, DecorationPropFamily family)
        {
            int count = 0;
            for (int i = 0; i < placements.Length; i++)
                if (placements[i].Family == family) count++;
            return count;
        }

        private static DecorationContext Context(
            uint seed,
            DecorationSpaceKind spaceKind,
            UtilityRoomSceneKind kind) => new DecorationContext
            {
                WorldSeed = seed,
                StructureId = 0x515CE001u + (uint)kind,
                SpaceId = 0x515CE100u + (uint)kind,
                StyleId = DecorationStyleIds.Compose(Style(kind), seed),
                StructureKind = DecorationStructureKind.Castle,
                SpaceKind = spaceKind,
                Wealth = (DecorationWealthTier)(seed % 5u),
                Condition = DecorationConditionTier.Maintained,
                Environment = DecorationEnvironmentTags.Interior,
            };

        private static DecorationStyleFamily Style(UtilityRoomSceneKind kind)
        {
            switch (kind)
            {
                case UtilityRoomSceneKind.GuardPost:
                case UtilityRoomSceneKind.Barracks: return DecorationStyleFamily.Martial;
                case UtilityRoomSceneKind.ChapelShrine: return DecorationStyleFamily.Sacred;
                case UtilityRoomSceneKind.Kitchen:
                case UtilityRoomSceneKind.Cellar:
                case UtilityRoomSceneKind.Storage: return DecorationStyleFamily.Rustic;
                default: return DecorationStyleFamily.Courtly;
            }
        }

        private static DecorationSpaceKind SpaceKind(UtilityRoomSceneKind kind)
        {
            switch (kind)
            {
                case UtilityRoomSceneKind.GuardPost: return DecorationSpaceKind.GuardPost;
                case UtilityRoomSceneKind.Kitchen:
                case UtilityRoomSceneKind.Cellar:
                case UtilityRoomSceneKind.Storage: return DecorationSpaceKind.Storage;
                case UtilityRoomSceneKind.LibraryStudy: return DecorationSpaceKind.Study;
                case UtilityRoomSceneKind.ChapelShrine: return DecorationSpaceKind.Chapel;
                case UtilityRoomSceneKind.Barracks: return DecorationSpaceKind.Bedroom;
                default: return DecorationSpaceKind.DiningRoom;
            }
        }

        private static DecorationSpace Space(DecorationSpaceKind kind, uint discriminator) =>
            new DecorationSpace
            {
                SpaceId = 0x515CE100u + discriminator,
                Kind = kind,
                Bounds = new DecorationBounds
                {
                    Min = new int3(-110, 10, -90),
                    MaxExclusive = new int3(110, 82, 90),
                },
            };
    }
}
