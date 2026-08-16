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
        public void LegacyKeepOffsetHasOneApiOwnedSourceOfTruth()
        {
            string layout = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api", "CastlePlan.cs"));
            string projection = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialProjection.cs"));
            string obsoleteAdapter = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepPlacementAdapter.cs");

            StringAssert.Contains("LegacyKeepCentreZOffset = 60", layout);
            StringAssert.Contains("CastleLayout.LegacyKeepCentreZOffset", projection);
            Assert.IsFalse(File.Exists(obsoleteAdapter),
                "Runtime must not keep a second castle keep-placement compatibility adapter.");
        }
    }
}
