using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedDungeonPipelineBoundaryTests
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
        public void CompletedSpatialBuildConsumesDungeonPlanWhileCompatibilityPathRemains()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string completion = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialPlanCompletion.cs"));

            StringAssert.Contains("_spatialDungeonPlan = spatialPlan.Dungeon", pipeline);
            StringAssert.Contains("CastlePlannedDungeonRealizer.Build(", pipeline);
            StringAssert.Contains("CastleDungeonRealizer.Build(", pipeline,
                "Compatibility builds still need the historical dungeon fallback.");
            StringAssert.Contains("AttachDungeon(in plan", completion);
            StringAssert.Contains("DungeonPlan dungeon", completion);
        }
    }
}
