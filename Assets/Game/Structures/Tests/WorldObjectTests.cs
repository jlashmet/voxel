using System.Collections.Generic;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class WorldObjectTests
    {
        [Test]
        public void AuthoringIsDeterministicAndLocalKeysAreStable()
        {
            WorldObjectDescriptor[] first = BuildSimpleDoorScene();
            WorldObjectDescriptor[] second = BuildSimpleDoorScene();

            Assert.AreEqual(first.Length, second.Length);
            for (int i = 0; i < first.Length; i++)
                Assert.AreEqual(first[i].Id, second[i].Id);
            Assert.AreNotEqual(first[0].Id, first[1].Id);
        }

        [Test]
        public void LeverSignalCanOpenDoorAndPersistResult()
        {
            var authoring = new WorldObjectAuthoringSession(100u, 200u);
            authoring.Place(1u, WorldObjectKind.Lever, Bounds(0), new int3(0, 0, 1));
            WorldObjectId doorId = authoring.Place(2u, WorldObjectKind.Door, Bounds(4), new int3(1, 0, 0));
            authoring.Connect(1u, WorldObjectSignal.Activated, 2u, WorldObjectAction.Open);

            WorldObjectDescriptor[] objects = authoring.BuildObjects();
            var graph = new WorldObjectSignalGraph(authoring.BuildConnections());
            var routed = new List<WorldObjectConnection>();
            Assert.AreEqual(1, graph.Route(objects[0].Id, WorldObjectSignal.Activated, routed));

            var store = new WorldObjectStateStore();
            WorldObjectResolvedState door = WorldObjectStateResolver.Resolve(in objects[1], store);
            Assert.IsTrue(WorldObjectActions.TryApply(in door, routed[0].Action, routed[0].Argument,
                out WorldObjectStateDelta delta, out WorldObjectSignal emitted));
            store.Set(in delta);

            WorldObjectResolvedState regenerated = WorldObjectStateResolver.Resolve(in objects[1], store);
            Assert.Multiple(() =>
            {
                Assert.AreEqual(doorId, regenerated.Descriptor.Id);
                Assert.IsTrue(regenerated.IsOpen);
                Assert.AreEqual(WorldObjectSignal.Opened, emitted);
                Assert.AreEqual(1, store.Count);
            });
        }

        [Test]
        public void LockedDoorRejectsOpenUntilUnlocked()
        {
            var authoring = new WorldObjectAuthoringSession(1u, 2u);
            authoring.Place(10u, WorldObjectKind.Door, Bounds(0), new int3(0, 0, 1),
                defaultState: WorldObjectStateFlags.Locked);
            WorldObjectDescriptor descriptor = authoring.BuildObjects()[0];
            WorldObjectResolvedState state = WorldObjectStateResolver.Resolve(in descriptor, null);

            Assert.IsFalse(WorldObjectActions.TryApply(in state, WorldObjectAction.Open, 0, out _, out _));
            Assert.IsTrue(WorldObjectActions.TryApply(in state, WorldObjectAction.Unlock, 0,
                out WorldObjectStateDelta unlocked, out _));

            var store = new WorldObjectStateStore();
            store.Set(in unlocked);
            WorldObjectResolvedState afterUnlock = WorldObjectStateResolver.Resolve(in descriptor, store);
            Assert.IsTrue(WorldObjectActions.TryApply(in afterUnlock, WorldObjectAction.Open, 0, out _, out _));
        }

        [Test]
        public void StatefulDecorationPromotesWithoutChangingStableId()
        {
            var placement = new DecorationPlacement
            {
                Id = new GeneratedPropId(12345UL),
                SceneId = 99u,
                SlotId = 4u,
                Family = DecorationPropFamily.Chest,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.Container | DecorationInteractionFlags.Lootable |
                              DecorationInteractionFlags.Destructible,
                Bounds = Bounds(0),
                Facing = new int3(0, 0, 1),
                Variant = 7u,
            };

            Assert.IsTrue(DecorationWorldObjectAdapter.TryCreate(in placement, out WorldObjectDescriptor descriptor));
            Assert.Multiple(() =>
            {
                Assert.AreEqual(placement.Id.Value, descriptor.Id.Value);
                Assert.AreEqual(WorldObjectKind.Chest, descriptor.Kind);
                Assert.IsTrue((descriptor.Capabilities & WorldObjectCapabilities.Container) != 0);
                Assert.IsTrue((descriptor.Capabilities & WorldObjectCapabilities.Persistent) != 0);
            });
        }

        [Test]
        public void CatalogContainsBroadInitialInteractableSet()
        {
            int configured = 0;
            for (int value = 1; value <= (int)WorldObjectKind.SpawnPoint; value++)
            {
                WorldObjectPreset preset = WorldObjectContentCatalog.Get((WorldObjectKind)value);
                if (preset.Kind != WorldObjectKind.Unknown) configured++;
            }
            Assert.GreaterOrEqual(configured, 45);
        }

        private static WorldObjectDescriptor[] BuildSimpleDoorScene()
        {
            var authoring = new WorldObjectAuthoringSession(0xC0FFEEu, 0xCA571Eu);
            authoring.Place(1u, WorldObjectKind.Lever, Bounds(0), new int3(0, 0, 1));
            authoring.Place(2u, WorldObjectKind.Door, Bounds(4), new int3(1, 0, 0));
            authoring.Connect(1u, WorldObjectSignal.Activated, 2u, WorldObjectAction.Open);
            return authoring.BuildObjects();
        }

        private static DecorationBounds Bounds(int x) => new DecorationBounds
        {
            Min = new int3(x, 0, 0),
            MaxExclusive = new int3(x + 2, 4, 2),
        };
    }
}
