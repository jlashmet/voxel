using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using VoxelEngine.Storage.Api;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Host admission, invalidation and publication for GPU-extracted solid voxel geometry.
    ///
    /// Compute samples authoritative voxel data from the persistent mirror. Curvature is reconstructed
    /// from local style rules, while collision and destruction continue to use discrete cells.
    ///
    /// Every non-liquid solid participates in this field. Surface semantics, rather than a
    /// brick-wide renderer classifier, control local reconstruction.
    /// </summary>
    public sealed class GpuSolidChunkCache : IDisposable
    {
        private static readonly ProfilerMarker s_PrepareMarker =
            new("Voxel.Surface.WorkerPrepare");
        public const int CellsPerAxis = 64;

        // A chunk is always CellsPerAxis cells regardless of ring, so extraction work per
        // chunk is constant; SourceStep only widens the world extent each cell spans. That
        // asymmetry is what makes coarse rings cheaper per unit volume: the density grid,
        // topology tables, and faceted masks below are all sized by cells, not voxels.
        // Only the snapshot brick cache scales, because it must still cover the full extent.
        public readonly int SourceStep;
        public readonly int VoxelsPerAxis;
        public readonly int BricksPerAxis;

        /// <summary>
        /// True when this ring reads the region mip pyramid rather than individual voxels.
        /// Rings finer than one brick have no mip level to read and require resident bricks;
        /// see <see cref="VoxelReadGrid.LevelForStride"/>.
        /// </summary>
        public readonly bool SamplesFromMips;
        /// <summary>
        /// Step 8 keeps the exact versioned/COW snapshot boundary but compresses each 8^3 block
        /// into eight spatial 4^3 HLOD subcells before meshing. This replaces the expensive exact
        /// Transvoxel fallback without ever treating Storage's any-solid block projection as
        /// render density.
        /// </summary>
        public bool UsesBlockHlod => SourceStep == VoxelReadGrid.BlockEdge;
        private const int FeaturePreservingFallbackStep = VoxelReadGrid.BlockEdge / 2;
        private bool SupportsFeaturePreservingFallback =>
            SourceStep == FeaturePreservingFallbackStep;

        /// <summary>Chunk geometry of the base ring (SourceStep 1). Authoring and capture tools
        /// address the world in full-resolution chunks, so they want these rather than the
        /// ring-dependent instance values.</summary>
        public const int BaseSourceStep = 1;
        public const int BaseVoxelsPerAxis = CellsPerAxis * BaseSourceStep;
        public const int BaseBricksPerAxis = BaseVoxelsPerAxis / VoxelReadGrid.BlockEdge;

        /// <summary>All solid LOD steps use GPU extraction, including conditional step-4 HLOD.</summary>
        internal static bool SupportsGpuSurfaceStep(int sourceStep) =>
            sourceStep == BaseSourceStep || sourceStep == BaseSourceStep * 2
            || sourceStep == FeaturePreservingFallbackStep || sourceStep == VoxelReadGrid.BlockEdge;

        private const int Padding = 1;
        private const int GridSize = CellsPerAxis + 3;
        private const int GridSampleCount = GridSize * GridSize * GridSize;
        private const int CellsPerSlice = 512;
        private const int BrickCachePadding = 1;
        private const int MaxExactSnapshotRegions = 27;
        private const int ExactMixedPinChecksPerDeadline = 16;
        private readonly int BrickCacheEdge;
        private readonly int BrickCacheCount;
        private const uint FullyLitOcclusion = 0x0000FF00u;

        public sealed class Entry : IDisposable
        {
            public int3 Coordinate { get; private set; }
            public readonly int VoxelsPerAxis;
            public readonly int SourceStep;
            public bool Ready;
            public bool IsGpuPaged => GpuHandle >= 0;
            public int GpuHandle { get; private set; } = -1;
            public int IndexCount => IsGpuPaged ? -1 : 0;
            public int LastUsedFrame;
            internal bool GpuDemandInBand;
            public long GpuBytes => 0; // Geometry size is GPU-owned, never read back per entry.
            public ulong SourceVersion { get; internal set; }
            public uint MaterialPaletteVersion { get; internal set; }
            public uint SurfaceCatalogueVersion { get; internal set; }
            public ulong SurfaceCatalogueHash { get; internal set; }
            public uint CoatingCatalogueVersion { get; internal set; }
            public ulong CoatingCatalogueHash { get; internal set; }

            internal Entry(int3 coordinate, int voxelsPerAxis, int sourceStep)
            {
                Coordinate = coordinate;
                VoxelsPerAxis = voxelsPerAxis;
                SourceStep = sourceStep;
            }

            internal void Reinitialize(int3 coordinate)
            {
                if (Ready || IsGpuPaged)
                    throw new InvalidOperationException("Release a GPU entry before reusing it.");
                Coordinate = coordinate;
                LastUsedFrame = 0;
                GpuDemandInBand = false;
                SourceVersion = 0;
                MaterialPaletteVersion = SurfaceCatalogueVersion = CoatingCatalogueVersion = 0;
                SurfaceCatalogueHash = CoatingCatalogueHash = 0;
            }

            internal void PublishGpuPaged(int handle)
            {
                if (handle < 0) throw new ArgumentOutOfRangeException(nameof(handle));
                GpuHandle = handle;
                Ready = true;
            }

            public Bounds WorldBounds(float voxelSize)
            {
                float size = VoxelsPerAxis * voxelSize;
                Vector3 min = new Vector3(Coordinate.x, Coordinate.y, Coordinate.z) * size;
                return new Bounds(min + Vector3.one * (size * 0.5f),
                    Vector3.one * (size + SourceStep * voxelSize * 2f));
            }

            public void Dispose()
            {
                Ready = false;
                GpuHandle = -1;
            }
        }

        private struct BuildState
        {
            public bool Active;
            public int3 Coordinate;
            public int Phase;
            public ulong SourceVersion;
            public uint SlotGeneration;
            public uint MaterialPaletteVersion;
            public uint SurfaceCatalogueVersion;
            public ulong SurfaceCatalogueHash;
            public uint CoatingCatalogueVersion;
            public ulong CoatingCatalogueHash;
            public bool GpuEligible;
            public double SnapshotCpuMs;
            public double BuildStartSeconds;
        }

        private readonly Dictionary<int3, Entry> _entries = new();
        private readonly Stack<Entry> _entryPool = new();
        private readonly SurfaceChunkSlotGrid _slotGrid;
        private readonly HashSet<int3> _known = new();
        private bool _clipmapWindowValid;
        private int3 _clipmapCenter;
        private int _clipmapRadius;
        // Camera motion retires only the slabs that left the previous clipmap window. The
        // traversal is resumable, so even a teleport never turns residency cleanup into a scan
        // of every known chunk or a full old-window walk in one frame.
        private const int ClipmapEdgeCandidatesPerPrepare = 32;
        private bool _clipmapEdgeRetirementPending;
        private int3 _clipmapRetirementFromCenter;
        private int3 _clipmapRetirementToCenter;
        private int _clipmapRetirementRadius;
        private int _clipmapRetirementAxis;
        private int _clipmapRetirementDepth;
        private int _clipmapRetirementPlaneCursor;
        // Known-chunk liveness is maintained incrementally. A full HashSet scan in every worker
        // turns residency pressure into O(world-residency) frame work, so each known chunk owns
        // one round-robin queue record instead.
        private readonly Queue<int3> _residencyQueue = new();
        private readonly HashSet<int3> _queuedResidency = new();
        private const int ResidencyChecksPerPrepare = 32;

        // Full-region invalidations (journal overflow, residency publication, atomic world swap)
        // are also incremental. Fine-grained edits continue to use the brick path immediately.
        private readonly Queue<int3> _regionInvalidationQueue = new();
        private readonly HashSet<int3> _queuedRegionInvalidations = new();
        private readonly HashSet<int3> _rescanRegionInvalidations = new();
        private const int RegionInvalidationCandidatesPerPrepare = 64;
        private bool _hasActiveRegionInvalidation;
        private int3 _activeRegionInvalidation;
        private int3 _activeRegionMinChunk;
        private int3 _activeRegionChunkCounts;
        private int _activeRegionCandidateCursor;

        private readonly HashSet<int3> _dirty = new();
        // Dirty work is also kept in a persistent FIFO. The HashSet remains the authoritative
        // membership/coalescing structure; the queue gives build admission bounded incremental
        // traversal instead of rescanning every dirty chunk whenever one workspace becomes free.
        private readonly Queue<int3> _dirtyQueue = new();
        private readonly HashSet<int3> _queuedDirty = new();
        // Missing/stale chunks that are inside the actual camera frustum get a second queue
        // record. This never changes authoritative dirty membership or the global frame budget;
        // it only prevents thousands of valid 360-degree prefetch records from delaying a hole
        // the player can already see. Stale priority records are harmless and self-pruning.
        private readonly Queue<int3> _visibleDirtyQueue = new();
        private readonly HashSet<int3> _queuedVisibleDirty = new();
        private const int BuildSelectionCandidatesPerSlice = 64;
        private const int VisibleBuildSelectionCandidatesPerSlice = 8;
        private readonly Dictionary<int3, ulong> _desiredVersions = new();
        // Chunks whose last completed build produced no geometry, and the source version that
        // proved it. They hold no Entry and no GPU memory, so they cost a dictionary slot
        // rather than a resident chunk, and they stay out of the dirty set until invalidated.
        private readonly Dictionary<int3, ulong> _emptyVersions = new();
        private readonly Dictionary<int3, double> _queuedAtSeconds = new();
        private ulong _versionCounter;
        private readonly List<Entry> _visible = new();
        private readonly Dictionary<int3, uint> _gpuDemandGeometry = new();
        internal bool GpuBuildDemandEnabled { get; set; }
        private readonly Plane[] _frustumPlanes = new Plane[6];

        private BuildState _build;
        private GpuSurfaceExtractionContext _gpuExtraction;
        private readonly bool _gpuCutoverConfigured;
        private readonly long _gpuMirrorBudgetBytes;
        private bool _gpuExtractionUnavailable;
        // A GPU build spans admission and one batched GPU publication. The CPU retains only the
        // stable handle and immutable source generation; it never owns output counts or ranges.
        private bool _gpuStagePending;
        private SurfaceCatalogueView _surfaceCatalogue;
        private CoatingCatalogueView _coatingCatalogue;
        private uint _materialPaletteVersion;
        private ProfileBlock[] _profileBlocks = Array.Empty<ProfileBlock>();
        private ProfileBlock[] _buildProfileBlocks = Array.Empty<ProfileBlock>();
        private readonly Dictionary<int3, ProfileBlock[]> _profileBlocksByChunk = new();
        private IProfileBlockReadSource _profileBlockStore;
        private uint _profileBlockVersion;
        private readonly VoxelTimingWindow _snapshotTiming = new();
        private readonly VoxelTimingWindow _densityTurnaroundTiming = new();
        private readonly VoxelTimingWindow _densityOnlyTiming = new();
        private readonly VoxelTimingWindow _topologyTurnaroundTiming = new();
        private readonly VoxelTimingWindow _topologyCompactTiming = new();
        private readonly VoxelTimingWindow _facetedTurnaroundTiming = new();
        private readonly VoxelTimingWindow _facetedMergeTiming = new();
        private readonly VoxelTimingWindow _profileEmitTiming = new();
        private readonly VoxelTimingWindow _uploadTiming = new();
        private readonly VoxelTimingWindow _queueLatencyTiming = new();
        private readonly VoxelTimingWindow _buildLatencyTiming = new();
        private readonly VoxelTimingWindow _gpuBuildLatencyTiming = new();
        private readonly VoxelTimingWindow _ruleSyncTiming = new();
        private readonly VoxelTimingWindow _residencyPruneTiming = new();
        private readonly VoxelTimingWindow _capacityTiming = new();
        private readonly VoxelTimingWindow _buildSelectionTiming = new();

        public GpuSolidChunkCache(int sourceStep = 1)
            : this(sourceStep, null) { }

        internal GpuSolidChunkCache(int sourceStep, SurfaceChunkSlotGrid slotGrid)
        {
            _slotGrid = slotGrid ?? new SurfaceChunkSlotGrid();
            if (sourceStep < 1 || (sourceStep & (sourceStep - 1)) != 0)
                throw new ArgumentOutOfRangeException(
                    nameof(sourceStep), sourceStep,
                    "Source step must be a positive power of two; chunk coordinates and brick "
                  + "decomposition rely on shifts.");
            SourceStep = sourceStep;
            VoxelsPerAxis = CellsPerAxis * sourceStep;
            BricksPerAxis = VoxelsPerAxis / VoxelReadGrid.BlockEdge;
            // A ring whose stride reaches a whole brick or more reads the mip pyramid instead
            // of caching bricks; its brick cache would grow with the cube of the stride and is
            // never allocated.
            SamplesFromMips = VoxelReadGrid.LevelForStride(sourceStep) >= 0;
            BrickCacheEdge = SamplesFromMips ? 0 : BricksPerAxis + BrickCachePadding * 2;
            BrickCacheCount = BrickCacheEdge * BrickCacheEdge * BrickCacheEdge;
            _surfaceCatalogue = SurfaceCatalogueView.CreateBuiltIns();
            _coatingCatalogue = CoatingCatalogueView.CreateBuiltIns();
            _gpuCutoverConfigured = SupportsGpuSurfaceStep(SourceStep) && !SamplesFromMips;
            _gpuMirrorBudgetBytes = (long)math.max(1, BrickCacheCount)
                                  * GpuBrickBufferLayout.BytesPerMixedBrick;
        }

        /// <summary>Allocate per-worker compute scratch on first demand; remember creation failure.</summary>
        private GpuSurfaceExtractionContext EnsureGpuExtraction()
        {
            if (_gpuExtraction != null) return _gpuExtraction;
            if (!_gpuCutoverConfigured || _gpuExtractionUnavailable
                || !GpuSurfaceMirrorCoordinator.HasPageArena) return null;

            _gpuExtraction = GpuSurfaceExtractionContext.TryCreate(
                CellsPerAxis, Padding, _gpuMirrorBudgetBytes, BrickCacheEdge,
                surfaceArena: null);
            if (_gpuExtraction == null) _gpuExtractionUnavailable = true;
            return _gpuExtraction;
        }

        public int MaxResidentChunks { get; set; } = 4096;

        /// <summary>
        /// Whether this worker may begin a new chunk build this frame. A build already in flight
        /// always runs to completion; this only gates starting another one.
        ///
        /// Extraction runs as Burst jobs, so its cost does not appear in this worker's own timings —
        /// it appears as the main thread waiting on a saturated job pool. Prefetch is 360 degrees and
        /// never runs out of work, so without a ceiling the extractor keeps every job worker busy
        /// forever, including on a view that is already complete.
        /// </summary>
        public bool CanStartNewBuild { get; set; } = true;
        /// <summary>
        /// Whether a worker with no visible demand may consume its build slot on 360-degree
        /// prefetch. The scheduler disables this while any visible chunk is missing so idle
        /// shards cannot fill the shared arena behind the camera while another shard is trying
        /// to close an on-screen hole.
        /// </summary>
        public bool AllowBackgroundBuilds { get; set; } = true;

        /// <summary>True while this worker is part way through building a chunk.</summary>
        public bool HasActiveBuild => _build.Active;
        /// <summary>
        /// Outer edge of this ring's band. Beyond it the next coarser ring takes over.
        /// </summary>
        public float MaxViewDistanceMetres { get; set; } = 96f;

        /// <summary>
        /// Inner edge of this ring's band. A chunk lying entirely inside it belongs to a finer
        /// ring and is neither drawn nor built here, so the rings partition the view rather
        /// than overlapping. Zero for the innermost ring.
        ///
        /// The test is against the chunk's *farthest* corner: a chunk is surrendered only once
        /// all of it is within the finer ring's reach, so a chunk straddling the boundary is
        /// still drawn here and the seam never opens into a gap.
        /// </summary>
        public float MinViewDistanceMetres { get; set; }

        /// <summary>
        /// Switches this ring off entirely: it builds nothing and draws nothing.
        ///
        /// A band is not turned off by collapsing it to zero width. The inner cut tests a chunk's
        /// farthest corner and the outer cut its nearest, so that the bands of adjacent rings
        /// overlap by one chunk instead of gapping while the viewer moves. Set
        /// <see cref="MinViewDistanceMetres"/> equal to <see cref="MaxViewDistanceMetres"/> and
        /// that tolerance becomes the whole band — the ring keeps a one-chunk-thick shell just
        /// inside the cut, meshed at its own step, over ground a finer ring already covers. At
        /// step 8 the shell is 51.2 m of coarse terrain drawn on top of the near field.
        ///
        /// A ring truncated past its inner radius by the streamed world size has to be suspended
        /// rather than collapsed.
        /// </summary>
        public bool RingSuspended { get; set; }

        public int ShardIndex { get; set; }
        public int ShardCount { get; set; } = 1;
        public int ResidentCount => _entries.Count;
        public int KnownCount => _known.Count;

        /// <summary>
        /// Counts every admission and invalidation this shard has recorded.
        ///
        /// Monotonic, so an authoritative voxel change or a newly admitted chunk permanently moves
        /// it. That is what lets the scheduler prove that nothing which could alter this shard's
        /// visible set has happened since the previous frame.
        /// </summary>
        public ulong DemandVersion => _versionCounter;

        /// <summary>
        /// Counts GPU publication, known-empty completion and drawable retirement.
        /// Every change to GPU candidate readiness advances this revision.
        ///
        /// Demand alone cannot see any of that: a chunk evicted to free arena space stops being
        /// ready without anything being admitted or invalidated, so a caller trusting
        /// <see cref="DemandVersion"/> by itself would keep drawing a set that has quietly lost
        /// members and would never mark them dirty again. That is geometry disappearing while the
        /// camera stands still, which is the exact failure the surface coverage assertions exist
        /// to catch.
        /// </summary>
        public ulong ReadySetVersion => _readySetVersion;

        private ulong _readySetVersion;

        /// <summary>Single funnel for entry retirement, so no removal can skip the signal.</summary>
        private void RemoveEntry(int3 coordinate)
        {
            if (_entries.Remove(coordinate)) _readySetVersion++;
        }
        public int SlotCount => _known.Count;
        /// <summary>Number of exact-snapshot brick records reserved by this build workspace.</summary>
        public int SnapshotBrickCapacity => BrickCacheCount;
        public int DirtyCount => _dirty.Count + (_build.Active ? 1 : 0);
        public ulong ActiveSurfaceCatalogueHash => _surfaceCatalogue.CatalogueHash;
        public ulong CompletedBuildCount { get; private set; }
        public ulong StaleBuildCount { get; private set; }
        /// <summary>
        /// Whether this ring can mesh on the GPU. True before the backend is built, because the
        /// context is created on first eligible chunk rather than at construction; it drops to false
        /// only once a creation attempt has proved the device cannot run it.
        /// </summary>
        public bool GpuCutoverAvailable =>
            _gpuExtraction != null || (_gpuCutoverConfigured && !_gpuExtractionUnavailable);
        /// <summary>Whether the GPU backend's buffers are currently allocated by this shard.</summary>
        public bool GpuBackendResident => _gpuExtraction != null;
        public ulong GpuCompletedBuildCount { get; private set; }
        public ulong GpuFallbackBuildCount { get; private set; }
        public ulong GpuUnsupportedBuildCount { get; private set; }
        public ulong GpuContextFailureBuildCount { get; private set; }
        public ulong GpuArenaFullBuildCount { get; private set; }
        public ulong GpuCountFailureBuildCount { get; private set; }
        public ulong GpuWriteFailureBuildCount { get; private set; }
        public ulong GpuStaleRejectedBuildCount { get; private set; }
        public ulong GpuSnapshotlessStageCount { get; private set; }
        public ulong GpuRequestedStageCount => _gpuExtraction?.ChunksRequested ?? 0UL;
        public ulong GpuMirrorReadyStageCount => _gpuExtraction?.ChunksMirrorReady ?? 0UL;
        public ulong GpuCountReadyStageCount => _gpuExtraction?.ChunksCountReady ?? 0UL;
        public ulong GpuWriteCompletedStageCount => _gpuExtraction?.ChunksWriteCompleted ?? 0UL;
        public ulong GpuCopiedStageCount => _gpuExtraction?.ChunksCopied ?? 0UL;
        public ulong GpuEmptyStageCount => _gpuExtraction?.ChunksEmpty ?? 0UL;
        public ulong GpuUnsupportedStageCount => _gpuExtraction?.ChunksUnsupported ?? 0UL;
        public ulong GpuUnsupportedReconstructionStageCount =>
            _gpuExtraction?.ChunksUnsupportedReconstruction ?? 0UL;
        public ulong GpuUnsupportedDecorationStageCount =>
            _gpuExtraction?.ChunksUnsupportedDecoration ?? 0UL;
        public ulong GpuCounterRetryCount => _gpuExtraction?.CountReadbackRetryCount ?? 0UL;
        public bool HasActiveGpuStage => _gpuExtraction?.HasActiveRequest ?? false;
        public int ActiveGpuStagePhase => _gpuExtraction?.ActiveRequestPhase ?? 0;
        internal string GpuCoverageProgress => _gpuExtraction?.CoverageProgress;
        public double ActiveGpuStageAgeMs => _gpuExtraction?.ActiveRequestAgeMs ?? 0.0;
        /// <summary>
        /// Legacy telemetry retained for capture-schema compatibility. The final paged path never
        /// increments it because production performs no completion or counter readback.
        /// </summary>
        public ulong GpuReadbackWaitSlices { get; private set; }
        /// <summary>Build latency for chunks that completed on the GPU, against
        /// <see cref="BuildLatencyTiming"/> for every chunk however it was meshed.</summary>
        public VoxelTimingSummary GpuBuildLatencyTiming => _gpuBuildLatencyTiming.Snapshot();
        public ulong ExactMetadataScheduleCount { get; private set; }
        public ulong ExactMetadataCompleteCount { get; private set; }
        public ulong ExactMetadataRevisionRejectCount { get; private set; }
        public ulong ExactMetadataPinRejectCount { get; private set; }
        // Step-4 false-empty fallback lifecycle diagnostics. These counters do not affect
        // admission or publication; they distinguish policy selection, worker output and final
        // visibility when a coarse exact-owned chunk disappears in production.
        public ulong FeaturePreservingFallbackScheduleCount { get; private set; }
        public ulong FeaturePreservingFallbackCompleteCount { get; private set; }
        public ulong FeaturePreservingFallbackNonEmptyCount { get; private set; }
        public ulong FeaturePreservingFallbackPublishCount { get; private set; }
        // Last visibility pass diagnostics. These counters are reset by BeginVisibilityCollection
        // and never participate in scheduling; they distinguish ring ownership, frustum routing,
        // current-ready and current-empty states when a production LOD disappears.
        public int LastVisibilityKnownCount { get; private set; }
        public int LastVisibilityInBandCount { get; private set; }
        public int LastVisibilityFrustumCount { get; private set; }
        public int LastVisibilityReadyCount { get; private set; }
        public int LastVisibilityEmptyCount { get; private set; }
        public ulong MaterialPaletteInvalidationCount { get; private set; }
        public ulong SurfaceCatalogueInvalidationCount { get; private set; }
        public ulong CoatingCatalogueInvalidationCount { get; private set; }
        public ulong ProfileBlockInvalidationCount { get; private set; }
        public ulong UploadedGeometryBytes { get; private set; }
        public ulong CompletedDecorationClumps { get; private set; }
        public int MissingVisibleCount { get; private set; }
        public ulong CapacityPressureCount { get; private set; }
        private ulong _framePathBlockingCompletionViolations;
        public ulong FramePathBlockingCompletionViolations => _framePathBlockingCompletionViolations;
        public int RunningJobCount => _gpuStagePending ? 1 : 0;
        public int ActiveBuildPhase => _build.Active ? _build.Phase : -1;
        public uint ActiveJobMask => 0;
        public int PendingUploadCount => 0;
        public int PendingUploadBytes => 0;
        public double LastSnapshotMs { get; private set; }
        public double LastTopologyCompactMs { get; private set; }
        public double LastUploadMs { get; private set; }
        public VoxelTimingSummary SnapshotTiming => _snapshotTiming.Snapshot();
        public VoxelTimingSummary DensityTurnaroundTiming => _densityTurnaroundTiming.Snapshot();
        /// <summary>Density job alone, observed when its own handle completes.</summary>
        public VoxelTimingSummary DensityOnlyTiming => _densityOnlyTiming.Snapshot();
        public VoxelTimingSummary TopologyJobTurnaroundTiming => _topologyTurnaroundTiming.Snapshot();
        public VoxelTimingSummary TopologyCompactTiming => _topologyCompactTiming.Snapshot();
        public VoxelTimingSummary FacetedJobTurnaroundTiming => _facetedTurnaroundTiming.Snapshot();
        public VoxelTimingSummary FacetedMergeTiming => _facetedMergeTiming.Snapshot();
        public VoxelTimingSummary ProfileEmitTiming => _profileEmitTiming.Snapshot();
        public VoxelTimingSummary UploadTiming => _uploadTiming.Snapshot();
        public VoxelTimingSummary QueueLatencyTiming => _queueLatencyTiming.Snapshot();
        public VoxelTimingSummary BuildLatencyTiming => _buildLatencyTiming.Snapshot();
        public VoxelTimingSummary RuleSyncTiming => _ruleSyncTiming.Snapshot();
        public VoxelTimingSummary ResidencyPruneTiming => _residencyPruneTiming.Snapshot();
        public VoxelTimingSummary CapacityTiming => _capacityTiming.Snapshot();
        public VoxelTimingSummary BuildSelectionTiming => _buildSelectionTiming.Snapshot();
        public long ResidentGpuBytes
        {
            get
            {
                long total = 0;
                foreach (Entry entry in _entries.Values) total += entry.GpuBytes;
                return total;
            }
        }
        public IReadOnlyList<Entry> Visible => _visible;

        public bool OwnsRenderedChunk(int3 coordinate) =>
            _entries.TryGetValue(coordinate, out Entry entry) && entry.Ready;

        internal bool OwnsReplacementNode(int3 coordinate, Vector3 cameraPosition, float voxelSize) =>
            WithinClipmapWindow(coordinate) && WithinRingBand(ChunkWorldBounds(coordinate, voxelSize), cameraPosition);

        internal bool HasCurrentReplacementNode(int3 coordinate, out bool knownEmpty)
        {
            bool hasDesired = _desiredVersions.TryGetValue(coordinate, out ulong desired);
            knownEmpty = _emptyVersions.TryGetValue(coordinate, out ulong emptyVersion)
                && (!hasDesired || emptyVersion >= desired);
            return knownEmpty || (_entries.TryGetValue(coordinate, out Entry entry) && entry.Ready
                && (!hasDesired || entry.SourceVersion >= desired));
        }

        public int IndexedProfileBlockCount(int3 coordinate) =>
            _profileBlocksByChunk.TryGetValue(coordinate, out ProfileBlock[] blocks)
                ? blocks.Length : 0;

        /// <summary>
        /// Whether a chunk's sampled extent, including its one-sample halo, reaches into a
        /// region. The halo is a full <paramref name="sourceStep"/> wide, so a coarse ring
        /// reaches further past its own bounds than the base ring does.
        /// </summary>
        public static bool ChunkOverlapsRegion(int3 chunk, int3 region,
                                               int voxelsPerAxis = BaseVoxelsPerAxis,
                                               int sourceStep = BaseSourceStep)
        {
            int3 chunkMin = chunk * voxelsPerAxis - Padding * sourceStep;
            int3 chunkMax = (chunk + 1) * voxelsPerAxis + Padding * sourceStep;
            int3 regionMin = region * VoxelGrid.RegionVoxelEdge;
            int3 regionMax = regionMin + VoxelGrid.RegionVoxelEdge;
            return !math.any(chunkMax <= regionMin) && !math.any(chunkMin >= regionMax);
        }

        /// <summary>
        /// Admits chunks discovered from immutable Storage surface summaries. Discovery is not a
        /// mutation signal: once a chunk is known, its build snapshots the entire authoritative
        /// chunk, so later 512-brick publication slices from the same unchanged region must not
        /// advance its source generation and kill in-flight geometry. Real voxel edits continue
        /// through <see cref="InvalidateSurfaceBricks"/> and region invalidation below.
        /// Returns the number of newly admitted chunks.
        /// </summary>
        internal int DiscoverSurfaceBricks(IReadOnlyList<int3> worldBricks)
        {
            if (worldBricks == null) return 0;
            int admitted = 0;

            for (int i = 0; i < worldBricks.Count; i++)
            {
                int3 brick = worldBricks[i];
                int3 baseChunk = new(FloorDiv(brick.x, BricksPerAxis),
                                     FloorDiv(brick.y, BricksPerAxis),
                                     FloorDiv(brick.z, BricksPerAxis));
                int rx = FloorMod(brick.x, BricksPerAxis);
                int ry = FloorMod(brick.y, BricksPerAxis);
                int rz = FloorMod(brick.z, BricksPerAxis);

                int minX = rx == 0 ? -1 : 0;
                int maxX = rx == BricksPerAxis - 1 ? 1 : 0;
                int minY = ry == 0 ? -1 : 0;
                int maxY = ry == BricksPerAxis - 1 ? 1 : 0;
                int minZ = rz == 0 ? -1 : 0;
                int maxZ = rz == BricksPerAxis - 1 ? 1 : 0;

                for (int z = minZ; z <= maxZ; z++)
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    int3 chunk = baseChunk + new int3(x, y, z);
                    if (!OwnsShard(chunk) || _known.Contains(chunk)) continue;
                    if (!TrackKnown(chunk)) continue;

                    // Discovery establishes authoritative source state, not immediate build
                    // demand. Every LOD ring learns the same surface summaries, but only the
                    // ring currently owning this chunk should consume the renderer-wide build
                    // budget. CollectVisibleCoordinate activates in-band demand before worker
                    // admission; retaining only the desired generation here prevents thousands
                    // of finer/coarser off-band chunks from filling the dirty FIFO at startup.
                    _desiredVersions[chunk] = ++_versionCounter;
                    admitted++;
                }
            }
            return admitted;
        }

        /// <summary>
        /// Invalidates chunks touched by an authoritative voxel change. Unlike surface discovery,
        /// this path intentionally advances already-known chunk generations so active/ready
        /// geometry cannot publish stale voxel content.
        /// The one-sample Transvoxel padding can consume a neighbouring chunk's edge,
        /// so face/edge/corner neighbours are dirtied only when the brick lies on a chunk border.
        /// </summary>
        public void InvalidateSurfaceBricks(IReadOnlyList<int3> worldBricks)
        {
            if (worldBricks == null) return;

            for (int i = 0; i < worldBricks.Count; i++)
            {
                int3 brick = worldBricks[i];
                int3 baseChunk = new(FloorDiv(brick.x, BricksPerAxis),
                                     FloorDiv(brick.y, BricksPerAxis),
                                     FloorDiv(brick.z, BricksPerAxis));
                int rx = FloorMod(brick.x, BricksPerAxis);
                int ry = FloorMod(brick.y, BricksPerAxis);
                int rz = FloorMod(brick.z, BricksPerAxis);

                int minX = rx == 0 ? -1 : 0;
                int maxX = rx == BricksPerAxis - 1 ? 1 : 0;
                int minY = ry == 0 ? -1 : 0;
                int maxY = ry == BricksPerAxis - 1 ? 1 : 0;
                int minZ = rz == 0 ? -1 : 0;
                int maxZ = rz == BricksPerAxis - 1 ? 1 : 0;

                for (int z = minZ; z <= maxZ; z++)
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    int3 chunk = baseChunk + new int3(x, y, z);
                    if (!OwnsShard(chunk) || !TrackKnown(chunk)) continue;
                    Invalidate(chunk);
                }
            }
        }

        /// <summary>
        /// Consumes scheduler-local regions derived from the versioned change journal. Existing
        /// chunks in or adjacent to an edited region are rebuilt; the old ready mesh stays alive
        /// until the replacement is uploaded.
        /// </summary>
        public void InvalidateDirtyRegions(HashSet<int3> dirtyRegions)
        {
            if (dirtyRegions != null)
            {
                foreach (int3 region in dirtyRegions)
                {
                    if (_hasActiveRegionInvalidation && region.Equals(_activeRegionInvalidation))
                    {
                        _rescanRegionInvalidations.Add(region);
                        continue;
                    }
                    if (_queuedRegionInvalidations.Add(region))
                        _regionInvalidationQueue.Enqueue(region);
                }
            }

            StepRegionInvalidation();
        }

        private void StepRegionInvalidation()
        {
            int remaining = RegionInvalidationCandidatesPerPrepare;
            while (remaining > 0)
            {
                if (!_hasActiveRegionInvalidation)
                {
                    if (_regionInvalidationQueue.Count == 0) return;
                    _activeRegionInvalidation = _regionInvalidationQueue.Dequeue();
                    _hasActiveRegionInvalidation = true;
                    _activeRegionCandidateCursor = 0;

                    int halo = Padding * SourceStep;
                    int3 regionMin = _activeRegionInvalidation * VoxelGrid.RegionVoxelEdge;
                    int3 regionMax = regionMin + VoxelGrid.RegionVoxelEdge;
                    _activeRegionMinChunk = new int3(
                        FloorDiv(regionMin.x - halo, VoxelsPerAxis),
                        FloorDiv(regionMin.y - halo, VoxelsPerAxis),
                        FloorDiv(regionMin.z - halo, VoxelsPerAxis));
                    int3 maxChunk = new int3(
                        FloorDiv(regionMax.x + halo - 1, VoxelsPerAxis),
                        FloorDiv(regionMax.y + halo - 1, VoxelsPerAxis),
                        FloorDiv(regionMax.z + halo - 1, VoxelsPerAxis));
                    _activeRegionChunkCounts = maxChunk - _activeRegionMinChunk + 1;
                }

                int total = _activeRegionChunkCounts.x
                          * _activeRegionChunkCounts.y
                          * _activeRegionChunkCounts.z;
                while (remaining > 0 && _activeRegionCandidateCursor < total)
                {
                    int linear = _activeRegionCandidateCursor++;
                    int x = linear % _activeRegionChunkCounts.x;
                    int y = (linear / _activeRegionChunkCounts.x) % _activeRegionChunkCounts.y;
                    int z = linear / (_activeRegionChunkCounts.x * _activeRegionChunkCounts.y);
                    int3 chunk = _activeRegionMinChunk + new int3(x, y, z);
                    remaining--;
                    if (!OwnsShard(chunk) || !_known.Contains(chunk)) continue;
                    if (ChunkOverlapsRegion(chunk, _activeRegionInvalidation,
                                            VoxelsPerAxis, SourceStep))
                        Invalidate(chunk);
                }

                if (_activeRegionCandidateCursor < total) return;

                int3 completed = _activeRegionInvalidation;
                bool rescan = _rescanRegionInvalidations.Remove(completed);
                _queuedRegionInvalidations.Remove(completed);
                _hasActiveRegionInvalidation = false;
                _activeRegionCandidateCursor = 0;
                if (rescan && _queuedRegionInvalidations.Add(completed))
                    _regionInvalidationQueue.Enqueue(completed);
            }
        }

        public void Prepare(IRegionReadSource source, in MaterialPaletteView palette,
                            in SurfaceCatalogueView surfaceCatalogue,
                            in CoatingCatalogueView coatingCatalogue,
                            IProfileBlockReadSource profileBlocks, Camera camera,
                            float voxelSize, int frame, double budgetMs = 0.20)
        {
            using var prepareScope = s_PrepareMarker.Auto();
            double sectionStart = Time.realtimeSinceStartupAsDouble;
            SetSurfaceCatalogue(in surfaceCatalogue);
            SetCoatingCatalogue(in coatingCatalogue);
            SetMaterialPaletteVersion(palette.Version);
            SetProfileBlocks(profileBlocks);
            _ruleSyncTiming.Add(ElapsedMs(sectionStart));
            sectionStart = Time.realtimeSinceStartupAsDouble;
            StepClipmapEdgeRetirement();
            StepResidencyPrune(source);
            _residencyPruneTiming.Add(ElapsedMs(sectionStart));
            sectionStart = Time.realtimeSinceStartupAsDouble;
            EnforceCapacity(camera, voxelSize);
            _capacityTiming.Add(ElapsedMs(sectionStart));
            if (camera == null || _dirty.Count == 0 && !_build.Active) return;

            double deadline = Time.realtimeSinceStartupAsDouble + math.max(0.0, budgetMs) * 0.001;
            do
            {
                if (!_build.Active)
                {
                    if (!CanStartNewBuild) break;
                    double selectionStart = Time.realtimeSinceStartupAsDouble;
                    bool selected = BeginNearestBuild(camera, voxelSize, deadline);
                    _buildSelectionTiming.Add(ElapsedMs(selectionStart));
                    if (!selected) break;
                }
                if (!_gpuStagePending)
                {
                    if (!TryStageGpuBuild(in palette, voxelSize, camera.transform.position)) break;
                    _build.Phase = 9;
                }
                if (!_gpuExtraction.TryTakePagedBatch(out int handle, out bool failed)) break;
                if (failed)
                {
                    int3 retry = _build.Coordinate;
                    ResetCompletedBuild();
                    MarkDirty(retry);
                    continue;
                }
                FinishPagedGpuBuild(handle, frame);
            }
            while (Time.realtimeSinceStartupAsDouble < deadline);
        }

        private bool TryStageGpuBuild(in MaterialPaletteView palette, float voxelSize,
                                      Vector3 cameraPosition)
        {
            double start = Time.realtimeSinceStartupAsDouble;
            GpuSurfaceExtractionContext gpu = EnsureGpuExtraction();
            if (gpu == null)
            {
                // Keep authoritative demand and the previous publication intact. Unsupported
                // devices cannot silently change rendering backend or publish an empty result.
                if (_gpuExtractionUnavailable)
                    throw new InvalidOperationException("The solid GPU renderer could not create its extraction context.");
                return false;
            }
            _build.MaterialPaletteVersion = palette.Version;
            _buildProfileBlocks = _profileBlocksByChunk.TryGetValue(_build.Coordinate, out var blocks)
                ? blocks : Array.Empty<ProfileBlock>();
            gpu.SetCatalogues(_surfaceCatalogue, _coatingCatalogue, palette);
            int3 origin = _build.Coordinate * VoxelsPerAxis;
            var request = new GpuChunkExtraction(origin,
                (origin >> VoxelReadGrid.BlockEdgeLog2) - BrickCachePadding,
                SourceStep, voxelSize,
                transitionFaceMask: BuildGpuTransitionFaceMask(_build.Coordinate, voxelSize, cameraPosition),
                profileBlocks: _buildProfileBlocks);
            if (gpu.TryBeginStage(default, default, default, default, request, _build.SourceVersion)
                != GpuStageOutcome.Staged) return false;
            _build.GpuEligible = true;
            _gpuStagePending = true;
            GpuSnapshotlessStageCount++;
            AccumulateSnapshotSlice(start, completed: true);
            return true;
        }

        private readonly SurfaceVisibilityGeometryCache _visibilityGeometry = new();
        private bool _collectGpuCandidates;

        public void BeginVisibilityCollection()
        {
            _visibilityGeometry.Disable();
            _collectGpuCandidates = false;
            _visible.Clear();
            MissingVisibleCount = 0;
            LastVisibilityKnownCount = 0;
            LastVisibilityInBandCount = 0;
            LastVisibilityFrustumCount = 0;
            LastVisibilityReadyCount = 0;
            LastVisibilityEmptyCount = 0;
        }

        internal void BeginVisibilityCollection(Plane[] planes, Vector3 position, float voxelSize, bool gpuCandidates = false)
        {
            int previousMissing = MissingVisibleCount;
            int previousInBand = LastVisibilityInBandCount, previousFrustum = LastVisibilityFrustumCount;
            BeginVisibilityCollection();
            _collectGpuCandidates = gpuCandidates;
            if (gpuCandidates)
            {
                // Metadata rebuilds do not constitute a newer GPU visibility observation.
                MissingVisibleCount = previousMissing;
                LastVisibilityInBandCount = previousInBand;
                LastVisibilityFrustumCount = previousFrustum;
            }
            if (!_collectGpuCandidates)
                _visibilityGeometry.Prepare(planes, position, voxelSize,
                    MinViewDistanceMetres, MaxViewDistanceMetres, RingSuspended);
        }

        internal void BeginGpuDemandFeedback()
        {
            _gpuDemandGeometry.Clear();
            MissingVisibleCount = 0;
            LastVisibilityInBandCount = 0;
            LastVisibilityFrustumCount = 0;
        }

        // The GPU supplies presentation urgency only. Always consult current host generation
        // and publication state: a delayed classification can neither complete an edit nor
        // reactivate a removed coordinate or the generation already being built.
        internal void ApplyGpuDemand(int3 coordinate, uint geometry, int frame)
        {
            if (!_known.Contains(coordinate)) return;
            if (RingSuspended) geometry = 0;
            _gpuDemandGeometry[coordinate] = geometry;
            bool ready = _entries.TryGetValue(coordinate, out Entry entry) && entry.Ready;
            if (ready) entry.GpuDemandInBand = (geometry & 1) != 0;
            if ((geometry & 1) == 0)
            {
                if (_dirty.Contains(coordinate)) ParkDirty(coordinate);
                return;
            }
            LastVisibilityInBandCount++;
            if (ready) entry.LastUsedFrame = frame;
            bool hasDesired = _desiredVersions.TryGetValue(coordinate, out ulong desired);
            bool currentReady = ready && (!hasDesired || entry.SourceVersion >= desired);
            bool currentEmpty = _emptyVersions.TryGetValue(coordinate, out ulong empty)
                && (!hasDesired || empty >= desired);
            if (currentReady || currentEmpty) return;
            bool building = CurrentBuildCoversDesiredGeneration(coordinate, hasDesired, desired);
            if (!building) MarkDirty(coordinate);
            if ((geometry & 2) == 0) return;
            LastVisibilityFrustumCount++;
            if (!building) PromoteVisibleDirty(coordinate);
            if (!ready) MissingVisibleCount++;
        }

        internal void RefreshGpuResidentAges(int frame)
        {
            // Reuse the GPU's classification while its query is unchanged. This is only a
            // host lifetime stamp; no bounds, distance, frustum or dictionary query is repeated.
            foreach (var entry in _visible)
                if (entry.GpuDemandInBand) entry.LastUsedFrame = frame;
        }

        public readonly struct CoordinateVisibility
        {
            public readonly bool Drawable;
            public readonly bool CurrentViewComplete;
            public readonly bool GpuCandidate;

            internal CoordinateVisibility(bool drawable, bool currentViewComplete, bool gpuCandidate = false)
            {
                Drawable = drawable;
                CurrentViewComplete = currentViewComplete;
                GpuCandidate = gpuCandidate;
            }
        }

        /// <summary>
        /// Evaluates one clipmap coordinate already routed to this shard. Visibility traversal is
        /// driven by the bounded camera-centred ring grid, never by the lifetime size of _known.
        /// </summary>
        public CoordinateVisibility CollectVisibleCoordinate(int3 coordinate, Plane[] frustumPlanes,
                                             Vector3 cameraPosition, float voxelSize, int frame)
        {
            if (!_known.Contains(coordinate)) return default;
            LastVisibilityKnownCount++;

            bool hasDesired = _desiredVersions.TryGetValue(coordinate, out ulong desired);
            bool currentGenerationInFlight = CurrentBuildCoversDesiredGeneration(
                coordinate, hasDesired, desired);
            bool ready = _entries.TryGetValue(coordinate, out Entry entry) && entry.Ready;
            bool currentReady = ready && (!hasDesired || entry.SourceVersion >= desired);
            bool currentEmpty = _emptyVersions.TryGetValue(coordinate, out ulong emptyVersion)
                             && (!hasDesired || emptyVersion >= desired);

            if (_collectGpuCandidates)
            {
                // Camera-independent transport only. GPU feedback owns band/frustum demand
                // and resident ages, including missing/stale and out-of-band candidates.
                if (ready) _visible.Add(entry);
                return new CoordinateVisibility(ready, currentReady || currentEmpty, true);
            }

            if (!_visibilityGeometry.TryGet(coordinate, out byte geometry))
            {
                Bounds bounds = ChunkWorldBounds(coordinate, voxelSize);
                geometry = !WithinRingBand(bounds, cameraPosition) ? (byte)0
                    : GeometryUtility.TestPlanesAABB(frustumPlanes, bounds) ? (byte)2 : (byte)1;
                _visibilityGeometry.Store(coordinate, geometry);
            }
            if (geometry == 0)
            {
                if (_dirty.Contains(coordinate)) ParkDirty(coordinate);
                return default;
            }
            LastVisibilityInBandCount++;
            if (!currentReady && !currentEmpty && !currentGenerationInFlight)
                MarkDirty(coordinate);

            if (geometry != 2) return new CoordinateVisibility(false, true);
            LastVisibilityFrustumCount++;
            if (currentReady) LastVisibilityReadyCount++;
            if (currentEmpty) LastVisibilityEmptyCount++;

            // Background prefetch above remains intentionally 360 degrees. Once a chunk is in
            // the actual camera frustum, however, promote its still-needed generation so build
            // selection cannot make a visible hole wait behind the entire prefetch shell.
            if (!currentReady && !currentEmpty && !currentGenerationInFlight)
                PromoteVisibleDirty(coordinate);

            if (ready)
            {
                // Keep the previous mesh drawable while a newer authoritative generation builds.
                // CurrentBuildCoversDesiredGeneration above prevents visibility from recreating a
                // duplicate dirty record for the exact generation already in flight.
                if (!entry.IsGpuPaged && entry.IndexCount == 0)
                    return new CoordinateVisibility(false, currentReady || currentEmpty);
                entry.LastUsedFrame = frame;
                _visible.Add(entry);
                return new CoordinateVisibility(true, currentReady || currentEmpty);
            }

            // A current known-empty result is complete, not a visual hole. Any other in-band
            // visible coordinate remains missing until its authoritative generation publishes.
            if (currentEmpty) return new CoordinateVisibility(false, true);

            MissingVisibleCount++;
            return default;
        }

        /// <summary>
        /// Compatibility entry point for focused tests/tools. Production scheduling performs one
        /// ring traversal in VoxelSurfaceScheduler and routes coordinates directly to shards.
        /// This fallback is still bounded by the ring's configured view distance.
        /// </summary>
        public IReadOnlyList<Entry> CollectVisible(Camera camera, float voxelSize, int frame)
        {
            BeginVisibilityCollection();
            if (camera == null) return _visible;

            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
            Vector3 cameraPosition = camera.transform.position;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            int radius = Mathf.CeilToInt(MaxViewDistanceMetres / chunkMetres) + 1;
            int3 centre = new(
                Mathf.FloorToInt(cameraPosition.x / chunkMetres),
                Mathf.FloorToInt(cameraPosition.y / chunkMetres),
                Mathf.FloorToInt(cameraPosition.z / chunkMetres));

            for (int z = -radius; z <= radius; z++)
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                int3 coordinate = centre + new int3(x, y, z);
                if (!OwnsShard(coordinate)) continue;
                CollectVisibleCoordinate(coordinate, _frustumPlanes, cameraPosition,
                                         voxelSize, frame);
            }
            return _visible;
        }

        private bool BeginNearestBuild(Camera camera, float voxelSize,
                                       double deadlineSeconds)
        {
            if (_dirty.Count == 0
                || _dirtyQueue.Count == 0 && _visibleDirtyQueue.Count == 0
                || Time.realtimeSinceStartupAsDouble >= deadlineSeconds)
                return false;

            int3 best = default;
            bool hasBest = false;
            float bestScore = float.PositiveInfinity;
            if (GpuBuildDemandEnabled)
            {
                // Admission consumes the latest GPU classification, not another CPU bounds
                // scan. Visible FIFO retains priority; the bounded background queue compares
                // GPU distance ranks to preserve near-first 360-degree prefetch.
                hasBest = TryTakeGpuDemand(_visibleDirtyQueue, _queuedVisibleDirty,
                    VisibleBuildSelectionCandidatesPerSlice, true, out best);
                if (!hasBest && AllowBackgroundBuilds)
                    hasBest = TryTakeGpuDemand(_dirtyQueue, _queuedDirty,
                        BuildSelectionCandidatesPerSlice, false, out best);
            }
            else
            {
                float chunkMetres = VoxelsPerAxis * voxelSize;
                Vector3 cameraWorldPosition = camera.transform.position;
                GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

                // First sample only demand that was actually visible when collected. Camera motion can
                // stale that classification, so recheck both ring ownership and the current frustum.
                // A priority record that moved offscreen simply falls back to its existing background
                // FIFO record; no authoritative work is lost.
                int visibleCandidates = math.min(
                    VisibleBuildSelectionCandidatesPerSlice, _visibleDirtyQueue.Count);
                for (int i = 0; i < visibleCandidates; i++)
                {
                    int3 candidate = _visibleDirtyQueue.Dequeue();
                    _queuedVisibleDirty.Remove(candidate);
                    if (!_dirty.Contains(candidate)) continue;

                    Bounds bounds = ChunkWorldBounds(candidate, voxelSize);
                    if (!WithinRingBand(bounds, cameraWorldPosition))
                    {
                        ParkDirty(candidate);
                        continue;
                    }
                    if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                        continue;

                    // Visibility already established urgency. Ranking dozens of visible holes by
                    // distance cost the entire renderer-wide build budget in production (0.52 ms
                    // selection p95 against a 0.50 ms budget). FIFO is fair, deterministic and lets
                    // the selected workspace spend this frame advancing geometry instead.
                    best = candidate;
                    hasBest = true;
                    break;
                }

                // No currently visible hole was ready for this workspace. Preserve the original
                // bounded background selection so 360-degree prefetch still converges opportunistically.
                if (!hasBest && AllowBackgroundBuilds)
                {
                    int candidates = math.min(BuildSelectionCandidatesPerSlice, _dirtyQueue.Count);
                    for (int i = 0; i < candidates; i++)
                    {
                        int3 candidate = _dirtyQueue.Dequeue();
                        _queuedDirty.Remove(candidate);
                        if (!_dirty.Contains(candidate)) continue; // stale queue record

                        Bounds bounds = ChunkWorldBounds(candidate, voxelSize);
                        if (!WithinRingBand(bounds, cameraWorldPosition))
                        {
                            ParkDirty(candidate);
                            continue;
                        }

                        Vector3 centre = (new Vector3(candidate.x, candidate.y, candidate.z)
                                        + Vector3.one * 0.5f) * chunkMetres;
                        float distance = (centre - cameraWorldPosition).sqrMagnitude;
                        float score = GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds)
                            ? distance : distance + 1_000_000_000f;
                        if (!hasBest || score < bestScore)
                        {
                            if (hasBest) RequeueDirty(best);
                            bestScore = score;
                            best = candidate;
                            hasBest = true;
                        }
                        else
                        {
                            RequeueDirty(candidate);
                        }

                        // Score checks are cheap, but a destruction burst can enqueue thousands. The
                        // frame contract wins over exact global nearest ordering; later slices continue
                        // from the queue tail and converge without a scan spike.
                        if (Time.realtimeSinceStartupAsDouble >= deadlineSeconds) break;
                    }
                }

            }

            if (!hasBest) return false;
            // Priority selection leaves the background queue's physical record in place. Clear its
            // membership bit before admission so a failed slot acquisition can be reactivated on a
            // later visibility pass; the old physical record will self-prune as stale.
            _queuedDirty.Remove(best);
            _queuedVisibleDirty.Remove(best);
            if (!_slotGrid.TryGet(best, out SurfaceChunkSlot buildSlot)
                && !_slotGrid.TryAcquire(best, out buildSlot))
            {
                _dirty.Remove(best);
                return false;
            }
            _dirty.Remove(best);
            _build = new BuildState
            {
                Active = true, Coordinate = best, Phase = 0,
                SourceVersion = _desiredVersions.TryGetValue(best, out ulong version) ? version : 0,
                SlotGeneration = buildSlot.Generation,
                SurfaceCatalogueVersion = _surfaceCatalogue.Version,
                SurfaceCatalogueHash = _surfaceCatalogue.CatalogueHash,
                CoatingCatalogueVersion = _coatingCatalogue.Version,
                CoatingCatalogueHash = _coatingCatalogue.CatalogueHash,
                BuildStartSeconds = Time.realtimeSinceStartupAsDouble
            };
            if (_queuedAtSeconds.TryGetValue(best, out double queuedAt))
                _queueLatencyTiming.Add(ElapsedMs(queuedAt));
            return true;
        }

        private bool TryTakeGpuDemand(Queue<int3> queue, HashSet<int3> queued, int limit,
                                      bool visibleOnly, out int3 selected)
        {
            int count = math.min(limit, queue.Count);
            bool found = false;
            uint bestScore = uint.MaxValue;
            selected = default;
            for (int i = 0; i < count; i++)
            {
                int3 coordinate = queue.Dequeue();
                queued.Remove(coordinate);
                if (!_dirty.Contains(coordinate)) continue;
                if (RingSuspended || !_gpuDemandGeometry.TryGetValue(coordinate, out uint geometry)
                    || (geometry & 1) == 0)
                { ParkDirty(coordinate); continue; }
                if (visibleOnly)
                {
                    if ((geometry & 2) == 0) continue;
                    selected = coordinate;
                    return true;
                }
                uint score = (geometry >> 3) + ((geometry & 2) == 0 ? 0x40000000u : 0u);
                if (!found || score < bestScore)
                {
                    if (found) RequeueDirty(selected);
                    selected = coordinate; bestScore = score; found = true;
                }
                else RequeueDirty(coordinate);
            }
            return found;
        }

        private void Invalidate(int3 chunk)
        {
            _emptyVersions.Remove(chunk);
            _desiredVersions[chunk] = ++_versionCounter;
            MarkDirty(chunk);
        }

        private void MarkDirty(int3 chunk)
        {
            // Every active dirty record needs a durable desired generation. Most callers arrive
            // through Invalidate, but arena/capacity eviction can request a rebuild directly.
            // Keeping that generation lets parked work be reactivated safely when it becomes
            // visible again.
            if (!_desiredVersions.ContainsKey(chunk))
                _desiredVersions[chunk] = ++_versionCounter;
            if (_dirty.Add(chunk))
                _queuedAtSeconds[chunk] = Time.realtimeSinceStartupAsDouble;
            RequeueDirty(chunk);
            if (GpuBuildDemandEnabled && _gpuDemandGeometry.TryGetValue(chunk, out uint geometry)
                && (geometry & 3) == 3) RequeueVisibleDirty(chunk);
        }

        private void RequeueDirty(int3 chunk)
        {
            if (!_dirty.Contains(chunk) || !_queuedDirty.Add(chunk)) return;
            _dirtyQueue.Enqueue(chunk);
        }

        private void PromoteVisibleDirty(int3 chunk)
        {
            if (!_dirty.Contains(chunk)) MarkDirty(chunk);
            RequeueVisibleDirty(chunk);
        }

        private void RequeueVisibleDirty(int3 chunk)
        {
            if (!_dirty.Contains(chunk) || !_queuedVisibleDirty.Add(chunk)) return;
            _visibleDirtyQueue.Enqueue(chunk);
        }

        private void ParkDirty(int3 chunk)
        {
            _dirty.Remove(chunk);
            _queuedDirty.Remove(chunk);
            _queuedVisibleDirty.Remove(chunk);
            _queuedAtSeconds.Remove(chunk);
            // Intentionally retain _desiredVersions: discovery/edit state remains authoritative,
            // and CollectVisibleCoordinate will reactivate it if this chunk enters the ring.
        }

        private bool CurrentBuildCoversDesiredGeneration(int3 chunk, bool hasDesired, ulong desired)
        {
            return _build.Active && _build.Coordinate.Equals(chunk)
                && (!hasDesired || _build.SourceVersion >= desired);
        }

        public static int ShardForChunk(int3 chunk, int shardCount)
        {
            int count = math.max(1, shardCount);
            return (int)(math.hash(chunk) % (uint)count);
        }

        private bool OwnsShard(int3 chunk) =>
            ShardForChunk(chunk, ShardCount) == math.clamp(ShardIndex, 0, math.max(1, ShardCount) - 1);

        private void SetSurfaceCatalogue(in SurfaceCatalogueView catalogue)
        {
            ulong hash = catalogue.CatalogueHash != 0
                ? catalogue.CatalogueHash : catalogue.ComputeHash();
            if (_surfaceCatalogue.Version == catalogue.Version
                && _surfaceCatalogue.CatalogueHash == hash) return;

            _surfaceCatalogue = catalogue;
            if (_surfaceCatalogue.CatalogueHash == 0)
                _surfaceCatalogue.Seal(_surfaceCatalogue.Version, hash);

            // Catalogue data participates in geometry. Existing meshes may remain visible while
            // every known chunk queues a replacement built from the new immutable snapshot.
            SurfaceCatalogueInvalidationCount++;
            foreach (int3 chunk in _known) Invalidate(chunk);
        }

        private void SetMaterialPaletteVersion(uint version)
        {
            if (_materialPaletteVersion == version) return;
            _materialPaletteVersion = version;
            MaterialPaletteInvalidationCount++;
            foreach (int3 chunk in _known) Invalidate(chunk);
        }

        private void SetCoatingCatalogue(in CoatingCatalogueView catalogue)
        {
            ulong hash = catalogue.CatalogueHash != 0
                ? catalogue.CatalogueHash : catalogue.ComputeHash();
            if (_coatingCatalogue.Version == catalogue.Version
                && _coatingCatalogue.CatalogueHash == hash) return;

            _coatingCatalogue = catalogue;
            if (_coatingCatalogue.CatalogueHash == 0)
                _coatingCatalogue.Seal(_coatingCatalogue.Version, hash);
            CoatingCatalogueInvalidationCount++;
            foreach (int3 chunk in _known) Invalidate(chunk);
        }

        private void SetProfileBlocks(IProfileBlockReadSource store)
        {
            uint version = store?.Version ?? 0;
            if (ReferenceEquals(_profileBlockStore, store) && _profileBlockVersion == version)
                return;
            _profileBlockStore = store;
            _profileBlockVersion = version;
            _profileBlocks = store?.Snapshot() ?? Array.Empty<ProfileBlock>();
            RebuildProfileBlockIndex();
            ProfileBlockInvalidationCount++;
            foreach (int3 chunk in _known) Invalidate(chunk);
        }

        private void RebuildProfileBlockIndex()
        {
            _profileBlocksByChunk.Clear();
            var staging = new Dictionary<int3, List<ProfileBlock>>();
            for (int i = 0; i < _profileBlocks.Length; i++)
            {
                ProfileBlock block = _profileBlocks[i];
                block.Bounds(out int3 min, out int3 max);
                int3 first = new(FloorDiv(min.x, VoxelsPerAxis),
                                 FloorDiv(min.y, VoxelsPerAxis),
                                 FloorDiv(min.z, VoxelsPerAxis));
                int3 last = new(FloorDiv(max.x, VoxelsPerAxis),
                                FloorDiv(max.y, VoxelsPerAxis),
                                FloorDiv(max.z, VoxelsPerAxis));
                for (int z = first.z; z <= last.z; z++)
                for (int y = first.y; y <= last.y; y++)
                for (int x = first.x; x <= last.x; x++)
                {
                    int3 chunk = new(x, y, z);
                    if (!staging.TryGetValue(chunk, out List<ProfileBlock> blocks))
                        staging.Add(chunk, blocks = new List<ProfileBlock>());
                    blocks.Add(block);
                }
            }
            foreach (var pair in staging) _profileBlocksByChunk.Add(pair.Key, pair.Value.ToArray());
        }

        private const int SnapshotMipSamplesPerDeadlineCheck = 64;

        /// <summary>
        /// Advances the authoritative-to-immutable snapshot boundary without ever walking a full
        /// chunk in one frame. The snapshot lives entirely in this workspace's persistent native
        /// buffers; borrowed Storage views are reacquired inside each slice and never survive the
        /// call. A later journal invalidation rejects the partial generation before publication.
        /// </summary>
        private void AccumulateSnapshotSlice(double sliceStart, bool completed)
        {
            _build.SnapshotCpuMs += ElapsedMs(sliceStart);
            if (!completed) return;
            LastSnapshotMs = _build.SnapshotCpuMs;
            _snapshotTiming.Add(LastSnapshotMs);
        }

        internal static bool RetainedProfileOwnsTriangle(
            in ProfileBlock block, float3 a, float3 b, float3 c, byte material)
        {
            if (material != block.Material) return false;

            int axisA = (block.Axis + 1) % 3;
            int axisB = (block.Axis + 2) % 3;
            float start = math.atan2(block.StartDirection.y, block.StartDirection.x);
            float finish = math.atan2(block.EndDirection.y, block.EndDirection.x);
            if (finish <= start) finish += math.PI * 2f;
            float inner = block.InnerRadiusQ4 * (1f / 16f);
            float outer = block.OuterRadiusQ4 * (1f / 16f);
            float front = math.min(block.FrontQ4, block.BackQ4) * (1f / 16f);
            float back = math.max(block.FrontQ4, block.BackQ4) * (1f / 16f);
            float3 centre = block.Centre;
            int profileAxis = block.Axis;
            const float topologyPadding = 0.55f;
            float3 point = (a + b + c) * (1f / 3f);
            float da = point[axisA] - centre[axisA];
            float db = point[axisB] - centre[axisB];
            float radius = math.sqrt(da * da + db * db);
            if (radius < inner - topologyPadding || radius > outer + topologyPadding
                || point[profileAxis] < front - topologyPadding
                || point[profileAxis] > back + topologyPadding)
                return false;

            float angle = math.atan2(db, da);
            while (angle < start) angle += math.PI * 2f;
            // The primitive owns its raw wedge. JointHalfWidthQ4 only insets the retained faces;
            // their radial sides present the joint and do not grant duplicate topology ownership.
            return angle >= start && angle <= finish;
        }

        private Entry AcquireEntry(int3 coordinate)
        {
            if (_entryPool.Count == 0)
                return new Entry(coordinate, VoxelsPerAxis, SourceStep);

            Entry entry = _entryPool.Pop();
            entry.Reinitialize(coordinate);
            return entry;
        }

        private void RecycleEntry(Entry entry)
        {
            if (entry == null) return;
            // Dispose is the one place Ready goes false, so it is a drawable-set change even when
            // the dictionary removal happened somewhere else.
            _readySetVersion++;
            if (entry.IsGpuPaged)
                GpuSurfaceMirrorCoordinator.ReleaseChunkHandle(
                    entry.Coordinate * entry.VoxelsPerAxis,
                    entry.SourceStep, entry.SourceVersion);
            entry.Dispose();
            _entryPool.Push(entry);
        }

        private void FinishPagedGpuBuild(int handle, int frame)
        {
            if (!BuildOwnsCurrentSlot()
                || _build.MaterialPaletteVersion != _materialPaletteVersion
                || _build.SurfaceCatalogueVersion != _surfaceCatalogue.Version
                || _build.SurfaceCatalogueHash != _surfaceCatalogue.CatalogueHash
                || _build.CoatingCatalogueVersion != _coatingCatalogue.Version
                || _build.CoatingCatalogueHash != _coatingCatalogue.CatalogueHash
                || _desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                   && desired > _build.SourceVersion)
            {
                RejectPendingOrCompletedBuild(stale: true);
                return;
            }

            _gpuExtraction.ApprovePagedCandidate(handle, frame);
            _gpuExtraction.Release();
            _gpuStagePending = false;
            _emptyVersions.Remove(_build.Coordinate);
            if (!_entries.TryGetValue(_build.Coordinate, out Entry entry))
            {
                entry = AcquireEntry(_build.Coordinate);
                _entries.Add(_build.Coordinate, entry);
            }
            entry.PublishGpuPaged(handle);
            _readySetVersion++;
            entry.LastUsedFrame = frame;
            entry.SourceVersion = _build.SourceVersion;
            entry.MaterialPaletteVersion = _build.MaterialPaletteVersion;
            entry.SurfaceCatalogueVersion = _build.SurfaceCatalogueVersion;
            entry.SurfaceCatalogueHash = _build.SurfaceCatalogueHash;
            entry.CoatingCatalogueVersion = _build.CoatingCatalogueVersion;
            entry.CoatingCatalogueHash = _build.CoatingCatalogueHash;
            CompletedBuildCount++;
            GpuCompletedBuildCount++;
            _buildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));
            _gpuBuildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));
            _desiredVersions.Remove(_build.Coordinate);
            _queuedAtSeconds.Remove(_build.Coordinate);
            ResetCompletedBuild();
        }

        private void RejectPendingOrCompletedBuild(bool stale)
        {
            bool rejectedGpu = _gpuStagePending || _build.GpuEligible;
            if (_entries.TryGetValue(_build.Coordinate, out Entry entry))
            {
                if (!entry.Ready)
                {
                    RecycleEntry(entry);
                    RemoveEntry(_build.Coordinate);
                }
            }
            if (stale)
            {
                StaleBuildCount++;
                if (rejectedGpu) GpuStaleRejectedBuildCount++;
            }
            int3 retry = _build.Coordinate;
            ResetCompletedBuild();
            if (GpuBuildDemandEnabled && _known.Contains(retry)) MarkDirty(retry);
        }

        private void ReleasePendingGpuBuild()
        {
            if (_gpuStagePending)
            {
                _gpuExtraction?.Release();
                _gpuStagePending = false;
            }
        }

        private void ResetCompletedBuild()
        {
            ReleasePendingGpuBuild();
            _build = default;
        }

        public void SetClipmapWindow(int3 centre, int radius)
        {
            int nextRadius = math.max(0, radius);
            if (_clipmapWindowValid && _clipmapRadius == nextRadius
                && math.any(_clipmapCenter != centre))
                ScheduleClipmapEdgeRetirement(_clipmapCenter, centre, nextRadius);

            _clipmapCenter = centre;
            _clipmapRadius = nextRadius;
            _clipmapWindowValid = true;
            _slotGrid.UpdateWindow(centre, nextRadius);
        }

        private void ScheduleClipmapEdgeRetirement(int3 fromCenter, int3 toCenter, int radius)
        {
            if (math.all(fromCenter == toCenter)) return;

            if (_clipmapEdgeRetirementPending && _clipmapRetirementRadius == radius)
            {
                int3 activeDelta = _clipmapRetirementToCenter - _clipmapRetirementFromCenter;
                int3 extendedDelta = toCenter - _clipmapRetirementFromCenter;
                bool sameDirection = true;
                for (int axis = 0; axis < 3; axis++)
                {
                    int active = activeDelta[axis];
                    int extended = extendedDelta[axis];
                    if (active != 0 && extended != 0
                        && math.sign(active) != math.sign(extended))
                    {
                        sameDirection = false;
                        break;
                    }
                }

                // Continuous movement in the same direction simply extends the outgoing slab.
                // Keep the existing cursor so already-checked edge coordinates are not revisited.
                if (sameDirection)
                {
                    _clipmapRetirementToCenter = toCenter;
                    return;
                }
            }

            _clipmapRetirementFromCenter = fromCenter;
            _clipmapRetirementToCenter = toCenter;
            _clipmapRetirementRadius = radius;
            _clipmapRetirementAxis = 0;
            _clipmapRetirementDepth = 0;
            _clipmapRetirementPlaneCursor = 0;
            _clipmapEdgeRetirementPending = true;
        }

        private void StepClipmapEdgeRetirement()
        {
            if (!_clipmapEdgeRetirementPending) return;

            int remaining = ClipmapEdgeCandidatesPerPrepare;
            int edge = _clipmapRetirementRadius * 2 + 1;
            int planeCount = edge * edge;
            int3 delta = _clipmapRetirementToCenter - _clipmapRetirementFromCenter;

            while (remaining > 0 && _clipmapRetirementAxis < 3)
            {
                int axis = _clipmapRetirementAxis;
                int shift = delta[axis];
                int depthCount = math.min(math.abs(shift), edge);
                if (depthCount == 0 || _clipmapRetirementDepth >= depthCount)
                {
                    _clipmapRetirementAxis++;
                    _clipmapRetirementDepth = 0;
                    _clipmapRetirementPlaneCursor = 0;
                    continue;
                }

                int axisA = (axis + 1) % 3;
                int axisB = (axis + 2) % 3;
                while (remaining > 0 && _clipmapRetirementPlaneCursor < planeCount)
                {
                    int linear = _clipmapRetirementPlaneCursor++;
                    int a = linear % edge;
                    int b = linear / edge;
                    int3 coordinate = _clipmapRetirementFromCenter;
                    coordinate[axisA] += a - _clipmapRetirementRadius;
                    coordinate[axisB] += b - _clipmapRetirementRadius;
                    coordinate[axis] += shift > 0
                        ? -_clipmapRetirementRadius + _clipmapRetirementDepth
                        : _clipmapRetirementRadius - _clipmapRetirementDepth;
                    remaining--;

                    // Diagonal movement makes edge planes overlap. Current-window ownership and
                    // _known membership make those duplicates free without another hash set.
                    if (WithinClipmapWindow(coordinate) || !OwnsShard(coordinate)
                        || !_known.Contains(coordinate))
                        continue;
                    if (!TryRemoveChunk(coordinate)) RequeueResidency(coordinate);
                }

                if (_clipmapRetirementPlaneCursor < planeCount) return;
                _clipmapRetirementPlaneCursor = 0;
                _clipmapRetirementDepth++;
            }

            if (_clipmapRetirementAxis < 3) return;
            _clipmapEdgeRetirementPending = false;
            _clipmapRetirementAxis = 0;
            _clipmapRetirementDepth = 0;
            _clipmapRetirementPlaneCursor = 0;
        }

        private bool WithinClipmapWindow(int3 chunk)
        {
            if (!_clipmapWindowValid) return true;
            int3 delta = math.abs(chunk - _clipmapCenter);
            return math.cmax(delta) <= _clipmapRadius;
        }

        private bool TrackKnown(int3 chunk)
        {
            // Surface discovery/change feeds can cover a much larger resident Storage window than
            // this LOD ring draws. Render residency is admitted only inside the camera clipmap;
            // otherwise _known and, critically, the build queue would grow with world streaming
            // rather than the fixed view footprint.
            if (!WithinClipmapWindow(chunk)) return false;
            if (_known.Contains(chunk)) return true;
            if (!_slotGrid.TryAcquire(chunk, out _)) return false;

            _known.Add(chunk);
            RequeueResidency(chunk);
            return true;
        }

        private bool BuildOwnsCurrentSlot()
        {
            return _build.Active && WithinClipmapWindow(_build.Coordinate)
                && _slotGrid.TryGet(_build.Coordinate, out SurfaceChunkSlot slot)
                && slot.Generation == _build.SlotGeneration;
        }

        private void RetireSlot(int3 chunk)
        {
            _slotGrid.Retire(chunk);
        }

        private void RequeueResidency(int3 chunk)
        {
            if (!_known.Contains(chunk) || !_queuedResidency.Add(chunk)) return;
            _residencyQueue.Enqueue(chunk);
        }

        private void StepResidencyPrune(IRegionReadSource source)
        {
            int checks = math.min(ResidencyChecksPerPrepare, _residencyQueue.Count);
            for (int i = 0; i < checks; i++)
            {
                int3 chunk = _residencyQueue.Dequeue();
                _queuedResidency.Remove(chunk);
                if (!_known.Contains(chunk)) continue;

                if (WithinClipmapWindow(chunk) && AllOwnedCoreRegionsResident(source, chunk))
                {
                    RequeueResidency(chunk);
                    continue;
                }

                // Out-of-window or non-resident chunks both retire incrementally. In-flight
                // geometry is never waited on, and BuildOwnsCurrentSlot prevents an out-of-window
                // generation from publishing while it waits for this cleanup pass. If removal is deferred, put the chunk
                // back in the liveness queue and recheck it on a later frame.
                if (!TryRemoveChunk(chunk)) RequeueResidency(chunk);
            }
        }

        /// <summary>
        /// Whether every Storage region intersecting the chunk's unpadded owned core is
        /// currently resident. Exact extraction may optimistically treat an unavailable halo as
        /// empty, but a missing core region can never satisfy exact-snapshot completeness and
        /// must not remain active build demand merely because its halo touches resident Storage.
        /// A later residency publication re-runs surface discovery and readmits the chunk.
        /// </summary>
        private bool AllOwnedCoreRegionsResident(IRegionReadSource source, int3 chunk)
        {
            int3 minVoxel = chunk * VoxelsPerAxis;
            int3 maxVoxel = (chunk + 1) * VoxelsPerAxis - 1;
            int3 minRegion = new(FloorDiv(minVoxel.x, VoxelGrid.RegionVoxelEdge),
                                 FloorDiv(minVoxel.y, VoxelGrid.RegionVoxelEdge),
                                 FloorDiv(minVoxel.z, VoxelGrid.RegionVoxelEdge));
            int3 maxRegion = new(FloorDiv(maxVoxel.x, VoxelGrid.RegionVoxelEdge),
                                 FloorDiv(maxVoxel.y, VoxelGrid.RegionVoxelEdge),
                                 FloorDiv(maxVoxel.z, VoxelGrid.RegionVoxelEdge));

            for (int z = minRegion.z; z <= maxRegion.z; z++)
            for (int y = minRegion.y; y <= maxRegion.y; y++)
            for (int x = minRegion.x; x <= maxRegion.x; x++)
                if (!source.IsRegionResident(new int3(x, y, z))) return false;
            return true;
        }


        internal bool TryEvictOneForArenaPressure(Camera camera, float voxelSize)
        {
            if (_entries.Count == 0) return false;

            int3 victim = default;
            float farthest = -1f;
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            if (camera != null) GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

            foreach (var pair in _entries)
            {
                // Keep current replacement geometry alive. Arena pressure may only retire a
                // different, already-published, offscreen lease.
                if (_build.Active && pair.Key.Equals(_build.Coordinate)) continue;
                Bounds bounds = ChunkWorldBounds(pair.Key, voxelSize);
                if (camera != null && GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                    continue;

                Vector3 centre = (new Vector3(pair.Key.x, pair.Key.y, pair.Key.z)
                                + Vector3.one * 0.5f) * chunkMetres;
                float distance = (centre - cameraPosition).sqrMagnitude;
                if (distance <= farthest) continue;
                farthest = distance;
                victim = pair.Key;
            }

            if (farthest < 0f) return false;
            if (_entries.TryGetValue(victim, out Entry entry)) RecycleEntry(entry);
            RemoveEntry(victim);
            MarkDirty(victim);
            return true;
        }

        /// <summary>
        /// Squared camera distance of the chunk this worker is waiting to publish, if any. The
        /// scheduler uses the nearest such chunk to decide which resident leases may be retired
        /// when the arena has no offscreen geometry left to give up.
        /// </summary>
        /// <summary>
        /// Retires up to <paramref name="maxEvictions"/> of the farthest eligible leases in a single
        /// pass over the entry table.
        ///
        /// Relief used to answer "give me one victim", so freeing N chunks meant N full scans of the
        /// table with a frustum test per entry every time. Under pressure that is the dominant cost
        /// in SchedulerPrepare — the same scan repeated, on every frame, over a table holding
        /// thousands of leases. One pass that selects N victims costs what one old call did.
        ///
        /// With <paramref name="offscreenOnly"/> the pass gives up only geometry outside the frustum,
        /// which is the cheap choice and always the first one tried. Otherwise it retires anything
        /// published that sits farther than <paramref name="minDistanceSq"/>, which is how a fully
        /// on-screen resident set still makes room for the chunk nearest the camera.
        /// </summary>
        internal int EvictFarthest(Camera camera, float voxelSize, bool offscreenOnly,
                                   float minDistanceSq, int maxEvictions, bool gpuOnly = false)
        {
            if (_entries.Count == 0 || maxEvictions <= 0) return 0;

            int wanted = math.min(maxEvictions, MaxEvictionVictims);
            if (_evictionVictims == null || _evictionVictims.Length < MaxEvictionVictims)
            {
                _evictionVictims = new int3[MaxEvictionVictims];
                _evictionVictimDistances = new float[MaxEvictionVictims];
            }

            int found = 0;
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            if (camera != null) GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

            foreach (var pair in _entries)
            {
                if (gpuOnly && !pair.Value.IsGpuPaged) continue;
                // Keep current replacement geometry alive. Relief may only retire a different,
                // already-published lease.
                if (_build.Active && pair.Key.Equals(_build.Coordinate)) continue;
                if (offscreenOnly)
                {
                    Bounds bounds = ChunkWorldBounds(pair.Key, voxelSize);
                    if (camera != null && GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                        continue;
                }
                else if (!pair.Value.Ready)
                {
                    continue;
                }

                float distance = ChunkDistanceSq(pair.Key, camera, voxelSize);
                if (!offscreenOnly && distance <= minDistanceSq) continue;
                if (found == wanted && distance <= _evictionVictimDistances[found - 1]) continue;

                // Keep the running selection ordered farthest-first; it is at most a few entries.
                int slot = found < wanted ? found++ : wanted - 1;
                while (slot > 0 && _evictionVictimDistances[slot - 1] < distance)
                {
                    _evictionVictimDistances[slot] = _evictionVictimDistances[slot - 1];
                    _evictionVictims[slot] = _evictionVictims[slot - 1];
                    slot--;
                }
                _evictionVictimDistances[slot] = distance;
                _evictionVictims[slot] = pair.Key;
            }

            for (int i = 0; i < found; i++)
            {
                int3 victim = _evictionVictims[i];
                if (_entries.TryGetValue(victim, out Entry entry)) RecycleEntry(entry);
                RemoveEntry(victim);
                MarkDirty(victim);
            }
            return found;
        }

        private const int MaxEvictionVictims = 16;
        private int3[] _evictionVictims;
        private float[] _evictionVictimDistances;

        private float ChunkDistanceSq(int3 coordinate, Camera camera, float voxelSize)
        {
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            Vector3 centre = (new Vector3(coordinate.x, coordinate.y, coordinate.z)
                            + Vector3.one * 0.5f) * chunkMetres;
            return (centre - cameraPosition).sqrMagnitude;
        }

        private void EnforceCapacity(Camera camera, float voxelSize)
        {
            if (_entries.Count < MaxResidentChunks || _dirty.Count == 0) return;

            int3 victim = default;
            float farthest = -1f;
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            if (camera != null) GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

            foreach (var pair in _entries)
            {
                // Capacity pressure is also bounded: at most one offscreen lease retires from
                // this workspace per Prepare call. Repeated eviction loops turn a cache miss into
                // a frame spike exactly when streaming is already under pressure.
                if (camera != null && GeometryUtility.TestPlanesAABB(
                        _frustumPlanes, ChunkWorldBounds(pair.Key, voxelSize)))
                    continue;
                Vector3 centre = (new Vector3(pair.Key.x, pair.Key.y, pair.Key.z)
                                + Vector3.one * 0.5f) * chunkMetres;
                float distance = (centre - cameraPosition).sqrMagnitude;
                if (distance <= farthest) continue;
                farthest = distance;
                victim = pair.Key;
            }

            if (farthest < 0f)
            {
                CapacityPressureCount++;
                return;
            }
            if (_entries.TryGetValue(victim, out Entry entry)) RecycleEntry(entry);
            RemoveEntry(victim);
            MarkDirty(victim);
        }

        /// <summary>
        /// Whether a chunk falls in this ring's band.
        ///
        /// <para>The band is an axis-aligned box shell, not a sphere. That is deliberate and
        /// load-bearing: a spherical boundary cuts across chunk faces, so a coarse chunk would
        /// meet a finer neighbour over part of one face and a same-resolution neighbour over
        /// the rest, and a transition cell has nowhere to attach. Snapping the boundary to the
        /// coarse ring's own chunk grid means every LOD change happens exactly on a chunk face,
        /// which is the precondition for stitching it. This is the standard clipmap
        /// arrangement.</para>
        ///
        /// <para>The inner cut tests the chunk's farthest corner and the outer cut its nearest,
        /// so the bands overlap by up to one chunk rather than leaving a gap when the viewer
        /// moves between frames.</para>
        /// </summary>
        private bool WithinRingBand(Bounds bounds, Vector3 cameraPosition)
        {
            if (RingSuspended) return false;

            Vector3 extents = bounds.extents;
            Vector3 delta = bounds.center - cameraPosition;
            float nearX = Mathf.Max(0f, Mathf.Abs(delta.x) - extents.x);
            float nearY = Mathf.Max(0f, Mathf.Abs(delta.y) - extents.y);
            float nearZ = Mathf.Max(0f, Mathf.Abs(delta.z) - extents.z);
            // Chebyshev distance: the box shell's defining metric.
            float near = Mathf.Max(nearX, Mathf.Max(nearY, nearZ));
            if (near > MaxViewDistanceMetres) return false;
            if (MinViewDistanceMetres <= 0f) return true;

            float far = Mathf.Max(Mathf.Abs(delta.x) + extents.x,
                        Mathf.Max(Mathf.Abs(delta.y) + extents.y,
                                  Mathf.Abs(delta.z) + extents.z));
            return far > MinViewDistanceMetres;
        }

        /// <summary>
        /// Whether the neighbour across <paramref name="face"/> belongs to a finer ring, which
        /// is where this chunk must emit transition geometry. Faces are indexed as
        /// 0=-X, 1=+X, 2=-Y, 3=+Y, 4=-Z, 5=+Z.
        ///
        /// A finer neighbour exists exactly when this chunk sits on the inner edge of the band:
        /// the neighbour in that direction lies wholly inside <see cref="MinViewDistanceMetres"/>
        /// and is therefore owned by the ring one step finer.
        /// </summary>
        public bool FaceNeedsTransition(int3 coordinate, int face, float voxelSize,
                                        Vector3 cameraPosition)
        {
            if (MinViewDistanceMetres <= 0f) return false;

            int axis = face >> 1;
            int direction = (face & 1) == 0 ? -1 : 1;
            int3 neighbour = coordinate;
            neighbour[axis] += direction;

            Bounds neighbourBounds = ChunkWorldBounds(neighbour, voxelSize);
            Vector3 extents = neighbourBounds.extents;
            Vector3 delta = neighbourBounds.center - cameraPosition;
            float far = Mathf.Max(Mathf.Abs(delta.x) + extents.x,
                        Mathf.Max(Mathf.Abs(delta.y) + extents.y,
                                  Mathf.Abs(delta.z) + extents.z));
            // Wholly inside the inner cut means the finer ring owns it outright.
            return far <= MinViewDistanceMetres;
        }

        /// <summary>Builds the GPU transition mask with the same finer-neighbour ownership test
        /// as the CPU seam path. Bits are 0=-X, 1=+X, 2=-Y, 3=+Y, 4=-Z, 5=+Z.</summary>
        internal int BuildGpuTransitionFaceMask(int3 coordinate, float voxelSize,
                                                Vector3 cameraPosition)
        {
            int mask = 0;
            for (int face = 0; face < 6; face++)
                if (FaceNeedsTransition(coordinate, face, voxelSize, cameraPosition))
                    mask |= 1 << face;
            return mask;
        }

        private Bounds ChunkWorldBounds(int3 coordinate, float voxelSize)
        {
            float size = VoxelsPerAxis * voxelSize;
            Vector3 min = new Vector3(coordinate.x, coordinate.y, coordinate.z) * size;
            return new Bounds(min + Vector3.one * (size * 0.5f),
                              Vector3.one * (size + SourceStep * voxelSize * 2f));
        }

        private bool TryRemoveChunk(int3 chunk)
        {
            _known.Remove(chunk);
            _gpuDemandGeometry.Remove(chunk);
            RetireSlot(chunk);
            _queuedResidency.Remove(chunk);
            _dirty.Remove(chunk);
            _queuedDirty.Remove(chunk);
            _queuedVisibleDirty.Remove(chunk);
            _desiredVersions.Remove(chunk);
            _emptyVersions.Remove(chunk);
            _queuedAtSeconds.Remove(chunk);
            if (_entries.TryGetValue(chunk, out Entry entry))
            {
                RecycleEntry(entry);
                RemoveEntry(chunk);
            }
            if (_build.Active && _build.Coordinate.Equals(chunk))
            {
                // Every handle was observed complete above, so these Complete calls only
                // release job safety dependencies; none can stall the frame.
                ReleasePendingGpuBuild();
                _build = default;
            }
            return true;
        }


        /// <summary>
        /// Maps a chunk coordinate in this ring's own coordinate space to the region that
        /// contains its origin. Derived from the voxel origin rather than a chunks-per-region
        /// shift, because a coarse ring's chunk can be as large as, or larger than, a region.
        /// </summary>
        private int3 ChunkRegion(int3 chunk)
        {
            int3 originVoxel = chunk * VoxelsPerAxis;
            return new int3(FloorDiv(originVoxel.x, VoxelGrid.RegionVoxelEdge),
                            FloorDiv(originVoxel.y, VoxelGrid.RegionVoxelEdge),
                            FloorDiv(originVoxel.z, VoxelGrid.RegionVoxelEdge));
        }


        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static int FloorMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        public void Dispose()
        {
            ReleasePendingGpuBuild();
            foreach (Entry entry in _entries.Values)
            {
                if (entry.IsGpuPaged)
                    GpuSurfaceMirrorCoordinator.ReleaseChunkHandle(
                        entry.Coordinate * entry.VoxelsPerAxis,
                        entry.SourceStep, entry.SourceVersion);
                entry.Dispose();
            }
            _entries.Clear();
            foreach (Entry entry in _entryPool) entry.Dispose();
            _entryPool.Clear();
            _gpuExtraction?.Dispose();
            _gpuExtraction = null;
            _known.Clear();
            _gpuDemandGeometry.Clear();
            _dirty.Clear();
            _desiredVersions.Clear();
            _queuedAtSeconds.Clear();
            _visible.Clear();
            _build = default;
        }

        private static double ElapsedMs(double startSeconds) => startSeconds <= 0.0
            ? 0.0 : (Time.realtimeSinceStartupAsDouble - startSeconds) * 1000.0;
    }
}
