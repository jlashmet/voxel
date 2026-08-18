using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationRuntimeStateTests
    {
        [Test]
        public void RuntimePlanKeepsMetadataSeparateAndBatchesStaticPlacementsByBackend()
        {
            const int count = 1000;
            var placements = new DecorationPlacement[count];
            int expectedDynamic = 0;
            for (int i = 0; i < count; i++)
            {
                bool movable = i % 10 == 0;
                if (movable) expectedDynamic++;
                placements[i] = Placement(
                    i + 1,
                    (DecorationRenderBackend)(i % 4),
                    movable ? DecorationInteractionFlags.Movable : DecorationInteractionFlags.Destructible,
                    i % 3 == 0 ? DecorationPropFamily.Crate : DecorationPropFamily.Bed);
            }

            Assert.IsTrue(DecorationRuntimePlanner.TryBuild(placements, out DecorationRuntimePlan plan));

            int staticPlacementCount = 0;
            for (int i = 0; i < plan.StaticBatches.Length; i++)
                staticPlacementCount += plan.StaticBatches[i].PlacementIndices.Length;

            Assert.Multiple(() =>
            {
                Assert.AreEqual(count, plan.Metadata.Length);
                Assert.AreEqual(expectedDynamic, plan.DynamicProps.Length);
                Assert.AreEqual(count - expectedDynamic, staticPlacementCount);
                Assert.LessOrEqual(plan.StaticBatches.Length, 4,
                    "Static batching should scale with backend count, not prop count.");
                Assert.IsTrue(plan.Metadata[0].IsInteractable);
                Assert.AreEqual(placements[0].Id, plan.Metadata[0].Id);
            });
        }

        [Test]
        public void DetailPolicyDropsClutterBeforeStandardAndEssentialProps()
        {
            DecorationPlacement bed = Placement(1, DecorationRenderBackend.BoxAssembly,
                DecorationInteractionFlags.None, DecorationPropFamily.Bed);
            DecorationPlacement painting = Placement(2, DecorationRenderBackend.ThinSurface,
                DecorationInteractionFlags.None, DecorationPropFamily.Painting);
            DecorationPlacement crate = Placement(3, DecorationRenderBackend.BoxAssembly,
                DecorationInteractionFlags.None, DecorationPropFamily.Crate);
            var placements = new[] { bed, painting, crate };

            DecorationPlacement[] medium = DecorationDetailPolicy.Filter(placements, 300f);
            DecorationPlacement[] far = DecorationDetailPolicy.Filter(placements, 800f);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(DecorationDetailClass.Essential,
                    DecorationDetailPolicy.Classify(DecorationPropFamily.Bed));
                Assert.AreEqual(DecorationDetailClass.Standard,
                    DecorationDetailPolicy.Classify(DecorationPropFamily.Painting));
                Assert.AreEqual(DecorationDetailClass.Clutter,
                    DecorationDetailPolicy.Classify(DecorationPropFamily.Crate));
                Assert.AreEqual(2, medium.Length);
                Assert.AreEqual(DecorationPropFamily.Bed, medium[0].Family);
                Assert.AreEqual(DecorationPropFamily.Painting, medium[1].Family);
                Assert.AreEqual(1, far.Length);
                Assert.AreEqual(DecorationPropFamily.Bed, far[0].Family);
            });
        }

        [Test]
        public void PersistenceDeltasOverrideRegeneratedBaselineByStablePropId()
        {
            DecorationSpace space = BedroomSpace();
            DecorationContext context = BedroomContext();
            Assert.IsTrue(BedroomSceneResolver.TryResolve(
                in space, in context, null, out DecorationPlacement[] baseline));
            Assert.IsTrue(BedroomSceneResolver.TryResolve(
                in space, in context, null, out DecorationPlacement[] regenerated));

            DecorationPlacement movedBaseline = baseline[0];
            DecorationBounds movedBounds = movedBaseline.Bounds;
            movedBounds.Min += new int3(3, 0, 2);
            movedBounds.MaxExclusive += new int3(3, 0, 2);
            var deltas = new[]
            {
                new DecorationPersistenceDelta
                {
                    Id = baseline[0].Id,
                    Flags = DecorationPersistenceFlags.Moved,
                    MovedBounds = movedBounds,
                    MovedFacing = new int3(0, 0, 1),
                },
                new DecorationPersistenceDelta
                {
                    Id = baseline[2].Id,
                    Flags = DecorationPersistenceFlags.Looted,
                },
                new DecorationPersistenceDelta
                {
                    Id = baseline[4].Id,
                    Flags = DecorationPersistenceFlags.Destroyed,
                },
            };

            Assert.IsTrue(DecorationPersistenceResolver.TryApply(
                baseline, deltas, out DecorationResolvedState[] first));
            Assert.IsTrue(DecorationPersistenceResolver.TryApply(
                regenerated, deltas, out DecorationResolvedState[] second));

            Assert.Multiple(() =>
            {
                Assert.AreEqual(baseline[0].Id, first[0].Placement.Id);
                Assert.AreEqual(movedBounds.Min, first[0].Placement.Bounds.Min);
                Assert.AreEqual(new int3(0, 0, 1), first[0].Placement.Facing);
                Assert.IsTrue(first[0].IsVisible);

                Assert.AreEqual(baseline[2].Id, first[2].Placement.Id);
                Assert.IsTrue(first[2].IsLooted);
                Assert.IsTrue(first[2].IsVisible);

                Assert.AreEqual(baseline[4].Id, first[4].Placement.Id);
                Assert.IsFalse(first[4].IsVisible);

                Assert.AreEqual(baseline[1].Bounds.Min, first[1].Placement.Bounds.Min,
                    "Untouched deterministic props should retain their baseline transform.");
            });

            Assert.AreEqual(first.Length, second.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i].Placement.Id, second[i].Placement.Id);
                Assert.AreEqual(first[i].Placement.Bounds.Min, second[i].Placement.Bounds.Min);
                Assert.AreEqual(first[i].Placement.Bounds.MaxExclusive, second[i].Placement.Bounds.MaxExclusive);
                Assert.AreEqual(first[i].Persistence, second[i].Persistence);
            }
        }

        [Test]
        public void DuplicatePersistenceDeltasAreRejected()
        {
            DecorationPlacement placement = Placement(9, DecorationRenderBackend.BoxAssembly,
                DecorationInteractionFlags.Destructible, DecorationPropFamily.Bed);
            var deltas = new[]
            {
                new DecorationPersistenceDelta
                {
                    Id = placement.Id,
                    Flags = DecorationPersistenceFlags.Destroyed,
                },
                new DecorationPersistenceDelta
                {
                    Id = placement.Id,
                    Flags = DecorationPersistenceFlags.Looted,
                },
            };

            Assert.IsFalse(DecorationPersistenceResolver.TryApply(
                new[] { placement }, deltas, out _));
        }

        private static DecorationPlacement Placement(
            int id,
            DecorationRenderBackend backend,
            DecorationInteractionFlags interaction,
            DecorationPropFamily family) => new DecorationPlacement
        {
            Id = new GeneratedPropId((ulong)id),
            SceneId = 1u,
            SlotId = (uint)id,
            Family = family,
            Backend = backend,
            Interaction = interaction,
            Bounds = new DecorationBounds
            {
                Min = new int3(id * 2, 0, 0),
                MaxExclusive = new int3(id * 2 + 2, 3, 2),
            },
            Facing = new int3(0, 1, 0),
            Variant = (uint)id,
        };

        private static DecorationSpace BedroomSpace() => new DecorationSpace
        {
            SpaceId = 0xBED001u,
            Kind = DecorationSpaceKind.Bedroom,
            Bounds = new DecorationBounds
            {
                Min = new int3(-60, 10, -50),
                MaxExclusive = new int3(60, 58, 50),
            },
        };

        private static DecorationContext BedroomContext() => new DecorationContext
        {
            WorldSeed = 0xC0FFEEu,
            StructureId = 0xCA571Eu,
            SpaceId = 0xBED001u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Courtly, 11u),
            StructureKind = DecorationStructureKind.Castle,
            SpaceKind = DecorationSpaceKind.Bedroom,
            Wealth = DecorationWealthTier.Wealthy,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior | DecorationEnvironmentTags.Residential,
        };
    }
}
