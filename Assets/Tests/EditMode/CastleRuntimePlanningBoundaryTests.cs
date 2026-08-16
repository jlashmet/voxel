using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleRuntimePlanningBoundaryTests
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
        public void SpatialKeepFurnishingConsumesPlannedRoomSemanticSeed()
        {
            string keep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));

            StringAssert.Contains("roomPlan.SemanticSeed", keep);
            StringAssert.Contains("RoomFurnishingPlanSeed(roomPlan.SemanticSeed, furnishingRecipe)", keep);
            StringAssert.Contains("CastlePlan furnishingPlan = plan", keep);
            StringAssert.Contains("in furnishingPlan", keep,
                "Spatial room realization must pass the semantic-seed-adapted plan to the furnisher.");
            StringAssert.Contains("in plan, min, size, y, f", keep,
                "Compatibility builds must retain the historical per-floor seed flow.");
        }

        [Test]
        public void StructuresRuntimeDoesNotInvokePlanningEntryPoints()
        {
            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string[] forbiddenCalls =
            {
                "CastleLayoutPlanner.Create(",
                "CastleSpatialPlanner.Create(",
                "CastleInnerWardTowerPlanner.Create(",
                "CastleKeepInteriorPlanner.Create(",
                "CastleCourtyardPlanner.Create(",
                "CastleAccessRoutePlanner.Create(",
                "CastleLandscapePlanner.Create(",
                "CastleCavePlanning.Create(",
                "CastleCaveDecorationPlanner.Create(",
                "DungeonPlanner.Create(",
                "CavePlanner.Create(",
                "CastleSpatialPlanCompletion.Complete(",
            };

            foreach (string file in Directory.GetFiles(runtimeDirectory, "*.cs"))
            {
                string source = File.ReadAllText(file);
                for (int i = 0; i < forbiddenCalls.Length; i++)
                {
                    StringAssert.DoesNotContain(
                        forbiddenCalls[i],
                        source,
                        $"{Path.GetFileName(file)} must realize completed plan data, not call {forbiddenCalls[i]}.");
                }
            }
        }
    }
}
