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
    /// Player-visible frame-time regression for the exact VoxelShowcase startup path. Once the
    /// renderer has a live world, every player-loop frame is sampled continuously through terrain
    /// streaming, castle terrain snapshot, worker authoring, bounded live publication, and terminal
    /// landmark/far-field finalisation. Starting before CastleBuildStage becomes non-zero prevents
    /// the first (allocation/snapshot) castle frame from escaping the hard hitch gate.
    /// </summary>
    public sealed class ShowcaseNoStutterTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const double MaxCastleMainThreadSliceMs = 6.0;
        private const double MaxP95FrameMs = 18.0;
        private const double MaxP99FrameMs = 25.0;
        private const double MaxSingleFrameMs = 33.34;

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

            // Scene/domain load itself is outside a PlayMode coroutine's observable frame window.
            // Begin as soon as the production surface pass has a real world, before castle work is
            // admitted, so every live showcase frame through finalisation is included.
            int readyGuard = 0;
            while ((!VoxelRenderBridge.SurfaceBuildEnabled
                    || !VoxelRenderBridge.TryGetWorld(out _))
                   && readyGuard++ < 3600)
                yield return null;
            Assert.True(VoxelRenderBridge.SurfaceBuildEnabled);
            Assert.True(VoxelRenderBridge.TryGetWorld(out _),
                "VoxelShowcase never bound a live world to the production renderer.");

            var frameTimesMs = new List<double>(4096);
            var frameClock = Stopwatch.StartNew();
            int frames = 0;
            int firstFrame = Time.frameCount;
            bool sawCastleBuild = world.CastleBuildStage > 0;
            double deadline = Time.realtimeSinceStartupAsDouble + 90.0;

            // Stage 9 is terminal AsyncCastleBuildSession completion. CastleVoxels is assigned only
            // after StepLandmarks has also built the reference arch, published all castle regions,
            // captured far-field silhouettes, and recorded that final main-thread stage. Waiting
            // for both therefore covers the complete player-visible construction window.
            while (!CastleFullyFinalised(world)
                   && frames++ < 9000
                   && Time.realtimeSinceStartupAsDouble < deadline)
            {
                frameClock.Restart();
                yield return null;
                frameClock.Stop();
                frameTimesMs.Add(frameClock.Elapsed.TotalMilliseconds);
                sawCastleBuild |= world.CastleBuildStage > 0;

                var metrics = VoxelRenderBridge.SurfaceMetrics;
                Assert.AreEqual(0ul, metrics.FramePathBlockingCompletionViolations,
                    "Geometry work synchronously completed a worker job from the frame path.");
                Assert.True(VoxelRenderBridge.SurfaceBuildEnabled,
                    "Voxel surface rendering was disabled during live showcase streaming.");
            }

            Assert.True(sawCastleBuild,
                "The real showcase never entered castle construction during the measured window.");
            Assert.True(CastleFullyFinalised(world),
                $"Castle did not fully finalise while frames continued to advance; "
              + $"stage={world.CastleBuildStage}, voxels={world.CastleVoxels}, frames={frames}, "
              + $"maxCastleMainThread={world.MaxCastleStageMs:F2} ms.");
            Assert.Greater(world.CastleVoxels, 100_000,
                "The terminal castle was too small for this to exercise the production build.");
            Assert.Greater(frameTimesMs.Count, 8,
                "Showcase completed too quickly to provide a meaningful player-loop sample.");
            Assert.GreaterOrEqual(Time.frameCount - firstFrame, frameTimesMs.Count,
                "The coroutine advanced without corresponding player-loop frames.");

            frameTimesMs.Sort();
            double p95 = Percentile(frameTimesMs, 0.95);
            double p99 = Percentile(frameTimesMs, 0.99);
            double maximum = frameTimesMs[^1];

            // Castle complexity belongs on the worker. Snapshot, commit and finalisation may touch
            // the live world, but no one castle slice gets even half of a 60-Hz frame.
            Assert.Less(world.MaxCastleStageMs, MaxCastleMainThreadSliceMs,
                $"Castle main-thread slice reached {world.MaxCastleStageMs:F2} ms "
              + $"(stage {world.MaxCastleStage}); snapshot/publication/finalisation is too coarse.");

            // Hard player-loop gates on the Metal validation machine. Percentiles prevent a stream
            // of merely-slow frames, while the absolute ceiling means no individual measured live
            // showcase frame may collapse below 30 fps while the castle is being produced.
            Assert.Less(p95, MaxP95FrameMs,
                $"Live-showcase p95 was {p95:F2} ms (p99={p99:F2}, max={maximum:F2}).");
            Assert.Less(p99, MaxP99FrameMs,
                $"Live-showcase p99 was {p99:F2} ms (max={maximum:F2}).");
            Assert.Less(maximum, MaxSingleFrameMs,
                $"VoxelShowcase produced a {maximum:F2} ms player-loop hitch before castle "
              + "finalisation; no measured live frame may fall below 30 fps on the validation machine.");
        }

        private static bool CastleFullyFinalised(ShowcaseWorld world) =>
            world.CastleBuildStage >= 9 && world.CastleVoxels > 0;

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
