using System;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// GPU-only coarse occupancy/material preparation. Each int4 request names a world brick;
    /// w = 1 requires a held proof of complete mirror coverage for the source region,
    /// allowing absent directory entries as air. CPU region readiness alone is insufficient.
    /// w = 0 keeps absent entries unknown;
    /// output is two occupancy words, sixteen packed material words and an unknown-source flag.
    /// The caller owns source/output leases through ordered completion. No feedback or waiting
    /// occurs here; an unknown source must reject downstream publication, never become known air.
    /// </summary>
    internal static class GpuBlockHlodSummary
    {
        internal const int WordsPerBlock = 19;
        internal const int MaximumBlocksPerDispatch = 1024;

        /// <summary>Consumes the existing GPU-resolved cache only after complete mirror coverage
        /// has been admitted and retained. Dispatch dimensions cover each bounded chunk layout.</summary>
        internal static void DispatchDense(ComputeShader shader, GpuVoxelBrickMirror mirror,
            ComputeBuffer entries, ComputeBuffer summaries, int bricksPerChunk, int batches, uint waterMask)
        {
            if (shader == null || mirror == null || entries == null || summaries == null)
                throw new ArgumentNullException();
            if (mirror.IsDisposed) throw new ObjectDisposedException(nameof(mirror));
            if (mirror.IsClearPending) throw new InvalidOperationException("HLOD source is awaiting reset.");
            const int maximumEdge = GpuBlockHlodMesher.MaximumCoreBrickEdge + 2;
            if (bricksPerChunk < 1 || bricksPerChunk > maximumEdge * maximumEdge * maximumEdge
                || batches < 1 || batches > GpuBlockHlodMesher.MaximumBatchCount
                || entries.stride != 4 || summaries.stride != 4
                || entries.count < bricksPerChunk * batches
                || summaries.count < bricksPerChunk * batches * WordsPerBlock)
                throw new ArgumentOutOfRangeException(nameof(bricksPerChunk));
            int kernel = shader.FindKernel("CSSummarizeDenseBlocks");
            shader.SetBuffer(kernel, "_BrickMaterials", mirror.Materials);
            shader.SetBuffer(kernel, "_HlodDenseEntries", entries);
            shader.SetBuffer(kernel, "_HlodSummaries", summaries);
            shader.SetInt("_HlodBlockCount", bricksPerChunk);
            shader.SetInt("_SolidWaterMaterialMask", unchecked((int)waterMask));
            shader.Dispatch(kernel, Math.Min(1024, bricksPerChunk), (bricksPerChunk + 1023) / 1024, batches);
        }

        internal static void Dispatch(ComputeShader shader, GpuVoxelBrickMirror mirror,
            ComputeBuffer blocks, ComputeBuffer summaries, int count, uint waterMaterialMask)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));
            if (summaries == null) throw new ArgumentNullException(nameof(summaries));
            if (count < 1 || count > MaximumBlocksPerDispatch || count > blocks.count
                || count * WordsPerBlock > summaries.count || blocks.stride != 16 || summaries.stride != 4)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (mirror.IsDisposed) throw new ObjectDisposedException(nameof(mirror));
            if (mirror.IsClearPending) throw new InvalidOperationException("HLOD source is awaiting reset.");
            mirror.FlushPendingUploads();
            int kernel = shader.FindKernel("CSSummarizeBlocks");
            shader.SetBuffer(kernel, "_BrickMaterials", mirror.Materials);
            shader.SetBuffer(kernel, "_HlodBlocks", blocks);
            shader.SetBuffer(kernel, "_HlodSummaries", summaries);
            shader.SetInt("_HlodBlockCount", count);
            shader.SetInt("_HlodDirectoryOffset", mirror.DirectoryWordOffset);
            shader.SetInt("_HlodDirectoryMask", mirror.DirectoryCapacity - 1);
            shader.SetInt("_SolidWaterMaterialMask", unchecked((int)waterMaterialMask));
            shader.Dispatch(kernel, count, 1, 1);
        }
    }
}
