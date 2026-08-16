using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleLayoutPlannerTests
    {
        [Test]
        public void SameSeedProducesSameTopology()
        {
            for (uint seed = 0; seed <= 256; seed++)
            {
                CastleTopologyPlan first = CastleLayoutPlanner.Create(seed);
                CastleTopologyPlan second = CastleLayoutPlanner.Create(seed);

                Assert.AreEqual(first.Perimeter, second.Perimeter, $"seed {seed}: perimeter");
                Assert.AreEqual(first.KeepPlacement, second.KeepPlacement,
                    $"seed {seed}: keep placement");
                Assert.AreEqual(first.Wards, second.Wards, $"seed {seed}: wards");
                Assert.AreEqual(first.DesiredTowerCount, second.DesiredTowerCount,
                    $"seed {seed}: tower count");
                Assert.AreEqual(first.HasPosternGate, second.HasPosternGate,
                    $"seed {seed}: postern gate");
                Assert.AreEqual(first.HasKeepAnnexPlan, second.HasKeepAnnexPlan,
                    $"seed {seed}: annex planning presence");
                Assert.AreEqual(first.KeepAnnexes.HasGreatHallWing,
                                second.KeepAnnexes.HasGreatHallWing,
                    $"seed {seed}: great-hall wing");
                Assert.AreEqual(first.KeepAnnexes.HasChapelWing,
                                second.KeepAnnexes.HasChapelWing,
                    $"seed {seed}: chapel wing");
                Assert.AreEqual(first.KeepAnnexes.HasBellTower,
                                second.KeepAnnexes.HasBellTower,
                    $"seed {seed}: bell tower");
            }
        }

        [Test]
        public void TopologyChoicesRespectGrammarInvariants()
        {
            for (uint seed = 0; seed < 1024; seed++)
            {
                CastleTopologyPlan plan = CastleLayoutPlanner.Create(seed);

                Assert.IsTrue(
                    CastleTopologyPlanValidator.TryValidate(
                        in plan, out CastleTopologyPlanIssue topologyIssue),
                    $"seed {seed}: invalid topology: {topologyIssue}");
                Assert.GreaterOrEqual(plan.DesiredTowerCount, 4, $"seed {seed}: tower minimum");
                Assert.LessOrEqual(plan.DesiredTowerCount, 8, $"seed {seed}: tower maximum");
                Assert.IsTrue(plan.HasKeepAnnexPlan, $"seed {seed}: missing keep-annex plan");
                CastleKeepAnnexPlan annexes = plan.KeepAnnexes;
                Assert.IsTrue(
                    CastleKeepAnnexPlanValidator.TryValidate(
                        in annexes, out CastleKeepAnnexPlanIssue annexIssue),
                    $"seed {seed}: invalid keep-annex plan: {annexIssue}");
                Assert.IsTrue(annexes.HasGreatHallWing,
                    $"seed {seed}: compatibility recipe lost Great Hall wing");
                Assert.IsTrue(annexes.HasChapelWing,
                    $"seed {seed}: compatibility recipe lost chapel wing");
                Assert.IsTrue(annexes.HasBellTower,
                    $"seed {seed}: compatibility recipe lost bell tower");

                if (plan.Perimeter == CastlePerimeterKind.Concentric)
                {
                    Assert.AreEqual(CastleWardPattern.InnerAndOuterWards, plan.Wards,
                        $"seed {seed}: concentric castles require nested wards");
                    Assert.GreaterOrEqual(plan.DesiredTowerCount, 6,
                        $"seed {seed}: concentric tower minimum");
                }

                if (plan.Perimeter == CastlePerimeterKind.IrregularPolygon)
                    Assert.GreaterOrEqual(plan.DesiredTowerCount, 5,
                        $"seed {seed}: polygon tower minimum");
            }
        }

        [Test]
        public void TopologyValidatorRejectsConcentricSingleWard()
        {
            CastleTopologyPlan plan = CastleLayoutPlanner.Create(11u);
            plan.Perimeter = CastlePerimeterKind.Concentric;
            plan.Wards = CastleWardPattern.SingleWard;
            plan.DesiredTowerCount = 6;

            Assert.IsFalse(
                CastleTopologyPlanValidator.TryValidate(
                    in plan, out CastleTopologyPlanIssue issue));
            Assert.AreEqual(CastleTopologyPlanIssue.ConcentricRequiresNestedWards, issue);
        }

        [Test]
        public void TopologyValidatorRejectsPerimeterTowerCountsBelowGrammarMinimum()
        {
            CastleTopologyPlan polygon = CastleLayoutPlanner.Create(13u);
            polygon.Perimeter = CastlePerimeterKind.IrregularPolygon;
            polygon.Wards = CastleWardPattern.SingleWard;
            polygon.DesiredTowerCount = 4;

            Assert.IsFalse(
                CastleTopologyPlanValidator.TryValidate(
                    in polygon, out CastleTopologyPlanIssue polygonIssue));
            Assert.AreEqual(CastleTopologyPlanIssue.InvalidTowerCount, polygonIssue);

            CastleTopologyPlan concentric = CastleLayoutPlanner.Create(17u);
            concentric.Perimeter = CastlePerimeterKind.Concentric;
            concentric.Wards = CastleWardPattern.InnerAndOuterWards;
            concentric.DesiredTowerCount = 5;

            Assert.IsFalse(
                CastleTopologyPlanValidator.TryValidate(
                    in concentric, out CastleTopologyPlanIssue concentricIssue));
            Assert.AreEqual(CastleTopologyPlanIssue.InvalidTowerCount, concentricIssue);
        }

        [Test]
        public void TopologyValidatorRejectsAnnexFlagsWithoutAnnexPlan()
        {
            CastleTopologyPlan plan = CastleLayoutPlanner.Create(19u);
            plan.HasKeepAnnexPlan = false;

            Assert.IsFalse(
                CastleTopologyPlanValidator.TryValidate(
                    in plan, out CastleTopologyPlanIssue issue));
            Assert.AreEqual(CastleTopologyPlanIssue.UnexpectedKeepAnnexPlan, issue);
        }

        [Test]
        public void SeedSpaceProducesEverySupportedTopologicalFamily()
        {
            bool rectangular = false;
            bool quadrilateral = false;
            bool polygon = false;
            bool concentric = false;
            bool centralKeep = false;
            bool rearKeep = false;
            bool highKeep = false;
            bool integratedKeep = false;
            bool singleWard = false;
            bool nestedWards = false;
            bool postern = false;
            bool noPostern = false;

            for (uint seed = 0; seed < 2048; seed++)
            {
                CastleTopologyPlan plan = CastleLayoutPlanner.Create(seed);
                rectangular |= plan.Perimeter == CastlePerimeterKind.Rectangular;
                quadrilateral |= plan.Perimeter == CastlePerimeterKind.IrregularQuadrilateral;
                polygon |= plan.Perimeter == CastlePerimeterKind.IrregularPolygon;
                concentric |= plan.Perimeter == CastlePerimeterKind.Concentric;
                centralKeep |= plan.KeepPlacement == CastleKeepPlacement.Central;
                rearKeep |= plan.KeepPlacement == CastleKeepPlacement.Rear;
                highKeep |= plan.KeepPlacement == CastleKeepPlacement.HighestGround;
                integratedKeep |= plan.KeepPlacement == CastleKeepPlacement.WallIntegrated;
                singleWard |= plan.Wards == CastleWardPattern.SingleWard;
                nestedWards |= plan.Wards == CastleWardPattern.InnerAndOuterWards;
                postern |= plan.HasPosternGate;
                noPostern |= !plan.HasPosternGate;
            }

            Assert.IsTrue(rectangular, "No rectangular castle was reachable.");
            Assert.IsTrue(quadrilateral, "No irregular quadrilateral castle was reachable.");
            Assert.IsTrue(polygon, "No irregular polygon castle was reachable.");
            Assert.IsTrue(concentric, "No concentric castle was reachable.");
            Assert.IsTrue(centralKeep, "No central keep was reachable.");
            Assert.IsTrue(rearKeep, "No rear keep was reachable.");
            Assert.IsTrue(highKeep, "No highest-ground keep was reachable.");
            Assert.IsTrue(integratedKeep, "No wall-integrated keep was reachable.");
            Assert.IsTrue(singleWard, "No single ward was reachable.");
            Assert.IsTrue(nestedWards, "No nested wards were reachable.");
            Assert.IsTrue(postern, "No postern-gate castle was reachable.");
            Assert.IsTrue(noPostern, "Every castle unexpectedly had a postern gate.");
        }

        [Test]
        public void TopologyPlanningDoesNotPerturbLegacyDimensionPlan()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                var before = CastlePlanner.Create(Unity.Mathematics.int3.zero, seed);
                CastleLayoutPlanner.Create(seed);
                var after = CastlePlanner.Create(Unity.Mathematics.int3.zero, seed);

                Assert.AreEqual(before.BaileyHalfX, after.BaileyHalfX, $"seed {seed}: bailey X");
                Assert.AreEqual(before.BaileyHalfZ, after.BaileyHalfZ, $"seed {seed}: bailey Z");
                Assert.AreEqual(before.KeepHalfX, after.KeepHalfX, $"seed {seed}: keep X");
                Assert.AreEqual(before.KeepHalfZ, after.KeepHalfZ, $"seed {seed}: keep Z");
                Assert.AreEqual(before.Floors, after.Floors, $"seed {seed}: floors");
            }
        }
    }
}
