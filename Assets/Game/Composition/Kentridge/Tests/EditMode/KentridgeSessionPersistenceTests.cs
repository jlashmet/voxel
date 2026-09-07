using System;
using System.Collections.Generic;
using System.IO;
using Game.Composition.Campaign.Content;
using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Runtime;
using Game.Composition.WorldBuilderWorldGen;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using Game.Persistence.Runtime;
using Game.SessionOrchestration.Api;
using Game.SessionOrchestration.Runtime;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;

namespace Game.Composition.Kentridge.Tests
{
    public sealed class KentridgeSessionPersistenceTests
    {
        private const uint Seed = 0x51A7u;

        [Test]
        public void ResumeRestoresSemanticCampaignStateIntoFreshGraphWithoutReplayingNewGame()
        {
            string saveRoot = Path.Combine(
                Path.GetTempPath(),
                "GameSystem26-" + Guid.NewGuid().ToString("N"));

            try
            {
                KnownOpeningCampaignContent content = KnownOpeningCampaignContent.Build(
                    DialogueOnly("destination-conversation"));
                AuthoredTownPlan town = WorldBuilderTownAuthoring.Author(
                    WorldBuilderTownIds.Kentridge,
                    Seed);
                SettlementPlan settlement = KentridgeDefinition.Build(Seed);
                KentridgeCampaignGenerationPlan generation = KentridgeCampaignSessionBootstrap.Plan(
                    content.Blueprint,
                    town);
                var realizationFacts = new KentridgeCampaignRealizationFacts(
                    new KentridgeVoxelSiteRealizationFacts(settlement, 1));
                var store = new FileSessionSaveStore(saveRoot);
                var persistence = new KentridgeSessionPersistenceBridge(store, () => 123456789L);

                var sourceIdentity = new GameSessionIdentity(
                    "game-system-26-campaign",
                    "kentridge",
                    "source-slot",
                    "default");
                KentridgeSessionRuntimeGraphFactory sourceFactory = CreateFactory(
                    content,
                    generation,
                    realizationFacts);
                var source = new GameSessionOrchestrator(sourceFactory, persistence);

                Assert.That(
                    source.Prepare(GameSessionStartRequest.NewGame(sourceIdentity)).Succeeded,
                    Is.True);
                Assert.That(source.EnterRunning().Succeeded, Is.True);
                KentridgeSessionRuntimeGraph sourceGraph = sourceFactory.Current;
                Assert.That(sourceGraph, Is.Not.Null);
                Assert.That(sourceGraph.LastNewGameMatchedCount, Is.GreaterThan(0));

                DrainActiveCutscene(source, sourceGraph, content.IntroCutscene);
                Assert.That(
                    sourceGraph.Session.Runtime.IsCutsceneCompleted(content.IntroCutscene),
                    Is.True);
                Assert.That(
                    sourceGraph.Session.Runtime.IsObjectiveActive(content.TravelObjective),
                    Is.True);
                Assert.That(sourceGraph.Session.Runtime.HasActiveCutscene, Is.False);

                Assert.That(source.Capture().Succeeded, Is.True);
                Assert.That(source.Shutdown().Succeeded, Is.True);
                Assert.That(sourceFactory.Current, Is.Null,
                    "Shutdown must release the captured graph before a fresh restore graph is composed.");

                var resumeIdentity = new GameSessionIdentity(
                    "game-system-26-campaign",
                    "kentridge",
                    "resume-slot",
                    "default");
                KentridgeSessionRuntimeGraphFactory resumeFactory = CreateFactory(
                    content,
                    generation,
                    realizationFacts);
                var resumed = new GameSessionOrchestrator(resumeFactory, persistence);

                GameSessionOperationResult prepared = resumed.Prepare(
                    GameSessionStartRequest.Resume(resumeIdentity, sourceIdentity.SessionId));
                Assert.That(prepared.Succeeded, Is.True, prepared.Diagnostic);
                KentridgeSessionRuntimeGraph resumeGraph = resumeFactory.Current;
                Assert.That(resumeGraph, Is.Not.Null);
                Assert.That(resumeGraph, Is.Not.SameAs(sourceGraph),
                    "System14 resume must restore into the newly composed graph, not reuse the captured runtime.");
                Assert.That(resumeGraph.LastNewGameMatchedCount, Is.EqualTo(0),
                    "System14 restore must not run NewGame initialization while composing the fresh graph.");
                Assert.That(resumeGraph.Session.Runtime.HasActiveCutscene, Is.False,
                    "Completed historical cutscenes must not replay during System16 restore.");
                Assert.That(
                    resumeGraph.Session.Runtime.IsCutsceneCompleted(content.IntroCutscene),
                    Is.True);
                Assert.That(
                    resumeGraph.Session.Runtime.IsObjectiveActive(content.TravelObjective),
                    Is.True,
                    "Current progression state must be restored alongside completed one-shot history.");

                Assert.That(resumed.EnterRunning().Succeeded, Is.True);
                Assert.That(resumeGraph.LastNewGameMatchedCount, Is.EqualTo(0));
                Assert.That(resumeGraph.Session.Runtime.HasActiveCutscene, Is.False);
                Assert.That(resumed.Shutdown().Succeeded, Is.True);
            }
            finally
            {
                if (Directory.Exists(saveRoot))
                    Directory.Delete(saveRoot, true);
            }
        }

        private static KentridgeSessionRuntimeGraphFactory CreateFactory(
            KnownOpeningCampaignContent content,
            KentridgeCampaignGenerationPlan generation,
            KentridgeCampaignRealizationFacts realizationFacts)
        {
            var actors = new ActorHost();
            actors.AddPlayer(0, new Actor(new CutsceneInt3(-999, -999, -999)));
            return new KentridgeSessionRuntimeGraphFactory(
                content.Blueprint,
                generation,
                realizationFacts,
                actors,
                new ImmediatePresentation());
        }

        private static void DrainActiveCutscene(
            GameSessionOrchestrator orchestrator,
            KentridgeSessionRuntimeGraph graph,
            CutsceneRef expected)
        {
            Assert.That(graph.Session.Runtime.HasActiveCutscene, Is.True);
            Assert.That(graph.Session.Runtime.ActiveCutscene, Is.EqualTo(expected));
            for (var i = 0; i < 64 && graph.Session.Runtime.HasActiveCutscene; i++)
            {
                GameSessionOperationResult tick = orchestrator.Tick(100000);
                Assert.That(tick.Succeeded, Is.True, tick.Diagnostic);
            }
            Assert.That(graph.Session.Runtime.HasActiveCutscene, Is.False,
                "Cutscene did not complete within the deterministic integration-test tick budget.");
        }

        private static CutsceneDefinition DialogueOnly(string id) =>
            new CutsceneDefinition(
                id,
                CutsceneStageSetupDefinition.Empty,
                new[] { CutsceneStep.Dialogue(new CutsceneCueId(id + ".dialogue")) });

        private sealed class Actor : ICutsceneActorRuntime
        {
            public CutsceneInt3 Position { get; private set; }

            public Actor(CutsceneInt3 position) => Position = position;

            public void PlaceAt(CutsceneStagePoint destination) => Position = destination.Position;

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

        private sealed class ActorHost : IKentridgeCampaignActorHost
        {
            private readonly Dictionary<NpcRef, Actor> _npcs = new Dictionary<NpcRef, Actor>();
            private readonly Dictionary<int, Actor> _players = new Dictionary<int, Actor>();

            public void AddPlayer(int slot, Actor actor) => _players.Add(slot, actor);

            public void PrepareNpcs(IReadOnlyList<ResolvedNpcWorldPlacement> placements)
            {
                for (var i = 0; i < placements.Count; i++)
                {
                    ResolvedNpcWorldPlacement placement = placements[i];
                    _npcs[placement.Npc] = new Actor(
                        new CutsceneInt3(
                            placement.Position.Position.X,
                            placement.Position.Position.Y,
                            placement.Position.Position.Z));
                }
            }

            public bool TryResolveNpc(NpcRef npc, out ICutsceneActorRuntime actor)
            {
                Actor value;
                bool found = _npcs.TryGetValue(npc, out value);
                actor = value;
                return found;
            }

            public bool TryResolvePlayer(int playerSlot, out ICutsceneActorRuntime actor)
            {
                Actor value;
                bool found = _players.TryGetValue(playerSlot, out value);
                actor = value;
                return found;
            }
        }

        private sealed class ImmediatePresentation : ICutscenePresentation
        {
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
