using System;
using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCaveDecorationBoundaryTests
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
        public void SpatialDungeonConsumesFrozenCaveDecorationWithoutRuntimeRng()
        {
            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string pipeline = File.ReadAllText(Path.Combine(
                runtimeDirectory, "CastleBuildPipeline.cs"));
            string plannedDungeon = File.ReadAllText(Path.Combine(
                runtimeDirectory, "CastlePlannedDungeonRealizer.cs"));
            string plannedCave = File.ReadAllText(Path.Combine(
                runtimeDirectory, "CastlePlannedCaveDecorator.cs"));
            string legacyCave = File.ReadAllText(Path.Combine(
                runtimeDirectory, "CastleCaveRealizer.cs"));
            string preflight = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleBuildPreflight.cs"));

            StringAssert.Contains("CastlePlannedDungeonRealizer.Build(", pipeline);
            StringAssert.Contains("_spatialCaveDecoration", pipeline,
                "Spatial stage 7 must pass the frozen cave-decoration plan into realization.");
            StringAssert.Contains("CastlePlannedCaveDecorator.Build(", plannedDungeon);

            StringAssert.DoesNotContain("Random", plannedCave,
                "Spatial cave decoration must consume planned formations without rerolling them.");
            StringAssert.DoesNotContain("NextInt", plannedCave);
            StringAssert.DoesNotContain("NextBool", plannedCave);
            StringAssert.DoesNotContain("CastleSeedPartition.Derive", plannedCave);

            StringAssert.Contains("new Random(", legacyCave,
                "Dimension-only compatibility caves retain their historical RNG recipe.");
            StringAssert.Contains("MissingCaveDecorationPlan", preflight,
                "Runtime-ready preflight must reject a spatial castle without frozen cave decoration.");
        }
    }
}
