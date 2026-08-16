using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleTopologyPlanValidatorTests
    {
        [Test]
        public void GeneratedTopologyAlwaysSatisfiesSemanticGrammar()
        {
            for (uint seed = 0; seed < 4096; seed++)
            {
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                Assert.IsTrue(
                    CastleTopologyPlanValidator.TryValidate(
                        in topology, out CastleTopologyPlanIssue issue),
                    $"seed {seed}: generated invalid topology: {issue}");
            }
        }

        [Test]
        public void RejectsConcentricCastleWithoutNestedWard()
        {
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(17u);
            topology.Perimeter = CastlePerimeterKind.Concentric;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.DesiredTowerCount = 6;

            Assert.IsFalse(
                CastleTopologyPlanValidator.TryValidate(
                    in topology, out CastleTopologyPlanIssue issue));
            Assert.AreEqual(CastleTopologyPlanIssue.ConcentricRequiresNestedWards, issue);
        }

        [TestCase(CastlePerimeterKind.Rectangular, 3)]
        [TestCase(CastlePerimeterKind.IrregularQuadrilateral, 3)]
        [TestCase(CastlePerimeterKind.IrregularPolygon, 4)]
        [TestCase(CastlePerimeterKind.Concentric, 5)]
        [TestCase(CastlePerimeterKind.Rectangular, 9)]
        public void RejectsTowerCountsOutsidePerimeterGrammar(
            CastlePerimeterKind perimeter,
            int towerCount)
        {
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(29u);
            topology.Perimeter = perimeter;
            topology.Wards = perimeter == CastlePerimeterKind.Concentric
                ? CastleWardPattern.InnerAndOuterWards
                : CastleWardPattern.SingleWard;
            topology.DesiredTowerCount = towerCount;

            Assert.IsFalse(
                CastleTopologyPlanValidator.TryValidate(
                    in topology, out CastleTopologyPlanIssue issue));
            Assert.AreEqual(CastleTopologyPlanIssue.InvalidTowerCount, issue);
        }

        [Test]
        public void RejectsMissingSiteRecipe()
        {
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(31u);
            topology.Site = default;

            Assert.IsFalse(
                CastleTopologyPlanValidator.TryValidate(
                    in topology, out CastleTopologyPlanIssue issue));
            Assert.AreEqual(CastleTopologyPlanIssue.InvalidSitePlan, issue);
        }

        [Test]
        public void RejectsAnnexPayloadWhenPresenceFlagIsFalse()
        {
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(37u);
            topology.HasKeepAnnexPlan = false;
            topology.KeepAnnexes = new CastleKeepAnnexPlan(
                hasGreatHallWing: true,
                hasChapelWing: false,
                hasBellTower: false);

            Assert.IsFalse(
                CastleTopologyPlanValidator.TryValidate(
                    in topology, out CastleTopologyPlanIssue issue));
            Assert.AreEqual(CastleTopologyPlanIssue.UnexpectedKeepAnnexPlan, issue);
        }

        [Test]
        public void RejectsInvalidAnnexRelationships()
        {
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(41u);
            topology.HasKeepAnnexPlan = true;
            topology.KeepAnnexes = new CastleKeepAnnexPlan(
                hasGreatHallWing: true,
                hasChapelWing: false,
                hasBellTower: true);

            Assert.IsFalse(
                CastleTopologyPlanValidator.TryValidate(
                    in topology, out CastleTopologyPlanIssue issue));
            Assert.AreEqual(CastleTopologyPlanIssue.InvalidKeepAnnexPlan, issue);
        }

        [Test]
        public void RejectsUnknownSemanticEnumValuesBeforeSpatialPlanning()
        {
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(53u);

            topology.Perimeter = (CastlePerimeterKind)255;
            Assert.IsFalse(CastleTopologyPlanValidator.TryValidate(
                in topology, out CastleTopologyPlanIssue perimeterIssue));
            Assert.AreEqual(CastleTopologyPlanIssue.InvalidPerimeter, perimeterIssue);

            topology = CastleLayoutPlanner.Create(53u);
            topology.KeepPlacement = (CastleKeepPlacement)255;
            Assert.IsFalse(CastleTopologyPlanValidator.TryValidate(
                in topology, out CastleTopologyPlanIssue keepIssue));
            Assert.AreEqual(CastleTopologyPlanIssue.InvalidKeepPlacement, keepIssue);

            topology = CastleLayoutPlanner.Create(53u);
            topology.Wards = (CastleWardPattern)255;
            Assert.IsFalse(CastleTopologyPlanValidator.TryValidate(
                in topology, out CastleTopologyPlanIssue wardIssue));
            Assert.AreEqual(CastleTopologyPlanIssue.InvalidWardPattern, wardIssue);
        }
    }
}
