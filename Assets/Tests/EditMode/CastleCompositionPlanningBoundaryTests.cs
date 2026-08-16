using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCompositionPlanningBoundaryTests
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
        public void CompositionOwnsTerrainResolutionBeforeSpatialRuntimeBuild()
        {
            string composition = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "StructuresComposition.cs"));
            string runtime = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));

            StringAssert.Contains(
                "PlanCastleSpatial(in CastlePlan plan, uint terrainSeed)", composition);
            StringAssert.Contains("CastleTerrainPlanning.Resolve(", composition);
            StringAssert.Contains("CastleSpatialPlan resolvedSpatialPlan", composition);
            StringAssert.Contains("KeepRequiresTerrainResolution", runtime);
            StringAssert.Contains("Resolve HighestGround placement before starting runtime realization", runtime);
        }
    }
}
