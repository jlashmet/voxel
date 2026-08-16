using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedDungeonBoundaryTests
    {
        [Test]
        public void RuntimeConsumesDungeonPlanWithoutPlanningAndKeepsCavesSeparate()
        {
            string root = RepoRoot;
            string adapter = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedDungeonRealizer.cs"));
            string generic = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime",
                "DungeonRealizer.cs"));

            StringAssert.Contains("DungeonRealizer.Build(", adapter);
            StringAssert.Contains("CastleCaveRealizer.Build(", adapter);
            StringAssert.Contains("CaveThresholdRoomId", adapter);
            StringAssert.DoesNotContain("DungeonPlanner.Create(", adapter);
            StringAssert.DoesNotContain("CastleDungeonPlanner.Create(", adapter);
            StringAssert.DoesNotContain("CastleCaveRealizer", generic,
                "The generic dungeon realizer must stop at the designed-to-natural handoff.");
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
