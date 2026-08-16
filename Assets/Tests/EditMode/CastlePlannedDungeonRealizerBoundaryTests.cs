using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedDungeonRealizerBoundaryTests
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
        public void PlannedDungeonCarvesThenFurnishesThenRestoresTrapdoorBeforePlannedCave()
        {
            string source = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedDungeonRealizer.cs"));

            int carve = source.IndexOf("DungeonRealizer.Build(ref brush, dungeonPlan)");
            int furnish = source.IndexOf("DungeonRoomFurnisher.FurnishAll(ref brush, dungeonPlan)");
            int hatch = source.IndexOf("BuildTrapdoor(ref brush, dungeonPlan.Entrance)");
            int cave = source.IndexOf("CaveRealizer.Build(ref brush, cavePlan)");

            Assert.GreaterOrEqual(carve, 0, "Planned dungeon must realize its room graph.");
            Assert.Greater(furnish, carve,
                "Semantic furnishing must happen after room/corridor carving.");
            Assert.Greater(hatch, furnish,
                "Entrance carving clears the hatch plane, so the authored trapdoor must be restored last.");
            Assert.Greater(cave, hatch,
                "Planned natural cave continuation stays downstream of designed dungeon realization.");

            StringAssert.Contains("new int3(half * 2, 2, half * 2)", source,
                "The planned path must restore the same closed wood hatch footprint used by interaction.");
            StringAssert.Contains("Mat.Wood", source);
            StringAssert.Contains("Mat.Gold", source);
            StringAssert.DoesNotContain("CastleCaveRealizer", source,
                "Spatial dungeon realization must consume CavePlan instead of falling back to fixed castle cave geometry.");
            StringAssert.DoesNotContain("CastlePlan keepPlan", source,
                "Planned dungeon/cave realization must not require castle-scale geometry once planning is complete.");
        }

        [Test]
        public void GenericDungeonFurnisherOwnsPurposeDrivenRoomDetail()
        {
            string source = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "DungeonRoomFurnisher.cs"));

            StringAssert.Contains("DungeonRoomPurpose.Archive", source);
            StringAssert.Contains("DungeonRoomPurpose.GreatHall", source);
            StringAssert.Contains("DungeonRoomPurpose.Puzzle", source);
            StringAssert.Contains("DungeonRoomPurpose.Treasury", source);
            StringAssert.Contains("DungeonRoomPurpose.CaveThreshold", source);
            StringAssert.DoesNotContain("CastlePlan", source,
                "Reusable dungeon furnishing must not depend on castle placement/dimensions.");
        }
    }
}
