using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleGatehouseWallStyleBoundaryTests
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
        public void SpatialGatehouseUsesSameFrozenCrenellationStyleAsCurtainWalls()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string gatehouse = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedGatehouseRealizer.cs"));

            StringAssert.Contains("in _gatehousePlan,\n                            in _wallPlan", pipeline);
            StringAssert.Contains("in CastleWallPlan walls", gatehouse);
            StringAssert.Contains("walls.CrenellationMerlonLength", gatehouse);
            StringAssert.Contains("walls.CrenellationGapLength", gatehouse);
            StringAssert.Contains("walls.CrenellationHeight", gatehouse);
            StringAssert.DoesNotContain("const float merlon = 26", gatehouse);
            StringAssert.DoesNotContain("const float gap = 18", gatehouse);
        }
    }
}
