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
