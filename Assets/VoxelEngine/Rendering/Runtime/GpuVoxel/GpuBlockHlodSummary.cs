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
