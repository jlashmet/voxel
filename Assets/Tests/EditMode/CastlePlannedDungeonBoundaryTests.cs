using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedDungeonBoundaryTests
    {
        [Test]
        public void RuntimeConsumesDungeonAndCavePlansWithoutPlanningTopology()
        {
            string root = RepoRoot;
            string adapter = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedDungeonRealizer.cs"));
            string genericDungeon = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime",
                "DungeonRealizer.cs"));
            string pipeline = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));

            StringAssert.Contains("DungeonRealizer.Build(", adapter);
            StringAssert.Contains("CaveRealizer.Build(", adapter);
            StringAssert.Contains("CavePlan cavePlan", adapter);
            StringAssert.Contains("CaveThresholdRoomId", adapter);
            StringAssert.DoesNotContain("DungeonPlanner.Create(", adapter);
            StringAssert.DoesNotContain("CavePlanner.Create(", adapter);
            StringAssert.DoesNotContain("CastleCavePlanning.Create(", adapter);
            StringAssert.DoesNotContain("CastleCaveRealizer", genericDungeon,
                "The generic dungeon realizer must stop at the designed-to-natural handoff.");

            StringAssert.Contains("CavePlanSnapshot.CloneValidated", pipeline);
            StringAssert.Contains("_spatialDungeonPlan, _spatialCavePlan", pipeline);
            StringAssert.DoesNotContain(
                "in dungeonPlan, _spatialDungeonPlan",
                pipeline,
                "Spatial stage 7 must not route through the compatibility fixed-cave overload.");
        }

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
    }
}
