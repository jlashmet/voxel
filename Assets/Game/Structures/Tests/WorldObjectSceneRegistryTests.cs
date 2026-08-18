using System;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class WorldObjectSceneRegistryTests
    {
        [Test]
        public void DecorationStateSurvivesUnloadAndDeterministicReload()
        {
            const uint parentId = 77u;
            DecorationPlacement placement = ChestPlacement();
            var registry = new WorldObjectSceneRegistry();

            WorldObjectGeneratedScene first = registry.LoadDecorations(parentId, new[] { placement });
            WorldObjectId id = first.Objects[0].Id;
            Assert.IsTrue(first.Runtime.TryInteract(id, WorldObjectInteraction.Primary, out _));
            Assert.IsTrue(first.Runtime.TryResolve(id, out WorldObjectResolvedState opened));
            Assert.IsTrue(opened.IsOpen);

            Assert.IsTrue(registry.Unload(parentId));
            Assert.AreEqual(0, registry.LoadedSceneCount);
            Assert.AreEqual(1, registry.PersistentSceneCount);

            WorldObjectGeneratedScene second = registry.LoadDecorations(parentId, new[] { placement });
            Assert.AreEqual(id, second.Objects[0].Id);
            Assert.IsTrue(second.Runtime.TryResolve(id, out WorldObjectResolvedState restored));
            Assert.IsTrue(restored.IsOpen);
        }

        [Test]
        public void SnapshotCanRestoreStateIntoFreshRegistry()
        {
            const uint parentId = 88u;
            DecorationPlacement placement = ChestPlacement();
            var firstRegistry = new WorldObjectSceneRegistry();
            WorldObjectGeneratedScene first = firstRegistry.LoadDecorations(parentId, new[] { placement });
            WorldObjectId id = first.Objects[0].Id;
            Assert.IsTrue(first.Runtime.TryInteract(id, WorldObjectInteraction.Primary, out _));
            WorldObjectStateDelta[] snapshot = firstRegistry.Snapshot(parentId);
            Assert.Greater(snapshot.Length, 0);

            var restoredRegistry = new WorldObjectSceneRegistry();
            restoredRegistry.Restore(parentId, snapshot);
            WorldObjectGeneratedScene restoredScene = restoredRegistry.LoadDecorations(parentId, new[] { placement });
            Assert.IsTrue(restoredScene.Runtime.TryResolve(id, out WorldObjectResolvedState restored));
            Assert.IsTrue(restored.IsOpen);
        }

        [Test]
        public void DuplicateLoadedParentIsRejectedUntilUnload()
        {
            const uint parentId = 91u;
            var registry = new WorldObjectSceneRegistry();
            registry.LoadDecorations(parentId, new[] { ChestPlacement() });

            Assert.Throws<InvalidOperationException>(
                () => registry.LoadDecorations(parentId, new[] { ChestPlacement() }));

            Assert.IsTrue(registry.Unload(parentId));
            Assert.DoesNotThrow(() => registry.LoadDecorations(parentId, new[] { ChestPlacement() }));
        }

        [Test]
        public void RestoreWhileLoadedIsRejected()
        {
            const uint parentId = 92u;
            var registry = new WorldObjectSceneRegistry();
            registry.LoadDecorations(parentId, new[] { ChestPlacement() });

            Assert.Throws<InvalidOperationException>(
                () => registry.Restore(parentId, Array.Empty<WorldObjectStateDelta>()));
        }

        [Test]
        public void RegistryPublishesLoadAndUnloadWithoutTransferringOwnership()
        {
            const uint parentId = 93u;
            var registry = new WorldObjectSceneRegistry();
            WorldObjectSceneRegistry loadedRegistry = null;
            WorldObjectGeneratedScene loadedScene = null;
            WorldObjectSceneRegistry unloadedRegistry = null;
            uint loadedParent = 0;
            uint unloadedParent = 0;

            Action<WorldObjectSceneRegistry, uint, WorldObjectGeneratedScene> loaded =
                (owner, id, scene) =>
                {
                    loadedRegistry = owner;
                    loadedParent = id;
                    loadedScene = scene;
                };
            Action<WorldObjectSceneRegistry, uint> unloaded =
                (owner, id) =>
                {
                    unloadedRegistry = owner;
                    unloadedParent = id;
                };

            WorldObjectSceneLifecycle.Loaded += loaded;
            WorldObjectSceneLifecycle.Unloaded += unloaded;
            try
            {
                WorldObjectGeneratedScene scene = registry.LoadDecorations(parentId, new[] { ChestPlacement() });
                Assert.AreSame(registry, loadedRegistry);
                Assert.AreEqual(parentId, loadedParent);
                Assert.AreSame(scene, loadedScene);

                Assert.IsTrue(registry.Unload(parentId));
                Assert.AreSame(registry, unloadedRegistry);
                Assert.AreEqual(parentId, unloadedParent);
                Assert.AreEqual(1, registry.PersistentSceneCount,
                    "Presentation lifecycle observation must not take ownership of persistent state.");
            }
            finally
            {
                WorldObjectSceneLifecycle.Loaded -= loaded;
                WorldObjectSceneLifecycle.Unloaded -= unloaded;
            }
        }

        private static DecorationPlacement ChestPlacement() => new DecorationPlacement
        {
            Id = new GeneratedPropId(424242UL),
            SceneId = 99u,
            SlotId = 4u,
            Family = DecorationPropFamily.Chest,
            Backend = DecorationRenderBackend.BoxAssembly,
            Interaction = DecorationInteractionFlags.Container | DecorationInteractionFlags.Lootable |
                          DecorationInteractionFlags.Destructible,
            Bounds = new DecorationBounds
            {
                Min = int3.zero,
                MaxExclusive = new int3(12, 8, 8),
            },
            Facing = new int3(0, 0, 1),
            Variant = 1u,
        };
    }
}
