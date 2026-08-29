using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
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

        private GpuChunkExtraction _staged;
        private GpuExtractionCounts _stagedCounts;
        private bool _hasStaged;
        private bool _stageAdmissionPending;
        private ulong _stageStorageGeneration;
        private ComputeBuffer _writeVertices;
        private ComputeBuffer _writeIndices;
        private int _writeVertexStart;
        private int _writeVertexCapacity;
        private int _writeIndexStart;
        private int _writeIndexCapacity;
        private int _countReadbackRetries;
        private int _writeReadbackRetries;

        public GpuVoxelBrickMirror Mirror => _mirror;
        public GpuTransvoxelTables Tables => _tables;
        public GpuSurfaceExtractor Extractor => _extractor;
        public int BrickCacheEdge => _brickCacheEdge;

        public ulong ChunksStaged { get; private set; }
        public ulong ChunksWritten { get; private set; }
        public ulong ChunksRefusedNoSlot { get; private set; }
        public ulong ChunksEmpty { get; private set; }
        public ulong ChunksOverflowed { get; private set; }
        public ulong CountReadbackRetryCount { get; private set; }
        public ulong WriteReadbackRetryCount { get; private set; }
        public long MirrorCommittedBytes => _mirror.CommittedBytes;

        private GpuSurfaceExtractionContext(ComputeShader shader, int cellsPerAxis, int padding,
                                            GpuVoxelBrickMirror mirror, int brickCacheEdge)
        {
            _mirror = mirror ?? throw new ArgumentNullException(nameof(mirror));
            _tables = GpuTransvoxelTables.CreateDefault();
            _extractor = new GpuSurfaceExtractor(shader, cellsPerAxis, padding, brickCacheEdge);
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
            if (!SystemInfo.supportsComputeShaders) return null;
            if (brickCacheEdge < 0) throw new ArgumentOutOfRangeException(nameof(brickCacheEdge));

            shader ??= Resources.Load<ComputeShader>(ShaderResourcePath);
            if (shader == null) return null;

            GpuVoxelBrickMirror mirror = null;
            try
            {
                mirror = GpuSurfaceMirrorCoordinator.Acquire(mirrorBudgetBytes);
                return new GpuSurfaceExtractionContext(
                    shader, cellsPerAxis, padding, mirror, brickCacheEdge);
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

        internal GpuStageOutcome TryStage(NativeArray<TransvoxelDensityBrick> bricks,
                                          NativeArray<byte> mixedVoxels,
                                          NativeArray<ushort> mixedSurfaceSemantics,
                                          NativeArray<byte> mixedBoundarySamples,
                                          in GpuChunkExtraction request,
                                          ulong generation)
        {
            Release();
            if (!TryCaptureStorageGeneration(out ulong storageGeneration)
                || !BeginPersistentStage(request, storageGeneration))
                return GpuStageOutcome.NoSlot;

            _stagedCounts = _extractor.Count(_mirror, _tables, request);
            ChunksStaged++;
            if (_stagedCounts.IsEmpty)
            {
                Release();
                ChunksEmpty++;
                return GpuStageOutcome.Empty;
            }
            return GpuStageOutcome.Staged;
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
            if (!TryCaptureStorageGeneration(out _stageStorageGeneration))
            {
                ChunksRefusedNoSlot++;
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

            _extractor.BeginCount(_mirror, _tables, _staged);
            _stageAdmissionPending = false;
            return true;
        }

        private bool BeginPersistentStage(in GpuChunkExtraction request, ulong generation)
        {
            ThrowIfDisposed();

            int coreExtentVoxels = _extractor.CellsPerAxis * request.SourceStep;
            int3 coreMaxVoxelExclusive = request.ChunkOriginVoxel + new int3(coreExtentVoxels);
            if (!GpuSurfaceMirrorCoordinator.PrepareFromBridge(generation)
                || !GpuSurfaceMirrorCoordinator.Covers(
                    request.BrickCacheOrigin, _brickCacheEdge,
                    request.ChunkOriginVoxel, coreMaxVoxelExclusive, generation))
            {
                ChunksRefusedNoSlot++;
                return false;
            }

            ConfigurePersistentLookupHeader();
            _staged = request;
            _hasStaged = true;
            GpuSurfaceMirrorCoordinator.BeginExtraction();
            _sharedExtractionActive = true;
            return true;
        }

        private void ConfigurePersistentLookupHeader()
        {
            // Low two bits stay zero so the legacy raw-brick classifier treats the three header
            // words as empty entries. CPU exact-snapshot classification still guards unsupported
            // semantics; density/material reads use the persistent directory on GPU.
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
            if (!_hasStaged) return GpuSurfaceExtractor.GpuCounterPoll.Failed;

            GpuSurfaceExtractor.GpuCounterPoll poll = _extractor.TryCompleteCount(out counts);
            if (poll == GpuSurfaceExtractor.GpuCounterPoll.Pending) return poll;
            if (poll == GpuSurfaceExtractor.GpuCounterPoll.Failed)
            {
                // A failed async counter transfer is not evidence that this supported chunk needs
                // the CPU mesher. The count dispatch already owns a stable shared-mirror extraction
                // window, so retry the four-word bookkeeping transfer by redispatching the same
                // count against the same immutable request. Only repeated failure escapes to the
                // caller's existing device/error fallback.
                if (_countReadbackRetries < MaxCounterReadbackRetries)
                {
                    _countReadbackRetries++;
                    CountReadbackRetryCount++;
                    _extractor.BeginCount(_mirror, _tables, _staged);
                    return GpuSurfaceExtractor.GpuCounterPoll.Pending;
                }
                Release();
                return poll;
            }

            _countReadbackRetries = 0;
            _stagedCounts = counts;
            ChunksStaged++;
            if (counts.IsEmpty)
            {
                // The count itself completed successfully, so this is not a GPU failure. Keep the
                // staged extraction alive and let the normal write/publication path install a
                // zero-index ready entry. CpuTransvoxelChunkCache historically grouped this case
                // with lost readbacks and arena exhaustion, routing valid empty results through
                // the 64^3 CPU mesher and producing the moving-showcase fallback spikes.
                //
                // The cache currently treats IsEmpty as "do not allocate", so expose a one-element
                // sizing token only to that caller while retaining the authoritative zero counts in
                // _stagedCounts. SurfaceGeometryArena aligns this to its existing minimum lease;
                // the write verification below still compares against the true zero counts and the
                // published indirect args contain indexCount=0.
                ChunksEmpty++;
                counts = new GpuExtractionCounts(1, 1);
            }
            return poll;
        }

        public void BeginWriteRange(ComputeBuffer vertices, ComputeBuffer indices,
                                    int vertexStart, int vertexCapacity,
                                    int indexStart, int indexCapacity)
        {
            ThrowIfDisposed();
            _writeVertices = vertices;
            _writeIndices = indices;
            _writeVertexStart = vertexStart;
            _writeVertexCapacity = vertexCapacity;
            _writeIndexStart = indexStart;
            _writeIndexCapacity = indexCapacity;
            _writeReadbackRetries = 0;
            _extractor.BeginWriteRange(_mirror, _tables, _staged, vertices, indices,
                                       vertexStart, vertexCapacity, indexStart, indexCapacity);
        }

        public GpuSurfaceExtractor.GpuCounterPoll TryCompleteWriteRange(out int indexCount)
        {
            ThrowIfDisposed();
            indexCount = 0;
            if (!_hasStaged) return GpuSurfaceExtractor.GpuCounterPoll.Failed;

            GpuSurfaceExtractor.GpuCounterPoll poll = _extractor.TryCompleteWriteRange(
                _writeVertexCapacity, _writeIndexCapacity, out GpuExtractionResult result);
            if (poll == GpuSurfaceExtractor.GpuCounterPoll.Pending) return poll;
            if (poll == GpuSurfaceExtractor.GpuCounterPoll.Failed)
            {
                // Re-running a failed verification transfer is safe: the write targets the same
                // invisible staging lease, so a second dispatch only overwrites that unpublished
                // range. Do not route a transient async readback failure into CPU topology work.
                if (_writeReadbackRetries < MaxCounterReadbackRetries
                    && _writeVertices != null && _writeIndices != null)
                {
                    _writeReadbackRetries++;
                    WriteReadbackRetryCount++;
                    _extractor.BeginWriteRange(
                        _mirror, _tables, _staged, _writeVertices, _writeIndices,
                        _writeVertexStart, _writeVertexCapacity,
                        _writeIndexStart, _writeIndexCapacity);
                    return GpuSurfaceExtractor.GpuCounterPoll.Pending;
                }
                return poll;
            }

            _writeReadbackRetries = 0;
            if (result.Overflowed
                || result.VertexCount != _stagedCounts.VertexCount
                || result.IndexCount != _stagedCounts.IndexCount)
            {
                ChunksOverflowed++;
                return GpuSurfaceExtractor.GpuCounterPoll.Failed;
            }

            indexCount = result.IndexCount;
            ChunksWritten++;
            return GpuSurfaceExtractor.GpuCounterPoll.Ready;
        }

        public GpuExtractionCounts StagedCounts => _stagedCounts;

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
            if (!_hasStaged && !_sharedExtractionActive && !_stageAdmissionPending) return;
            _stageAdmissionPending = false;
            _stageStorageGeneration = 0;
            _hasStaged = false;
            _countReadbackRetries = 0;
            _writeReadbackRetries = 0;
            _writeVertices = null;
            _writeIndices = null;
            _writeVertexStart = 0;
            _writeVertexCapacity = 0;
            _writeIndexStart = 0;
            _writeIndexCapacity = 0;
            _extractor.CancelPendingCounters();
            if (_sharedExtractionActive)
            {
                _sharedExtractionActive = false;
                GpuSurfaceMirrorCoordinator.EndExtraction();
            }
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