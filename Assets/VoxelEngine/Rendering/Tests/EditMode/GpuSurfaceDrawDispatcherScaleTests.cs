using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuSurfaceDrawDispatcherScaleTests
    {
        private const int VisibleCount = 600;

        [StructLayout(LayoutKind.Sequential)]
        private struct GeometryRecord
        {
            public uint GenerationLow;
            public uint GenerationHigh;
            public uint Bank;
            public uint VertexCount;
            public uint IndexCount;
            public uint VertexPageCount;
            public uint IndexPageCount;
            public uint Ready;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DrawMetadata
        {
            public uint Handle;
            public uint IndexCount;
            public uint Bank;
            public uint Padding;
        }

        private ComputeShader _arenaShader;
        private ComputeShader _drawShader;
        private GpuSurfacePageArena _arena;
        private GpuSurfaceDrawDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            Assert.That(SystemInfo.supportsComputeShaders, Is.True);
            _arenaShader = UnityEngine.Object.Instantiate(
                Resources.Load<ComputeShader>("GpuSurfacePageArena"));
            _drawShader = UnityEngine.Object.Instantiate(
                Resources.Load<ComputeShader>("GpuSurfaceDrawCompact"));
            Assert.That(_arenaShader, Is.Not.Null);
            Assert.That(_drawShader, Is.Not.Null);
            _arena = new GpuSurfacePageArena(
                _arenaShader,
                GpuSurfacePageArena.VertexPageSize * 4,
                GpuSurfacePageArena.IndexPageSize * 4,
                handleCapacity: 1024);
            _dispatcher = new GpuSurfaceDrawDispatcher(_drawShader, _arena);
        }

        [TearDown]
        public void TearDown()
        {
            // Reading the tiny args buffer drains the test's compute dispatches before disposal.
            if (_dispatcher?.ActiveIndirectArgs != null)
            {
                var drain = new uint[GpuSurfaceDrawDispatcher.BucketCount * 4];
                _dispatcher.ActiveIndirectArgs.GetData(drain);
            }
            _dispatcher?.Dispose();
            _arena?.Dispose();
            if (_drawShader != null) UnityEngine.Object.DestroyImmediate(_drawShader);
            if (_arenaShader != null) UnityEngine.Object.DestroyImmediate(_arenaShader);
            _dispatcher = null;
            _arena = null;
            _drawShader = null;
            _arenaShader = null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RasterVertex
        {
            public Vector3 Position, Normal;
            public uint Material, Active;
        }

        // A narrow raster-addressing fixture, not art or visual acceptance. It uses the shipped
        // vertex shader and real page lookup/compaction; debug coverage only bypasses lighting.
        [Test]
        public void SeparateBucketsRasterizeTheirOwnPagedGeometry()
        {
            var records = new GeometryRecord[_arena.HandleCapacity];
            var vertices = new RasterVertex[3 * GpuSurfacePageArena.VertexPageSize];
            var indices = new uint[3 * GpuSurfacePageArena.IndexPageSize];
            var vertexPages = new uint[_arena.VertexPageTable.count];
            var indexPages = new uint[_arena.IndexPageTable.count];
            var visible = new List<int> { 0, 1, 2 };
            for (int handle = 0; handle < 3; handle++)
            {
                float x = (handle - 1) * 0.65f;
                int v = handle * GpuSurfacePageArena.VertexPageSize;
                vertices[v] = new RasterVertex { Position = new Vector3(x - 0.2f, -0.3f, 0), Normal = Vector3.back };
                vertices[v+1] = new RasterVertex { Position = new Vector3(x, 0.3f, 0), Normal = Vector3.back };
                vertices[v+2] = new RasterVertex { Position = new Vector3(x + 0.2f, -0.3f, 0), Normal = Vector3.back };
                uint count = 3u << handle;
                for (int i = 0; i < count; i++)
                    indices[handle * GpuSurfacePageArena.IndexPageSize + i] = (uint)(i % 3);
                vertexPages[handle * 2 * GpuSurfacePageArena.MaxVertexPagesPerChunk] = (uint)handle;
                indexPages[handle * 2 * GpuSurfacePageArena.MaxIndexPagesPerChunk] = (uint)handle;
                records[handle] = new GeometryRecord { GenerationLow = 1, VertexCount = 3,
                    IndexCount = count, VertexPageCount = 1, IndexPageCount = 1, Ready = 1 };
            }
            _arena.Vertices.SetData(vertices);
            _arena.Indices.SetData(indices);
            _arena.VertexPageTable.SetData(vertexPages);
            _arena.IndexPageTable.SetData(indexPages);
            _arena.LiveChunkGeometry.SetData(records);
            _dispatcher.Prepare(visible, 1);
            var material = new Material(Shader.Find("Hidden/VoxelEngine/SmoothSurface"));
            var target = new RenderTexture(192, 64, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(192, 64, TextureFormat.RGBA32, false);
            var commands = new CommandBuffer();
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create();
                material.SetInteger("_SurfacePagedDraw", 1);
                material.SetFloat("_DebugCoverage", 1);
                material.SetInteger("_CutawayEnabled", 0);
                material.SetBuffer("_PagedSurfaceVertices", _arena.Vertices);
                material.SetBuffer("_PagedSurfaceIndices", _arena.Indices);
                material.SetBuffer("_PagedVertexPageTable", _arena.VertexPageTable);
                material.SetBuffer("_PagedIndexPageTable", _arena.IndexPageTable);
                material.SetBuffer("_PagedDrawMetadata", _dispatcher.ActiveDrawMetadata);
                material.SetBuffer("_PagedDrawBucketState", _dispatcher.ActiveBucketState);
                material.SetInteger("_PagedVertexPageSize", GpuSurfacePageArena.VertexPageSize);
                material.SetInteger("_PagedIndexPageSize", GpuSurfacePageArena.IndexPageSize);
                material.SetInteger("_PagedMaxVertexPagesPerChunk", GpuSurfacePageArena.MaxVertexPagesPerChunk);
                material.SetInteger("_PagedMaxIndexPagesPerChunk", GpuSurfacePageArena.MaxIndexPagesPerChunk);
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(true, true, Color.clear);
                var projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-1, 1, -1, 1, -1, 1), true);
                commands.SetViewProjectionMatrices(Matrix4x4.identity, projection);
                commands.SetGlobalMatrix("unity_MatrixVP", projection);
                for (int bucket = 0; bucket < GpuSurfaceDrawDispatcher.BucketCount; bucket++)
                {
                    commands.SetGlobalInteger("_PagedDrawBucket", bucket);
                    commands.DrawProceduralIndirect(Matrix4x4.identity, material, 0,
                        MeshTopology.Triangles, _dispatcher.ActiveIndirectArgs, bucket * 16);
                }
                Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 192, 64), 0, 0);
                pixels.Apply();
                for (int handle = 0; handle < 3; handle++)
                {
                    int x = Mathf.RoundToInt(((handle - 1) * 0.65f + 1f) * 96f);
                    Assert.That(pixels.GetPixel(x, 32).a, Is.GreaterThan(0.9f),
                        $"Bucket for handle {handle} did not rasterize its own triangle.");
                }
                Assert.That(pixels.GetPixel(3, 3).a, Is.LessThan(0.1f));
            }
            finally
            {
                RenderTexture.active = previous;
                commands.Release();
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void SixHundredVisibleHandlesSurviveBucketPrefixAndScatterExactlyOnce()
        {
            var records = new GeometryRecord[_arena.HandleCapacity];
            var visible = new List<int>(VisibleCount);
            var expectedCounts = new uint[_arena.HandleCapacity];
            for (int handle = 0; handle < VisibleCount; handle++)
            {
                // Spread the workload across many logarithmic buckets instead of validating a
                // degenerate single-bucket fixture. Counts stay triangle-aligned but otherwise
                // vary enough to exercise nonzero GPU metadata prefixes.
                uint exponent = (uint)(4 + handle % 13);
                uint lower = 1u << (int)exponent;
                uint quarter = Math.Max(1u, lower / 4u);
                uint raw = lower + (uint)(handle % 4) * quarter + (uint)(handle % 17);
                uint indexCount = Math.Max(3u, raw - raw % 3u);
                records[handle] = new GeometryRecord
                {
                    GenerationLow = (uint)(handle + 1),
                    GenerationHigh = 0u,
                    Bank = (uint)(handle & 1),
                    VertexCount = indexCount / 2u + 8u,
                    IndexCount = indexCount,
                    VertexPageCount = 1u,
                    IndexPageCount = 1u,
                    Ready = 1u,
                };
                expectedCounts[handle] = indexCount;
                visible.Add(handle);
            }
            _arena.LiveChunkGeometry.SetData(records);

            _dispatcher.Prepare(visible, frame: 7);

            var args = new uint[GpuSurfaceDrawDispatcher.BucketCount * 4];
            var metadata = new DrawMetadata[_arena.HandleCapacity];
            var bucketState = new uint[GpuSurfaceDrawDispatcher.BucketCount * 4];
            _dispatcher.ActiveBucketState.GetData(bucketState);
            _dispatcher.ActiveIndirectArgs.GetData(args);
            _dispatcher.ActiveDrawMetadata.GetData(metadata);

            int totalInstances = 0;
            int expectedStart = 0;
            var seen = new bool[_arena.HandleCapacity];
            int nonEmptyBuckets = 0;
            for (int bucket = 0; bucket < GpuSurfaceDrawDispatcher.BucketCount; bucket++)
            {
                int word = bucket * 4;
                uint maxIndexCount = args[word + 0];
                int instanceCount = unchecked((int)args[word + 1]);
                uint startVertex = args[word + 2];
                int startInstance = unchecked((int)bucketState[word + 2]);
                Assert.That(args[word + 3], Is.Zero, "Metadata offsets must not depend on API base-instance semantics.");
                Assert.That(startVertex, Is.Zero);
                Assert.That(startInstance, Is.EqualTo(expectedStart),
                    $"bucket {bucket} did not point at its scattered metadata prefix");
                if (instanceCount > 0)
                {
                    nonEmptyBuckets++;
                    Assert.That(maxIndexCount, Is.GreaterThan(0u));
                }

                for (int i = 0; i < instanceCount; i++)
                {
                    DrawMetadata draw = metadata[startInstance + i];
                    Assert.That(draw.Handle, Is.LessThan((uint)VisibleCount));
                    int handle = unchecked((int)draw.Handle);
                    Assert.That(seen[handle], Is.False,
                        $"handle {handle} was scattered more than once");
                    seen[handle] = true;
                    Assert.That(draw.IndexCount, Is.EqualTo(expectedCounts[handle]));
                    Assert.That(draw.Bank, Is.EqualTo((uint)(handle & 1)));
                    Assert.That(draw.IndexCount, Is.LessThanOrEqualTo(maxIndexCount));
                }

                totalInstances += instanceCount;
                expectedStart += instanceCount;
            }

            Assert.That(nonEmptyBuckets, Is.GreaterThan(8),
                "Fixture must exercise many GPU bucket metadata offsets.");
            Assert.That(totalInstances, Is.EqualTo(VisibleCount));
            Assert.That(expectedStart, Is.EqualTo(VisibleCount));
            for (int handle = 0; handle < VisibleCount; handle++)
                Assert.That(seen[handle], Is.True, $"handle {handle} disappeared during compaction");
        }
    }
}
