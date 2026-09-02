using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>Compacts CPU-visible chunk handles into GPU-authored indirect draw buckets.</summary>
    internal sealed class GpuSurfaceDrawDispatcher : IDisposable
    {
        internal const int BucketCount = 64;
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
        private readonly List<int> _nonEmptyBuckets = new(BucketCount);
        private AsyncGPUReadbackRequest _argsReadbackRequest;
        private int _argsReadbackSequence;
        private bool _readbackPending;
        private bool _disposed;

        internal ComputeBuffer ActiveIndirectArgs { get; private set; }
        internal ComputeBuffer ActiveDrawMetadata { get; private set; }
        internal IReadOnlyList<int> NonEmptyBuckets => _nonEmptyBuckets;
        internal bool HasNonEmptyBuckets { get; private set; }

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

            // The GPU already knows exactly which size buckets received work. A tiny asynchronous
            // readback of the indirect args lets the render pass skip empty buckets without adding
            // a CPU classification pass or a synchronous GPU wait on every visible-set change.
            unchecked { _argsReadbackSequence++; }
            if (visibleHandles.Count == 0)
            {
                _nonEmptyBuckets.Clear();
                _readbackPending = false;
                HasNonEmptyBuckets = true;
            }
            else
            {
                _argsReadbackRequest = AsyncGPUReadback.Request(args);
                _readbackPending = true;
            }
        }

        internal void PollNonEmptyBuckets()
        {
            if (!_readbackPending) return;
            AsyncGPUReadbackRequest request = _argsReadbackRequest;
            if (request.hasError)
            {
                _readbackPending = false;
                return;
            }
            if (!request.done) return;
            _readbackPending = false;
            NativeArray<uint> data = request.GetData<uint>();
            _nonEmptyBuckets.Clear();
            for (int bucket = 0; bucket < BucketCount; bucket++)
            {
                // IndirectArguments layout: vertexCount, instanceCount, startIndex, baseInstance.
                if (data.Length > (bucket * 4) + 1 && data[(bucket * 4) + 1] != 0u)
                    _nonEmptyBuckets.Add(bucket);
            }
            data.Dispose();
            HasNonEmptyBuckets = true;
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
            _argsReadbackSequence = unchecked(++_argsReadbackSequence);
            _nonEmptyBuckets.Clear();
            HasNonEmptyBuckets = false;
            for (int frame = 0; frame < BufferedFrames; frame++)
            {
                _visibleHandles[frame]?.Release();
                _bucketState[frame]?.Release();
                _indirectArgs[frame]?.Release();
                _drawMetadata[frame]?.Release();
            }
            ActiveIndirectArgs = null;
            ActiveDrawMetadata = null;
        }
    }
}
