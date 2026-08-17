#!/usr/bin/env python3
"""Apply the T2 demand-driven admission change and its architecture guards.

Temporary branch tooling: this script is removed after it produces the normal source/test commit.
Every edit is assertion-based so a changed source shape fails instead of guessing.
"""
from pathlib import Path

CACHE = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs")
TEST = Path("Assets/Tests/EditMode/SurfaceDemandSchedulingArchitectureTests.cs")
TEST_META = Path("Assets/Tests/EditMode/SurfaceDemandSchedulingArchitectureTests.cs.meta")
TRACKER = Path("docs/GPU_VOXEL_RENDERER_MIGRATION_PLAN.md")


def replace_once(text: str, old: str, new: str, label: str) -> str:
    if old in text:
        return text.replace(old, new, 1)
    if new in text:
        return text
    raise RuntimeError(f"could not find expected source shape for {label}")


def main() -> None:
    cache = CACHE.read_text()
    old_discovery = """                    if (!OwnsShard(chunk) || _known.Contains(chunk)) continue;
                    if (!TrackKnown(chunk)) continue;
                    Invalidate(chunk);
                    admitted++;"""
    new_discovery = """                    if (!OwnsShard(chunk) || _known.Contains(chunk)) continue;
                    if (!TrackKnown(chunk)) continue;
                    // Discovery establishes cache ownership only. Expensive geometry work is
                    // requested explicitly by hierarchical visible/coverage demand.
                    admitted++;"""
    cache = replace_once(cache, old_discovery, new_discovery, "admission-only surface discovery")
    CACHE.write_text(cache)

    test_content = r'''using System;
using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SurfaceDemandSchedulingArchitectureTests
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

        private static string CacheSource() => File.ReadAllText(Path.Combine(
            RepoRoot, "Assets", "VoxelEngine", "Rendering", "Runtime", "SurfaceExtraction",
            "CpuTransvoxelChunkCache.cs"));

        private static string MethodSlice(string source, string startMarker, string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            int end = source.IndexOf(endMarker, start + 1, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, $"Missing start marker: {startMarker}");
            Assert.Greater(end, start, $"Missing end marker after {startMarker}: {endMarker}");
            return source.Substring(start, end - start);
        }

        [Test]
        public void SurfaceDiscoveryAdmitsKnownChunksWithoutEnqueueingGeometry()
        {
            string source = CacheSource();
            string discovery = MethodSlice(source,
                "internal int DiscoverSurfaceBricks",
                "public void InvalidateSurfaceBricks");

            StringAssert.Contains("TrackKnown(chunk)", discovery);
            StringAssert.DoesNotContain("Invalidate(chunk)", discovery);
            StringAssert.DoesNotContain("MarkDirty(chunk)", discovery);

            string mutation = MethodSlice(source,
                "public void InvalidateSurfaceBricks",
                "public void InvalidateDirtyRegions");
            StringAssert.Contains("Invalidate(chunk)", mutation,
                "Authoritative voxel mutation must still invalidate its render generation.");
        }

        [Test]
        public void HierarchicalCoverageRequestIsTheExplicitColdBuildTrigger()
        {
            string source = CacheSource();
            string request = MethodSlice(source,
                "internal bool RequestHierarchyCoverage",
                "internal void BeginHierarchyActiveFrame");

            StringAssert.Contains("_hierarchyRequested.Add(coordinate)", request);
            StringAssert.Contains("Invalidate(coordinate)", request);
            StringAssert.Contains("MarkDirty(coordinate)", request);
        }

        [Test]
        public void ActiveCoverageCannotBeSelectedByEitherColdEvictionPath()
        {
            string source = CacheSource();
            string arena = MethodSlice(source,
                "internal bool TryEvictOneForArenaPressure",
                "private void EnforceCapacity");
            string capacity = MethodSlice(source,
                "private void EnforceCapacity",
                "private bool TryRemoveChunk");

            StringAssert.Contains("_hierarchyActive.Contains(pair.Key)", arena);
            StringAssert.Contains("_hierarchyActive.Contains(pair.Key)", capacity);
        }

        [Test]
        public void ColdEvictionDoesNotImmediatelyRedirtyItsVictim()
        {
            string source = CacheSource();
            string arena = MethodSlice(source,
                "internal bool TryEvictOneForArenaPressure",
                "private void EnforceCapacity");
            string capacity = MethodSlice(source,
                "private void EnforceCapacity",
                "private bool TryRemoveChunk");

            StringAssert.Contains("_entries.Remove(victim)", arena);
            StringAssert.Contains("_entries.Remove(victim)", capacity);
            StringAssert.DoesNotContain("MarkDirty(victim)", arena);
            StringAssert.DoesNotContain("MarkDirty(victim)", capacity);
        }
    }
}
'''
    if TEST.exists() and TEST.read_text() != test_content:
        raise RuntimeError(f"{TEST} already exists with unexpected content")
    TEST.write_text(test_content)
    meta_content = "fileFormatVersion: 2\nguid: 4e56f1c3a9284f17a62c91ec4c83507b\n"
    if TEST_META.exists() and TEST_META.read_text() != meta_content:
        raise RuntimeError(f"{TEST_META} already exists with unexpected content")
    TEST_META.write_text(meta_content)

    tracker = TRACKER.read_text()
    completed = [
        "T0.6",
        "T1.2", "T1.3", "T1.4", "T1.5", "T1.6", "T1.7",
    ]
    for task in completed:
        old = f"- [ ] **{task}**"
        new = f"- [x] **{task}**"
        tracker = replace_once(tracker, old, new, f"tracker {task}")

    note = """### 2026-08-16 - CPU hierarchy integration validated\n\n- Generation-aware `Ready`/`KnownEmpty` completion and scheduler-owned active parent/child coverage are integrated in production source.\n- Distance shells now select desired refinement while active hierarchy leaves own drawing, including atomic inward/outward swaps and negative-coordinate coverage.\n- Active fallback leases are pinned against arena/capacity eviction and cold eviction no longer re-dirties itself.\n- Latest EditMode and Architecture Boundary Gate were green before beginning T2; constrained-budget visual acceptance remains T1.8.\n\n"""
    marker = "### 2026-08-16 - Initial direction\n"
    if note not in tracker:
        if marker not in tracker:
            raise RuntimeError("could not find working-notes insertion marker")
        tracker = tracker.replace(marker, note + marker, 1)
    TRACKER.write_text(tracker)


if __name__ == "__main__":
    main()
