using System;
using System.Collections.Generic;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>Selects GPU LOD coverage and compacts candidate handles into indirect draw buckets.</summary>
    internal sealed class GpuSurfaceDrawDispatcher : IDisposable
    {
        // Two intervals per uint exponent: fewer host submissions, at most <1.5x padded
        // vertex invocations per instance. Live index counts still bound every shader fetch.
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
        private bool _disposed;
        private GpuSurfaceLodInputs _lodInputs;
        private readonly ComputeBuffer[] _lodNodes = new ComputeBuffer[BufferedFrames];
        private readonly ComputeBuffer[] _lodState = new ComputeBuffer[BufferedFrames];
        private readonly ComputeBuffer[] _lodSelected = new ComputeBuffer[BufferedFrames];
        private readonly uint[] _lodUploadedVersions = new uint[BufferedFrames];
        private readonly int _lodClearKernel, _lodClassifyKernel, _lodReduceKernel, _lodSelectKernel;
        private readonly Vector4[] _lodPlanes = new Vector4[6];
        private bool _useLodSelection;

        // One bounded readback in flight. GPU buffers are reused; the staging arrays are
        // limited by the existing candidate capacity, not world lifetime. Callback copies data
        // before Unity expires the native readback view. Only the main-thread consumer acts on it.
        private AsyncGPUReadbackRequest _demandRequest;
        private readonly Action<AsyncGPUReadbackRequest> _receiveDemand;
        private uint[] _demandGeometry;
        private uint[] _previousDemandGeometry;
        private uint _previousDemandInputs, _previousDemandSettings;
        private uint _previousDemandTopology;
        private bool _requestQueryValid, _currentDemandFrustum, _requestDemandFrustum;
        private uint _requestInputVersion, _requestSettingsVersion;
        private Vector3 _currentDemandPosition, _requestDemandPosition;
        private readonly Vector4[] _requestPlanes = new Vector4[6];
        private bool _demandPending, _demandReady;
        private int _activeLodSlot = -1, _demandCount;
        private uint _demandTopology, _demandSettings, _settingsVersion;
        private float _settingsVoxelSize;
        private bool _settingsBandsEnabled;
        private readonly Vector4[] _settingsBands = new Vector4[4];
        internal ulong DemandFeedbackAccepted { get; private set; }
        internal ulong DemandFeedbackDiscarded { get; private set; }
        internal ulong DemandFeedbackErrors { get; private set; }

        internal ulong DemandFullRefreshes { get; private set; }
        internal ulong DemandCoordinateRefreshes { get; private set; }
        internal ulong DemandRankUpdates { get; private set; }

        internal bool TryConsumeDemand(Action<bool> begin, Action<SurfaceLodNodeKey, uint, bool> consume)
        {
            if (!_demandReady || _disposed) return false;
            _demandReady = false;
            if (_activeLodSlot < 0 || _demandTopology != _lodInputs.TopologyBuildCount
                || _demandSettings != _settingsVersion || _demandCount != _lodInputs.Count)
            { DemandFeedbackDiscarded++; _requestQueryValid = false; return false; }
            bool newTopology = _previousDemandTopology != _demandTopology;
            // Only diff against an unchanged live metadata image. A readiness/handle change,
            // including one newer than the readback, requires live generation checks again.
            bool refreshAll = newTopology || _previousDemandInputs != _lodInputs.Version
                || _requestInputVersion != _lodInputs.Version || _previousDemandSettings != _demandSettings;
            begin(refreshAll);
            if (refreshAll) DemandFullRefreshes++;
            for (int i = 0; i < _demandCount; i++)
            {
                uint geometry = _demandGeometry[i];
                bool classificationChanged = (geometry & 3u) != (_previousDemandGeometry[i] & 3u);
                if ((geometry & 4u) != 0)
                {
                    bool refresh = refreshAll ? newTopology || (geometry & 1u) != 0 || classificationChanged
                        : classificationChanged;
                    if (refresh)
                    {
                        consume(_lodInputs.KeyAt(i), geometry & ~4u, true);
                        DemandCoordinateRefreshes++;
                    }
                    else if (!refreshAll && (geometry & 1u) != 0 && (_lodInputs.Nodes[i].Flags & 2u) == 0
                        && geometry != _previousDemandGeometry[i])
                    {
                        // Completed chunks need no admission rank. Pending demand keeps fresh
                        // GPU ranks without rerunning host publication/queue/accounting work.
                        consume(_lodInputs.KeyAt(i), geometry & ~4u, false);
                        DemandRankUpdates++;
                    }
                }
                _previousDemandGeometry[i] = geometry;
            }
            _previousDemandTopology = _demandTopology;
            _previousDemandInputs = _lodInputs.Version;
            _previousDemandSettings = _demandSettings;
            DemandFeedbackAccepted++;
            return true;
        }

        internal void RequestDemandFeedback()
        {
            if (_disposed || _activeLodSlot < 0 || _demandPending || _demandReady) return;
            bool sameQuery = _requestQueryValid && _requestInputVersion == _lodInputs.Version
                && _requestSettingsVersion == _settingsVersion
                && _requestDemandPosition.Equals(_currentDemandPosition)
                && _requestDemandFrustum == _currentDemandFrustum;
            for (int p = 0; sameQuery && _currentDemandFrustum && p < 6; p++)
                sameQuery &= _requestPlanes[p].Equals(_lodPlanes[p]);
            if (sameQuery) return;
            _requestQueryValid = true;
            _requestInputVersion = _lodInputs.Version;
            _requestSettingsVersion = _settingsVersion;
            _requestDemandPosition = _currentDemandPosition;
            _requestDemandFrustum = _currentDemandFrustum;
            for (int p = 0; p < 6; p++) _requestPlanes[p] = _lodPlanes[p];
            _demandTopology = _lodInputs.TopologyBuildCount;
            _demandSettings = _settingsVersion;
            _demandCount = _lodInputs.Count;
            if (_demandCount == 0) { _demandReady = true; return; }
            _demandPending = true;
            _demandRequest = AsyncGPUReadback.Request(_lodState[_activeLodSlot],
                _demandCount * sizeof(uint), 0, _receiveDemand);
        }

        private void ReceiveDemand(AsyncGPUReadbackRequest request)
        {
            _demandPending = false;
            if (_disposed) return;
            if (request.hasError) { DemandFeedbackErrors++; _requestQueryValid = false; return; }
            var data = request.GetData<uint>();
            for (int i = 0; i < _demandCount; i++)
                _demandGeometry[i] = data[i] >> 4;
            _demandReady = true;
        }

        private void UpdateDemandSettings(float voxelSize, Vector4[] bands)
        {
            bool changed = !_settingsVoxelSize.Equals(voxelSize) || _settingsBandsEnabled != (bands != null);
            for (int i = 0; bands != null && i < 4; i++)
            {
                changed |= !_settingsBands[i].Equals(bands[i]);
                _settingsBands[i] = bands[i];
            }
            if (changed) _settingsVersion++;
            _settingsVoxelSize = voxelSize;
            _settingsBandsEnabled = bands != null;
        }

        internal double LastLodInputMs { get; private set; }
        internal int LastLodUploadedNodes { get; private set; }
        internal int LodNodeCount => _lodInputs?.Count ?? 0;
        internal ComputeBuffer ActiveLodState => _activeLodSlot >= 0 ? _lodState[_activeLodSlot] : null;
        internal ComputeBuffer ActiveLodNodes => _activeLodSlot >= 0 ? _lodNodes[_activeLodSlot] : null;
        internal ComputeBuffer ActiveLodSelection => _activeLodSlot >= 0 ? _lodSelected[_activeLodSlot] : null;
        internal GpuSurfaceIndexDispatcher IndexedDraw { get; private set; }
        internal ComputeBuffer ActiveIndirectArgs { get; private set; }
        internal ComputeBuffer ActiveDrawMetadata { get; private set; }
        internal ComputeBuffer ActiveBucketState { get; private set; }

        internal GpuSurfaceDrawDispatcher(ComputeShader shader, GpuSurfacePageArena arena)
        {
            _receiveDemand = ReceiveDemand;
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
                                 Vector4[] bands = null, Vector3 cameraPosition = default, bool reuseInputImage = false)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuSurfaceDrawDispatcher));
            if (handles.Count > _arena.HandleCapacity) throw new ArgumentOutOfRangeException(nameof(handles));
            if (_lodInputs == null)
            {
                // Four levels plus proof-only siblings, bounded independently of world lifetime.
                _lodInputs = new GpuSurfaceLodInputs(_arena.HandleCapacity * 8);
                _demandGeometry = new uint[_lodInputs.Nodes.Length];
                _previousDemandGeometry = new uint[_lodInputs.Nodes.Length];
                for (int i = 0; i < BufferedFrames; i++)
                {
                    _lodNodes[i] = new ComputeBuffer(_lodInputs.Nodes.Length, 64);
                    _lodState[i] = new ComputeBuffer(_lodInputs.Nodes.Length, 4);
                }
            }
            double inputStart = Time.realtimeSinceStartupAsDouble;
            LastLodUploadedNodes = 0;
            // The scheduler already versions membership/readiness before reusing its lists.
            // Keep dynamic camera/band classification below active even when the image is reused.
            if (!reuseInputImage || _lodInputs.Version == 0)
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
            UpdateDemandSettings(voxelSize, bands);
            _activeLodSlot = slot;
            _currentDemandPosition = cameraPosition;
            _currentDemandFrustum = planes != null;
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

        internal void Prepare(IReadOnlyList<int> visibleHandles, int frame)
        {
            _activeLodSlot = -1;
            _requestQueryValid = false;
            PrepareCore(visibleHandles, frame);
        }

        private void PrepareCore(IReadOnlyList<int> visibleHandles, int frame)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuSurfaceDrawDispatcher));
            if (visibleHandles == null) throw new ArgumentNullException(nameof(visibleHandles));
            if (visibleHandles.Count > _arena.HandleCapacity)
                throw new ArgumentOutOfRangeException(nameof(visibleHandles));
            // Indexed production drawing consumes the GPU selection directly. Bucket work is
            // retained for explicit-handle consumers and the narrow addressing regressions.
            if (_useLodSelection && IndexedDraw != null) return;
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

        internal GpuSurfaceIndexDispatcher GetIndexedDraw()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuSurfaceDrawDispatcher));
            return IndexedDraw ??= new GpuSurfaceIndexDispatcher(
                Resources.Load<ComputeShader>("GpuSurfaceIndexCompact"), _arena);
        }

        private static int Groups(int count) => (count + ThreadGroupSize - 1) / ThreadGroupSize;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            IndexedDraw?.Dispose();
            // Teardown is the only blocking boundary; a live callback must not outlive its buffers.
            if (_demandPending) _demandRequest.WaitForCompletion();
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
