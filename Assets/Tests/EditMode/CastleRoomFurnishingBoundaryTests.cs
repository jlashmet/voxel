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
            string keep = ReadRuntime("CastleKeepRealizer.cs");
            string furnisher = ReadRuntime("CastleRoomFurnisher.cs");
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

        [Test]
        public void SpatialKeepSelectsFurnishingRecipeFromPlannedPurposeNotPhysicalFloor()
        {
            string keep = ReadRuntime("CastleKeepRealizer.cs");

            StringAssert.Contains("FurnishingRecipe(in roomPlan, f)", keep);
            StringAssert.Contains("switch (roomPlan.Purpose)", keep);
            StringAssert.Contains("case CastleKeepFloorPurpose.GreatHall:", keep);
            StringAssert.Contains("case CastleKeepFloorPurpose.Bedchamber:", keep);
            StringAssert.Contains("case CastleKeepFloorPurpose.LibraryAndStores:", keep);
            StringAssert.Contains("return 0;", keep);
            StringAssert.Contains("return 1;", keep);
            StringAssert.Contains("return 2;", keep);

            StringAssert.DoesNotContain(
                "FurnishPlanned(ref brush, in plan, min, size, y, f,",
                keep,
                "Planned furnishing must not derive room semantics from the physical floor index.");
        }

        private static string ReadRuntime(string file) =>
            File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime", file));
    }
}
