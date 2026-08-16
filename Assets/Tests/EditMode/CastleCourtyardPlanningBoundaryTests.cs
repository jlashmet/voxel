using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCourtyardPlanningBoundaryTests
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
        public void SpatialCourtyardConsumesPlannedWellWithoutRuntimePlacement()
        {
            string planner = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialPlanner.cs"));
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string courtyard = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleCourtyardRealizer.cs"));

            StringAssert.Contains("CastleCourtyardPlacementGeometry.TryChooseWell(", planner);
            StringAssert.Contains("spatialPlan.HasWell", pipeline);
            StringAssert.Contains("spatialPlan.WellCentre", pipeline);
            StringAssert.Contains("_hasSpatialWell", pipeline);
            StringAssert.Contains("_spatialWellCentre", pipeline);
            StringAssert.Contains("bool hasWell", courtyard);
            StringAssert.Contains("int2 localWellCentre", courtyard);
            StringAssert.DoesNotContain("TryChooseWell", courtyard,
                "Runtime courtyard realization must not choose semantic well placement.");
        }
    }
}
