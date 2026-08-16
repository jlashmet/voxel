using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialPreflightTests
    {
        [Test]
        public void SpatialEstimateIsDeterministicAcrossGeneratedPlans()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

                long first = CastleBuildPreflight.EstimateWrites(in plan, spatial);
                long second = CastleBuildPreflight.EstimateWrites(in plan, spatial);

                Assert.AreEqual(first, second, $"seed {seed}: spatial estimate drifted");
                Assert.Greater(first, 0, $"seed {seed}: spatial estimate must be positive");
            }
        }

        [Test]
        public void InnerWardCostsMoreThanOtherwiseIdenticalSingleWard()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 101u);
            var singleTopology = new CastleTopologyPlan
            {
                Perimeter = CastlePerimeterKind.Rectangular,
                KeepPlacement = CastleKeepPlacement.Central,
                Wards = CastleWardPattern.SingleWard,
                DesiredTowerCount = 4,
                HasPosternGate = false,
            };
            CastleTopologyPlan nestedTopology = singleTopology;
            nestedTopology.Wards = CastleWardPattern.InnerAndOuterWards;

            CastleSpatialPlan single = CastleSpatialPlanner.Create(in plan, in singleTopology);
            CastleSpatialPlan nested = CastleSpatialPlanner.Create(in plan, in nestedTopology);

            Assert.Greater(
                CastleBuildPreflight.EstimateWrites(in plan, nested),
                CastleBuildPreflight.EstimateWrites(in plan, single));
        }

        [Test]
        public void MorePlannedTowersIncreaseSpatialEstimate()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 103u);
            var fourTopology = new CastleTopologyPlan
            {
                Perimeter = CastlePerimeterKind.Rectangular,
                KeepPlacement = CastleKeepPlacement.Central,
                Wards = CastleWardPattern.SingleWard,
                DesiredTowerCount = 4,
                HasPosternGate = false,
            };
            CastleTopologyPlan sixTopology = fourTopology;
            sixTopology.DesiredTowerCount = 6;

            CastleSpatialPlan four = CastleSpatialPlanner.Create(in plan, in fourTopology);
            CastleSpatialPlan six = CastleSpatialPlanner.Create(in plan, in sixTopology);

            Assert.Greater(
                CastleBuildPreflight.EstimateWrites(in plan, six),
                CastleBuildPreflight.EstimateWrites(in plan, four));
        }

        [Test]
        public void PosternAddsBudgetCostWithoutChangingPrimaryGateSemantics()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 107u);
            var closedTopology = new CastleTopologyPlan
            {
                Perimeter = CastlePerimeterKind.Rectangular,
                KeepPlacement = CastleKeepPlacement.Central,
                Wards = CastleWardPattern.SingleWard,
                DesiredTowerCount = 4,
                HasPosternGate = false,
            };
            CastleTopologyPlan posternTopology = closedTopology;
            posternTopology.HasPosternGate = true;

            CastleSpatialPlan closed = CastleSpatialPlanner.Create(in plan, in closedTopology);
            CastleSpatialPlan postern = CastleSpatialPlanner.Create(in plan, in posternTopology);

            Assert.Greater(
                CastleBuildPreflight.EstimateWrites(in plan, postern),
                CastleBuildPreflight.EstimateWrites(in plan, closed));
        }

        [Test]
        public void LargerAttachedCaveIncreasesSpatialEstimate()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 108u);
            var topology = new CastleTopologyPlan
            {
                Perimeter = CastlePerimeterKind.Rectangular,
                KeepPlacement = CastleKeepPlacement.Central,
                Wards = CastleWardPattern.SingleWard,
                DesiredTowerCount = 4,
                HasPosternGate = false,
            };
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(in plan, spatial);
            Assert.NotNull(completed.Cave);

            long before = CastleBuildPreflight.EstimateWrites(in plan, completed);
            CaveChamberPlan chamber = completed.Cave.Chambers[0];
            chamber.Radii += new int3(12, 6, 12);
            completed.Cave.Chambers[0] = chamber;
            Assert.IsTrue(CavePlanValidator.TryValidate(completed.Cave, out CavePlanIssue issue),
                issue.ToString());

            long after = CastleBuildPreflight.EstimateWrites(in plan, completed);
            Assert.Greater(after, before,
                "Spatial admission cost should follow the attached cave geometry, not a fixed allowance.");
        }

        [Test]
        public void SpatialPreflightRejectsComplexPlanAtLegacyOnlyBudget()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 109u);
            var topology = new CastleTopologyPlan
            {
                Perimeter = CastlePerimeterKind.Rectangular,
                KeepPlacement = CastleKeepPlacement.Central,
                Wards = CastleWardPattern.InnerAndOuterWards,
                DesiredTowerCount = 6,
                HasPosternGate = true,
            };
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            long legacyEstimate = CastleBuildPreflight.EstimateWrites(in plan);
            long spatialEstimate = CastleBuildPreflight.EstimateWrites(in plan, spatial);

            Assert.Greater(spatialEstimate, legacyEstimate,
                "The nested spatial plan should expose work hidden by the rectangular legacy estimate.");
            Assert.IsTrue(CastleBuildPreflight.Evaluate(in plan, legacyEstimate).IsValid);

            CastleBuildPreflightResult result = CastleBuildPreflight.Evaluate(
                in plan, spatial, legacyEstimate);

            Assert.AreEqual(CastleBuildPreflightIssue.WriteBudgetExceeded, result.Issue);
            Assert.AreEqual(spatialEstimate, result.EstimatedWrites);
            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void SpatialPreflightRejectsInvalidSpatialGeometryBeforeBudgeting()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 113u);
            var topology = new CastleTopologyPlan
            {
                Perimeter = CastlePerimeterKind.Rectangular,
                KeepPlacement = CastleKeepPlacement.Central,
                Wards = CastleWardPattern.SingleWard,
                DesiredTowerCount = 4,
                HasPosternGate = false,
            };
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            spatial.OuterWardVertices[spatial.PrimaryGate.EdgeIndex] += new int2(7, 0);

            CastleBuildPreflightResult result = CastleBuildPreflight.Evaluate(
                in plan, spatial, long.MaxValue);

            Assert.AreEqual(CastleBuildPreflightIssue.InvalidSpatialPlan, result.Issue);
            Assert.AreNotEqual(CastleSpatialPlanIssue.None, result.SpatialPlanIssue);
            Assert.AreEqual(0L, result.EstimatedWrites);
            Assert.IsFalse(result.IsValid);
        }
    }
}
