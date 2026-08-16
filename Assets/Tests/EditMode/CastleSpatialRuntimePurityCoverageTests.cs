using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialRuntimePurityCoverageTests
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
        public void RemainingPlannedRealizersDoNotOwnAuthoredRandomness()
        {
            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string[] realizationFiles =
            {
                "CastlePosternRealizer.cs",
                "CastleWallDoorRealizer.cs",
                "CastlePlannedCaveDecorator.cs",
            };

            for (int i = 0; i < realizationFiles.Length; i++)
            {
                string source = File.ReadAllText(Path.Combine(runtimeDirectory, realizationFiles[i]));
                StringAssert.DoesNotContain("new Random(", source,
                    $"{realizationFiles[i]} must consume planned variation rather than create an RNG.");
                StringAssert.DoesNotContain("CastleSeedPartition.Derive(", source,
                    $"{realizationFiles[i]} must not derive authored seeds during realization.");
            }
        }
    }
}
