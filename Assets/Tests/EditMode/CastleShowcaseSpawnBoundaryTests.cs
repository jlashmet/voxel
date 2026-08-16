using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleShowcaseSpawnBoundaryTests
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
        public void CastleSpawnUsesPlannedGateFrameAndCompleteBuildEnvelope()
        {
            string source = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseCastleSpawnPlanner.cs"));

            StringAssert.Contains("projection.Approach", source);
            StringAssert.Contains("bounds.Min", source);
            StringAssert.Contains("bounds.MaxExclusive", source);
            StringAssert.Contains("approach.Outward", source);
            StringAssert.Contains("approach.LocalPoint(", source);

            StringAssert.DoesNotContain("-220", source,
                "Spatial spawn must not retain the historical fixed southern coordinate.");
            StringAssert.DoesNotContain("new float2(0f, -1f)", source,
                "Spatial spawn direction must come from the planned primary gate.");
        }
    }
}
