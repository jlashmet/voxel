using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleGatehouseRuntimeBoundaryTests
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
        public void SpatialRuntimeConsumesFrozenGatehouseRecipeWithoutRuntimePlanning()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string planned = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedGatehouseRealizer.cs"));

            StringAssert.Contains("CastlePlannedGatehouseRealizer.Build(", pipeline);
            StringAssert.Contains("topology.Gatehouse", pipeline);
            StringAssert.DoesNotContain("CastleGatehousePlanner.Create(", pipeline);
            StringAssert.DoesNotContain("CastleGatehousePlanner.Create(", planned);
            StringAssert.Contains("CastleTowerRealizer.BuildPlanned(", planned);
            StringAssert.Contains("gatehouse.LeftTowerSlits", planned);
            StringAssert.Contains("gatehouse.RightTowerSlits", planned);
        }
    }
}
