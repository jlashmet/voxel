using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleAccessRouteReadinessTests
    {
        [Test]
        public void CompletedSpatialPlansAdmitOnlyWithValidGateToKeepRoutes()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                if (topology.KeepPlacement == CastleKeepPlacement.HighestGround)
                    topology.KeepPlacement = CastleKeepPlacement.Central;

                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
                CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                    in plan, spatial);
                CastleAccessRoute route = CastleAccessRoute.Create(in plan, completed);

                Assert.IsTrue(
                    CastleAccessRouteValidator.TryValidate(
                        in route,
                        completed.OuterWardVertices,
                        completed.InnerWardVertices,
                        out CastleAccessRouteIssue routeIssue),
                    $"seed {seed}: route invalid before readiness: {routeIssue}");
                Assert.IsTrue(
                    CastleSpatialBuildReadiness.TryValidate(
                        in plan,
                        completed,
                        out CastleSpatialBuildReadinessIssue readinessIssue),
                    $"seed {seed}: runtime readiness rejected completed plan: {readinessIssue}");
            }
        }

        [Test]
        public void RuntimeReadinessRejectsAccessRouteThatLeavesItsWard()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 331u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(331u);
            topology.Perimeter = CastlePerimeterKind.IrregularPolygon;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 8;
            topology.HasPosternGate = false;

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                in plan, spatial);
            CastleAccessRoute route = CastleAccessRoute.Create(in plan, completed);

            // Corrupt only the ward consumed by the derived route check. Readiness intentionally
            // runs this admission invariant before the individual realization sub-plan checks.
            for (int i = 0; i < completed.OuterWardVertices.Length; i++)
                completed.OuterWardVertices[i] = new int2(10_000 + i * 7, 10_000 + i * 11);

            Assert.IsFalse(
                CastleAccessRouteValidator.TryValidate(
                    in route,
                    completed.OuterWardVertices,
                    completed.InnerWardVertices,
                    out _),
                "Test corruption failed to invalidate the derived access route.");

            Assert.IsFalse(
                CastleSpatialBuildReadiness.TryValidate(
                    in plan,
                    completed,
                    out CastleSpatialBuildReadinessIssue issue));
            Assert.AreEqual(CastleSpatialBuildReadinessIssue.InvalidAccessRoute, issue);
        }
    }
}
