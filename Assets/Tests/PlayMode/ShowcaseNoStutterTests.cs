using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Player-visible frame-time regression for the VoxelShowcase startup path.
    ///
    /// Castle and nearby terrain generation are now an editor/build-time concern. Scene activation
    /// restores a semantic world bake before the first gameplay frame; this test therefore proves
    /// the opposite of the old contract: no castle authoring session may run in Play mode, the
    /// finished castle is already present, and production surface extraction converges without a
    /// player-loop hitch while ordinary outer-ring streaming continues.
    /// </summary>
    public sealed class ShowcaseNoStutterTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const double MaxP95FrameMs = 18.0;
        private const double MaxP99FrameMs = 25.0;
        private const double MaxSingleFrameMs = 33.34;
        private const int RequiredConvergedFrames = 4;
        private const int MinimumMeasuredFrames = 60;

        [UnityTest, Timeout(900000)]
        public IEnumerator BakedStartup_NeverBuildsCastleDuringPlayAndNeverStallsRendering()
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
            Assert.True(VoxelRenderBridge.TryGetWorld(out _),
                "VoxelShowcase did not bind its live world during scene activation.");
            Assert.True(VoxelRenderBridge.SurfaceBuildEnabled,
                "Production solid rendering must be enabled from the first gameplay frame.");
            Assert.Greater(world.CastleVoxels, 100_000,
                "The baked startup world did not restore the production castle before gameplay.");
            Assert.AreEqual(0, world.CastleBuildStage,
                "A castle authoring session exists in Play mode; startup must restore the bake instead.");
            Assert.AreEqual(0.0, world.MaxCastleStageMs, 0.0001,
                "Play mode recorded castle authoring work even though the world should be baked.");

            var frameTimesMs = new List<double>(4096);
            var frameClock = Stopwatch.StartNew();
            int firstFrame = Time.frameCount;
            int convergedFrames = 0;
            bool sawSurfaceBuildWork = false;
            bool sawVisibleSolids = false;
            VoxelSurfaceMetrics lastMetrics = default;
            double deadline = Time.realtimeSinceStartupAsDouble + 120.0;

            // Measure a real gameplay window rather than stopping the instant the initial mesh tail
            // goes quiet. The baked startup neighbourhood is already resident, while StepStreaming
            // continues filling the outer load radius under its normal per-frame budget. This keeps
            // the regression sensitive to both initial surface extraction and steady startup
            // streaming without reintroducing castle construction into the measured path.
            while ((frameTimesMs.Count < MinimumMeasuredFrames
                    || convergedFrames < RequiredConvergedFrames)
                   && Time.realtimeSinceStartupAsDouble < deadline)
            {
                frameClock.Restart();
                yield return null;
                frameClock.Stop();
                frameTimesMs.Add(frameClock.Elapsed.TotalMilliseconds);

                Assert.AreEqual(0, world.CastleBuildStage,
                    "VoxelShowcase started castle authoring after gameplay began.");
                Assert.True(VoxelRenderBridge.TryGetWorld(out _),
                    "VoxelShowcase lost the production renderer world binding during startup.");

                lastMetrics = VoxelRenderBridge.SurfaceMetrics;
                Assert.AreEqual(0ul, lastMetrics.FramePathBlockingCompletionViolations,
                    "Geometry work synchronously completed a worker job from the frame path.");

                sawVisibleSolids |= lastMetrics.VisibleSolidChunks > 0;
                sawSurfaceBuildWork |= lastMetrics.CompletedSolidBuilds > 0
                                    || lastMetrics.RunningSolidJobs > 0
                                    || lastMetrics.SolidDirtyChunks > 0
                                    || lastMetrics.SolidMeshesAwaitingUpload > 0
                                    || lastMetrics.SolidPendingUploadBytes > 0;

                bool rendererConverged = lastMetrics.VisibleSolidChunks > 0
                                      && lastMetrics.SolidDirtyChunks == 0
                                      && lastMetrics.RunningSolidJobs == 0
                                      && lastMetrics.SolidMeshesAwaitingUpload == 0
                                      && lastMetrics.SolidPendingUploadBytes == 0
                                      && lastMetrics.MissingVisibleSolidChunks == 0;
                convergedFrames = rendererConverged ? convergedFrames + 1 : 0;
            }

            Assert.True(sawSurfaceBuildWork,
                "The measured window never observed production solid build work for the restored world.");
            Assert.True(sawVisibleSolids,
                "Production rendering never produced visible solid chunks during startup.");
            Assert.GreaterOrEqual(convergedFrames, RequiredConvergedFrames,
                $"Production solid rendering never converged after baked startup; "
              + $"dirty={lastMetrics.SolidDirtyChunks}, running={lastMetrics.RunningSolidJobs}, "
              + $"uploadMeshes={lastMetrics.SolidMeshesAwaitingUpload}, "
              + $"uploadBytes={lastMetrics.SolidPendingUploadBytes}, "
              + $"missingVisible={lastMetrics.MissingVisibleSolidChunks}, "
              + $"visible={lastMetrics.VisibleSolidChunks}.");
            Assert.GreaterOrEqual(frameTimesMs.Count, MinimumMeasuredFrames,
                "Showcase did not provide the requested gameplay-frame sample.");
            Assert.GreaterOrEqual(Time.frameCount - firstFrame, frameTimesMs.Count,
                "The coroutine advanced without corresponding player-loop frames.");

            frameTimesMs.Sort();
            double p95 = Percentile(frameTimesMs, 0.95);
            double p99 = Percentile(frameTimesMs, 0.99);
            double maximum = frameTimesMs[^1];

            Assert.Less(p95, MaxP95FrameMs,
                $"Live-showcase p95 was {p95:F2} ms (p99={p99:F2}, max={maximum:F2}).");
            Assert.Less(p99, MaxP99FrameMs,
                $"Live-showcase p99 was {p99:F2} ms (max={maximum:F2}).");
            Assert.Less(maximum, MaxSingleFrameMs,
                $"VoxelShowcase produced a {maximum:F2} ms player-loop hitch after baked startup; "
              + "no measured live frame may fall below 30 fps on the validation machine.");
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
