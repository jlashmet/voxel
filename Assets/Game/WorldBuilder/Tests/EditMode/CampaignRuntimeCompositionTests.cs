using System;
using System.Collections.Generic;
using Game.Composition.Campaign;
using Game.Composition.Campaign.Runtime;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.Cutscenes.Runtime;
using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CampaignRuntimeCompositionTests
    {
        [Test]
        public void KnownOpeningExecutesAcrossStoryCutsceneAndObjectiveComposition()
        {
            var game = Campaign.Create("runtime-opening");
            SiteRef startingPub = game.World.RequireSite("starting-pub", site => site
                .Archetype(SiteArchetype.Pub));
            SiteRef destination = game.World.RequireSite("first-destination", null);

            NpcRef madeline = game.World.RequireNpc("madeline", npc => npc.PlaceAt(startingPub));
            NpcRef steven = game.World.RequireNpc("steven", npc => npc.PlaceAt(startingPub));
            NpcRef logan = game.World.RequireNpc("logan", npc => npc.PlaceAt(startingPub));
            NpcRef destinationNpc = game.World.RequireNpc("destination-npc", npc => npc
                .PlaceAt(destination)
                .RequireConversation());

            ObjectiveRef travelObjective = game.Story.Objective(
                "travel-to-first-destination",
                objective => objective
                    .Target(destination)
                    .CompleteWhen(ObjectiveCompletion.InteractWith(destinationNpc)));

            var destinationDefinition = new CutsceneDefinition(
                "destination-conversation",
                CutsceneStageSetupDefinition.Empty,
                new[]
                {
                    CutsceneStep.Dialogue(new CutsceneCueId("destination-conversation.dialogue"))
                });
            CutsceneRef destinationCutscene = game.Story.Cutscene(
                destinationDefinition,
                scene => scene.At(destination));

            CutsceneRef intro = game.Story.Cutscene(
                KentridgeOpeningCutscene.Definition,
                scene => scene
                    .At(startingPub)
                    .Bind(KentridgeOpeningCutscene.Lead, CutsceneActorTarget.Player(0))
                    .Bind(KentridgeOpeningCutscene.Madeline, CutsceneActorTarget.Npc(madeline))
                    .Bind(KentridgeOpeningCutscene.Steven, CutsceneActorTarget.Npc(steven))
                    .Bind(KentridgeOpeningCutscene.Logan, CutsceneActorTarget.Npc(logan)));

            game.Story.Rule("start-intro", rule => rule
                .When(StoryTrigger.NewGame())
                .Then(StoryEffect.PlayCutscene(intro)));
            game.Story.Rule("start-travel-after-intro", rule => rule
                .When(StoryTrigger.CutsceneCompleted(intro))
                .Then(StoryEffect.StartObjective(travelObjective)));
            game.Story.Rule("destination-conversation-trigger", rule => rule
                .When(StoryTrigger.InteractWith(destinationNpc))
                .If(StoryCondition.ObjectiveActive(travelObjective))
                .If(StoryCondition.CutsceneNotCompleted(destinationCutscene))
                .Then(StoryEffect.PlayCutscene(destinationCutscene)));

            CampaignBlueprint blueprint = game.Build();
            CutsceneStageBinding introStage = BindAllStagePoints(KentridgeOpeningCutscene.Definition);
            var stages = new[]
            {
                new CutsceneStageRealization(intro, startingPub, introStage)
            };

            var actors = new FakeActorProvider()
                .Player(0, new FakeActorRuntime())
                .Npc(madeline, new FakeActorRuntime())
                .Npc(steven, new FakeActorRuntime())
                .Npc(logan, new FakeActorRuntime());

            var runtime = new CampaignRuntime(
                blueprint,
                stages,
                actors,
                CompletedPresentation.Instance);

            Assert.That(runtime.StartNewGame(), Is.EqualTo(1));
            Assert.That(runtime.HasActiveCutscene, Is.True);
            Assert.That(runtime.ActiveCutscene, Is.EqualTo(intro));
            Assert.That(runtime.IsObjectiveActive(travelObjective), Is.False);

            runtime.Tick(20000);

            Assert.That(runtime.HasActiveCutscene, Is.False);
            Assert.That(runtime.IsCutsceneCompleted(intro), Is.True);
            Assert.That(runtime.IsObjectiveActive(travelObjective), Is.True);
            Assert.That(runtime.IsObjectiveCompleted(travelObjective), Is.False);

            Assert.That(runtime.InteractWithNpc(destinationNpc), Is.EqualTo(1));

            Assert.That(runtime.HasActiveCutscene, Is.True,
                "The interaction rule must see the travel objective active before interaction completion.");
            Assert.That(runtime.ActiveCutscene, Is.EqualTo(destinationCutscene));
            Assert.That(runtime.IsObjectiveActive(travelObjective), Is.False);
            Assert.That(runtime.IsObjectiveCompleted(travelObjective), Is.True);

            runtime.Tick(0);

            Assert.That(runtime.HasActiveCutscene, Is.False);
            Assert.That(runtime.IsCutsceneCompleted(destinationCutscene), Is.True);
        }

        [Test]
        public void StagedCutsceneCannotStartWithoutRealizedStageBinding()
        {
            var game = Campaign.Create("missing-runtime-stage");
            SiteRef site = game.World.RequireSite("site", value => value.Archetype(SiteArchetype.Pub));
            var actor = new CutsceneActorId("speaker");
            var point = new CutsceneStagePointId("speaker-mark");
            var definition = new CutsceneDefinition(
                "staged-scene",
                new CutsceneStageSetupDefinition(new[]
                {
                    new CutsceneActorPlacement(actor, point)
                }),
                new[]
                {
                    CutsceneStep.Dialogue(actor, new CutsceneCueId("speaker.line"))
                },
                new[]
                {
                    new CutsceneStagePointRequirement(
                        point,
                        CutsceneStageRegion.InteriorGatheringArea,
                        8)
                });

            CutsceneRef cutscene = game.Story.Cutscene(definition, scene => scene
                .At(site)
                .Bind(actor, CutsceneActorTarget.Player(0)));
            game.Story.Rule("start-scene", rule => rule
                .When(StoryTrigger.NewGame())
                .Then(StoryEffect.PlayCutscene(cutscene)));

            var actors = new FakeActorProvider()
                .Player(0, new FakeActorRuntime());
            var runtime = new CampaignRuntime(
                game.Build(),
                Array.Empty<CutsceneStageRealization>(),
                actors,
                CompletedPresentation.Instance);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                runtime.StartNewGame());

            StringAssert.Contains("no stage realization was supplied", error.Message);
            Assert.That(runtime.HasActiveCutscene, Is.False);
        }

        private static CutsceneStageBinding BindAllStagePoints(CutsceneDefinition definition)
        {
            var stage = new CutsceneStageBinding();
            for (var i = 0; i < definition.RequiredStagePoints.Count; i++)
            {
                CutsceneStagePointId point = definition.RequiredStagePoints[i];
                stage.Bind(point, new CutsceneStagePoint(
                    new CutsceneInt3(100 + i * 20, 30, 200 + i * 20),
                    new CutsceneInt3(0, 0, 1)));
            }
            return stage;
        }

        private sealed class FakeActorProvider : IWorldBoundCutsceneActorProvider
        {
            private readonly Dictionary<NpcRef, ICutsceneActorRuntime> _npcs =
                new Dictionary<NpcRef, ICutsceneActorRuntime>();
            private readonly Dictionary<int, ICutsceneActorRuntime> _players =
                new Dictionary<int, ICutsceneActorRuntime>();

            public FakeActorProvider Npc(NpcRef npc, ICutsceneActorRuntime actor)
            {
                _npcs[npc] = actor;
                return this;
            }

            public FakeActorProvider Player(int slot, ICutsceneActorRuntime actor)
            {
                _players[slot] = actor;
                return this;
            }

            public bool TryResolveNpc(NpcRef npc, out ICutsceneActorRuntime actor) =>
                _npcs.TryGetValue(npc, out actor);

            public bool TryResolvePlayer(int playerSlot, out ICutsceneActorRuntime actor) =>
                _players.TryGetValue(playerSlot, out actor);
        }

        private sealed class FakeActorRuntime : ICutsceneActorRuntime
        {
            public CutsceneInt3 Position { get; private set; }

            public void PlaceAt(CutsceneStagePoint destination) =>
                Position = destination.Position;

            public ICutsceneOperation MoveTo(
                CutsceneStagePoint destination,
                int durationHintMilliseconds)
            {
                Position = destination.Position;
                return CompletedCutsceneOperation.Instance;
            }

            public ICutsceneOperation FaceTowards(CutsceneInt3 targetPosition) =>
                CompletedCutsceneOperation.Instance;
        }

        private sealed class CompletedPresentation : ICutscenePresentation
        {
            public static readonly CompletedPresentation Instance = new CompletedPresentation();
            private CompletedPresentation() { }

            public ICutsceneOperation SetCamera(CutsceneCueId cameraCue) =>
                CompletedCutsceneOperation.Instance;

            public ICutsceneOperation ShowDialogue(
                CutsceneActorId speaker,
                CutsceneCueId dialogueCue) =>
                CompletedCutsceneOperation.Instance;

            public ICutsceneOperation PlaySound(CutsceneCueId soundCue) =>
                CompletedCutsceneOperation.Instance;
        }
    }
}
