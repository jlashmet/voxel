using System.Linq;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldBuilderBlueprintTests
    {
        [Test]
        public void KnownOpeningBeatsCanBeExpressedWithoutWorldCoordinates()
        {
            var game = Campaign.Create("main-campaign");

            var secretLoot = game.Loot.Table("standard-secret-treasure", loot => loot
                .RollCount(1, 3)
                .Guaranteed(LootCategory.Currency)
                .Weighted(LootCategory.Consumable, 4)
                .Weighted(LootCategory.Equipment, 1));

            var startingPub = game.World.RequireSite("starting-pub", site => site
                .Archetype(SiteArchetype.Pub)
                .RequireCapability(SiteCapability.Interior)
                .RequireCapability(SiteCapability.PlayerSpawn(4))
                .RequireCapability(SiteCapability.CutsceneStage)
                .RequireCapability(SiteCapability.PublicExit));

            var firstDestination = game.World.RequireSite("first-destination", site => site
                .DifferentSiteFrom(startingPub)
                .ReachableFrom(startingPub, TraversalProfile.NormalParty)
                .RequireCapability(SiteCapability.ConversationSpace));

            var madeline = game.World.RequireNpc("madeline", npc => npc.PlaceAt(startingPub));
            var steven = game.World.RequireNpc("steven", npc => npc.PlaceAt(startingPub));
            var logan = game.World.RequireNpc("logan", npc => npc.PlaceAt(startingPub));
            var destinationNpc = game.World.RequireNpc("destination-npc", npc => npc
                .PlaceAt(firstDestination)
                .RequireConversation());

            var travelObjective = game.Story.Objective("travel-to-first-destination", objective => objective
                .Target(firstDestination)
                .CompleteWhen(ObjectiveCompletion.InteractWith(destinationNpc)));

            var destinationDefinition = DialogueOnly("destination-conversation");
            var destinationCutscene = game.Story.Cutscene(destinationDefinition, scene => scene
                .At(firstDestination)
                .Trigger(StoryTrigger.InteractWith(destinationNpc))
                .If(StoryCondition.ObjectiveActive(travelObjective))
                .If(StoryCondition.CutsceneNotCompleted(scene.Ref)));

            var introRef = game.Story.Cutscene(KentridgeOpeningCutscene.Definition, scene => scene
                .At(startingPub)
                .Bind(KentridgeOpeningCutscene.Lead, CutsceneActorTarget.Player(0))
                .Bind(KentridgeOpeningCutscene.Madeline, CutsceneActorTarget.Npc(madeline))
                .Bind(KentridgeOpeningCutscene.Steven, CutsceneActorTarget.Npc(steven))
                .Bind(KentridgeOpeningCutscene.Logan, CutsceneActorTarget.Npc(logan))
                .Trigger(StoryTrigger.NewGame())
                .Then(StoryEffect.StartObjective(travelObjective)));

            game.World.Secrets.Policy("world-secrets", policy => policy
                .Scope(SecretScope.ExplorableSites)
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .Distribution(new SecretDistribution(0, 2, 2500))
                .RequireHiddenSpace()
                .RewardWith(secretLoot));

            var blueprint = game.Build();
            var validation = BlueprintValidator.Validate(blueprint);
            var intro = blueprint.Cutscenes.Single(c => c.Ref.Equals(introRef));

            Assert.That(validation.IsValid, Is.True);
            Assert.That(validation.Diagnostics.Any(d => d.Code == "WB1001"), Is.True);
            Assert.That(blueprint.Sites.Count, Is.EqualTo(2));
            Assert.That(blueprint.Npcs.Single(n => n.Ref.Equals(destinationNpc)).Site, Is.EqualTo(firstDestination));
            Assert.That(blueprint.Objectives.Single().Target, Is.EqualTo(firstDestination));
            Assert.That(blueprint.Cutscenes.Any(c => c.Ref.Equals(destinationCutscene)), Is.True);
            Assert.That(intro.Definition.RequiredActors.Count, Is.EqualTo(4));
            Assert.That(intro.ActorBindings.Count, Is.EqualTo(4));
            Assert.That(intro.StageRequirements.Count, Is.EqualTo(7));
            Assert.That(intro.Definition.StageRequirements.All(r => r.Region != CutsceneStageRegion.Unspecified), Is.True);
            Assert.That(intro.Definition.StageRequirements.Any(r => r.Region == CutsceneStageRegion.PublicEntrance), Is.True);
            Assert.That(intro.Definition.StageRequirements.Any(r => r.Region == CutsceneStageRegion.InteriorGatheringArea), Is.True);
            Assert.That(
                blueprint.SpatialConstraints.Any(c =>
                    c.Kind == SpatialConstraintKind.ReachableFrom &&
                    c.Subject.Equals(firstDestination) &&
                    c.Target.Equals(startingPub)),
                Is.True);
        }

        [Test]
        public void CompilerCarriesCutsceneActorsAndStagePointsIntoGenerationPlan()
        {
            var game = Campaign.Create("compiler-test");
            var actor = new CutsceneActorId("guide");
            var stagePoint = new CutsceneStagePointId("guide-mark");
            var definition = new CutsceneDefinition(
                "destination-scene",
                new CutsceneStageSetupDefinition(new[] { new CutsceneActorPlacement(actor, stagePoint) }),
                new[] { CutsceneStep.Dialogue(actor, new CutsceneCueId("guide.arrives")) },
                new[]
                {
                    new CutsceneStagePointRequirement(
                        stagePoint,
                        CutsceneStageRegion.InteriorGatheringArea,
                        8,
                        CutsceneStageFacingHint.TowardStageCenter)
                });

            var destination = game.World.RequireSite("destination", site => site
                .Archetype(SiteArchetype.Ruin)
                .RequireCapability(SiteCapability.CutsceneStage));
            var npc = game.World.RequireNpc("npc", value => value.PlaceAt(destination).RequireConversation());
            var objective = game.Story.Objective("objective", value => value
                .Target(destination)
                .CompleteWhen(ObjectiveCompletion.InteractWith(npc)));

            game.Story.Cutscene(definition, scene => scene
                .At(destination)
                .Bind(actor, CutsceneActorTarget.Npc(npc))
                .Trigger(StoryTrigger.InteractWith(npc))
                .If(StoryCondition.ObjectiveActive(objective)));

            var graph = BlueprintCompiler.Compile(game.Build());
            var destinationScene = graph.Nodes.Single(n => n.Id == "cutscene:destination-scene");
            var stage = graph.CutsceneStages.Single(s => s.Cutscene.Id == "destination-scene");
            var requirement = stage.Requirements.Single();

            Assert.That(destinationScene.Dependencies, Does.Contain("site:destination"));
            Assert.That(destinationScene.Dependencies, Does.Contain("npc:npc"));
            Assert.That(destinationScene.Dependencies, Does.Not.Contain("objective:objective"));
            Assert.That(stage.Site, Is.EqualTo(destination));
            Assert.That(requirement.Point, Is.EqualTo(stagePoint));
            Assert.That(requirement.Region, Is.EqualTo(CutsceneStageRegion.InteriorGatheringArea));
            Assert.That(requirement.MinimumClearanceDecimetres, Is.EqualTo(8));
        }

        [Test]
        public void ValidatorRejectsMissingCutsceneActorBinding()
        {
            var game = Campaign.Create("missing-binding");
            var speaker = new CutsceneActorId("speaker");
            var definition = new CutsceneDefinition(
                "scene",
                CutsceneStageSetupDefinition.Empty,
                new[] { CutsceneStep.Dialogue(speaker, new CutsceneCueId("speaker.line")) });
            var site = game.World.RequireSite("site", value => value.Archetype(SiteArchetype.Pub));

            game.Story.Cutscene(definition, scene => scene
                .At(site)
                .Trigger(StoryTrigger.NewGame()));

            var validation = BlueprintValidator.Validate(game.Build());
            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Diagnostics.Any(d => d.Code == "WB2211"), Is.True);
        }

        private static CutsceneDefinition DialogueOnly(string id) =>
            new CutsceneDefinition(
                id,
                CutsceneStageSetupDefinition.Empty,
                new[] { CutsceneStep.Dialogue(new CutsceneCueId(id + ".dialogue")) });
    }
}
