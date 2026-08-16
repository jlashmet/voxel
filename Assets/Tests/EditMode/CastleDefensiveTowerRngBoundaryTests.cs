using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleDefensiveTowerRngBoundaryTests
    {
        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                    dir = dir.Parent;
                Assert.NotNull(dir);
                return dir.FullName;
            }
        }

        [Test]
        public void MigratedDefensiveTowerPathsConsumeFrozenSlitPlans()
        {
            string runtime = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string outer = File.ReadAllText(Path.Combine(runtime, "CastlePlannedTowerRealizer.cs"));
            string inner = File.ReadAllText(Path.Combine(runtime, "CastleInnerWardTowerRealizer.cs"));
            string gatehouse = File.ReadAllText(Path.Combine(runtime, "CastlePlannedGatehouseRealizer.cs"));

            StringAssert.Contains("CastleTowerRealizer.BuildPlanned(", outer);
            StringAssert.Contains("tower.Slits", outer);
            StringAssert.DoesNotContain("new Random", outer);

            StringAssert.Contains("CastleTowerRealizer.BuildPlanned(", inner);
            StringAssert.Contains("tower.Slits", inner);
            StringAssert.DoesNotContain("new Random", inner);

            StringAssert.Contains("CastleTowerRealizer.BuildPlanned(", gatehouse);
            StringAssert.Contains("gatehouse.LeftTowerSlits", gatehouse);
            StringAssert.Contains("gatehouse.RightTowerSlits", gatehouse);
            StringAssert.DoesNotContain("new Random", gatehouse);
        }
    }
}
