using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseCastleDungeonPresentationBoundaryTests
    {
        [Test]
        public void CastlePresentationFollowsTheCompletedDungeonGraph()
        {
            string root = RepoRoot;
            string layout = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseCastleSpatialLayout.cs"));
            string world = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.CastleSpatial.cs"));

            StringAssert.Contains("DungeonPlan dungeon", layout);
            StringAssert.Contains("DungeonPlanValidator.TryValidate", layout);
            StringAssert.Contains("DungeonRoomPurpose.Archive", layout);
            StringAssert.Contains("DungeonRoomPurpose.GreatHall", layout);
            StringAssert.Contains("DungeonRoomPurpose.Puzzle", layout);
            StringAssert.Contains("DungeonRoomPurpose.Treasury", layout);
            StringAssert.Contains("DungeonRoomPurpose.CaveThreshold", layout);
            StringAssert.Contains("RoomFloorY(in cave)", layout);

            StringAssert.DoesNotContain("caveZ = trapZ - 411", layout,
                "Cave presentation must follow the seeded CaveThreshold rather than legacy -Z geometry.");
            StringAssert.DoesNotContain("keepCentreX + 226", layout,
                "Side-room lights must follow planned puzzle/treasury room centres.");
            StringAssert.DoesNotContain("keepCentreX - 226", layout,
                "Side-room lights must follow planned puzzle/treasury room centres.");

            StringAssert.Contains("_plannedCastle.Spatial.Dungeon", world,
                "The showcase must pass the same completed dungeon graph that Runtime realizes.");
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
