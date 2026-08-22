using System;
using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Source contract for standalone renderer benchmarks selected through the single-test workflow.
    /// </summary>
    public sealed class StationaryRenderBenchmarkTests
    {
        [Test]
        public void SmallVoxelShowcaseMovingBuild12()
        {
            AssertSmallMovingProfile(nameof(SmallVoxelShowcaseMovingBuild12), "12");
        }

        [Test]
        public void SmallVoxelShowcaseMovingBuild8()
        {
            AssertSmallMovingProfile(nameof(SmallVoxelShowcaseMovingBuild8), "8");
        }

        private static void AssertSmallMovingProfile(string method, string convergingBuilds)
        {
            string capture = File.ReadAllText("tools/showcase-player-capture.sh");
            string filter = $"VoxelEngine.Tests.PlayMode.StationaryRenderBenchmarkTests.{method}";
            int start = capture.IndexOf(filter, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, $"missing real-player profile for {filter}");
            int end = capture.IndexOf(";;", start, StringComparison.Ordinal);
            Assert.Greater(end, start, $"unterminated real-player profile for {filter}");
            string profile = capture.Substring(start, end - start);

            StringAssert.Contains("Assets/Scenes/SmallVoxelShowcase.unity", profile);
            StringAssert.Contains(": \"${RUN_SECONDS:=90}\"", profile);
            StringAssert.Contains(": \"${AUTOWALK_AFTER:=20}\"", profile);
            StringAssert.Contains($": \"${{CONVERGING_BUILDS:={convergingBuilds}}}\"", profile);
        }

        [Test]
        public void StationaryBenchmarkIsSeparatedFromSurveyCapture()
        {
            string harness = File.ReadAllText(
                "Assets/Scenes/Showcase/StationaryRenderBenchmarkHarness.cs");
            string concurrencyHarness = File.ReadAllText(
                "Assets/Scenes/Showcase/SurfaceBuildConcurrencyHarness.cs");
            string prepareHarness = File.ReadAllText(
                "Assets/Scenes/Showcase/SurfacePrepareTimingHarness.cs");
            string composition = File.ReadAllText(
                "Assets/VoxelEngine/Composition/RenderingDiagnosticsComposition.cs");
            string build = File.ReadAllText(
                "Assets/Scenes/Showcase/Editor/ShowcasePlayerBuild.cs");
            string capture = File.ReadAllText("tools/showcase-player-capture.sh");
            string singleWorkflow = File.ReadAllText(".github/workflows/tests-single.yml");

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

            int sampleStart = harness.IndexOf("private void SampleFrame", StringComparison.Ordinal);
            int sampleEnd = harness.IndexOf("private void CaptureUnityFrameTiming", sampleStart,
                                            StringComparison.Ordinal);
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

            StringAssert.Contains(
                "VoxelEngine.Tests.PlayMode.ShowcaseTraversalPerformanceTests.*", singleWorkflow);
            StringAssert.Contains("WORKER_ARGS=(-job-worker-count 8)", singleWorkflow);
            StringAssert.Contains("=== UNITY TEST FAILURE DETAILS ===", singleWorkflow,
                "failed traversal assertions must stay visible when artifact upload is unavailable");
            StringAssert.Contains("group: voxel-single-test-self-hosted-mac", singleWorkflow);
            StringAssert.Contains("cancel-in-progress: false", singleWorkflow,
                "new requests should replace stale pending work without killing the active Unity run");
            StringAssert.Contains(
                "ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap",
                capture);
            StringAssert.Contains(": \"${AUTOWALK_AFTER:=60}\"", capture);
            StringAssert.Contains("-voxel-converging-builds", capture);
            StringAssert.Contains("-voxel-converging-builds", concurrencyHarness);
            StringAssert.Contains("SetVoxelBuildConcurrency(converging, 0)", concurrencyHarness,
                "the concurrency A/B must preserve the production converged ceiling");
            StringAssert.Contains("GetSurfaceBenchmarkState()", prepareHarness);
            StringAssert.Contains("state.RunningSolidJobs", prepareHarness);
            StringAssert.Contains("state.MissingVisibleSolidChunks", prepareHarness);
            StringAssert.Contains("jobs={31} missing={32}", prepareHarness,
                "the sparse player diagnostic must correlate prepare windows with live geometry pressure");
            StringAssert.Contains("=== REAL PLAYER FPS TAIL ===", capture);
            StringAssert.Contains("=== REAL PLAYER PREPARE SECTIONS ===", capture);
            StringAssert.Contains("PREPARESECTIONS", capture);
            StringAssert.Contains("=== REAL PLAYER SURFACE TAIL ===", capture);
            StringAssert.Contains("SURFACE t=", capture);
            StringAssert.Contains("=== REAL PLAYER RINGS TAIL ===", capture);
            StringAssert.Contains("RINGS ", capture);

            StringAssert.Contains(": \"${SURVEY_AFTER:=10}\"", capture);
            StringAssert.Contains("-voxel-screenshot-every 10", capture);
        }
    }
}
