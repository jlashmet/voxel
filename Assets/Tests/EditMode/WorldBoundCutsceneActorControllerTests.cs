using System;
using System.Collections.Generic;
using System.Linq;
using Game.Composition.Campaign;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.Cutscenes.Runtime;
using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldBoundCutsceneActorControllerTests
    {
        [Test]
        public void KentridgeOpeningExecutesThroughWorldBuilderActorBindings()
        {
            var game = Campaign.Create("actor-binding-opening");
            SiteRef pub = game.World.RequireSite("starting-pub", site => site.Archetype(SiteArchetype.Pub));
            NpcRef madeline = game.World.RequireNpc("madeline", npc => npc.PlaceAt(pub));
            NpcRef steven = game.World.RequireNpc("steven", npc => npc.PlaceAt(pub));
            NpcRef logan = game.World.RequireNpc("logan", npc => npc.PlaceAt(pub));

            CutsceneRef intro = game.Story.Cutscene(KentridgeOpeningCutscene.Definition, scene => scene
                .At(pub)
                .Bind(KentridgeOpeningCutscene.Lead, CutsceneActorTarget.Player(0))
                .Bind(KentridgeOpeningCutscene.Madeline, CutsceneActorTarget.Npc(madeline))
                .Bind(KentridgeOpeningCutscene.Steven, CutsceneActorTarget.Npc(steven))
                .Bind(KentridgeOpeningCutscene.Logan, CutsceneActorTarget.Npc(logan)));

            CutsceneSpec spec = game.Build().Cutscenes.Single(value => value.Ref.Equals(intro));
            var leadRuntime = new FakeActorRuntime();
            var madelineRuntime = new FakeActorRuntime();
            var stevenRuntime = new FakeActorRuntime();
            var loganRuntime = new FakeActorRuntime();
            var provider = new FakeActorProvider()
                .Player(0, leadRuntime)
                .Npc(madeline, madelineRuntime)
                .Npc(steven, stevenRuntime)
                .Npc(logan, loganRuntime);

            var actors = new WorldBoundCutsceneActorController(spec, provider);
            CutsceneStageBinding stage = BindAllStagePoints(spec.Definition);
            CutsceneRunner runner = CutscenePlayback.Start(
                spec.Definition,
                actors,
                CompletedPresentation.Instance,
                stage);

            runner.Tick(20000);

            Assert.That(runner.IsComplete, Is.True);
            Assert.That(leadRuntime.Position,
                Is.EqualTo(stage.Resolve(KentridgeOpeningCutscene.LeadStage).Position));
            Assert.That(loganRuntime.Position,
                Is.EqualTo(stage.Resolve(KentridgeOpeningCutscene.LoganStop).Position));
            Assert.That(madelineRuntime.Position,
                Is.EqualTo(stage.Resolve(KentridgeOpeningCutscene.MadelineStage).Position));
            Assert.That(stevenRuntime.Position,
                Is.EqualTo(stage.Resolve(KentridgeOpeningCutscene.StevenStage).Position));
        }

        [Test]
        public void MissingRuntimeTargetFailsBeforePlayback()
        {
            var game = Campaign.Create("missing-runtime-actor");
            SiteRef pub = game.World.RequireSite("pub", site => site.Archetype(SiteArchetype.Pub));
            NpcRef speaker = game.World.RequireNpc("speaker", npc => npc.PlaceAt(pub));
            var actor = new CutsceneActorId("speaker");
            var definition = new CutsceneDefinition(
                "speaker-scene",
                CutsceneStageSetupDefinition.Empty,
                new[] { CutsceneStep.Dialogue(actor, new CutsceneCueId("speaker.line")) });
            CutsceneRef sceneRef = game.Story.Cutscene(definition, scene => scene
                .At(pub)
                .Bind(actor, CutsceneActorTarget.Npc(speaker)));
            CutsceneSpec spec = game.Build().Cutscenes.Single(value => value.Ref.Equals(sceneRef));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                new WorldBoundCutsceneActorController(spec, new FakeActorProvider()));

            StringAssert.Contains("cannot resolve runtime target", error.Message);
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

            public ICutsceneOperation ShowDialogue(CutsceneActorId speaker, CutsceneCueId dialogueCue) =>
                CompletedCutsceneOperation.Instance;

            public ICutsceneOperation PlaySound(CutsceneCueId soundCue) =>
                CompletedCutsceneOperation.Instance;
        }
    }
}
