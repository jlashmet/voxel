using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>Compacts CPU-visible chunk handles into GPU-authored indirect draw buckets.</summary>
    internal sealed class GpuSurfaceDrawDispatcher : IDisposable
    {
        internal const int BucketCount = 128;
        private const int BufferedFrames = 3;
        private const int ThreadGroupSize = 64;
        private const int DrawMetadataStride = sizeof(uint) * 4;

        private static readonly int IdVisibleHandles = Shader.PropertyToID("_VisibleChunkHandles");
        private static readonly int IdLiveGeometry = Shader.PropertyToID("_LiveChunkGeometry");
        private static readonly int IdBucketState = Shader.PropertyToID("_DrawBucketState");
        private static readonly int IdIndirectArgs = Shader.PropertyToID("_DrawIndirectArgs");
        private static readonly int IdDrawMetadata = Shader.PropertyToID("_PagedDrawMetadata");
        private static readonly int IdVisibleCount = Shader.PropertyToID("_VisibleHandleCount");
        private static readonly int IdBucketCount = Shader.PropertyToID("_DrawBucketCount");

        private readonly ComputeShader _shader;
        private readonly GpuSurfacePageArena _arena;
        private readonly int _clearKernel;
        private readonly int _classifyKernel;
        private readonly int _prefixKernel;
        private readonly int _scatterKernel;
        private readonly ComputeBuffer[] _visibleHandles = new ComputeBuffer[BufferedFrames];
        private readonly ComputeBuffer[] _bucketState = new ComputeBuffer[BufferedFrames];
        private readonly ComputeBuffer[] _indirectArgs = new ComputeBuffer[BufferedFrames];
        private readonly ComputeBuffer[] _drawMetadata = new ComputeBuffer[BufferedFrames];
        private readonly uint[] _handleStaging;
        private bool _disposed;

        internal ComputeBuffer ActiveIndirectArgs { get; private set; }
        internal ComputeBuffer ActiveDrawMetadata { get; private set; }

        internal GpuSurfaceDrawDispatcher(ComputeShader shader, GpuSurfacePageArena arena)
        {
            _shader = shader != null ? shader : throw new ArgumentNullException(nameof(shader));
            _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            _clearKernel = shader.FindKernel("CSClearDrawBuckets");
            _classifyKernel = shader.FindKernel("CSClassifyVisibleHandles");
            _prefixKernel = shader.FindKernel("CSPrefixDrawBuckets");
            _scatterKernel = shader.FindKernel("CSScatterVisibleHandles");
            _handleStaging = new uint[arena.HandleCapacity];
            for (int frame = 0; frame < BufferedFrames; frame++)
            {
                _visibleHandles[frame] = new ComputeBuffer(arena.HandleCapacity, sizeof(uint),
                    ComputeBufferType.Structured, ComputeBufferMode.Dynamic);
                _bucketState[frame] = new ComputeBuffer(BucketCount * 4, sizeof(uint),
                    ComputeBufferType.Structured);
                _indirectArgs[frame] = new ComputeBuffer(BucketCount * 4, sizeof(uint),
                    ComputeBufferType.IndirectArguments);
                _drawMetadata[frame] = new ComputeBuffer(arena.HandleCapacity,
                    DrawMetadataStride, ComputeBufferType.Structured);
            }
        }

        internal void Prepare(IReadOnlyList<int> visibleHandles, int frame)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuSurfaceDrawDispatcher));
            if (visibleHandles == null) throw new ArgumentNullException(nameof(visibleHandles));
            if (visibleHandles.Count > _arena.HandleCapacity)
                throw new ArgumentOutOfRangeException(nameof(visibleHandles));
            int slot = Math.Abs(frame % BufferedFrames);
            ComputeBuffer handles = _visibleHandles[slot];
            ComputeBuffer state = _bucketState[slot];
            ComputeBuffer args = _indirectArgs[slot];
            ComputeBuffer metadata = _drawMetadata[slot];
            for (int i = 0; i < visibleHandles.Count; i++)
                _handleStaging[i] = unchecked((uint)visibleHandles[i]);
            if (visibleHandles.Count > 0)
                handles.SetData(_handleStaging, 0, 0, visibleHandles.Count);

            Bind(_clearKernel, handles, state, args, metadata);
            Bind(_classifyKernel, handles, state, args, metadata);
            Bind(_prefixKernel, handles, state, args, metadata);
            Bind(_scatterKernel, handles, state, args, metadata);
            _shader.SetInt(IdVisibleCount, visibleHandles.Count);
            _shader.SetInt(IdBucketCount, BucketCount);
            _shader.Dispatch(_clearKernel, Groups(BucketCount), 1, 1);
            if (visibleHandles.Count > 0)
                _shader.Dispatch(_classifyKernel, Groups(visibleHandles.Count), 1, 1);
            _shader.Dispatch(_prefixKernel, 1, 1, 1);
            if (visibleHandles.Count > 0)
                _shader.Dispatch(_scatterKernel, Groups(visibleHandles.Count), 1, 1);
            ActiveIndirectArgs = args;
            ActiveDrawMetadata = metadata;
        }

        private void Bind(int kernel, ComputeBuffer handles, ComputeBuffer state,
                          ComputeBuffer args, ComputeBuffer metadata)
        {
            _shader.SetBuffer(kernel, IdVisibleHandles, handles);
            _shader.SetBuffer(kernel, IdLiveGeometry, _arena.LiveChunkGeometry);
            _shader.SetBuffer(kernel, IdBucketState, state);
            _shader.SetBuffer(kernel, IdIndirectArgs, args);
            _shader.SetBuffer(kernel, IdDrawMetadata, metadata);
        }

        private static int Groups(int count) => (count + ThreadGroupSize - 1) / ThreadGroupSize;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = 0; i < BufferedFrames; i++)
            {
                _visibleHandles[i]?.Release();
                _bucketState[i]?.Release();
                _indirectArgs[i]?.Release();
                _drawMetadata[i]?.Release();
            }
            ActiveIndirectArgs = null;
            ActiveDrawMetadata = null;
        }
    }
}
