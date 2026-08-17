#!/usr/bin/env python3
"""Apply T2.7 demand/coverage telemetry, then remove this helper."""
from pathlib import Path

CACHE = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs")
SCHED = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs")
TEST = Path("Assets/Tests/EditMode/SurfaceDemandSchedulingArchitectureTests.cs")


def replace_once(text: str, old: str, new: str, label: str) -> str:
    if old not in text:
        raise RuntimeError(f"could not find {label}")
    return text.replace(old, new, 1)


def main() -> None:
    cache = CACHE.read_text()
    cache = replace_once(
        cache,
        """        public int DirtyCount => _dirty.Count + (_build.Active ? 1 : 0);
        public ulong ActiveSurfaceCatalogueHash => _surfaceCatalogue.CatalogueHash;""",
        """        public int DirtyCount => _dirty.Count + (_build.Active ? 1 : 0);
        internal int HierarchyActiveCount => _hierarchyActive.Count;
        internal int ColdKnownCount
        {
            get
            {
                int activeRequestedOverlap = 0;
                foreach (int3 active in _hierarchyActive)
                    if (_hierarchyRequestPriorities.ContainsKey(active)) activeRequestedOverlap++;
                return math.max(0, _known.Count - _hierarchyActive.Count
                    - _hierarchyRequestPriorities.Count + activeRequestedOverlap);
            }
        }
        internal void GetHierarchyRequestCounts(out int p0, out int p1, out int p2, out int p3)
        {
            p0 = p1 = p2 = p3 = 0;
            foreach (SurfaceBuildPriority priority in _hierarchyRequestPriorities.Values)
            {
                switch (priority)
                {
                    case SurfaceBuildPriority.MissingVisibleCoverage: p0++; break;
                    case SurfaceBuildPriority.PreserveActiveCoverage: p1++; break;
                    case SurfaceBuildPriority.VisibleRefinement: p2++; break;
                    default: p3++; break;
                }
            }
        }
        public ulong ActiveSurfaceCatalogueHash => _surfaceCatalogue.CatalogueHash;""",
        "cache demand telemetry properties",
    )
    CACHE.write_text(cache)

    sched = SCHED.read_text()
    sched = replace_once(
        sched,
        """        public readonly int SolidDirtyChunks;
        public readonly int WaterResidentChunks;""",
        """        public readonly int SolidDirtyChunks;
        public readonly int ActiveSolidCoverageNodes;
        public readonly int FallbackSolidParentNodes;
        public readonly int ColdKnownSolidChunks;
        public readonly int RequestedSolidP0MissingCoverage;
        public readonly int RequestedSolidP1PreserveCoverage;
        public readonly int RequestedSolidP2VisibleRefinement;
        public readonly int RequestedSolidP3Prefetch;
        public readonly long SolidStagingBytes;
        public readonly int WaterResidentChunks;""",
        "metrics demand fields",
    )

    sched = replace_once(
        sched,
        """            SolidKnownChunks = solids.KnownCount;
            SolidResidentChunks = solids.ResidentCount;
            SolidDirtyChunks = solids.DirtyCount;
            WaterResidentChunks = water.ResidentCount;""",
        """            SolidKnownChunks = solids.KnownCount;
            SolidResidentChunks = solids.ResidentCount;
            SolidDirtyChunks = solids.DirtyCount;
            ActiveSolidCoverageNodes = solids.HierarchyActiveCount;
            FallbackSolidParentNodes = 0;
            ColdKnownSolidChunks = solids.ColdKnownCount;
            solids.GetHierarchyRequestCounts(
                out RequestedSolidP0MissingCoverage,
                out RequestedSolidP1PreserveCoverage,
                out RequestedSolidP2VisibleRefinement,
                out RequestedSolidP3Prefetch);
            SolidStagingBytes = solids.PendingUploadBytes;
            WaterResidentChunks = water.ResidentCount;""",
        "single-worker metrics demand assignments",
    )

    sched = replace_once(
        sched,
        """                                     int changeRecords, int discoveredSurfaceBricks,
                                     int visibleSolidChunks,
                                     int solidUploadBudgetBytes,""",
        """                                     int changeRecords, int discoveredSurfaceBricks,
                                     int visibleSolidChunks,
                                     int activeSolidCoverageNodes,
                                     int fallbackSolidParentNodes,
                                     int solidUploadBudgetBytes,""",
        "multi-worker metrics constructor parameters",
    )

    sched = replace_once(
        sched,
        """            VisibleSolidChunks = visibleSolidChunks;
            VisibleDetailSolidChunks = 0;
            int known = 0, resident = 0, dirty = 0, missing = 0, running = 0, uploads = 0;""",
        """            VisibleSolidChunks = visibleSolidChunks;
            VisibleDetailSolidChunks = 0;
            ActiveSolidCoverageNodes = activeSolidCoverageNodes;
            FallbackSolidParentNodes = fallbackSolidParentNodes;
            int known = 0, resident = 0, dirty = 0, missing = 0, running = 0, uploads = 0;
            int coldKnown = 0, requestedP0 = 0, requestedP1 = 0, requestedP2 = 0, requestedP3 = 0;""",
        "multi-worker demand accumulators",
    )

    sched = replace_once(
        sched,
        """                running += worker.RunningJobCount;
                if (worker.SourceStep == 4)""",
        """                running += worker.RunningJobCount;
                coldKnown += worker.ColdKnownCount;
                worker.GetHierarchyRequestCounts(
                    out int workerP0, out int workerP1, out int workerP2, out int workerP3);
                requestedP0 += workerP0;
                requestedP1 += workerP1;
                requestedP2 += workerP2;
                requestedP3 += workerP3;
                if (worker.SourceStep == 4)""",
        "worker demand aggregation",
    )

    sched = replace_once(
        sched,
        """            SolidKnownChunks = known;
            SolidResidentChunks = resident;
            SolidDirtyChunks = dirty;
            MissingVisibleSolidChunks = missing;""",
        """            SolidKnownChunks = known;
            SolidResidentChunks = resident;
            SolidDirtyChunks = dirty;
            ColdKnownSolidChunks = coldKnown;
            RequestedSolidP0MissingCoverage = requestedP0;
            RequestedSolidP1PreserveCoverage = requestedP1;
            RequestedSolidP2VisibleRefinement = requestedP2;
            RequestedSolidP3Prefetch = requestedP3;
            MissingVisibleSolidChunks = missing;""",
        "multi-worker demand assignments",
    )

    sched = replace_once(
        sched,
        """            SolidMeshesAwaitingUpload = uploads;
            SolidPendingUploadBytes = pendingUploadBytes;
            SolidUploadBudgetBytes = solidUploadBudgetBytes;""",
        """            SolidMeshesAwaitingUpload = uploads;
            SolidPendingUploadBytes = pendingUploadBytes;
            SolidStagingBytes = pendingUploadBytes;
            SolidUploadBudgetBytes = solidUploadBudgetBytes;""",
        "multi-worker staging telemetry",
    )

    sched = replace_once(
        sched,
        """        private readonly HashSet<SurfaceLodNodeKey> _desiredLodNodes = new();
        private readonly List<SurfaceLodNodeKey> _activeLodScratch = new(512);""",
        """        private readonly HashSet<SurfaceLodNodeKey> _desiredLodNodes = new();
        private readonly HashSet<SurfaceLodNodeKey> _fallbackLodNodes = new();
        private readonly List<SurfaceLodNodeKey> _activeLodScratch = new(512);""",
        "fallback telemetry set",
    )

    sched = replace_once(
        sched,
        """            _visibleSolids.Clear();
            _desiredLodNodes.Clear();
            _lastVisibilityCandidateChecks = 0;""",
        """            _visibleSolids.Clear();
            _desiredLodNodes.Clear();
            _fallbackLodNodes.Clear();
            _lastVisibilityCandidateChecks = 0;""",
        "fallback telemetry clear",
    )

    sched = replace_once(
        sched,
        """                    foreach (SurfaceLodNodeKey desired in _desiredLodNodes)
                    {
                        if (EnsureDesiredCoverage(desired)) continue;
                        WorkerFor(desired).RecordHierarchyMissingVisible();
                    }""",
        """                    foreach (SurfaceLodNodeKey desired in _desiredLodNodes)
                    {
                        bool covered = EnsureDesiredCoverage(desired);
                        if (_activeLodCoverage.TryFindActiveAncestorOrSelf(desired, out SurfaceLodNodeKey active)
                            && active.SourceStep > desired.SourceStep)
                            _fallbackLodNodes.Add(active);
                        if (!covered) WorkerFor(desired).RecordHierarchyMissingVisible();
                    }""",
        "fallback parent observation",
    )

    sched = replace_once(
        sched,
        """            _allWorkers, _water, _lastChangeRecords, _discoveredSurfaceBricks.Count,
            _visibleSolids.Count, SolidUploadBudgetBytes, _lastFrameSolidUploadedBytes,""",
        """            _allWorkers, _water, _lastChangeRecords, _discoveredSurfaceBricks.Count,
            _visibleSolids.Count, _activeLodCoverage.Count, _fallbackLodNodes.Count,
            SolidUploadBudgetBytes, _lastFrameSolidUploadedBytes,""",
        "metrics snapshot active/fallback arguments",
    )
    SCHED.write_text(sched)

    test = TEST.read_text()
    extra = r'''

        [Test]
        public void DemandTelemetryIsBoundedAndExposesCoveragePriorityState()
        {
            string cache = CacheSource();
            StringAssert.Contains("internal int HierarchyActiveCount", cache);
            StringAssert.Contains("internal int ColdKnownCount", cache);
            StringAssert.Contains("GetHierarchyRequestCounts", cache);
            StringAssert.DoesNotContain("foreach (int3 chunk in _known)",
                MethodSlice(cache, "internal int ColdKnownCount", "public ulong ActiveSurfaceCatalogueHash"));

            string scheduler = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Rendering", "Runtime", "SurfaceExtraction",
                "VoxelSurfaceScheduler.cs"));
            StringAssert.Contains("ActiveSolidCoverageNodes", scheduler);
            StringAssert.Contains("FallbackSolidParentNodes", scheduler);
            StringAssert.Contains("ColdKnownSolidChunks", scheduler);
            StringAssert.Contains("RequestedSolidP0MissingCoverage", scheduler);
            StringAssert.Contains("RequestedSolidP1PreserveCoverage", scheduler);
            StringAssert.Contains("RequestedSolidP2VisibleRefinement", scheduler);
            StringAssert.Contains("RequestedSolidP3Prefetch", scheduler);
            StringAssert.Contains("SolidStagingBytes", scheduler);
            StringAssert.Contains("QueueLatencyTiming", scheduler);
        }
'''
    if extra.strip() not in test:
        insert_at = test.rfind("\n    }\n}")
        if insert_at < 0:
            raise RuntimeError("could not find demand scheduling test class tail")
        test = test[:insert_at] + extra + test[insert_at:]
    TEST.write_text(test)


if __name__ == "__main__":
    main()
