using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DiningSceneTests
    {
        [Test]
        public void DiningFamiliesAndRecipeAreWellFormed()
        {
            DecorationContext context = Context(17u, DecorationWealthTier.Noble, DecorationConditionTier.Maintained);
            DecorationSceneSlot[] slots = DiningSceneDefinition.CreateSlots();

            Assert.Multiple(() =>
            {
                Assert.IsTrue(DiningPropPresets.Table(in context).IsWellFormed);
                Assert.IsTrue(DiningPropPresets.Bench(in context, DiningSceneDefinition.BenchNegativeSlot).IsWellFormed);
                Assert.IsTrue(DiningPropPresets.Chair(in context, DiningSceneDefinition.ChairNegativeSlot).IsWellFormed);
                Assert.IsTrue(DecorationValidation.ValidateScene(slots, out uint error), $"Dining scene failed at slot {error}.");
            });
        }

        [Test]
        public void PoorDiningRoomUsesRequiredTableAndOpposedBenches()
        {
            DecorationSpace space = Space();
            DecorationContext context = Context(31u, DecorationWealthTier.Poor, DecorationConditionTier.Maintained);

            Assert.IsTrue(DiningSceneResolver.TryResolve(
                in space, in context, EntranceExclusions(), DiningLongAxis.X, out DecorationPlacement[] placements));

            Assert.Multiple(() =>
            {
                Assert.AreEqual(3, placements.Length);
                Assert.AreEqual(DecorationPropFamily.Table, placements[0].Family);
                Assert.AreEqual(DecorationPropFamily.Bench, placements[1].Family);
                Assert.AreEqual(DecorationPropFamily.Bench, placements[2].Family);
                Assert.AreEqual(DiningSceneDefinition.TableSlot, placements[1].AnchorSlotId);
                Assert.AreEqual(DiningSceneDefinition.TableSlot, placements[2].AnchorSlotId);
                Assert.AreEqual(-placements[1].Facing, placements[2].Facing);
            });
        }

        [Test]
        public void NobleDiningRoomAddsBothHeadChairsDeterministically()
        {
            DecorationSpace space = Space();
            DecorationContext context = Context(0xD1A1u, DecorationWealthTier.Noble, DecorationConditionTier.Maintained);
            DecorationExclusion[] exclusions = EntranceExclusions();

            Assert.IsTrue(DiningSceneResolver.TryResolve(
                in space, in context, exclusions, DiningLongAxis.X, out DecorationPlacement[] first));
            Assert.IsTrue(DiningSceneResolver.TryResolve(
                in space, in context, exclusions, DiningLongAxis.X, out DecorationPlacement[] second));

            Assert.Multiple(() =>
            {
                Assert.AreEqual(5, first.Length);
                Assert.AreEqual(DecorationPropFamily.Chair, first[3].Family);
                Assert.AreEqual(DecorationPropFamily.Chair, first[4].Family);
                Assert.AreEqual(DiningSceneDefinition.TableSlot, first[3].AnchorSlotId);
                Assert.AreEqual(DiningSceneDefinition.TableSlot, first[4].AnchorSlotId);
                Assert.AreEqual(-first[3].Facing, first[4].Facing);
            });

            for (int i = 0; i < first.Length; i++)
            {
                Assert.Multiple(() =>
                {
                    Assert.IsTrue(first[i].IsWellFormed, $"Placement {i} malformed.");
                    Assert.IsTrue(space.Bounds.Contains(in first[i].Bounds), $"Placement {i} escaped dining room.");
                    Assert.AreEqual(first[i].Id, second[i].Id, $"Placement {i} ID changed.");
                    Assert.AreEqual(first[i].Bounds.Min, second[i].Bounds.Min, $"Placement {i} minimum changed.");
                    Assert.AreEqual(first[i].Bounds.MaxExclusive, second[i].Bounds.MaxExclusive,
                        $"Placement {i} maximum changed.");
                });

                for (int e = 0; e < exclusions.Length; e++)
                    Assert.IsFalse(first[i].Bounds.Overlaps(in exclusions[e].Bounds),
                        $"Placement {i} overlapped exclusion {e}.");
            }
        }

        [Test]
        public void AbandonedDiningRoomDropsOptionalHeadChairs()
        {
            DecorationSpace space = Space();
            DecorationContext context = Context(73u, DecorationWealthTier.Noble, DecorationConditionTier.Abandoned);

            Assert.IsTrue(DiningSceneResolver.TryResolve(
                in space, in context, null, DiningLongAxis.Z, out DecorationPlacement[] placements));

            Assert.AreEqual(3, placements.Length);
            Assert.AreEqual(new int3(0, 0, 1), placements[0].Facing);
        }

        [Test]
        public void CentralGameplayExclusionExplicitlyRejectsRequiredTable()
        {
            DecorationSpace space = Space();
            DecorationContext context = Context(99u, DecorationWealthTier.Comfortable, DecorationConditionTier.Maintained);
            var exclusions = new[]
            {
                new DecorationExclusion
                {
                    Kind = DecorationExclusionKind.Gameplay | DecorationExclusionKind.Navigation,
                    Bounds = new DecorationBounds
                    {
                        Min = new int3(-30, space.Bounds.Min.y, -20),
                        MaxExclusive = new int3(30, space.Bounds.MaxExclusive.y, 20),
                    },
                },
            };

            Assert.IsFalse(DiningSceneResolver.TryResolve(
                in space, in context, exclusions, DiningLongAxis.X, out _));
        }

        private static DecorationContext Context(
            uint seed,
            DecorationWealthTier wealth,
            DecorationConditionTier condition) => new DecorationContext
            {
                WorldSeed = seed,
                StructureId = 0xD1A1A6u,
                SpaceId = 0xD1A100u,
                StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Courtly, seed),
                StructureKind = DecorationStructureKind.Castle,
                SpaceKind = DecorationSpaceKind.DiningRoom,
                Wealth = wealth,
                Condition = condition,
                Environment = DecorationEnvironmentTags.Interior,
            };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0xD1A100u,
            Kind = DecorationSpaceKind.DiningRoom,
            Bounds = new DecorationBounds
            {
                Min = new int3(-80, 10, -50),
                MaxExclusive = new int3(80, 50, 50),
            },
        };

        private static DecorationExclusion[] EntranceExclusions() => new[]
        {
            new DecorationExclusion
            {
                Kind = DecorationExclusionKind.Door | DecorationExclusionKind.Navigation,
                Bounds = new DecorationBounds
                {
                    Min = new int3(-10, 10, -50),
                    MaxExclusive = new int3(10, 36, -34),
                },
            },
        };
    }
}
