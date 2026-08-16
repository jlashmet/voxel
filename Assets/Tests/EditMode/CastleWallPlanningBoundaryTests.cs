using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleWallPlanningBoundaryTests
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
        public void InvalidWallRecipeFailsSpatialValidationThroughTopology()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 41u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(41u);
            CastleWallPlan walls = topology.Walls;
            walls.ArrowSlitSpacing = 0;
            topology.Walls = walls;

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.InvalidTopology, issue);
        }

        [Test]
        public void SpatialPipelineConsumesFrozenWallRecipeInsteadOfHistoricalLiterals()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string perimeter = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePerimeterRealizer.cs"));

            StringAssert.Contains("_wallPlan = topology.Walls", pipeline);
            StringAssert.Contains("in _wallPlan", pipeline);
            StringAssert.DoesNotContain("CastleLayout.FrontGateWidth + 12", pipeline,
                "The spatial pipeline must not recreate the historical gate-gap recipe.");

            StringAssert.Contains("in CastleWallPlan walls", perimeter);
            StringAssert.DoesNotContain("CastleSeedPartition.Derive(", perimeter,
                "Compatibility tower variation may call the API recipe, but Runtime must not derive authored seeds.");
            StringAssert.DoesNotContain("new Random(", perimeter);
        }
    }
}
