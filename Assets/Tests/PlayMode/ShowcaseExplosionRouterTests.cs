using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ShowcaseExplosionRouterTests
    {
        [Test]
        public void ConnectedImpactRequestsAuthorityWithoutLocalMutation()
        {
            var world = new RecordingWorld(changedVoxels: 73);
            var network = new RecordingNetwork(active: true, requestResult: true);
            int3 origin = new int3(12, 34, 56);

            ShowcaseExplosionRouteResult result = ShowcaseExplosionRouter.Apply(
                world, network, origin, 12, new float3(0f, 0f, 1f));

            Assert.IsTrue(result.Networked);
            Assert.IsTrue(result.RequestSent);
            Assert.AreEqual(0, result.ChangedVoxels);
            Assert.AreEqual(1, network.RequestCount);
            Assert.AreEqual(origin, network.LastOrigin);
            Assert.AreEqual(12, network.LastRadius);
            Assert.AreEqual(0, world.ExplosionCount,
                "Connected clients must not mutate their local world before server authority.");
        }

        [Test]
        public void ConnectedImpactNeverFallsBackLocallyWhileRequestIsWaiting()
        {
            var world = new RecordingWorld(changedVoxels: 73);
            var network = new RecordingNetwork(active: true, requestResult: false);

            ShowcaseExplosionRouteResult result = ShowcaseExplosionRouter.Apply(
                world, network, new int3(4, 5, 6), 8, new float3(1f, 0f, 0f));

            Assert.IsTrue(result.Networked);
            Assert.IsFalse(result.RequestSent);
            Assert.AreEqual(0, world.ExplosionCount,
                "A temporarily unready network request must not create a local-only crater.");
        }

        [Test]
        public void OfflineImpactPreservesExistingLocalExplosionPath()
        {
            var world = new RecordingWorld(changedVoxels: 41);
            var network = new RecordingNetwork(active: false, requestResult: true);

            ShowcaseExplosionRouteResult result = ShowcaseExplosionRouter.Apply(
                world, network, new int3(1, 2, 3), 9, new float3(0f, 1f, 0f));

            Assert.IsFalse(result.Networked);
            Assert.IsFalse(result.RequestSent);
            Assert.AreEqual(41, result.ChangedVoxels);
            Assert.AreEqual(1, world.ExplosionCount);
            Assert.AreEqual(0, network.RequestCount);
        }

        private sealed class RecordingWorld : IShowcaseExplosionWorld
        {
            private readonly int _changedVoxels;

            public RecordingWorld(int changedVoxels) => _changedVoxels = changedVoxels;

            public int ExplosionCount { get; private set; }

            public int Explode(int3 originVoxel, ushort radiusVoxels, float3 impulseDirection)
            {
                ExplosionCount++;
                return _changedVoxels;
            }
        }

        private sealed class RecordingNetwork : IShowcaseExplosionNetwork
        {
            private readonly bool _requestResult;

            public RecordingNetwork(bool active, bool requestResult)
            {
                IsActive = active;
                _requestResult = requestResult;
            }

            public bool IsActive { get; }
            public int RequestCount { get; private set; }
            public int3 LastOrigin { get; private set; }
            public int LastRadius { get; private set; }

            public bool TryRequestExplosion(int3 originVoxel, int radiusVoxels)
            {
                RequestCount++;
                LastOrigin = originVoxel;
                LastRadius = radiusVoxels;
                return _requestResult;
            }
        }
    }
}
