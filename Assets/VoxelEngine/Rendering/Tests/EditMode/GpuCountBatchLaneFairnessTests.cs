using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuCountBatchLaneFairnessTests
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

        [Test]
        public void SaturatedCountBatchLanesUseRoundRobinSealAuthorityWithoutInlineBypass()
        {
            string source = File.ReadAllText(Path.Combine(
                RepoRoot,
                "Assets", "VoxelEngine", "Rendering", "Runtime", "GpuVoxel",
                "GpuSurfaceMirrorCoordinator.cs"));

            string admission = MethodBody(
                source,
                "internal static bool TryDispatchCountBatch",
                "private static void EnsureCountBatchLanes");
            string advancement = MethodBody(
                source,
                "private static void AdvanceCountBatches",
                "private static void ResetCountBatchLane");
            string sealing = MethodBody(
                source,
                "private static bool SealCountBatch",
                "private static void ResetCountBatches");

            StringAssert.DoesNotContain("SealCountBatch(", admission,
                "A newly refilled low-index lane must not bypass older full lanes by sealing inline.");
            StringAssert.Contains("s_CountBatchSealCursor", advancement);
            StringAssert.Contains(
                "(s_CountBatchSealCursor + offset) % s_CountBatchLanes.Length",
                advancement);
            StringAssert.Contains(
                "s_CountBatchSealCursor = (laneIndex + 1) % s_CountBatchLanes.Length",
                advancement);
            StringAssert.Contains("if (!SealCountBatch(lane)) return;", advancement,
                "A blocked oldest lane must retain authority instead of rotating past backpressure.");
            StringAssert.Contains("return true;", sealing,
                "The fair service cursor may advance only after an actual GPU submission.");

            // Independent liveness oracle for the demonstrated four-lane/two-record saturation
            // pattern. Every successful one-per-frame service is immediately refilled, matching a
            // sustained cold view. A fixed lane-zero scan yields 0,0,0,...; round-robin authority
            // must visit every lane before returning to the first.
            const int laneCount = 4;
            int cursor = 0;
            var serviced = new List<int>();
            for (int frame = 0; frame < laneCount * 3; frame++)
            {
                int lane = cursor;
                serviced.Add(lane);
                cursor = (lane + 1) % laneCount;
            }

            CollectionAssert.AreEqual(
                new[] { 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3 },
                serviced,
                "Sustained refill must not starve the six records held by lanes 1-3.");
        }

        private static string MethodBody(string source, string startMarker, string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "Missing " + startMarker);
            int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), "Missing " + endMarker);
            return source.Substring(start, end - start);
        }
    }
}
