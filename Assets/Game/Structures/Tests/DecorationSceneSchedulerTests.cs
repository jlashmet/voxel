using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;

namespace Game.Structures.Tests
{
    public sealed class DecorationSceneSchedulerTests
    {
        [Test]
        public void RequiredDependentPullsOptionalAnchorAndOrdersAnchorFirst()
        {
            DecorationContext context = Context(0x11223344u);
            var slots = new[]
            {
                Slot(20, DecorationPropFamily.Painting, required: true, anchorSlotId: 10, weight: 1),
                Slot(10, DecorationPropFamily.Dresser, required: false, anchorSlotId: 0, weight: 1),
            };

            Assert.IsTrue(DecorationSceneScheduler.TrySelectAndOrder(
                in context, 0x53434E31u, slots, 0, out DecorationSceneSlot[] ordered));

            Assert.Multiple(() =>
            {
                Assert.AreEqual(2, ordered.Length);
                Assert.AreEqual(10u, ordered[0].SlotId);
                Assert.AreEqual(20u, ordered[1].SlotId);
                Assert.IsFalse(ordered[0].Required);
                Assert.IsTrue(ordered[1].Required);
            });
        }

        [Test]
        public void OptionalBudgetIsDeterministicAndCannotBreakDependencies()
        {
            DecorationContext context = Context(0x55667788u);
            var slots = new[]
            {
                Slot(1, DecorationPropFamily.Bed, required: true, anchorSlotId: 0, weight: 1),
                // Put the dependent before its anchor to prove array order cannot bypass dependency rules.
                Slot(3, DecorationPropFamily.Painting, required: false, anchorSlotId: 2, weight: ushort.MaxValue),
                Slot(2, DecorationPropFamily.Dresser, required: false, anchorSlotId: 0, weight: 2),
                Slot(4, DecorationPropFamily.WallTorch, required: false, anchorSlotId: 0, weight: 3),
            };

            Assert.IsTrue(DecorationSceneScheduler.TrySelectAndOrder(
                in context, 0x53434E32u, slots, 1, out DecorationSceneSlot[] first));
            Assert.IsTrue(DecorationSceneScheduler.TrySelectAndOrder(
                in context, 0x53434E32u, slots, 1, out DecorationSceneSlot[] second));

            Assert.Multiple(() =>
            {
                Assert.AreEqual(2, first.Length, "One required slot plus one optional slot should resolve.");
                Assert.AreEqual(first.Length, second.Length);
                Assert.AreEqual(1u, first[0].SlotId);
                Assert.AreEqual(first[1].SlotId, second[1].SlotId);
                Assert.AreNotEqual(3u, first[1].SlotId,
                    "The painting requires two optional slots of budget when its dresser is not selected.");
            });
        }

        [Test]
        public void OptionalDependentResolvesWhenBudgetCanCoverItsAnchorChain()
        {
            DecorationContext context = Context(0xA5A5A5A5u);
            var slots = new[]
            {
                Slot(2, DecorationPropFamily.Painting, required: false, anchorSlotId: 1, weight: ushort.MaxValue),
                Slot(1, DecorationPropFamily.Dresser, required: false, anchorSlotId: 0, weight: 1),
            };

            Assert.IsTrue(DecorationSceneScheduler.TrySelectAndOrder(
                in context, 0x53434E33u, slots, 1, out DecorationSceneSlot[] oneSlot));
            Assert.IsTrue(DecorationSceneScheduler.TrySelectAndOrder(
                in context, 0x53434E33u, slots, 2, out DecorationSceneSlot[] twoSlots));

            Assert.Multiple(() =>
            {
                Assert.AreEqual(1, oneSlot.Length);
                Assert.AreEqual(1u, oneSlot[0].SlotId,
                    "With one slot of budget only the independent anchor can fit.");
                Assert.AreEqual(2, twoSlots.Length);
                Assert.AreEqual(1u, twoSlots[0].SlotId);
                Assert.AreEqual(2u, twoSlots[1].SlotId);
            });
        }

        [Test]
        public void ZeroOptionalBudgetReturnsOnlyRequiredClosure()
        {
            DecorationContext context = Context(99u);
            var slots = new[]
            {
                Slot(1, DecorationPropFamily.Bed, required: true, anchorSlotId: 0, weight: 1),
                Slot(2, DecorationPropFamily.Rug, required: false, anchorSlotId: 1, weight: ushort.MaxValue),
                Slot(3, DecorationPropFamily.WallTorch, required: false, anchorSlotId: 0, weight: ushort.MaxValue),
            };

            Assert.IsTrue(DecorationSceneScheduler.TrySelectAndOrder(
                in context, 0x53434E34u, slots, 0, out DecorationSceneSlot[] ordered));

            Assert.Multiple(() =>
            {
                Assert.AreEqual(1, ordered.Length);
                Assert.AreEqual(1u, ordered[0].SlotId);
                Assert.IsTrue(ordered[0].Required);
            });
        }

        private static DecorationContext Context(uint seed) => new DecorationContext
        {
            WorldSeed = seed,
            StructureId = 0xCA571Eu,
            SpaceId = 0xBED001u,
            StyleId = 17u,
            StructureKind = DecorationStructureKind.Castle,
            SpaceKind = DecorationSpaceKind.Bedroom,
            Wealth = DecorationWealthTier.Comfortable,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior | DecorationEnvironmentTags.Residential,
        };

        private static DecorationSceneSlot Slot(
            uint slotId,
            DecorationPropFamily family,
            bool required,
            uint anchorSlotId,
            ushort weight) => new DecorationSceneSlot
        {
            SlotId = slotId,
            Family = family,
            RequestedSocket = family == DecorationPropFamily.Rug
                ? DecorationSocketKind.BesideAnchor
                : family == DecorationPropFamily.Painting
                    ? DecorationSocketKind.AboveAnchor
                    : DecorationSocketKind.Wall,
            AnchorSlotId = anchorSlotId,
            Weight = weight,
            Required = required,
        };
    }
}
