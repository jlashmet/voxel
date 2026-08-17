#!/usr/bin/env python3
"""Apply T2.4 lazy invalidation for cold render nodes, then remove this helper."""
from pathlib import Path

CACHE = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs")
TEST = Path("Assets/Tests/EditMode/SurfaceDemandSchedulingArchitectureTests.cs")


def replace_once(text: str, old: str, new: str, label: str) -> str:
    if old not in text:
        raise RuntimeError(f"could not find {label}")
    return text.replace(old, new, 1)


def main() -> None:
    cache = CACHE.read_text()
    old_request = """        internal bool RequestHierarchyCoverage(int3 coordinate, SurfaceBuildPriority priority)
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
    new_request = """        internal bool RequestHierarchyCoverage(int3 coordinate, SurfaceBuildPriority priority)
        {
            if (!OwnsShard(coordinate) || !TrackKnown(coordinate)) return false;

            bool hasReady = _entries.TryGetValue(coordinate, out Entry entry) && entry.Ready;
            bool hasEmpty = _emptyVersions.ContainsKey(coordinate);
            bool hasDesiredGeneration = _desiredVersions.ContainsKey(coordinate);
            if (!hasDesiredGeneration && (hasReady || hasEmpty))
            {
                // A current proof needs no work request. Visibility observing an already-current
                // node must not keep it artificially hot after it leaves active coverage.
                _hierarchyRequestPriorities.Remove(coordinate);
                return true;
            }

            if (_hierarchyRequestPriorities.TryGetValue(coordinate, out SurfaceBuildPriority existing))
            {
                if (priority < existing) _hierarchyRequestPriorities[coordinate] = priority;
            }
            else
            {
                _hierarchyRequestPriorities.Add(coordinate, priority);
            }

            if (!hasDesiredGeneration)
                Invalidate(coordinate);
            else if (!_dirty.Contains(coordinate)
                     && (!_build.Active || !_build.Coordinate.Equals(coordinate)))
                MarkDirty(coordinate);
            return true;
        }"""
    cache = replace_once(cache, old_request, new_request, "current-proof request release")

    old_invalidate = """        private void Invalidate(int3 chunk)
        {
            _emptyVersions.Remove(chunk);
            _desiredVersions[chunk] = ++_versionCounter;
            MarkDirty(chunk);
        }"""
    new_invalidate = """        private void Invalidate(int3 chunk)
        {
            _emptyVersions.Remove(chunk);
            _desiredVersions[chunk] = ++_versionCounter;

            // Invalidation changes truth; it is not itself a render-work request. Active coverage
            // and already-requested refinement rebuild immediately, while cold/offscreen nodes
            // remain stale until visibility explicitly requests their new generation.
            if (_hierarchyActive.Contains(chunk)
                || _hierarchyRequestPriorities.ContainsKey(chunk))
                MarkDirty(chunk);
        }"""
    cache = replace_once(cache, old_invalidate, new_invalidate, "lazy invalidation")

    cache = cache.replace(
        "// every known chunk queues a replacement built from the new immutable snapshot.",
        "// every known chunk invalidates its proof; only active/requested chunks queue replacement work.",
    )
    CACHE.write_text(cache)

    test = TEST.read_text()
    extra = r'''

        [Test]
        public void InvalidationAdvancesTruthWithoutQueueingColdNodes()
        {
            string source = CacheSource();
            string invalidation = MethodSlice(source,
                "private void Invalidate(int3 chunk)",
                "private void MarkDirty(int3 chunk)");

            StringAssert.Contains("_desiredVersions[chunk] = ++_versionCounter", invalidation);
            StringAssert.Contains("_hierarchyActive.Contains(chunk)", invalidation);
            StringAssert.Contains("_hierarchyRequestPriorities.ContainsKey(chunk)", invalidation);
            StringAssert.Contains("MarkDirty(chunk)", invalidation);
        }

        [Test]
        public void CurrentCompletionProofDoesNotRemainAnOutstandingRequest()
        {
            string source = CacheSource();
            string request = MethodSlice(source,
                "internal bool RequestHierarchyCoverage",
                "internal void BeginHierarchyActiveFrame");

            StringAssert.Contains("!hasDesiredGeneration && (hasReady || hasEmpty)", request);
            StringAssert.Contains("_hierarchyRequestPriorities.Remove(coordinate)", request);
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
