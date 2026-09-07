using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.PlayMode
{
    // Narrow shader-addressing fixture, never art/visual acceptance. Synthetic vertices isolate
    // coverage and feathering; the production cache entry, shader and GPU indirect-argument kernel
    // perform all rendering. Storage-to-GPU tests separately exercise actual extraction/publication.
    internal sealed class GpuWaterRasterFixture : IDisposable
    {
        private readonly GpuWaterSurfaceChunkCache _cache = new();
        private readonly GpuWaterSurfaceChunkCache.Entry _entry;
        private readonly GameObject _cameraObject = new("Water raster fixture camera");
        private readonly Camera _camera;
        private readonly GpuSurfacePageArena _arena;
        private readonly ComputeBuffer _args;
        private const int PhysicalPage = 1;
        private const int Bank = 1;

        private struct GeometryRecord
        {
            public uint GenerationLow, GenerationHigh, Bank, VertexCount;
            public uint IndexCount, VertexPageCount, IndexPageCount, Ready;
        }

        internal GpuWaterRasterFixture()
        {
            _arena = Field<GpuSurfacePageArena>(_cache, "_geometryArena");
            _args = Field<ComputeBuffer>(_cache, "_drawArgs");
            Assert.That(_arena.TryAcquireHandle(out int blocker), Is.True);
            Assert.That(blocker, Is.Zero);
            _entry = new GpuWaterSurfaceChunkCache.Entry(int3.zero, _cache);
            Assert.That(_entry.AcquireHandle(), Is.True);
            Assert.That(_entry.Handle, Is.EqualTo(1));
            Field<Dictionary<int3, GpuWaterSurfaceChunkCache.Entry>>(_cache, "_entries")
                .Add(int3.zero, _entry);
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.transform.position = new Vector3(0, 0, -4);
            _camera.transform.LookAt(Vector3.zero);
        }

        internal void SetGeometry(NativeList<SmoothSurfaceVertex> vertices, NativeList<uint> indices)
        {
            Assert.That(vertices.Length, Is.InRange(1, GpuSurfacePageArena.VertexPageSize));
            Assert.That(indices.Length, Is.InRange(1, GpuSurfacePageArena.IndexPageSize));
            _arena.Vertices.SetData(vertices.AsArray(), 0,
                PhysicalPage * GpuSurfacePageArena.VertexPageSize, vertices.Length);
            _arena.Indices.SetData(indices.AsArray(), 0,
                PhysicalPage * GpuSurfacePageArena.IndexPageSize, indices.Length);
            _arena.VertexPageTable.SetData(new uint[] { PhysicalPage }, 0,
                (_entry.Handle * 2 + Bank) * GpuSurfacePageArena.MaxVertexPagesPerChunk, 1);
            _arena.IndexPageTable.SetData(new uint[] { PhysicalPage }, 0,
                (_entry.Handle * 2 + Bank) * GpuSurfacePageArena.MaxIndexPagesPerChunk, 1);
            _arena.LiveChunkGeometry.SetData(new[] { new GeometryRecord
            {
                GenerationLow = 1, Bank = Bank, VertexCount = (uint)vertices.Length,
                IndexCount = (uint)indices.Length, VertexPageCount = 1, IndexPageCount = 1, Ready = 1
            } }, 0, _entry.Handle, 1);
            _entry.Ready = true;
            Assert.That(_cache.CollectVisible(_camera, 0.1f).Count, Is.EqualTo(1));
        }

        internal void Draw(CommandBuffer commands, Material material, MaterialPropertyBlock properties) =>
            _entry.Draw(commands, material, properties);

        internal uint[] ReadArguments()
        {
            var args = new uint[4];
            _args.GetData(args, 0, _entry.Handle * 4, 4);
            return args;
        }

        internal static SmoothSurfaceVertex[] ReadPublishedVertices(
            GpuWaterSurfaceChunkCache cache, GpuWaterSurfaceChunkCache.Entry entry)
        {
            var arena = Field<GpuSurfacePageArena>(cache, "_geometryArena");
            var records = new GeometryRecord[arena.HandleCapacity];
            arena.LiveChunkGeometry.GetData(records);
            GeometryRecord live = records[entry.Handle];
            Assert.That(live.Ready, Is.EqualTo(1));
            Assert.That(live.IndexCount, Is.GreaterThan(0));
            var pages = new uint[arena.VertexPageTable.count];
            arena.VertexPageTable.GetData(pages);
            var result = new SmoothSurfaceVertex[live.VertexCount];
            for (int start = 0; start < result.Length; start += GpuSurfacePageArena.VertexPageSize)
            {
                uint page = pages[(entry.Handle * 2 + live.Bank) * GpuSurfacePageArena.MaxVertexPagesPerChunk
                    + start / GpuSurfacePageArena.VertexPageSize];
                arena.Vertices.GetData(result, start, (int)page * GpuSurfacePageArena.VertexPageSize,
                    Math.Min(GpuSurfacePageArena.VertexPageSize, result.Length - start));
            }
            return result;
        }

        private static T Field<T>(GpuWaterSurfaceChunkCache cache, string name) =>
            (T)typeof(GpuWaterSurfaceChunkCache).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cache);

        public void Dispose()
        {
            _cache.Dispose();
            AsyncGPUReadback.WaitAllRequests(); // Test-only drain, never a production frame wait.
            UnityEngine.Object.DestroyImmediate(_cameraObject);
        }
    }
}
