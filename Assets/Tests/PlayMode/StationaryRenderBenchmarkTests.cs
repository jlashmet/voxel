using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Source contract for the standalone stationary benchmark. Selecting this test through the
    /// single-test workflow also maps it to VoxelShowcase and executes the real-player benchmark.
    /// </summary>
    public sealed class StationaryRenderBenchmarkTests
    {
        [Test]
        public void StationaryBenchmarkIsSeparatedFromSurveyCapture()
        {
            string harness = File.ReadAllText(
                "Assets/Scenes/Showcase/StationaryRenderBenchmarkHarness.cs");
            string composition = File.ReadAllText(
                "Assets/VoxelEngine/Composition/RenderingDiagnosticsComposition.cs");
            string build = File.ReadAllText(
                "Assets/Scenes/Showcase/Editor/ShowcasePlayerBuild.cs");
            string capture = File.ReadAllText("tools/showcase-player-capture.sh");

            StringAssert.Contains("state.IsConverged", harness);
            StringAssert.Contains("ConvergedFramesRequired = 30", harness);
            StringAssert.Contains("ResetSolidRenderBenchmark()", harness);
            StringAssert.Contains("MinimumRendererSamples = 128", harness);
            StringAssert.Contains("cameraStable=", harness);
            StringAssert.Contains("projectionStable=", harness);
            StringAssert.Contains("prepare.ms[p50=", harness);
            StringAssert.Contains("visibility.ms[p50=", harness);
            StringAssert.Contains("staging.ms[p50=", harness);
            StringAssert.Contains("submission.ms[p50=", harness);
            StringAssert.Contains("FrameTimingManager.GetLatestTimings", harness);
            StringAssert.Contains("cpuMainThreadFrameTime", harness);
            StringAssert.Contains("cpuRenderThreadFrameTime", harness);
            StringAssert.Contains("gpuFrameTime", harness);
            StringAssert.Contains("StartCoroutine(CaptureAndQuit(pass))", harness);
            StringAssert.Contains("ScreenCapture.CaptureScreenshot(path)", harness);
            StringAssert.DoesNotContain("AutoSurvey", harness);
            StringAssert.DoesNotContain("AutoWalk", harness);

            int sampleStart = harness.IndexOf("private void SampleFrame", System.StringComparison.Ordinal);
            int sampleEnd = harness.IndexOf("private void CaptureUnityFrameTiming", sampleStart,
                                            System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(sampleStart, 0);
            Assert.Greater(sampleEnd, sampleStart);
            string measuredFramePath = harness.Substring(sampleStart, sampleEnd - sampleStart);
            StringAssert.DoesNotContain("ScreenCapture", measuredFramePath,
                "screenshots must stay outside the measured stationary interval");
            StringAssert.DoesNotContain("Debug.Log", measuredFramePath,
                "per-frame logging would perturb the measured stationary interval");

            StringAssert.Contains("VoxelSolidRenderTelemetry.Snapshot", composition);
            StringAssert.Contains("metrics.SchedulerPrepareTiming", composition);
            StringAssert.Contains("metrics.VisibilityTiming", composition);
            StringAssert.Contains("RunningSolidJobs", composition);
            StringAssert.Contains("SolidMeshesAwaitingUpload", composition);

            StringAssert.Contains("-voxelFrameTimingStats", build);
            StringAssert.Contains("PlayerSettings.enableFrameTimingStats = true", build);
            StringAssert.Contains("PlayerSettings.enableFrameTimingStats = previousFrameTimingStats", build);

            StringAssert.Contains("VoxelEngine.Tests.PlayMode.StationaryRenderBenchmarkTests", capture);
            StringAssert.Contains(": \"${STATIONARY_SAMPLE:=10}\"", capture);
            StringAssert.Contains("BUILD_ARGS+=(-voxelFrameTimingStats)", capture);
            StringAssert.Contains("-voxel-stationary-sample-seconds", capture);
            StringAssert.Contains("-voxel-stationary-timeout-seconds", capture);
            StringAssert.Contains("-voxel-stationary-screenshot-dir", capture);
            StringAssert.Contains("post-measurement screenshot", capture);
            StringAssert.Contains("stationary benchmark did not publish a passing result", capture);

            // The moving traversal correctness test runs under the generic one-job-worker Unity
            // test process, so it must also map to the actual production player before its timing
            // can be interpreted as renderer performance.
            StringAssert.Contains(
                "ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap",
                capture);
            StringAssert.Contains(": \"${AUTOWALK_AFTER:=60}\"", capture);
            StringAssert.Contains("=== REAL PLAYER FPS TAIL ===", capture);

            // The existing visual profile remains a distinct moving/screenshot run.
            StringAssert.Contains(": \"${SURVEY_AFTER:=10}\"", capture);
            StringAssert.Contains("-voxel-screenshot-every 10", capture);
        }
    }
}
