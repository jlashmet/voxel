using Game.Composition.WorldObjects.Runtime;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldObjectRuntimeCompositionTests
    {
        [Test]
        public void TickAdvancesExternallyOwnedRegistryExactlyOnce()
        {
            var root = new GameObject("WorldObjectRuntimeCompositionTests");
            var composition = root.AddComponent<WorldObjectRuntimeComposition>();
            var registry = new WorldObjectSceneRegistry();
            const uint parentId = 0x54455354u;
            CastlePlan plan = default;

            try
            {
                WorldObjectGeneratedScene scene = registry.LoadCastle(
                    geometry: null,
                    worldSeed: 123u,
                    parentId: parentId,
                    plan: in plan,
                    emissionMode: WorldObjectGeometryEmissionMode.None);

                Assert.AreEqual(1, composition.ActiveRegistryCount);
                Assert.AreEqual(1, composition.PresentedSceneCount);

                WorldObjectDescriptor timedCrusher = default;
                bool found = false;
                for (int i = 0; i < scene.Objects.Length; i++)
                {
                    if (scene.Objects[i].Kind != WorldObjectKind.Crusher || scene.Objects[i].Parameter0 <= 0)
                        continue;
                    timedCrusher = scene.Objects[i];
                    found = true;
                    break;
                }

                Assert.IsTrue(found, "Generated castle must contain a timed crusher regression target.");
                Assert.IsTrue(scene.Runtime.TryInteract(
                    timedCrusher.Id, WorldObjectInteraction.Primary, out _));
                Assert.IsTrue(scene.Runtime.TryResolve(timedCrusher.Id, out WorldObjectResolvedState triggered));
                Assert.AreNotEqual(0, triggered.State & WorldObjectStateFlags.Triggered);
                Assert.AreEqual(timedCrusher.Parameter0, triggered.RuntimeValue1);

                Assert.Greater(composition.Tick(timedCrusher.Parameter0 - 1), 0);
                Assert.IsTrue(scene.Runtime.TryResolve(timedCrusher.Id, out WorldObjectResolvedState almostReset));
                Assert.AreNotEqual(0, almostReset.State & WorldObjectStateFlags.Triggered);
                Assert.AreEqual(1, almostReset.RuntimeValue1);

                Assert.Greater(composition.Tick(1), 0);
                Assert.IsTrue(scene.Runtime.TryResolve(timedCrusher.Id, out WorldObjectResolvedState reset));
                Assert.AreEqual(0, reset.State & WorldObjectStateFlags.Triggered);
                Assert.AreEqual(0, reset.RuntimeValue1);
            }
            finally
            {
                registry.Unload(parentId);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MultipleScenesInOneRegistryDoNotMultiplyRegistryTicks()
        {
            var root = new GameObject("WorldObjectRuntimeCompositionMultiSceneTests");
            var composition = root.AddComponent<WorldObjectRuntimeComposition>();
            var registry = new WorldObjectSceneRegistry();
            DecorationPlacement[] placements = { ChestPlacement() };

            try
            {
                registry.LoadDecorations(100u, placements);
                registry.LoadDecorations(101u, placements);

                Assert.AreEqual(2, composition.PresentedSceneCount);
                Assert.AreEqual(1, composition.ActiveRegistryCount);

                Assert.IsTrue(registry.Unload(100u));
                Assert.AreEqual(1, composition.PresentedSceneCount);
                Assert.AreEqual(1, composition.ActiveRegistryCount);

                Assert.IsTrue(registry.Unload(101u));
                Assert.AreEqual(0, composition.PresentedSceneCount);
                Assert.AreEqual(0, composition.ActiveRegistryCount);
            }
            finally
            {
                registry.Unload(100u);
                registry.Unload(101u);
                Object.DestroyImmediate(root);
            }
        }

        private static DecorationPlacement ChestPlacement() => new DecorationPlacement
        {
            Id = new GeneratedPropId(987654UL),
            SceneId = 10u,
            SlotId = 11u,
            Family = DecorationPropFamily.Chest,
            Backend = DecorationRenderBackend.BoxAssembly,
            Interaction = DecorationInteractionFlags.Container | DecorationInteractionFlags.Lootable,
            Bounds = new DecorationBounds
            {
                Min = Unity.Mathematics.int3.zero,
                MaxExclusive = new Unity.Mathematics.int3(12, 8, 8),
            },
            Facing = new Unity.Mathematics.int3(0, 0, 1),
            Variant = 1u,
        };
    }
}
