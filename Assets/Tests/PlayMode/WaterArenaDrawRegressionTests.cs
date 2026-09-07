using System.Collections.Generic;
using VoxelEngine.Rendering.Tests.RuntimeSupport;
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
    public sealed class WaterArenaDrawRegressionTests
    {
        [Test]
        public void VerticalWaterFixtureEmitsReusableBoundaryTopology()
        {
            const byte water = 7;
            const float voxelSize = 0.25f;
            var brickBases = new NativeArray<int3>(1, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            var snapshot = new NativeArray<byte>(GpuWaterExtractionFixture.SnapshotStride,
                Allocator.Temp, NativeArrayOptions.ClearMemory);
            var vertices = new NativeList<SmoothSurfaceVertex>(256, Allocator.Temp);
            var indices = new NativeList<uint>(384, Allocator.Temp);

            try
            {
                // Independent one-voxel-wide vertical ribbon. Material 7 is intentionally arbitrary:
                // shared extraction only receives the semantic water mask, never a game material ID.
                for (int y = 2; y <= 5; y++)
                    snapshot[3 + y * GpuWaterExtractionFixture.Edge
                               + 3 * GpuWaterExtractionFixture.Edge * GpuWaterExtractionFixture.Edge] = water;

                GpuWaterExtractionFixture.Extract(brickBases, snapshot, 1u << water, voxelSize, vertices, indices);
                Assert.That(vertices.Length, Is.GreaterThan(0));
                int verticalCount = 0;
                int lipCount = 0;
                int impactCount = 0;
                int edgeCount = 0;
                int sprayCount = 0;
                uint sprayUvCorners = 0u;
                var sprayVertices = new List<Vector3>();
                Vector3 sprayMin = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                Vector3 sprayMax = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
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
                    if ((vertex.Material & SmoothSurfaceVertex.WaterSprayFlag) != 0)
                    {
                        sprayCount++;
                        uint localCorner = vertex.Active
                                         & (SmoothSurfaceVertex.WaterSprayUFlag
                                            | SmoothSurfaceVertex.WaterSprayVFlag);
                        sprayUvCorners |= 1u << (int)localCorner;
                        sprayVertices.Add(vertex.Position);
                        sprayMin = Vector3.Min(sprayMin, vertex.Position);
                        sprayMax = Vector3.Max(sprayMax, vertex.Position);
                    }
                }

                Assert.That(verticalCount, Is.GreaterThan(0));
                Assert.That(lipCount, Is.GreaterThan(0),
                    "The canonical extractor must mark the top boundary of a vertical water ribbon.");
                Assert.That(impactCount, Is.GreaterThan(0),
                    "The canonical extractor must mark the lower impact boundary of a vertical water ribbon.");
                Assert.That(edgeCount, Is.EqualTo(verticalCount),
                    "A one-voxel-wide ribbon must expose reusable side-edge topology on every vertical vertex.");
                Assert.That(sprayCount, Is.GreaterThanOrEqualTo(12),
                    "A true lower boundary must emit layered reusable spray into the same canonical water mesh.");
                Assert.That(sprayCount % 12, Is.Zero,
                    "Each canonical impact boundary emits three ordinary spray sheets, not a secondary geometry path.");
                Assert.That(sprayUvCorners & 0xFu, Is.EqualTo(0xFu),
                    "Canonical spray sheets must carry all four local corners so the shared shader can feather their borders.");
                Assert.That(sprayVertices.Count % 4, Is.Zero);
                float minBaseSpan = float.PositiveInfinity;
                float maxBaseSpan = 0f;
                for (int i = 0; i < sprayVertices.Count; i += 4)
                {
                    float baseSpan = Vector3.Distance(sprayVertices[i], sprayVertices[i + 1]);
                    float crownSpan = Vector3.Distance(sprayVertices[i + 3], sprayVertices[i + 2]);
                    minBaseSpan = Mathf.Min(minBaseSpan, baseSpan);
                    maxBaseSpan = Mathf.Max(maxBaseSpan, baseSpan);
                    Assert.That(crownSpan, Is.LessThan(baseSpan - voxelSize * 0.05f),
                        "Each reusable spray sheet must taper away from impact instead of exposing a rectangular slab.");
                }
                Assert.That(maxBaseSpan - minBaseSpan, Is.GreaterThan(voxelSize * 0.1f),
                    "Layered spray must use distinct lower footprints instead of pivoting three same-span planes around one hinge.");
                Assert.That(sprayMax.y - sprayMin.y, Is.GreaterThanOrEqualTo(voxelSize * 5.5f),
                    "Impact mist needs a multi-voxel vertical footprint so it remains readable beside a multi-metre fall.");
                float horizontalSpan = Mathf.Max(sprayMax.x - sprayMin.x, sprayMax.z - sprayMin.z);
                Assert.That(horizontalSpan, Is.GreaterThanOrEqualTo(voxelSize * 8f),
                    "Impact mist needs a multi-voxel outward footprint rather than a sub-metre skirt hidden by the curtain.");
            }
            finally
            {
                brickBases.Dispose();
                snapshot.Dispose();
                vertices.Dispose();
                indices.Dispose();
            }
        }

        [Test]
        public void WaterfallBodyPunchesRealCoverageWhileStillWaterRemainsContinuous()
        {
            var entry = new GpuWaterRasterFixture();
            var vertices = new NativeList<SmoothSurfaceVertex>(4, Allocator.Temp);
            var indices = new NativeList<uint>(6, Allocator.Temp);
            Material material = null;
            RenderTexture target = null;
            Texture2D readback = null;
            var commandBuffer = new CommandBuffer { name = "Waterfall body cutout discriminator" };

            try
            {
                VoxelMaterialPresentationInstaller.Apply(GameMaterialRenderingDefinitions.Create());
                AddBodyQuad(vertices, GameMaterialIds.Water);
                AddQuadIndices(indices);

                entry.SetGeometry(vertices, indices);

                Shader shader = Shader.Find("Hidden/VoxelEngine/WaterSurface");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                target = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Waterfall body cutout discriminator target"
                };
                target.Create();
                readback = new Texture2D(96, 96, TextureFormat.RGBA32, false);

                int stillPixels = RenderAndCountVisiblePixels(
                    entry, material, commandBuffer, target, readback);
                Assert.That(stillPixels, Is.GreaterThan(0),
                    "The control still-water body must rasterize as a continuous production surface.");

                vertices.Clear();
                AddBodyQuad(vertices, GameMaterialIds.Cascade);
                entry.SetGeometry(vertices, indices);
                int waterfallPixels = RenderAndCountVisiblePixels(
                    entry, material, commandBuffer, target, readback);

                Assert.That(waterfallPixels, Is.GreaterThan(0),
                    "Waterfall breakup must retain coherent visible body coverage.");
                Assert.That(waterfallPixels, Is.LessThan(stillPixels * 0.95f),
                    "Vertical Waterfall fragments must punch real coverage holes instead of only lowering alpha while still stamping depth.");
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
                VoxelMaterialPresentationInstaller.Apply(GameMaterialRenderingDefinitions.Create());
            }
        }

        [Test]
        public void SprayTaggedArenaGeometryRasterizesOnlyForWaterfallProfile()
        {
            var entry = new GpuWaterRasterFixture();
            var vertices = new NativeList<SmoothSurfaceVertex>(4, Allocator.Temp);
            var indices = new NativeList<uint>(6, Allocator.Temp);
            Material material = null;
            RenderTexture target = null;
            Texture2D readback = null;
            var commandBuffer = new CommandBuffer { name = "Water spray raster visibility discriminator" };

            try
            {
                VoxelMaterialPresentationInstaller.Apply(GameMaterialRenderingDefinitions.Create());
                uint sprayFlags = SmoothSurfaceVertex.WaterImpactFlag
                                | SmoothSurfaceVertex.WaterEdgeFlag
                                | SmoothSurfaceVertex.WaterSprayFlag;
                AddSprayQuad(vertices, GameMaterialIds.Cascade, sprayFlags);
                AddQuadIndices(indices);

                entry.SetGeometry(vertices, indices);

                Shader shader = Shader.Find("Hidden/VoxelEngine/WaterSurface");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                Assert.That(material.passCount, Is.GreaterThanOrEqualTo(2),
                    "Water material must retain a dedicated depth-writing body pass and depth-neutral spray pass.");
                target = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Water spray raster visibility discriminator target"
                };
                target.Create();
                readback = new Texture2D(96, 96, TextureFormat.RGBA32, false);

                int cascadePixels = RenderAndCountVisiblePixels(
                    entry, material, commandBuffer, target, readback);
                Assert.That(cascadePixels, Is.GreaterThan(0),
                    "A spray-tagged canonical arena draw with an installed waterfall profile must rasterize visible pixels.");

                vertices.Clear();
                AddSprayQuad(vertices, GameMaterialIds.Water, sprayFlags);
                entry.SetGeometry(vertices, indices);
                int stillPixels = RenderAndCountVisiblePixels(
                    entry, material, commandBuffer, target, readback);
                Assert.That(stillPixels, Is.Zero,
                    "The same spray-tagged canonical arena geometry must remain clipped for a non-waterfall profile.");
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
                VoxelMaterialPresentationInstaller.Apply(GameMaterialRenderingDefinitions.Create());
            }
        }

        [Test]
        public void SecondWaterHandleBindsExplicitPagedIdentityAndIndirectArguments()
        {
            var entry = new GpuWaterRasterFixture();
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

                entry.SetGeometry(vertices, indices);

                Shader shader = Shader.Find("Hidden/VoxelEngine/WaterSurface");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                var properties = new MaterialPropertyBlock();
                entry.Draw(commandBuffer, material, properties);

                Assert.That(properties.GetInteger(Shader.PropertyToID("_WaterDrawHandle")), Is.EqualTo(1),
                    "The second handle must be explicit draw state, independent of Metal base-instance behavior.");
                Assert.That(properties.GetInteger(Shader.PropertyToID("_WaterPagedDraw")), Is.EqualTo(2));
                uint[] args = entry.ReadArguments();
                Assert.That(args[0], Is.EqualTo(3));
                Assert.That(args[1], Is.EqualTo(1));
                Assert.That(args[2], Is.Zero);
                Assert.That(args[3], Is.Zero,
                    "startInstance stays neutral; the vertex shader selects the explicit water handle.");
            }
            finally
            {
                entry.Dispose();
                commandBuffer.Release();
                if (material != null) Object.DestroyImmediate(material);
                vertices.Dispose();
                indices.Dispose();
            }
        }

        private static void AddBodyQuad(NativeList<SmoothSurfaceVertex> vertices, byte material)
        {
            vertices.Add(new SmoothSurfaceVertex
            {
                Position = new Vector3(-0.8f, -0.8f, 0f),
                Normal = Vector3.back,
                Material = material,
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
                Material = material,
                Active = 0u,
            });
        }

        private static void AddSprayQuad(NativeList<SmoothSurfaceVertex> vertices,
                                         byte material, uint flags)
        {
            uint packed = material | flags;
            vertices.Add(new SmoothSurfaceVertex
            {
                Position = new Vector3(-0.8f, -0.8f, 0f),
                Normal = Vector3.back,
                Material = packed,
                Active = 0u,
            });
            vertices.Add(new SmoothSurfaceVertex
            {
                Position = new Vector3(0.8f, -0.8f, 0f),
                Normal = Vector3.back,
                Material = packed,
                Active = SmoothSurfaceVertex.WaterSprayUFlag,
            });
            vertices.Add(new SmoothSurfaceVertex
            {
                Position = new Vector3(0.8f, 0.8f, 0f),
                Normal = Vector3.back,
                Material = packed,
                Active = SmoothSurfaceVertex.WaterSprayUFlag
                       | SmoothSurfaceVertex.WaterSprayVFlag,
            });
            vertices.Add(new SmoothSurfaceVertex
            {
                Position = new Vector3(-0.8f, 0.8f, 0f),
                Normal = Vector3.back,
                Material = packed,
                Active = SmoothSurfaceVertex.WaterSprayVFlag,
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
            GpuWaterRasterFixture entry, Material material,
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
