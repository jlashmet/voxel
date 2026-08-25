using System;
using System.Collections.Generic;
using System.Linq;
using Game.Composition.Campaign.Content;
using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Runtime;
using Game.Composition.WorldBuilderWorldGen;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using Game.Cutscenes.Runtime;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeCampaignSessionBootstrapTests
    {
        private const uint Seed = 0x51A7u;

        private sealed class Actor : ICutsceneActorRuntime
        {
            public CutsceneInt3 Position { get; private set; }

            public Actor(CutsceneInt3 position) => Position = position;

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

        private sealed class ActorHost : IKentridgeCampaignActorHost
        {
            private readonly Dictionary<NpcRef, Actor> _npcs = new Dictionary<NpcRef, Actor>();
            private readonly Dictionary<int, Actor> _players = new Dictionary<int, Actor>();
            public readonly List<NpcRef> Prepared = new List<NpcRef>();

            public void AddPlayer(int slot, Actor actor) => _players.Add(slot, actor);
            public Actor Npc(NpcRef npc) => _npcs[npc];

            public void PrepareNpcs(IReadOnlyList<ResolvedNpcWorldPlacement> placements)
            {
                for (var i = 0; i < placements.Count; i++)
                {
                    ResolvedNpcWorldPlacement placement = placements[i];
                    Prepared.Add(placement.Npc);
                    _npcs[placement.Npc] = new Actor(ToCutscene(placement.Position.Position));
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

        private sealed class CameraCueRuntime : ICutsceneCameraCueRuntime
        {
            public ICutsceneOperation Execute(CutsceneCueId cue) => CompletedCutsceneOperation.Instance;
        }

        private sealed class DialogueCueRuntime : ICutsceneDialogueCueRuntime
        {
            public ICutsceneOperation Execute(CutsceneActorId speaker, CutsceneCueId cue) =>
                CompletedCutsceneOperation.Instance;
        }

        private sealed class SoundCueRuntime : ICutsceneSoundCueRuntime
        {
            public ICutsceneOperation Execute(CutsceneCueId cue) => CompletedCutsceneOperation.Instance;
        }

        [Test]
        public void BootstrapRunsKnownOpeningFromGeneratedWorldIntoDestinationConversation()
        {
            KnownOpeningCampaignContent content = KnownOpeningCampaignContent.Build(
                DialogueOnly("destination-conversation"));
            AuthoredTownPlan town = WorldBuilderTownAuthoring.Author(WorldBuilderTownIds.Kentridge, Seed);
            SettlementPlan settlement = KentridgeDefinition.Build(Seed);
            KentridgeCampaignGenerationPlan generation = KentridgeCampaignSessionBootstrap.Plan(
                content.Blueprint,
                town);
            var realizationFacts = new KentridgeCampaignRealizationFacts(
                new KentridgeVoxelSiteRealizationFacts(settlement, 1));

            var actors = new ActorHost();
            var player = new Actor(new CutsceneInt3(-999, -999, -999));
            actors.AddPlayer(0, player);

            KentridgeCampaignSession session = KentridgeCampaignSessionBootstrap.CreateSession(
                content.Blueprint,
                generation,
                realizationFacts,
                actors,
                Presentation());

            Assert.That(session.Blueprint, Is.SameAs(content.Blueprint));
            Assert.That(actors.Prepared.Count, Is.EqualTo(4));
            CollectionAssert.AreEquivalent(
                session.World.Npcs.Select(value => value.Npc).ToArray(),
                actors.Prepared.ToArray());
            for (var i = 0; i < session.World.Npcs.Count; i++)
            {
                ResolvedNpcWorldPlacement placement = session.World.Npcs[i];
                Assert.That(
                    actors.Npc(placement.Npc).Position,
                    Is.EqualTo(ToCutscene(placement.Position.Position)),
                    "NPCs must enter the authoritative actor host at their exact post-generation placement before story starts.");
            }

            Assert.That(session.Runtime.HasActiveCutscene, Is.False);
            int matched = session.StartNewGame();

            Assert.That(matched, Is.EqualTo(1));
            Assert.That(session.Runtime.HasActiveCutscene, Is.True);
            Assert.That(session.Runtime.ActiveCutscene, Is.EqualTo(content.IntroCutscene));

            CutsceneStageRealization stage = session.World.CutsceneStages
                .Single(value => value.Cutscene.Equals(content.IntroCutscene));
            Assert.That(
                player.Position,
                Is.EqualTo(stage.Binding.Resolve(
                    Game.Cutscenes.Content.Kentridge.KentridgeOpeningCutscene.LeadStart).Position),
                "Starting the opening must place the authoritative player runtime at the generated LeadStart stage point.");

            DrainActiveCutscene(session);
            Assert.That(session.Runtime.IsCutsceneCompleted(content.IntroCutscene), Is.True);
            Assert.That(session.Runtime.IsObjectiveActive(content.TravelObjective), Is.True,
                "Completing the recovered opening cutscene must activate the authored travel objective.");

            int interactionMatches = session.Runtime.InteractWithNpc(content.DestinationNpc);
            Assert.That(interactionMatches, Is.EqualTo(1));
            Assert.That(session.Runtime.IsObjectiveCompleted(content.TravelObjective), Is.True,
                "The destination interaction completes the travel objective after matching story rules observe its active state.");
            Assert.That(session.Runtime.HasActiveCutscene, Is.True);
            Assert.That(session.Runtime.ActiveCutscene, Is.EqualTo(content.DestinationCutscene));

            DrainActiveCutscene(session);
            Assert.That(session.Runtime.IsCutsceneCompleted(content.DestinationCutscene), Is.True);
        }

        [Test]
        public void MissingRequiredPlayerFailsBeforeNpcHostMutation()
        {
            KnownOpeningCampaignContent content = KnownOpeningCampaignContent.Build(
                DialogueOnly("destination-conversation"));
            AuthoredTownPlan town = WorldBuilderTownAuthoring.Author(WorldBuilderTownIds.Kentridge, Seed);
            SettlementPlan settlement = KentridgeDefinition.Build(Seed);
            KentridgeCampaignGenerationPlan generation = KentridgeCampaignSessionBootstrap.Plan(
                content.Blueprint,
                town);
            var realizationFacts = new KentridgeCampaignRealizationFacts(
                new KentridgeVoxelSiteRealizationFacts(settlement, 1));
            var actors = new ActorHost();

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                KentridgeCampaignSessionBootstrap.CreateSession(
                    content.Blueprint,
                    generation,
                    realizationFacts,
                    actors,
                    Presentation()));

            Assert.That(error.Message, Does.Contain("player slot 0"));
            Assert.That(actors.Prepared, Is.Empty,
                "Player preflight must fail before the authoritative NPC batch is created or repositioned.");
        }

        private static CutscenePresentationRouter Presentation() =>
            new CutscenePresentationRouter(
                new CameraCueRuntime(),
                new DialogueCueRuntime(),
                new SoundCueRuntime());

        private static void DrainActiveCutscene(KentridgeCampaignSession session)
        {
            for (var i = 0; i < 64 && session.Runtime.HasActiveCutscene; i++)
                session.Runtime.Tick(100000);

            Assert.That(session.Runtime.HasActiveCutscene, Is.False,
                "Cutscene did not complete within the deterministic integration-test tick budget.");
        }

        private static CutsceneDefinition DialogueOnly(string id) =>
            new CutsceneDefinition(
                id,
                CutsceneStageSetupDefinition.Empty,
                new[] { CutsceneStep.Dialogue(new CutsceneCueId(id + ".dialogue")) });

        private static CutsceneInt3 ToCutscene(Int3 value) =>
            new CutsceneInt3(value.X, value.Y, value.Z);
    }
}
