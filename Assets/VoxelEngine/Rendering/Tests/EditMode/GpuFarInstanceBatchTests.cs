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
        public void SharedReplacementCompactionPreservesBatchOffsetsAndEverySubmesh()
        {
            Mesh mesh=_renderer.ResolveMesh(Instance(0));
            var materials=new Material[mesh.subMeshCount];
            for(int i=0;i<materials.Length;i++)materials[i]=_renderer.ResolveMaterial(Instance(0),i);
            var second=new List<FarFeatureInstance>();for(int i=0;i<65;i++)second.Add(Instance(i+3));
            using var firstBatch=new GpuFarInstanceBatch(_shader,mesh,materials,new[]{Instance(0),Instance(1),Instance(2)});
            using var secondBatch=new GpuFarInstanceBatch(_shader,mesh,materials,second);
            int words=mesh.subMeshCount*5;
            var initial=new uint[words*2];firstBatch.WriteSharedArguments(initial,0);secondBatch.WriteSharedArguments(initial,words);
            using var arguments=new ComputeBuffer(initial.Length,4,ComputeBufferType.IndirectArguments);
            using var metadata=new ComputeBuffer(2,16);
            using var replacement=new ComputeBuffer(68,4);
            using var visible=new ComputeBuffer(68,4);
            using var counts=new ComputeBuffer(2,4);
            arguments.SetData(initial);
            metadata.SetData(new[]{new uint4(0,3,0,(uint)mesh.subMeshCount),new uint4(3,65,(uint)words,(uint)mesh.subMeshCount)});
            int kernel=_shader.FindKernel("CSCompactReplacementBatches");
            _shader.SetInt("_FarBatchCount",2);
            _shader.SetBuffer(kernel,"_FarArguments",arguments);_shader.SetBuffer(kernel,"_FarBatches",metadata);
            _shader.SetBuffer(kernel,"_FarReplacement",replacement);_shader.SetBuffer(kernel,"_FarVisibleIndices",visible);
            _shader.SetBuffer(kernel,"_FarBatchCounts",counts);
            for(int mode=0;mode<3;mode++)
            {
                var proof=new uint[68];for(int i=0;i<68;i++)proof[i]=mode==0?0u:mode==1?1u:(uint)(i&1);
                replacement.SetData(proof);_shader.Dispatch(kernel,2,1,1);
                var actual=new uint[initial.Length];arguments.GetData(actual);
                var indices=new uint[68];visible.GetData(indices);
                var totals=new uint[2];counts.GetData(totals);
                for(int batch=0;batch<2;batch++)
                {
                    int start=batch==0?0:3,count=batch==0?3:65,expected=0;
                    for(int i=0;i<count;i++)if(proof[start+i]==0)expected++;
                    Assert.AreEqual(expected,totals[batch]);
                    for(int submesh=0;submesh<mesh.subMeshCount;submesh++)
                    for(int word=0;word<5;word++)
                    {
                        int at=batch*words+submesh*5+word;
                        Assert.AreEqual(word==1?(uint)expected:initial[at],actual[at]);
                    }
                    var unique=new HashSet<uint>();
                    for(int i=0;i<expected;i++)
                    {
                        uint local=indices[start+i];Assert.Less(local,(uint)count);
                        Assert.True(unique.Add(local));Assert.AreEqual(0,proof[start+local]);
                    }
                }
                firstBatch.UpdateReplacement(_=>true);firstBatch.Prepare();
                firstBatch.UseSharedDraw(arguments,0,visible,0);
                using var command=new CommandBuffer();firstBatch.RecordDraws(command);
                Assert.Greater(command.sizeInBytes,0,"CPU visibility must not override GPU replacement arguments.");
            }
        }

        [Test]
        public void MaterialDrawsMergeMeshesAndPreserveEveryVisibleTriangleAcrossPageBoundaries()
        {
            var primitives = new FarFeatureGeometryPrimitive[9];
            for (int i = 0; i < primitives.Length; i++)
                primitives[i] = new FarFeatureGeometryPrimitive(FarFeatureGeometryShape.Box,
                    new float3(i, 0, 0), new float3(i + 1, 1, 1), presentationSlot: i < 7 ? 0 : 1);
            _geometry = new FarFeatureGeometry(primitives, new[] {
                new FarFeaturePresentation(new float4(1), 0.8f),
                new FarFeaturePresentation(new float4(0.5f, 0.2f, 0.1f, 1), 0.3f) });
            Mesh mesh = _renderer.ResolveMesh(Instance(0));
            var materials = new[] { _renderer.ResolveMaterial(Instance(0), 0), _renderer.ResolveMaterial(Instance(0), 1) };
            var second = new List<FarFeatureInstance>();
            for (int i = 3; i < 68; i++) second.Add(Instance(i));
            using var firstBatch = new GpuFarInstanceBatch(_shader, mesh, materials, new[] { Instance(0), Instance(1), Instance(2) });
            using var secondBatch = new GpuFarInstanceBatch(_shader, mesh, materials, second);
            using var draws = new GpuFarMaterialDraws(new[] { firstBatch, secondBatch });
            using var replacement = new ComputeBuffer(68, 4);
            using var diagnosticCount = new ComputeBuffer(1, 4);
            Assert.AreEqual(2, draws.DrawCount, "Shared materials merge separate batches.");
            var sourceIndices = new uint[draws.Indices.count]; draws.Indices.GetData(sourceIndices);
            var expectedIndices = new List<uint>();
            for (int submesh = 0; submesh < 2; submesh++)
                foreach (int index in mesh.GetIndices(submesh)) expectedIndices.Add((uint)index);
            CollectionAssert.AreEqual(expectedIndices, sourceIndices, "Shared geometry is stored once with exact indices.");
            var transforms = new GpuFarInstanceBatch.InstanceTransform[68]; draws.Transforms.GetData(transforms);
            for (int i = 0; i < 68; i++)
                Assert.AreEqual(Matrix4x4.TRS(Instance(i).Position, Instance(i).Rotation, Instance(i).Scale), transforms[i].ObjectToWorld);
            for (int mode = 0; mode < 3; mode++)
            {
                var proof = new uint[68];
                for (int i = 0; i < 68; i++) proof[i] = mode == 0 ? 0u : mode == 1 ? 1u : (uint)(i & 1);
                replacement.SetData(proof); draws.Prepare(replacement);
                var args = new uint[8]; draws.Arguments.GetData(args);
                var pages = new uint4[draws.Pages.count]; draws.Pages.GetData(pages);
                int expectedInstances = mode == 0 ? 68 : mode == 1 ? 0 : 34;
                draws.CountVisible(replacement, diagnosticCount);
                var visibleTotal = new uint[1]; diagnosticCount.GetData(visibleTotal);
                Assert.AreEqual(expectedInstances, visibleTotal[0], "Diagnostics count instances once, independent of material pages.");
                for (int material = 0; material < 2; material++)
                {
                    int indexCount = (int)mesh.GetIndexCount(material);
                    int pageCount = (indexCount + GpuFarMaterialDraws.IndicesPerPage - 1) / GpuFarMaterialDraws.IndicesPerPage;
                    Assert.AreEqual(192, args[material * 4]);
                    Assert.AreEqual(pageCount * expectedInstances, args[material * 4 + 1]);
                    Assert.AreEqual(0, args[material * 4 + 2]); Assert.AreEqual(0, args[material * 4 + 3]);
                    int sourceStart = material == 0 ? 0 : (int)mesh.GetIndexCount(0);
                    var unique = new HashSet<ulong>(); var totals = new int[68];
                    for (int i = 0; i < args[material * 4 + 1]; i++)
                    {
                        uint4 page = pages[draws.PageOffset(material) + i];
                        Assert.Less(page.x, 68u); Assert.AreEqual(0, proof[page.x]);
                        Assert.True(unique.Add(((ulong)page.x << 32) | page.y));
                        Assert.GreaterOrEqual(page.y, (uint)sourceStart);
                        int local = (int)page.y - sourceStart;
                        Assert.AreEqual(0, local % 192);
                        Assert.AreEqual(Math.Min(192, indexCount - local), page.z);
                        totals[page.x] += (int)page.z;
                    }
                    for (int i = 0; i < 68; i++) Assert.AreEqual(proof[i] == 0 ? indexCount : 0, totals[i]);
                }
                using var command = new CommandBuffer(); draws.Record(command);
                Assert.Greater(command.sizeInBytes, 0, "GPU arguments control zero-count submissions.");
            }
        }

        [Test]
        public void MaterialAtlasPreservesDistinctMeshOffsetsAndSurvivesRepeatedLifetimes()
        {
            var geometry = new FarFeatureGeometry(new[] { new FarFeatureGeometryPrimitive(
                FarFeatureGeometryShape.Cylinder, new float3(-2), new float3(3)) },
                new[] { new FarFeaturePresentation(new float4(1), 0.8f) });
            var other = new FarFeatureInstance(99, new float3(12, 3, -8), quaternion.identity,
                new float3(1, 2, 3), new float3(12, 3, -8), new float3(8),
                "alternate-geometry", "style", FarFeatureTier.Mid, FarFeatureVisualFlags.None, geometry);
            Mesh firstMesh = _renderer.ResolveMesh(Instance(0)), secondMesh = _renderer.ResolveMesh(other);
            Assert.AreNotSame(firstMesh, secondMesh);
            using var first = new GpuFarInstanceBatch(_shader, firstMesh,
                new[] { _renderer.ResolveMaterial(Instance(0), 0), _renderer.ResolveMaterial(Instance(0), 1) }, new[] { Instance(0) });
            using var second = new GpuFarInstanceBatch(_shader, secondMesh,
                new[] { _renderer.ResolveMaterial(other, 0) }, new[] { other });
            using var replacement = new ComputeBuffer(2, 4); replacement.SetData(new uint[2]);
            var expectedIndices = new List<uint>();
            foreach (int index in firstMesh.GetIndices(0)) expectedIndices.Add((uint)index);
            foreach (int index in secondMesh.GetIndices(0)) expectedIndices.Add((uint)(firstMesh.vertexCount + index));
            var expectedVertices = new List<float3x2>();
            foreach (Mesh mesh in new[] { firstMesh, secondMesh })
            {
                var positions = mesh.vertices; var normals = mesh.normals;
                for (int i = 0; i < positions.Length; i++) expectedVertices.Add(new float3x2(positions[i], normals[i]));
            }
            for (int cycle = 0; cycle < 4; cycle++)
            {
                using var draws = new GpuFarMaterialDraws(new[] { first, second });
                Assert.AreEqual(1, draws.DrawCount);
                var indices = new uint[draws.Indices.count]; draws.Indices.GetData(indices);
                CollectionAssert.AreEqual(expectedIndices, indices);
                var vertices = new float3x2[draws.Vertices.count]; draws.Vertices.GetData(vertices);
                CollectionAssert.AreEqual(expectedVertices, vertices);
                draws.Prepare(replacement);
                var args = new uint[4]; draws.Arguments.GetData(args);
                var pages = new uint4[draws.Pages.count]; draws.Pages.GetData(pages);
                int firstCount = (int)firstMesh.GetIndexCount(0), secondCount = (int)secondMesh.GetIndexCount(0);
                var totals = new int[2]; var unique = new HashSet<ulong>();
                for (int i = 0; i < args[1]; i++)
                {
                    uint4 page = pages[i]; Assert.Less(page.x, 2u);
                    Assert.True(unique.Add(((ulong)page.x << 32) | page.y));
                    int start = page.x == 0 ? 0 : firstCount, count = page.x == 0 ? firstCount : secondCount;
                    Assert.GreaterOrEqual(page.y, (uint)start);
                    Assert.Less(page.y, (uint)(start + count));
                    totals[page.x] += (int)page.z;
                }
                CollectionAssert.AreEqual(new[] { firstCount, secondCount }, totals);
                draws.Dispose(); draws.Dispose();
                Assert.Throws<ObjectDisposedException>(() => draws.Prepare(replacement));
                using var command = new CommandBuffer();
                Assert.Throws<ObjectDisposedException>(() => draws.Record(command));
            }
        }

        [Test]
        public void MaterialAtlasRejectsUnrepresentedVertexChannels()
        {
            Mesh mesh = _renderer.ResolveMesh(Instance(0));
            mesh.uv = new Vector2[mesh.vertexCount];
            using var batch = new GpuFarInstanceBatch(_shader, mesh,
                new[] { _renderer.ResolveMaterial(Instance(0), 0), _renderer.ResolveMaterial(Instance(0), 1) }, new[] { Instance(0) });
            Assert.Throws<InvalidOperationException>(() => new GpuFarMaterialDraws(new[] { batch }));
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
