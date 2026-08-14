using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Rendering.SurfaceExtraction
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
            TopologyJobTurnaroundTiming = solids.TopologyTurnaroundTiming;
            TopologyCompactTiming = solids.TopologyCompactTiming;
            FacetedJobTurnaroundTiming = solids.FacetedTurnaroundTiming;
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
                    TopologyJobTurnaroundTiming, worker.TopologyTurnaroundTiming);
                TopologyCompactTiming = VoxelTimingSummary.WorstOf(
                    TopologyCompactTiming, worker.TopologyCompactTiming);
                FacetedJobTurnaroundTiming = VoxelTimingSummary.WorstOf(
                    FacetedJobTurnaroundTiming, worker.FacetedTurnaroundTiming);
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
        private static readonly ProfilerMarker s_PrepareMarker =
            new("Voxel.Surface.SchedulerPrepare");
        private static readonly ProfilerMarker s_JournalMarker =
            new("Voxel.Surface.ChangeJournal");
        private static readonly ProfilerMarker s_InvalidationMarker =
            new("Voxel.Surface.Invalidation");
        private static readonly ProfilerMarker s_DiscoveryMarker =
            new("Voxel.Surface.Discovery");
        private static readonly ProfilerMarker s_WorkersMarker =
            new("Voxel.Surface.WorkerAdmission");
        private static readonly ProfilerMarker s_VisibilityMarker =
            new("Voxel.Surface.Visibility");
        // Each lane owns fixed scratch storage and at most one in-flight chunk. Eight overlaps
        // Burst extraction without saturating the 16-core target as the 12-lane experiment did;
        // memory remains bounded by this constant rather than dirty or resident chunk count.
        public const int SolidWorkerCount = 8;

        /// <summary>
        /// One concentric LOD ring: a shard set that extracts at a fixed stride and serves a
        /// radial band around the viewer.
        ///
        /// Ring N samples every <see cref="CpuTransvoxelChunkCache.SourceStep"/> voxels, so its
        /// chunks span that many times more world while costing the same 64³ extraction. Chunk
        /// coordinates are therefore ring-local: the same world position has a different chunk
        /// coordinate in every ring, which is why each ring owns an independent cache rather
        /// than sharing one keyed by coordinate.
        /// </summary>
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
                        // A ring only draws its own band. The inner cut is what prevents two
                        // rings from both rendering the same terrain and z-fighting; the outer
                        // cut is the ring's share of the total view distance.
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

        /// <summary>
        /// Ring layout, in metres, for 10 cm voxels. Chunk edges are 6.4 m, 12.8 m, 25.6 m,
        /// 51.2 m and 102.4 m respectively, so each ring covers roughly the same number of
        /// chunks despite spanning far more world than the one inside it.
        ///
        /// The two innermost rings sample voxels directly and so require resident bricks. The
        /// outer rings read the mip pyramid, which is what lets them cover ground the client
        /// holds only as a coarse summary. See <see cref="VoxelMipSampler.LevelForStride"/>.
        /// </summary>
        private static readonly (int SourceStep, float Inner, float Outer)[] s_RingLayout =
        {
            (1, 0f, 96f),
            (2, 96f, 192f),
            (4, 192f, 288f),
            (8, 288f, MaxVoxelRingRadiusMetres),
        };

        /// <summary>
        /// Outer limit of voxel-meshed rings, in metres.
        ///
        /// A ring can only mesh chunks whose regions are resident, so this must not exceed the
        /// streaming radius — roughly 410 m for the showcase's eight-region load radius. Rings
        /// past it are pure waste: each one allocates eight shard caches with persistent scratch
        /// and then finds nothing to build. Everything beyond this distance is drawn by the
        /// analytic far-terrain clipmap, which needs no resident regions at all.
        /// </summary>
        public const float MaxVoxelRingRadiusMetres = 420f;
        // Deliberately stops at 1600 m. A ring can only mesh regions that are resident, and a
        // Region costs 1 MB of brick pointers whatever it contains, so covering 4 km with
        // resident regions would run to gigabytes. Terrain beyond the streaming radius is drawn
        // by VoxelFarTerrain from the same analytic height function instead — see its summary.

        private readonly SurfaceRing[] _rings;
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
        private ulong _changeCursor;
        private VoxelChangeJournal _journal;
        private int _lastChangeRecords;
        private readonly VoxelTimingWindow _prepareTiming = new();
        private readonly VoxelTimingWindow _journalTiming = new();
        private readonly VoxelTimingWindow _invalidationTiming = new();
        private readonly VoxelTimingWindow _discoveryTiming = new();
        private readonly VoxelTimingWindow _workerPrepareTiming = new();
        private readonly VoxelTimingWindow _visibilityTiming = new();

        public double SolidBuildBudgetMs { get; set; } = 0.20;
        public double WaterBuildBudgetMs { get; set; } = 0.15;

        public IReadOnlyList<CpuTransvoxelChunkCache.Entry> VisibleSolids => _visibleSolids;
        public IReadOnlyList<CpuWaterSurfaceChunkCache.Entry> VisibleWater => _water.Visible;
        public VoxelSurfaceMetrics Metrics => new(
            AllWorkers(), _water, _lastChangeRecords, _discoveredSurfaceBricks.Count,
            _visibleSolids.Count, _prepareTiming.Snapshot(), _journalTiming.Snapshot(),
            _invalidationTiming.Snapshot(), _discoveryTiming.Snapshot(),
            _workerPrepareTiming.Snapshot(), _visibilityTiming.Snapshot());

        /// <summary>Flattens every ring's shards for metric aggregation.</summary>
        private CpuTransvoxelChunkCache[] AllWorkers()
        {
            var all = new CpuTransvoxelChunkCache[_rings.Length * SolidWorkerCount];
            int n = 0;
            for (int r = 0; r < _rings.Length; r++)
                for (int i = 0; i < _rings[r].Workers.Length; i++)
                    all[n++] = _rings[r].Workers[i];
            return all;
        }

        public VoxelSurfaceScheduler()
        {
            _rings = new SurfaceRing[s_RingLayout.Length];
            for (int i = 0; i < s_RingLayout.Length; i++)
            {
                var layout = s_RingLayout[i];
                _rings[i] = new SurfaceRing(layout.SourceStep, layout.Inner, layout.Outer, 4096);
            }
        }

        public void Prepare(ref RegionTable table, ref BrickPool pool, in MaterialPalette palette,
                            in SurfaceCatalogue surfaceCatalogue,
                            in CoatingCatalogue coatingCatalogue,
                            ProfileBlockStore profileBlocks,
                            VoxelChangeJournal journal, Camera camera, float voxelSize, int frame)
        {
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
                    using var resident = table.GetResidentCoords(Allocator.Temp);
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
                        if (math.any(extent >= VoxelDimensions.RegionVoxelEdge))
                        {
                            if (affectsSolids) _changedSolidRegions.Add(change.Region);
                            if (affectsWater) _changedWaterRegions.Add(change.Region);
                            _surfaceDiscoveryRegions.Add(change.Region);
                            continue;
                        }

                        int3 minBrick = change.MinVoxel >> VoxelDimensions.BrickEdgeLog2;
                        int3 maxBrick = (change.MaxVoxelExclusive - 1)
                                      >> VoxelDimensions.BrickEdgeLog2;
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
                for (int r = 0; r < _rings.Length; r++)
                for (int i = 0; i < _rings[r].Workers.Length; i++)
                    _rings[r].Workers[i].InvalidateDirtyRegions(_changedSolidRegions);
                _water.InvalidateDirtyRegions(_changedWaterRegions);
                for (int r = 0; r < _rings.Length; r++)
                for (int i = 0; i < _rings[r].Workers.Length; i++)
                    _rings[r].Workers[i].InvalidateSurfaceBricks(_changedBricks);
                _water.InvalidateSurfaceBricks(ref table, in pool, _changedWaterBricks);
            }
            _invalidationTiming.Add(ElapsedMs(invalidationStart));
            double discoveryStart = Time.realtimeSinceStartupAsDouble;
            using (s_DiscoveryMarker.Auto())
                DiscoverSurfaceBricks(ref table, in pool, _surfaceDiscoveryRegions,
                                      _discoveredSurfaceBricks);
            _discoveryTiming.Add(ElapsedMs(discoveryStart));
            _visibleSolids.Clear();
            // Jobs overlap on worker threads. Dividing this tiny admission budget by worker
            // count reduced each shard below timer resolution and serialized post-job stages.
            // Each worker gets the configured per-worker main-thread slice; the render pass
            // remains bounded by SolidWorkerCount * SolidBuildBudgetMs in the worst case.
            double workerBudget = SolidBuildBudgetMs;
            double workersStart = Time.realtimeSinceStartupAsDouble;
            double visibilityMs = 0.0;
            using var workersScope = s_WorkersMarker.Auto();
            for (int r = 0; r < _rings.Length; r++)
            {
                SurfaceRing ring = _rings[r];
                for (int i = 0; i < ring.Workers.Length; i++)
                {
                    CpuTransvoxelChunkCache worker = ring.Workers[i];
                    worker.InvalidateSurfaceBricks(_discoveredSurfaceBricks);
                    worker.Prepare(ref table, in pool, in palette, in surfaceCatalogue,
                                   in coatingCatalogue, profileBlocks, camera, voxelSize, frame,
                                   workerBudget);
                    double visibilityStart = Time.realtimeSinceStartupAsDouble;
                    IReadOnlyList<CpuTransvoxelChunkCache.Entry> visible;
                    using (s_VisibilityMarker.Auto())
                        visible = worker.CollectVisible(camera, voxelSize, frame);
                    visibilityMs += ElapsedMs(visibilityStart);
                    for (int j = 0; j < visible.Count; j++) _visibleSolids.Add(visible[j]);
                }
            }

            _water.InvalidateSurfaceBricks(ref table, in pool, _discoveredSurfaceBricks);
            _water.Prepare(ref table, in pool, camera, voxelSize, WaterBuildBudgetMs);
            _water.CollectVisible(camera, voxelSize);
            _workerPrepareTiming.Add(ElapsedMs(workersStart) - visibilityMs);
            _visibilityTiming.Add(visibilityMs);
            _prepareTiming.Add(ElapsedMs(prepareStart));
        }

        private static void DiscoverSurfaceBricks(ref RegionTable table, in BrickPool pool,
                                                  HashSet<int3> regions,
                                                  List<int3> destination)
        {
            using var flags = new NativeArray<byte>(VoxelDimensions.BricksPerRegion,
                                                    Allocator.TempJob,
                                                    NativeArrayOptions.UninitializedMemory);
            foreach (int3 regionCoord in regions)
            {
                if (!table.TryGetRegion(regionCoord, out Region region)) continue;
                NativeArray<BrickRef> refs = region.BrickRefs;
                int edge = VoxelDimensions.RegionEdge;
                int3 origin = regionCoord * edge;
                new SurfaceBrickDiscoveryJob
                {
                    Bricks = refs,
                    Occupancy = pool.Occupancy,
                    IsSurface = flags,
                    Edge = edge,
                    OccupancyWordsPerBrick = VoxelDimensions.OccupancyWordsPerBrick,
                }.Schedule(refs.Length, 256).Complete();
                for (int i = 0; i < refs.Length; i++)
                {
                    if (flags[i] == 0) continue;
                    int bx = i & VoxelDimensions.RegionEdgeMask;
                    int by = (i >> VoxelDimensions.RegionEdgeLog2)
                           & VoxelDimensions.RegionEdgeMask;
                    int bz = i >> (VoxelDimensions.RegionEdgeLog2 * 2);
                    destination.Add(origin + new int3(bx, by, bz));
                }
            }
        }

        public void Dispose()
        {
            _water.Dispose();
            for (int r = 0; r < _rings.Length; r++) _rings[r].Dispose();
        }

        private static double ElapsedMs(double startSeconds) =>
            (Time.realtimeSinceStartupAsDouble - startSeconds) * 1000.0;
    }
}
