using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseCastleSpawnActivationTests
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
        public void SceneDriverSpawnsAndLooksFromThePlannedCastleGeometry()
        {
            string driver = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "Scenes", "Showcase", "VoxelShowcase.cs"));
            string spawn = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.CastleSpawn.cs"));
            string planner = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseCastleSpawnPlanner.cs"));

            StringAssert.Contains("_world.CastleSpawnPosition()", driver);
            StringAssert.Contains("_world.CastleLookTargetPosition()", driver);
            StringAssert.DoesNotContain("_world.SpawnPosition()", driver,
                "The active showcase must not fall back to the historical fixed -Z spawn.");

            StringAssert.Contains("CastleBuildBoundsResolver.Resolve(", spawn);
            StringAssert.Contains("ShowcaseCastleSpawnPlanner.PlanColumn(", spawn);
            StringAssert.Contains("projection.KeepCentreWorld", spawn);
            StringAssert.Contains("projection.Approach", planner);
            StringAssert.Contains("BuildClearance", planner);
        }
    }
}
