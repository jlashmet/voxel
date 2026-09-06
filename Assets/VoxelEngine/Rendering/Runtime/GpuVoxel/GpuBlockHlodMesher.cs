using System;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// Bounded count/write slices for feature-preserving coarse geometry. Summaries are padded
    /// by one brick on every side, concatenated per descriptor in X/Y/Z order. The caller holds
    /// immutable summaries, descriptors, counters and arena leases until ordered GPU completion.
    /// Count every slice, allocate with GpuSurfacePageArena, write every slice, then finalize and
    /// explicitly commit through the same versioned transaction used by near geometry.
    /// No geometry, allocation count or completion is read back here.
    /// </summary>
    internal static class GpuBlockHlodMesher
    {
        internal const int MaximumCoreBrickEdge = 64;
        internal const int MaximumBricksPerSlice = 1024;
        internal const int MaximumBatchCount = 4;
        private static readonly uint[] CounterZeros = new uint[
            GpuSurfaceExtractor.BatchHeaderWords + MaximumBatchCount * GpuSurfaceExtractor.BatchRecordWords];

        internal static void SelectFallback(ComputeShader shader, ComputeBuffer descriptors,
            ComputeBuffer counters, ComputeBuffer selection, int batchCount)
        {
            if (selection == null || selection.stride != sizeof(uint) || selection.count < batchCount
                || batchCount < 1 || batchCount > MaximumBatchCount)
                throw new ArgumentException("Fallback selection must cover the bounded batch.", nameof(selection));
            int kernel = shader.FindKernel("CSSelectHlodFallback");
            shader.SetBuffer(kernel, "_BatchChunks", descriptors);
            shader.SetBuffer(kernel, "_BatchCounters", counters);
            shader.SetBuffer(kernel, "_HlodSelection", selection);
            shader.SetInt("_HlodBatchCount", batchCount);
            shader.Dispatch(kernel, 1, 1, 1);
        }

        internal static void Count(ComputeShader shader, ComputeBuffer summaries,
            ComputeBuffer descriptors, ComputeBuffer counters, int coreBrickEdge,
            int batchCount, int brickStart, int brickCount, ComputeBuffer selection = null)
        {
            int kernel = Bind(shader, "CSCountHlodFaces", summaries, descriptors, counters,
                coreBrickEdge, batchCount, brickStart, brickCount, selection);
            if (brickStart == 0 && selection == null)
                counters.SetData(CounterZeros, 0, 0, GpuSurfaceExtractor.BatchHeaderWords
                    + batchCount * GpuSurfaceExtractor.BatchRecordWords);
            shader.Dispatch(kernel, (brickCount * 24 + 63) / 64, batchCount, 1);
        }

        internal static void Write(ComputeShader shader, ComputeBuffer summaries,
            ComputeBuffer descriptors, ComputeBuffer counters, GpuSurfacePageArena arena,
            int coreBrickEdge, int batchCount, int brickStart, int brickCount, ComputeBuffer selection = null)
        {
            if (arena == null) throw new ArgumentNullException(nameof(arena));
            int kernel = Bind(shader, "CSWriteHlodFaces", summaries, descriptors, counters,
                coreBrickEdge, batchCount, brickStart, brickCount, selection);
            shader.SetBuffer(kernel, "_Vertices", arena.Vertices);
            shader.SetBuffer(kernel, "_Indices", arena.Indices);
            shader.SetBuffer(kernel, "_VertexPageTable", arena.VertexPageTable);
            shader.SetBuffer(kernel, "_IndexPageTable", arena.IndexPageTable);
            shader.SetInt("_VertexPageSize", GpuSurfacePageArena.VertexPageSize);
            shader.SetInt("_IndexPageSize", GpuSurfacePageArena.IndexPageSize);
            shader.SetInt("_MaxVertexPages", GpuSurfacePageArena.MaxVertexPagesPerChunk);
            shader.SetInt("_MaxIndexPages", GpuSurfacePageArena.MaxIndexPagesPerChunk);
            shader.Dispatch(kernel, (brickCount * 24 + 63) / 64, batchCount, 1);
        }

        private static int Bind(ComputeShader shader, string kernelName, ComputeBuffer summaries,
            ComputeBuffer descriptors, ComputeBuffer counters, int edge, int batches, int start, int count, ComputeBuffer selection)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            if (summaries == null) throw new ArgumentNullException(nameof(summaries));
            if (descriptors == null) throw new ArgumentNullException(nameof(descriptors));
            if (counters == null) throw new ArgumentNullException(nameof(counters));
            if (edge < 1 || edge > MaximumCoreBrickEdge) throw new ArgumentOutOfRangeException(nameof(edge));
            if (batches < 1 || batches > MaximumBatchCount) throw new ArgumentOutOfRangeException(nameof(batches));
            int total = edge * edge * edge, padded = edge + 2;
            if (start < 0 || count < 1 || count > MaximumBricksPerSlice || start > total - count)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (summaries.stride != 4 || summaries.count < padded * padded * padded * batches
                    * GpuBlockHlodSummary.WordsPerBlock
                || descriptors.stride != GpuSurfaceExtractor.BatchChunkDescriptor.Stride
                || descriptors.count < batches || counters.stride != 4
                || counters.count < GpuSurfaceExtractor.BatchHeaderWords
                    + batches * GpuSurfaceExtractor.BatchRecordWords)
                throw new ArgumentException("Coarse mesh buffers do not match the padded batch layout.");
            if (selection != null && (selection.stride != sizeof(uint) || selection.count < batches))
                throw new ArgumentException("Fallback selection must cover every descriptor.", nameof(selection));
            int kernel = shader.FindKernel(kernelName);
            shader.SetBuffer(kernel, "_HlodSelection", selection ?? counters);
            shader.SetInt("_HlodConditional", selection == null ? 0 : 1);
            shader.SetBuffer(kernel, "_HlodSummaries", summaries);
            shader.SetBuffer(kernel, "_BatchChunks", descriptors);
            shader.SetBuffer(kernel, "_BatchCounters", counters);
            shader.SetInt("_HlodCoreBrickEdge", edge);
            shader.SetInt("_HlodBatchCount", batches);
            shader.SetInt("_HlodBrickStart", start);
            shader.SetInt("_HlodBrickCount", count);
            return kernel;
        }
    }
}
