#!/usr/bin/env python3
"""Apply T2.3 explicit build-priority scheduling, then remove this helper."""
from pathlib import Path

CACHE = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs")
SCHEDULER = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs")
TEST = Path("Assets/Tests/EditMode/SurfaceDemandSchedulingArchitectureTests.cs")


def replace_once(text: str, old: str, new: str, label: str) -> str:
    if old not in text:
        raise RuntimeError(f"could not find {label}")
    return text.replace(old, new, 1)


def main() -> None:
    cache = CACHE.read_text()
    cache = replace_once(
        cache,
        "        private readonly HashSet<int3> _hierarchyRequested = new();",
        "        private readonly Dictionary<int3, SurfaceBuildPriority> _hierarchyRequestPriorities = new();",
        "hierarchy request field",
    )

    old_request = """        internal bool RequestHierarchyCoverage(int3 coordinate)
        {
            if (!OwnsShard(coordinate) || !TrackKnown(coordinate)) return false;
            _hierarchyRequested.Add(coordinate);

            bool hasReady = _entries.TryGetValue(coordinate, out Entry entry) && entry.Ready;
            bool hasEmpty = _emptyVersions.ContainsKey(coordinate);
            if (!_desiredVersions.ContainsKey(coordinate) && !hasReady && !hasEmpty)
                Invalidate(coordinate);
            else if (_desiredVersions.ContainsKey(coordinate)
                     && !_dirty.Contains(coordinate)
                     && (!_build.Active || !_build.Coordinate.Equals(coordinate)))
                MarkDirty(coordinate);
            return true;
        }"""
    new_request = """        internal bool RequestHierarchyCoverage(int3 coordinate, SurfaceBuildPriority priority)
        {
            if (!OwnsShard(coordinate) || !TrackKnown(coordinate)) return false;
            if (_hierarchyRequestPriorities.TryGetValue(coordinate, out SurfaceBuildPriority existing))
            {
                if (priority < existing) _hierarchyRequestPriorities[coordinate] = priority;
            }
            else
            {
                _hierarchyRequestPriorities.Add(coordinate, priority);
            }

            bool hasReady = _entries.TryGetValue(coordinate, out Entry entry) && entry.Ready;
            bool hasEmpty = _emptyVersions.ContainsKey(coordinate);
            if (!_desiredVersions.ContainsKey(coordinate) && !hasReady && !hasEmpty)
                Invalidate(coordinate);
            else if (_desiredVersions.ContainsKey(coordinate)
                     && !_dirty.Contains(coordinate)
                     && (!_build.Active || !_build.Coordinate.Equals(coordinate)))
                MarkDirty(coordinate);
            return true;
        }"""
    cache = replace_once(cache, old_request, new_request, "priority-aware hierarchy request")

    cache = replace_once(
        cache,
        """            int3 best = default;
            bool hasBest = false;
            float bestScore = float.PositiveInfinity;
            float chunkMetres = VoxelsPerAxis * voxelSize;""",
        """            int3 best = default;
            bool hasBest = false;
            SurfaceBuildPriority bestPriority = SurfaceBuildPriority.Prefetch;
            bool bestVisible = false;
            float bestScore = float.PositiveInfinity;
            float chunkMetres = VoxelsPerAxis * voxelSize;""",
        "build-selection state",
    )
    cache = replace_once(
        cache,
        "                    && !_hierarchyRequested.Contains(candidate))",
        "                    && !_hierarchyRequestPriorities.ContainsKey(candidate))",
        "out-of-band hierarchy eligibility",
    )
    old_score = """                Vector3 centre = (new Vector3(candidate.x, candidate.y, candidate.z)
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
                }"""
    new_score = """                Vector3 centre = (new Vector3(candidate.x, candidate.y, candidate.z)
                                + Vector3.one * 0.5f) * chunkMetres;
                float distance = (centre - cameraWorldPosition).sqrMagnitude;
                bool visible = GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds);
                SurfaceBuildPriority priority = _hierarchyRequestPriorities.TryGetValue(
                    candidate, out SurfaceBuildPriority requestedPriority)
                        ? requestedPriority : SurfaceBuildPriority.Prefetch;
                bool better = !hasBest
                    || priority < bestPriority
                    || (priority == bestPriority && visible && !bestVisible)
                    || (priority == bestPriority && visible == bestVisible && distance < bestScore);
                if (better)
                {
                    if (hasBest) RequeueDirty(best);
                    bestPriority = priority;
                    bestVisible = visible;
                    bestScore = distance;
                    best = candidate;
                    hasBest = true;
                }
                else
                {
                    RequeueDirty(candidate);
                }"""
    cache = replace_once(cache, old_score, new_score, "priority-first build selection")

    cache = cache.replace("_hierarchyRequested.Remove", "_hierarchyRequestPriorities.Remove")
    cache = cache.replace("_hierarchyRequested.Clear", "_hierarchyRequestPriorities.Clear")
    if "_hierarchyRequested" in cache:
        raise RuntimeError("stale _hierarchyRequested reference remains after priority migration")
    CACHE.write_text(cache)

    scheduler = SCHEDULER.read_text()
    scheduler = scheduler.replace(
        "RequestAndSync(active);",
        "RequestAndSync(active, SurfaceBuildPriority.PreserveActiveCoverage);",
    )
    scheduler = scheduler.replace(
        "RequestAndSync(desired);",
        "RequestAndSync(desired, SurfaceBuildPriority.PreserveActiveCoverage);",
    )
    scheduler = scheduler.replace(
        "RequestAndSync(root);",
        "RequestAndSync(root, SurfaceBuildPriority.MissingVisibleCoverage);",
    )
    scheduler = scheduler.replace(
        "allObserved &= RequestAndSync(child);",
        "allObserved &= RequestAndSync(child, SurfaceBuildPriority.VisibleRefinement);",
    )
    old_method = """        private bool RequestAndSync(in SurfaceLodNodeKey key)
        {
            CpuTransvoxelChunkCache worker = WorkerFor(key);
            if (!worker.RequestHierarchyCoverage(key.Coordinate)) return false;
            return SyncLodState(key, worker);
        }"""
    new_method = """        private bool RequestAndSync(in SurfaceLodNodeKey key, SurfaceBuildPriority priority)
        {
            CpuTransvoxelChunkCache worker = WorkerFor(key);
            if (!worker.RequestHierarchyCoverage(key.Coordinate, priority)) return false;
            return SyncLodState(key, worker);
        }"""
    scheduler = replace_once(scheduler, old_method, new_method, "priority-aware scheduler request")
    if "RequestAndSync(active);" in scheduler or "RequestAndSync(desired);" in scheduler \
            or "RequestAndSync(root);" in scheduler or "RequestAndSync(child);" in scheduler:
        raise RuntimeError("unprioritized scheduler hierarchy request remains")
    SCHEDULER.write_text(scheduler)

    test = TEST.read_text()
    test = replace_once(
        test,
        "using NUnit.Framework;\n",
        "using NUnit.Framework;\nusing VoxelEngine.Rendering.Runtime.SurfaceExtraction;\n",
        "test namespace import",
    )
    test = replace_once(
        test,
        '            StringAssert.Contains("_hierarchyRequested.Add(coordinate)", request);',
        '            StringAssert.Contains("_hierarchyRequestPriorities", request);',
        "explicit request architecture assertion",
    )
    extra = r'''

        [Test]
        public void BuildPriorityOrderingKeepsCoverageAheadOfRefinement()
        {
            Assert.Less((byte)SurfaceBuildPriority.MissingVisibleCoverage,
                        (byte)SurfaceBuildPriority.PreserveActiveCoverage);
            Assert.Less((byte)SurfaceBuildPriority.PreserveActiveCoverage,
                        (byte)SurfaceBuildPriority.VisibleRefinement);
            Assert.Less((byte)SurfaceBuildPriority.VisibleRefinement,
                        (byte)SurfaceBuildPriority.Prefetch);
        }

        [Test]
        public void SchedulerAssignsExplicitCoverageAndRefinementPriorities()
        {
            string scheduler = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Rendering", "Runtime", "SurfaceExtraction",
                "VoxelSurfaceScheduler.cs"));
            StringAssert.Contains(
                "RequestAndSync(root, SurfaceBuildPriority.MissingVisibleCoverage)", scheduler);
            StringAssert.Contains(
                "RequestAndSync(desired, SurfaceBuildPriority.PreserveActiveCoverage)", scheduler);
            StringAssert.Contains(
                "RequestAndSync(child, SurfaceBuildPriority.VisibleRefinement)", scheduler);

            string cache = CacheSource();
            StringAssert.Contains(
                "Dictionary<int3, SurfaceBuildPriority> _hierarchyRequestPriorities", cache);
            StringAssert.Contains("priority < bestPriority", cache);
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
