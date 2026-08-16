using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepCompatibilityCirculationBoundaryTests
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
        public void KeepCoordinatorDelegatesHistoricalCirculationWithoutOwningGeometry()
        {
            string runtime = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string keep = File.ReadAllText(Path.Combine(runtime, "CastleKeepRealizer.cs"));
            string compatibility = File.ReadAllText(Path.Combine(
                runtime, "CastleKeepCompatibilityCirculationRealizer.cs"));
            string planned = File.ReadAllText(Path.Combine(
                runtime, "CastleKeepCirculationRealizer.cs"));

            StringAssert.Contains("CastleKeepCompatibilityCirculationRealizer.Build(", keep);
            StringAssert.DoesNotContain("private static void BuildCirculation", keep);
            StringAssert.Contains("brush.SpiralStair(", compatibility);
            StringAssert.Contains("brush.Arch(", compatibility);
            StringAssert.Contains("CastleKeepCirculationPlan", planned,
                "Spatial circulation must remain a planner-owned contract, not reuse the compatibility recipe.");
        }
    }
}
