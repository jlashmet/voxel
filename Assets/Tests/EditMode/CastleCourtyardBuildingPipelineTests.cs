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
            string completion = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialPlanCompletion.cs"));
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string courtyard = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleCourtyardRealizer.cs"));

            StringAssert.Contains("CastleSpatialPlanCompletion.CompleteResolved", terrainPlanning);
            StringAssert.Contains("AttachCourtyardBuildings", completion);
            StringAssert.Contains("AttachDungeon", completion);
            StringAssert.Contains("spatialPlan.CourtyardBuildings.Clone()", pipeline);
            StringAssert.Contains("_courtyardBuildings);", pipeline);
            StringAssert.Contains("CastleCourtyardBuildingRealizer.BuildAll", courtyard);

            StringAssert.DoesNotContain("HalfExtents", courtyard);
            StringAssert.DoesNotContain("EntranceDirection", courtyard);
            StringAssert.DoesNotContain("RoofRidgeAlongX", courtyard);
            StringAssert.DoesNotContain("CastleCourtyardBuildingPlanner.Create", pipeline,
                "Runtime must consume completed building specs rather than re-plan courtyard semantics.");
            StringAssert.DoesNotContain("DungeonPlanner.Create", pipeline,
                "Runtime must consume the completed dungeon graph rather than plan it.");
        }

        [Test]
        public void CourtyardBuildingPlannerDependencyIsOneWay()
        {
            string planner = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleCourtyardBuildingPlanner.cs"));
            string compatibilityGeometry = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleCourtyardBuildingPlacementGeometry.cs"));

            StringAssert.Contains("CastleCourtyardBuildingPlanner.Create(", compatibilityGeometry,
                "Resolved-piece compatibility callers should delegate to the public wall-relative planner.");
            StringAssert.DoesNotContain("CastleCourtyardBuildingPlacementGeometry.Plan(", planner,
                "The public planner must never call back through its compatibility adapter; " +
                "that creates an infinite Planner.Create -> PlacementGeometry.Plan recursion.");
            StringAssert.Contains("TryAdd(", planner,
                "The public planner should remain the single owner of courtyard-building placement policy.");
        }
    }
}
