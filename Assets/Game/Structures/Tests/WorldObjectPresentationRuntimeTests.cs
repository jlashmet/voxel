using System.Collections.Generic;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class WorldObjectPresentationRuntimeTests
    {
        [Test]
        public void OpeningDoorPushesNonBlockingRotatedPresentation()
        {
            var a = new WorldObjectAuthoringSession(1u, 2u);
            WorldObjectId lever = a.Place(1u, WorldObjectKind.Lever, B(0), new int3(0, 0, 1));
            WorldObjectId door = a.Place(2u, WorldObjectKind.Door, B(4), new int3(1, 0, 0));
            a.Connect(1u, WorldObjectSignal.Activated, 2u, WorldObjectAction.Open);
            var scene = new WorldObjectGeneratedScene
            {
                Objects = a.BuildObjects(),
                Connections = a.BuildConnections(),
            };
            scene.Runtime = new WorldObjectSceneRuntime(scene.Objects, scene.Connections);
            var sink = new RecordingSink();

            using (var presentation = new WorldObjectPresentationRuntime(scene, sink))
            {
                Assert.IsTrue(sink.Plans.TryGetValue(door, out WorldObjectPresentationPlan closed));
                Assert.IsTrue(closed.BlocksNavigation);

                Assert.IsTrue(scene.Runtime.TryInteract(lever, WorldObjectInteraction.Primary, out _));
                Assert.IsTrue(sink.Plans.TryGetValue(door, out WorldObjectPresentationPlan opened));
                Assert.AreEqual(90, opened.RotationDegrees.y);
                Assert.IsFalse(opened.BlocksNavigation);
            }
        }

        [Test]
        public void DestroyedDynamicObjectIsRemovedFromPresentationSink()
        {
            var a = new WorldObjectAuthoringSession(3u, 4u);
            WorldObjectId wall = a.Place(1u, WorldObjectKind.BreakableWall, B(0), new int3(0, 0, 1));
            var scene = new WorldObjectGeneratedScene
            {
                Objects = a.BuildObjects(),
                Connections = a.BuildConnections(),
            };
            scene.Runtime = new WorldObjectSceneRuntime(scene.Objects, scene.Connections);
            var sink = new RecordingSink();

            using (var presentation = new WorldObjectPresentationRuntime(scene, sink))
            {
                Assert.IsTrue(sink.Plans.ContainsKey(wall));
                Assert.IsTrue(scene.Runtime.TryInteract(wall, WorldObjectInteraction.Attack, out _));
                Assert.IsFalse(sink.Plans.ContainsKey(wall));
                Assert.IsTrue(sink.Removed.Contains(wall));
            }
        }

        [Test]
        public void StaticFurnitureDoesNotCreateDynamicProxy()
        {
            var a = new WorldObjectAuthoringSession(5u, 6u);
            WorldObjectId bed = a.Place(1u, WorldObjectKind.Bed, B(0), new int3(0, 0, 1));
            var scene = new WorldObjectGeneratedScene
            {
                Objects = a.BuildObjects(),
                Connections = a.BuildConnections(),
            };
            scene.Runtime = new WorldObjectSceneRuntime(scene.Objects, scene.Connections);
            var sink = new RecordingSink();

            using (var presentation = new WorldObjectPresentationRuntime(scene, sink))
                Assert.IsFalse(sink.Plans.ContainsKey(bed));
        }

        private static DecorationBounds B(int x) => new DecorationBounds
        {
            Min = new int3(x, 0, 0),
            MaxExclusive = new int3(x + 3, 5, 3),
        };

        private sealed class RecordingSink : IWorldObjectPresentationSink
        {
            public readonly Dictionary<WorldObjectId, WorldObjectPresentationPlan> Plans =
                new Dictionary<WorldObjectId, WorldObjectPresentationPlan>();
            public readonly HashSet<WorldObjectId> Removed = new HashSet<WorldObjectId>();

            public void CreateOrUpdate(in WorldObjectPresentationPlan plan)
            {
                Plans[plan.Id] = plan;
                Removed.Remove(plan.Id);
            }

            public void Remove(WorldObjectId id)
            {
                Plans.Remove(id);
                Removed.Add(id);
            }
        }
    }
}
