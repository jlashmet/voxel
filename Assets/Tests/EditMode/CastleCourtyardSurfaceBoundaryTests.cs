using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCourtyardSurfaceBoundaryTests
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
        public void SpatialCourtyardConsumesPlannedSurfaceMaskWhileCompatibilityKeepsLegacyRng()
        {
            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string courtyard = File.ReadAllText(Path.Combine(
                runtimeDirectory, "CastleCourtyardRealizer.cs"));
            string pipeline = File.ReadAllText(Path.Combine(
                runtimeDirectory, "CastleBuildPipeline.cs"));

            StringAssert.Contains("in _sitePlan,", pipeline,
                "Production spatial stage 5 must pass the frozen site/surface plan.");
            StringAssert.Contains("_courtyardBuildings", pipeline);

            StringAssert.Contains("bool hasPlannedSurface", courtyard);
            StringAssert.Contains("if (!hasPlannedSurface)", courtyard,
                "Runtime RNG initialization must remain gated to compatibility calls.");
            StringAssert.Contains("? sitePlan.ShouldUseCourtyardStone(x, z)", courtyard,
                "Spatial paving material must be resolved from the planner-owned surface mask.");
            StringAssert.Contains(": rng.NextInt(0, 100) < 82", courtyard,
                "The historical draw may remain only as the compatibility fallback.");

            StringAssert.Contains("true,\n                in sitePlan,", courtyard,
                "The production overload must mark the surface plan as authoritative.");
            StringAssert.Contains("false,\n                in unusedSitePlan,", courtyard,
                "The compatibility overload must remain explicit rather than silently sharing the planned path.");
        }
    }
}
