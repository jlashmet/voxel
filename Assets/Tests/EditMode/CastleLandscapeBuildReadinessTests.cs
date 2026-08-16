using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleLandscapeBuildReadinessTests
    {
        [Test]
        public void IntermediateSpatialPlanIsNotLandscapeReady()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 31u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(31u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            Assert.IsFalse(CastleLandscapeBuildReadiness.TryValidate(spatial, out var issue));
            Assert.AreEqual(CastleLandscapeBuildReadinessIssue.MissingLandscapePlan, issue);
        }

        [Test]
        public void CompletedSpatialPlanIsLandscapeReady()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 37u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(37u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(in plan, spatial);

            Assert.IsTrue(CastleLandscapeBuildReadiness.TryValidate(completed, out var issue),
                issue.ToString());
            Assert.AreEqual(CastleLandscapeBuildReadinessIssue.None, issue);
        }

        [Test]
        public void CorruptedLandscapeIsRejectedAtAdmissionBoundary()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 41u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(41u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(in plan, spatial);

            CastleLandscapeDecorationSpec[] decorations = completed.Landscape.Decorations;
            Assert.Greater(decorations.Length, 0);
            decorations[0].Radius = 0;

            Assert.IsFalse(CastleLandscapeBuildReadiness.TryValidate(completed, out var issue));
            Assert.AreEqual(CastleLandscapeBuildReadinessIssue.InvalidLandscapePlan, issue);
        }
    }
}
