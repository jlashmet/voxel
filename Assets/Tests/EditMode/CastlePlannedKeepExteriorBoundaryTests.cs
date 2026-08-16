using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedKeepExteriorBoundaryTests
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
        public void SpatialKeepExteriorConsumesPlannedRearOrielChoice()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string exterior = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedKeepExteriorRealizer.cs"));

            StringAssert.Contains("_hasSpatialKeep && _keepStage == 5", pipeline);
            StringAssert.Contains("CastlePlannedKeepExteriorRealizer.Build(", pipeline);
            StringAssert.Contains("annexes.HasRearOriel", exterior);
            StringAssert.Contains("BuildRearOriel(", exterior);
            StringAssert.DoesNotContain("CastleKeepAnnexPlanner.Create(", exterior,
                "Runtime must consume the planned annex choice rather than choose it again.");
        }
    }
}
