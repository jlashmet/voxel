using System;
using System.IO;
using NUnit.Framework;
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
        public void TransitionMeshingIsScheduledAndNeverRunInline()
        {
            string source = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            int start = source.IndexOf("private bool StepTransitionFaces", StringComparison.Ordinal);
            int end = source.IndexOf("private void InitialiseTopologyTables", start,
                                     StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            Assert.Greater(end, start);
            string transition = source.Substring(start, end - start);
            StringAssert.Contains("_transitionJobHandle = job.Schedule();", transition);
            StringAssert.Contains("if (!_transitionJobHandle.IsCompleted) return false;", transition);
            StringAssert.DoesNotContain(".Run();", transition);
        }

        [Test]
        public void SolidPublicationIsQueuedAndGloballyBudgeted()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            StringAssert.Contains("public bool TryPublishPending", cache);
            StringAssert.Contains("entry.AdvanceUpload(_vertices, _indices, byteBudget", cache);
            StringAssert.DoesNotContain("entry.Upload(_vertices, _indices)", cache);
            StringAssert.Contains("SolidUploadBudgetBytes", scheduler);
            StringAssert.Contains("_lastFrameSolidUploadedBytes += uploadedBytes", scheduler);
        }

        [Test]
        public void KnownFramePathJobCompletionsAreReadinessGated()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            StringAssert.Contains("if (!_transitionJobHandle.IsCompleted) return false;", cache);
            StringAssert.Contains("&& !ScheduledJobsComplete())", cache);
            int discovery = scheduler.IndexOf(
                "if (!_surfaceDiscoveryJobHandle.IsCompleted)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(discovery, 0);
            StringAssert.Contains("return;", scheduler.Substring(discovery, 140));
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
            int visibilityEnd = scheduler.IndexOf("private void EnqueueSurfaceDiscovery",
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
        public void SolidBuildOutputStaysNativeThroughArenaUpload()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string arena = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceGeometryArena.cs"));
            StringAssert.Contains("private NativeList<SmoothSurfaceVertex> _vertices;", cache);
            StringAssert.Contains("private NativeList<uint> _indices;", cache);
            StringAssert.DoesNotContain("private readonly List<SmoothSurfaceVertex> _vertices", cache);
            StringAssert.DoesNotContain("private readonly List<uint> _indices", cache);
            StringAssert.Contains("NativeArray<SmoothSurfaceVertex> source", arena);
            StringAssert.Contains("NativeArray<uint> source", arena);
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
        public void AuthoritativeSnapshotAssemblyIsResumable()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            StringAssert.Contains("private bool StepDensitySnapshot", cache);
            StringAssert.Contains("SnapshotCursor", cache);
            StringAssert.Contains("SnapshotBlocksPerDeadlineCheck", cache);
            StringAssert.Contains("Time.realtimeSinceStartupAsDouble >= deadlineSeconds", cache);
            StringAssert.DoesNotContain("private void ScheduleDensityJob", cache);
            StringAssert.DoesNotContain("private void ScheduleMipDensityJob", cache);
            StringAssert.DoesNotContain("private bool SnapshotCoreHasSolid", cache);
        }


        [Test]
        public void CompletedJobResultsAreMergedUnderDeadline()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            StringAssert.Contains("StepCompletedResultAppend(deadline)", cache);
            StringAssert.Contains("private bool StepAppendNativeGeometry", cache);
            StringAssert.Contains("AppendElementsPerDeadlineCheck", cache);
            StringAssert.Contains("_transitionResultPending", cache);
            StringAssert.DoesNotContain("private void CompactTopology", cache);
            StringAssert.DoesNotContain("private void AppendFacetedTopology", cache);
        }


        [Test]
        public void DirtyBuildSelectionIsIncremental()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
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
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
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
        public void GameplaySurfaceDiagnosticsAndIndirectArgsAvoidManagedFrameGarbage()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
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
        public void SolidVisibilityTraversesBoundedClipmapCoordinatesOncePerRing()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            int collect = scheduler.IndexOf("private void CollectVisibility", StringComparison.Ordinal);
            int collectEnd = scheduler.IndexOf("private void EnqueueSurfaceDiscovery", collect,
                                               StringComparison.Ordinal);
            Assert.GreaterOrEqual(collect, 0);
            Assert.Greater(collectEnd, collect);
            string productionVisibility = scheduler.Substring(collect, collectEnd - collect);
            StringAssert.Contains("for (int r = 0; r < _rings.Length; r++)", productionVisibility);
            StringAssert.Contains("ShardForChunk", productionVisibility);
            StringAssert.Contains("CollectVisibleCoordinate", productionVisibility);
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
        public void SurfaceEntriesAreReusedAfterResidencyChurn()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
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
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string slot = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceChunkSlot.cs"));
            StringAssert.Contains("uint Generation", slot);
            StringAssert.Contains("public uint SlotGeneration", cache);
            StringAssert.Contains("SlotGeneration = buildSlot.Generation", cache);
            StringAssert.Contains("private bool BuildOwnsCurrentSlot", cache);
            StringAssert.Contains("if (!BuildOwnsCurrentSlot())", cache);
            StringAssert.Contains("RetireSlot(chunk)", cache);
        }


        [Test]
        public void ClipmapWindowOwnsRenderResidencyAdmission()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            StringAssert.Contains("public void SetClipmapWindow", cache);
            StringAssert.Contains("if (!WithinClipmapWindow(chunk)) return;", cache);
            StringAssert.Contains("WithinClipmapWindow(_build.Coordinate)", cache);
            StringAssert.Contains("UpdateClipmapWindow(cameraPosition, voxelSize)", scheduler);
            StringAssert.Contains("ClipmapCentre", scheduler);
            StringAssert.Contains("ClipmapRadius", scheduler);
        }

    }
}