using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSitePlanValidationTests
    {
        [Test]
        public void GeneratedSitePlansAreStructurallyValid()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                CastleSitePlan site = CastleSitePlanner.Create(seed);
                Assert.IsTrue(
                    CastleSitePlanValidator.TryValidate(in site, out CastleSitePlanIssue issue),
                    $"seed {seed}: {issue}");
            }
        }

        [Test]
        public void TopologyRejectsMissingDefaultSitePlan()
        {
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(97u);
            topology.Site = default;

            Assert.IsFalse(
                CastleTopologyPlanValidator.TryValidate(
                    in topology, out CastleTopologyPlanIssue issue));
            Assert.AreEqual(CastleTopologyPlanIssue.InvalidSitePlan, issue);
        }

        [Test]
        public void SitePlanRejectsDefaultGeometryEvenWithValidSeeds()
        {
            CastleSitePlan generated = CastleSitePlanner.Create(103u);
            CastleSiteGeometryPlan geometry = default;
            var invalid = new CastleSitePlan(
                generated.GrassPatternSeed,
                generated.GrassCoveragePercent,
                generated.CourtyardPatternSeed,
                generated.CourtyardStonePercent,
                in geometry);

            Assert.IsFalse(
                CastleSitePlanValidator.TryValidate(
                    in invalid, out CastleSitePlanIssue issue));
            Assert.AreEqual(CastleSitePlanIssue.InvalidEdgeRecipe, issue);
        }

        [Test]
        public void DisabledCourtyardPatternDoesNotRequireSeed()
        {
            CastleSitePlan generated = CastleSitePlanner.Create(107u);
            var site = new CastleSitePlan(
                generated.GrassPatternSeed,
                generated.GrassCoveragePercent);

            Assert.IsTrue(
                CastleSitePlanValidator.TryValidate(
                    in site, out CastleSitePlanIssue issue),
                issue.ToString());
        }
    }
}
