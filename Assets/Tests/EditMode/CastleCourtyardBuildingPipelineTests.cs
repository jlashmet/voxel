using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCourtyardBuildingPipelineTests
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
        public void CompletedBuildingSpecsFlowIntoReusableRuntimeRealizer()
        {
            string terrainPlanning = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "CastleTerrainPlanning.cs"));
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string courtyard = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleCourtyardRealizer.cs"));

            StringAssert.Contains("CastleSpatialPlanCompletion.AttachCourtyardBuildings", terrainPlanning);
            StringAssert.Contains("spatialPlan.CourtyardBuildings.Clone()", pipeline);
            StringAssert.Contains("_courtyardBuildings);", pipeline);
            StringAssert.Contains("CastleCourtyardBuildingRealizer.BuildAll", courtyard);

            StringAssert.DoesNotContain("HalfExtents", courtyard);
            StringAssert.DoesNotContain("EntranceDirection", courtyard);
            StringAssert.DoesNotContain("RoofRidgeAlongX", courtyard);
            StringAssert.DoesNotContain("CastleCourtyardBuildingPlanner.Create", pipeline,
                "Runtime must consume completed building specs rather than re-plan courtyard semantics.");
        }
    }
}
