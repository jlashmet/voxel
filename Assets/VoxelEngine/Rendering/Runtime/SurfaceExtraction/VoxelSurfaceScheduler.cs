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
        public readonly int Step4KnownChunks;
        public readonly int Step4ResidentChunks;
        public readonly int Step4DirtyChunks;
        public readonly int Step4MissingVisibleChunks;
        public readonly int Step4RunningJobs;
        public readonly uint Step4BuildPhaseMask;
        public readonly uint Step4ActiveJobMask;
        public readonly int RunningGeometryJobs;
        public readonly ulong FramePathBlockingCompletionViolations;
        public readonly long LastFrameManagedAllocationBytes;
        public readonly int SolidMeshesAwaitingUpload;
        public readonly long SolidPendingUploadBytes;
        public readonly int SolidUploadBudgetBytes;
        public readonly int LastFrameSolidUploadedBytes;
        public readonly int LastFrameSolidUploadCompletions;
        public readonly long SolidArenaCommittedBytes;
        public readonly long SolidArenaUsedBytes;
        public readonly int SolidArenaActiveLeases;
        public readonly ulong SolidArenaAllocationFailures;
        public readonly ulong SolidArenaPressureEvictions;
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
            bool isStep4 = solids.SourceStep == 4;
            Step4KnownChunks = isStep4 ? solids.KnownCount : 0;
            Step4ResidentChunks = isStep4 ? solids.ResidentCount : 0;
            Step4DirtyChunks = isStep4 ? solids.DirtyCount : 0;
            Step4MissingVisibleChunks = isStep4 ? solids.MissingVisibleCount : 0;
            Step4RunningJobs = isStep4 ? solids.RunningJobCount : 0;
            Step4BuildPhaseMask = isStep4 && solids.ActiveBuildPhase >= 0
                ? 1u << solids.ActiveBuildPhase : 0u;
            Step4ActiveJobMask = isStep4 ? solids.ActiveJobMask : 0u;
            RunningGeometryJobs = solids.RunningJobCount + water.RunningJobCount;
            FramePathBlockingCompletionViolations =
                solids.FramePathBlockingCompletionViolations
                + water.FramePathBlockingCompletionViolations;
            LastFrameManagedAllocationBytes = 0;
            SolidMeshesAwaitingUpload = solids.PendingUploadCount;
            SolidPendingUploadBytes = solids.PendingUploadBytes;
            SolidUploadBudgetBytes = int.MaxValue;
            LastFrameSolidUploadedBytes = 0;
            LastFrameSolidUploadCompletions = 0;
            SolidArenaCommittedBytes = 0;
            SolidArenaUsedBytes = 0;
            SolidArenaActiveLeases = 0;
            SolidArenaAllocationFailures = 0;
            SolidArenaPressureEvictions = 0;
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
                                     long solidArenaCommittedBytes,
                                     long solidArenaUsedBytes,
                                     int solidArenaActiveLeases,
                                     ulong solidArenaAllocationFailures,
                                     ulong solidArenaPressureEvictions,
                                     in VoxelTimingSummary schedulerPrepare,
                                     in VoxelTimingSummary journal,
                                     in VoxelTimingSummary invalidation,
                                     in VoxelTimingSummary discovery,
                                     in VoxelTimingSummary workerPrepare,
                                     in VoxelTimingSummary visibility,
                                     int schedulerRunningJobs,
                                     ulong schedulerCompletionViolations,
                                     long lastFrameManagedAllocationBytes)
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
            int step4Known = 0, step4Resident = 0, step4Dirty = 0, step4Missing = 0, step4Running = 0;
            uint step4BuildPhaseMask = 0, step4ActiveJobMask = 0;
            long pendingUploadBytes = 0;
            ulong completed = 0, stale = 0, uploadedBytes = water.UploadedGeometryBytes;
            ulong decorations = 0, pressure = 0;
            ulong completionViolations = water.FramePathBlockingCompletionViolations;
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
                if (worker.SourceStep == 4)
                {
                    step4Known += worker.KnownCount;
                    step4Resident += worker.ResidentCount;
                    step4Dirty += worker.DirtyCount;
                    step4Missing += worker.MissingVisibleCount;
                    step4Running += worker.RunningJobCount;
                    if (worker.ActiveBuildPhase >= 0)
                        step4BuildPhaseMask |= 1u << worker.ActiveBuildPhase;
                    step4ActiveJobMask |= worker.ActiveJobMask;
                }
                uploads += worker.PendingUploadCount;
                pendingUploadBytes += worker.PendingUploadBytes;
                completed += worker.CompletedBuildCount;
                stale += worker.StaleBuildCount;
                uploadedBytes += worker.UploadedGeometryBytes;
                decorations += worker.CompletedDecorationClumps;
                pressure += worker.CapacityPressureCount;
                completionViolations += worker.FramePathBlockingCompletionViolations;
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
            Step4KnownChunks = step4Known;
            Step4ResidentChunks = step4Resident;
            Step4DirtyChunks = step4Dirty;
            Step4MissingVisibleChunks = step4Missing;
            Step4RunningJobs = step4Running;
            Step4BuildPhaseMask = step4BuildPhaseMask;
            Step4ActiveJobMask = step4ActiveJobMask;
            RunningGeometryJobs = running + water.RunningJobCount + schedulerRunningJobs;
            FramePathBlockingCompletionViolations =
                completionViolations + schedulerCompletionViolations;
            LastFrameManagedAllocationBytes = lastFrameManagedAllocationBytes;
            SolidMeshesAwaitingUpload = uploads;
            SolidPendingUploadBytes = pendingUploadBytes;
            SolidUploadBudgetBytes = solidUploadBudgetBytes;
            LastFrameSolidUploadedBytes = lastFrameSolidUploadedBytes;
            LastFrameSolidUploadCompletions = lastFrameSolidUploadCompletions;
            SolidArenaCommittedBytes = solidArenaCommittedBytes;
            SolidArenaUsedBytes = solidArenaUsedBytes;
            SolidArenaActiveLeases = solidArenaActiveLeases;
            SolidArenaAllocationFailures = solidArenaAllocationFailures;
            SolidArenaPressureEvictions = solidArenaPressureEvictions;
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
        public const int NearSolidWorkerCount = 8;
        [Obsolete("Use NearSolidWorkerCount for the base ring or WorkerCountForSourceStep for a specific LOD.")]
        public const int SolidWorkerCount = NearSolidWorkerCount;

        /// <summary>
        /// Build workspaces are deliberately not uniform across LODs. Exact-sampling snapshot
        /// storage grows with the cube of SourceStep (step 8 has a 66^3 padded brick cache), while
        /// the number of chunks needed to cover a coarse ring falls sharply. Keeping eight giant
        /// caches in the outer ring wastes tens of megabytes of persistent scratch and increases
        /// memory pressure without increasing the renderer-wide frame budget.
        /// </summary>
        public static int WorkerCountForSourceStep(int sourceStep) => sourceStep switch
        {
            <= 2 => NearSolidWorkerCount,
            4 => 4,
            _ => 2,
        };

        private sealed class SurfaceRing : IDisposable
        {
            public readonly int SourceStep;
            public readonly float InnerRadiusMetres;
            public readonly float OuterRadiusMetres;
            public readonly CpuTransvoxelChunkCache[] Workers;
            private readonly SurfaceChunkSlotGrid _slotGrid = new();
            public int3 ClipmapCentre { get; private set; }
            public int ClipmapRadius { get; private set; }
            public bool HasClipmapWindow { get; private set; }
            public int3 ClipmapRegionMin { get; private set; }
            public int3 ClipmapRegionMaxExclusive { get; private set; }
            public int ActiveSlotCount => _slotGrid.ActiveCount;
            public int3 ActiveSlotCoordinate(int index) => _slotGrid.ActiveCoordinateAt(index);

            public SurfaceRing(int sourceStep, float innerRadiusMetres, float outerRadiusMetres,
                               int maxResidentChunks, SurfaceGeometryArena geometryArena,
                               TransvoxelLookupTables lookupTables)
            {
                SourceStep = sourceStep;
                InnerRadiusMetres = innerRadiusMetres;
                OuterRadiusMetres = outerRadiusMetres;
                Workers = new CpuTransvoxelChunkCache[WorkerCountForSourceStep(sourceStep)];
                for (int i = 0; i < Workers.Length; i++)
                {
                    Workers[i] = new CpuTransvoxelChunkCache(
                        sourceStep, geometryArena, lookupTables, _slotGrid)
                    {
                        ShardIndex = i,
                        ShardCount = Workers.Length,
                        MaxResidentChunks = maxResidentChunks / Workers.Length,
                        MinViewDistanceMetres = innerRadiusMetres,
                        MaxViewDistanceMetres = outerRadiusMetres,
                    };
                }
            }

            public void UpdateClipmapWindow(Vector3 cameraPosition, float voxelSize)
            {
                UpdateClipmapWindow(cameraPosition, voxelSize, out _, out _, out _, out _, out _);
            }

            public bool UpdateClipmapWindow(Vector3 cameraPosition, float voxelSize,
                                            out bool hadPrevious,
                                            out int3 previousRegionMin,
                                            out int3 previousRegionMaxExclusive,
                                            out int3 currentRegionMin,
                                            out int3 currentRegionMaxExclusive)
            {
                hadPrevious = HasClipmapWindow;
                previousRegionMin = ClipmapRegionMin;
                previousRegionMaxExclusive = ClipmapRegionMaxExclusive;

                float chunkMetres = CpuTransvoxelChunkCache.CellsPerAxis * SourceStep * voxelSize;
                int radius = Mathf.CeilToInt(OuterRadiusMetres / chunkMetres) + 1;
                int3 centre = new(
                    Mathf.FloorToInt(cameraPosition.x / chunkMetres),
                    Mathf.FloorToInt(cameraPosition.y / chunkMetres),
                    Mathf.FloorToInt(cameraPosition.z / chunkMetres));
                int voxelsPerChunk = CpuTransvoxelChunkCache.CellsPerAxis * SourceStep;
                int3 minChunk = centre - radius;
                int3 maxChunkExclusive = centre + radius + 1;
                int3 minVoxel = minChunk * voxelsPerChunk;
                int3 maxVoxelExclusive = maxChunkExclusive * voxelsPerChunk;

                currentRegionMin = FloorDiv(minVoxel, VoxelGrid.RegionVoxelEdge);
                currentRegionMaxExclusive = FloorDiv(
                    maxVoxelExclusive - 1, VoxelGrid.RegionVoxelEdge) + 1;

                ClipmapCentre = centre;
                ClipmapRadius = radius;
                ClipmapRegionMin = currentRegionMin;
                ClipmapRegionMaxExclusive = currentRegionMaxExclusive;
                HasClipmapWindow = true;
                for (int i = 0; i < Workers.Length; i++)
                    Workers[i].SetClipmapWindow(centre, radius);

                return !hadPrevious
                    || math.any(previousRegionMin != currentRegionMin)
                    || math.any(previousRegionMaxExclusive != currentRegionMaxExclusive);
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

        public const float MaxVoxelRingRadiusMetres = 409.6f;
        // Allocated once with the scheduler. Runtime streaming may wait for a free range but
        // cannot grow these buffers and create a render-thread GPU allocation spike.
        private const int SurfaceArenaVertexCapacity = 2 * 1024 * 1024;
        private const int SurfaceArenaIndexCapacity = 6 * 1024 * 1024;
        public const int SurfaceArenaDrawCapacity = 16 * 1024;

        private readonly SurfaceGeometryArena _geometryArena;
        private readonly TransvoxelLookupTables _lookupTables;
        private readonly SurfaceRing[] _rings;
        private readonly CpuTransvoxelChunkCache[] _allWorkers;
        private readonly List<CpuTransvoxelChunkCache.Entry> _visibleSolids = new(256);
        private readonly Plane[] _visibilityFrustumPlanes = new Plane[6];
        private int _lastVisibilityCandidateChecks;
        private readonly CpuWaterSurfaceChunkCache _water = new();
        private const int ChangeReadRecordsPerFrame = 64;
        private const int ChangeBrickExpansionsPerFrame = 256;
        private const int ChangeRecoverySlotsPerFrame = 32;
        private readonly List<VoxelChangeRecord> _changeScratch = new(ChangeReadRecordsPerFrame);
        private NativeArray<int3> _changeRecoveryRegions;
        private int _changeRecordIndex;
        private bool _changeFeedHasMore;
        private bool _recoveringChangeOverflow;
        private int _changeRecoveryCursor;
        private bool _changeExpansionActive;
        private int3 _changeExpansionMinBrick;
        private int3 _changeExpansionCounts;
        private int _changeExpansionCursor;
        private bool _changeExpansionAffectsSolids;
        private bool _changeExpansionAffectsWater;
        private readonly HashSet<int3> _changedSolidRegions = new();
        private readonly HashSet<int3> _changedWaterRegions = new();
        private readonly HashSet<int3> _changedBrickSet = new();
        private readonly List<int3> _changedBricks = new(ChangeBrickExpansionsPerFrame);
        private readonly HashSet<int3> _changedWaterBrickSet = new();
        private readonly List<int3> _changedWaterBricks = new(ChangeBrickExpansionsPerFrame);
        private readonly HashSet<int3> _surfaceDiscoveryRegions = new();
        private readonly List<int3> _discoveredSurfaceBricks = new(512);
        private readonly Queue<int3> _surfaceDiscoveryQueue = new();
        private readonly HashSet<int3> _queuedSurfaceDiscoveryRegions = new();
        private readonly HashSet<int3> _surfaceDiscoveryRescanRegions = new();

        private readonly struct ClipmapRegionBox
        {
            public readonly int3 Min;
            public readonly int3 MaxExclusive;

            public ClipmapRegionBox(int3 min, int3 maxExclusive)
            {
                Min = min;
                MaxExclusive = maxExclusive;
            }
        }

        private const int ClipmapAdmissionRegionsPerFrame = 64;
        private readonly Queue<ClipmapRegionBox> _clipmapAdmissionQueue = new();
        private ClipmapRegionBox _activeClipmapAdmissionBox;
        private int _activeClipmapAdmissionCursor;
        private bool _hasActiveClipmapAdmission;
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
        private int _arenaPressureCursor;
        private ulong _observedArenaAllocationFailures;
        private ulong _arenaPressureEvictions;
        private int _lastAdvancedFrame = -1;
        private readonly VoxelTimingWindow _prepareTiming = new();
        private readonly VoxelTimingWindow _journalTiming = new();
        private readonly VoxelTimingWindow _invalidationTiming = new();
        private readonly VoxelTimingWindow _discoveryTiming = new();
        private readonly VoxelTimingWindow _workerPrepareTiming = new();
        private readonly VoxelTimingWindow _visibilityTiming = new();
        private ulong _framePathBlockingCompletionViolations;
        private long _lastFrameManagedAllocationBytes;

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
        public int LastAdvancedFrame => _lastAdvancedFrame;
        public int SolidBuildWorkspaceCount => _allWorkers.Length;
        public int LastVisibilityCandidateChecks => _lastVisibilityCandidateChecks;
        public long LastFrameManagedAllocationBytes => _lastFrameManagedAllocationBytes;
        public int SolidArenaMaxActiveLeases
        {
            get => _geometryArena.MaxActiveLeases;
            set => _geometryArena.MaxActiveLeases = value;
        }
        internal int KnownChunkCountForSourceStep(int sourceStep)
        {
            int count = 0;
            for (int r = 0; r < _rings.Length; r++)
            {
                if (_rings[r].SourceStep != sourceStep) continue;
                for (int w = 0; w < _rings[r].Workers.Length; w++)
                    count += _rings[r].Workers[w].KnownCount;
            }
            return count;
        }
        public bool ChangeFeedBacklogged => _changeFeedHasMore
            || _changeRecordIndex < _changeScratch.Count || _changeExpansionActive;
        public bool RecoveringChangeFeedOverflow => _recoveringChangeOverflow;
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
        /// <summary>Maximum water geometry payload copied into its fixed arena per frame.</summary>
        public int WaterUploadBudgetBytes { get; set; } = 256 * 1024;
        /// <summary>Wall-clock gate for the single water publication slice.</summary>
        public double WaterUploadBudgetMs { get; set; } = 0.10;
        public int LastFrameWaterUploadedBytes { get; private set; }
        private ulong _observedWaterArenaAllocationFailures;

        public IReadOnlyList<CpuTransvoxelChunkCache.Entry> VisibleSolids => _visibleSolids;
        public IReadOnlyList<CpuWaterSurfaceChunkCache.Entry> VisibleWater => _water.Visible;
        public VoxelSurfaceMetrics Metrics => new(
            _allWorkers, _water, _lastChangeRecords, _discoveredSurfaceBricks.Count,
            _visibleSolids.Count, SolidUploadBudgetBytes, _lastFrameSolidUploadedBytes,
            _lastFrameSolidUploadCompletions, _geometryArena.CommittedGpuBytes,
            _geometryArena.UsedGpuBytes, _geometryArena.UsedArgsRecords,
            _geometryArena.AllocationFailureCount,
            _arenaPressureEvictions, _prepareTiming.Snapshot(), _journalTiming.Snapshot(),
            _invalidationTiming.Snapshot(), _discoveryTiming.Snapshot(),
            _workerPrepareTiming.Snapshot(), _visibilityTiming.Snapshot(),
            _surfaceDiscoveryJobScheduled ? 1 : 0,
            _framePathBlockingCompletionViolations,
            _lastFrameManagedAllocationBytes);

        public VoxelSurfaceScheduler()
        {
            _geometryArena = new SurfaceGeometryArena(SurfaceArenaVertexCapacity,
                                                       SurfaceArenaIndexCapacity,
                                                       SurfaceArenaDrawCapacity);
            _lookupTables = new TransvoxelLookupTables();
            _rings = new SurfaceRing[s_RingLayout.Length];
            int totalWorkers = 0;
            for (int i = 0; i < s_RingLayout.Length; i++)
                totalWorkers += WorkerCountForSourceStep(s_RingLayout[i].SourceStep);
            _allWorkers = new CpuTransvoxelChunkCache[totalWorkers];
            int workerIndex = 0;
            for (int i = 0; i < s_RingLayout.Length; i++)
            {
                var layout = s_RingLayout[i];
                SurfaceRing ring = new(layout.SourceStep, layout.Inner, layout.Outer,
                                           4096, _geometryArena, _lookupTables);
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
            _changeRecoveryRegions = new NativeArray<int3>(
                ChangeRecoverySlotsPerFrame, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
        }

        public void Prepare(IRegionReadSource storage, in MaterialPaletteView palette,
                            in SurfaceCatalogueView surfaceCatalogue,
                            in CoatingCatalogueView coatingCatalogue,
                            IProfileBlockReadSource profileBlocks,
                            IVoxelChangeSource journal, Camera camera, float voxelSize, int frame)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));

            // RenderGraph records this pass once per camera. Geometry derivation is world/frame
            // work, not camera work: a second camera in the same Unity frame may change only
            // visibility, never consume another journal/build/upload budget.
            if (_lastAdvancedFrame == frame)
            {
                CollectVisibility(camera, voxelSize, frame);
                return;
            }

            // Measure only the once-per-world-frame geometry orchestration path. Secondary
            // camera visibility collection is intentionally excluded: this counter answers the
            // merge-gate question "did streaming/geometry allocate after warmup?" without being
            // polluted by unrelated camera/test-runner allocations.
            long managedAllocationStart = GC.GetAllocatedBytesForCurrentThread();
            _lastAdvancedFrame = frame;

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

            if (camera != null)
            {
                Vector3 cameraPosition = camera.transform.position;
                bool clipmapMoved = false;
                for (int r = 0; r < _rings.Length; r++)
                {
                    SurfaceRing ring = _rings[r];
                    bool changed = ring.UpdateClipmapWindow(
                        cameraPosition, voxelSize,
                        out bool hadPrevious,
                        out int3 previousMin,
                        out int3 previousMaxExclusive,
                        out int3 currentMin,
                        out int3 currentMaxExclusive);
                    if (!changed || !hadPrevious) continue;
                    clipmapMoved = true;
                    EnqueueClipmapRegionDifference(
                        previousMin, previousMaxExclusive,
                        currentMin, currentMaxExclusive);
                }

                if (clipmapMoved)
                    AddImmediateCameraDiscoveryRegions(storage, cameraPosition, voxelSize);
                StepClipmapAdmissionDiscovery(storage);
            }

            double journalStart = Time.realtimeSinceStartupAsDouble;
            using (s_JournalMarker.Auto())
                ProcessChangeFeed(storage, journal);
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

            // Arena exhaustion is backpressure, never a reason to allocate another GPU
            // buffer. Reclaim at most one old offscreen lease per frame; the pending build then
            // retries publication on a later frame while its previous geometry remains visible.
            ulong arenaFailures = _geometryArena.AllocationFailureCount;
            if (arenaFailures > _observedArenaAllocationFailures && workerCount > 0)
            {
                _observedArenaAllocationFailures = arenaFailures;
                for (int offset = 0; offset < workerCount; offset++)
                {
                    int index = (_arenaPressureCursor + offset) % workerCount;
                    if (!_allWorkers[index].TryEvictOneForArenaPressure(camera, voxelSize))
                        continue;
                    _arenaPressureCursor = (index + 1) % workerCount;
                    _arenaPressureEvictions++;
                    break;
                }
            }

            _water.InvalidateSurfaceBricks(storage, _discoveredSurfaceBricks);
            _water.Prepare(storage, camera, voxelSize, WaterBuildBudgetMs);
            LastFrameWaterUploadedBytes = 0;
            double waterUploadDeadline = Time.realtimeSinceStartupAsDouble
                                       + Math.Max(0.0, WaterUploadBudgetMs) * 0.001;
            if (_water.PendingUploadCount > 0 && WaterUploadBudgetBytes > 0
                && Time.realtimeSinceStartupAsDouble < waterUploadDeadline)
            {
                _water.TryPublishPending(WaterUploadBudgetBytes,
                                         out int waterUploadedBytes);
                LastFrameWaterUploadedBytes = waterUploadedBytes;
            }
            ulong waterArenaFailures = _water.ArenaAllocationFailures;
            if (waterArenaFailures > _observedWaterArenaAllocationFailures)
            {
                _observedWaterArenaAllocationFailures = waterArenaFailures;
                _water.TryEvictOneForArenaPressure(camera, voxelSize);
            }
            _workerPrepareTiming.Add(workerPrepareMs);

            // Geometry jobs are intentionally never completed while unfinished. Explicitly flush
            // the once-per-world-frame batch after all solid/discovery/water scheduling so jobs
            // cannot remain buffered waiting for an unrelated Unity subsystem to force dispatch.
            // ScheduleBatchedJobs is non-blocking; readiness is still polled on later frames.
            JobHandle.ScheduleBatchedJobs();

            CollectVisibility(camera, voxelSize, frame);
            _prepareTiming.Add(ElapsedMs(prepareStart));
            _lastFrameManagedAllocationBytes = Math.Max(
                0L, GC.GetAllocatedBytesForCurrentThread() - managedAllocationStart);
        }

        /// <summary>
        /// Camera-specific half of scheduling. This may run multiple times in one Unity
        /// frame; it never consumes change, extraction, water-build, or GPU-upload budgets.
        /// </summary>
        private void CollectVisibility(Camera camera, float voxelSize, int frame)
        {
            _visibleSolids.Clear();
            _lastVisibilityCandidateChecks = 0;
            double visibilityStart = Time.realtimeSinceStartupAsDouble;
            using (s_VisibilityMarker.Auto())
            {
                if (camera != null)
                {
                    GeometryUtility.CalculateFrustumPlanes(camera, _visibilityFrustumPlanes);
                    Vector3 cameraPosition = camera.transform.position;
                    for (int r = 0; r < _rings.Length; r++)
                    {
                        SurfaceRing ring = _rings[r];
                        for (int w = 0; w < ring.Workers.Length; w++)
                            ring.Workers[w].BeginVisibilityCollection();

                        if (!ring.HasClipmapWindow)
                            ring.UpdateClipmapWindow(cameraPosition, voxelSize);
                        int radius = ring.ClipmapRadius;
                        int3 centre = ring.ClipmapCentre;

                        // The ring's toroidal grid already knows exactly which clipmap cells own
                        // discovered surface chunks. Walk that dense active list rather than the
                        // entire (2r+1)^3 coordinate volume. Outgoing slots can remain active for
                        // a few frames while retirement is sliced; skip them against the current
                        // window so delayed cleanup never draws stale residency.
                        int activeSlots = ring.ActiveSlotCount;
                        for (int slotIndex = 0; slotIndex < activeSlots; slotIndex++)
                        {
                            int3 coordinate = ring.ActiveSlotCoordinate(slotIndex);
                            int3 delta = math.abs(coordinate - centre);
                            if (math.cmax(delta) > radius) continue;

                            int shard = CpuTransvoxelChunkCache.ShardForChunk(
                                coordinate, ring.Workers.Length);
                            ring.Workers[shard].CollectVisibleCoordinate(
                                coordinate, _visibilityFrustumPlanes, cameraPosition,
                                voxelSize, frame);
                            _lastVisibilityCandidateChecks++;
                        }

                        for (int w = 0; w < ring.Workers.Length; w++)
                        {
                            IReadOnlyList<CpuTransvoxelChunkCache.Entry> visible =
                                ring.Workers[w].Visible;
                            for (int i = 0; i < visible.Count; i++)
                                _visibleSolids.Add(visible[i]);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < _allWorkers.Length; i++)
                        _allWorkers[i].BeginVisibilityCollection();
                }

                _water.CollectVisible(camera, voxelSize);
            }
            _visibilityTiming.Add(ElapsedMs(visibilityStart));
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static int3 FloorDiv(int3 value, int divisor) => new(
            FloorDiv(value.x, divisor),
            FloorDiv(value.y, divisor),
            FloorDiv(value.z, divisor));

        private void AddImmediateCameraDiscoveryRegions(IRegionReadSource storage,
                                                        Vector3 cameraPosition,
                                                        float voxelSize)
        {
            float safeVoxelSize = math.max(1e-6f, voxelSize);
            int3 cameraVoxel = new(
                Mathf.FloorToInt(cameraPosition.x / safeVoxelSize),
                Mathf.FloorToInt(cameraPosition.y / safeVoxelSize),
                Mathf.FloorToInt(cameraPosition.z / safeVoxelSize));
            int3 cameraRegion = FloorDiv(cameraVoxel, VoxelGrid.RegionVoxelEdge);
            for (int z = -1; z <= 1; z++)
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                int3 region = cameraRegion + new int3(x, y, z);
                if (storage.IsRegionResident(region))
                    _surfaceDiscoveryRegions.Add(region);
            }
        }

        private void EnqueueClipmapRegionDifference(int3 oldMin, int3 oldMaxExclusive,
                                                    int3 newMin, int3 newMaxExclusive)
        {
            int3 overlapMin = math.max(oldMin, newMin);
            int3 overlapMax = math.min(oldMaxExclusive, newMaxExclusive);
            if (math.any(overlapMin >= overlapMax))
            {
                EnqueueClipmapRegionBox(newMin, newMaxExclusive);
                return;
            }

            // X slabs own the full Y/Z span. Y slabs are restricted to the overlapping X span,
            // and Z slabs to overlapping X/Y, making the six boxes disjoint.
            EnqueueClipmapRegionBox(
                newMin, new int3(overlapMin.x, newMaxExclusive.y, newMaxExclusive.z));
            EnqueueClipmapRegionBox(
                new int3(overlapMax.x, newMin.y, newMin.z), newMaxExclusive);
            EnqueueClipmapRegionBox(
                new int3(overlapMin.x, newMin.y, newMin.z),
                new int3(overlapMax.x, overlapMin.y, newMaxExclusive.z));
            EnqueueClipmapRegionBox(
                new int3(overlapMin.x, overlapMax.y, newMin.z),
                new int3(overlapMax.x, newMaxExclusive.y, newMaxExclusive.z));
            EnqueueClipmapRegionBox(
                new int3(overlapMin.x, overlapMin.y, newMin.z),
                new int3(overlapMax.x, overlapMax.y, overlapMin.z));
            EnqueueClipmapRegionBox(
                new int3(overlapMin.x, overlapMin.y, overlapMax.z),
                new int3(overlapMax.x, overlapMax.y, newMaxExclusive.z));
        }

        private void EnqueueClipmapRegionBox(int3 min, int3 maxExclusive)
        {
            if (math.any(min >= maxExclusive)) return;
            _clipmapAdmissionQueue.Enqueue(new ClipmapRegionBox(min, maxExclusive));
        }

        private void StepClipmapAdmissionDiscovery(IRegionReadSource storage)
        {
            int remaining = ClipmapAdmissionRegionsPerFrame;
            while (remaining > 0)
            {
                if (!_hasActiveClipmapAdmission)
                {
                    if (_clipmapAdmissionQueue.Count == 0) return;
                    _activeClipmapAdmissionBox = _clipmapAdmissionQueue.Dequeue();
                    _activeClipmapAdmissionCursor = 0;
                    _hasActiveClipmapAdmission = true;
                }

                int3 counts = _activeClipmapAdmissionBox.MaxExclusive
                            - _activeClipmapAdmissionBox.Min;
                int total = counts.x * counts.y * counts.z;
                while (remaining > 0 && _activeClipmapAdmissionCursor < total)
                {
                    int linear = _activeClipmapAdmissionCursor++;
                    int x = linear % counts.x;
                    int y = (linear / counts.x) % counts.y;
                    int z = linear / (counts.x * counts.y);
                    int3 region = _activeClipmapAdmissionBox.Min + new int3(x, y, z);
                    remaining--;
                    if (storage.IsRegionResident(region))
                        _surfaceDiscoveryRegions.Add(region);
                }

                if (_activeClipmapAdmissionCursor < total) return;
                _hasActiveClipmapAdmission = false;
                _activeClipmapAdmissionCursor = 0;
            }
        }

        private void ProcessChangeFeed(IRegionReadSource storage, IVoxelChangeSource journal)
        {
            _lastChangeRecords = 0;
            if (journal == null)
            {
                ResetChangeFeedState(null);
                return;
            }

            if (!ReferenceEquals(journal, _journal))
                ResetChangeFeedState(journal);

            if (_recoveringChangeOverflow)
            {
                StepChangeOverflowRecovery(storage);
                return;
            }

            if (_changeRecordIndex >= _changeScratch.Count)
            {
                _changeScratch.Clear();
                _changeRecordIndex = 0;
                _changeExpansionActive = false;
                bool incremental = journal.ReadSince(
                    ref _changeCursor, _changeScratch, ChangeReadRecordsPerFrame,
                    out _changeFeedHasMore);
                if (!incremental)
                {
                    _changeScratch.Clear();
                    _changeRecordIndex = 0;
                    _changeExpansionActive = false;
                    _changeFeedHasMore = false;
                    _recoveringChangeOverflow = true;
                    _changeRecoveryCursor = 0;
                    StepChangeOverflowRecovery(storage);
                    return;
                }
            }

            StepChangeRecords();
        }

        private void ResetChangeFeedState(IVoxelChangeSource journal)
        {
            _journal = journal;
            _changeCursor = 0;
            _changeScratch.Clear();
            _changeRecordIndex = 0;
            _changeFeedHasMore = false;
            _recoveringChangeOverflow = false;
            _changeRecoveryCursor = 0;
            _changeExpansionActive = false;
            _changeExpansionCursor = 0;
        }

        private void StepChangeOverflowRecovery(IRegionReadSource storage)
        {
            bool complete = storage.CopyResidentRegionCoords(
                ref _changeRecoveryCursor, _changeRecoveryRegions, out int count);
            for (int i = 0; i < count; i++)
            {
                int3 region = _changeRecoveryRegions[i];
                _changedSolidRegions.Add(region);
                _changedWaterRegions.Add(region);
                _surfaceDiscoveryRegions.Add(region);
            }

            if (!complete) return;
            _recoveringChangeOverflow = false;
            _changeRecoveryCursor = 0;
        }

        private void StepChangeRecords()
        {
            int brickBudget = ChangeBrickExpansionsPerFrame;
            int recordBudget = ChangeReadRecordsPerFrame;
            while (_changeRecordIndex < _changeScratch.Count && recordBudget > 0)
            {
                VoxelChangeRecord change = _changeScratch[_changeRecordIndex];
                if (!_changeExpansionActive)
                {
                    bool affectsSolids = (change.Kind & (VoxelChangeKind.Occupancy
                        | VoxelChangeKind.BaseMaterial | VoxelChangeKind.SurfaceStyle
                        | VoxelChangeKind.Coating | VoxelChangeKind.Residency)) != 0;
                    bool affectsWater = (change.Kind & (VoxelChangeKind.Occupancy
                        | VoxelChangeKind.BaseMaterial | VoxelChangeKind.Water
                        | VoxelChangeKind.Residency)) != 0;
                    int3 extent = change.MaxVoxelExclusive - change.MinVoxel;
                    if (!affectsSolids && !affectsWater || math.any(extent <= 0))
                    {
                        _changeRecordIndex++;
                        _lastChangeRecords++;
                        recordBudget--;
                        continue;
                    }

                    if (math.any(extent >= VoxelGrid.RegionVoxelEdge))
                    {
                        if (affectsSolids) _changedSolidRegions.Add(change.Region);
                        if (affectsWater) _changedWaterRegions.Add(change.Region);
                        _surfaceDiscoveryRegions.Add(change.Region);
                        _changeRecordIndex++;
                        _lastChangeRecords++;
                        recordBudget--;
                        continue;
                    }

                    int3 minBrick = change.MinVoxel >> VoxelReadGrid.BlockEdgeLog2;
                    int3 maxBrick = (change.MaxVoxelExclusive - 1)
                                  >> VoxelReadGrid.BlockEdgeLog2;
                    _changeExpansionMinBrick = minBrick;
                    _changeExpansionCounts = maxBrick - minBrick + 1;
                    _changeExpansionCursor = 0;
                    _changeExpansionAffectsSolids = affectsSolids;
                    _changeExpansionAffectsWater = affectsWater;
                    _changeExpansionActive = true;
                }

                int total = _changeExpansionCounts.x
                          * _changeExpansionCounts.y
                          * _changeExpansionCounts.z;
                while (_changeExpansionCursor < total && brickBudget > 0)
                {
                    int linear = _changeExpansionCursor++;
                    int x = linear % _changeExpansionCounts.x;
                    int y = (linear / _changeExpansionCounts.x) % _changeExpansionCounts.y;
                    int z = linear / (_changeExpansionCounts.x * _changeExpansionCounts.y);
                    int3 brick = _changeExpansionMinBrick + new int3(x, y, z);
                    if (_changeExpansionAffectsSolids && _changedBrickSet.Add(brick))
                        _changedBricks.Add(brick);
                    if (_changeExpansionAffectsWater && _changedWaterBrickSet.Add(brick))
                        _changedWaterBricks.Add(brick);
                    brickBudget--;
                }

                if (_changeExpansionCursor < total) return;
                _changeExpansionActive = false;
                _changeExpansionCursor = 0;
                _changeRecordIndex++;
                _lastChangeRecords++;
                recordBudget--;
            }
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

                    // This is an acknowledgement only. The shared guard refuses to wait if a
                    // future refactor accidentally reaches it before the worker is complete.
                    if (!GeometryFrameJobCompletionGuard.TryCompleteReady(
                            _surfaceDiscoveryJobHandle,
                            ref _framePathBlockingCompletionViolations))
                        return;
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
            if (_changeRecoveryRegions.IsCreated) _changeRecoveryRegions.Dispose();
            if (_surfaceDiscoveryFlags.IsCreated) _surfaceDiscoveryFlags.Dispose();
            if (_surfaceDiscoveryOccupiedWords.IsCreated) _surfaceDiscoveryOccupiedWords.Dispose();
            if (_surfaceDiscoveryFullySolidWords.IsCreated) _surfaceDiscoveryFullySolidWords.Dispose();

            _water.Dispose();
            for (int r = 0; r < _rings.Length; r++) _rings[r].Dispose();
            _lookupTables.Dispose();
            _geometryArena.Dispose();
        }

        private static double ElapsedMs(double startSeconds) =>
            (Time.realtimeSinceStartupAsDouble - startSeconds) * 1000.0;
    }
}
