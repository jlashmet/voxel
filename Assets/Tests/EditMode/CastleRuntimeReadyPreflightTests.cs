using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleRuntimeReadyPreflightTests
    {
        [Test]
        public void IntermediateSpatialPlanCanBeStructurallyValidButNotRuntimeReady()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 211u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(211u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            Assert.IsEmpty(spatial.KeepFloors);
            Assert.IsNull(spatial.Dungeon);
            Assert.IsTrue(
                CastleBuildPreflight.Evaluate(in plan, spatial, long.MaxValue).IsValid,
                "Planning-time validation should continue to accept the intermediate spatial plan.");

            CastleBuildPreflightResult runtime = CastleBuildPreflight.EvaluateRuntimeReady(
                in plan, spatial, long.MaxValue);

            Assert.AreEqual(CastleBuildPreflightIssue.IncompleteSpatialPlan, runtime.Issue);
            Assert.AreEqual(
                CastleSpatialBuildReadinessIssue.MissingKeepFloorPlan,
                runtime.ReadinessIssue);
            Assert.IsFalse(runtime.IsValid);
        }

        [Test]
        public void CompletedSpatialPlanPassesRuntimeReadyPreflight()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 223u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(223u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan raw = CastleSpatialPlanner.Create(in plan, in topology);
            CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                in plan, raw);

            Assert.NotNull(completed.Dungeon);
            CastleBuildPreflightResult runtime = CastleBuildPreflight.EvaluateRuntimeReady(
                in plan, completed, long.MaxValue);

            Assert.AreEqual(CastleBuildPreflightIssue.None, runtime.Issue);
            Assert.AreEqual(CastleSpatialBuildReadinessIssue.None, runtime.ReadinessIssue);
            Assert.IsTrue(runtime.IsValid);
        }
    }
}
