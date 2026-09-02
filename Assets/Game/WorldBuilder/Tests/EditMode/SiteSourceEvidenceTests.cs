using System.Linq;
using Game.Composition.Campaign.Content;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SiteSourceEvidenceTests
    {
        [Test]
        public void OpeningPubRetainsRecoveredKentridgePubMapEvidence()
        {
            KnownOpeningCampaignContent content = KnownOpeningCampaignContent.Build(
                DialogueOnly("destination-conversation"));

            SiteSourceEvidenceSpec source = content.Blueprint.SiteSourceEvidence
                .Single(value => value.Site.Equals(content.StartingPub));

            Assert.That(source.Kind, Is.EqualTo(SiteSourceEvidenceKind.LegacyMap));
            Assert.That(source.SourceSystem, Is.EqualTo("mounting-force"));
            Assert.That(source.SourceId, Is.EqualTo("kentridge-pub"));
        }

        [Test]
        public void SemanticSiteCanRetainMultipleLegacyMapSourcesWithoutConstrainingItsIdentity()
        {
            var game = Campaign.Create("site-source-evidence-test");
            RegionHandle region = game.World.Region("test-region");
            SettlementHandle settlement = region.Town("test-town");
            SiteHandle site = settlement.Site("test-site")
                .LegacyMap("legacy-game", "map-upper")
                .LegacyMap("legacy-game", "map-lower");

            CampaignBlueprint blueprint = game.Build();
            SiteSourceEvidenceSpec[] sources = blueprint.SiteSourceEvidence
                .Where(value => value.Site.Equals(site.Ref))
                .ToArray();

            Assert.That(sources.Length, Is.EqualTo(2));
            CollectionAssert.AreEquivalent(
                new[] { "map-upper", "map-lower" },
                sources.Select(value => value.SourceId).ToArray());

            SiteSpec siteSpec = blueprint.Sites.Single(value => value.Ref.Equals(site.Ref));
            Assert.That(siteSpec.ResolutionMode, Is.EqualTo(SiteResolutionMode.ConstraintMatch),
                "Source evidence must remain provenance only and must not turn a semantic role into a hard generation constraint.");
        }

        private static CutsceneDefinition DialogueOnly(string id) =>
            new CutsceneDefinition(
                id,
                CutsceneStageSetupDefinition.Empty,
                new[] { CutsceneStep.Dialogue(new CutsceneCueId(id + ".dialogue")) });
    }
}
