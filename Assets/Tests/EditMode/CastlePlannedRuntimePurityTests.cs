using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedRuntimePurityTests
    {
        [Test]
        public void PlannedCastleRealizersContainNoRandomOrPlannerCalls()
        {
            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string[] files = Directory.GetFiles(runtimeDirectory, "CastlePlanned*.cs");

            Assert.GreaterOrEqual(files.Length, 4,
                "Expected the spatial castle path to remain decomposed into planned realizers.");

            foreach (string file in files)
            {
                string source = File.ReadAllText(file);
                string name = Path.GetFileName(file);

                StringAssert.DoesNotContain("Unity.Mathematics.Random", source,
                    $"{name} must consume planned choices instead of owning an RNG.");
                StringAssert.DoesNotContain("CastleSeedPartition", source,
                    $"{name} must not derive new authored choices during realization.");
                StringAssert.DoesNotContain("Planner.Create(", source,
                    $"{name} must not invoke planning during voxel realization.");
            }
        }

        [Test]
        public void SpatialKeepUsesPlannedRoomAccentsWithoutSeedShim()
        {
            string keep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));
            string rooms = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleRoomFurnisher.cs"));

            StringAssert.Contains("CastleRoomFurnisher.FurnishPlanned(", keep);
            StringAssert.Contains("roomPlan.Accents", keep);
            StringAssert.DoesNotContain("RoomFurnishingPlanSeed", keep,
                "Spatial furnishing should consume explicit accents instead of adapting a seed back into Runtime RNG.");

            StringAssert.Contains("FurnishLegacyAccents", rooms,
                "Dimension-only compatibility builds must retain their historical RNG recipe.");
            StringAssert.Contains("FurnishPlannedAccents", rooms);
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
