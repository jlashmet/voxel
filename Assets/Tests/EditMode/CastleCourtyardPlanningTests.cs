using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCourtyardPlanningTests
    {
        [Test]
        public void ResolvedSpatialPlansCarryDeterministicValidatedWell()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.KeepPlacement = CastleKeepPlacement.Central;

                CastleSpatialPlan first = CastleSpatialPlanner.Create(in dimensions, in topology);
                CastleSpatialPlan second = CastleSpatialPlanner.Create(in dimensions, in topology);

                Assert.IsTrue(first.HasWell, $"seed {seed}: resolved castle had no planned well");
                Assert.AreEqual(first.WellCentre, second.WellCentre,
                    $"seed {seed}: well placement was not deterministic");
                Assert.IsTrue(
                    CastleSpatialPlanValidator.TryValidate(
                        in dimensions, first, out CastleSpatialPlanIssue issue),
                    $"seed {seed}: planned courtyard invalid: {issue}");
            }
        }

        [Test]
        public void NestedWardWellStaysInsideTheKeepsAssignedWard()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.Wards = CastleWardPattern.InnerAndOuterWards;
                topology.KeepPlacement = CastleKeepPlacement.Central;

                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

                Assert.IsTrue(spatial.HasWell,
                    $"seed {seed}: nested ward had no planned well");
                Assert.IsTrue(
                    CastlePolygonGeometry.ContainsPoint(
                        spatial.WellCentre, spatial.InnerWardVertices),
                    $"seed {seed}: courtyard well escaped the inner ward");
                Assert.IsTrue(
                    CastleSpatialPlanValidator.TryValidate(
                        in dimensions, spatial, out CastleSpatialPlanIssue issue),
                    $"seed {seed}: nested courtyard invalid: {issue}");
            }
        }

        [Test]
        public void HighestGroundDefersWellUntilKeepIsResolved()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 713u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(713u);
            topology.KeepPlacement = CastleKeepPlacement.HighestGround;
            CastleSpatialPlan unresolved = CastleSpatialPlanner.Create(in dimensions, in topology);

            Assert.IsTrue(unresolved.KeepRequiresTerrainResolution);
            Assert.IsFalse(unresolved.HasWell);
            Assert.AreEqual(int2.zero, unresolved.WellCentre);
            Assert.IsTrue(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, unresolved, out CastleSpatialPlanIssue unresolvedIssue),
                unresolvedIssue.ToString());

            CastleSpatialPlan resolved = CastleSpatialPlanner.ResolveHighestGroundKeep(
                in dimensions, unresolved, int2.zero);

            Assert.IsFalse(resolved.KeepRequiresTerrainResolution);
            Assert.IsTrue(resolved.HasWell);
            Assert.IsTrue(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, resolved, out CastleSpatialPlanIssue resolvedIssue),
                resolvedIssue.ToString());
        }

        [Test]
        public void ValidatorRejectsStaleWellWhenKeepFootprintChanges()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 977u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(977u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            Assert.IsTrue(spatial.HasWell);
            Assert.IsTrue(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, spatial, out CastleSpatialPlanIssue validIssue),
                validIssue.ToString());

            CastlePlan smallerKeep = dimensions;
            smallerKeep.KeepHalfX = math.max(1, dimensions.KeepHalfX - 1);

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(
                    in smallerKeep, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.InvalidWellPlacement, issue);
        }
    }
}
