using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleBuildAdmissionBoundaryTests
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
        public void RuntimeUsesSinglePreflightForSpatialCastleAdmission()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string preflight = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleBuildPreflight.cs"));

            StringAssert.Contains("CastleBuildPreflight.EvaluateRuntimeReady(", pipeline);
            StringAssert.DoesNotContain("CastleCaveBuildReadiness.TryValidate(", pipeline,
                "Runtime must not grow a second cave admission path beside CastleBuildPreflight.");
            StringAssert.Contains("CastleCaveBuildReadiness.TryValidate(", preflight,
                "Runtime-ready preflight owns the designed-dungeon to natural-cave handoff.");
            StringAssert.Contains("CastleSpatialBuildReadinessIssue.MissingCavePlan", preflight);
            StringAssert.Contains("CastleSpatialBuildReadinessIssue.CaveEntranceMismatch", preflight);
        }
    }
}
