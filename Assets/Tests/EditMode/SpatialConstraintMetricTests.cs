using System.Linq;
using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SpatialConstraintMetricTests
    {
        [Test]
        public void SiteDistanceDslEmitsThreeDistinctMetrics()
        {
            var game = Campaign.Create("distance-metrics");
            SiteRef origin = game.World.RequireSite("origin", site => site
                .Archetype(SiteArchetype.Pub)
                .RequireCapability(SiteCapability.PublicExit));

            game.World.RequireSite("target", site => site
                .Archetype(SiteArchetype.Ruin)
                .RequireCapability(SiteCapability.PublicExit)
                .BoundaryDistanceFrom(origin, new DistanceRangeMetres(100, 200))
                .EntranceDistanceFrom(origin, new DistanceRangeMetres(120, 240))
                .TravelDistanceFrom(origin, TraversalProfile.NormalParty, new DistanceRangeMetres(180, 400)));

            var constraints = game.Build().SpatialConstraints;
            Assert.That(constraints.Count, Is.EqualTo(3));

            SpatialConstraintSpec boundary = constraints.Single(c =>
                c.DistanceMetric == SiteDistanceMetric.BoundaryToBoundaryEuclidean);
            Assert.That(boundary.Distance.Minimum, Is.EqualTo(100));
            Assert.That(boundary.Distance.Maximum, Is.EqualTo(200));

            SpatialConstraintSpec entrance = constraints.Single(c =>
                c.DistanceMetric == SiteDistanceMetric.PublicEntranceToPublicEntranceEuclidean);
            Assert.That(entrance.Distance.Minimum, Is.EqualTo(120));
            Assert.That(entrance.Distance.Maximum, Is.EqualTo(240));

            SpatialConstraintSpec travel = constraints.Single(c =>
                c.DistanceMetric == SiteDistanceMetric.TraversalPathLength);
            Assert.That(travel.Traversal, Is.EqualTo(TraversalProfile.NormalParty));
            Assert.That(travel.Distance.Minimum, Is.EqualTo(180));
            Assert.That(travel.Distance.Maximum, Is.EqualTo(400));
        }

        [Test]
        public void ReachabilityIsNotAHiddenDistanceConstraint()
        {
            var game = Campaign.Create("reachability-only");
            SiteRef pub = game.World.RequireSite("pub", site => site.Archetype(SiteArchetype.Pub));
            game.World.RequireSite("cave", site => site
                .Archetype(SiteArchetype.Cave)
                .ReachableFrom(pub, TraversalProfile.NormalParty));

            SpatialConstraintSpec constraint = game.Build().SpatialConstraints.Single();
            Assert.That(constraint.Kind, Is.EqualTo(SpatialConstraintKind.ReachableFrom));
            Assert.That(constraint.Traversal, Is.EqualTo(TraversalProfile.NormalParty));
        }
    }
}
