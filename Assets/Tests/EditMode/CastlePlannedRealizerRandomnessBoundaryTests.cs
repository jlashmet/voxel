using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedRealizerRandomnessBoundaryTests
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
        public void EveryPlannedRealizerConsumesFrozenVariationInsteadOfDrawingRandomness()
        {
            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string[] files = Directory.GetFiles(runtimeDirectory, "CastlePlanned*.cs");

            Assert.Greater(files.Length, 0,
                "The planned-realizer naming convention changed or the planned path disappeared.");

            for (int i = 0; i < files.Length; i++)
            {
                string source = File.ReadAllText(files[i]);
                string fileName = Path.GetFileName(files[i]);

                StringAssert.DoesNotContain("new Random(", source,
                    $"{fileName} must consume planned variation rather than create an RNG.");
                StringAssert.DoesNotContain("NextInt(", source,
                    $"{fileName} must not draw authored integer variation during realization.");
                StringAssert.DoesNotContain("NextFloat(", source,
                    $"{fileName} must not draw authored floating-point variation during realization.");
                StringAssert.DoesNotContain("CastleSeedPartition.Derive(", source,
                    $"{fileName} must not derive authored seeds during realization.");
            }
        }
    }
}
