using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GeometryPipelineArchitectureTests
    {
        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                    dir = dir.Parent;
                Assert.NotNull(dir, "Could not locate project root containing Assets/.");
                return dir.FullName;
            }
        }

        private static string ReadRenderingSource(string relativePath) => File.ReadAllText(
            Path.Combine(RepoRoot, "Assets", "VoxelEngine", "Rendering", "Runtime", relativePath));

        [Test]
        public void KnownFramePathJobCompletionsAreReadinessGated()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuSolidChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            int discovery = scheduler.IndexOf(
                "if (!_surfaceDiscoveryJobHandle.IsCompleted)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(discovery, 0);
            StringAssert.Contains("return;", scheduler.Substring(discovery, 140));
        }

        [Test]
        public void GeometryJobsAreFlushedOnceWithoutWaiting()
        {
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            const string flush = "JobHandle.ScheduleBatchedJobs();";
            int first = scheduler.IndexOf(flush, StringComparison.Ordinal);
            Assert.GreaterOrEqual(first, 0,
                "Async geometry jobs need an explicit non-blocking dispatch boundary.");
            Assert.AreEqual(first, scheduler.LastIndexOf(flush, StringComparison.Ordinal),
                "Job batches should flush once per world frame, not once per worker/job.");
            int water = scheduler.IndexOf("_water.Prepare(storage, camera, voxelSize, WaterBuildBudgetMs);",
                                          StringComparison.Ordinal);
            int visibility = scheduler.LastIndexOf(
                "CollectVisibility(camera, voxelSize, frame);", StringComparison.Ordinal);
            int frameAccounting = scheduler.IndexOf(
                "_prepareTiming.Add(ElapsedMs(prepareStart));", first, StringComparison.Ordinal);
            Assert.Greater(first, water,
                "Flush must include water and solid jobs scheduled this frame.");
            Assert.Greater(first, visibility,
                "Current-ring/visible demand must be collected before worker admission and its "
              + "single non-blocking batch flush.");
            Assert.Greater(frameAccounting, first,
                "The non-blocking batch flush must occur before the scheduler returns the frame.");
        }

        [Test]
        public void MultipleCamerasCannotMultiplyGeometryFrameBudgets()
        {
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            int guard = scheduler.IndexOf("if (_lastAdvancedFrame == frame)",
                                          StringComparison.Ordinal);
            int advancement = scheduler.IndexOf("_lastAdvancedFrame = frame;", guard,
                                                StringComparison.Ordinal);
            Assert.GreaterOrEqual(guard, 0);
            Assert.Greater(advancement, guard);
            string sameFramePath = scheduler.Substring(guard, advancement - guard);
            StringAssert.Contains("CollectVisibility(camera, voxelSize, frame);", sameFramePath);
            StringAssert.Contains("return;", sameFramePath);

            int visibilityStart = scheduler.IndexOf("private void CollectVisibility",
                                                    StringComparison.Ordinal);
            int visibilityEnd = scheduler.IndexOf("private void ProcessChangeFeed",
                                                  visibilityStart, StringComparison.Ordinal);
            Assert.GreaterOrEqual(visibilityStart, 0);
            Assert.Greater(visibilityEnd, visibilityStart);
            string visibility = scheduler.Substring(visibilityStart,
                                                    visibilityEnd - visibilityStart);
            StringAssert.DoesNotContain("ReadSince(", visibility);
            StringAssert.DoesNotContain("ProcessSurfaceDiscovery(", visibility);
            StringAssert.DoesNotContain("TryPublishPending(", visibility);
            StringAssert.DoesNotContain(".Prepare(storage,", visibility);
        }

        [Test]
        public void CoarseExactSamplingUsesFewerBuildWorkspaces()
        {
            Assert.AreEqual(8, VoxelSurfaceScheduler.WorkerCountForSourceStep(1));
            Assert.AreEqual(8, VoxelSurfaceScheduler.WorkerCountForSourceStep(2));
            Assert.AreEqual(4, VoxelSurfaceScheduler.WorkerCountForSourceStep(4));
            Assert.AreEqual(2, VoxelSurfaceScheduler.WorkerCountForSourceStep(8));
            Assert.Less(VoxelSurfaceScheduler.WorkerCountForSourceStep(8),
                        VoxelSurfaceScheduler.WorkerCountForSourceStep(1),
                "The exact step-8 ring must not duplicate its 66^3 snapshot cache eight times.");
        }


        [Test]
        public void DirtyBuildSelectionIsIncremental()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuSolidChunkCache.cs"));
            StringAssert.Contains("BuildSelectionCandidatesPerSlice", cache);
            StringAssert.Contains("private readonly Queue<int3> _dirtyQueue", cache);
            StringAssert.Contains("BeginNearestBuild(camera, voxelSize, deadline)", cache);
            StringAssert.DoesNotContain("foreach (int3 candidate in _dirty)", cache);
            StringAssert.DoesNotContain("while (_entries.Count >= MaxResidentChunks", cache);
        }


        [Test]
        public void GeometryMaintenanceDoesNotScanAllKnownChunksEachFrame()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuSolidChunkCache.cs"));
            StringAssert.Contains("ResidencyChecksPerPrepare", cache);
            StringAssert.Contains("RegionInvalidationCandidatesPerPrepare", cache);
            StringAssert.Contains("private readonly Queue<int3> _residencyQueue", cache);
            StringAssert.Contains("private readonly Queue<int3> _regionInvalidationQueue", cache);
            StringAssert.DoesNotContain("private void DropNoLongerResident", cache);
            StringAssert.DoesNotContain("List<int3> affected", cache);
            int residency = cache.IndexOf("private void StepResidencyPrune", StringComparison.Ordinal);
            int residencyEnd = cache.IndexOf("/// <summary>", residency, StringComparison.Ordinal);
            Assert.GreaterOrEqual(residency, 0);
            Assert.Greater(residencyEnd, residency);
            StringAssert.DoesNotContain("foreach (int3 chunk in _known)",
                cache.Substring(residency, residencyEnd - residency));
        }

        [Test]
        public void RenderPassDrawStagingNeverResizesAfterConstruction()
        {
            string renderPass = ReadRenderingSource(
                Path.Combine("RenderFeature", "VoxelRenderPass.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            string water = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuWaterSurfaceChunkCache.cs"));

            StringAssert.Contains("GpuWaterSurfaceChunkCache.ArenaDrawCapacity", renderPass);
            StringAssert.Contains("public const int SurfaceArenaDrawCapacity", scheduler);
            StringAssert.Contains("public const int ArenaDrawCapacity", water);
            StringAssert.DoesNotContain("Array.Resize", renderPass);
            StringAssert.DoesNotContain("EnsureCapacity(ref _transvoxelDrawEntries", renderPass);
            StringAssert.DoesNotContain("EnsureCapacity(ref _waterDrawEntries", renderPass);
        }

        [Test]
        public void SolidSurfaceDrawsAreBucketedInsteadOfSubmittedPerChunk()
        {
            string renderPass = ReadRenderingSource(
                Path.Combine("RenderFeature", "VoxelRenderPass.cs"));
            string shader = File.ReadAllText(
                "Assets/VoxelEngine/Rendering/Runtime/Shaders/SmoothSurface.shader");

            StringAssert.Contains("GpuSurfaceDrawDispatcher.BucketCount", renderPass);
            StringAssert.Contains("DrawProceduralIndirect", renderPass);
            StringAssert.Contains("SV_InstanceID", shader);
            StringAssert.Contains("_PagedDrawMetadata", shader);
            StringAssert.DoesNotContain(
                "passData.TransvoxelEntries[i].Draw(cmd, passData.Material)", renderPass);
        }

        [Test]
        public void ProductionGpuSurfacePathHasOnlyRenderControlFeedbackAndNoCpuRangePublication()
        {
            string coordinator = ReadRenderingSource(
                Path.Combine("GpuVoxel", "GpuSurfaceMirrorCoordinator.cs"));
            string extractor = ReadRenderingSource(
                Path.Combine("GpuVoxel", "GpuSurfaceExtractor.cs"));
            string mesher = File.ReadAllText(
                "Assets/VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute");
            string context = ReadRenderingSource(
                Path.Combine("GpuVoxel", "GpuSurfaceExtractionContext.cs"));
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuSolidChunkCache.cs"));
            string renderPass = ReadRenderingSource(
                Path.Combine("RenderFeature", "VoxelRenderPass.cs"));

            StringAssert.Contains("AsyncGPUReadback.Request(lane.Counters, sizeof(uint),", coordinator);
            StringAssert.Contains("(GpuSurfaceExtractor.BatchHeaderWords + 10) * sizeof(uint)", coordinator);
            StringAssert.DoesNotContain("WaitForCompletion", coordinator);
            StringAssert.DoesNotContain("WaitAllRequests", coordinator);
            StringAssert.DoesNotContain("TryPublishCountBatch", coordinator);
            StringAssert.Contains("CompletePagedBatch", coordinator);
            StringAssert.DoesNotContain("TryCompleteStage(out", cache);
            StringAssert.DoesNotContain("TryTakeCountBatchLease", cache);
            StringAssert.Contains("TryTakePagedBatch", cache);
            StringAssert.Contains("if (_stageAdmissionPending)", context);
            StringAssert.Contains("TryAdmitPendingStage()", context);
            StringAssert.Contains("else if (_countDispatchPending)", context);
            StringAssert.Contains("TryDispatchPendingCount()", context);
            StringAssert.Contains("if (IsBlockDemanded(block) || IsBlockActive(block))", coordinator);
            StringAssert.DoesNotContain(
                "s_Mirror.Remove(block);\n                unchecked { s_CoverageEpoch++; }",
                coordinator);
            StringAssert.Contains("Graphics.CreateGraphicsFence", coordinator);
            StringAssert.Contains("!s_ExtractionFence.passed", coordinator);
            StringAssert.Contains("PagedIndirectArgs", renderPass);
            StringAssert.Contains("DrawProceduralIndirect", renderPass);
            StringAssert.DoesNotContain(
                "&& _buildProfileBlocks.Length == 0\n                              && _build.RequiresContinuousTopology",
                cache);
            StringAssert.Contains("profileBlocks: _buildProfileBlocks", cache);
            StringAssert.Contains("request.ProfileBlocks", context);
            StringAssert.Contains("resources.StageProfiles(requests, recordCount)", extractor);
            StringAssert.Contains("CSBatchCountProfiles", mesher);
            StringAssert.Contains("CSBatchWriteProfiles", mesher);
            StringAssert.Contains("BatchProfileSuppressionMask", mesher);
        }


        [Test]
        public void GameplaySurfaceDiagnosticsAndIndirectArgsAvoidManagedFrameGarbage()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuSolidChunkCache.cs"));
            string arena = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceGeometryArena.cs"));
            string renderPass = ReadRenderingSource(
                Path.Combine("RenderFeature", "VoxelRenderPass.cs"));
            StringAssert.DoesNotContain("new uint[4]", cache);
            StringAssert.Contains("NativeArray<uint> _argsScratch", arena);
            StringAssert.Contains("VerboseSurfaceDiagnostics", renderPass);
            StringAssert.Contains("LastSurfacePassState = \"feature-aware\"", renderPass);
        }


        [Test]
        public void FramePathJobCompletionIsNonBlockingAndObservable()
        {
            string solid = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuSolidChunkCache.cs"));
            string water = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuWaterSurfaceChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            string guard = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GeometryFrameJobCompletionGuard.cs"));
            string timing = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelTiming.cs"));

            StringAssert.Contains("if (!handle.IsCompleted)", guard);
            StringAssert.Contains("violationCount++", guard);
            StringAssert.Contains("handle.Complete()", guard);
            StringAssert.Contains("P99Ms", timing);
            StringAssert.Contains("FramePathBlockingCompletionViolations", scheduler);
            StringAssert.Contains("RunningGeometryJobs", scheduler);
            StringAssert.DoesNotContain(".Complete()", solid);
            StringAssert.DoesNotContain(".Complete()", water);
            int waterQuit = water.IndexOf("internal void DisposeForApplicationQuit()", StringComparison.Ordinal);
            Assert.Greater(waterQuit, 0);
            StringAssert.DoesNotContain("WaitAllRequests", water.Substring(0, waterQuit));
            StringAssert.Contains("GeometryFrameJobCompletionGuard.TryCompleteReady", scheduler);

            int solidTeardown = solid.IndexOf("public void Dispose()", StringComparison.Ordinal);
            int waterTeardown = water.IndexOf("public void Dispose()", StringComparison.Ordinal);
            int schedulerTeardown = scheduler.IndexOf("public void Dispose()", StringComparison.Ordinal);
            Assert.Greater(solidTeardown, 0);
            Assert.Greater(waterTeardown, 0);
            Assert.Greater(schedulerTeardown, 0);
            StringAssert.DoesNotContain(".Complete();", solid.Substring(0, solidTeardown));
            StringAssert.DoesNotContain(".Complete();", water.Substring(0, waterTeardown));
            StringAssert.DoesNotContain(".Complete();", scheduler.Substring(0, schedulerTeardown));
        }

        [Test]
        public void GeometrySchedulerExposesFrameScopedManagedAllocationCounter()
        {
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            StringAssert.Contains("LastFrameManagedAllocationBytes", scheduler);
            StringAssert.Contains("GC.GetAllocatedBytesForCurrentThread()", scheduler);
            StringAssert.Contains("long managedAllocationStart", scheduler);
            StringAssert.DoesNotContain("GC.GetTotalMemory", scheduler);

            int start = scheduler.IndexOf("long managedAllocationStart", StringComparison.Ordinal);
            int end = scheduler.IndexOf("private void CollectVisibility", start,
                                        StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            Assert.Greater(end, start);
            string framePath = scheduler.Substring(start, end - start);
            Assert.GreaterOrEqual(CountOccurrences(
                framePath, "GC.GetAllocatedBytesForCurrentThread()"), 2);
        }


        [Test]
        public void SolidVisibilityTraversesOnlyActiveToroidalSlotsOncePerRing()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuSolidChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            int collect = scheduler.IndexOf("private void CollectVisibility", StringComparison.Ordinal);
            int collectEnd = scheduler.IndexOf("private void ProcessChangeFeed", collect,
                                               StringComparison.Ordinal);
            Assert.GreaterOrEqual(collect, 0);
            Assert.Greater(collectEnd, collect);
            string productionVisibility = scheduler.Substring(collect, collectEnd - collect);
            StringAssert.Contains("for (int r = 0; r < _rings.Length; r++)", productionVisibility);
            StringAssert.Contains("ring.ActiveSlotCount", productionVisibility);
            StringAssert.Contains("ring.ActiveSlotCoordinate(slotIndex)", productionVisibility);
            StringAssert.Contains("ShardForChunk", productionVisibility);
            StringAssert.Contains("CollectVisibleCoordinate", productionVisibility);
            StringAssert.DoesNotContain("for (int z = -radius; z <= radius; z++)", productionVisibility);
            StringAssert.DoesNotContain("_allWorkers[i].CollectVisible", productionVisibility);

            int cacheCollect = cache.IndexOf("public IReadOnlyList<Entry> CollectVisible(",
                                             StringComparison.Ordinal);
            int cacheCollectEnd = cache.IndexOf("private bool BeginNearestBuild", cacheCollect,
                                                StringComparison.Ordinal);
            Assert.GreaterOrEqual(cacheCollect, 0);
            StringAssert.DoesNotContain("foreach (int3 coordinate in _known)",
                cache.Substring(cacheCollect, cacheCollectEnd - cacheCollect));
        }


        [Test]
        public void ChangeJournalAttachmentBaselinesCurrentVersionWithoutInvalidatingCurrentState()
        {
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            int reset = scheduler.IndexOf("private void ResetChangeFeedState",
                                          StringComparison.Ordinal);
            int recovery = scheduler.IndexOf("private void StepChangeOverflowRecovery", reset,
                                             StringComparison.Ordinal);
            Assert.GreaterOrEqual(reset, 0);
            Assert.Greater(recovery, reset);
            string attach = scheduler.Substring(reset, recovery - reset);
            StringAssert.Contains("_changeCursor = journal?.CurrentVersion ?? 0;", attach);
            StringAssert.Contains("_recoveringChangeOverflow = false;", attach);
            StringAssert.DoesNotContain("_changeCursor = 0;", attach,
                "A newly attached journal must not replay retained pre-render history.");
        }


        [Test]
        public void ChangeJournalAndOverflowRecoveryAreFrameBounded()
        {
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            StringAssert.Contains("ChangeReadRecordsPerFrame", scheduler);
            StringAssert.Contains("ChangeBrickExpansionsPerFrame", scheduler);
            StringAssert.Contains("journal.ReadSince(", scheduler);
            StringAssert.Contains("ChangeReadRecordsPerFrame", scheduler);
            StringAssert.Contains("CopyResidentRegionCoords", scheduler);
            StringAssert.DoesNotContain("storage.GetResidentRegionCoords(Allocator.Temp)", scheduler);
        }


        [Test]
        public void GeometryResidencyRequiresOwnedCoreRegions()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuSolidChunkCache.cs"));
            int start = cache.IndexOf("private bool AllOwnedCoreRegionsResident",
                                      StringComparison.Ordinal);
            int end = cache.IndexOf("internal bool TryEvictOneForArenaPressure", start,
                                    StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            Assert.Greater(end, start);
            string residency = cache.Substring(start, end - start);
            StringAssert.Contains("int3 minVoxel = chunk * VoxelsPerAxis;", residency);
            StringAssert.Contains("int3 maxVoxel = (chunk + 1) * VoxelsPerAxis - 1;", residency);
            StringAssert.Contains(
                "if (!source.IsRegionResident(new int3(x, y, z))) return false;", residency);
            StringAssert.DoesNotContain("int halo = Padding * SourceStep;", residency);
        }


        [Test]
        public void SurfaceEntriesAreReusedAfterResidencyChurn()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuSolidChunkCache.cs"));
            StringAssert.Contains("private readonly Stack<Entry> _entryPool", cache);
            StringAssert.Contains("private Entry AcquireEntry", cache);
            StringAssert.Contains("private void RecycleEntry", cache);
            StringAssert.Contains("entry.Reinitialize(coordinate)", cache);
            StringAssert.DoesNotContain(
                "entry = new Entry(_build.Coordinate, VoxelsPerAxis, SourceStep", cache);
        }


        [Test]
        public void SurfaceSlotGenerationGuardsRecycledResidency()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuSolidChunkCache.cs"));
            string slot = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceChunkSlot.cs"));
            StringAssert.Contains("uint Generation", slot);
            StringAssert.Contains("public uint SlotGeneration", cache);
            StringAssert.Contains("SlotGeneration = buildSlot.Generation", cache);
            StringAssert.Contains("private bool BuildOwnsCurrentSlot", cache);
            StringAssert.Contains("if (!BuildOwnsCurrentSlot()", cache);
            StringAssert.Contains("RetireSlot(chunk)", cache);
        }


        [Test]
        public void ClipmapWindowOwnsRenderResidencyAdmission()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuSolidChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            StringAssert.Contains("public void SetClipmapWindow", cache);
            StringAssert.Contains("private bool TrackKnown", cache);
            StringAssert.Contains("if (!WithinClipmapWindow(chunk)) return false;", cache);
            StringAssert.Contains("!TrackKnown(chunk)) continue;", cache);
            StringAssert.Contains("WithinClipmapWindow(_build.Coordinate)", cache);
            StringAssert.Contains("UpdateClipmapWindow(cameraPosition, voxelSize)", scheduler);
            StringAssert.Contains("ClipmapCentre", scheduler);
            StringAssert.Contains("ClipmapRadius", scheduler);
        }


        [Test]
        public void WaterPublicationUsesFixedArenaAndBoundedSlices()
        {
            string water = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuWaterSurfaceChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            StringAssert.Contains("GpuSurfacePageArena _geometryArena", water);
            StringAssert.DoesNotContain("NativeList<SmoothSurfaceVertex>", water);
            StringAssert.DoesNotContain("NativeList<uint>", water);
            StringAssert.Contains("GpuWaterSurfaceMesher.MaximumBricksPerSlice", water);
            StringAssert.Contains("TryPublishPending", water);
            StringAssert.Contains("Time.realtimeSinceStartupAsDouble >= deadline", water);
            string afterConstruction = water.Substring(water.IndexOf("public int ResidentCount", StringComparison.Ordinal));
            StringAssert.DoesNotContain("new ComputeBuffer", afterConstruction);
            StringAssert.DoesNotContain("new uint[]", water);
            StringAssert.Contains("WaterUploadBudgetBytes", scheduler);
            StringAssert.Contains("_water.TryPublishPending", scheduler);
        }


        [Test]
        public void WaterMaintenanceAndBuildAdmissionAreIncremental()
        {
            string water = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuWaterSurfaceChunkCache.cs"));
            StringAssert.Contains("BuildSelectionCandidatesPerPrepare", water);
            StringAssert.Contains("RegionInvalidationCandidatesPerPrepare", water);
            StringAssert.Contains("ResidencyChecksPerPrepare", water);
            StringAssert.Contains("private readonly Queue<int3> _dirtyQueue", water);
            StringAssert.Contains("private readonly Queue<int3> _residencyQueue", water);
            StringAssert.Contains("private readonly Queue<int3> _regionInvalidationQueue", water);
            StringAssert.DoesNotContain("private readonly List<int3> _buildBricks", water);
            StringAssert.DoesNotContain("foreach (int3 candidate in _dirty)", water);
            StringAssert.DoesNotContain("private void DropNoLongerResident", water);
            StringAssert.DoesNotContain("List<int3> gone", water);
            int pressure = water.IndexOf("TryEvictOneForArenaPressure", StringComparison.Ordinal);
            int pressureEnd = water.IndexOf("public void Dispose()", pressure,
                                            StringComparison.Ordinal);
            Assert.GreaterOrEqual(pressure, 0);
            Assert.Greater(pressureEnd, pressure);
            string pressurePath = water.Substring(pressure, pressureEnd - pressure);
            StringAssert.Contains("MarkDirty(victim)", pressurePath);
            StringAssert.DoesNotContain("RemoveWaterChunk(victim)", pressurePath);
        }


        [Test]
        public void FixedToroidalSurfaceSlotsAreSharedPerLodRing()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuSolidChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            string grid = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceChunkSlotGrid.cs"));
            string slot = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceChunkSlot.cs"));

            StringAssert.Contains("private readonly SurfaceChunkSlotGrid _slotGrid = new();", scheduler);
            StringAssert.Contains("sourceStep, _slotGrid", scheduler);
            StringAssert.Contains("private readonly SurfaceChunkSlotGrid _slotGrid;", cache);
            StringAssert.DoesNotContain("Dictionary<int3, SurfaceChunkSlot>", cache);
            StringAssert.DoesNotContain("Stack<SurfaceChunkSlot>", cache);
            StringAssert.Contains("SurfaceChunkSlot[] _slots", grid);
            StringAssert.Contains("int[] _activeSlotIndices", grid);
            StringAssert.Contains("ActiveCoordinateAt(int activeIndex)", grid);
            StringAssert.Contains("SlotIndex(int3 coordinate)", grid);
            StringAssert.Contains("current.Reinitialize(coordinate, NextGeneration())", grid);
            StringAssert.Contains("internal struct SurfaceChunkSlot", slot);
        }


        [Test]
        public void ClipmapMovementRetiresOnlyOutgoingEdgesIncrementally()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuSolidChunkCache.cs"));
            StringAssert.Contains("ClipmapEdgeCandidatesPerPrepare", cache);
            StringAssert.Contains("ScheduleClipmapEdgeRetirement", cache);
            StringAssert.Contains("StepClipmapEdgeRetirement();", cache);

            int step = cache.IndexOf("private void StepClipmapEdgeRetirement",
                                     StringComparison.Ordinal);
            int stepEnd = cache.IndexOf("private bool WithinClipmapWindow", step,
                                        StringComparison.Ordinal);
            Assert.GreaterOrEqual(step, 0);
            Assert.Greater(stepEnd, step);
            string retirement = cache.Substring(step, stepEnd - step);
            StringAssert.Contains("remaining = ClipmapEdgeCandidatesPerPrepare", retirement);
            StringAssert.Contains("WithinClipmapWindow(coordinate)", retirement);
            StringAssert.Contains("TryRemoveChunk(coordinate)", retirement);
            StringAssert.DoesNotContain("foreach (int3 chunk in _known)", retirement);
            StringAssert.DoesNotContain("foreach (var pair in _entries)", retirement);
        }


        [Test]
        public void BrickPoolSupportsGenerationStampedCowReaders()
        {
            string pool = File.ReadAllText(Path.Combine(
                Application.dataPath, "VoxelEngine", "Storage", "Runtime", "BrickPool.cs"));
            StringAssert.Contains("public readonly struct PinToken", pool);
            StringAssert.Contains("private NativeArray<int> _pinCounts", pool);
            StringAssert.Contains("private NativeArray<uint> _slotGenerations", pool);
            StringAssert.Contains("private NativeArray<byte> _writeBorrowedSlots", pool);
            StringAssert.Contains("public int EnsureWritable", pool);
            StringAssert.Contains("CopyBrick(brickIndex, clone)", pool);
            StringAssert.Contains("_retiredSlots[token.Slot] != 0", pool);
            StringAssert.Contains("_writeBorrowedSlots[token.Slot] == 0", pool);
            StringAssert.Contains("_freeList.Add(token.Slot)", pool);
        }


        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int offset = 0;
            while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }
            return count;
        }


        [Test]
        public void ProductionMixedBrickMutationsPublishCowVersions()
        {
            string storageRoot = Path.Combine(Application.dataPath, "VoxelEngine", "Storage", "Runtime");
            string voxelAccess = File.ReadAllText(Path.Combine(storageRoot, "VoxelAccess.cs"));
            string mutationStore = File.ReadAllText(Path.Combine(storageRoot, "RegionMutationStore.cs"));
            string showcase = File.ReadAllText(Path.Combine(
                Application.dataPath, "Game", "Composition", "Showcase", "ShowcaseWorld.cs"));
            StringAssert.Contains("pool.EnsureWritable(poolIndex)", voxelAccess);
            Assert.GreaterOrEqual(CountOccurrences(mutationStore, "_pool.EnsureWritable("), 2);
            Assert.GreaterOrEqual(CountOccurrences(showcase, "_pool.EnsureWritable(brick.PoolIndex)"), 2);
        }


        [Test]
        public void PinnedGeometryNeverReadsBorrowedWriterPayloads()
        {
            string pool = File.ReadAllText(Path.Combine(
                Application.dataPath, "VoxelEngine", "Storage", "Runtime", "BrickPool.cs"));
            string store = File.ReadAllText(Path.Combine(
                Application.dataPath, "VoxelEngine", "Storage", "Runtime", "RegionMutationStore.cs"));
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "GpuSolidChunkCache.cs"));
            StringAssert.Contains("_writeBorrowedSlots", pool);
            StringAssert.Contains("public bool TryPin", pool);
            StringAssert.Contains("_pool.BeginWrite(poolIndex)", store);
            StringAssert.Contains("_pool.EndWrite(mutation.PoolIndex)", store);

        }


        [Test]
        public void RegionMetadataLeasesAreVersionedAndEvictionSafe()
        {
            string api = File.ReadAllText(Path.Combine(
                Application.dataPath, "VoxelEngine", "Storage", "Api", "IRegionReadSource.cs"));
            string table = File.ReadAllText(Path.Combine(
                Application.dataPath, "VoxelEngine", "Storage", "Runtime", "RegionTable.cs"));
            StringAssert.Contains("TryPinRegionBlockRefs", api);
            StringAssert.Contains("IsPinnedRegionCurrent", api);
            StringAssert.Contains("ReleasePinnedRegion", api);
            StringAssert.Contains("_contentRevisions", table);
            StringAssert.Contains("_retiredSlots", table);
            StringAssert.Contains("ReleaseRetiredSlot", table);
            StringAssert.Contains("_contentRevisions[slot] =", table);
        }


        [Test]
        public void BorrowedBrickRefPublicationAdvancesRegionRevisionImmediately()
        {
            string store = File.ReadAllText(Path.Combine(
                Application.dataPath, "VoxelEngine", "Storage", "Runtime",
                "RegionMutationStore.cs"));
            int materialize = store.IndexOf("private VoxelBlockMutation MaterializeBlock",
                                            StringComparison.Ordinal);
            int end = store.IndexOf("private static byte DecodeUniformMaterial", materialize,
                                    StringComparison.Ordinal);
            Assert.GreaterOrEqual(materialize, 0);
            Assert.Greater(end, materialize);
            string method = store.Substring(materialize, end - materialize);
            StringAssert.Contains("publishedPhysicalRef", method);
            StringAssert.Contains("_table.CommitRegion(in writable)", method);
            Assert.Less(method.IndexOf("_table.CommitRegion(in writable)",
                                       StringComparison.Ordinal),
                        method.IndexOf("_pool.BeginWrite(poolIndex)", StringComparison.Ordinal));
        }



    }
}
