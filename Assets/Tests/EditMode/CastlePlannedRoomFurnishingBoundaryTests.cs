using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedRoomFurnishingBoundaryTests
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
        public void SpatialKeepUsesPlannerOwnedRoomAccentsWithoutRuntimeReroll()
        {
            string keep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));
            string furnisher = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleRoomFurnisher.cs"));

            StringAssert.Contains("CastleRoomFurnisher.FurnishPlanned(", keep);
            StringAssert.Contains("roomPlan.Accents", keep);
            StringAssert.DoesNotContain("RoomFurnishingPlanSeed", keep);

            int plannedStart = furnisher.IndexOf("internal static void FurnishPlanned(");
            int fixedStart = furnisher.IndexOf("private static void FurnishFixed(", plannedStart);
            Assert.GreaterOrEqual(plannedStart, 0);
            Assert.Greater(fixedStart, plannedStart);
            string plannedEntry = furnisher.Substring(plannedStart, fixedStart - plannedStart);

            StringAssert.Contains("FurnishPlannedAccents", plannedEntry);
            StringAssert.DoesNotContain("new Random", plannedEntry);
            StringAssert.DoesNotContain("NextInt(", plannedEntry);
            StringAssert.DoesNotContain("NextBool(", plannedEntry);

            StringAssert.Contains("FurnishLegacyAccents", furnisher);
            StringAssert.Contains("new Random(plan.Seed", furnisher,
                "Only the dimension-only compatibility path should retain historical room RNG.");
        }
    }
}
