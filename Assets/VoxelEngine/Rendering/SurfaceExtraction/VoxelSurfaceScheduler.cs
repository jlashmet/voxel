using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Storage;
using VoxelEngine.Storage.Api;

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
        private static readonly ProfilerMarker s_PrepareMarker = new("Voxel.Surface.SchedulerPrepare");
        private static readonly ProfilerMarker s_JournalMarker = new("Voxel.Surface.ChangeJournal");
        private static readonly ProfilerMarker s_InvalidationMarker = new("Voxel.Surface.Invalidation");
        private static readonly ProfilerMarker s_DiscoveryMarker = new("Voxel.Surface.Discovery");
        private static readonly ProfilerMarker s_WorkersMarker = new("Voxel.Surface.WorkerAdmission");
        private static readonly ProfilerMarker s_VisibilityMarker = new("Voxel.Surface.Visibility");
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
        private RegionReadSource _readSource;
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
            _readSource ??= new RegionReadSource(in table, in pool);
            _readSource.Refresh(in table, in pool);

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
                    using var resident = _readSource.GetResidentRegionCoords(Allocator.Temp);
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
                for (int r = 0; r < _rings.Length; r++)
                for (int i = 0; i < _rings[r].Workers.Length; i++)
                    _rings[r].Workers[i].InvalidateDirtyRegions(_changedSolidRegions);
                _water.InvalidateDirtyRegions(_changedWaterRegions);
                for (int r = 0; r < _rings.Length; r++)
                for (int i = 0; i < _rings[r].Workers.Length; i++)
                    _rings[r].Workers[i].InvalidateSurfaceBricks(_changedBricks);
                _water.InvalidateSurfaceBricks(_readSource, _changedWaterBricks);
            }
            _invalidationTiming.Add(ElapsedMs(invalidationStart));

            double discoveryStart = Time.realtimeSinceStartupAsDouble;
            using (s_DiscoveryMarker.Auto())
                DiscoverSurfaceBricks(_readSource, _surfaceDiscoveryRegions,
                                      _discoveredSurfaceBricks);
            _discoveryTiming.Add(ElapsedMs(discoveryStart));

            _visibleSolids.Clear();
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
                    // Solid extraction is the final remaining physical-storage reader in
                    // Rendering; it is cut over in the next step of this same branch.
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

            _water.InvalidateSurfaceBricks(_readSource, _discoveredSurfaceBricks);
            _water.Prepare(_readSource, camera, voxelSize, WaterBuildBudgetMs);
            _water.CollectVisible(camera, voxelSize);
            _workerPrepareTiming.Add(ElapsedMs(workersStart) - visibilityMs);
            _visibilityTiming.Add(visibilityMs);
            _prepareTiming.Add(ElapsedMs(prepareStart));
        }

        private static void DiscoverSurfaceBricks(IRegionReadSource storage,
                                                  HashSet<int3> regions,
                                                  List<int3> destination)
        {
            int edge = VoxelReadGrid.BlocksPerRegionEdge;
            int blockCount = edge * edge * edge;
            using var flags = new NativeArray<byte>(blockCount, Allocator.TempJob,
                                                    NativeArrayOptions.UninitializedMemory);
            foreach (int3 regionCoord in regions)
            {
                if (!storage.TryAcquireRegion(regionCoord, out RegionReadView region)) continue;
                int3 origin = regionCoord * edge;
                new SurfaceBrickDiscoveryJob
                {
                    Region = region,
                    IsSurface = flags,
                    Edge = edge,
                }.Schedule(blockCount, 256).Complete();
                for (int i = 0; i < blockCount; i++)
                {
                    if (flags[i] == 0) continue;
                    int bx = i & VoxelReadGrid.BlocksPerRegionEdgeMask;
                    int by = (i >> VoxelReadGrid.BlocksPerRegionEdgeLog2)
                           & VoxelReadGrid.BlocksPerRegionEdgeMask;
                    int bz = i >> (VoxelReadGrid.BlocksPerRegionEdgeLog2 * 2);
                    destination.Add(origin + new int3(bx, by, bz));
                }
            }
        }

        public void Dispose()
        {
            _water.Dispose();
            for (int r = 0; r < _rings.Length; r++) _rings[r].Dispose();
            _readSource = null;
        }

        private static double ElapsedMs(double startSeconds) =>
            (Time.realtimeSinceStartupAsDouble - startSeconds) * 1000.0;
    }
}
