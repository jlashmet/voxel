using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Game.Materials.Api;
using Game.Materials.Runtime;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class WaterfallEdgeCoverageRegressionTests
    {
        [Test]
        public void SemanticSideEdgeErodesCascadeSilhouetteWithoutChangingStillWater()
        {
            var arena = new SurfaceGeometryArena(1024, 2048, 8);
            var entry = new CpuWaterSurfaceChunkCache.Entry(int3.zero, arena);
            var vertices = new NativeList<SmoothSurfaceVertex>(4, Allocator.Temp);
            var indices = new NativeList<uint>(6, Allocator.Temp);
            Material material = null;
            RenderTexture target = null;
            Texture2D readback = null;
            var commandBuffer = new CommandBuffer { name = "Waterfall semantic edge coverage discriminator" };

            try
            {
                VoxelMaterialPresentationInstaller.Apply(GameMaterialRenderingDefinitions.Create());
                AddQuadIndices(indices);

                Shader shader = Shader.Find("Hidden/VoxelEngine/WaterSurface");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                target = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Waterfall semantic edge coverage target"
                };
                target.Create();
                readback = new Texture2D(96, 96, TextureFormat.RGBA32, false);

                AddBodyQuad(vertices, GameMaterialIds.Cascade, false);
                int byteBudget = vertices.Length * SmoothSurfaceVertex.Stride
                               + indices.Length * sizeof(uint)
                               + SurfaceGeometryArena.ArgsWordsPerDraw * sizeof(uint);
                Assert.That(entry.AdvanceUpload(vertices, indices, byteBudget, out _), Is.True);
                int cascadeBodyPixels = RenderAndCountVisiblePixels(
                    entry, material, commandBuffer, target, readback);
                Assert.That(cascadeBodyPixels, Is.GreaterThan(0));

                vertices.Clear();
                AddBodyQuad(vertices, GameMaterialIds.Cascade, true);
                Assert.That(entry.AdvanceUpload(vertices, indices, byteBudget, out _), Is.True);
                int cascadeEdgePixels = RenderAndCountVisiblePixels(
                    entry, material, commandBuffer, target, readback);
                Assert.That(cascadeEdgePixels, Is.GreaterThan(0));
                Assert.That(cascadeEdgePixels, Is.LessThan(cascadeBodyPixels * 0.98f),
                    "Semantic WaterEdgeFlag topology must erode a waterfall side silhouette instead of only tinting foam inside the same rectangular body coverage.");

                vertices.Clear();
                AddBodyQuad(vertices, GameMaterialIds.Water, false);
                Assert.That(entry.AdvanceUpload(vertices, indices, byteBudget, out _), Is.True);
                int stillBodyPixels = RenderAndCountVisiblePixels(
                    entry, material, commandBuffer, target, readback);

                vertices.Clear();
                AddBodyQuad(vertices, GameMaterialIds.Water, true);
                Assert.That(entry.AdvanceUpload(vertices, indices, byteBudget, out _), Is.True);
                int stillEdgePixels = RenderAndCountVisiblePixels(
                    entry, material, commandBuffer, target, readback);
                Assert.That(stillEdgePixels, Is.EqualTo(stillBodyPixels),
                    "Waterfall edge erosion is presentation-profile behavior; semantic edge tags must not punch still-water coverage.");
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

        private static void AddBodyQuad(NativeList<SmoothSurfaceVertex> vertices,
                                        byte material, bool leftEdge)
        {
            uint edgeMaterial = leftEdge
                ? material | SmoothSurfaceVertex.WaterEdgeFlag
                : material;
            vertices.Add(new SmoothSurfaceVertex
            {
                Position = new Vector3(-0.8f, -0.8f, 0f),
                Normal = Vector3.back,
                Material = edgeMaterial,
                Active = 0u,
            });
            vertices.Add(new SmoothSurfaceVertex
            {
                Position = new Vector3(0.8f, -0.8f, 0f),
                Normal = Vector3.back,
                Material = material,
                Active = 0u,
            });
            vertices.Add(new SmoothSurfaceVertex
            {
                Position = new Vector3(0.8f, 0.8f, 0f),
                Normal = Vector3.back,
                Material = material,
                Active = 0u,
            });
            vertices.Add(new SmoothSurfaceVertex
            {
                Position = new Vector3(-0.8f, 0.8f, 0f),
                Normal = Vector3.back,
                Material = edgeMaterial,
                Active = 0u,
            });
        }

        private static void AddQuadIndices(NativeList<uint> indices)
        {
            indices.Add(0u);
            indices.Add(1u);
            indices.Add(2u);
            indices.Add(0u);
            indices.Add(2u);
            indices.Add(3u);
        }

        private static int RenderAndCountVisiblePixels(
            CpuWaterSurfaceChunkCache.Entry entry, Material material,
            CommandBuffer commandBuffer, RenderTexture target, Texture2D readback)
        {
            commandBuffer.Clear();
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

            Color32[] pixels = readback.GetPixels32();
            int visible = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.r > 8 || pixel.g > 8 || pixel.b > 8)
                    visible++;
            }
            return visible;
        }
    }
}
