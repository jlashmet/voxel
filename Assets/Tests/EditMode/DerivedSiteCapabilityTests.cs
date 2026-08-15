using System.Linq;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class DerivedSiteCapabilityTests
    {
        [Test]
        public void BindingStagedCutsceneAutomaticallyConstrainsHostSite()
        {
            var game = Campaign.Create("derived-cutscene-stage");
            SiteRef pub = game.World.RequireSite("pub", site => site
                .Archetype(SiteArchetype.Pub));

            var actor = new CutsceneActorId("lead");
            var mark = new CutsceneStagePointId("lead-mark");
            var entrance = new CutsceneStagePointId("entrance");
            var definition = new CutsceneDefinition(
                "opening",
                new CutsceneStageSetupDefinition(new[]
                {
                    new CutsceneActorPlacement(actor, mark)
                }),
                new[]
                {
                    CutsceneStep.FacePoint(actor, entrance),
                    CutsceneStep.Dialogue(actor, new CutsceneCueId("lead.line"))
                },
                new[]
                {
                    new CutsceneStagePointRequirement(
                        mark,
                        CutsceneStageRegion.InteriorGatheringArea,
                        minimumClearanceDecimetres: 10),
                    new CutsceneStagePointRequirement(
                        entrance,
                        CutsceneStageRegion.PublicEntrance,
                        minimumClearanceDecimetres: 4)
                });

            game.Story.Cutscene(definition, cutscene => cutscene
                .At(pub)
                .Bind(actor, CutsceneActorTarget.Player(0)));

            CampaignBlueprint blueprint = game.Build();
            SiteSpec resolvedPub = blueprint.Sites.Single(site => site.Ref.Equals(pub));
            SiteCapabilityRequirement stage = resolvedPub.Capabilities.Single(capability =>
                capability.Kind == SiteCapabilityKind.CutsceneStage);
            SiteCapabilityRequirement interior = resolvedPub.Capabilities.Single(capability =>
                capability.Kind == SiteCapabilityKind.Interior);
            SiteCapabilityRequirement publicExit = resolvedPub.Capabilities.Single(capability =>
                capability.Kind == SiteCapabilityKind.PublicExit);

            Assert.That(stage.Source, Is.EqualTo(SiteCapabilitySource.Derived));
            Assert.That(interior.Source, Is.EqualTo(SiteCapabilitySource.Derived));
            Assert.That(publicExit.Source, Is.EqualTo(SiteCapabilitySource.Derived));
            Assert.That(BlueprintValidator.Validate(blueprint).IsValid, Is.True);

            PlanningGraph graph = BlueprintCompiler.Compile(blueprint);
            Assert.That(graph.CutsceneStages.Single().Site, Is.EqualTo(pub));
            Assert.That(graph.CutsceneStages.Single().Requirements.Count, Is.EqualTo(2));
        }

        [Test]
        public void PlayerSpawnStageRegionAutomaticallyRequiresMinimumSpawnCapability()
        {
            var game = Campaign.Create("derived-player-spawn");
            SiteRef site = game.World.RequireSite("opening-site", _ => { });
            var actor = new CutsceneActorId("lead");
            var spawn = new CutsceneStagePointId("lead-spawn");
            var definition = new CutsceneDefinition(
                "opening",
                new CutsceneStageSetupDefinition(new[]
                {
                    new CutsceneActorPlacement(actor, spawn)
                }),
                new[]
                {
                    CutsceneStep.Dialogue(actor, new CutsceneCueId("lead.line"))
                },
                new[]
                {
                    new CutsceneStagePointRequirement(
                        spawn,
                        CutsceneStageRegion.PlayerSpawnArea,
                        minimumClearanceDecimetres: 10)
                });

            game.Story.Cutscene(definition, cutscene => cutscene
                .At(site)
                .Bind(actor, CutsceneActorTarget.Player(0)));

            SiteCapabilityRequirement spawnCapability = game.Build().Sites.Single()
                .Capabilities.Single(capability => capability.Kind == SiteCapabilityKind.PlayerSpawn);

            Assert.That(spawnCapability.Source, Is.EqualTo(SiteCapabilitySource.Derived));
            Assert.That(spawnCapability.MinimumCapacity, Is.EqualTo(1));
        }

        [Test]
        public void ConversationNpcAutomaticallyRequiresConversationSpace()
        {
            var game = Campaign.Create("derived-conversation-space");
            SiteRef destination = game.World.RequireSite("destination", site => site
                .Archetype(SiteArchetype.Ruin));

            game.World.RequireNpc("guide", npc => npc
                .PlaceAt(destination)
                .RequireConversation());

            CampaignBlueprint blueprint = game.Build();
            SiteCapabilityRequirement conversation = blueprint.Sites.Single()
                .Capabilities.Single(capability => capability.Kind == SiteCapabilityKind.ConversationSpace);

            Assert.That(conversation.Source, Is.EqualTo(SiteCapabilitySource.Derived));
            Assert.That(BlueprintValidator.Validate(blueprint).IsValid, Is.True);
        }

        [Test]
        public void ExplicitCapabilityRemainsAuthoredWhenContentAlsoRequiresIt()
        {
            var game = Campaign.Create("authored-capability-provenance");
            SiteRef destination = game.World.RequireSite("destination", site => site
                .Archetype(SiteArchetype.Ruin)
                .RequireCapability(SiteCapability.ConversationSpace));

            game.World.RequireNpc("guide", npc => npc
                .PlaceAt(destination)
                .RequireConversation());

            CampaignBlueprint blueprint = game.Build();
            SiteCapabilityRequirement conversation = blueprint.Sites.Single()
                .Capabilities.Single(capability => capability.Kind == SiteCapabilityKind.ConversationSpace);

            Assert.That(conversation.Source, Is.EqualTo(SiteCapabilitySource.Authored));
            Assert.That(blueprint.Sites.Single().Capabilities.Count(capability =>
                capability.Kind == SiteCapabilityKind.ConversationSpace), Is.EqualTo(1));
        }
    }
}
