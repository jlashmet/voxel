using System;
using System.Linq;
using Game.Composition.Campaign.Content;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KnownOpeningCampaignContentTests
    {
        [Test]
        public void ActorfulDestinationCutsceneRequiresAnExplicitCampaignBinding()
        {
            CutsceneActorId speaker = new CutsceneActorId("destination-speaker");
            CutsceneDefinition definition = ActorfulDestination(speaker);

            Assert.Throws<ArgumentException>(() =>
                KnownOpeningCampaignContent.Build(definition));
        }

        [Test]
        public void DestinationCutsceneCanBindItsActorIdToTheKnownDestinationNpcRole()
        {
            CutsceneActorId speaker = new CutsceneActorId("destination-speaker");
            CutsceneDefinition definition = ActorfulDestination(speaker);

            KnownOpeningCampaignContent content = KnownOpeningCampaignContent.Build(
                definition,
                (scene, roles) => scene.Bind(speaker, roles.DestinationNpc));

            CampaignBlueprint blueprint = content.Blueprint;
            CutsceneSpec destination = blueprint.Cutscenes
                .Single(value => value.Ref.Equals(content.DestinationCutscene));
            Assert.That(destination.Site, Is.EqualTo(content.FirstDestination));
            Assert.That(destination.ActorBindings.Count, Is.EqualTo(1));
            Assert.That(destination.ActorBindings[0].Actor, Is.EqualTo(speaker));
            Assert.That(destination.ActorBindings[0].Target.Kind, Is.EqualTo(CutsceneActorTargetKind.Npc));
            Assert.That(destination.ActorBindings[0].Target.Npc, Is.EqualTo(content.DestinationNpc));

            RegionSpec region = blueprint.Hierarchy.Regions.Single();
            Assert.That(region.Ref.Id, Is.EqualTo("kentridge-overworld"));

            SettlementSpec settlement = blueprint.Hierarchy.Settlements.Single();
            Assert.That(settlement.Ref.Id, Is.EqualTo("kentridge"));
            Assert.That(settlement.Region, Is.EqualTo(region.Ref));
            Assert.That(settlement.Archetype, Is.EqualTo(SettlementArchetype.Town));

            SitePlacementSpec pubPlacement = blueprint.Hierarchy.SitePlacements
                .Single(value => value.Site.Equals(content.StartingPub));
            Assert.That(pubPlacement.Kind, Is.EqualTo(SitePlacementKind.Settlement));
            Assert.That(pubPlacement.Settlement, Is.EqualTo(settlement.Ref));

            SitePlacementSpec destinationPlacement = blueprint.Hierarchy.SitePlacements
                .Single(value => value.Site.Equals(content.FirstDestination));
            Assert.That(destinationPlacement.Kind, Is.EqualTo(SitePlacementKind.Region));
            Assert.That(destinationPlacement.Region, Is.EqualTo(region.Ref));

            SiteSpec pubSite = blueprint.Sites
                .Single(value => value.Ref.Equals(content.StartingPub));
            SiteCapabilityRequirement spawn = pubSite.Capabilities
                .Single(value => value.Kind == SiteCapabilityKind.PlayerSpawn);
            Assert.That(spawn.Source, Is.EqualTo(SiteCapabilitySource.Authored));
            Assert.That(spawn.MinimumCapacity, Is.EqualTo(4),
                "Four-player campaign capacity remains explicit even though the opening cutscene only binds player slot 0.");
            Assert.That(pubSite.Capabilities.Single(value => value.Kind == SiteCapabilityKind.Interior).Source,
                Is.EqualTo(SiteCapabilitySource.Derived));
            Assert.That(pubSite.Capabilities.Single(value => value.Kind == SiteCapabilityKind.PublicExit).Source,
                Is.EqualTo(SiteCapabilitySource.Derived));
            Assert.That(pubSite.Capabilities.Single(value => value.Kind == SiteCapabilityKind.CutsceneStage).Source,
                Is.EqualTo(SiteCapabilitySource.Derived));

            SiteSpec destinationSite = blueprint.Sites
                .Single(value => value.Ref.Equals(content.FirstDestination));
            Assert.That(destinationSite.ResolutionMode, Is.EqualTo(SiteResolutionMode.ConstraintMatch));
            Assert.That(destinationSite.Archetype, Is.EqualTo(SiteArchetype.Unspecified),
                "The first destination remains generator-selected; hierarchy ownership must not invent its archetype.");
            Assert.That(destinationSite.Capabilities
                    .Single(value => value.Kind == SiteCapabilityKind.ConversationSpace).Source,
                Is.EqualTo(SiteCapabilitySource.Derived));
        }

        [Test]
        public void RecoveredCatalogRegistersAllSixNormalizedRegionSettlementPairs()
        {
            var game = Campaign.Create("recovered-world-catalog-test");
            RecoveredMountingForceWorldCatalog.RegisterHierarchy(game.World);
            CampaignBlueprint blueprint = game.Build();

            Assert.That(blueprint.Hierarchy.Regions.Count, Is.EqualTo(6));
            Assert.That(blueprint.Hierarchy.Settlements.Count, Is.EqualTo(6));
            CollectionAssert.AreEquivalent(
                RecoveredMountingForceWorldCatalog.All.Select(value => value.RegionId).ToArray(),
                blueprint.Hierarchy.Regions.Select(value => value.Ref.Id).ToArray());
            CollectionAssert.AreEquivalent(
                RecoveredMountingForceWorldCatalog.All.Select(value => value.SettlementId).ToArray(),
                blueprint.Hierarchy.Settlements.Select(value => value.Ref.Id).ToArray());
        }

        private static CutsceneDefinition ActorfulDestination(CutsceneActorId speaker) =>
            new CutsceneDefinition(
                "destination-conversation",
                CutsceneStageSetupDefinition.Empty,
                new[]
                {
                    CutsceneStep.Dialogue(
                        speaker,
                        new CutsceneCueId("destination-conversation.dialogue"))
                });
    }
}
