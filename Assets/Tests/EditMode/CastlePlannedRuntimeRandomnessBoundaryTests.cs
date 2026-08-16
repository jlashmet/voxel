using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedRuntimeRandomnessBoundaryTests
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
        public void SpatialCastleRealizersDoNotPlanOrDrawAuthoredRandomness()
        {
            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");

            foreach (string file in Directory.GetFiles(runtimeDirectory, "CastlePlanned*.cs"))
                AssertRealizationOnly(file);

            string[] spatialHelpers =
            {
                "CastleKeepCirculationRealizer.cs",
                "CastleWallDoorRealizer.cs",
                "CastleInnerWardTowerRealizer.cs",
                "CastleRearOrielRealizer.cs",
                "CastleKeepAnnexRealizer.cs",
                "DungeonRealizer.cs",
                "DungeonRoomFurnisher.cs",
                "CaveRealizer.cs",
            };

            for (int i = 0; i < spatialHelpers.Length; i++)
                AssertRealizationOnly(Path.Combine(runtimeDirectory, spatialHelpers[i]));
        }

        private static void AssertRealizationOnly(string path)
        {
            string source = File.ReadAllText(path);
            string name = Path.GetFileName(path);

            StringAssert.DoesNotContain("Unity.Mathematics.Random", source,
                $"{name} must consume frozen plan variation rather than construct RNG state.");
            StringAssert.DoesNotContain("using Random =", source,
                $"{name} must not alias an RNG type on the spatial realization path.");
            StringAssert.DoesNotContain("new Random(", source,
                $"{name} must not draw authored randomness during voxel mutation.");
            StringAssert.DoesNotContain("CastleSeedPartition", source,
                $"{name} must not derive authored variation during voxel mutation.");
            StringAssert.DoesNotContain("Planner.Create(", source,
                $"{name} must consume completed plans rather than invoke a planner.");
            StringAssert.DoesNotContain("Planner.TryValidate(", source,
                $"{name} must depend on validators, not planner-owned validation entry points.");
        }
    }
}
