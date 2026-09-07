using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime.FarWorld;
using Object = UnityEngine.Object;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuFarInstanceBatchTests
    {
        private GameObject _root;
        private ProceduralFarFeatureRenderer _renderer;
        private ComputeShader _shader;
        private FarFeatureGeometry _geometry;

        [SetUp] public void SetUp()
        {
            Assert.That(SystemInfo.supportsComputeShaders, Is.True);
            _root = new GameObject("far-instance-bookkeeping-test");
            _renderer = _root.AddComponent<ProceduralFarFeatureRenderer>();
            _shader = Resources.Load<ComputeShader>("GpuFarInstanceCompact");
            // Real geometry realization; this fixture validates GPU bookkeeping, not visual finish.
            _geometry = new FarFeatureGeometry(new[] { new FarFeatureGeometryPrimitive(
                FarFeatureGeometryShape.Box, float3.zero, new float3(1)) }, new[] {
                new FarFeaturePresentation(new float4(1), 0.8f),
                new FarFeaturePresentation(new float4(0.5f, 0.2f, 0.1f, 1), 0.3f) });
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(_root); }
        private FarFeatureInstance Instance(int i) => new((ulong)i, new float3(i, 2, -3),
            quaternion.EulerXYZ(0.1f, 0.3f, 0.4f), new float3(2, 3, 4),
            new float3(i, 2, -3), new float3(4), "geometry", "style", FarFeatureTier.Mid,
            FarFeatureVisualFlags.None, _geometry);

        [TestCase(1)] [TestCase(65)] [TestCase(1025)]
        public void CompactionPreservesTransformsAndEverySubmeshAcrossReplacementChanges(int count)
        {
            var instances = new List<FarFeatureInstance>();
            for (int i = 0; i < count; i++) instances.Add(Instance(i));
            Mesh mesh = _renderer.ResolveMesh(instances[0]);
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = _renderer.ResolveMaterial(instances[0], i);
            using var batch = new GpuFarInstanceBatch(_shader, mesh, materials, instances);
            var transforms = new GpuFarInstanceBatch.InstanceTransform[count];
            batch.Transforms.GetData(transforms);
            for (int i = 0; i < count; i++)
            {
                Matrix4x4 expected = Matrix4x4.TRS(instances[i].Position, instances[i].Rotation, instances[i].Scale);
                Assert.AreEqual(expected, transforms[i].ObjectToWorld);
                Vector3 point = new(1, 2, 3);
                Assert.That(Vector3.Distance(point, transforms[i].WorldToObject.MultiplyPoint3x4(
                    transforms[i].ObjectToWorld.MultiplyPoint3x4(point))), Is.LessThan(0.0002f));
            }
            for (int cycle = 0; cycle < 6; cycle++)
            {
                int mode = cycle % 3;
                batch.UpdateReplacement(bounds => mode == 1 || (mode == 2 && (int)bounds.center.x % 2 == 0));
                batch.Prepare();
                int expectedCount = mode == 0 ? count : mode == 1 ? 0 : count / 2;
                var args = new uint[mesh.subMeshCount * 5]; batch.Arguments.GetData(args);
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    Assert.AreEqual(mesh.GetIndexCount(submesh), args[submesh * 5]);
                    Assert.AreEqual(expectedCount, args[submesh * 5 + 1]);
                    Assert.AreEqual(mesh.GetIndexStart(submesh), args[submesh * 5 + 2]);
                    Assert.AreEqual(mesh.GetBaseVertex(submesh), args[submesh * 5 + 3]);
                    Assert.AreEqual(0, args[submesh * 5 + 4]);
                }
                var indices = new uint[count]; batch.VisibleIndices.GetData(indices);
                var unique = new HashSet<uint>();
                for (int i = 0; i < expectedCount; i++)
                {
                    Assert.That(indices[i], Is.LessThan((uint)count));
                    Assert.That(unique.Add(indices[i]), Is.True);
                    if (mode == 2) Assert.AreEqual(1u, indices[i] % 2);
                }
                using (var command = new CommandBuffer())
                {
                    batch.RecordDraws(command);
                    if (expectedCount == 0) Assert.AreEqual(0, command.sizeInBytes,
                        "Fully replaced batches must not submit empty draws.");
                    else Assert.That(command.sizeInBytes, Is.GreaterThan(0));
                }
                int uploads = batch.VisibilityUploads;
                batch.Prepare();
                Assert.AreEqual(uploads, batch.VisibilityUploads, "Unchanged visibility must reuse resident draw data.");
            }
            batch.Dispose(); batch.Dispose();
            Assert.That(batch.Arguments, Is.Null);
            Assert.Throws<ObjectDisposedException>(() => batch.Prepare());
        }

        [Test]
        public void CapacityRejectionPreservesTheExistingScene()
        {
            typeof(ProceduralFarFeatureRenderer).GetField("maximumInstances",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(_renderer, 2);
            _renderer.SetInstances(new[] { Instance(0), Instance(1) });
            int builds = _renderer.BatchBuildCount;
            Assert.Throws<InvalidOperationException>(() =>
                _renderer.SetInstances(new[] { Instance(0), Instance(1), Instance(2) }));
            Assert.AreEqual(2, _renderer.InstanceCount);
            Assert.AreEqual(builds, _renderer.BatchBuildCount);
        }

        [Test]
        public void CameraHandoffAndRepeatedEnablePreserveBatchesUntilSourceChanges()
        {
            var instances = new[] { Instance(1), Instance(2) };
            _renderer.UseSurfaceReplacementHandoff = true;
            _renderer.SetInstances(instances);
            int builds = _renderer.BatchBuildCount;
            var consumers = new List<ProceduralFarFeatureRenderer>();
            for (int cycle = 0; cycle < 16; cycle++)
            {
                _renderer.enabled = false;
                ProceduralFarFeatureRenderer.PrepareSurfaceConsumers(consumers, _ => false);
                Assert.That(consumers, Is.Empty);
                _renderer.enabled = true;
                _renderer.SetInstances(instances);
                ProceduralFarFeatureRenderer.PrepareSurfaceConsumers(consumers, _ => cycle % 2 == 0);
                Assert.AreEqual(cycle % 2 == 0 ? 0 : 2, _renderer.InstanceCount);
                Assert.AreEqual(builds, _renderer.BatchBuildCount);
            }
            instances[1] = Instance(3);
            _renderer.SetInstances(instances);
            Assert.AreEqual(builds + 1, _renderer.BatchBuildCount);
            _renderer.Clear();
            Assert.AreEqual(0, _renderer.InstanceCount);
            _renderer.SetInstances(instances);
            Assert.AreEqual(2, _renderer.InstanceCount);
        }
    }
}
