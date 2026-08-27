using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using VoxelEngine.Storage.Api;
using VoxelEngine.Tiering.Api;

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
        public readonly bool GpuCutoverAvailable;
        /// <summary>Shards whose GPU extraction buffers are currently allocated. The backend is
        /// built on a shard's first eligible chunk, so this trails the shard count until the
        /// cutover has actually claimed work on each one.</summary>
        public readonly int GpuResidentBackends;
        public readonly ulong GpuCompletedSolidBuilds;
        public readonly ulong GpuFallbackSolidBuilds;
        public readonly ulong GpuReadbackWaitSlices;
        public readonly VoxelTimingSummary GpuBuildLatencyTiming;
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
        public readonly ulong Step4ExactMetadataScheduled;
        public readonly ulong Step4ExactMetadataCompleted;
        public readonly ulong Step4ExactMetadataRevisionRejects;
        public readonly ulong Step4ExactMetadataPinRejects;
        public readonly ulong Step4FeatureFallbackScheduled;
        public readonly ulong Step4FeatureFallbackCompleted;
        public readonly ulong Step4FeatureFallbackNonEmpty;
        public readonly ulong Step4FeatureFallbackPublished;
        public readonly int Step4VisibilityKnown;
        public readonly int Step4VisibilityInBand;
        public readonly int Step4VisibilityFrustum;
        public readonly int Step4VisibilityReady;
        public readonly int Step4VisibilityEmpty;
        public readonly ulong MaterialPaletteInvalidations;
        public readonly ulong SurfaceCatalogueInvalidations;
        public readonly ulong CoatingCatalogueInvalidations;
        public readonly ulong ProfileBlockInvalidations;
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
        /// <summary>
        /// Vertex and index occupancy of the shared solid arena. The two buffers are sized
        /// independently, so a stall can come from either running out while the other still has
        /// room; the ratio is the only way to tell those apart.
        /// </summary>
        /// <summary>
        /// Visibility funnel across every ring, not just source step 4. Known -> in band -> in
        /// frustum shows whether a large missing-chunk count comes from weak culling, from rings
        /// claiming more chunks than their LOD should, or simply from a large view.
        /// </summary>
        public readonly int VisibilityKnownCandidates;
        public readonly int VisibilityInBandCandidates;
        public readonly int VisibilityFrustumCandidates;
        public readonly int SolidArenaUsedVertices;
        public readonly int SolidArenaVertexCapacity;
        public readonly int SolidArenaUsedIndices;
        public readonly int SolidArenaIndexCapacity;
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
        public readonly VoxelTimingSummary DensityOnlyTiming;
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
            GpuCutoverAvailable = solids.GpuCutoverAvailable;
            GpuResidentBackends = solids.GpuBackendResident ? 1 : 0;
            GpuCompletedSolidBuilds = solids.GpuCompletedBuildCount;
            GpuFallbackSolidBuilds = solids.GpuFallbackBuildCount;
            GpuReadbackWaitSlices = solids.GpuReadbackWaitSlices;
            GpuBuildLatencyTiming = solids.GpuBuildLatencyTiming;
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
            Step4ExactMetadataScheduled = isStep4 ? solids.ExactMetadataScheduleCount : 0UL;
            Step4ExactMetadataCompleted = isStep4 ? solids.ExactMetadataCompleteCount : 0UL;
            Step4ExactMetadataRevisionRejects = isStep4 ? solids.ExactMetadataRevisionRejectCount : 0UL;
            Step4ExactMetadataPinRejects = isStep4 ? solids.ExactMetadataPinRejectCount : 0UL;
            Step4FeatureFallbackScheduled = isStep4
                ? solids.FeaturePreservingFallbackScheduleCount : 0UL;
            Step4FeatureFallbackCompleted = isStep4
                ? solids.FeaturePreservingFallbackCompleteCount : 0UL;
            Step4FeatureFallbackNonEmpty = isStep4
                ? solids.FeaturePreservingFallbackNonEmptyCount : 0UL;
            Step4FeatureFallbackPublished = isStep4
                ? solids.FeaturePreservingFallbackPublishCount : 0UL;
            Step4VisibilityKnown = isStep4 ? solids.LastVisibilityKnownCount : 0;
            Step4VisibilityInBand = isStep4 ? solids.LastVisibilityInBandCount : 0;
            Step4VisibilityFrustum = isStep4 ? solids.LastVisibilityFrustumCount : 0;
            Step4VisibilityReady = isStep4 ? solids.LastVisibilityReadyCount : 0;
            Step4VisibilityEmpty = isStep4 ? solids.LastVisibilityEmptyCount : 0;
            MaterialPaletteInvalidations = solids.MaterialPaletteInvalidationCount;
            SurfaceCatalogueInvalidations = solids.SurfaceCatalogueInvalidationCount;
            CoatingCatalogueInvalidations = solids.CoatingCatalogueInvalidationCount;
            ProfileBlockInvalidations = solids.ProfileBlockInvalidationCount;
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
            VisibilityKnownCandidates = 0;
            VisibilityInBandCandidates = 0;
            VisibilityFrustumCandidates = 0;
            SolidArenaUsedVertices = 0;
            SolidArenaVertexCapacity = 0;
            SolidArenaUsedIndices = 0;
            SolidArenaIndexCapacity = 0;
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
            DensityOnlyTiming = solids.DensityOnlyTiming;
            TopologyJobTurnaroundTiming = solids.TopologyJobTurnaroundTiming;
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
                                     int solidUploadBudgetBytes,
                                     int lastFrameSolidUploadedBytes,
                                     int lastFrameSolidUploadCompletions,
                                     long solidArenaCommittedBytes,
                                     long solidArenaUsedBytes,
                                     int solidArenaUsedVertices,
                                     int solidArenaVertexCapacity,
                                     int solidArenaUsedIndices,
                                     int solidArenaIndexCapacity,
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
            ulong step4MetadataScheduled = 0, step4MetadataCompleted = 0;
            ulong step4MetadataRevisionRejects = 0, step4MetadataPinRejects = 0;
            ulong step4FallbackScheduled = 0, step4FallbackCompleted = 0;
            ulong step4FallbackNonEmpty = 0, step4FallbackPublished = 0;
            int visibilityKnown = 0, visibilityInBand = 0, visibilityFrustum = 0;
            int step4VisibilityKnown = 0, step4VisibilityInBand = 0;
            int step4VisibilityFrustum = 0, step4VisibilityReady = 0, step4VisibilityEmpty = 0;
            ulong materialInvalidations = 0, surfaceInvalidations = 0;
            ulong coatingInvalidations = 0, profileInvalidations = 0;
            long pendingUploadBytes = 0;
            ulong completed = 0, stale = 0, uploadedBytes = water.UploadedGeometryBytes;
            ulong gpuCompleted = 0, gpuFallback = 0;
            bool gpuAvailable = false;
            int gpuResidentBackends = 0;
            ulong gpuWaitSlices = 0;
            VoxelTimingSummary gpuBuildLatency = default;
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
                visibilityKnown += worker.LastVisibilityKnownCount;
                visibilityInBand += worker.LastVisibilityInBandCount;
                visibilityFrustum += worker.LastVisibilityFrustumCount;
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
                    step4MetadataScheduled += worker.ExactMetadataScheduleCount;
                    step4MetadataCompleted += worker.ExactMetadataCompleteCount;
                    step4MetadataRevisionRejects += worker.ExactMetadataRevisionRejectCount;
                    step4MetadataPinRejects += worker.ExactMetadataPinRejectCount;
                    step4FallbackScheduled += worker.FeaturePreservingFallbackScheduleCount;
                    step4FallbackCompleted += worker.FeaturePreservingFallbackCompleteCount;
                    step4FallbackNonEmpty += worker.FeaturePreservingFallbackNonEmptyCount;
                    step4FallbackPublished += worker.FeaturePreservingFallbackPublishCount;
                    step4VisibilityKnown += worker.LastVisibilityKnownCount;
                    step4VisibilityInBand += worker.LastVisibilityInBandCount;
                    step4VisibilityFrustum += worker.LastVisibilityFrustumCount;
                    step4VisibilityReady += worker.LastVisibilityReadyCount;
                    step4VisibilityEmpty += worker.LastVisibilityEmptyCount;
                }
                materialInvalidations += worker.MaterialPaletteInvalidationCount;
                surfaceInvalidations += worker.SurfaceCatalogueInvalidationCount;
                coatingInvalidations += worker.CoatingCatalogueInvalidationCount;
                profileInvalidations += worker.ProfileBlockInvalidationCount;
                uploads += worker.PendingUploadCount;
                pendingUploadBytes += worker.PendingUploadBytes;
                completed += worker.CompletedBuildCount;
                stale += worker.StaleBuildCount;
                gpuAvailable |= worker.GpuCutoverAvailable;
                if (worker.GpuBackendResident) gpuResidentBackends++;
                gpuCompleted += worker.GpuCompletedBuildCount;
                gpuFallback += worker.GpuFallbackBuildCount;
                gpuWaitSlices += worker.GpuReadbackWaitSlices;
                gpuBuildLatency = VoxelTimingSummary.WorstOf(gpuBuildLatency, worker.GpuBuildLatencyTiming);
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
            Step4ExactMetadataScheduled = step4MetadataScheduled;
            Step4ExactMetadataCompleted = step4MetadataCompleted;
            Step4ExactMetadataRevisionRejects = step4MetadataRevisionRejects;
            Step4ExactMetadataPinRejects = step4MetadataPinRejects;
            Step4FeatureFallbackScheduled = step4FallbackScheduled;
            Step4FeatureFallbackCompleted = step4FallbackCompleted;
            Step4FeatureFallbackNonEmpty = step4FallbackNonEmpty;
            Step4FeatureFallbackPublished = step4FallbackPublished;
            Step4VisibilityKnown = step4VisibilityKnown;
            Step4VisibilityInBand = step4VisibilityInBand;
            Step4VisibilityFrustum = step4VisibilityFrustum;
            Step4VisibilityReady = step4VisibilityReady;
            Step4VisibilityEmpty = step4VisibilityEmpty;
            MaterialPaletteInvalidations = materialInvalidations;
            SurfaceCatalogueInvalidations = surfaceInvalidations;
            CoatingCatalogueInvalidations = coatingInvalidations;
            ProfileBlockInvalidations = profileInvalidations;
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
            VisibilityKnownCandidates = visibilityKnown;
            VisibilityInBandCandidates = visibilityInBand;
            VisibilityFrustumCandidates = visibilityFrustum;
            SolidArenaUsedVertices = solidArenaUsedVertices;
            SolidArenaVertexCapacity = solidArenaVertexCapacity;
            SolidArenaUsedIndices = solidArenaUsedIndices;
            SolidArenaIndexCapacity = solidArenaIndexCapacity;
            SolidArenaActiveLeases = solidArenaActiveLeases;
            SolidArenaAllocationFailures = solidArenaAllocationFailures;
            SolidArenaPressureEvictions = solidArenaPressureEvictions;
            CompletedSolidBuilds = completed;
            RejectedStaleSolidBuilds = stale;
            GpuCutoverAvailable = gpuAvailable;
            GpuResidentBackends = gpuResidentBackends;
            GpuCompletedSolidBuilds = gpuCompleted;
            GpuFallbackSolidBuilds = gpuFallback;
            GpuReadbackWaitSlices = gpuWaitSlices;
            GpuBuildLatencyTiming = gpuBuildLatency;
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
            DensityOnlyTiming = default;
            TopologyJobTurnaroundTiming = default;
            TopologyCompactTiming = default;
            FacetedJobTurnaroundTiming = default;
            FacetedMergeTiming = default;
            ProfileEmitTiming = default;
            UploadTiming = default;
            QueueLatencyTiming = default;
            BuildLatencyTiming = default;
            GpuBuildLatencyTiming = default;
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
                DensityOnlyTiming = VoxelTimingSummary.WorstOf(
                    DensityOnlyTiming, worker.DensityOnlyTiming);
                TopologyJobTurnaroundTiming = VoxelTimingSummary.WorstOf(
                    TopologyJobTurnaroundTiming, worker.TopologyJobTurnaroundTiming);
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

    public readonly struct SurfaceAdmissionFrameTimingSnapshot
    {
        public readonly int Frame;
        public readonly double TotalMs;
        public readonly double SolidMs;
        public readonly double ArenaReliefMs;
        public readonly double WaterMs;
        public readonly double ScheduleBatchedJobsMs;

        internal SurfaceAdmissionFrameTimingSnapshot(
            int frame, double totalMs, double solidMs, double arenaReliefMs,
            double waterMs, double scheduleBatchedJobsMs)
        {
            Frame = frame;
            TotalMs = totalMs;
            SolidMs = solidMs;
            ArenaReliefMs = arenaReliefMs;
            WaterMs = waterMs;
            ScheduleBatchedJobsMs = scheduleBatchedJobsMs;
        }
    }

    public static class SurfaceAdmissionTimingTelemetry
    {
        public static SurfaceAdmissionFrameTimingSnapshot Snapshot { get; private set; }

        internal static void Record(int frame, double totalMs, double solidMs,
                                    double arenaReliefMs, double waterMs,
                                    double scheduleBatchedJobsMs)
        {
            Snapshot = new SurfaceAdmissionFrameTimingSnapshot(
                frame, totalMs, solidMs, arenaReliefMs, waterMs, scheduleBatchedJobsMs);
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
            (8, 288f, MaxVoxelRingRadiusMetresDefault),
        };

        internal static (float Inner, float Outer, bool Suspended) ResolveRingBand(
            float configuredInner, float configuredOuter, float ringCap, bool lodEnabled)
        {
            if (!lodEnabled)
            {
                bool isFinest = configuredInner <= 0f;
                return (0f, ringCap, !isFinest);
            }

            float outer = Math.Min(configuredOuter, ringCap);
            float inner = Math.Min(configuredInner, outer);
            return (inner, outer, inner > 0f && inner >= outer);
        }

        internal static (float Inner, float Outer, bool Suspended) ResolveScaledRingBand(
            float configuredInner, float configuredOuter, float detailBandScale,
            float ringCap, bool lodEnabled, bool isOutermost)
        {
            float scale = Math.Max(0.05f, detailBandScale);
            float scaledInner = configuredInner * scale;
            float scaledOuter = isOutermost ? ringCap : configuredOuter * scale;
            return ResolveRingBand(
                scaledInner, scaledOuter, ringCap, lodEnabled);
        }

        public const float MaxVoxelRingRadiusMetresDefault = 409.6f;
        private const double SurfaceArenaIndicesPerVertex = 1.75;
        private const int SurfaceArenaMinVertexCapacity = 256 * 1024;
        public const int SurfaceArenaDrawCapacity = 16 * 1024;

        private readonly SurfaceGeometryArena _geometryArena;
        private readonly TransvoxelLookupTables _lookupTables;
        private readonly SurfaceRing[] _rings;
        private readonly CpuTransvoxelChunkCache[] _allWorkers;
        private readonly List<CpuTransvoxelChunkCache.Entry> _visibleSolids = new(256);
        private readonly SurfaceLodVisibilitySelector _lodVisibilitySelector = new();
        private readonly List<SurfaceLodNodeKey> _lodDrawableNodes = new(256);
        private readonly List<SurfaceLodNodeKey> _lodCurrentCompleteNodes = new(256);
        private readonly Plane[] _visibilityFrustumPlanes = new Plane[6];
        private int _lastVisibilityCandidateChecks;
        private readonly CpuWaterSurfaceChunkCache _water = new();
        private readonly WaterSurfaceDiscoveryAdmission _waterDiscoveryAdmission = new();
        private const int ChangeReadRecordsPerFrame = 64;
        private const int ChangeBrickExpansionsPerFrame = 256;
        private const int ChangeRecoverySlotsPerFrame = 32;
        private readonly List<VoxelChangeRecord> _changeScratch = new(ChangeReadRecordsPerFrame);
        private NativeArray<int3> _changeRecoveryRegions;
        private readonly HashSet<int3> _sweptResidentRegions = new();
        private int _initialSurfaceDiscoveryCursor;
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
        private readonly List<int3>[] _ownedDiscoveryShardBuckets =
            new List<int3>[NearSolidWorkerCount];
        private readonly Queue<int3> _surfaceDiscoveryQueue = new();
        private readonly Queue<int3> _prioritySurfaceDiscoveryQueue = new();
        private readonly HashSet<int3> _queuedSurfaceDiscoveryRegions = new();
        private readonly HashSet<int3> _queuedPrioritySurfaceDiscoveryRegions = new();
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
        private bool _activeSurfaceDiscoveryPriority;
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

        public double SolidBuildBudgetMs { get; set; } = 0.20;
        public double ConvergenceBudgetScale { get; set; } = 1.0;
        private int _lastMissingVisibleCount;
        private double CurrentBudgetScale =>
            _lastMissingVisibleCount > 0 ? Math.Max(1.0, ConvergenceBudgetScale) : 1.0;
        private const int MaxArenaEvictionsPerFrame = 16;
        public static bool TrackSurfaceReappearance;
        private const int ReappearanceWindowFrames = 12;
        private const int ReappearancePruneFrames = 240;

        private readonly struct SurfaceChunkIdentity : IEquatable<SurfaceChunkIdentity>
        {
            public readonly int3 Coordinate;
            public readonly int SourceStep;

            public SurfaceChunkIdentity(int3 coordinate, int sourceStep)
            {
                Coordinate = coordinate;
                SourceStep = sourceStep;
            }

            public bool Equals(SurfaceChunkIdentity other) =>
                SourceStep == other.SourceStep && Coordinate.Equals(other.Coordinate);
            public override bool Equals(object obj) =>
                obj is SurfaceChunkIdentity other && Equals(other);
            public override int GetHashCode() =>
                unchecked(((int)math.hash(Coordinate) * 397) ^ SourceStep);
        }

        private readonly Dictionary<SurfaceChunkIdentity, int> _lastDrawnFrame = new();
        private int _lastPruneFrame;
        public int LastFrameReappearances { get; private set; }
        public ulong TotalReappearances { get; private set; }

        private void TrackReappearances(int frame)
        {
            if (!TrackSurfaceReappearance) return;

            int reappeared = 0;
            for (int i = 0; i < _visibleSolids.Count; i++)
            {
                CpuTransvoxelChunkCache.Entry entry = _visibleSolids[i];
                var identity = new SurfaceChunkIdentity(entry.Coordinate, entry.SourceStep);
                if (_lastDrawnFrame.TryGetValue(identity, out int seen))
                {
                    int gap = frame - seen;
                    if (gap > 1 && gap <= ReappearanceWindowFrames) reappeared++;
                }
                _lastDrawnFrame[identity] = frame;
            }

            LastFrameReappearances = reappeared;
            TotalReappearances += (ulong)reappeared;
            if (frame - _lastPruneFrame < ReappearancePruneFrames) return;
            _lastPruneFrame = frame;
            _pruneScratch.Clear();
            foreach (KeyValuePair<SurfaceChunkIdentity, int> pair in _lastDrawnFrame)
                if (frame - pair.Value > ReappearancePruneFrames) _pruneScratch.Add(pair.Key);
            for (int i = 0; i < _pruneScratch.Count; i++) _lastDrawnFrame.Remove(_pruneScratch[i]);
        }

        private readonly List<SurfaceChunkIdentity> _pruneScratch = new();
        private const int SteadyArenaReliefInterval = 8;
        public float MaxVoxelRingRadiusMetres { get; set; } = MaxVoxelRingRadiusMetresDefault;
        public static float DetailBandScale
        {
            get => s_DetailBandScale;
            set => s_DetailBandScale = Math.Max(0.05f, value);
        }
        private static float s_DetailBandScale = 1f;
        public bool LodEnabled { get; set; } = true;
        public int MaxResidentChunksPerRing { get; set; } = 4096;
        public int MaxConcurrentBuildsConverging { get; set; } = 12;
        public int MaxConcurrentBuildsConverged { get; set; } = 0;
        private static int ScaleBudget(int budget, double scale) =>
            (int)Math.Min(int.MaxValue, Math.Max(0L, (long)(budget * scale)));
        public double SurfaceDiscoveryBudgetMs { get; set; } = 0.10;
        public int SolidUploadBudgetBytes { get; set; } = 1024 * 1024;
        public int SolidUploadSliceBytes { get; set; } = 256 * 1024;
        public int SolidUploadWorkerBudget { get; set; } = 4;
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
        public double LastPrepareMainThreadMs { get; private set; }
        public double LastInvalidationMs { get; private set; }
        public double LastDiscoveryMs { get; private set; }
        public double LastAdmissionMs { get; private set; }
        public double LastVisibilityMainThreadMs { get; private set; }

        public string DescribeRingResidency()
        {
            var text = new System.Text.StringBuilder("RINGS");
            text.Append($" arena[v={_geometryArena.UsedVertices}/{_geometryArena.VertexCapacity}"
                        + $" i={_geometryArena.UsedIndices}/{_geometryArena.IndexCapacity}"
                        + $" draws={_geometryArena.UsedArgsRecords}/{_geometryArena.ArgsRecordCapacity}"
                        + $" leaseFail={_geometryArena.AllocationFailureCount}]");
            text.Append($" missingVisible={_lastMissingVisibleCount}");
            text.Append($" prepare[total={LastPrepareMainThreadMs:0.00}"
                        + $" invalidate={LastInvalidationMs:0.00}"
                        + $" discover={LastDiscoveryMs:0.00}"
                        + $" admit={LastAdmissionMs:0.00}"
                        + $" visible={LastVisibilityMainThreadMs:0.00}"
                        + $" cand={_lastVisibilityCandidateChecks}"
                        + $" drawn={_visibleSolids.Count}]");
            int knownExits = 0, inBand = 0, inFrustum = 0;
            for (int i = 0; i < _allWorkers.Length; i++)
            {
                knownExits += _allWorkers[i].LastVisibilityKnownCount;
                inBand += _allWorkers[i].LastVisibilityInBandCount;
                inFrustum += _allWorkers[i].LastVisibilityFrustumCount;
            }
            text.Append($" exits[known={knownExits} inBand={inBand} frustum={inFrustum}]");
            for (int r = 0; r < _rings.Length; r++)
            {
                SurfaceRing ring = _rings[r];
                int resident = 0;
                int known = 0;
                for (int w = 0; w < ring.Workers.Length; w++)
                {
                    resident += ring.Workers[w].ResidentCount;
                    known += ring.Workers[w].KnownCount;
                }

                CpuTransvoxelChunkCache first = ring.Workers[0];
                text.Append($" step{ring.SourceStep}[{first.MinViewDistanceMetres:0.#}"
                            + $"-{first.MaxViewDistanceMetres:0.#}m res={resident} known={known}]");
            }
            return text.ToString();
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
        public int WaterUploadBudgetBytes { get; set; } = 256 * 1024;
        public double WaterUploadBudgetMs { get; set; } = 0.10;
        public int LastFrameWaterUploadedBytes { get; private set; }
        private ulong _observedWaterArenaAllocationFailures;
        public IReadOnlyList<CpuTransvoxelChunkCache.Entry> VisibleSolids => _visibleSolids;
        internal ComputeBuffer SolidGeometryVertices => _geometryArena.Vertices;
        internal ComputeBuffer SolidGeometryIndices => _geometryArena.Indices;
        public IReadOnlyList<CpuWaterSurfaceChunkCache.Entry> VisibleWater => _water.Visible;

        public VoxelSurfaceMetrics Metrics => new(
            _allWorkers, _water, _lastChangeRecords, _discoveredSurfaceBricks.Count,
            _visibleSolids.Count, SolidUploadBudgetBytes, _lastFrameSolidUploadedBytes,
            _lastFrameSolidUploadCompletions, _geometryArena.CommittedGpuBytes,
            _geometryArena.UsedGpuBytes,
            _geometryArena.UsedVertices, _geometryArena.VertexCapacity,
            _geometryArena.UsedIndices, _geometryArena.IndexCapacity,
            _geometryArena.UsedArgsRecords,
            _geometryArena.AllocationFailureCount,
            _arenaPressureEvictions, _prepareTiming.Snapshot(), _journalTiming.Snapshot(),
            _invalidationTiming.Snapshot(), _discoveryTiming.Snapshot(),
            _workerPrepareTiming.Snapshot(), _visibilityTiming.Snapshot(),
            _surfaceDiscoveryJobScheduled ? 1 : 0,
            _framePathBlockingCompletionViolations,
            _lastFrameManagedAllocationBytes);

        internal static void SplitSurfaceArenaBudget(long budgetBytes,
                                                     out int vertexCapacity,
                                                     out int indexCapacity)
        {
            double bytesPerVertex = SmoothSurfaceVertex.Stride
                                  + SurfaceArenaIndicesPerVertex * sizeof(uint);
            long argsBytes = (long)SurfaceArenaDrawCapacity * ArgsWordsPerDrawBytes;
            double usable = Math.Max(0, budgetBytes - argsBytes);

            long vertices = (long)(usable / bytesPerVertex);
            vertices = Math.Max(SurfaceArenaMinVertexCapacity, Math.Min(vertices, int.MaxValue));
            long indices = (long)(vertices * SurfaceArenaIndicesPerVertex);
            indices = Math.Max(SurfaceArenaMinVertexCapacity, Math.Min(indices, int.MaxValue));

            vertexCapacity = (int)vertices;
            indexCapacity = (int)indices;
        }

        private const int ArgsWordsPerDrawBytes =
            SurfaceGeometryArena.ArgsWordsPerDraw * sizeof(uint);

        public VoxelSurfaceScheduler()
            : this(VoxelRenderBridge.SurfaceArenaBudgetBytesOverride > 0
                       ? VoxelRenderBridge.SurfaceArenaBudgetBytesOverride
                       : DeviceTierBudget.GetForTier(DeviceTierBudget.Detect()).SurfaceGeometryBudget)
        {
        }

        public VoxelSurfaceScheduler(long surfaceGeometryBudgetBytes)
        {
            SplitSurfaceArenaBudget(surfaceGeometryBudgetBytes,
                                    out int vertexCapacity, out int indexCapacity);
            _geometryArena = new SurfaceGeometryArena(vertexCapacity,
                                                       indexCapacity,
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

            for (int shard = 0; shard < _ownedDiscoveryShardBuckets.Length; shard++)
                _ownedDiscoveryShardBuckets[shard] =
                    new List<int3>(SurfaceDiscoveryPublishBatch);

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

            if (_lastAdvancedFrame == frame)
            {
                CollectVisibility(camera, voxelSize, frame);
                return;
            }

            long managedAllocationStart = GC.GetAllocatedBytesForCurrentThread();
            _lastAdvancedFrame = frame;
            _geometryArena.RetireExpiredLeases(frame);

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
                bool clipmapChanged = false;
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
                    if (!changed) continue;
                    clipmapChanged = true;
                    if (!hadPrevious) continue;
                    EnqueueClipmapRegionDifference(
                        previousMin, previousMaxExclusive,
                        currentMin, currentMaxExclusive);
                }

                if (clipmapChanged)
                    AddImmediateCameraDiscoveryRegions(storage, cameraPosition, voxelSize);
                StepClipmapAdmissionDiscovery(storage);
            }

            StepInitialSurfaceDiscovery(storage);

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
            LastInvalidationMs = ElapsedMs(invalidationStart);
            _invalidationTiming.Add(LastInvalidationMs);

            double discoveryStart = Time.realtimeSinceStartupAsDouble;
            using (s_DiscoveryMarker.Auto())
            {
                EnqueueSurfaceDiscovery(_surfaceDiscoveryRegions);
                ProcessSurfaceDiscovery(storage, _discoveredSurfaceBricks,
                                        SurfaceDiscoveryBudgetMs);
            }
            LastDiscoveryMs = ElapsedMs(discoveryStart);
            _discoveryTiming.Add(LastDiscoveryMs);

            for (int r = 0; r < _rings.Length; r++)
            {
                SurfaceRing ring = _rings[r];
                int bricksPerChunkAxis = ring.Workers[0].BricksPerAxis;
                SurfaceDiscoveryChunkOwner.PartitionByOwningShard(
                    _discoveredSurfaceBricks, bricksPerChunkAxis,
                    ring.Workers.Length, _ownedDiscoveryShardBuckets);
                for (int w = 0; w < ring.Workers.Length; w++)
                    ring.Workers[w].DiscoverSurfaceBricks(
                        _ownedDiscoveryShardBuckets[w]);
            }

            CollectVisibility(camera, voxelSize, frame);

            double workersStart = Time.realtimeSinceStartupAsDouble;
            float ringCap = Math.Max(0f, MaxVoxelRingRadiusMetres);
            for (int r = 0; r < _rings.Length; r++)
            {
                SurfaceRing ring = _rings[r];
                CpuTransvoxelChunkCache[] ringWorkers = ring.Workers;
                int perWorker = Math.Max(1, MaxResidentChunksPerRing / ringWorkers.Length);
                (float inner, float outer, bool suspended) = ResolveScaledRingBand(
                    ring.InnerRadiusMetres, ring.OuterRadiusMetres, DetailBandScale,
                    ringCap, LodEnabled, isOutermost: r == _rings.Length - 1);

                for (int i = 0; i < ringWorkers.Length; i++)
                {
                    ringWorkers[i].MaxResidentChunks = perWorker;
                    ringWorkers[i].MinViewDistanceMetres = inner;
                    ringWorkers[i].MaxViewDistanceMetres = outer;
                    ringWorkers[i].RingSuspended = suspended;
                }
            }

            double budgetScale = CurrentBudgetScale;
            double solidDeadline = workersStart
                                 + Math.Max(0.0, SolidBuildBudgetMs * budgetScale) * 0.001;
            int admittedWorkers = 0;
            double admissionStart = Time.realtimeSinceStartupAsDouble;
            using var workersScope = s_WorkersMarker.Auto();
            int workerCount = _allWorkers.Length;
            int buildCeiling = ResolveBuildCeiling(
                _lastMissingVisibleCount,
                MaxConcurrentBuildsConverging,
                MaxConcurrentBuildsConverged);
            bool allowBackgroundBuilds = ShouldAllowBackgroundBuilds(
                _lastMissingVisibleCount, buildCeiling);
            int activeBuilds = 0;
            for (int i = 0; i < workerCount; i++)
                if (_allWorkers[i].HasActiveBuild) activeBuilds++;

            for (int offset = 0; offset < workerCount; offset++)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                double remainingMs = (solidDeadline - now) * 1000.0;
                if (remainingMs <= 0.0) break;

                int index = (_workerAdmissionCursor + offset) % workerCount;
                CpuTransvoxelChunkCache worker = _allWorkers[index];
                bool wasBuilding = worker.HasActiveBuild;
                worker.CanStartNewBuild = wasBuilding || activeBuilds < buildCeiling;
                worker.AllowBackgroundBuilds = allowBackgroundBuilds;
                worker.Prepare(storage, in palette, in surfaceCatalogue,
                               in coatingCatalogue, profileBlocks, camera, voxelSize, frame,
                               remainingMs);
                if (!wasBuilding && worker.HasActiveBuild) activeBuilds++;
                admittedWorkers++;
            }
            if (workerCount > 0)
            {
                int advance = Math.Max(1, admittedWorkers);
                _workerAdmissionCursor = (_workerAdmissionCursor + advance) % workerCount;
            }

            double workerPrepareMs = ElapsedMs(workersStart);

            _lastFrameSolidUploadedBytes = 0;
            _lastFrameSolidUploadCompletions = 0;
            int uploadBudget = ScaleBudget(Math.Max(0, SolidUploadBudgetBytes), budgetScale);
            int uploadSlice = Math.Max(0, SolidUploadSliceBytes);
            int uploadWorkerBudget = ScaleBudget(Math.Max(0, SolidUploadWorkerBudget), budgetScale);
            double uploadDeadline = Time.realtimeSinceStartupAsDouble
                                  + Math.Max(0.0, SolidUploadBudgetMs * budgetScale) * 0.001;
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
            double solidAdmissionMs = ElapsedMs(admissionStart);

            double arenaReliefStart = Time.realtimeSinceStartupAsDouble;
            ulong arenaFailures = _geometryArena.AllocationFailureCount;
            int workersAwaitingPublication = 0;
            for (int i = 0; i < workerCount; i++)
                if (_allWorkers[i].PendingUploadCount > 0) workersAwaitingPublication++;

            bool converging = _lastMissingVisibleCount > 0;
            bool relievePeriodically = workersAwaitingPublication > 0
                                    && frame % SteadyArenaReliefInterval == 0;
            bool needsArenaRelief = (converging || relievePeriodically)
                                 && arenaFailures > _observedArenaAllocationFailures
                                 && workerCount > 0;
            _observedArenaAllocationFailures = arenaFailures;
            if (needsArenaRelief)
            {
                int evictionBudget = Math.Clamp(
                    workersAwaitingPublication, 1, MaxArenaEvictionsPerFrame);

                float nearestPending = float.MaxValue;
                for (int i = 0; i < workerCount; i++)
                {
                    if (_allWorkers[i].TryGetPendingPublishDistanceSq(
                            camera, voxelSize, out float pendingDistance)
                        && pendingDistance < nearestPending)
                        nearestPending = pendingDistance;
                }

                int freed = 0;
                for (int offset = 0; offset < workerCount && freed < evictionBudget; offset++)
                {
                    int index = (_arenaPressureCursor + offset) % workerCount;
                    int evicted = _allWorkers[index].EvictFarthest(
                        camera, voxelSize, offscreenOnly: true, 0f, evictionBudget - freed);
                    if (evicted == 0) continue;
                    freed += evicted;
                    _arenaPressureEvictions += (ulong)evicted;
                    _arenaPressureCursor = (index + 1) % workerCount;
                }

                if (VoxelRenderBridge.SurfaceEvictVisibleUnderArenaPressure
                    && freed < evictionBudget && nearestPending < float.MaxValue)
                {
                    for (int offset = 0; offset < workerCount && freed < evictionBudget; offset++)
                    {
                        int index = (_arenaPressureCursor + offset) % workerCount;
                        int evicted = _allWorkers[index].EvictFarthest(
                            camera, voxelSize, offscreenOnly: false, nearestPending,
                            evictionBudget - freed);
                        if (evicted == 0) continue;
                        freed += evicted;
                        _arenaPressureEvictions += (ulong)evicted;
                        _arenaPressureCursor = (index + 1) % workerCount;
                    }
                }
            }
            double arenaReliefMs = ElapsedMs(arenaReliefStart);

            double waterStart = Time.realtimeSinceStartupAsDouble;
            _waterDiscoveryAdmission.EnqueueAndStep(_water, storage, _discoveredSurfaceBricks);
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
            double waterMs = ElapsedMs(waterStart);

            _workerPrepareTiming.Add(workerPrepareMs);

            double scheduleStart = Time.realtimeSinceStartupAsDouble;
            JobHandle.ScheduleBatchedJobs();
            double scheduleBatchedJobsMs = ElapsedMs(scheduleStart);

            LastAdmissionMs = ElapsedMs(admissionStart);
            SurfaceAdmissionTimingTelemetry.Record(
                frame, LastAdmissionMs, solidAdmissionMs, arenaReliefMs,
                waterMs, scheduleBatchedJobsMs);
            _prepareTiming.Add(ElapsedMs(prepareStart));
            LastPrepareMainThreadMs = ElapsedMs(prepareStart);
            _lastFrameManagedAllocationBytes = Math.Max(
                0L, GC.GetAllocatedBytesForCurrentThread() - managedAllocationStart);
        }

        internal static int ResolveBuildCeiling(
            int missingVisibleCount, int convergingCeiling, int convergedCeiling) =>
            Math.Max(0, missingVisibleCount > 0 ? convergingCeiling : convergedCeiling);

        internal static bool ShouldAllowBackgroundBuilds(
            int missingVisibleCount, int buildCeiling) =>
            missingVisibleCount <= 0 && buildCeiling > 0;

        private const float ConvergingVisibilityReuseDistanceMetres = 0.75f;
        private const float ConvergingVisibilityReuseAngleDegrees = 2f;
        private Vector3 _lastVisibilityCameraPosition;
        private Quaternion _lastVisibilityCameraRotation;
        private ulong _lastVisibilityDemandVersion;
        private int _lastFullVisibilityFrame = -1;
        private bool _hasVisibilityCache;
        public static bool VisibilityReuseEnabled { get; set; } = true;

        private ulong CurrentVisibilityDemandVersion()
        {
            ulong demand = 0;
            for (int i = 0; i < _allWorkers.Length; i++)
                demand += _allWorkers[i].DemandVersion + _allWorkers[i].ReadySetVersion;
            return demand;
        }

        private bool TryReuseVisibility(Camera camera, float voxelSize, int frame)
        {
            if (camera == null || !VisibilityReuseEnabled || !_hasVisibilityCache) return false;

            ulong demand = CurrentVisibilityDemandVersion();
            if (demand != _lastVisibilityDemandVersion) return false;

            bool hasMissing = false;
            for (int i = 0; i < _allWorkers.Length; i++)
                hasMissing |= _allWorkers[i].MissingVisibleCount != 0;

            Transform cameraTransform = camera.transform;
            Vector3 position = cameraTransform.position;
            Quaternion rotation = cameraTransform.rotation;
            bool samePose = position == _lastVisibilityCameraPosition
                         && rotation == _lastVisibilityCameraRotation;
            bool stableReuse = !hasMissing && samePose;

            Vector3 delta = position - _lastVisibilityCameraPosition;
            bool boundedConvergingReuse = hasMissing
                && _lastMissingVisibleCount > 0
                && _visibleSolids.Count > 0
                && frame == _lastFullVisibilityFrame + 1
                && delta.sqrMagnitude <= ConvergingVisibilityReuseDistanceMetres
                                       * ConvergingVisibilityReuseDistanceMetres
                && Quaternion.Angle(rotation, _lastVisibilityCameraRotation)
                   <= ConvergingVisibilityReuseAngleDegrees;
            if (!stableReuse && !boundedConvergingReuse) return false;

            for (int i = 0; i < _visibleSolids.Count; i++)
                _visibleSolids[i].LastUsedFrame = frame;

            double reuseStart = Time.realtimeSinceStartupAsDouble;
            _lastVisibilityCandidateChecks = 0;
            _water.CollectVisible(camera, voxelSize);
            TrackReappearances(frame);
            LastVisibilityMainThreadMs = ElapsedMs(reuseStart);
            _visibilityTiming.Add(LastVisibilityMainThreadMs);
            return true;
        }

        private void RecordFullVisibilityCache(Camera camera, int frame)
        {
            if (camera == null || !VisibilityReuseEnabled)
            {
                _hasVisibilityCache = false;
                _lastFullVisibilityFrame = -1;
                return;
            }

            Transform cameraTransform = camera.transform;
            _lastVisibilityDemandVersion = CurrentVisibilityDemandVersion();
            _lastVisibilityCameraPosition = cameraTransform.position;
            _lastVisibilityCameraRotation = cameraTransform.rotation;
            _lastFullVisibilityFrame = frame;
            _hasVisibilityCache = true;
        }

        private void CollectVisibility(Camera camera, float voxelSize, int frame)
        {
            if (TryReuseVisibility(camera, voxelSize, frame)) return;

            _visibleSolids.Clear();
            _lodDrawableNodes.Clear();
            _lodCurrentCompleteNodes.Clear();
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

                        int activeSlots = ring.ActiveSlotCount;
                        for (int slotIndex = 0; slotIndex < activeSlots; slotIndex++)
                        {
                            int3 coordinate = ring.ActiveSlotCoordinate(slotIndex);
                            int3 delta = math.abs(coordinate - centre);
                            if (math.cmax(delta) > radius) continue;

                            int shard = CpuTransvoxelChunkCache.ShardForChunk(
                                coordinate, ring.Workers.Length);
                            CpuTransvoxelChunkCache worker = ring.Workers[shard];
                            int inBandBefore = worker.LastVisibilityInBandCount;
                            int frustumBefore = worker.LastVisibilityFrustumCount;
                            int readyBefore = worker.LastVisibilityReadyCount;
                            int emptyBefore = worker.LastVisibilityEmptyCount;
                            int visibleBefore = worker.Visible.Count;
                            worker.CollectVisibleCoordinate(
                                coordinate, _visibilityFrustumPlanes, cameraPosition,
                                voxelSize, frame);
                            var node = new SurfaceLodNodeKey(ring.SourceStep, coordinate);
                            if (worker.Visible.Count > visibleBefore)
                                _lodDrawableNodes.Add(node);
                            bool inBand = worker.LastVisibilityInBandCount > inBandBefore;
                            bool inFrustum = worker.LastVisibilityFrustumCount > frustumBefore;
                            bool currentReady = worker.LastVisibilityReadyCount > readyBefore;
                            bool currentEmpty = worker.LastVisibilityEmptyCount > emptyBefore;
                            if (SurfaceLodVisibilitySelector.IsCurrentViewComplete(
                                    inBand, inFrustum, currentReady, currentEmpty))
                                _lodCurrentCompleteNodes.Add(node);
                            _lastVisibilityCandidateChecks++;
                        }
                    }

                    _lodVisibilitySelector.Rebuild(
                        _lodDrawableNodes, _lodCurrentCompleteNodes);
                    for (int r = 0; r < _rings.Length; r++)
                    {
                        SurfaceRing ring = _rings[r];
                        for (int w = 0; w < ring.Workers.Length; w++)
                        {
                            IReadOnlyList<CpuTransvoxelChunkCache.Entry> visible =
                                ring.Workers[w].Visible;
                            for (int i = 0; i < visible.Count; i++)
                            {
                                CpuTransvoxelChunkCache.Entry entry = visible[i];
                                var node = new SurfaceLodNodeKey(
                                    entry.SourceStep, entry.Coordinate);
                                if (_lodVisibilitySelector.IsActive(node))
                                    _visibleSolids.Add(entry);
                            }
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

            int missingVisible = 0;
            for (int i = 0; i < _allWorkers.Length; i++)
                missingVisible += _allWorkers[i].MissingVisibleCount;
            _lastMissingVisibleCount = missingVisible;
            RecordFullVisibilityCache(camera, frame);

            TrackReappearances(frame);
            LastVisibilityMainThreadMs = ElapsedMs(visibilityStart);
            _visibilityTiming.Add(LastVisibilityMainThreadMs);
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
                    EnqueuePrioritySurfaceDiscovery(region);
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

        private void StepInitialSurfaceDiscovery(IRegionReadSource storage)
        {
            bool complete = storage.CopyResidentRegionCoords(
                ref _initialSurfaceDiscoveryCursor, _changeRecoveryRegions, out int count);
            for (int i = 0; i < count; i++)
            {
                int3 region = _changeRecoveryRegions[i];
                if (_sweptResidentRegions.Add(region))
                    _surfaceDiscoveryRegions.Add(region);
            }
            if (!complete) return;
            _initialSurfaceDiscoveryCursor = 0;
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
            _changeCursor = journal?.CurrentVersion ?? 0;
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

        private void EnqueuePrioritySurfaceDiscovery(int3 region)
        {
            if (_hasActiveSurfaceDiscovery && region.Equals(_activeSurfaceDiscoveryRegion))
            {
                _activeSurfaceDiscoveryPriority = true;
                _surfaceDiscoveryRescanRegions.Add(region);
                return;
            }

            _queuedSurfaceDiscoveryRegions.Add(region);
            if (_queuedPrioritySurfaceDiscoveryRegions.Add(region))
                _prioritySurfaceDiscoveryQueue.Enqueue(region);
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

                if (_hasActiveSurfaceDiscovery && region.Equals(_activeSurfaceDiscoveryRegion))
                    _surfaceDiscoveryRescanRegions.Add(region);
            }
        }

        private bool TryDequeueSurfaceDiscovery(out int3 region, out bool priority)
        {
            while (_prioritySurfaceDiscoveryQueue.Count > 0)
            {
                region = _prioritySurfaceDiscoveryQueue.Dequeue();
                _queuedPrioritySurfaceDiscoveryRegions.Remove(region);
                if (!_queuedSurfaceDiscoveryRegions.Contains(region)) continue;
                priority = true;
                return true;
            }

            while (_surfaceDiscoveryQueue.Count > 0)
            {
                region = _surfaceDiscoveryQueue.Dequeue();
                if (!_queuedSurfaceDiscoveryRegions.Contains(region)) continue;
                if (_queuedPrioritySurfaceDiscoveryRegions.Contains(region)) continue;
                priority = false;
                return true;
            }

            region = default;
            priority = false;
            return false;
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

                if (!TryDequeueSurfaceDiscovery(
                        out _activeSurfaceDiscoveryRegion,
                        out _activeSurfaceDiscoveryPriority))
                    break;

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
                return;
            }
        }

        private void FinishSurfaceDiscovery(bool requeue)
        {
            int3 region = _activeSurfaceDiscoveryRegion;
            bool priority = _activeSurfaceDiscoveryPriority;
            _surfaceDiscoveryRescanRegions.Remove(region);
            _queuedSurfaceDiscoveryRegions.Remove(region);
            _queuedPrioritySurfaceDiscoveryRegions.Remove(region);
            _hasActiveSurfaceDiscovery = false;
            _activeSurfaceDiscoveryPriority = false;
            _surfaceDiscoveryPublishIndex = 0;
            _surfaceDiscoveryResults.Clear();

            if (!requeue || !_queuedSurfaceDiscoveryRegions.Add(region)) return;

            if (priority)
            {
                _queuedPrioritySurfaceDiscoveryRegions.Add(region);
                _prioritySurfaceDiscoveryQueue.Enqueue(region);
            }
            else
            {
                _surfaceDiscoveryQueue.Enqueue(region);
            }
        }

        public void Dispose()
        {
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
