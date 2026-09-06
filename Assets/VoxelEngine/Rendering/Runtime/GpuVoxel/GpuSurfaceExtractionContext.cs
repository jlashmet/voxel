using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    public enum GpuStageOutcome
    {
        Staged = 0,
        NoSlot = 1,
        Empty = 2,
    }

    /// <summary>
    /// Per-worker compute scratch for GPU surface extraction.
    ///
    /// Voxel ownership is intentionally not per worker. All contexts share the world-scoped mirror
    /// maintained by <see cref="GpuSurfaceMirrorCoordinator"/>. A chunk stage therefore performs no
    /// CPU brick-neighbourhood walk, no per-chunk voxel publication and no per-brick pin/unpin loop.
    /// The only CPU cache data sent before count is a three-word persistent-lookup header; actual
    /// world-brick resolution happens in VoxelBrickDensity.hlsl on the GPU.
    /// </summary>
    public sealed class GpuSurfaceExtractionContext : IDisposable
    {
        public const string ShaderResourcePath = "VoxelBrickMesher";
        private const int MaxCounterReadbackRetries = 2;

        private readonly GpuVoxelBrickMirror _mirror;
        private readonly GpuTransvoxelTables _tables;
        private readonly GpuSurfaceExtractor _extractor;
        private readonly SurfaceGeometryArena _surfaceArena;
        private readonly int _brickCacheEdge;
        private readonly uint[] _materialDefaultStyles = new uint[256];
        private bool _cataloguesUploaded;
        private uint _uploadedPaletteVersion;
        private uint _uploadedSurfaceVersion;
        private ulong _uploadedSurfaceHash;
        private uint _uploadedCoatingVersion;
        private ulong _uploadedCoatingHash;
        private bool _disposed;
        private bool _sharedExtractionActive;
        // Blocking parity/oracle tests can still stage an explicit dense snapshot without a
        // VoxelRenderBridge world. Production uses only the persistent shared-world path below.
        private readonly List<int3> _legacyPinnedBricks = new();

        private GpuChunkExtraction _staged;
        private GpuExtractionCounts _stagedCounts;
        private bool _hasStaged;
        private bool _stageAdmissionPending;
        private bool _countDispatchPending;
        private bool _writeDispatchPending;
        private bool _copyDispatchPending;
        private bool _copyQueuedForPublication;
        private int _copyIndexCount;
        private ulong _stageStorageGeneration;
        private ulong _stageRendererGeneration;
        private bool _coverageRequested;
        private uint _coverageEpoch;
        private int _coverageScanCursor;
        private bool _coverageRoundIncomplete;
        private bool _coverageReady;
        private int _lastCoveragePollFrame = -1;
        private ComputeBuffer _writeVertices;
        private ComputeBuffer _writeIndices;
        private ComputeBuffer _writeArgs;
        private int _writeArgsWordStart;
        private int _writeVertexStart;
        private int _writeVertexCapacity;
        private int _writeIndexStart;
        private int _writeIndexCapacity;
        private int _countReadbackRetries;
        private uint _countBatchToken;
        private bool _countBatchResultReady;
        private bool _countBatchFailed;
        private GpuExtractionCounts _countBatchCounts;
        private SurfaceGeometryLease _countBatchLease;
        private bool _countBatchGeometryPublished;
        private bool _pagedBatchReady;
        private bool _pagedBatchFailed;
        private int _pagedHandle = -1;
        private bool _pagedCandidatePendingResolution;
        private double _stageRequestStartedSeconds;

        public GpuVoxelBrickMirror Mirror => _mirror;
        public GpuTransvoxelTables Tables => _tables;
        public GpuSurfaceExtractor Extractor => _extractor;
        internal SurfaceGeometryArena SurfaceArena => _surfaceArena;
        public int BrickCacheEdge => _brickCacheEdge;

        public ulong ChunksStaged { get; private set; }
        public ulong ChunksWritten { get; private set; }
        public ulong ChunksRequested { get; private set; }
        public ulong ChunksMirrorReady { get; private set; }
        public ulong ChunksCountReady { get; private set; }
        public ulong ChunksWriteCompleted { get; private set; }
        public ulong ChunksCopied { get; private set; }
        public ulong ChunksRefusedNoSlot { get; private set; }
        public ulong ChunksEmpty { get; private set; }
        public ulong ChunksUnsupported { get; private set; }
        public ulong ChunksUnsupportedReconstruction { get; private set; }
        public ulong ChunksUnsupportedDecoration { get; private set; }
        public ulong ChunksOverflowed { get; private set; }
        public ulong CountReadbackRetryCount { get; private set; }
        public long MirrorCommittedBytes => _mirror.CommittedBytes;
        public bool HasActiveRequest => _stageRequestStartedSeconds > 0.0;
        public double ActiveRequestAgeMs => !HasActiveRequest ? 0.0
            : Math.Max(0.0, (Time.realtimeSinceStartupAsDouble
                           - _stageRequestStartedSeconds) * 1000.0);
        public int ActiveRequestPhase => !HasActiveRequest ? 0
            : _stageAdmissionPending ? 1
            : _copyDispatchPending ? 4
            : _writeVertices != null ? 3
            : _hasStaged ? 2
            : 1;

        private GpuSurfaceExtractionContext(ComputeShader shader, int cellsPerAxis, int padding,
                                            GpuVoxelBrickMirror mirror, int brickCacheEdge,
                                            SurfaceGeometryArena surfaceArena)
        {
            _mirror = mirror ?? throw new ArgumentNullException(nameof(mirror));
            _tables = GpuTransvoxelTables.CreateDefault();
            _extractor = new GpuSurfaceExtractor(shader, cellsPerAxis, padding, brickCacheEdge);
            _surfaceArena = surfaceArena;
            _brickCacheEdge = _extractor.BrickCacheEdge;
        }

        public static GpuSurfaceExtractionContext TryCreate(int cellsPerAxis, int padding,
                                                            long mirrorBudgetBytes,
                                                            ComputeShader shader = null)
        {
            return TryCreate(cellsPerAxis, padding, mirrorBudgetBytes, brickCacheEdge: 0, shader);
        }

        public static GpuSurfaceExtractionContext TryCreate(int cellsPerAxis, int padding,
                                                            long mirrorBudgetBytes,
                                                            int brickCacheEdge,
                                                            ComputeShader shader = null)
        {
            return TryCreate(cellsPerAxis, padding, mirrorBudgetBytes, brickCacheEdge,
                             surfaceArena: null, shader: shader);
        }

        internal static GpuSurfaceExtractionContext TryCreate(int cellsPerAxis, int padding,
                                                               long mirrorBudgetBytes,
                                                               int brickCacheEdge,
                                                               SurfaceGeometryArena surfaceArena,
                                                               ComputeShader shader = null)
        {
            if (!SystemInfo.supportsComputeShaders) return null;
            if (brickCacheEdge < 0) throw new ArgumentOutOfRangeException(nameof(brickCacheEdge));

            shader ??= Resources.Load<ComputeShader>(ShaderResourcePath);
            if (shader == null) return null;

            GpuVoxelBrickMirror mirror = null;
            try
            {
                mirror = GpuSurfaceMirrorCoordinator.Acquire(mirrorBudgetBytes);
                return new GpuSurfaceExtractionContext(
                    shader, cellsPerAxis, padding, mirror, brickCacheEdge, surfaceArena);
            }
            catch (Exception e)
            {
                if (mirror != null) GpuSurfaceMirrorCoordinator.ReleaseReference();
                Debug.LogWarning($"GPU surface extraction unavailable: {e.Message}");
                return null;
            }
        }

        public void SetCatalogues(in SurfaceCatalogueView surfaces, in CoatingCatalogueView coatings,
                                  in MaterialPaletteView palette)
        {
            if (_cataloguesUploaded
                && _uploadedPaletteVersion == palette.Version
                && _uploadedSurfaceVersion == surfaces.Version
                && _uploadedSurfaceHash == surfaces.CatalogueHash
                && _uploadedCoatingVersion == coatings.Version
                && _uploadedCoatingHash == coatings.CatalogueHash)
                return;

            for (int i = 0; i < 256; i++)
                _materialDefaultStyles[i] = palette.GetDefaultSurfaceStyle((byte)i);
            _extractor.SetCatalogues(surfaces, coatings, _materialDefaultStyles);
            _uploadedPaletteVersion = palette.Version;
            _uploadedSurfaceVersion = surfaces.Version;
            _uploadedSurfaceHash = surfaces.CatalogueHash;
            _uploadedCoatingVersion = coatings.Version;
            _uploadedCoatingHash = coatings.CatalogueHash;
            _cataloguesUploaded = true;
        }

#if UNITY_EDITOR
        // Blocking explicit-snapshot staging exists only for GPU oracle tests. Production player
        // builds expose solely the persistent shared-mirror, batched asynchronous path.
        internal GpuStageOutcome TryStage(NativeArray<TransvoxelDensityBrick> bricks,
                                          NativeArray<byte> mixedVoxels,
                                          NativeArray<ushort> mixedSurfaceSemantics,
                                          NativeArray<byte> mixedBoundarySamples,
                                          in GpuChunkExtraction request,
                                          ulong generation)
        {
            Release();
            if (!TryPinLegacySnapshot(bricks, mixedVoxels, mixedSurfaceSemantics,
                                      mixedBoundarySamples, request, generation))
                return GpuStageOutcome.NoSlot;

            _stagedCounts = _extractor.Count(_mirror, _tables, request);
            ChunksStaged++;
            if (_stagedCounts.Unsupported)
            {
                Release();
                ChunksUnsupported++;
                return GpuStageOutcome.NoSlot;
            }
            if (_stagedCounts.IsEmpty)
            {
                Release();
                ChunksEmpty++;
                return GpuStageOutcome.Empty;
            }
            return GpuStageOutcome.Staged;
        }

        private bool TryPinLegacySnapshot(
            NativeArray<TransvoxelDensityBrick> bricks,
            NativeArray<byte> mixedVoxels,
            NativeArray<ushort> mixedSurfaceSemantics,
            NativeArray<byte> mixedBoundarySamples,
            in GpuChunkExtraction request,
            ulong generation)
        {
            ThrowIfDisposed();
            int expected = _brickCacheEdge * _brickCacheEdge * _brickCacheEdge;
            if (bricks.Length < expected)
                throw new ArgumentException(
                    $"Brick snapshot holds {bricks.Length} entries; the GPU cache spans {expected}.",
                    nameof(bricks));

            _extractor.ClearBrickCache();
            for (int z = 0; z < _brickCacheEdge; z++)
            for (int y = 0; y < _brickCacheEdge; y++)
            for (int x = 0; x < _brickCacheEdge; x++)
            {
                int3 local = new(x, y, z);
                TransvoxelDensityBrick brick =
                    bricks[x + _brickCacheEdge * (y + _brickCacheEdge * z)];
                int3 coordinate = request.BrickCacheOrigin + local;
                if (brick.Kind != 2)
                {
                    VoxelBrickContent content = brick.Kind == 1
                        ? VoxelBrickContent.Uniform : VoxelBrickContent.Empty;
                    _extractor.SetBrickCacheEntry(local,
                        GpuSurfaceExtractor.PackBrickCacheEntry(
                            content, brick.UniformMaterial, -1));
                    continue;
                }

                GpuBrickPublish published = _mirror.Publish(
                    VoxelBrickDelta.MixedAt(coordinate, generation, brick.MixedOffset),
                    mixedVoxels, mixedSurfaceSemantics, mixedBoundarySamples,
                    brick.MixedOffset, hasPayload: true);
                if (published is GpuBrickPublish.NoSlot or GpuBrickPublish.PayloadMissing
                    || !_mirror.TryGetSlot(coordinate, out int slot)
                    || !_mirror.Pin(coordinate))
                {
                    ReleaseLegacyPins();
                    ChunksRefusedNoSlot++;
                    return false;
                }

                _legacyPinnedBricks.Add(coordinate);
                _extractor.SetBrickCacheEntry(local,
                    GpuSurfaceExtractor.PackBrickCacheEntry(
                        VoxelBrickContent.Mixed, 0, slot));
            }

            _mirror.FlushPendingUploads();
            _staged = request;
            _hasStaged = true;
            return true;
        }
#endif

        private void ReleaseLegacyPins()
        {
            for (int i = 0; i < _legacyPinnedBricks.Count; i++)
                _mirror.Unpin(_legacyPinnedBricks[i]);
            _legacyPinnedBricks.Clear();
        }

        internal GpuStageOutcome TryBeginStage(NativeArray<TransvoxelDensityBrick> bricks,
                                               NativeArray<byte> mixedVoxels,
                                               NativeArray<ushort> mixedSurfaceSemantics,
                                               NativeArray<byte> mixedBoundarySamples,
                                               in GpuChunkExtraction request,
                                               ulong generation)
        {
            ThrowIfDisposed();
            Release();
            ChunksRequested++;
            _stageRequestStartedSeconds = Time.realtimeSinceStartupAsDouble;
            _stageRendererGeneration = generation;
            if (!TryCaptureStorageGeneration(out _stageStorageGeneration))
            {
                ChunksRefusedNoSlot++;
                _stageRequestStartedSeconds = 0.0;
                _stageRendererGeneration = 0;
                return GpuStageOutcome.NoSlot;
            }

            // The caller's generation is its renderer-local dirty/build sequence; the mirror uses
            // Storage/change-journal versions. The persistent mirror represents live Storage, not a
            // historical snapshot, so a bounded recovery can legitimately span later Storage
            // generations. TryAdmitPendingStage refreshes this mirror-only gate on every retry.
            // CpuTransvoxelChunkCache keeps the renderer generation on the immutable build and
            // rejects that build before publication when a relevant edit made it stale. This lets
            // recovery converge without ever publishing newer mirror data as an older render build.
            _staged = request;
            _stageAdmissionPending = true;
            _coverageRequested = false;
            _lastCoveragePollFrame = -1;
            _countReadbackRetries = 0;
            TryAdmitPendingStage();

            // Mirror recovery is deliberately bounded. Not-ready this frame is backpressure, not
            // evidence that an implemented GPU path is unsupported. Report Staged so the worker
            // remains on its GPU phase and retries without routing eligible work through the CPU.
            return GpuStageOutcome.Staged;
        }

        private bool TryCaptureStorageGeneration(out ulong generation)
        {
            generation = 0;
            if (!VoxelRenderBridge.TryGetWorld(out VoxelWorldView world) || world.Storage == null)
                return false;
            generation = world.Storage.Version;
            return true;
        }

        private bool TryAdmitPendingStage()
        {
            if (!_stageAdmissionPending) return _hasStaged;

            // Covers() must gate against the generation the live persistent mirror is currently
            // trying to represent. Holding the handoff generation forever creates a liveness trap:
            // one relevant Storage edit makes Covers(oldGeneration) permanently false even after
            // the demanded blocks have been recovered. Refresh only this mirror generation; the
            // caller's immutable renderer generation is deliberately unchanged and remains the
            // authority that can discard this build before publication.
            if (!TryCaptureStorageGeneration(out _stageStorageGeneration)) return false;
            if (!BeginPersistentStage(_staged, _stageStorageGeneration)) return false;

            unchecked { _countBatchToken++; }
            _countBatchResultReady = false;
            _countBatchFailed = false;
            _countDispatchPending = true;
            _stageAdmissionPending = false;
            TryDispatchPendingCount();
            return true;
        }

        private bool BeginPersistentStage(in GpuChunkExtraction request, ulong generation)
        {
            ThrowIfDisposed();

            if (!GpuSurfaceMirrorCoordinator.PrepareFromBridge(generation))
            {
                ChunksRefusedNoSlot++;
                return false;
            }
            int coreExtentVoxels = _extractor.CellsPerAxis * request.SourceStep;
            int3 coreMaxVoxelExclusive =
                request.ChunkOriginVoxel + new int3(coreExtentVoxels);
            uint epoch = GpuSurfaceMirrorCoordinator.CoverageEpoch;
            if (!_coverageRequested || _coverageEpoch != epoch)
            {
                if (_coverageRequested)
                    ReleasePersistentCoverage(_staged);
                GpuSurfaceMirrorCoordinator.RequestCoverage(
                    request.BrickCacheOrigin, _brickCacheEdge,
                    request.ChunkOriginVoxel, coreMaxVoxelExclusive);
                _coverageRequested = true;
                _coverageEpoch = epoch;
                _coverageScanCursor = 0;
                _coverageRoundIncomplete = false;
                _coverageReady = false;
            }
            if (_lastCoveragePollFrame == Time.frameCount) return false;
            _lastCoveragePollFrame = Time.frameCount;
            if (!_coverageReady)
            {
                if (!GpuSurfaceMirrorCoordinator.Covers(
                        request.BrickCacheOrigin, _brickCacheEdge,
                        request.ChunkOriginVoxel, coreMaxVoxelExclusive, generation,
                        ref _coverageScanCursor, ref _coverageRoundIncomplete))
                    return false;
                _coverageReady = true;
            }
            if (!GpuSurfaceMirrorCoordinator.TryBeginExtraction(
                    request.BrickCacheOrigin, _brickCacheEdge))
                return false;
            ConfigurePersistentLookupHeader();
            int handle = GpuSurfaceMirrorCoordinator.PrepareChunkHandle(
                request.ChunkOriginVoxel, request.SourceStep, _stageRendererGeneration);
            if (handle < 0)
            {
                GpuSurfaceMirrorCoordinator.EndExtraction(
                    request.BrickCacheOrigin, _brickCacheEdge);
                return false;
            }
            _staged = new GpuChunkExtraction(
                request.ChunkOriginVoxel, request.BrickCacheOrigin,
                request.SourceStep, request.VoxelSize, request.TransitionFaceMask,
                handle, _stageRendererGeneration, request.ProfileBlocks);
            _hasStaged = true;
            ChunksMirrorReady++;
            _sharedExtractionActive = true;
            return true;
        }

        private void ConfigurePersistentLookupHeader()
        {
            // Density and semantic emission resolve this footprint through the persistent
            // world-coordinate directory.
            _extractor.ClearBrickCache();
            uint magic = GpuVoxelBrickMirror.PersistentLookupMagic & ~3u;
            _extractor.SetBrickCacheEntry(new int3(0, 0, 0), magic);
            _extractor.SetBrickCacheEntry(new int3(1, 0, 0),
                unchecked((uint)_mirror.DirectoryWordOffset << 2));
            _extractor.SetBrickCacheEntry(new int3(2, 0, 0),
                unchecked((uint)_mirror.DirectoryMask << 2));
        }

        internal GpuSurfaceExtractor.GpuCounterPoll TryCompleteStage(
            out GpuExtractionCounts counts)
        {
            ThrowIfDisposed();
            counts = default;
            if (_stageAdmissionPending)
            {
                if (!TryAdmitPendingStage())
                    return GpuSurfaceExtractor.GpuCounterPoll.Pending;

                // Count was dispatched only now. Give Metal at least one worker slice before
                // polling its async counters, exactly as an immediately admitted stage does.
                return GpuSurfaceExtractor.GpuCounterPoll.Pending;
            }
            if (_countDispatchPending)
            {
                if (!TryDispatchPendingCount())
                    return GpuSurfaceExtractor.GpuCounterPoll.Pending;

                return GpuSurfaceExtractor.GpuCounterPoll.Pending;
            }
            if (!_hasStaged) return GpuSurfaceExtractor.GpuCounterPoll.Failed;

            if (!_countBatchResultReady)
                return GpuSurfaceExtractor.GpuCounterPoll.Pending;

            _countBatchResultReady = false;
            GpuSurfaceExtractor.GpuCounterPoll poll = _countBatchFailed
                ? GpuSurfaceExtractor.GpuCounterPoll.Failed
                : GpuSurfaceExtractor.GpuCounterPoll.Ready;
            counts = _countBatchCounts;
            if (poll == GpuSurfaceExtractor.GpuCounterPoll.Failed)
            {
                // A failed async counter transfer is not evidence that this supported chunk needs
                // the CPU mesher. The count dispatch already owns a stable shared-mirror extraction
                // window, so retry the four-word bookkeeping transfer against the same immutable
                // request. After the local retry allowance the worker records the failure and calls
                // RetryCount; keep this stage alive so that handoff cannot become a visible hole.
                if (_countReadbackRetries < MaxCounterReadbackRetries)
                {
                    _countReadbackRetries++;
                    CountReadbackRetryCount++;
                    _countDispatchPending = true;
                    TryDispatchPendingCount();
                    return GpuSurfaceExtractor.GpuCounterPoll.Pending;
                }
                return poll;
            }

            _countReadbackRetries = 0;
            _stagedCounts = counts;
            ChunksStaged++;
            ChunksCountReady++;
            if (counts.Unsupported)
            {
                Release();
                ChunksUnsupported++;
                if ((counts.UnsupportedMask & 1u) != 0u) ChunksUnsupportedReconstruction++;
                if ((counts.UnsupportedMask & 2u) != 0u) ChunksUnsupportedDecoration++;
                return poll;
            }
            if (counts.IsEmpty)
            {
                // The count itself completed successfully, so this is not a GPU failure. Return
                // the authoritative empty result unchanged. CpuTransvoxelChunkCache publishes air
                // atomically without an arena lease and removes any older drawable representation.
                ChunksEmpty++;
            }
            return poll;
        }

        /// <summary>
        /// Re-dispatches count for the same immutable staged footprint after a transient Metal
        /// counter-readback failure. Region readers remain registered, so
        /// journal recovery cannot mutate the sampled mirror between attempts.
        /// </summary>
        internal void RetryCount()
        {
            ThrowIfDisposed();
            if (!_hasStaged) throw new InvalidOperationException("No GPU chunk is staged.");
            unchecked { _countBatchToken++; }
            _countBatchResultReady = false;
            _countBatchFailed = false;
            _countDispatchPending = true;
            TryDispatchPendingCount();
        }

        private bool TryDispatchPendingCount()
        {
            if (!_countDispatchPending) return true;
            if (!GpuSurfaceMirrorCoordinator.TryDispatchCountBatch(
                    this, _countBatchToken, _extractor, _tables, _staged, Time.frameCount))
                return false;
            _countDispatchPending = false;
            return true;
        }

        internal bool CompleteBatchedCount(uint token, in GpuExtractionCounts counts, bool failed,
                                           in SurfaceGeometryLease lease = default,
                                           bool geometryPublished = false)
        {
            if (_disposed || !_hasStaged || token != _countBatchToken) return false;
            _countBatchCounts = counts;
            _countBatchFailed = failed;
            _countBatchLease = lease;
            _countBatchGeometryPublished = geometryPublished;
            _countBatchResultReady = true;
            return true;
        }

        internal bool TryTakeCountBatchLease(out SurfaceGeometryLease lease)
        {
            lease = _countBatchLease;
            _countBatchLease = default;
            return lease.IsValid;
        }

        internal bool CompletePagedBatch(uint token, int handle)
        {
            if (_disposed || !_hasStaged || token != _countBatchToken || handle < 0) return false;
            _pagedHandle = handle;
            _pagedCandidatePendingResolution = true;
            _pagedBatchReady = true;
            return true;
        }

        internal bool FailPagedBatch(uint token)
        {
            if (_disposed || !_hasStaged || token != _countBatchToken) return false;
            _pagedHandle = -1;
            _pagedCandidatePendingResolution = false;
            _pagedBatchFailed = true;
            _pagedBatchReady = true;
            return true;
        }

        internal bool TryTakePagedBatch(out int handle, out bool failed)
        {
            // The worker's paged phase is the owner of this asynchronous state machine. Mirror
            // coverage can take several bounded scans/recovery frames, and an admitted request can
            // still wait for a free cross-chunk lane. The former counter-readback poll used to
            // advance both states incidentally; the readback-free path must do so explicitly.
            if (_stageAdmissionPending)
            {
                if (!TryAdmitPendingStage())
                {
                    handle = -1;
                    failed = false;
                    return false;
                }
            }
            else if (_countDispatchPending)
            {
                if (!TryDispatchPendingCount())
                {
                    handle = -1;
                    failed = false;
                    return false;
                }
            }

            if (!_pagedBatchReady)
            {
                handle = -1;
                failed = false;
                return false;
            }

            // Dispatch/admission above is allowed to complete the paged batch synchronously.
            // Snapshot the result only after advancing that state machine so a fresh handle cannot
            // be lost behind the previous sentinel value (-1).
            handle = _pagedHandle;
            failed = _pagedBatchFailed;
            _pagedBatchReady = false;
            _pagedBatchFailed = false;
            return true;
        }

        /// <summary>
        /// Authorizes the one completed paged candidate owned by this context. The cache calls
        /// this only after its slot/generation acceptance checks pass; every other Release path
        /// resolves the same candidate through AbortPendingPagedBatch instead.
        /// </summary>
        internal void CommitPagedBatch(int handle, int frame)
        {
            ThrowIfDisposed();
            if (!_hasStaged || !_pagedCandidatePendingResolution || handle < 0
                || handle != _pagedHandle || handle != _staged.Handle)
                throw new InvalidOperationException(
                    "GPU paged commit requires the unresolved candidate for the active staged build.");
            GpuSurfacePageArena.CommitPendingForActiveArena(
                handle, _staged.Generation, frame);
            _pagedCandidatePendingResolution = false;
        }

        private void AbortPendingPagedBatch()
        {
            if (!_pagedCandidatePendingResolution || _pagedHandle < 0) return;
            GpuSurfacePageArena.AbortPendingForActiveArena(
                _pagedHandle, _staged.Generation, Time.frameCount);
            _pagedCandidatePendingResolution = false;
        }

        public void BeginWriteRange(ComputeBuffer vertices, ComputeBuffer indices,
                                    ComputeBuffer args, int argsWordStart,
                                    int vertexStart, int vertexCapacity,
                                    int indexStart, int indexCapacity)
        {
            ThrowIfDisposed();
            _writeVertices = vertices;
            _writeIndices = indices;
            _writeArgs = args ?? throw new ArgumentNullException(nameof(args));
            if (argsWordStart < 0) throw new ArgumentOutOfRangeException(nameof(argsWordStart));
            _writeArgsWordStart = argsWordStart;
            _writeVertexStart = vertexStart;
            _writeVertexCapacity = vertexCapacity;
            _writeIndexStart = indexStart;
            _writeIndexCapacity = indexCapacity;

            if (_countBatchGeometryPublished)
            {
                _countBatchGeometryPublished = false;
                _copyIndexCount = _stagedCounts.IndexCount;
                _writeDispatchPending = false;
                _copyDispatchPending = false;
                _copyQueuedForPublication = true;
                ChunksCopied++;
                return;
            }

            // A completed zero count is already the authoritative payload. Dispatching the write
            // kernel just to prove that it writes zero vertices/indices adds a second async
            // readback failure surface and can incorrectly route valid empty chunks to the CPU.
            // Keep the caller's tiny staging lease as its publication token and queue the same
            // GPU args-plus-fence stage as non-empty geometry, with an index count of zero.
            if (_stagedCounts.IsEmpty)
            {
                _copyIndexCount = 0;
                _copyDispatchPending = true;
                TryDispatchPendingCopy();
                return;
            }
            _writeDispatchPending = true;
            TryDispatchPendingWrite();
        }

        public GpuSurfaceExtractor.GpuCounterPoll TryCompleteWriteRange(out int indexCount)
        {
            double pollStarted = Time.realtimeSinceStartupAsDouble;
            try
            {
                return TryCompleteWriteRangeCore(out indexCount);
            }
            finally
            {
                GpuSurfaceMirrorCoordinator.RecordCompletionPoll(
                    (Time.realtimeSinceStartupAsDouble - pollStarted) * 1000.0);
            }
        }

        private GpuSurfaceExtractor.GpuCounterPoll TryCompleteWriteRangeCore(out int indexCount)
        {
            ThrowIfDisposed();
            indexCount = 0;
            if (!_hasStaged) return GpuSurfaceExtractor.GpuCounterPoll.Failed;
            if (_writeDispatchPending)
            {
                if (!TryDispatchPendingWrite())
                    return GpuSurfaceExtractor.GpuCounterPoll.Pending;
                return GpuSurfaceExtractor.GpuCounterPoll.Pending;
            }
            if (_copyDispatchPending)
            {
                if (!TryDispatchPendingCopy())
                    return GpuSurfaceExtractor.GpuCounterPoll.Pending;
                return GpuSurfaceExtractor.GpuCounterPoll.Pending;
            }
            if (_copyQueuedForPublication)
            {
                _copyQueuedForPublication = false;
                indexCount = _copyIndexCount;
                ChunksWriteCompleted++;
                ChunksWritten++;
                return GpuSurfaceExtractor.GpuCounterPoll.Ready;
            }
            return GpuSurfaceExtractor.GpuCounterPoll.Failed;
        }

        private bool TryDispatchPendingWrite()
        {
            if (!_writeDispatchPending) return true;
            if (!GpuSurfaceMirrorCoordinator.TryReserveExtractionDispatch(Time.frameCount))
                return false;

            double dispatchStarted = Time.realtimeSinceStartupAsDouble;
            _copyIndexCount = _stagedCounts.IndexCount;
            _extractor.WriteScratchCopyAndPublish(
                _mirror, _tables, _staged,
                _writeVertexCapacity, _writeIndexCapacity,
                _writeVertices, _writeIndices, _writeArgs, _writeArgsWordStart,
                _writeVertexStart, _stagedCounts.VertexCount,
                _writeIndexStart, _copyIndexCount);
            _writeDispatchPending = false;
            _copyDispatchPending = false;
            _copyQueuedForPublication = true;
            ChunksCopied++;
            GpuSurfaceMirrorCoordinator.RecordWriteDispatch(
                (Time.realtimeSinceStartupAsDouble - dispatchStarted) * 1000.0);
            return true;
        }

        private bool TryDispatchPendingCopy()
        {
            if (!_copyDispatchPending) return true;
            if (!GpuSurfaceMirrorCoordinator.TryReserveExtractionDispatch(Time.frameCount))
                return false;

            DispatchCopyAndPublish();
            return true;
        }

        private void DispatchCopyAndPublish()
        {
            double dispatchStarted = Time.realtimeSinceStartupAsDouble;
            if (!_stagedCounts.IsEmpty)
                throw new InvalidOperationException(
                    "Non-empty production writes must be submitted as one write/copy batch.");
            _extractor.PublishEmpty(_writeArgs, _writeArgsWordStart);
            // Every call above is submitted to Unity's graphics queue. Its ordering is the lifetime
            // guarantee: a later scratch write, arena reuse, or indirect draw cannot overtake this
            // write -> copy -> args-publication chain. The CPU therefore publishes ownership
            // without a fence poll or completion readback; args remain the GPU-side visibility
            // record and are written last.
            _copyQueuedForPublication = true;
            _copyDispatchPending = false;
            ChunksCopied++;
            GpuSurfaceMirrorCoordinator.RecordCopyDispatch(
                (Time.realtimeSinceStartupAsDouble - dispatchStarted) * 1000.0);
        }

#if UNITY_EDITOR
        public GpuExtractionCounts StagedCounts => _stagedCounts;
#endif

        public bool TryWriteRange(ComputeBuffer vertices, ComputeBuffer indices,
                                  int vertexStart, int vertexCapacity,
                                  int indexStart, int indexCapacity,
                                  out int indexCount)
        {
            ThrowIfDisposed();
            indexCount = 0;
            if (!_hasStaged) return false;

            GpuExtractionResult result = _extractor.WriteRange(
                _mirror, _tables, _staged, vertices, indices,
                vertexStart, vertexCapacity, indexStart, indexCapacity);
            if (result.Overflowed
                || result.VertexCount != _stagedCounts.VertexCount
                || result.IndexCount != _stagedCounts.IndexCount)
            {
                ChunksOverflowed++;
                return false;
            }

            indexCount = result.IndexCount;
            ChunksWritten++;
            return true;
        }

        public void Release()
        {
            if (!_hasStaged && !_sharedExtractionActive && !_stageAdmissionPending
                && !_countDispatchPending && !_writeDispatchPending && !_copyDispatchPending
                && !_copyQueuedForPublication && !_pagedCandidatePendingResolution
                && !_countBatchLease.IsValid
                && _legacyPinnedBricks.Count == 0)
                return;
            AbortPendingPagedBatch();
            if (_coverageRequested)
                ReleasePersistentCoverage(_staged);
            _stageAdmissionPending = false;
            _countDispatchPending = false;
            _writeDispatchPending = false;
            _copyDispatchPending = false;
            _copyQueuedForPublication = false;
            _copyIndexCount = 0;
            _stageStorageGeneration = 0;
            _stageRendererGeneration = 0;
            _coverageRequested = false;
            _lastCoveragePollFrame = -1;
            _hasStaged = false;
            _countReadbackRetries = 0;
            unchecked { _countBatchToken++; }
            _countBatchResultReady = false;
            _countBatchFailed = false;
            _countBatchCounts = default;
            _countBatchGeometryPublished = false;
            _pagedBatchReady = false;
            _pagedBatchFailed = false;
            _pagedHandle = -1;
            _pagedCandidatePendingResolution = false;
            _surfaceArena?.Release(in _countBatchLease);
            _countBatchLease = default;
            _writeVertices = null;
            _writeIndices = null;
            _writeArgs = null;
            _writeArgsWordStart = 0;
            _writeVertexStart = 0;
            _writeVertexCapacity = 0;
            _writeIndexStart = 0;
            _writeIndexCapacity = 0;
            _stageRequestStartedSeconds = 0.0;
            _extractor.CancelPendingCounters();
            ReleaseLegacyPins();
            if (_sharedExtractionActive)
            {
                _sharedExtractionActive = false;
                GpuSurfaceMirrorCoordinator.EndExtraction(
                    _staged.BrickCacheOrigin, _brickCacheEdge);
            }
        }

        private void ReleasePersistentCoverage(in GpuChunkExtraction request)
        {
            int coreExtentVoxels = _extractor.CellsPerAxis * request.SourceStep;
            int3 coreMaxVoxelExclusive =
                request.ChunkOriginVoxel + new int3(coreExtentVoxels);
            GpuSurfaceMirrorCoordinator.ReleaseCoverage(
                request.BrickCacheOrigin, _brickCacheEdge,
                request.ChunkOriginVoxel, coreMaxVoxelExclusive);
            _coverageRequested = false;
            _coverageScanCursor = 0;
            _coverageRoundIncomplete = false;
            _coverageReady = false;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuSurfaceExtractionContext));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Release();
            _extractor.Dispose();
            _tables.Dispose();
            GpuSurfaceMirrorCoordinator.ReleaseReference();
        }
    }
}
