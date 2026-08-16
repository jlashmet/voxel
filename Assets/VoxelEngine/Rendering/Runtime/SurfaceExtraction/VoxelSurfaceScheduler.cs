using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    public readonly struct VoxelSurfaceMetrics
    {
        public readonly int ChangeRecords;
        public readonly int DiscoveredSurfaceBricks;
        public readonly int SolidKnownChunks;
        public readonly int SolidResidentChunks;
        public readonly int SolidDirtyChunks;
        public readonly int WaterResidentChunks;
        public readonly int WaterDirtyChunks;
        public readonly int VisibleSolidChunks;
        public readonly int MissingVisibleSolidChunks;
        public readonly int VisibleDetailSolidChunks;
        public readonly int VisibleWaterChunks;
        public readonly ulong CompletedSolidBuilds;
        public readonly ulong RejectedStaleSolidBuilds;
        public readonly ulong CompletedWaterBuilds;
        public readonly ulong RejectedStaleWaterBuilds;
        public readonly long ResidentGeometryBytes;
        public readonly ulong UploadedGeometryBytes;
        public readonly ulong SolidDecorationClumps;
        public readonly ulong SolidCapacityPressureEvents;
        public readonly int RunningSolidJobs;
        public readonly int SolidMeshesAwaitingUpload;
        public readonly long SolidPendingUploadBytes;
        public readonly int SolidUploadBudgetBytes;
        public readonly int LastFrameSolidUploadedBytes;
        public readonly int LastFrameSolidUploadCompletions;
        public readonly double LastSolidSnapshotMs;
        public readonly double LastSolidTopologyCompactMs;
        public readonly double LastSolidUploadMs;
        public readonly VoxelTimingSummary SchedulerPrepareTiming;
        public readonly VoxelTimingSummary ChangeJournalTiming;
        public readonly VoxelTimingSummary InvalidationTiming;
        public readonly VoxelTimingSummary SurfaceDiscoveryTiming;
        public readonly VoxelTimingSummary WorkerPrepareTiming;
        public readonly VoxelTimingSummary VisibilityTiming;
        public readonly VoxelTimingSummary SnapshotTiming;
        public readonly VoxelTimingSummary DensityJobTurnaroundTiming;
        public readonly VoxelTimingSummary TopologyJobTurnaroundTiming;
        public readonly VoxelTimingSummary TopologyCompactTiming;
        public readonly VoxelTimingSummary FacetedJobTurnaroundTiming;
        public readonly VoxelTimingSummary FacetedMergeTiming;
        public readonly VoxelTimingSummary ProfileEmitTiming;
        public readonly VoxelTimingSummary UploadTiming;
        public readonly VoxelTimingSummary QueueLatencyTiming;
        public readonly VoxelTimingSummary BuildLatencyTiming;
        public readonly VoxelTimingSummary RuleSyncTiming;
        public readonly VoxelTimingSummary ResidencyPruneTiming;
        public readonly VoxelTimingSummary CapacityTiming;
        public readonly VoxelTimingSummary BuildSelectionTiming;

        internal VoxelSurfaceMetrics(CpuTransvoxelChunkCache solids,
                                     CpuWaterSurfaceChunkCache water,
                                     int changeRecords, int discoveredSurfaceBricks)
        {
            ChangeRecords = changeRecords;
            DiscoveredSurfaceBricks = discoveredSurfaceBricks;
            SolidKnownChunks = solids.KnownCount;
            SolidResidentChunks = solids.ResidentCount;
            SolidDirtyChunks = solids.DirtyCount;
            WaterResidentChunks = water.ResidentCount;
            WaterDirtyChunks = water.DirtyCount;
            VisibleSolidChunks = solids.Visible.Count;
            MissingVisibleSolidChunks = solids.MissingVisibleCount;
            VisibleDetailSolidChunks = 0;
            VisibleWaterChunks = water.Visible.Count;
            CompletedSolidBuilds = solids.CompletedBuildCount;
            RejectedStaleSolidBuilds = solids.StaleBuildCount;
            CompletedWaterBuilds = water.CompletedBuildCount;
            RejectedStaleWaterBuilds = water.StaleBuildCount;
            ResidentGeometryBytes = solids.ResidentGpuBytes + water.ResidentGpuBytes;
            UploadedGeometryBytes = solids.UploadedGeometryBytes + water.UploadedGeometryBytes;
            SolidDecorationClumps = solids.CompletedDecorationClumps;
            SolidCapacityPressureEvents = solids.CapacityPressureCount;
            RunningSolidJobs = solids.RunningJobCount;
            SolidMeshesAwaitingUpload = solids.PendingUploadCount;
            SolidPendingUploadBytes = solids.PendingUploadBytes;
            SolidUploadBudgetBytes = int.MaxValue;
            LastFrameSolidUploadedBytes = 0;
            LastFrameSolidUploadCompletions = 0;
            LastSolidSnapshotMs = solids.LastSnapshotMs;
            LastSolidTopologyCompactMs = solids.LastTopologyCompactMs;
            LastSolidUploadMs = solids.LastUploadMs;
            SchedulerPrepareTiming = default;
            ChangeJournalTiming = default;
            InvalidationTiming = default;
            SurfaceDiscoveryTiming = default;
            WorkerPrepareTiming = default;
            VisibilityTiming = default;
            SnapshotTiming = solids.SnapshotTiming;
            DensityJobTurnaroundTiming = solids.DensityTurnaroundTiming;
            TopologyJobTurnaroundTiming = solids.TopologyJobTurnaroundTiming;
            TopologyCompactTiming = solids.TopologyCompactTiming;
            FacetedJobTurnaroundTiming = solids.FacetedJobTurnaroundTiming;
            FacetedMergeTiming = solids.FacetedMergeTiming;
            ProfileEmitTiming = solids.ProfileEmitTiming;
            UploadTiming = solids.UploadTiming;
            QueueLatencyTiming = solids.QueueLatencyTiming;
            BuildLatencyTiming = solids.BuildLatencyTiming;
            RuleSyncTiming = solids.RuleSyncTiming;
            ResidencyPruneTiming = solids.ResidencyPruneTiming;
            CapacityTiming = solids.CapacityTiming;
            BuildSelectionTiming = solids.BuildSelectionTiming;
        }

        internal VoxelSurfaceMetrics(CpuTransvoxelChunkCache[] workers,
                                     CpuWaterSurfaceChunkCache water,
                                     int changeRecords, int discoveredSurfaceBricks,
                                     int visibleSolidChunks,
                                     int solidUploadBudgetBytes,
                                     int lastFrameSolidUploadedBytes,
                                     int lastFrameSolidUploadCompletions,
                                     in VoxelTimingSummary schedulerPrepare,
                                     in VoxelTimingSummary journal,
                                     in VoxelTimingSummary invalidation,
                                     in VoxelTimingSummary discovery,
                                     in VoxelTimingSummary workerPrepare,
                                     in VoxelTimingSummary visibility)
        {
            ChangeRecords = changeRecords;
            DiscoveredSurfaceBricks = discoveredSurfaceBricks;
            WaterResidentChunks = water.ResidentCount;
            WaterDirtyChunks = water.DirtyCount;
            VisibleWaterChunks = water.Visible.Count;
            CompletedWaterBuilds = water.CompletedBuildCount;
            RejectedStaleWaterBuilds = water.StaleBuildCount;
            VisibleSolidChunks = visibleSolidChunks;
            VisibleDetailSolidChunks = 0;
            int known = 0, resident = 0, dirty = 0, missing = 0, running = 0, uploads = 0;
            long pendingUploadBytes = 0;
            ulong completed = 0, stale = 0, uploadedBytes = water.UploadedGeometryBytes;
            ulong decorations = 0, pressure = 0;
            long geometryBytes = water.ResidentGpuBytes;
            double snapshotMs = 0, compactMs = 0, uploadMs = 0;
            for (int i = 0; i < workers.Length; i++)
            {
                CpuTransvoxelChunkCache worker = workers[i];
                known += worker.KnownCount;
                resident += worker.ResidentCount;
                dirty += worker.DirtyCount;
                missing += worker.MissingVisibleCount;
                running += worker.RunningJobCount;
                uploads += worker.PendingUploadCount;
                pendingUploadBytes += worker.PendingUploadBytes;
                completed += worker.CompletedBuildCount;
                stale += worker.StaleBuildCount;
                uploadedBytes += worker.UploadedGeometryBytes;
                decorations += worker.CompletedDecorationClumps;
                pressure += worker.CapacityPressureCount;
                geometryBytes += worker.ResidentGpuBytes;
                snapshotMs = Math.Max(snapshotMs, worker.LastSnapshotMs);
                compactMs = Math.Max(compactMs, worker.LastTopologyCompactMs);
                uploadMs = Math.Max(uploadMs, worker.LastUploadMs);
            }
            SolidKnownChunks = known;
            SolidResidentChunks = resident;
            SolidDirtyChunks = dirty;
            MissingVisibleSolidChunks = missing;
            RunningSolidJobs = running;
            SolidMeshesAwaitingUpload = uploads;
            SolidPendingUploadBytes = pendingUploadBytes;
            SolidUploadBudgetBytes = solidUploadBudgetBytes;
            LastFrameSolidUploadedBytes = lastFrameSolidUploadedBytes;
            LastFrameSolidUploadCompletions = lastFrameSolidUploadCompletions;
            CompletedSolidBuilds = completed;
            RejectedStaleSolidBuilds = stale;
            UploadedGeometryBytes = uploadedBytes;
            SolidDecorationClumps = decorations;
            SolidCapacityPressureEvents = pressure;
            ResidentGeometryBytes = geometryBytes;
            LastSolidSnapshotMs = snapshotMs;
            LastSolidTopologyCompactMs = compactMs;
            LastSolidUploadMs = uploadMs;
            SchedulerPrepareTiming = schedulerPrepare;
            ChangeJournalTiming = journal;
            InvalidationTiming = invalidation;
            SurfaceDiscoveryTiming = discovery;
            WorkerPrepareTiming = workerPrepare;
            VisibilityTiming = visibility;
            SnapshotTiming = default;
            DensityJobTurnaroundTiming = default;
            TopologyJobTurnaroundTiming = default;
            TopologyCompactTiming = default;
            FacetedJobTurnaroundTiming = default;
            FacetedMergeTiming = default;
            ProfileEmitTiming = default;
            UploadTiming = default;
            QueueLatencyTiming = default;
            BuildLatencyTiming = default;
            RuleSyncTiming = default;
            ResidencyPruneTiming = default;
            CapacityTiming = default;
            BuildSelectionTiming = default;
            for (int i = 0; i < workers.Length; i++)
            {
                CpuTransvoxelChunkCache worker = workers[i];
                SnapshotTiming = VoxelTimingSummary.WorstOf(SnapshotTiming, worker.SnapshotTiming);
                DensityJobTurnaroundTiming = VoxelTimingSummary.WorstOf(
                    DensityJobTurnaroundTiming, worker.DensityTurnaroundTiming);
                TopologyJobTurnaroundTiming = VoxelTimingSummary.WorstOf(
                    TopologyJobTurnaroundTiming, worker.TopologyJobTurnaroundTiming);
                TopologyCompactTiming = VoxelTimingSummary.WorstOf(
                    TopologyCompactTiming, worker.TopologyCompactTiming);
                FacetedJobTurnaroundTiming = VoxelTimingSummary.WorstOf(
                    FacetedJobTurnaroundTiming, worker.FacetedJobTurnaroundTiming);
                FacetedMergeTiming = VoxelTimingSummary.WorstOf(
                    FacetedMergeTiming, worker.FacetedMergeTiming);
                ProfileEmitTiming = VoxelTimingSummary.WorstOf(
                    ProfileEmitTiming, worker.ProfileEmitTiming);
                UploadTiming = VoxelTimingSummary.WorstOf(UploadTiming, worker.UploadTiming);
                QueueLatencyTiming = VoxelTimingSummary.WorstOf(
                    QueueLatencyTiming, worker.QueueLatencyTiming);
                BuildLatencyTiming = VoxelTimingSummary.WorstOf(
                    BuildLatencyTiming, worker.BuildLatencyTiming);
                RuleSyncTiming = VoxelTimingSummary.WorstOf(
                    RuleSyncTiming, worker.RuleSyncTiming);
                ResidencyPruneTiming = VoxelTimingSummary.WorstOf(
                    ResidencyPruneTiming, worker.ResidencyPruneTiming);
                CapacityTiming = VoxelTimingSummary.WorstOf(
                    CapacityTiming, worker.CapacityTiming);
                BuildSelectionTiming = VoxelTimingSummary.WorstOf(
                    BuildSelectionTiming, worker.BuildSelectionTiming);
            }
        }
    }

    /// <summary>
    /// Common invalidation, residency, build-budget, and handoff owner for derived voxel surfaces.
    /// Render passes consume its ready entries and never interpret voxel semantics themselves.
    /// </summary>
    public sealed class VoxelSurfaceScheduler : IDisposable
    {
        private static readonly ProfilerMarker s_PrepareMarker = new("Voxel.Surface.SchedulerPrepare");
        private static readonly ProfilerMarker s_JournalMarker = new("Voxel.Surface.ChangeJournal");
        private static readonly ProfilerMarker s_InvalidationMarker = new("Voxel.Surface.Invalidation");
        private static readonly ProfilerMarker s_DiscoveryMarker = new("Voxel.Surface.Discovery");
        private static readonly ProfilerMarker s_WorkersMarker = new("Voxel.Surface.WorkerAdmission");
        private static readonly ProfilerMarker s_VisibilityMarker = new("Voxel.Surface.Visibility");
        private const int SurfaceDiscoveryPublishBatch = 512;
        public const int SolidWorkerCount = 8;

        private sealed class SurfaceRing : IDisposable
        {
            public readonly int SourceStep;
            public readonly float InnerRadiusMetres;
            public readonly float OuterRadiusMetres;
            public readonly CpuTransvoxelChunkCache[] Workers;

            public SurfaceRing(int sourceStep, float innerRadiusMetres, float outerRadiusMetres,
                               int maxResidentChunks)
            {
                SourceStep = sourceStep;
                InnerRadiusMetres = innerRadiusMetres;
                OuterRadiusMetres = outerRadiusMetres;
                Workers = new CpuTransvoxelChunkCache[SolidWorkerCount];
                for (int i = 0; i < Workers.Length; i++)
                {
                    Workers[i] = new CpuTransvoxelChunkCache(sourceStep)
                    {
                        ShardIndex = i,
                        ShardCount = Workers.Length,
                        MaxResidentChunks = maxResidentChunks / Workers.Length,
                        MinViewDistanceMetres = innerRadiusMetres,
                        MaxViewDistanceMetres = outerRadiusMetres,
                    };
                }
            }

            public void Dispose()
            {
                for (int i = 0; i < Workers.Length; i++) Workers[i].Dispose();
            }
        }

        private static readonly (int SourceStep, float Inner, float Outer)[] s_RingLayout =
        {
            (1, 0f, 96f),
            (2, 96f, 192f),
            (4, 192f, 288f),
            (8, 288f, MaxVoxelRingRadiusMetres),
        };

        public const float MaxVoxelRingRadiusMetres = 420f;

        private readonly SurfaceRing[] _rings;
        private readonly CpuTransvoxelChunkCache[] _allWorkers;
        private readonly List<CpuTransvoxelChunkCache.Entry> _visibleSolids = new(256);
        private readonly CpuWaterSurfaceChunkCache _water = new();
        private readonly List<VoxelChangeRecord> _changeScratch = new(256);
        private readonly HashSet<int3> _changedSolidRegions = new();
        private readonly HashSet<int3> _changedWaterRegions = new();
        private readonly HashSet<int3> _changedBrickSet = new();
        private readonly List<int3> _changedBricks = new(64);
        private readonly HashSet<int3> _changedWaterBrickSet = new();
        private readonly List<int3> _changedWaterBricks = new(64);
        private readonly HashSet<int3> _surfaceDiscoveryRegions = new();
        private readonly List<int3> _discoveredSurfaceBricks = new(512);
        private readonly Queue<int3> _surfaceDiscoveryQueue = new();
        private readonly HashSet<int3> _queuedSurfaceDiscoveryRegions = new();
        private readonly HashSet<int3> _surfaceDiscoveryRescanRegions = new();
        private NativeArray<ulong> _surfaceDiscoveryOccupiedWords;
        private NativeArray<ulong> _surfaceDiscoveryFullySolidWords;
        private NativeArray<byte> _surfaceDiscoveryFlags;
        private NativeList<int3> _surfaceDiscoveryResults;
        private JobHandle _surfaceDiscoveryJobHandle;
        private bool _surfaceDiscoveryJobScheduled;
        private bool _hasActiveSurfaceDiscovery;
        private int3 _activeSurfaceDiscoveryRegion;
        private int _surfaceDiscoveryPublishIndex;
        private ulong _changeCursor;
        private IVoxelChangeSource _journal;
        private int _lastChangeRecords;
        private int _workerAdmissionCursor;
        private int _uploadAdmissionCursor;
        private int _lastFrameSolidUploadedBytes;
        private int _lastFrameSolidUploadCompletions;
        private readonly VoxelTimingWindow _prepareTiming = new();
        private readonly VoxelTimingWindow _journalTiming = new();
        private readonly VoxelTimingWindow _invalidationTiming = new();
        private readonly VoxelTimingWindow _discoveryTiming = new();
        private readonly VoxelTimingWindow _workerPrepareTiming = new();
        private readonly VoxelTimingWindow _visibilityTiming = new();

        /// <summary>
        /// Renderer-wide main-thread budget for admitting solid surface work. This is shared by
        /// every LOD ring and worker; it is not multiplied by worker count.
        /// </summary>
        public double SolidBuildBudgetMs { get; set; } = 0.20;
        /// <summary>
        /// Main-thread budget for snapshotting and publishing full-region surface discovery.
        /// Classification and compaction themselves run on Burst jobs and never borrow Storage
        /// memory. Ordinary edits bypass this path through the fine-grained change journal.
        /// </summary>
        public double SurfaceDiscoveryBudgetMs { get; set; } = 0.10;
        /// <summary>Maximum solid geometry payload copied to GPU buffers per frame.</summary>
        public int SolidUploadBudgetBytes { get; set; } = 1024 * 1024;
        /// <summary>Maximum payload slice given to one worker in a frame.</summary>
        public int SolidUploadSliceBytes { get; set; } = 256 * 1024;
        /// <summary>Caps workers touched by GPU publication, including staging starts.</summary>
        public int SolidUploadWorkerBudget { get; set; } = 4;
        /// <summary>Wall-clock admission deadline around upload slices.</summary>
        public double SolidUploadBudgetMs { get; set; } = 0.20;
        public int LastFrameSolidUploadedBytes => _lastFrameSolidUploadedBytes;
        public int LastFrameSolidUploadCompletions => _lastFrameSolidUploadCompletions;
        public int PendingSolidUploadBytes
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _allWorkers.Length; i++)
                    total += _allWorkers[i].PendingUploadBytes;
                return total;
            }
        }
        public double WaterBuildBudgetMs { get; set; } = 0.15;

        public IReadOnlyList<CpuTransvoxelChunkCache.Entry> VisibleSolids => _visibleSolids;
        public IReadOnlyList<CpuWaterSurfaceChunkCache.Entry> VisibleWater => _water.Visible;
        public VoxelSurfaceMetrics Metrics => new(
            _allWorkers, _water, _lastChangeRecords, _discoveredSurfaceBricks.Count,
            _visibleSolids.Count, SolidUploadBudgetBytes, _lastFrameSolidUploadedBytes,
            _lastFrameSolidUploadCompletions, _prepareTiming.Snapshot(), _journalTiming.Snapshot(),
            _invalidationTiming.Snapshot(), _discoveryTiming.Snapshot(),
            _workerPrepareTiming.Snapshot(), _visibilityTiming.Snapshot());

        public VoxelSurfaceScheduler()
        {
            _rings = new SurfaceRing[s_RingLayout.Length];
            _allWorkers = new CpuTransvoxelChunkCache[s_RingLayout.Length * SolidWorkerCount];
            int workerIndex = 0;
            for (int i = 0; i < s_RingLayout.Length; i++)
            {
                var layout = s_RingLayout[i];
                SurfaceRing ring = new(layout.SourceStep, layout.Inner, layout.Outer, 4096);
                _rings[i] = ring;
                for (int worker = 0; worker < ring.Workers.Length; worker++)
                    _allWorkers[workerIndex++] = ring.Workers[worker];
            }

            _surfaceDiscoveryOccupiedWords = new NativeArray<ulong>(
                VoxelReadGrid.BlockSummaryWordCount, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            _surfaceDiscoveryFullySolidWords = new NativeArray<ulong>(
                VoxelReadGrid.BlockSummaryWordCount, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            _surfaceDiscoveryFlags = new NativeArray<byte>(
                VoxelReadGrid.BlocksPerRegion, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            _surfaceDiscoveryResults = new NativeList<int3>(
                VoxelReadGrid.BlocksPerRegion, Allocator.Persistent);
        }

        public void Prepare(IRegionReadSource storage, in MaterialPaletteView palette,
                            in SurfaceCatalogueView surfaceCatalogue,
                            in CoatingCatalogueView coatingCatalogue,
                            IProfileBlockReadSource profileBlocks,
                            IVoxelChangeSource journal, Camera camera, float voxelSize, int frame)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));

            double prepareStart = Time.realtimeSinceStartupAsDouble;
            using var prepareScope = s_PrepareMarker.Auto();
            _changedSolidRegions.Clear();
            _changedWaterRegions.Clear();
            _changedBrickSet.Clear();
            _changedBricks.Clear();
            _changedWaterBrickSet.Clear();
            _changedWaterBricks.Clear();
            _surfaceDiscoveryRegions.Clear();
            _discoveredSurfaceBricks.Clear();

            double journalStart = Time.realtimeSinceStartupAsDouble;
            using (s_JournalMarker.Auto()) if (journal != null)
            {
                if (!ReferenceEquals(journal, _journal))
                {
                    _journal = journal;
                    _changeCursor = 0;
                }
                bool complete = journal.ReadSince(ref _changeCursor, _changeScratch);
                _lastChangeRecords = _changeScratch.Count;
                if (!complete)
                {
                    using var resident = storage.GetResidentRegionCoords(Allocator.Temp);
                    for (int i = 0; i < resident.Length; i++)
                    {
                        _changedSolidRegions.Add(resident[i]);
                        _changedWaterRegions.Add(resident[i]);
                        _surfaceDiscoveryRegions.Add(resident[i]);
                    }
                }
                else
                {
                    for (int i = 0; i < _changeScratch.Count; i++)
                    {
                        VoxelChangeRecord change = _changeScratch[i];
                        bool affectsSolids = (change.Kind & (VoxelChangeKind.Occupancy
                            | VoxelChangeKind.BaseMaterial | VoxelChangeKind.SurfaceStyle
                            | VoxelChangeKind.Coating | VoxelChangeKind.Residency)) != 0;
                        bool affectsWater = (change.Kind & (VoxelChangeKind.Occupancy
                            | VoxelChangeKind.BaseMaterial | VoxelChangeKind.Water
                            | VoxelChangeKind.Residency)) != 0;
                        int3 extent = change.MaxVoxelExclusive - change.MinVoxel;
                        if (math.any(extent >= VoxelGrid.RegionVoxelEdge))
                        {
                            if (affectsSolids) _changedSolidRegions.Add(change.Region);
                            if (affectsWater) _changedWaterRegions.Add(change.Region);
                            _surfaceDiscoveryRegions.Add(change.Region);
                            continue;
                        }

                        int3 minBrick = change.MinVoxel >> VoxelReadGrid.BlockEdgeLog2;
                        int3 maxBrick = (change.MaxVoxelExclusive - 1)
                                      >> VoxelReadGrid.BlockEdgeLog2;
                        for (int z = minBrick.z; z <= maxBrick.z; z++)
                        for (int y = minBrick.y; y <= maxBrick.y; y++)
                        for (int x = minBrick.x; x <= maxBrick.x; x++)
                        {
                            int3 brick = new(x, y, z);
                            if (affectsSolids && _changedBrickSet.Add(brick))
                                _changedBricks.Add(brick);
                            if (affectsWater && _changedWaterBrickSet.Add(brick))
                                _changedWaterBricks.Add(brick);
                        }
                    }
                }
            }
            else
            {
                _journal = null;
                _changeCursor = 0;
                _lastChangeRecords = 0;
            }
            _journalTiming.Add(ElapsedMs(journalStart));

            double invalidationStart = Time.realtimeSinceStartupAsDouble;
            using (s_InvalidationMarker.Auto())
            {
                for (int i = 0; i < _allWorkers.Length; i++)
                    _allWorkers[i].InvalidateDirtyRegions(_changedSolidRegions);
                _water.InvalidateDirtyRegions(_changedWaterRegions);
                for (int i = 0; i < _allWorkers.Length; i++)
                    _allWorkers[i].InvalidateSurfaceBricks(_changedBricks);
                _water.InvalidateSurfaceBricks(storage, _changedWaterBricks);
            }
            _invalidationTiming.Add(ElapsedMs(invalidationStart));

            double discoveryStart = Time.realtimeSinceStartupAsDouble;
            using (s_DiscoveryMarker.Auto())
            {
                EnqueueSurfaceDiscovery(_surfaceDiscoveryRegions);
                ProcessSurfaceDiscovery(storage, _discoveredSurfaceBricks,
                                        SurfaceDiscoveryBudgetMs);
            }
            _discoveryTiming.Add(ElapsedMs(discoveryStart));

            // Discovery is correctness work rather than build admission: every worker must learn
            // about newly surfaced bricks even if this frame has no time left to rebuild them.
            for (int i = 0; i < _allWorkers.Length; i++)
                _allWorkers[i].InvalidateSurfaceBricks(_discoveredSurfaceBricks);

            _visibleSolids.Clear();
            double workersStart = Time.realtimeSinceStartupAsDouble;
            double solidDeadline = workersStart + Math.Max(0.0, SolidBuildBudgetMs) * 0.001;
            int admittedWorkers = 0;
            using var workersScope = s_WorkersMarker.Auto();

            // Start from a different worker after each admission so one expensive ring/shard
            // cannot permanently starve the rest when the global budget is intentionally tiny.
            int workerCount = _allWorkers.Length;
            for (int offset = 0; offset < workerCount; offset++)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                double remainingMs = (solidDeadline - now) * 1000.0;
                if (remainingMs <= 0.0) break;

                int index = (_workerAdmissionCursor + offset) % workerCount;
                CpuTransvoxelChunkCache worker = _allWorkers[index];
                worker.Prepare(storage, in palette, in surfaceCatalogue,
                               in coatingCatalogue, profileBlocks, camera, voxelSize, frame,
                               remainingMs);
                admittedWorkers++;
            }
            if (workerCount > 0)
            {
                int advance = Math.Max(1, admittedWorkers);
                _workerAdmissionCursor = (_workerAdmissionCursor + advance) % workerCount;
            }

            double workerPrepareMs = ElapsedMs(workersStart);

            // GPU publication has its own global frame contract. Each worker receives at
            // most one bounded slice, so a large completed chunk naturally spans frames
            // while its previous ready geometry remains visible.
            _lastFrameSolidUploadedBytes = 0;
            _lastFrameSolidUploadCompletions = 0;
            int uploadBudget = Math.Max(0, SolidUploadBudgetBytes);
            int uploadSlice = Math.Max(0, SolidUploadSliceBytes);
            int uploadWorkerBudget = Math.Max(0, SolidUploadWorkerBudget);
            double uploadDeadline = Time.realtimeSinceStartupAsDouble
                                  + Math.Max(0.0, SolidUploadBudgetMs) * 0.001;
            int uploadWorkersVisited = 0;
            int uploadScanAdvance = 0;
            if (uploadBudget > 0 && uploadSlice > 0 && uploadWorkerBudget > 0)
            {
                for (int offset = 0; offset < workerCount; offset++)
                {
                    if (_lastFrameSolidUploadedBytes >= uploadBudget
                        || uploadWorkersVisited >= uploadWorkerBudget
                        || Time.realtimeSinceStartupAsDouble >= uploadDeadline)
                        break;

                    int index = (_uploadAdmissionCursor + offset) % workerCount;
                    uploadScanAdvance = offset + 1;
                    CpuTransvoxelChunkCache worker = _allWorkers[index];
                    if (worker.PendingUploadCount == 0) continue;

                    int remaining = uploadBudget - _lastFrameSolidUploadedBytes;
                    int slice = Math.Min(remaining, uploadSlice);
                    if (slice <= 0) break;
                    bool completed = worker.TryPublishPending(frame, slice,
                                                              out int uploadedBytes);
                    _lastFrameSolidUploadedBytes += uploadedBytes;
                    uploadWorkersVisited++;
                    if (completed) _lastFrameSolidUploadCompletions++;
                }
            }
            if (workerCount > 0)
                _uploadAdmissionCursor = (_uploadAdmissionCursor
                                        + Math.Max(1, uploadScanAdvance)) % workerCount;

            double visibilityStart = Time.realtimeSinceStartupAsDouble;
            using (s_VisibilityMarker.Auto())
            {
                for (int i = 0; i < _allWorkers.Length; i++)
                {
                    IReadOnlyList<CpuTransvoxelChunkCache.Entry> visible =
                        _allWorkers[i].CollectVisible(camera, voxelSize, frame);
                    for (int j = 0; j < visible.Count; j++) _visibleSolids.Add(visible[j]);
                }
            }
            double visibilityMs = ElapsedMs(visibilityStart);

            _water.InvalidateSurfaceBricks(storage, _discoveredSurfaceBricks);
            _water.Prepare(storage, camera, voxelSize, WaterBuildBudgetMs);
            _water.CollectVisible(camera, voxelSize);
            _workerPrepareTiming.Add(workerPrepareMs);
            _visibilityTiming.Add(visibilityMs);
            _prepareTiming.Add(ElapsedMs(prepareStart));
        }

        private void EnqueueSurfaceDiscovery(HashSet<int3> regions)
        {
            foreach (int3 region in regions)
            {
                if (_queuedSurfaceDiscoveryRegions.Add(region))
                {
                    _surfaceDiscoveryQueue.Enqueue(region);
                    continue;
                }

                // A second full-region publication while this region is already being processed
                // invalidates the in-flight snapshot. Queued-but-not-started regions need no extra
                // entry because their snapshot has not been captured yet.
                if (_hasActiveSurfaceDiscovery && region.Equals(_activeSurfaceDiscoveryRegion))
                    _surfaceDiscoveryRescanRegions.Add(region);
            }
        }

        private void ProcessSurfaceDiscovery(IRegionReadSource storage,
                                             List<int3> destination,
                                             double budgetMs)
        {
            if (budgetMs <= 0.0) return;

            double deadline = Time.realtimeSinceStartupAsDouble
                            + Math.Max(0.0, budgetMs) * 0.001;
            int edge = VoxelReadGrid.BlocksPerRegionEdge;
            int blockCount = VoxelReadGrid.BlocksPerRegion;

            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (_surfaceDiscoveryJobScheduled)
                {
                    if (!_surfaceDiscoveryJobHandle.IsCompleted)
                        return;

                    // IsCompleted guarantees this Complete is a synchronization acknowledgement,
                    // not a frame stall waiting for worker execution.
                    _surfaceDiscoveryJobHandle.Complete();
                    _surfaceDiscoveryJobScheduled = false;
                    _surfaceDiscoveryPublishIndex = 0;
                }

                if (_hasActiveSurfaceDiscovery
                    && _surfaceDiscoveryRescanRegions.Contains(_activeSurfaceDiscoveryRegion))
                {
                    FinishSurfaceDiscovery(requeue: true);
                    continue;
                }

                if (_hasActiveSurfaceDiscovery)
                {
                    int end = Math.Min(_surfaceDiscoveryResults.Length,
                                       _surfaceDiscoveryPublishIndex + SurfaceDiscoveryPublishBatch);
                    int3 origin = _activeSurfaceDiscoveryRegion * edge;
                    for (int i = _surfaceDiscoveryPublishIndex; i < end; i++)
                        destination.Add(origin + _surfaceDiscoveryResults[i]);
                    _surfaceDiscoveryPublishIndex = end;

                    if (_surfaceDiscoveryPublishIndex < _surfaceDiscoveryResults.Length)
                        continue;

                    FinishSurfaceDiscovery(requeue: false);
                    continue;
                }

                if (_surfaceDiscoveryQueue.Count == 0)
                    break;

                _activeSurfaceDiscoveryRegion = _surfaceDiscoveryQueue.Dequeue();
                _hasActiveSurfaceDiscovery = true;
                _surfaceDiscoveryPublishIndex = 0;
                _surfaceDiscoveryResults.Clear();

                if (!storage.TryCopyBlockSummary(
                        _activeSurfaceDiscoveryRegion,
                        _surfaceDiscoveryOccupiedWords,
                        _surfaceDiscoveryFullySolidWords,
                        out _))
                {
                    bool retry = storage.IsRegionResident(_activeSurfaceDiscoveryRegion);
                    FinishSurfaceDiscovery(retry);
                    continue;
                }

                JobHandle classify = new SurfaceBrickDiscoveryJob
                {
                    OccupiedWords = _surfaceDiscoveryOccupiedWords,
                    FullySolidWords = _surfaceDiscoveryFullySolidWords,
                    IsSurface = _surfaceDiscoveryFlags,
                    Edge = edge,
                }.Schedule(blockCount, 256);
                _surfaceDiscoveryJobHandle = new SurfaceBrickCompactJob
                {
                    IsSurface = _surfaceDiscoveryFlags,
                    SurfaceBlocks = _surfaceDiscoveryResults,
                    Edge = edge,
                }.Schedule(classify);
                _surfaceDiscoveryJobScheduled = true;

                // Never spin on newly scheduled work. It may finish this frame, but publication
                // happens only when a later Prepare observes IsCompleted.
                return;
            }
        }

        private void FinishSurfaceDiscovery(bool requeue)
        {
            int3 region = _activeSurfaceDiscoveryRegion;
            _surfaceDiscoveryRescanRegions.Remove(region);
            _queuedSurfaceDiscoveryRegions.Remove(region);
            _hasActiveSurfaceDiscovery = false;
            _surfaceDiscoveryPublishIndex = 0;
            _surfaceDiscoveryResults.Clear();

            if (requeue && _queuedSurfaceDiscoveryRegions.Add(region))
                _surfaceDiscoveryQueue.Enqueue(region);
        }

        public void Dispose()
        {
            // Teardown is allowed to synchronize; the per-frame Prepare path never waits for an
            // unfinished discovery job.
            if (_surfaceDiscoveryJobScheduled)
            {
                _surfaceDiscoveryJobHandle.Complete();
                _surfaceDiscoveryJobScheduled = false;
            }
            if (_surfaceDiscoveryResults.IsCreated) _surfaceDiscoveryResults.Dispose();
            if (_surfaceDiscoveryFlags.IsCreated) _surfaceDiscoveryFlags.Dispose();
            if (_surfaceDiscoveryOccupiedWords.IsCreated) _surfaceDiscoveryOccupiedWords.Dispose();
            if (_surfaceDiscoveryFullySolidWords.IsCreated) _surfaceDiscoveryFullySolidWords.Dispose();

            _water.Dispose();
            for (int r = 0; r < _rings.Length; r++) _rings[r].Dispose();
        }

        private static double ElapsedMs(double startSeconds) =>
            (Time.realtimeSinceStartupAsDouble - startSeconds) * 1000.0;
    }
}
