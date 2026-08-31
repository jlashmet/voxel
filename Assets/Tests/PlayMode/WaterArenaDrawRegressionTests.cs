using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class WaterArenaDrawRegressionTests
    {
        [Test]
        public void VerticalWaterFixtureEmitsReusableBoundaryTopology()
        {
            const byte water = 7;
            var brickBases = new NativeArray<int3>(1, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            var snapshot = new NativeArray<byte>(WaterBrickMeshBatchJob.SnapshotStride,
                Allocator.Temp, NativeArrayOptions.ClearMemory);
            var mask = new NativeArray<byte>(WaterBrickMeshBatchJob.FaceArea, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            var vertices = new NativeList<SmoothSurfaceVertex>(256, Allocator.Temp);
            var indices = new NativeList<uint>(384, Allocator.Temp);
            var overflow = new NativeArray<int>(1, Allocator.Temp,
                NativeArrayOptions.ClearMemory);

            try
            {
                // Independent one-voxel-wide vertical ribbon. Material 7 is intentionally arbitrary:
                // shared extraction only receives the semantic water mask, never a game material ID.
                for (int y = 2; y <= 5; y++)
                    snapshot[3 + y * WaterBrickMeshBatchJob.Edge
                               + 3 * WaterBrickMeshBatchJob.Edge * WaterBrickMeshBatchJob.Edge] = water;

                var job = new WaterBrickMeshBatchJob
                {
                    BrickBaseVoxels = brickBases,
                    SnapshotMaterials = snapshot,
                    WaterMaterialMask = 1u << water,
                    BatchCount = 1,
                    VoxelSize = 0.25f,
                    MaskScratch = mask,
                    Vertices = vertices,
                    Indices = indices,
                    Overflow = overflow,
                };
                job.Execute();

                Assert.That(overflow[0], Is.Zero);
                Assert.That(vertices.Length, Is.GreaterThan(0));
                int verticalCount = 0;
                int lipCount = 0;
                int impactCount = 0;
                int edgeCount = 0;
                for (int i = 0; i < vertices.Length; i++)
                {
                    SmoothSurfaceVertex vertex = vertices[i];
                    Assert.That(vertex.Material & SmoothSurfaceVertex.BaseMaterialMask,
                                Is.EqualTo((uint)water),
                        "Topology packing must preserve opaque low-byte water identity.");
                    if (Mathf.Abs(vertex.Normal.y) > 0.01f)
                        continue;

                    verticalCount++;
                    if ((vertex.Material & SmoothSurfaceVertex.WaterLipFlag) != 0) lipCount++;
                    if ((vertex.Material & SmoothSurfaceVertex.WaterImpactFlag) != 0) impactCount++;
                    if ((vertex.Material & SmoothSurfaceVertex.WaterEdgeFlag) != 0) edgeCount++;
                }

                Assert.That(verticalCount, Is.GreaterThan(0));
                Assert.That(lipCount, Is.GreaterThan(0),
                    "The canonical extractor must mark the top boundary of a vertical water ribbon.");
                Assert.That(impactCount, Is.GreaterThan(0),
                    "The canonical extractor must mark the lower impact boundary of a vertical water ribbon.");
                Assert.That(edgeCount, Is.EqualTo(verticalCount),
                    "A one-voxel-wide ribbon must expose reusable side-edge topology on every vertical vertex.");
            }
            finally
            {
                brickBases.Dispose();
                snapshot.Dispose();
                mask.Dispose();
                vertices.Dispose();
                indices.Dispose();
                overflow.Dispose();
            }
        }

        [Test]
        public void SecondWaterEntryBindsExplicitArenaOffsets()
        {
            var arena = new SurfaceGeometryArena(1024, 2048, 8);
            Assert.That(arena.TryAcquire(3, 3, out SurfaceGeometryLease blocker), Is.True);
            var entry = new CpuWaterSurfaceChunkCache.Entry(int3.zero, arena);
            var vertices = new NativeList<SmoothSurfaceVertex>(3, Allocator.Temp);
            var indices = new NativeList<uint>(3, Allocator.Temp);
            Material material = null;
            var commandBuffer = new CommandBuffer { name = "Water arena offset regression" };

            try
            {
                for (int i = 0; i < 3; i++)
                    vertices.Add(new SmoothSurfaceVertex
                    {
                        Position = new Vector3(i, 0, 0),
                        Normal = Vector3.up,
                        Active = 1u,
                    });
                indices.Add(0u);
                indices.Add(1u);
                indices.Add(2u);

                int byteBudget = vertices.Length * SmoothSurfaceVertex.Stride
                               + indices.Length * sizeof(uint)
                               + SurfaceGeometryArena.ArgsWordsPerDraw * sizeof(uint);
                Assert.That(entry.AdvanceUpload(vertices, indices, byteBudget, out _), Is.True);

                Shader shader = Shader.Find("Hidden/VoxelEngine/WaterSurface");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                var properties = new MaterialPropertyBlock();
                entry.Draw(commandBuffer, material, properties);

                Assert.That(properties.GetInt(Shader.PropertyToID("_SurfaceVertexBase")),
                            Is.EqualTo(256),
                    "The second independently aligned vertex lease must be explicit draw state.");
                Assert.That(properties.GetInt(Shader.PropertyToID("_SurfaceIndexBase")),
                            Is.EqualTo(512));

                var args = new uint[arena.ArgsRecordCapacity * SurfaceGeometryArena.ArgsWordsPerDraw];
                arena.Args.GetData(args);
                Assert.That(args[SurfaceGeometryArena.ArgsWordsPerDraw + 3], Is.EqualTo(0u),
                    "startInstance must stay neutral because Metal does not deliver it as SV_InstanceID here.");
            }
            finally
            {
                entry.Dispose();
                arena.Release(in blocker);
                commandBuffer.Release();
                if (material != null) Object.DestroyImmediate(material);
                vertices.Dispose();
                indices.Dispose();
                arena.Dispose();
            }
        }
    }
}
