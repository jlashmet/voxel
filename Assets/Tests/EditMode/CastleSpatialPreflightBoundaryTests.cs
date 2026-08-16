using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialPreflightBoundaryTests
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
        public void SpatialPipelineRequiresRuntimeReadyPreflightBeforeSnapshottingGeometry()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string preflight = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleBuildPreflight.cs"));

            StringAssert.Contains(
                "CastleBuildPreflight.EvaluateRuntimeReady(",
                pipeline);
            StringAssert.Contains("InvalidSpatialPlan", pipeline);
            StringAssert.Contains("IncompleteSpatialPlan", pipeline);
            StringAssert.Contains("ReadinessIssue", pipeline);
            StringAssert.Contains(
                "EstimateWrites(in CastlePlan plan, CastleSpatialPlan spatialPlan)",
                preflight);
            StringAssert.Contains("PolygonPerimeter(spatialPlan.InnerWardVertices)", preflight);
            StringAssert.Contains("spatialPlan.Towers.Length", preflight);
        }
    }
}
