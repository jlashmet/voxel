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
            StringAssert.DoesNotContain("ScreenCapture", harness);
            StringAssert.DoesNotContain("AutoSurvey", harness);
            StringAssert.DoesNotContain("AutoWalk", harness);

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
            StringAssert.Contains("no screenshots", capture);
            StringAssert.Contains("stationary benchmark did not publish a passing result", capture);

            // The existing visual profile remains a distinct moving/screenshot run.
            StringAssert.Contains(": \"${SURVEY_AFTER:=10}\"", capture);
            StringAssert.Contains("-voxel-screenshot-every 10", capture);
        }
    }
}
