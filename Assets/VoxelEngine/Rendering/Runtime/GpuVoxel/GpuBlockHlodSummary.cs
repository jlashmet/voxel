using System;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// GPU coarse occupancy/material preparation. The production source-range kernel derives
    /// coordinates and readiness from explicit GPU directory keys and region-level residency.
    /// Missing-source indices request uploads; summary/geometry data never leaves the GPU.
    /// Legacy dense/block entry points retain their independent coverage-proof contracts.
    /// Callers own all source/output buffers through ordered completion.
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
            shader.SetInt("_HlodOutputBlockOffset", 0);
            shader.SetInt("_SolidWaterMaterialMask", unchecked((int)waterMask));
            shader.Dispatch(kernel, Math.Min(1024, bricksPerChunk), (bricksPerChunk + 1023) / 1024, batches);
        }

        /// <summary>Writes a bounded source portion into its final summary range without touching
        /// earlier portions. Sources may be released after ordered GPU completion; the caller must
        /// retain generation validity across all portions before admitting the completed summary.</summary>
        internal static void Dispatch(ComputeShader shader, GpuVoxelBrickMirror mirror,
            ComputeBuffer blocks, ComputeBuffer summaries, int count, uint waterMaterialMask,
            int outputBlockOffset = 0)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));
            if (summaries == null) throw new ArgumentNullException(nameof(summaries));
            if (count < 1 || count > MaximumBlocksPerDispatch || count > blocks.count
                || blocks.stride != 16 || summaries.stride != 4)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (outputBlockOffset < 0 || (long)outputBlockOffset + count > summaries.count / WordsPerBlock)
                throw new ArgumentOutOfRangeException(nameof(outputBlockOffset));
            if (mirror.IsDisposed) throw new ObjectDisposedException(nameof(mirror));
            if (mirror.IsClearPending) throw new InvalidOperationException("HLOD source is awaiting reset.");
            mirror.FlushPendingUploads();
            int kernel = shader.FindKernel("CSSummarizeBlocks");
            shader.SetBuffer(kernel, "_BrickMaterials", mirror.Materials);
            shader.SetBuffer(kernel, "_HlodBlocks", blocks);
            shader.SetBuffer(kernel, "_HlodSummaries", summaries);
            shader.SetInt("_HlodBlockCount", count);
            shader.SetInt("_HlodOutputBlockOffset", outputBlockOffset);
            shader.SetInt("_HlodDirectoryOffset", mirror.DirectoryWordOffset);
            shader.SetInt("_HlodDirectoryMask", mirror.DirectoryCapacity - 1);
            shader.SetInt("_PersistentDirectoryProbeCount", mirror.MaximumDirectoryProbeCount);
            shader.SetInt("_SolidWaterMaterialMask", unchecked((int)waterMaterialMask));
            shader.Dispatch(kernel, count, 1, 1);
        }
        internal static void DispatchSourceRange(ComputeShader shader, GpuVoxelBrickMirror mirror,
            ComputeBuffer regions, int regionCount, int3 origin, int3 extent,
            ComputeBuffer summaries, int outputBlockOffset, ComputeBuffer missing, uint waterMaterialMask,
            ComputeBuffer occupancy = null, ComputeBuffer blockReferences = null)
        {
            int count = checked(extent.x * extent.y * extent.z);
            if (count < 1 || count > MaximumBlocksPerDispatch || regionCount < 1
                || regionCount > regions.count || missing.count < count + 1)
                throw new ArgumentOutOfRangeException(nameof(extent));
            if (outputBlockOffset < 0 || (long)(outputBlockOffset + count) * WordsPerBlock > summaries.count)
                throw new ArgumentOutOfRangeException(nameof(outputBlockOffset));
            if (occupancy != null && (extent.z != 1 || occupancy.stride != 4
                || occupancy.count < regionCount * 128))
                throw new ArgumentException("Occupancy must contain one 64x64-bit Z slice per region.", nameof(occupancy));
            if (blockReferences != null && (extent.z != 1 || blockReferences.stride != 4
                || blockReferences.count < regionCount * 4096 || occupancy != null))
                throw new ArgumentException("Block references must contain one 64x64 Z slice per region.", nameof(blockReferences));
            if (mirror.IsDisposed || mirror.IsClearPending)
                throw new InvalidOperationException("HLOD source mirror is unavailable.");
            mirror.FlushPendingUploads();
            int kernel = shader.FindKernel("CSSummarizeSourceRange");
            shader.SetBuffer(kernel, "_BrickMaterials", mirror.Materials);
            shader.SetBuffer(kernel, "_HlodRegions", regions);
            shader.SetBuffer(kernel, "_HlodOccupancy", occupancy ?? missing);
            shader.SetInt("_HlodHasOccupancy", occupancy == null ? 0 : 1);
            shader.SetBuffer(kernel, "_HlodBlockReferences", blockReferences ?? missing);
            shader.SetInt("_HlodHasBlockReferences", blockReferences == null ? 0 : 1);
            shader.SetBuffer(kernel, "_HlodMissingBlocks", missing);
            shader.SetBuffer(kernel, "_HlodSummaries", summaries);
            shader.SetInts("_HlodRangeOrigin", origin.x, origin.y, origin.z);
            shader.SetInts("_HlodRangeExtent", extent.x, extent.y, extent.z);
            shader.SetInt("_HlodRegionCount", regionCount);
            shader.SetInt("_HlodBlockCount", count);
            shader.SetInt("_HlodOutputBlockOffset", outputBlockOffset);
            shader.SetInt("_HlodDirectoryOffset", mirror.DirectoryWordOffset);
            shader.SetInt("_HlodDirectoryMask", mirror.DirectoryCapacity - 1);
            shader.SetInt("_PersistentDirectoryProbeCount", mirror.MaximumDirectoryProbeCount);
            shader.SetInt("_SolidWaterMaterialMask", unchecked((int)waterMaterialMask));
            shader.Dispatch(kernel, count, 1, 1);
        }
    }
}
