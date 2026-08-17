using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class WorldObjectSceneRuntimeTests
    {
        [Test]
        public void LeverInteractionRoutesSignalAndOpensDoor()
        {
            var a = new WorldObjectAuthoringSession(11u, 22u);
            WorldObjectId lever = a.Place(1u, WorldObjectKind.Lever, B(0), new int3(0, 0, 1));
            WorldObjectId door = a.Place(2u, WorldObjectKind.Door, B(4), new int3(1, 0, 0));
            a.Connect(1u, WorldObjectSignal.Activated, 2u, WorldObjectAction.Open);

            var runtime = new WorldObjectSceneRuntime(a.BuildObjects(), a.BuildConnections());
            Assert.IsTrue(runtime.TryInteract(lever, WorldObjectInteraction.Primary, out _));
            Assert.IsTrue(runtime.TryResolve(door, out WorldObjectResolvedState state));
            Assert.IsTrue(state.IsOpen);
            Assert.Greater(runtime.StateStore.Count, 0);
        }

        [Test]
        public void PressurePlateRoutesPressedAndReleasedSignals()
        {
            var a = new WorldObjectAuthoringSession(33u, 44u);
            WorldObjectId plate = a.Place(1u, WorldObjectKind.PressurePlate, B(0), new int3(0, 1, 0));
            WorldObjectId spikes = a.Place(2u, WorldObjectKind.SpikeTrap, B(4), new int3(0, 1, 0));
            a.Connect(1u, WorldObjectSignal.Pressed, 2u, WorldObjectAction.Trigger);
            a.Connect(1u, WorldObjectSignal.Released, 2u, WorldObjectAction.Reset);

            var runtime = new WorldObjectSceneRuntime(a.BuildObjects(), a.BuildConnections());
            Assert.IsTrue(runtime.TryInteract(plate, WorldObjectInteraction.Enter, out _));
            Assert.IsTrue(runtime.TryResolve(spikes, out WorldObjectResolvedState triggered));
            Assert.IsTrue((triggered.State & WorldObjectStateFlags.Triggered) != 0);

            Assert.IsTrue(runtime.TryInteract(plate, WorldObjectInteraction.Exit, out _));
            Assert.IsTrue(runtime.TryResolve(spikes, out WorldObjectResolvedState reset));
            Assert.IsFalse((reset.State & WorldObjectStateFlags.Triggered) != 0);
        }

        [Test]
        public void DecorationPromotionPreservesIdAndUsesLiveRuntime()
        {
            var placement = new DecorationPlacement
            {
                Id = new GeneratedPropId(998877UL),
                SceneId = 9u,
                SlotId = 3u,
                Family = DecorationPropFamily.Chest,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.Container | DecorationInteractionFlags.Lootable |
                              DecorationInteractionFlags.Destructible,
                Bounds = B(0),
                Facing = new int3(0, 0, 1),
                Variant = 4u,
            };

            WorldObjectGeneratedScene scene = DecorationWorldObjectRuntimeBridge.Create(new[] { placement });
            Assert.AreEqual(1, scene.Objects.Length);
            Assert.AreEqual(placement.Id.Value, scene.Objects[0].Id.Value);
            Assert.IsTrue(scene.Runtime.TryInteract(scene.Objects[0].Id, WorldObjectInteraction.Primary, out _));
            Assert.IsTrue(scene.Runtime.TryResolve(scene.Objects[0].Id, out WorldObjectResolvedState opened));
            Assert.IsTrue(opened.IsOpen);
        }

        private static DecorationBounds B(int x) => new DecorationBounds
        {
            Min = new int3(x, 0, 0),
            MaxExclusive = new int3(x + 3, 5, 3),
        };
    }
}
