using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Tests.RuntimeSupport
{
    /// <summary>Test-only observation of the production GPU mesher. No CPU extraction algorithm.</summary>
    public static class GpuWaterExtractionFixture
    {
        public const int Edge = 8;
        public const int FaceArea = Edge * Edge;
        public const int VoxelsPerBrick = Edge * Edge * Edge;
        public const int SnapshotStride = GpuWaterSurfaceMesher.SnapshotWords * sizeof(uint);

        public static void Extract(NativeArray<int3> origins, NativeArray<byte> snapshots,
            uint waterMask, float voxelSize, NativeList<SmoothSurfaceVertex> vertices, NativeList<uint> indices)
        {
            var shader = UnityEngine.Object.Instantiate(Resources.Load<ComputeShader>("GpuWaterSurfaceMesher"));
            var arenaShader = UnityEngine.Object.Instantiate(Resources.Load<ComputeShader>("GpuSurfacePageArena"));
            try
            {
                int count = origins.Length;
                var packed = new uint[snapshots.Length / 4];
                var bases = new int4[count];
                for (int i = 0; i < count; i++) bases[i] = new int4(origins[i], 0);
                for (int i = 0; i < snapshots.Length; i++) packed[i / 4] |= (uint)snapshots[i] << ((i % 4) * 8);
                using var gpuOrigins = new ComputeBuffer(count, 16);
                using var materials = new ComputeBuffer(packed.Length, 4);
                using var counters = new ComputeBuffer(21, 4);
                using var descriptors = new ComputeBuffer(1, GpuSurfaceExtractor.BatchChunkDescriptor.Stride);
                using var arena = new GpuSurfacePageArena(arenaShader, 65536, 131072, 2, primaryArena: false);
                gpuOrigins.SetData(bases); materials.SetData(packed);
                if (!arena.TryAcquireHandle(out int handle)) throw new InvalidOperationException("No fixture handle.");
                arena.QueueGeneration(handle, 1); arena.FlushHandleCommands(1);
                descriptors.SetData(new[] { new GpuSurfaceExtractor.BatchChunkDescriptor
                    { Handle = (uint)handle, GenerationLow = 1 } });
                // Separate slices also exercise accumulation across source portions.
                for (int i = 0; i < count; i++)
                    GpuWaterSurfaceMesher.Count(shader, gpuOrigins, materials, counters, waterMask, voxelSize, i, 1);
                arena.AllocateBatch(descriptors, counters, 1, 17, 1);
                for (int i = 0; i < count; i++)
                    GpuWaterSurfaceMesher.Write(shader, gpuOrigins, materials, counters, arena, waterMask, voxelSize, i, 1);
                arena.PublishBatch(descriptors, counters, 1, 17, 1);
                var words = new uint[21]; counters.GetData(words);
                if (words[14] != 0 || words[12] != words[6] || words[13] != words[7])
                    throw new InvalidOperationException($"GPU water fixture transaction failed: status={words[14]}.");
                var vertexPages = new uint[arena.VertexPageTable.count]; arena.VertexPageTable.GetData(vertexPages);
                var indexPages = new uint[arena.IndexPageTable.count]; arena.IndexPageTable.GetData(indexPages);
                var outputVertices = new SmoothSurfaceVertex[words[6]];
                var outputIndices = new uint[words[7]];
                int bank = handle * 2 + (int)words[18];
                for (int i = 0; i < outputVertices.Length; i += GpuSurfacePageArena.VertexPageSize)
                {
                    uint page = vertexPages[bank * GpuSurfacePageArena.MaxVertexPagesPerChunk + i / GpuSurfacePageArena.VertexPageSize];
                    arena.Vertices.GetData(outputVertices, i, (int)page * GpuSurfacePageArena.VertexPageSize,
                        Math.Min(outputVertices.Length - i, GpuSurfacePageArena.VertexPageSize));
                }
                for (int i = 0; i < outputIndices.Length; i += GpuSurfacePageArena.IndexPageSize)
                {
                    uint page = indexPages[bank * GpuSurfacePageArena.MaxIndexPagesPerChunk + i / GpuSurfacePageArena.IndexPageSize];
                    arena.Indices.GetData(outputIndices, i, (int)page * GpuSurfacePageArena.IndexPageSize,
                        Math.Min(outputIndices.Length - i, GpuSurfacePageArena.IndexPageSize));
                }
                vertices.Clear(); indices.Clear();
                foreach (var vertex in outputVertices) vertices.Add(vertex);
                foreach (uint index in outputIndices) indices.Add(index);
            }
            finally { UnityEngine.Object.DestroyImmediate(shader); UnityEngine.Object.DestroyImmediate(arenaShader); }
        }
    }
}
