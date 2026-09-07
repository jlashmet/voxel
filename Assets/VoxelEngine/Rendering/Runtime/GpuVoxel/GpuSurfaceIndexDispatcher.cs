using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>GPU-only conversion of selected paged indices to a hardware index stream.</summary>
    internal sealed class GpuSurfaceIndexDispatcher : IDisposable
    {
        private readonly ComputeShader _shader;
        private readonly GpuSurfacePageArena _arena;
        private readonly ComputeBuffer _pages, _copyArgs;
        private readonly int _clear, _build, _buildSelected, _copy;
        internal GraphicsBuffer Indices { get; }
        internal ComputeBuffer Arguments { get; }
        internal long ResidentBytes { get; }

        internal GpuSurfaceIndexDispatcher(ComputeShader shader, GpuSurfacePageArena arena)
        {
            _shader = shader != null ? shader : throw new ArgumentNullException(nameof(shader));
            _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            _clear = shader.FindKernel("CSClearIndexedDraw");
            _build = shader.FindKernel("CSBuildIndexPages");
            _buildSelected = shader.FindKernel("CSBuildSelectedIndexPages");
            _copy = shader.FindKernel("CSCopyIndexPages");
            // Selected live banks own disjoint arena pages, so neither compacted stream can
            // exceed the source arena capacity. No per-frame CPU upload or triple buffering.
            Indices = new GraphicsBuffer(GraphicsBuffer.Target.Index | GraphicsBuffer.Target.Raw,
                arena.IndexPageCount * GpuSurfacePageArena.IndexPageSize, sizeof(uint));
            _pages = new ComputeBuffer(arena.IndexPageCount, sizeof(uint) * 4);
            Arguments = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
            _copyArgs = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);
            ResidentBytes = (long)Indices.count * Indices.stride + (long)_pages.count * _pages.stride + 32;
        }

        // Record immediately before drawing on the same graphics queue. This ordering allows
        // one GPU-owned scratch stream across frames/cameras without CPU waits or readbacks.
        internal void Record(CommandBuffer commands, ComputeBuffer metadata, ComputeBuffer buckets,
                             ComputeBuffer selected = null)
        {
            int build = selected != null ? _buildSelected : _build;
            commands.SetComputeBufferParam(_shader, _clear, "_IndexedDrawArgs", Arguments);
            commands.SetComputeBufferParam(_shader, _clear, "_IndexCopyArgs", _copyArgs);
            commands.DispatchCompute(_shader, _clear, 1, 1, 1);
            if (selected != null)
            {
                commands.SetComputeIntParam(_shader, "_HandleCapacity", _arena.HandleCapacity);
                commands.SetComputeBufferParam(_shader, build, "_LodSelected", selected);
                commands.SetComputeBufferParam(_shader, build, "_LiveChunkGeometry", _arena.LiveChunkGeometry);
            }
            else
            {
                commands.SetComputeIntParam(_shader, "_BucketCount", GpuSurfaceDrawDispatcher.BucketCount);
                commands.SetComputeBufferParam(_shader, build, "_PagedDrawMetadata", metadata);
                commands.SetComputeBufferParam(_shader, build, "_DrawBucketState", buckets);
            }
            commands.SetComputeBufferParam(_shader, build, "_IndexPageTable", _arena.IndexPageTable);
            commands.SetComputeBufferParam(_shader, build, "_IndexCopyPages", _pages);
            commands.SetComputeBufferParam(_shader, build, "_IndexedDrawArgs", Arguments);
            commands.SetComputeBufferParam(_shader, build, "_IndexCopyArgs", _copyArgs);
            commands.DispatchCompute(_shader, build, (_arena.HandleCapacity + 63) / 64, 1, 1);
            commands.SetComputeBufferParam(_shader, _copy, "_IndexCopyPages", _pages);
            commands.SetComputeBufferParam(_shader, _copy, "_VertexPageTable", _arena.VertexPageTable);
            commands.SetComputeBufferParam(_shader, _copy, "_SurfaceIndices", _arena.Indices);
            commands.SetComputeBufferParam(_shader, _copy, "_HardwareIndices", Indices);
            commands.DispatchCompute(_shader, _copy, _copyArgs, 0);
        }

        public void Dispose()
        {
            Indices?.Dispose(); Arguments?.Release(); _pages?.Release(); _copyArgs?.Release();
        }
    }
}
