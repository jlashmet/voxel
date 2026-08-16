using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCourtyardBuildingCompletionTests
    {
        [Test]
        public void TerrainCompletionAttachesDeterministicCourtyardBuildings()
        {
            int plansWithBuildings = 0;
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
                CastleSpatialPlan completed = CastleTerrainPlanning.Resolve(
                    in plan, spatial, seed ^ 0x71A5u);

                Assert.IsFalse(completed.KeepRequiresTerrainResolution,
                    $"seed {seed}: terrain completion left the keep unresolved");
                CastleCourtyardBuildingSpec[] expected =
                    CastleCourtyardBuildingPlanner.Create(in plan, completed);
                AssertBuildingsEqual(expected, completed.CourtyardBuildings, seed);
                if (completed.CourtyardBuildings.Length > 0)
                    plansWithBuildings++;
            }

            Assert.Greater(plansWithBuildings, 0,
                "The semantic courtyard planner never found a valid building site.");
        }

        [Test]
        public void CompletionDoesNotMutateInputSpatialPlan()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 47u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(47u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            Assert.AreEqual(0, spatial.CourtyardBuildings.Length);
            CastleSpatialPlan completed = CastleSpatialPlanCompletion.AttachCourtyardBuildings(
                in plan, spatial);

            Assert.AreEqual(0, spatial.CourtyardBuildings.Length,
                "Plan completion must return new immutable planning data rather than mutate input.");
            Assert.AreNotSame(spatial, completed);
            AssertBuildingsEqual(
                CastleCourtyardBuildingPlanner.Create(in plan, completed),
                completed.CourtyardBuildings,
                47u);
        }

        private static void AssertBuildingsEqual(
            CastleCourtyardBuildingSpec[] expected,
            CastleCourtyardBuildingSpec[] actual,
            uint seed)
        {
            Assert.NotNull(actual, $"seed {seed}: null building array");
            Assert.AreEqual(expected.Length, actual.Length, $"seed {seed}: building count");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Id, actual[i].Id, $"seed {seed}: building {i} id");
                Assert.AreEqual(expected[i].Purpose, actual[i].Purpose,
                    $"seed {seed}: building {i} purpose");
                Assert.AreEqual(expected[i].WallEdgeIndex, actual[i].WallEdgeIndex,
                    $"seed {seed}: building {i} edge");
                Assert.AreEqual(expected[i].Centre, actual[i].Centre,
                    $"seed {seed}: building {i} centre");
                Assert.AreEqual(expected[i].Tangent, actual[i].Tangent,
                    $"seed {seed}: building {i} tangent");
                Assert.AreEqual(expected[i].Inward, actual[i].Inward,
                    $"seed {seed}: building {i} inward");
                Assert.AreEqual(expected[i].Width, actual[i].Width,
                    $"seed {seed}: building {i} width");
                Assert.AreEqual(expected[i].Depth, actual[i].Depth,
                    $"seed {seed}: building {i} depth");
                Assert.AreEqual(expected[i].Height, actual[i].Height,
                    $"seed {seed}: building {i} height");
            }
        }
    }
}
