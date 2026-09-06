using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuWaterSurfaceChunkCacheTests
    {
        private uint _previousWaterMask;
        private static readonly PropertyInfo WaterMaskProperty = typeof(VoxelPresentationCatalogue).GetProperty("WaterMaterialMask");
        private IVoxelStorageRuntime _storage;
        private GpuWaterSurfaceChunkCache _cache;
        private GameObject _cameraObject;
        private Camera _camera;
        private readonly int3[] _bricks = { new(0, 0, 0), new(1, 0, 0) };

        [SetUp]
        public void SetUp()
        {
            _previousWaterMask = VoxelPresentationCatalogue.WaterMaterialMask;
            WaterMaskProperty.SetValue(null, (1u << 11) | (1u << 16) | (1u << 22));
            _storage = VoxelEngineBootstrap.CreateStorage(1, 1);
            _storage.Residency.EnsureRegionResident(int3.zero);
            foreach (int3 brick in _bricks) _storage.Mutations.SetWholeBlock(brick, 11, false);
            _storage.PublishAllResidentRegions();
            _cache = new GpuWaterSurfaceChunkCache();
            _cameraObject = new GameObject("GPU water publication camera");
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.transform.position = new Vector3(0.8f, 0.4f, -3f);
            _camera.transform.LookAt(new Vector3(0.8f, 0.4f, 0.4f));
            _cache.InvalidateSurfaceBricks(_storage.Reads, _bricks);
        }

        [TearDown]
        public void TearDown()
        {
            _cache?.Dispose();
            // Test teardown only: drain actual outstanding GPU readers before retiring the test.
            AsyncGPUReadback.WaitAllRequests();
            _storage?.Dispose();
            WaterMaskProperty.SetValue(null, _previousWaterMask);
            Object.DestroyImmediate(_cameraObject);
        }

        private IEnumerator Pending()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (_cache.PendingUploadCount == 0 && Time.realtimeSinceStartupAsDouble < deadline)
            {
                _cache.Prepare(_storage.Reads, _camera, 0.1f, 5);
                yield return null;
            }
            Assert.That(_cache.PendingUploadCount, Is.EqualTo(1), "GPU water transaction did not finish.");
        }

        private GpuSurfacePageArena Arena => (GpuSurfacePageArena)typeof(GpuWaterSurfaceChunkCache)
            .GetField("_geometryArena", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_cache);

        private uint[] Live(int handle)
        {
            // Test-only observation: runtime never retrieves these geometry counts.
            var data = new uint[Arena.HandleCapacity * 8];
            Arena.LiveChunkGeometry.GetData(data);
            var record = new uint[8];
            System.Array.Copy(data, handle * 8, record, 0, 8);
            return record;
        }

        [UnityTest]
        public IEnumerator CanonicalSnapshotsPublishGpuGeometryAndIndirectArguments()
        {
            yield return Pending();
            Assert.That(_cache.TryPublishPending(16, out int bytes), Is.True);
            Assert.That(bytes, Is.Zero);
            Assert.That(_cache.UploadedGeometryBytes, Is.Zero);
            Assert.That(_cache.UploadedSnapshotBytes, Is.GreaterThan(0));
            var visible = _cache.CollectVisible(_camera, 0.1f);
            Assert.That(visible.Count, Is.EqualTo(1));
            uint[] live = Live(visible[0].Handle);
            Assert.That(live[7], Is.EqualTo(1));
            Assert.That(live[3], Is.GreaterThan(0));
            Assert.That(live[4], Is.GreaterThan(0));
            var argsBuffer = (ComputeBuffer)typeof(GpuWaterSurfaceChunkCache)
                .GetField("_drawArgs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_cache);
            var args = new uint[4];
            argsBuffer.GetData(args, 0, visible[0].Handle * 4, 4);
            Assert.That(args[0], Is.EqualTo(live[4]));
            Assert.That(args[1], Is.EqualTo(1));
            Assert.That(args[2], Is.Zero); Assert.That(args[3], Is.Zero);
        }

        [UnityTest]
        public IEnumerator EditAfterGpuCompletionRejectsCandidateAndRebuilds()
        {
            yield return Pending();
            _storage.Mutations.SetWholeBlock(_bricks[0], 16, false);
            _cache.InvalidateSurfaceBricks(_storage.Reads, _bricks);
            Assert.That(_cache.TryPublishPending(16, out _), Is.False);
            Assert.That(_cache.StaleBuildCount, Is.EqualTo(1));
            Assert.That(_cache.CompletedBuildCount, Is.Zero);
            yield return Pending();
            Assert.That(_cache.TryPublishPending(16, out _), Is.True);
            Assert.That(_cache.CompletedBuildCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SameChunkOccluderEditRebuildsAlreadyKnownWater()
        {
            yield return Pending();
            Assert.That(_cache.TryPublishPending(16, out _), Is.True);
            int handle = _cache.CollectVisible(_camera, 0.1f)[0].Handle;
            uint oldCount = Live(handle)[4];
            int3 occluder = new(0, 1, 0);
            _storage.Mutations.SetWholeBlock(occluder, 1, false);
            _cache.InvalidateSurfaceBricks(_storage.Reads, new[] { occluder });
            yield return Pending();
            Assert.That(_cache.TryPublishPending(16, out _), Is.True);
            Assert.That(Live(handle)[4], Is.LessThan(oldCount), "The stone block must hide the water's top face.");
        }

        [UnityTest]
        public IEnumerator AdditionalRegisteredWaterMaterialIsDiscoveredAndPublished()
        {
            foreach (int3 brick in _bricks) _storage.Mutations.SetWholeBlock(brick, 22, false);
            _cache.InvalidateSurfaceBricks(_storage.Reads, _bricks);
            yield return Pending();
            Assert.That(_cache.TryPublishPending(16, out _), Is.True);
            var visible = _cache.CollectVisible(_camera, 0.1f);
            Assert.That(visible.Count, Is.EqualTo(1));
            Assert.That(Live(visible[0].Handle)[4], Is.GreaterThan(0));
        }

        [Test]
        public void DisposalDefersResourcesUntilSubmittedWorkCompletes()
        {
            _cache.Prepare(_storage.Reads, _camera, 0.1f, 5);
            ComputeBuffer vertices = Arena.Vertices;
            _cache.Dispose();
            Assert.That(vertices.IsValid(), Is.True, "Disposal must retain GPU inputs/outputs until its completion callback.");
            Assert.DoesNotThrow(_cache.Dispose);
            AsyncGPUReadback.WaitAllRequests();
            Assert.That(vertices.IsValid(), Is.False);
        }
    }
}
