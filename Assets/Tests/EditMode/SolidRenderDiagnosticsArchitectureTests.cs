using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SolidRenderDiagnosticsArchitectureTests
    {
        [Test]
        public void SolidRenderPathAggregatesStagingAndSubmissionDiagnostics()
        {
            string renderPass = File.ReadAllText(
                "Assets/VoxelEngine/Rendering/Runtime/RenderFeature/VoxelRenderPass.cs");
            string diagnostics = File.ReadAllText(
                "Assets/VoxelEngine/Rendering/Runtime/RenderFeature/VoxelSolidRenderDiagnostics.cs");

            StringAssert.Contains("long solidStagingStart = VoxelSolidRenderTelemetry.Timestamp()", renderPass);
            StringAssert.Contains("solidSubmissionCalls++", renderPass);
            StringAssert.Contains("data.VisibleSolidCount = transvoxelVisible.Count", renderPass);
            StringAssert.Contains("VoxelSolidRenderTelemetry.Record(", renderPass);
            StringAssert.Contains("VoxelTimingWindow StagingTiming", diagnostics);
            StringAssert.Contains("VoxelTimingWindow SubmissionTiming", diagnostics);
            StringAssert.Contains("Stopwatch.GetTimestamp()", diagnostics);
            StringAssert.Contains("public static VoxelSolidRenderDiagnostics Snapshot", diagnostics);
            StringAssert.Contains("public static void Reset()", diagnostics);
            StringAssert.DoesNotContain("Debug.Log", diagnostics);
            StringAssert.DoesNotContain("Debug.Log", renderPass);
        }
    }
}
