using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedRealizerPurityTests
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
        public void PlannedCastleRuntimeComponentsDoNotRerollPlannerChoices()
        {
            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string[] files = Directory.GetFiles(runtimeDirectory, "CastlePlanned*.cs");
            Assert.Greater(files.Length, 0, "Expected planned castle runtime components.");

            string[] forbidden =
            {
                "Unity.Mathematics.Random",
                "new Random(",
                "CastleSeedPartition.",
                "CastleLayoutPlanner.",
                "CastleSpatialPlanner.",
                "CastleLandscapePlanner.",
                "CastleCavePlanning.",
                "DungeonPlanner.",
                "CavePlanner.",
            };

            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string source = File.ReadAllText(files[fileIndex]);
                for (int forbiddenIndex = 0; forbiddenIndex < forbidden.Length; forbiddenIndex++)
                {
                    StringAssert.DoesNotContain(
                        forbidden[forbiddenIndex],
                        source,
                        $"{Path.GetFileName(files[fileIndex])} must consume frozen plan data; " +
                        $"it may not use {forbidden[forbiddenIndex]} during realization.");
                }
            }
        }
    }
}
