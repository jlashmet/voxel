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
        public void SecondWaterArenaLeaseBindsItsVertexBase()
        {
            var arena = new SurfaceGeometryArena(1024, 2048, 8);
            var first = new CpuWaterSurfaceChunkCache.Entry(int3.zero, arena);
            var second = new CpuWaterSurfaceChunkCache.Entry(new int3(1, 0, 0), arena);
            var vertices = new NativeList<SmoothSurfaceVertex>(3, Allocator.Temp);
            var indices = new NativeList<uint>(3, Allocator.Temp);
            var commandBuffer = new CommandBuffer();
            Material material = null;

            try
            {
                vertices.Add(new SmoothSurfaceVertex { Position = Vector3.zero, Normal = Vector3.up });
                vertices.Add(new SmoothSurfaceVertex { Position = Vector3.right, Normal = Vector3.up });
                vertices.Add(new SmoothSurfaceVertex { Position = Vector3.forward, Normal = Vector3.up });
                indices.Add(0);
                indices.Add(1);
                indices.Add(2);

                Assert.That(first.AdvanceUpload(vertices, indices, int.MaxValue, out _), Is.True);
                Assert.That(second.AdvanceUpload(vertices, indices, int.MaxValue, out _), Is.True,
                    "The discriminator requires a second independently allocated water vertex range.");

                Shader shader = Shader.Find("Hidden/VoxelEngine/WaterSurface");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                var properties = new MaterialPropertyBlock();

                second.Draw(commandBuffer, material, properties);

                int vertexBase = properties.GetInt(Shader.PropertyToID("_SurfaceVertexBase"));
                Assert.That(vertexBase, Is.GreaterThan(0),
                    "Water indices are chunk-local; a nonzero arena lease must bind its vertex base before draw.");
            }
            finally
            {
                if (material != null) Object.DestroyImmediate(material);
                commandBuffer.Release();
                indices.Dispose();
                vertices.Dispose();
                second.Dispose();
                first.Dispose();
                arena.Dispose();
            }
        }
    }
}
