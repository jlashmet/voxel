using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedDungeonTrapdoorBoundaryTests
    {
        [Test]
        public void PlannedDungeonRebuildsClosedCastleHatchAfterEntranceCarve()
        {
            string root = RepoRoot;
            string adapter = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedDungeonRealizer.cs"));

            StringAssert.Contains("DungeonRealizer.Build(ref brush, dungeonPlan);", adapter);
            StringAssert.Contains("BuildTrapdoor(ref brush, dungeonPlan.Entrance);", adapter);
            Assert.Less(
                adapter.IndexOf("DungeonRealizer.Build(ref brush, dungeonPlan);", System.StringComparison.Ordinal),
                adapter.IndexOf("BuildTrapdoor(ref brush, dungeonPlan.Entrance);", System.StringComparison.Ordinal),
                "The entrance carve must happen before the closed hatch is reconstructed.");
            StringAssert.Contains("CastleLayout.TrapdoorHalfSize", adapter);
            StringAssert.Contains("Mat.Wood", adapter);
            StringAssert.Contains("Mat.Gold", adapter);
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
