using System;
using System.Collections.Generic;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>Selects GPU LOD coverage and compacts candidate handles into indirect draw buckets.</summary>
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
        private GpuSurfaceLodInputs _lodInputs;
        private readonly ComputeBuffer[] _lodNodes = new ComputeBuffer[BufferedFrames];
        private readonly ComputeBuffer[] _lodState = new ComputeBuffer[BufferedFrames];
        private readonly ComputeBuffer[] _lodSelected = new ComputeBuffer[BufferedFrames];
        private readonly uint[] _lodUploadedVersions = new uint[BufferedFrames];
        private readonly int _lodClearKernel, _lodClassifyKernel, _lodReduceKernel, _lodSelectKernel;
        private readonly Vector4[] _lodPlanes = new Vector4[6];
        private bool _useLodSelection;

        internal double LastLodInputMs { get; private set; }
        internal int LastLodUploadedNodes { get; private set; }
        internal int LodNodeCount => _lodInputs?.Count ?? 0;
        internal ComputeBuffer ActiveIndirectArgs { get; private set; }
        internal ComputeBuffer ActiveDrawMetadata { get; private set; }
        internal ComputeBuffer ActiveBucketState { get; private set; }

        internal GpuSurfaceDrawDispatcher(ComputeShader shader, GpuSurfacePageArena arena)
        {
            _shader = shader != null ? shader : throw new ArgumentNullException(nameof(shader));
            _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            _clearKernel = shader.FindKernel("CSClearDrawBuckets");
            _classifyKernel = shader.FindKernel("CSClassifyVisibleHandles");
            _prefixKernel = shader.FindKernel("CSPrefixDrawBuckets");
            _scatterKernel = shader.FindKernel("CSScatterVisibleHandles");
            _lodClearKernel = shader.FindKernel("CSClearLodSelection");
            _lodClassifyKernel = shader.FindKernel("CSClassifyLodCandidates");
            _lodReduceKernel = shader.FindKernel("CSReduceLodCoverage");
            _lodSelectKernel = shader.FindKernel("CSSelectLodCoverage");
            _handleStaging = new uint[arena.HandleCapacity];
            for (int frame = 0; frame < BufferedFrames; frame++)
            {
                _lodSelected[frame] = new ComputeBuffer(arena.HandleCapacity, sizeof(uint));
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

        internal void PrepareLod(IReadOnlyList<SurfaceLodNodeKey> drawable,
                                 IReadOnlyList<int> handles,
                                 IReadOnlyList<SurfaceLodNodeKey> complete, int frame,
                                 IReadOnlyList<SurfaceLodNodeKey> owned = null,
                                 Plane[] planes = null, float voxelSize = 1f,
                                 Vector4[] bands = null, Vector3 cameraPosition = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuSurfaceDrawDispatcher));
            if (handles.Count > _arena.HandleCapacity) throw new ArgumentOutOfRangeException(nameof(handles));
            if (_lodInputs == null)
            {
                // Four levels plus proof-only siblings, bounded independently of world lifetime.
                _lodInputs = new GpuSurfaceLodInputs(_arena.HandleCapacity * 8);
                for (int i = 0; i < BufferedFrames; i++)
                {
                    _lodNodes[i] = new ComputeBuffer(_lodInputs.Nodes.Length, 64);
                    _lodState[i] = new ComputeBuffer(_lodInputs.Nodes.Length, 4);
                }
            }
            double inputStart = Time.realtimeSinceStartupAsDouble;
            LastLodUploadedNodes = 0;
            _lodInputs.Update(drawable, handles, complete, owned);
            int slot = Math.Abs(frame % BufferedFrames);
            if (_lodUploadedVersions[slot] != _lodInputs.Version)
            {
                if (_lodInputs.Count > 0)
                    _lodNodes[slot].SetData(_lodInputs.Nodes, 0, 0, _lodInputs.Count);
                _lodUploadedVersions[slot] = _lodInputs.Version;
                LastLodUploadedNodes = _lodInputs.Count;
            }
            LastLodInputMs = (Time.realtimeSinceStartupAsDouble - inputStart) * 1000.0;
            for (int k = 0; k < 4; k++)
            {
                int kernel = k == 0 ? _lodClearKernel : k == 1 ? _lodClassifyKernel : k == 2 ? _lodReduceKernel : _lodSelectKernel;
                _shader.SetBuffer(kernel, "_LodNodes", _lodNodes[slot]);
                _shader.SetBuffer(kernel, "_LodState", _lodState[slot]);
                _shader.SetBuffer(kernel, "_LodSelected", _lodSelected[slot]);
                _shader.SetBuffer(kernel, IdLiveGeometry, _arena.LiveChunkGeometry);
            }
            if (bands != null && bands.Length != 4) throw new ArgumentException("Four LOD bands are required.", nameof(bands));
            _shader.SetInt("_LodBandsEnabled", bands != null ? 1 : 0);
            if (bands != null) _shader.SetVectorArray("_LodBands", bands);
            _shader.SetVector("_LodCameraPosition", cameraPosition);
            _shader.SetInt("_LodFrustumEnabled", planes != null ? 1 : 0);
            if (planes != null)
            {
                for (int i = 0; i < 6; i++)
                    _lodPlanes[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
                _shader.SetVectorArray("_LodFrustumPlanes", _lodPlanes);
            }
            _shader.SetFloat("_LodVoxelSize", voxelSize);
            _shader.SetInt("_LodNodeCount", _lodInputs.Count);
            _shader.SetInt("_LodHandleCapacity", _arena.HandleCapacity);
            _shader.Dispatch(_lodClearKernel, Groups(_arena.HandleCapacity), 1, 1);
            if (_lodInputs.Count > 0)
            {
                _shader.Dispatch(_lodClassifyKernel, Groups(_lodInputs.Count), 1, 1);
                for (int step = 1; step <= 8; step *= 2)
                {
                    _shader.SetInt("_LodSourceStep", step);
                    _shader.Dispatch(_lodReduceKernel, Groups(_lodInputs.Count), 1, 1);
                }
                _shader.Dispatch(_lodSelectKernel, Groups(_lodInputs.Count), 1, 1);
            }
            _useLodSelection = true;
            try { PrepareCore(handles, frame); }
            finally { _useLodSelection = false; }
        }

        internal void Prepare(IReadOnlyList<int> visibleHandles, int frame) => PrepareCore(visibleHandles, frame);

        private void PrepareCore(IReadOnlyList<int> visibleHandles, int frame)
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
            int dispatchCount = _useLodSelection ? _arena.HandleCapacity : visibleHandles.Count;
            if (!_useLodSelection)
            {
                for (int i = 0; i < visibleHandles.Count; i++)
                    _handleStaging[i] = unchecked((uint)visibleHandles[i]);
                if (visibleHandles.Count > 0) handles.SetData(_handleStaging, 0, 0, visibleHandles.Count);
            }

            Bind(_clearKernel, handles, state, args, metadata);
            Bind(_classifyKernel, handles, state, args, metadata);
            Bind(_prefixKernel, handles, state, args, metadata);
            Bind(_scatterKernel, handles, state, args, metadata);
            _shader.SetInt(IdVisibleCount, dispatchCount);
            _shader.SetInt("_UseLodSelection", _useLodSelection ? 1 : 0);
            _shader.SetBuffer(_classifyKernel, "_LodSelected", _lodSelected[slot]);
            _shader.SetBuffer(_scatterKernel, "_LodSelected", _lodSelected[slot]);
            _shader.SetInt(IdBucketCount, BucketCount);
            _shader.Dispatch(_clearKernel, Groups(BucketCount), 1, 1);
            if (dispatchCount > 0)
                _shader.Dispatch(_classifyKernel, Groups(dispatchCount), 1, 1);
            _shader.Dispatch(_prefixKernel, 1, 1, 1);
            if (dispatchCount > 0)
                _shader.Dispatch(_scatterKernel, Groups(dispatchCount), 1, 1);
            ActiveIndirectArgs = args;
            ActiveDrawMetadata = metadata;
            ActiveBucketState = state;
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
                _lodNodes[i]?.Release();
                _lodState[i]?.Release();
                _lodSelected[i]?.Release();
                _visibleHandles[i]?.Release();
                _bucketState[i]?.Release();
                _indirectArgs[i]?.Release();
                _drawMetadata[i]?.Release();
            }
            ActiveIndirectArgs = null;
            ActiveDrawMetadata = null;
            ActiveBucketState = null;
        }
    }
}
