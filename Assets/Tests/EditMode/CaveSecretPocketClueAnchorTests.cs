using System.Linq;
using Game.Composition.CaveWorldBuilder;
using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CaveSecretPocketClueAnchorTests
    {
        [Test]
        public void AuthoredBreakableExposesIndependentPreSolveEvidenceWithoutSceneContracts()
        {
            var campaign = Campaign.Create("cave-secret-clue-anchors");
            SiteRef approach = campaign.World.Region("region").Site("approach", SiteArchetype.Ruin);
            var route = new SecretRouteId("breakable-route");

            SecretClueAnchorSpec[] anchors = CaveSecretPocketClueAnchors.ForAuthoredBreakable(approach, route);

            Assert.Multiple(() =>
            {
                Assert.That(anchors, Has.Length.EqualTo(2));
                Assert.That(anchors.All(x => x.PreSolveObservable), Is.True);
                Assert.That(anchors.All(x => x.Site.Equals(approach)), Is.True);
                Assert.That(anchors.All(x => x.HasExplainedRoute && x.ExplainedRoute.Equals(route)), Is.True);
                Assert.That(anchors.Any(x => x.Role == SecretClueAnchorRole.ApproachEvidence), Is.True);
                Assert.That(anchors.Any(x => x.Role == SecretClueAnchorRole.RouteAdjacentEvidence), Is.True);
                Assert.That(anchors.SelectMany(x => x.Channels).Distinct().Count(), Is.GreaterThanOrEqualTo(2),
                    "A Major generated cave secret must have independent clue channels available before selection.");
                Assert.That(anchors.Any(x => x.HiddenVolumeRelation == SecretHiddenVolumeRelation.Inside), Is.False,
                    "Required cave evidence must never originate from inside the hidden pocket it explains.");
            });
        }

        [Test]
        public void NaturalTraversalExposesNavigationAndSpatialEvidenceWithoutInteractableSemantics()
        {
            var campaign = Campaign.Create("natural-cave-secret-clue-anchors");
            SiteRef approach = campaign.World.Region("region").Site("approach", SiteArchetype.Ruin);
            var route = new SecretRouteId("natural-route");

            SecretClueAnchorSpec[] anchors = CaveSecretPocketClueAnchors.ForNaturalTraversal(approach, route);

            Assert.Multiple(() =>
            {
                Assert.That(anchors, Has.Length.EqualTo(2));
                Assert.That(anchors.All(x => x.PreSolveObservable), Is.True);
                Assert.That(anchors.Any(x => x.Role == SecretClueAnchorRole.TraversalHint), Is.True);
                Assert.That(anchors.SelectMany(x => x.Channels), Does.Contain(SecretClueChannel.Navigation));
                Assert.That(anchors.SelectMany(x => x.Channels), Does.Contain(SecretClueChannel.Spatial));
                Assert.That(anchors.All(x => x.HiddenVolumeRelation == SecretHiddenVolumeRelation.Outside), Is.True);
                Assert.That(anchors.All(x => x.HasExplainedRoute && x.ExplainedRoute.Equals(route)), Is.True);
            });
        }
    }
}
