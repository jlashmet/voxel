using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class WaterSprayFeatheringRegressionTests
    {
        [Test]
        public void SprayPassKeepsImpactHingeTransparentWhileFreeMistRemainsVisible()
        {
            var arena = new SurfaceGeometryArena(1024, 2048, 8);
            var entry = new CpuWaterSurfaceChunkCache.Entry(int3.zero, arena);
            var vertices = new NativeList<SmoothSurfaceVertex>(4, Allocator.Temp);
            var indices = new NativeList<uint>(6, Allocator.Temp);
            Material material = null;
            RenderTexture target = null;
            Texture2D readback = null;
            var commandBuffer = new CommandBuffer { name = "Water spray hinge feathering regression" };

            try
            {
                VoxelMaterialPresentationInstaller.Apply(GameMaterialRenderingDefinitions.Create());
                uint packed = GameMaterialIds.Cascade
                            | SmoothSurfaceVertex.WaterImpactFlag
                            | SmoothSurfaceVertex.WaterEdgeFlag
                            | SmoothSurfaceVertex.WaterSprayFlag;
                AddSprayVertex(vertices, new Vector3(-0.8f, -0.8f, 0f), packed, 0u);
                AddSprayVertex(vertices, new Vector3( 0.8f, -0.8f, 0f), packed,
                    SmoothSurfaceVertex.WaterSprayUFlag);
                AddSprayVertex(vertices, new Vector3( 0.8f,  0.8f, 0f), packed,
                    SmoothSurfaceVertex.WaterSprayUFlag | SmoothSurfaceVertex.WaterSprayVFlag);
                AddSprayVertex(vertices, new Vector3(-0.8f,  0.8f, 0f), packed,
                    SmoothSurfaceVertex.WaterSprayVFlag);
                indices.Add(0u);
                indices.Add(1u);
                indices.Add(2u);
                indices.Add(0u);
                indices.Add(2u);
                indices.Add(3u);

                int byteBudget = vertices.Length * SmoothSurfaceVertex.Stride
                               + indices.Length * sizeof(uint)
                               + SurfaceGeometryArena.ArgsWordsPerDraw * sizeof(uint);
                Assert.That(entry.AdvanceUpload(vertices, indices, byteBudget, out _), Is.True);
                Assert.That(entry.HasSpray, Is.True);

                Shader shader = Shader.Find("Hidden/VoxelEngine/WaterSurface");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                target = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Water spray hinge feathering target"
                };
                target.Create();
                readback = new Texture2D(96, 96, TextureFormat.RGBA32, false);

                commandBuffer.SetRenderTarget(target);
                commandBuffer.ClearRenderTarget(true, true, Color.black);
                commandBuffer.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                commandBuffer.SetGlobalFloat(Shader.PropertyToID("_WaterTime"), 1.25f);
                var properties = new MaterialPropertyBlock();
                entry.Draw(commandBuffer, material, properties);
                Graphics.ExecuteCommandBuffer(commandBuffer);

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                readback.Apply(false, false);
                RenderTexture.active = previous;

                // The canonical spray sheet spans NDC y=-0.8..0.8, or pixels ~10..86.
                // Rows 11..15 are the first ~7% above the authored impact edge. Rendering them
                // exposes the broad trapezoid hinge as a hard planar wedge; free mist belongs above
                // that contact band, while impact foam on the waterfall body owns the actual contact.
                int hingePixels = CountLitPixels(readback, 11, 15);
                int freeMistPixels = CountLitPixels(readback, 28, 60);
                Assert.That(hingePixels, Is.Zero,
                    "Depth-neutral spray must fully feather the broad authored impact hinge instead of advertising its planar trapezoid/triangle geometry.");
                Assert.That(freeMistPixels, Is.GreaterThan(20),
                    "Suppressing the planar hinge must retain a visible free-mist volume above the impact.");
            }
            finally
            {
                RenderTexture.active = null;
                entry.Dispose();
                commandBuffer.Release();
                if (readback != null) Object.DestroyImmediate(readback);
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }
                if (material != null) Object.DestroyImmediate(material);
                vertices.Dispose();
                indices.Dispose();
                arena.Dispose();
                VoxelMaterialPresentationInstaller.Apply(GameMaterialRenderingDefinitions.Create());
            }
        }

        [Test]
        public void SprayPassDoesNotAdvertiseBroadCarrierAsHigherBandWedge()
        {
            var arena = new SurfaceGeometryArena(1024, 2048, 8);
            var entry = new CpuWaterSurfaceChunkCache.Entry(int3.zero, arena);
            var vertices = new NativeList<SmoothSurfaceVertex>(4, Allocator.Temp);
            var indices = new NativeList<uint>(6, Allocator.Temp);
            Material material = null;
            RenderTexture target = null;
            Texture2D readback = null;
            var commandBuffer = new CommandBuffer { name = "Water spray upper-band wedge discriminator" };

            try
            {
                VoxelMaterialPresentationInstaller.Apply(GameMaterialRenderingDefinitions.Create());
                uint packed = GameMaterialIds.Cascade
                            | SmoothSurfaceVertex.WaterImpactFlag
                            | SmoothSurfaceVertex.WaterEdgeFlag
                            | SmoothSurfaceVertex.WaterSprayFlag;

                // Reproduce the production carrier relationship rather than a square fixture: the
                // widest canonical sheet retains roughly 59% of its lower span at the crown. Holding
                // that broad trapezoid constant isolates whether the spray pass masks its higher band
                // strongly enough; a failure here is masking/geometry coupling, not the impact hinge.
                AddSprayVertex(vertices, new Vector3(-0.88f, -0.8f, 0f), packed, 0u);
                AddSprayVertex(vertices, new Vector3( 0.88f, -0.8f, 0f), packed,
                    SmoothSurfaceVertex.WaterSprayUFlag);
                AddSprayVertex(vertices, new Vector3( 0.52f,  0.8f, 0f), packed,
                    SmoothSurfaceVertex.WaterSprayUFlag | SmoothSurfaceVertex.WaterSprayVFlag);
                AddSprayVertex(vertices, new Vector3(-0.52f,  0.8f, 0f), packed,
                    SmoothSurfaceVertex.WaterSprayVFlag);
                indices.Add(0u);
                indices.Add(1u);
                indices.Add(2u);
                indices.Add(0u);
                indices.Add(2u);
                indices.Add(3u);

                int byteBudget = vertices.Length * SmoothSurfaceVertex.Stride
                               + indices.Length * sizeof(uint)
                               + SurfaceGeometryArena.ArgsWordsPerDraw * sizeof(uint);
                Assert.That(entry.AdvanceUpload(vertices, indices, byteBudget, out _), Is.True);
                Assert.That(entry.HasSpray, Is.True);

                Shader shader = Shader.Find("Hidden/VoxelEngine/WaterSurface");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                target = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Water spray upper-band wedge target"
                };
                target.Create();
                readback = new Texture2D(96, 96, TextureFormat.RGBA32, false);

                commandBuffer.SetRenderTarget(target);
                commandBuffer.ClearRenderTarget(true, true, Color.black);
                commandBuffer.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                commandBuffer.SetGlobalFloat(Shader.PropertyToID("_WaterTime"), 1.25f);
                var properties = new MaterialPropertyBlock();
                entry.Draw(commandBuffer, material, properties);
                Graphics.ExecuteCommandBuffer(commandBuffer);

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                readback.Apply(false, false);
                RenderTexture.active = previous;

                int hingePixels = CountLitPixels(readback, 11, 15);
                int freeMistPixels = CountLitPixels(readback, 28, 55);
                int upperBandPixels = CountLitPixels(readback, 61, 84);
                Assert.That(hingePixels, Is.Zero,
                    "The already-approved transparent impact hinge must remain transparent in the upper-band discriminator.");
                Assert.That(freeMistPixels, Is.GreaterThan(20),
                    "The discriminator must retain readable free mist while suppressing carrier-shaped coverage.");
                Assert.That(upperBandPixels, Is.LessThan(180),
                    "The broad canonical carrier must not remain densely visible through the higher band as a planar/triangular wedge.");
            }
            finally
            {
                RenderTexture.active = null;
                entry.Dispose();
                commandBuffer.Release();
                if (readback != null) Object.DestroyImmediate(readback);
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }
                if (material != null) Object.DestroyImmediate(material);
                vertices.Dispose();
                indices.Dispose();
                arena.Dispose();
                VoxelMaterialPresentationInstaller.Apply(GameMaterialRenderingDefinitions.Create());
            }
        }

        private static void AddSprayVertex(
            NativeList<SmoothSurfaceVertex> vertices,
            Vector3 position,
            uint material,
            uint sprayUvBits)
        {
            vertices.Add(new SmoothSurfaceVertex
            {
                Position = position,
                Normal = Vector3.back,
                Material = material,
                Active = 0x0000FF00u | sprayUvBits,
            });
        }

        private static int CountLitPixels(Texture2D texture, int minY, int maxY)
        {
            int count = 0;
            Color32[] pixels = texture.GetPixels32();
            for (int y = minY; y <= maxY; y++)
            for (int x = 0; x < texture.width; x++)
            {
                Color32 pixel = pixels[x + y * texture.width];
                if (pixel.r > 2 || pixel.g > 2 || pixel.b > 2)
                    count++;
            }
            return count;
        }
    }
}
