using Game.Structures.Api;
using Game.Structures.Runtime;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ExplorationInteractablesSecretsShowcaseTests
    {
        private const uint Seed = 0x12345678u;

        [Test]
        public void GenericSourcesDriveDifferentMechanismsAndRecloseWithoutPairSpecificCode()
        {
            WorldObjectGeneratedScene scene = Load(new WorldObjectSceneRegistry());

            WorldObjectId plate = Id(ExplorationInteractablesSecretsShowcase.PressurePlateKey);
            WorldObjectId door = Id(ExplorationInteractablesSecretsShowcase.PressureDoorKey);
            Assert.IsTrue(scene.Runtime.TryInteract(plate, WorldObjectInteraction.Enter, out _));
            AssertOpen(scene.Runtime, door, true);
            Assert.IsTrue(scene.Runtime.TryInteract(plate, WorldObjectInteraction.Exit, out _));
            AssertOpen(scene.Runtime, door, false);

            WorldObjectId lever = Id(ExplorationInteractablesSecretsShowcase.BridgeLeverKey);
            WorldObjectId bridge = Id(ExplorationInteractablesSecretsShowcase.BridgeKey);
            AssertOpen(scene.Runtime, bridge, true);
            Assert.IsTrue(scene.Runtime.TryInteract(lever, WorldObjectInteraction.Primary, out _));
            AssertOpen(scene.Runtime, bridge, false);
            Assert.IsTrue(scene.Runtime.TryInteract(lever, WorldObjectInteraction.Primary, out _));
            AssertOpen(scene.Runtime, bridge, true);

            WorldObjectId button = Id(ExplorationInteractablesSecretsShowcase.VisibleButtonKey);
            WorldObjectId gate = Id(ExplorationInteractablesSecretsShowcase.ButtonGateKey);
            Assert.IsTrue(scene.Runtime.TryInteract(button, WorldObjectInteraction.Primary, out _));
            AssertOpen(scene.Runtime, gate, true);
        }

        [Test]
        public void RequiredSecretCompositionsUseSharedRuntimeAndCanonicalDiscoveryIsIdempotent()
        {
            WorldObjectGeneratedScene scene = Load(new WorldObjectSceneRegistry());

            WorldObjectId hiddenButton = Id(ExplorationInteractablesSecretsShowcase.HiddenBookshelfButtonKey);
            WorldObjectId panel = Id(ExplorationInteractablesSecretsShowcase.BookshelfPanelKey);
            WorldObjectId marker = Id(ExplorationInteractablesSecretsShowcase.BookshelfSecretMarkerKey);
            AssertHidden(scene.Runtime, hiddenButton, true);
            AssertHidden(scene.Runtime, panel, true);
            AssertHidden(scene.Runtime, marker, true);
            Assert.IsTrue(scene.Runtime.TryInteract(hiddenButton, WorldObjectInteraction.Primary, out _));
            AssertHidden(scene.Runtime, panel, false);
            AssertHidden(scene.Runtime, marker, false);
            AssertOpen(scene.Runtime, panel, true);

            WorldObjectId elevator = Id(ExplorationInteractablesSecretsShowcase.ElevatorKey);
            Assert.IsTrue(scene.Runtime.TryInteract(elevator, WorldObjectInteraction.Primary, out _));
            Assert.That(ContainsDelta(scene.Runtime.SnapshotState(), elevator), Is.True,
                "Operating the lift must change shared runtime state.");
            WorldObjectDescriptor liftDescriptor = Find(scene, ExplorationInteractablesSecretsShowcase.ElevatorKey);
            WorldObjectDescriptor elevatedSecret = Find(scene, ExplorationInteractablesSecretsShowcase.ElevatedSecretKey);
            Assert.Greater(elevatedSecret.Bounds.Min.y, liftDescriptor.Bounds.MaxExclusive.y,
                "The elevated secret must begin above the lift's baseline platform.");

            WorldObjectId routeLever = Id(ExplorationInteractablesSecretsShowcase.SecretRouteLeverKey);
            WorldObjectId routeGate = Id(ExplorationInteractablesSecretsShowcase.SecretRouteGateKey);
            Assert.IsTrue(scene.Runtime.TryInteract(routeLever, WorldObjectInteraction.Primary, out _));
            AssertOpen(scene.Runtime, routeGate, true);
            Assert.IsTrue(scene.Runtime.TryInteract(routeLever, WorldObjectInteraction.Primary, out _));
            AssertOpen(scene.Runtime, routeGate, false);

            var discoveries = new SecretDiscoveryState();
            var bookshelfSecret = new SecretCandidateId("showcase.bookshelf-passage");
            var highSecret = new SecretCandidateId("showcase.elevator-high-place");
            var remoteSecret = new SecretCandidateId("showcase.remote-lever-route");
            int events = 0;
            discoveries.Discovered += _ => events++;
            Assert.IsTrue(discoveries.TryDiscover(bookshelfSecret));
            Assert.IsFalse(discoveries.TryDiscover(bookshelfSecret));
            Assert.IsTrue(discoveries.TryDiscover(highSecret));
            Assert.IsFalse(discoveries.TryDiscover(highSecret));
            Assert.IsTrue(discoveries.TryDiscover(remoteSecret));
            Assert.IsFalse(discoveries.TryDiscover(remoteSecret));
            Assert.AreEqual(3, discoveries.Count);
            Assert.AreEqual(3, events);
        }

        [Test]
        public void DirectDoorTrapdoorAndDeterministicResetPreserveExistingBehavior()
        {
            WorldObjectGeneratedScene scene = Load(new WorldObjectSceneRegistry());
            WorldObjectId door = Id(ExplorationInteractablesSecretsShowcase.NormalDoorKey);
            WorldObjectId trapdoor = Id(ExplorationInteractablesSecretsShowcase.TrapdoorKey);

            Assert.IsTrue(scene.Runtime.TryInteract(door, WorldObjectInteraction.Primary, out _));
            AssertOpen(scene.Runtime, door, true);
            Assert.IsTrue(scene.Runtime.TryInteract(trapdoor, WorldObjectInteraction.Primary, out _));
            AssertOpen(scene.Runtime, trapdoor, true);
            Assert.Greater(scene.Runtime.ResetAll(), 0);
            AssertOpen(scene.Runtime, door, false);
            AssertOpen(scene.Runtime, trapdoor, false);

            var discoveries = new SecretDiscoveryState();
            discoveries.TryDiscover(new SecretCandidateId("showcase.reset-proof"));
            discoveries.Reset();
            Assert.AreEqual(0, discoveries.Count);
        }

        [Test]
        public void AuthoredClusterIsDeterministicBoundedAndContainsAcceptedStandaloneKinds()
        {
            WorldObjectGeneratedScene first = Load(new WorldObjectSceneRegistry());
            WorldObjectGeneratedScene second = Load(new WorldObjectSceneRegistry());
            Assert.AreEqual(first.Objects.Length, second.Objects.Length);
            Assert.AreEqual(first.Connections.Length, second.Connections.Length);

            int3 min = ExplorationInteractablesSecretsShowcase.Origin;
            int3 maxExclusive = min + ExplorationInteractablesSecretsShowcase.Extents;
            for (int i = 0; i < first.Objects.Length; i++)
            {
                Assert.AreEqual(first.Objects[i].Id, second.Objects[i].Id);
                Assert.AreEqual(first.Objects[i].Kind, second.Objects[i].Kind);
                Assert.AreEqual(first.Objects[i].Bounds.Min, second.Objects[i].Bounds.Min);
                Assert.AreEqual(first.Objects[i].Bounds.MaxExclusive, second.Objects[i].Bounds.MaxExclusive);
                Assert.That(math.all(first.Objects[i].Bounds.Min >= min), Is.True);
                Assert.That(math.all(first.Objects[i].Bounds.MaxExclusive <= maxExclusive), Is.True);
            }

            AssertKind(first, WorldObjectKind.Lever);
            AssertKind(first, WorldObjectKind.Button);
            AssertKind(first, WorldObjectKind.PressurePlate);
            AssertKind(first, WorldObjectKind.Door);
            AssertKind(first, WorldObjectKind.Trapdoor);
            AssertKind(first, WorldObjectKind.Gate);
            AssertKind(first, WorldObjectKind.Portcullis);
            AssertKind(first, WorldObjectKind.Elevator);
            AssertKind(first, WorldObjectKind.Drawbridge);
            AssertKind(first, WorldObjectKind.RotatingWall);
        }

        private static WorldObjectGeneratedScene Load(WorldObjectSceneRegistry registry)
        {
            var authoring = new WorldObjectAuthoringSession(Seed, ExplorationInteractablesSecretsShowcase.ParentId);
            ExplorationInteractablesSecretsShowcase.Author(authoring, ExplorationInteractablesSecretsShowcase.Origin);
            return registry.LoadAuthored(ExplorationInteractablesSecretsShowcase.ParentId,
                authoring.BuildObjects(), authoring.BuildConnections());
        }

        private static WorldObjectId Id(uint localKey) => ExplorationInteractablesSecretsShowcase.Id(Seed, localKey);

        private static WorldObjectDescriptor Find(WorldObjectGeneratedScene scene, uint localKey)
        {
            for (int i = 0; i < scene.Objects.Length; i++)
                if (scene.Objects[i].LocalKey == localKey) return scene.Objects[i];
            Assert.Fail("Missing authored local key " + localKey);
            return default;
        }

        private static bool ContainsDelta(WorldObjectStateDelta[] deltas, WorldObjectId id)
        {
            for (int i = 0; i < deltas.Length; i++) if (deltas[i].Id == id) return true;
            return false;
        }

        private static void AssertKind(WorldObjectGeneratedScene scene, WorldObjectKind kind)
        {
            for (int i = 0; i < scene.Objects.Length; i++) if (scene.Objects[i].Kind == kind) return;
            Assert.Fail("Showcase is missing accepted kind " + kind);
        }

        private static void AssertOpen(WorldObjectSceneRuntime runtime, WorldObjectId id, bool expected)
        {
            Assert.IsTrue(runtime.TryResolve(id, out WorldObjectResolvedState resolved));
            Assert.AreEqual(expected, resolved.IsOpen);
        }

        private static void AssertHidden(WorldObjectSceneRuntime runtime, WorldObjectId id, bool expected)
        {
            Assert.IsTrue(runtime.TryResolve(id, out WorldObjectResolvedState resolved));
            Assert.AreEqual(expected, (resolved.State & WorldObjectStateFlags.Hidden) != 0);
        }
    }
}
