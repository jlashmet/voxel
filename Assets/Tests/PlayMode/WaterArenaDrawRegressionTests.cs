using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class WaterArenaDrawRegressionTests
    {
        [Test]
        public void SecondArenaLeasePublishesVertexBaseInIndirectDrawRecord()
        {
            var arena = new SurfaceGeometryArena(1024, 2048, 8);
            try
            {
                Assert.That(arena.TryAcquire(3, 3, out SurfaceGeometryLease first), Is.True);
                Assert.That(arena.TryAcquire(3, 3, out SurfaceGeometryLease second), Is.True);
                Assert.That(second.VertexStart, Is.GreaterThan(0),
                    "The discriminator requires a second independently aligned vertex range.");

                arena.UploadArgs(3, in first);
                arena.UploadArgs(3, in second);

                var args = new uint[arena.ArgsRecordCapacity * SurfaceGeometryArena.ArgsWordsPerDraw];
                arena.Args.GetData(args);
                Assert.That(args[second.ArgsWordStart + 3], Is.EqualTo((uint)second.VertexStart),
                    "Water indices stay chunk-local, so the indirect record must carry the lease vertex base for the shader.");
            }
            finally
            {
                arena.Dispose();
            }
        }

        [Test]
        public void IndirectStartInstanceReachesShaderOnCurrentBackend()
        {
            Shader shader = Shader.Find("Hidden/VoxelEngine/Tests/IndirectStartInstanceProbe");
            Assert.That(shader, Is.Not.Null,
                "The focused start-instance probe shader must be available to the PlayMode repro.");

            using var args = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);
            args.SetData(new uint[] { 3u, 1u, 0u, 256u });

            var target = new RenderTexture(16, 16, 0, RenderTextureFormat.ARGB32)
            {
                name = "IndirectStartInstanceProbeTarget",
            };
            target.Create();
            var readback = new Texture2D(16, 16, TextureFormat.RGBA32, false, true);
            var material = new Material(shader);
            var commandBuffer = new CommandBuffer { name = "Indirect start-instance probe" };
            RenderTexture previous = RenderTexture.active;

            try
            {
                commandBuffer.SetRenderTarget(target);
                commandBuffer.ClearRenderTarget(true, true, Color.black);
                commandBuffer.DrawProceduralIndirect(
                    Matrix4x4.identity, material, 0, MeshTopology.Triangles, args, 0);
                Graphics.ExecuteCommandBuffer(commandBuffer);

                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 16, 16), 0, 0, false);
                readback.Apply(false, false);
                Color sample = readback.GetPixel(8, 8);

                Assert.That(sample.r, Is.GreaterThan(0.8f),
                    $"The shader saw SV_InstanceID=0 instead of indirect startInstance on {SystemInfo.graphicsDeviceType}; " +
                    "later water leases therefore cannot use startInstance as their vertex-buffer base.");
                Assert.That(sample.b, Is.LessThan(0.2f));
            }
            finally
            {
                RenderTexture.active = previous;
                commandBuffer.Release();
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(readback);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
