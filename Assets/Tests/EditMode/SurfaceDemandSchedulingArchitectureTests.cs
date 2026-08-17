using System;
using System.IO;
using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

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

            StringAssert.Contains("_hierarchyRequestPriorities", request);
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

    }
}
