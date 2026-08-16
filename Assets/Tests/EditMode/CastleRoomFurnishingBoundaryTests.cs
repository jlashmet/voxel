using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleRoomFurnishingBoundaryTests
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
        public void SpatialKeepConsumesPlannerOwnedRoomAccents()
        {
            string keep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));
            string furnisher = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleRoomFurnisher.cs"));
            string planning = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleKeepInteriorPlan.cs"));

            StringAssert.Contains("CastleRoomAccentPlanner.Create(", planning);
            StringAssert.Contains("CastleRoomFurnisher.FurnishPlanned(", keep);
            StringAssert.Contains("roomPlan.Accents", keep);
            StringAssert.DoesNotContain("RoomFurnishingPlanSeed", keep);
            StringAssert.DoesNotContain("furnishingPlan.Seed", keep);

            StringAssert.Contains("FurnishLegacyAccents(", furnisher,
                "The dimension-only compatibility path should keep its historical RNG recipe.");
            StringAssert.Contains("FurnishPlannedAccents(", furnisher);
            StringAssert.Contains("CastleRoomAccentPlan accents", furnisher);
        }
    }
}
