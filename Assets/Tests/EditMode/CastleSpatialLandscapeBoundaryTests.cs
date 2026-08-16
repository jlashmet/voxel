using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialLandscapeBoundaryTests
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
        public void SpatialLandscapeUsesPlannedPerimeterAndApproachWithoutLegacyWaterfall()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string spatial = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleSpatialLandscapeRealizer.cs"));

            StringAssert.Contains("CastleSpatialLandscapeRealizer.Build(", pipeline);
            StringAssert.Contains("CastleApproachFrame", spatial);
            StringAssert.Contains("localPerimeter", spatial);
            StringAssert.DoesNotContain("Waterfall", spatial);
            StringAssert.DoesNotContain("BaileyHalfX", spatial);
            StringAssert.DoesNotContain("BaileyHalfZ", spatial);
        }
    }
}
