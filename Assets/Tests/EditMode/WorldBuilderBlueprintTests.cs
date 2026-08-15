using System.Linq;
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

            // Test-only loot values exercise the grammar; these are not campaign design values.
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

            // The user's story only says "somewhere else" so the physical archetype is
            // intentionally left unresolved. Validation reports this as a warning, not an error.
            var firstDestination = game.World.RequireSite("first-destination", site => site
                .DifferentSiteFrom(startingPub)
                .ReachableFrom(startingPub, TraversalProfile.NormalParty)
                .RequireCapability(SiteCapability.ConversationSpace));

            var destinationNpc = game.World.RequireNpc("destination-npc", npc => npc
                .PlaceAt(firstDestination)
                .RequireConversation());

            var travelObjective = game.Story.Objective("travel-to-first-destination", objective => objective
                .Target(firstDestination)
                .CompleteWhen(ObjectiveCompletion.InteractWith(destinationNpc)));

            var destinationCutscene = game.Story.Cutscene("destination-conversation", scene => scene
                .At(firstDestination)
                .Trigger(StoryTrigger.InteractWith(destinationNpc))
                .If(StoryCondition.ObjectiveActive(travelObjective))
                .If(StoryCondition.CutsceneNotCompleted(scene.Ref)));

            game.Story.Cutscene("intro-pub", scene => scene
                .At(startingPub)
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

            Assert.That(validation.IsValid, Is.True);
            Assert.That(validation.Diagnostics.Any(d => d.Code == "WB1001"), Is.True);
            Assert.That(blueprint.Sites.Count, Is.EqualTo(2));
            Assert.That(blueprint.Npcs.Single().Site, Is.EqualTo(firstDestination));
            Assert.That(blueprint.Objectives.Single().Target, Is.EqualTo(firstDestination));
            Assert.That(blueprint.Cutscenes.Any(c => c.Ref.Equals(destinationCutscene)), Is.True);
            Assert.That(
                blueprint.SpatialConstraints.Any(c =>
                    c.Kind == SpatialConstraintKind.ReachableFrom &&
                    c.Subject.Equals(firstDestination) &&
                    c.Target.Equals(startingPub)),
                Is.True);
        }

        [Test]
        public void CompilerSeparatesGenerationDependenciesFromStoryStateDependencies()
        {
            var game = Campaign.Create("compiler-test");

            var pub = game.World.RequireSite("pub", site => site.Archetype(SiteArchetype.Pub));
            var destination = game.World.RequireSite("destination", site => site.Archetype(SiteArchetype.Ruin));
            var npc = game.World.RequireNpc("npc", value => value.PlaceAt(destination).RequireConversation());
            var objective = game.Story.Objective("objective", value => value
                .Target(destination)
                .CompleteWhen(ObjectiveCompletion.InteractWith(npc)));

            game.Story.Cutscene("intro", scene => scene
                .At(pub)
                .Trigger(StoryTrigger.NewGame())
                .Then(StoryEffect.StartObjective(objective)));

            game.Story.Cutscene("destination-scene", scene => scene
                .At(destination)
                .Trigger(StoryTrigger.InteractWith(npc))
                .If(StoryCondition.ObjectiveActive(objective)));

            var graph = BlueprintCompiler.Compile(game.Build());
            var destinationScene = graph.Nodes.Single(n => n.Id == "cutscene:destination-scene");

            Assert.That(destinationScene.Dependencies, Does.Contain("site:destination"));
            Assert.That(destinationScene.Dependencies, Does.Contain("npc:npc"));
            Assert.That(destinationScene.Dependencies, Does.Not.Contain("objective:objective"));
        }
    }
}
