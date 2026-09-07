using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class WorldObjectShowcaseMountTests
    {
        [Test]
        public void CanonicalTrapdoorBaseline_IsHorizontalForIndependentConsumers()
        {
            int3 size = WorldObjectCatalogQuery.BaselineSize(WorldObjectKind.Trapdoor);
            Assert.That(size.x, Is.GreaterThan(0));
            Assert.That(size.y, Is.GreaterThan(0).And.LessThan(size.x));
            Assert.That(size.y, Is.LessThan(size.z));

            // Independently consume the production size query, not the showcase's camera or UI.
            var authoring = new WorldObjectAuthoringSession(31u, 41u);
            authoring.Place(1u, WorldObjectKind.Trapdoor,
                new DecorationBounds { Min = int3.zero, MaxExclusive = size }, new int3(0, 1, 0));
            WorldObjectDescriptor[] objects = authoring.BuildObjects();
            Assert.That(objects.Length, Is.EqualTo(1));
            WorldObjectResolvedState state = WorldObjectStateResolver.Resolve(in objects[0], new WorldObjectStateStore());
            WorldObjectPresentationPlan plan = WorldObjectPresentationPlanner.Plan(in state);
            Assert.That(plan.BaselineBounds.Size, Is.EqualTo(size));
            Assert.That(plan.RotationDegrees, Is.EqualTo(int3.zero));
            Assert.That(plan.UsesDynamicProxy, Is.True);
        }

        [TestCase(17u)]
        [TestCase(91u)]
        public void TrapdoorRealization_UsesFloorMountAndPreservesCanonicalIdentity(uint seed)
        {
            DecorationShowcaseRealization realization = Realize(WorldObjectKind.Trapdoor, seed);
            WorldObjectDescriptor descriptor = realization.WorldObject.Descriptor;
            Assert.That(descriptor.Facing, Is.EqualTo(new int3(0, 1, 0)));
            Assert.That(descriptor.Bounds.Size.y, Is.LessThan(descriptor.Bounds.Size.x));
            Assert.That(descriptor.Bounds.Size.y, Is.LessThan(descriptor.Bounds.Size.z));
            Assert.That(realization.WorldObject.IsOpen, Is.False);

            DecorationShowcaseRealization repeated = Realize(WorldObjectKind.Trapdoor, seed);
            Assert.That(repeated.Entry.StableId, Is.EqualTo(realization.Entry.StableId));
            Assert.That(repeated.WorldObject.Descriptor.Id, Is.EqualTo(descriptor.Id));
            Assert.That(repeated.Bounds.Min, Is.EqualTo(realization.Bounds.Min));
            Assert.That(repeated.Bounds.MaxExclusive, Is.EqualTo(realization.Bounds.MaxExclusive));
        }

        [Test]
        public void TrapdoorOpenAndClose_KeepHorizontalBaselineAndExistingPitchSemantics()
        {
            DecorationShowcaseRealization realization = Realize(WorldObjectKind.Trapdoor, 17u);
            WorldObjectDescriptor descriptor = realization.WorldObject.Descriptor;
            WorldObjectResolvedState closed = realization.WorldObject;
            Assert.That(WorldObjectActions.TryApply(in closed, WorldObjectAction.Open, 0,
                out WorldObjectStateDelta openDelta, out _), Is.True);
            var store = new WorldObjectStateStore();
            store.Set(openDelta);
            WorldObjectResolvedState opened = WorldObjectStateResolver.Resolve(in descriptor, store);
            WorldObjectPresentationPlan openedPlan = WorldObjectPresentationPlanner.Plan(in opened);
            Assert.That(openedPlan.RotationDegrees, Is.EqualTo(new int3(-90, 0, 0)));
            Assert.That(openedPlan.BlocksNavigation, Is.False);
            Assert.That(openedPlan.BaselineBounds.Size, Is.EqualTo(realization.Bounds.Size));
            Assert.That(openedPlan.TranslationVoxels, Is.EqualTo(int3.zero));

            Assert.That(WorldObjectActions.TryApply(in opened, WorldObjectAction.Close, 0,
                out WorldObjectStateDelta closeDelta, out _), Is.True);
            store.Set(closeDelta);
            WorldObjectResolvedState closedAgain = WorldObjectStateResolver.Resolve(in descriptor, store);
            WorldObjectPresentationPlan closedPlan = WorldObjectPresentationPlanner.Plan(in closedAgain);
            Assert.That(closedPlan.RotationDegrees, Is.EqualTo(int3.zero));
            Assert.That(closedPlan.BaselineBounds.Size, Is.EqualTo(realization.Bounds.Size));
            Assert.That(closedPlan.Visible, Is.True);
        }

        [TestCase(WorldObjectKind.Door)]
        [TestCase(WorldObjectKind.SecretDoor)]
        public void UprightDoorKinds_RetainWallFacingAndTallBaseline(WorldObjectKind kind)
        {
            DecorationShowcaseRealization realization = Realize(kind, 17u);
            WorldObjectDescriptor descriptor = realization.WorldObject.Descriptor;
            Assert.That(descriptor.Facing, Is.EqualTo(new int3(0, 0, 1)));
            Assert.That(descriptor.Bounds.Size.y, Is.GreaterThan(descriptor.Bounds.Size.x));
            Assert.That(descriptor.Bounds.Size.y, Is.GreaterThan(descriptor.Bounds.Size.z));
            WorldObjectPresentationPlan plan = WorldObjectPresentationPlanner.Plan(in realization.WorldObject);
            Assert.That(plan.RotationDegrees, Is.EqualTo(int3.zero));
            Assert.That(plan.Visible, Is.True);
        }

        private static DecorationShowcaseRealization Realize(WorldObjectKind kind, uint seed)
        {
            var context = new DecorationContext
            {
                WorldSeed = seed,
                StructureId = 0x50525032u,
                SpaceId = 0x50525033u,
                StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Rustic, seed),
                StructureKind = DecorationStructureKind.House,
                SpaceKind = DecorationSpaceKind.Storage,
                Wealth = DecorationWealthTier.Comfortable,
                Condition = DecorationConditionTier.Maintained,
                Environment = DecorationEnvironmentTags.Interior,
            };
            foreach (DecorationShowcaseEntry entry in DecorationShowcaseCatalog.CreateEntries())
            {
                if (entry.Source != DecorationShowcaseEntrySource.WorldObject ||
                    !DecorationShowcaseCatalog.TryGetWorldObjectPreset(entry.SourceId, out WorldObjectPreset preset) ||
                    preset.Kind != kind)
                    continue;
                Assert.That(DecorationShowcaseRealizer.TryCreate(in entry, in context,
                    out DecorationShowcaseRealization realization), Is.True);
                Assert.That(realization.IsWellFormed, Is.True);
                return realization;
            }
            Assert.Fail($"Canonical catalogue did not expose {kind}.");
            return default;
        }
    }
}
