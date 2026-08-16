using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleRuntimeReadinessTests
    {
        [Test]
        public void RuntimeReadinessRejectsMissingKeepCirculationBeforeVoxelMutation()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 301u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(plan.Seed);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            spatial = CastleSpatialPlanCompletion.AttachTowerVariation(in plan, spatial);
            spatial = CastleSpatialPlanCompletion.AttachKeepFloors(in plan, spatial);
            // Deliberately skip AttachKeepCirculation.
            spatial = CastleSpatialPlanCompletion.AttachCourtyardBuildings(in plan, spatial);
            spatial = CastleSpatialPlanCompletion.AttachDungeon(in plan, spatial);
            spatial = CastleSpatialPlanCompletion.AttachCave(in plan, spatial);
            spatial = CastleSpatialPlanCompletion.AttachCaveDecoration(in plan, spatial);
            spatial = CastleSpatialPlanCompletion.AttachLandscape(in plan, spatial);

            CastleBuildPreflightResult readiness = CastleBuildPreflight.EvaluateRuntimeReady(
                in plan, spatial, long.MaxValue);

            Assert.AreEqual(CastleBuildPreflightIssue.IncompleteSpatialPlan, readiness.Issue);
            Assert.AreEqual(
                CastleSpatialBuildReadinessIssue.InvalidKeepCirculationPlan,
                readiness.ReadinessIssue);
        }

        [Test]
        public void RuntimeReadinessRejectsMissingKeepAnnexPlanBeforeVoxelMutation()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 307u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(plan.Seed);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.HasKeepAnnexPlan = false;
            topology.KeepAnnexes = default;

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            spatial = CastleSpatialPlanCompletion.CompleteResolved(in plan, spatial);

            CastleBuildPreflightResult readiness = CastleBuildPreflight.EvaluateRuntimeReady(
                in plan, spatial, long.MaxValue);

            Assert.AreEqual(CastleBuildPreflightIssue.IncompleteSpatialPlan, readiness.Issue);
            Assert.AreEqual(
                CastleSpatialBuildReadinessIssue.MissingKeepAnnexPlan,
                readiness.ReadinessIssue);
        }

        [Test]
        public void RuntimeReadinessRejectsMissingLandscapeBeforeVoxelMutation()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 311u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(plan.Seed);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            spatial = CastleSpatialPlanCompletion.AttachTowerVariation(in plan, spatial);
            spatial = CastleSpatialPlanCompletion.AttachKeepFloors(in plan, spatial);
            spatial = CastleSpatialPlanCompletion.AttachKeepCirculation(in plan, spatial);
            spatial = CastleSpatialPlanCompletion.AttachCourtyardBuildings(in plan, spatial);
            spatial = CastleSpatialPlanCompletion.AttachDungeon(in plan, spatial);
            spatial = CastleSpatialPlanCompletion.AttachCave(in plan, spatial);
            spatial = CastleSpatialPlanCompletion.AttachCaveDecoration(in plan, spatial);
            // Deliberately skip AttachLandscape.

            CastleBuildPreflightResult readiness = CastleBuildPreflight.EvaluateRuntimeReady(
                in plan, spatial, long.MaxValue);

            Assert.AreEqual(CastleBuildPreflightIssue.IncompleteSpatialPlan, readiness.Issue);
            Assert.AreEqual(
                CastleSpatialBuildReadinessIssue.MissingLandscapePlan,
                readiness.ReadinessIssue);
        }

        [Test]
        public void RuntimeReadinessAcceptsCompletedKeepMetadata()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 313u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(plan.Seed);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            spatial = CastleSpatialPlanCompletion.CompleteResolved(in plan, spatial);

            CastleBuildPreflightResult readiness = CastleBuildPreflight.EvaluateRuntimeReady(
                in plan, spatial, long.MaxValue);

            Assert.IsTrue(readiness.IsValid, readiness.ReadinessIssue.ToString());
            Assert.AreEqual(CastleSpatialBuildReadinessIssue.None, readiness.ReadinessIssue);
        }
    }
}
