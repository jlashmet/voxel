using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepProjectionBoundaryTests
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
        public void RuntimeKeepCoordinatorAndProjectionShareApiOwnedLegacyOffset()
        {
            string keep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));
            string projection = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialProjection.cs"));
            string layout = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastlePlan.cs"));

            StringAssert.Contains("CastleLayout.LegacyKeepCentreZOffset", keep);
            StringAssert.DoesNotContain("plan.Centre.z - hz + 60", keep,
                "The keep coordinator must not own a second copy of the compatibility anchor.");
            StringAssert.Contains("CastleLayout.LegacyKeepCentreZOffset", projection);
            StringAssert.Contains("LegacyKeepCentreZOffset = 60", layout);
        }
    }
}
