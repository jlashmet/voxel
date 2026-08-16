using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepFloorExtractionTests
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
        public void KeepDelegatesFloorSlabsAndRoomDispatch()
        {
            string runtime = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string keep = File.ReadAllText(Path.Combine(runtime, "CastleKeepRealizer.cs"));
            string floors = File.ReadAllText(Path.Combine(runtime, "CastleKeepFloorRealizer.cs"));

            StringAssert.Contains("CastleKeepFloorRealizer.Build(", keep);
            StringAssert.DoesNotContain("private static void BuildFloorsAndRooms", keep);
            StringAssert.DoesNotContain("private static int FurnishingRecipe", keep);

            StringAssert.Contains("CastleRoomFurnisher.Furnish(", floors);
            StringAssert.Contains("CastleRoomFurnisher.FurnishPlanned(", floors);
            StringAssert.Contains("CastleKeepFloorPurpose.GreatHall", floors);
            StringAssert.Contains("CastleKeepFloorPurpose.Bedchamber", floors);
            StringAssert.Contains("CastleKeepFloorPurpose.LibraryAndStores", floors);
            StringAssert.DoesNotContain("SpiralStair", floors);
            StringAssert.DoesNotContain("CastleTowerRealizer", floors);
        }
    }
}
