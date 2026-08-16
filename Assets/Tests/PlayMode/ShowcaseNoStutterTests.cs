using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Player-visible frame-time regression for the exact VoxelShowcase startup path. This samples
    /// the castle construction window itself; measuring only after convergence misses the original
    /// failure where castle authoring stopped the player loop entirely.
    /// </summary>
    public sealed class ShowcaseNoStutterTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";

        [UnityTest, Timeout(900000)]
        public IEnumerator CastleConstruction_NeverOwnsAFrameAndNeverStallsRendering()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            ShowcaseWorld world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            Assert.NotNull(world);

            // Do not include scene load/domain warmup in the castle-specific frame gate.
            int startGuard = 0;
            while (world.CastleBuildStage == 0 && world.CastleVoxels == 0
                   && startGuard++ < 3600)
                yield return null;

            Assert.Greater(world.CastleBuildStage, 0,
                "The real showcase never entered castle construction.");

            var frameTimesMs = new List<double>(2048);
            var frameClock = Stopwatch.StartNew();
            int frames = 0;
            double deadline = Time.realtimeSinceStartupAsDouble + 30.0;
            while (world.CastleVoxels == 0
                   && frames++ < 3600
                   && Time.realtimeSinceStartupAsDouble < deadline)
            {
                frameClock.Restart();
                yield return null;
                frameClock.Stop();
                frameTimesMs.Add(frameClock.Elapsed.TotalMilliseconds);

                VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                Assert.AreEqual(0ul, metrics.FramePathBlockingCompletionViolations,
                    "Geometry work synchronously completed a worker job from the frame path.");
            }

            Assert.Greater(world.CastleVoxels, 100_000,
                $"Castle did not complete while frames continued to advance; "
              + $"stage={world.CastleBuildStage}, frames={frames}, "
              + $"maxCastleMainThread={world.MaxCastleStageMs:F2} ms.");
            Assert.Greater(frameTimesMs.Count, 8,
                "Castle completed too quickly to provide a meaningful player-loop sample.");

            frameTimesMs.Sort();
            double p95 = Percentile(frameTimesMs, 0.95);
            double p99 = Percentile(frameTimesMs, 0.99);
            double maximum = frameTimesMs[^1];

            // Castle complexity belongs on the worker. The main thread may snapshot/publish a
            // bounded slice, but no castle slice gets even half of a 60-Hz frame.
            Assert.Less(world.MaxCastleStageMs, 8.0,
                $"Castle main-thread slice reached {world.MaxCastleStageMs:F2} ms "
              + $"(stage {world.MaxCastleStage}); authoring or publication is too coarse.");

            // Wall-clock gates exercise the real PlayMode loop on the Metal validation runner.
            // p95/p99 catch sustained hitching while the max catches a single zero-FPS-style stall.
            Assert.Less(p95, 20.0,
                $"Castle-build player-loop p95 was {p95:F2} ms (p99={p99:F2}, max={maximum:F2}).");
            Assert.Less(p99, 33.34,
                $"Castle-build player-loop p99 was {p99:F2} ms (max={maximum:F2}).");
            Assert.Less(maximum, 50.0,
                $"Castle construction produced a {maximum:F2} ms player-loop stall.");
        }

        private static double Percentile(List<double> sorted, double percentile)
        {
            int index = Mathf.Clamp(
                Mathf.CeilToInt((float)(sorted.Count * percentile)) - 1,
                0,
                sorted.Count - 1);
            return sorted[index];
        }
    }
}
