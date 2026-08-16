using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialDungeonAdmissionBoundaryTests
    {
        [Test]
        public void SpatialRuntimeRequiresCompletedDungeonAndNeverFallsBackToLegacyRecipe()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot,
                "Assets",
                "VoxelEngine",
                "Structures",
                "Runtime",
                "CastleBuildPipeline.cs"));

            StringAssert.Contains("CastleBuildPreflight.EvaluateRuntimeReady(", pipeline);
            StringAssert.Contains("CastleBuildPreflightIssue.IncompleteSpatialPlan", pipeline);
            StringAssert.Contains("CastlePlannedDungeonRealizer.Build(", pipeline);
            StringAssert.Contains("_spatialDungeonPlan == null", pipeline);
            StringAssert.Contains(
                "Spatial castle reached dungeon realization without a planned dungeon.",
                pipeline);

            StringAssert.DoesNotContain(
                "_hasSpatialKeep && _spatialDungeonPlan != null",
                pipeline,
                "A spatial build must fail closed rather than conditionally fall back to the legacy dungeon.");
        }

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
    }
}
