using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleShowcaseSpatialPresentationTests
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
        public void OptionalDungeonRoomsDoNotBecomeShowcaseRequirements()
        {
            string source = File.ReadAllText(Path.Combine(
                RepoRoot,
                "Assets",
                "VoxelEngine",
                "Composition",
                "Showcase",
                "ShowcaseCastleSpatialLayout.cs"));

            StringAssert.Contains(
                "FindRequiredRoom(dungeon, DungeonRoomPurpose.GreatHall)", source,
                "The semantically required great hall should remain a hard presentation invariant.");

            AssertOptional(source, "Archive");
            AssertOptional(source, "Puzzle");
            AssertOptional(source, "Treasury");
            AssertOptional(source, "CaveThreshold");

            StringAssert.Contains("lights = lightList.ToArray();", source);
            StringAssert.Contains("colours = colourList.ToArray();", source);
        }

        private static void AssertOptional(string source, string purpose)
        {
            StringAssert.Contains(
                $"TryFindRoom(dungeon, DungeonRoomPurpose.{purpose}", source,
                $"{purpose} presentation must be conditional when the dungeon planner omits it.");
            StringAssert.DoesNotContain(
                $"FindRequiredRoom(dungeon, DungeonRoomPurpose.{purpose})", source,
                $"{purpose} is optional dungeon content and must not block castle presentation.");
        }
    }
}
