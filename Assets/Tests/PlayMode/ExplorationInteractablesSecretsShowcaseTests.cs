using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ExplorationInteractablesSecretsShowcaseTests
    {
        private const uint Seed = 0x12345678u;

        [Test]
        public void InteractionClusterRoutesAllFourMechanismsAndPersistsSecretReveal()
        {
            var registry = new WorldObjectSceneRegistry();
            WorldObjectGeneratedScene scene = Load(registry);

            Assert.AreEqual(10, scene.Objects.Length);
            Assert.AreEqual(7, scene.Connections.Length);

            WorldObjectId proximity = Id(ExplorationInteractablesSecretsShowcase.ProximitySensorKey);
            WorldObjectId slidingDoor = Id(ExplorationInteractablesSecretsShowcase.SlidingDoorKey);
            Assert.IsTrue(scene.Runtime.TryInteract(proximity, WorldObjectInteraction.Enter, out _));
            AssertOpen(scene.Runtime, slidingDoor, true);
            Assert.IsTrue(scene.Runtime.TryInteract(proximity, WorldObjectInteraction.Exit, out _));
            AssertOpen(scene.Runtime, slidingDoor, false);

            WorldObjectId plate = Id(ExplorationInteractablesSecretsShowcase.PressurePlateKey);
            WorldObjectId pressureDoor = Id(ExplorationInteractablesSecretsShowcase.PressureDoorKey);
            Assert.IsTrue(scene.Runtime.TryInteract(plate, WorldObjectInteraction.Enter, out _));
            AssertOpen(scene.Runtime, pressureDoor, true);
            Assert.IsTrue(scene.Runtime.TryInteract(plate, WorldObjectInteraction.Exit, out _));
            AssertOpen(scene.Runtime, pressureDoor, false);

            WorldObjectId lever = Id(ExplorationInteractablesSecretsShowcase.BridgeLeverKey);
            WorldObjectId bridge = Id(ExplorationInteractablesSecretsShowcase.BridgeKey);
            AssertOpen(scene.Runtime, bridge, true); // retracted/up baseline
            Assert.IsTrue(scene.Runtime.TryInteract(lever, WorldObjectInteraction.Primary, out _));
            AssertOpen(scene.Runtime, bridge, false); // extended
            Assert.IsTrue(scene.Runtime.TryInteract(lever, WorldObjectInteraction.Primary, out _));
            AssertOpen(scene.Runtime, bridge, true); // retracted again

            WorldObjectId wall = Id(ExplorationInteractablesSecretsShowcase.SecretWallKey);
            WorldObjectId rubbleLeft = Id(ExplorationInteractablesSecretsShowcase.RubbleLeftKey);
            WorldObjectId rubbleRight = Id(ExplorationInteractablesSecretsShowcase.RubbleRightKey);
            WorldObjectId marker = Id(ExplorationInteractablesSecretsShowcase.SecretMarkerKey);
            AssertHidden(scene.Runtime, rubbleLeft, true);
            AssertHidden(scene.Runtime, rubbleRight, true);
            AssertHidden(scene.Runtime, marker, true);
            Assert.IsTrue(scene.Runtime.TryInteract(wall, WorldObjectInteraction.Attack, out _));
            AssertDestroyed(scene.Runtime, wall, true);
            AssertHidden(scene.Runtime, rubbleLeft, false);
            AssertHidden(scene.Runtime, rubbleRight, false);
            AssertHidden(scene.Runtime, marker, false);

            Assert.Greater(registry.Snapshot(ExplorationInteractablesSecretsShowcase.ParentId).Length, 0);
            Assert.IsTrue(registry.Unload(ExplorationInteractablesSecretsShowcase.ParentId));
            WorldObjectGeneratedScene reloaded = Load(registry);
            AssertDestroyed(reloaded.Runtime, wall, true);
            AssertHidden(reloaded.Runtime, rubbleLeft, false);
            AssertHidden(reloaded.Runtime, rubbleRight, false);
            AssertHidden(reloaded.Runtime, marker, false);
        }

        [Test]
        public void AuthoredClusterIsDeterministicAndBounded()
        {
            WorldObjectGeneratedScene first = Load(new WorldObjectSceneRegistry());
            WorldObjectGeneratedScene second = Load(new WorldObjectSceneRegistry());
            Assert.AreEqual(first.Objects.Length, second.Objects.Length);
            Assert.AreEqual(first.Connections.Length, second.Connections.Length);

            int3 min = ExplorationInteractablesSecretsShowcase.Origin;
            int3 maxExclusive = min + new int3(52, 16, 36);
            for (int i = 0; i < first.Objects.Length; i++)
            {
                Assert.AreEqual(first.Objects[i].Id, second.Objects[i].Id);
                Assert.AreEqual(first.Objects[i].Kind, second.Objects[i].Kind);
                Assert.AreEqual(first.Objects[i].Bounds.Min, second.Objects[i].Bounds.Min);
                Assert.AreEqual(first.Objects[i].Bounds.MaxExclusive, second.Objects[i].Bounds.MaxExclusive);
                Assert.That(math.all(first.Objects[i].Bounds.Min >= min), Is.True);
                Assert.That(math.all(first.Objects[i].Bounds.MaxExclusive <= maxExclusive), Is.True);
            }
        }

        private static WorldObjectGeneratedScene Load(WorldObjectSceneRegistry registry)
        {
            var authoring = new WorldObjectAuthoringSession(Seed, ExplorationInteractablesSecretsShowcase.ParentId);
            ExplorationInteractablesSecretsShowcase.Author(authoring, ExplorationInteractablesSecretsShowcase.Origin);
            return registry.LoadAuthored(
                ExplorationInteractablesSecretsShowcase.ParentId,
                authoring.BuildObjects(),
                authoring.BuildConnections());
        }

        private static WorldObjectId Id(uint localKey) =>
            ExplorationInteractablesSecretsShowcase.Id(Seed, localKey);

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

        private static void AssertDestroyed(WorldObjectSceneRuntime runtime, WorldObjectId id, bool expected)
        {
            Assert.IsTrue(runtime.TryResolve(id, out WorldObjectResolvedState resolved));
            Assert.AreEqual(expected, resolved.IsDestroyed);
        }
    }
}