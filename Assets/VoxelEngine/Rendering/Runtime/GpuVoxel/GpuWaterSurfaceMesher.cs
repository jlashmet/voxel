using System;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    // Canonical immutable water snapshots contain one 8³ brick and six 8² face halos,
    // packed four material bytes per word. CPU work supplies voxel inputs, never geometry.
    // Count all slices, allocate the GPU arena transaction, write the same slices, then finalize.
    internal static class GpuWaterSurfaceMesher
    {
        internal const int SnapshotWords = 224;
        internal const int MaximumBricks = 4096;
        internal const int MaximumBricksPerSlice = 8;
        private static readonly uint[] Zeros = new uint[21];

        internal static void Count(ComputeShader shader, ComputeBuffer origins, ComputeBuffer materials,
            ComputeBuffer counters, uint waterMask, float voxelSize, int start, int count)
        {
            int kernel = Bind(shader, "CSCountWater", origins, materials, counters, waterMask, voxelSize, start, count);
            if (start == 0) counters.SetData(Zeros);
            shader.Dispatch(kernel, 48, count, 1);
        }

        internal static void Write(ComputeShader shader, ComputeBuffer origins, ComputeBuffer materials,
            ComputeBuffer counters, GpuSurfacePageArena arena, uint waterMask, float voxelSize, int start, int count)
        {
            if (arena == null) throw new ArgumentNullException(nameof(arena));
            int kernel = Bind(shader, "CSWriteWater", origins, materials, counters, waterMask, voxelSize, start, count);
            shader.SetBuffer(kernel, "_Vertices", arena.Vertices);
            shader.SetBuffer(kernel, "_Indices", arena.Indices);
            shader.SetBuffer(kernel, "_VertexPageTable", arena.VertexPageTable);
            shader.SetBuffer(kernel, "_IndexPageTable", arena.IndexPageTable);
            shader.SetInt("_VertexPageSize", GpuSurfacePageArena.VertexPageSize);
            shader.SetInt("_IndexPageSize", GpuSurfacePageArena.IndexPageSize);
            shader.SetInt("_MaxVertexPages", GpuSurfacePageArena.MaxVertexPagesPerChunk);
            shader.SetInt("_MaxIndexPages", GpuSurfacePageArena.MaxIndexPagesPerChunk);
            shader.Dispatch(kernel, 48, count, 1);
        }

        private static int Bind(ComputeShader shader, string name, ComputeBuffer origins, ComputeBuffer materials,
            ComputeBuffer counters, uint mask, float voxelSize, int start, int count)
        {
            if (shader == null || origins == null || materials == null || counters == null)
                throw new ArgumentNullException();
            if (start < 0 || count < 1 || count > MaximumBricksPerSlice || start > MaximumBricks - count
                || origins.stride != 16 || materials.stride != 4 || counters.stride != 4
                || origins.count < start + count || materials.count / SnapshotWords < start + count
                || counters.count < Zeros.Length || !(voxelSize > 0) || float.IsInfinity(voxelSize))
                throw new ArgumentOutOfRangeException(nameof(count));
            int kernel = shader.FindKernel(name);
            shader.SetBuffer(kernel, "_WaterBrickOrigins", origins);
            shader.SetBuffer(kernel, "_WaterMaterials", materials);
            shader.SetBuffer(kernel, "_BatchCounters", counters);
            shader.SetInt("_WaterBrickStart", start);
            shader.SetInt("_WaterBrickCount", count);
            shader.SetInt("_WaterMaterialMask", unchecked((int)mask));
            shader.SetFloat("_WaterVoxelSize", voxelSize);
            return kernel;
        }
    }
}
