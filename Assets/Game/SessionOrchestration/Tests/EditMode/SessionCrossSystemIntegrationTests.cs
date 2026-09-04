using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Combat.Api;
using Game.Combat.Runtime;
using Game.Cutscenes.Api;
using Game.Encounters.Api;
using Game.Encounters.Runtime;
using Game.Outcomes.Api;
using Game.Quests.Api;
using Game.SessionOrchestration.Api;
using Game.SessionOrchestration.Runtime;
using Game.Story.Api;
using Game.Story.Runtime;
using Game.Vitality.Api;
using Game.Vitality.Runtime;
using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace Game.SessionOrchestration.Tests
{
    public sealed class SessionCrossSystemIntegrationTests
    {
        [Test]
        public void RunningGraph_OrdersSemanticInteractionStoryProgressionEncounterAndCombatThroughPublicApis()
        {
            var trace = new List<string>();
            CharacterId playerId = CharacterId.FromStableKey("session-orchestration", "player");
            CharacterId enemyId = CharacterId.FromStableKey("session-orchestration", "enemy");
            var characters = new FixtureCharacterQuery(playerId, enemyId);
            var encounters = new EncounterRegistry(characters);
            var vitality = new VitalityRegistry();
            Assert.That(vitality.Register(VitalitySnapshot.Alive(playerId, 6)), Is.True);
            Assert.That(vitality.Register(VitalitySnapshot.Alive(enemyId, 6)), Is.True);
            var combat = new CombatService(vitality);
            var graph = new IntegrationGraph(
                trace,
                encounters,
                new EncounterCombatCoordinator(combat),
                combat,
                playerId,
                enemyId);
            var runtime = new GameSessionOrchestrator(new SingleGraphFactory(graph));

            GameSessionOperationResult prepared = runtime.Prepare(GameSessionStartRequest.NewGame(
                new GameSessionIdentity("integration-campaign", "integration-world", "integration-session", "headless")));
            Assert.That(prepared.Succeeded, Is.True);
            Assert.That(runtime.EnterRunning().Succeeded, Is.True);
            Assert.That(runtime.Tick(16).Succeeded, Is.True);

            CollectionAssert.AreEqual(new[] { "interaction", "encounter", "combat" }, trace,
                "SessionOrchestration must execute semantic adapters in deterministic cross-system order.");
            Assert.That(graph.StoryAdvanced, Is.True,
                "The semantic interaction must be evaluated by the real Story rule engine.");
            Assert.That(graph.ProgressionAdvanced, Is.True,
                "The Story effect must cross the public progression adapter boundary.");
            Assert.That(encounters.TryGet(graph.EncounterId, out EncounterSnapshot snapshot), Is.True);
            Assert.That(snapshot.Lifecycle, Is.EqualTo(EncounterLifecycleState.Resolved));
            Assert.That(combat.State, Is.EqualTo(CombatLifecycleState.Completed));
            Assert.That(combat.WinningTeam.HasValue, Is.True);
        }

        private sealed class IntegrationGraph : ISessionRuntimeGraph
        {
            private const string JoinedPartyMember = "integration-ally";

            private readonly EncounterRegistry _encounters;
            private readonly IEncounterCombatCoordinator _combatCoordinator;
            private readonly CombatService _combat;
            private readonly CharacterId _playerId;
            private readonly CharacterId _enemyId;
            private readonly IReadOnlyList<ISessionUpdateStep> _steps;
            private readonly CampaignBlueprint _campaign;
            private readonly NpcRef _storyNpc;
            private readonly StoryProgressAdapter _storyProgress = new StoryProgressAdapter();
            private int _matchedStoryRules;

            public IntegrationGraph(
                List<string> trace,
                EncounterRegistry encounters,
                IEncounterCombatCoordinator combatCoordinator,
                CombatService combat,
                CharacterId playerId,
                CharacterId enemyId)
            {
                _encounters = encounters;
                _combatCoordinator = combatCoordinator;
                _combat = combat;
                _playerId = playerId;
                _enemyId = enemyId;
                EncounterId = new EncounterId("session-orchestration:encounter");

                CampaignBuilder campaign = Campaign.Create("integration-campaign");
                RegionHandle region = campaign.World.Region("integration-region");
                SiteHandle site = region.Site("interaction-site");
                NpcHandle npc = site.Npc("progression-npc", builder => builder.RequireConversation());
                campaign.Story.Rule("interaction-progresses-story", rule => rule
                    .When(StoryTrigger.InteractWith(npc))
                    .Then(StoryEffect.JoinPartyMember(JoinedPartyMember)));
                _campaign = campaign.Build();
                _storyNpc = npc.Ref;

                _steps = new ISessionUpdateStep[]
                {
                    new CombatStep(this, trace),
                    new EncounterStep(this, trace),
                    new InteractionStep(this, trace)
                };
            }

            public EncounterId EncounterId { get; }
            public bool StoryAdvanced => _matchedStoryRules == 1;
            public bool ProgressionAdvanced => _storyProgress.IsPartyMemberJoined(JoinedPartyMember);
            public bool GameplayBindingsReady => true;
            public IReadOnlyList<ISessionUpdateStep> UpdateSteps => _steps;
            public IGameOutcomeQuery OutcomeQuery => null;

            public void InitializeNewGame()
            {
                Assert.That(_encounters.Register(
                    new EncounterDefinition(EncounterId, EncounterCombatPolicy.Required, "headless integration"),
                    out _), Is.EqualTo(EncounterMutationFailure.None));
                Assert.That(_encounters.Join(
                    EncounterId,
                    new EncounterParticipant(_playerId, EncounterParticipantOwnership.Persistent, "player"),
                    out _), Is.EqualTo(EncounterMutationFailure.None));
                Assert.That(_encounters.Join(
                    EncounterId,
                    new EncounterParticipant(_enemyId, EncounterParticipantOwnership.EncounterOwned, "enemy"),
                    out _), Is.EqualTo(EncounterMutationFailure.None));
            }

            public void StartCommands() { }
            public void StopCommands() { }
            public void SettleAuthoritativeState() { }
            public void DetachExternalAdapters() { }
            public void Dispose() { }

            private void ApplyInteraction()
            {
                _matchedStoryRules = StoryRuleEngine.Dispatch(
                    _campaign.StoryRules,
                    StoryEvent.NpcInteracted(_storyNpc),
                    _storyProgress,
                    _storyProgress);
                Assert.That(_matchedStoryRules, Is.EqualTo(1),
                    "Semantic interaction must match the authored Story rule exactly once.");
                Assert.That(_encounters.Activate(
                    new EncounterActivationRequest(EncounterId, "semantic-interaction"),
                    out _), Is.EqualTo(EncounterMutationFailure.None));
            }

            private void RouteEncounterToCombat()
            {
                Assert.That(_encounters.TryTakeCombatRequest(out EncounterCombatRequest request), Is.True);
                var participants = new List<CombatParticipant>(request.Participants.Count);
                for (int i = 0; i < request.Participants.Count; i++)
                {
                    EncounterParticipant participant = request.Participants[i];
                    CombatTeam team = participant.Role == "player" ? CombatTeam.Player : CombatTeam.Enemy;
                    participants.Add(CombatParticipant.FromCharacter(participant.CharacterId, team));
                }
                _combatCoordinator.Start(new CombatStartRequest(request.EncounterId, participants));
            }

            private void ResolveCombatIntoEncounter()
            {
                new CombatAiBattleDriver(_combat, 41).RunToCompletion(64);
                Assert.That(_combatCoordinator.TryTakeResolved(out CombatResolved resolved), Is.True);
                EncounterResolution resolution = resolved.WinningTeam == CombatTeam.Player
                    ? new EncounterResolution(EncounterResolutionResult.Completed, "player team won combat")
                    : new EncounterResolution(EncounterResolutionResult.Failed, "enemy team won combat");
                Assert.That(_encounters.ApplyCombatResolved(EncounterId, resolution, out _),
                    Is.EqualTo(EncounterMutationFailure.None));
            }

            private sealed class InteractionStep : ISessionUpdateStep
            {
                private readonly IntegrationGraph _owner;
                private readonly List<string> _trace;
                public InteractionStep(IntegrationGraph owner, List<string> trace) { _owner = owner; _trace = trace; }
                public SessionUpdatePhase Phase => SessionUpdatePhase.Interaction;
                public int Order => 0;
                public string SemanticId => "integration.interaction";
                public void Tick(int elapsedMilliseconds) { _trace.Add("interaction"); _owner.ApplyInteraction(); }
            }

            private sealed class EncounterStep : ISessionUpdateStep
            {
                private readonly IntegrationGraph _owner;
                private readonly List<string> _trace;
                public EncounterStep(IntegrationGraph owner, List<string> trace) { _owner = owner; _trace = trace; }
                public SessionUpdatePhase Phase => SessionUpdatePhase.Encounter;
                public int Order => 0;
                public string SemanticId => "integration.encounter";
                public void Tick(int elapsedMilliseconds) { _trace.Add("encounter"); _owner.RouteEncounterToCombat(); }
            }

            private sealed class CombatStep : ISessionUpdateStep
            {
                private readonly IntegrationGraph _owner;
                private readonly List<string> _trace;
                public CombatStep(IntegrationGraph owner, List<string> trace) { _owner = owner; _trace = trace; }
                public SessionUpdatePhase Phase => SessionUpdatePhase.Combat;
                public int Order => 0;
                public string SemanticId => "integration.combat";
                public void Tick(int elapsedMilliseconds) { _trace.Add("combat"); _owner.ResolveCombatIntoEncounter(); }
            }
        }

        private sealed class StoryProgressAdapter : IStoryStateView, IStoryProgressEffectSink
        {
            private readonly HashSet<string> _joinedPartyMembers = new HashSet<string>(StringComparer.Ordinal);

            public bool IsPartyMemberJoined(string memberId) => _joinedPartyMembers.Contains(memberId);
            public bool IsObjectiveActive(ObjectiveRef objective) => false;
            public bool IsQuestActive(QuestRef quest) => false;
            public bool IsQuestCompleted(QuestRef quest) => false;
            public bool IsCutsceneCompleted(CutsceneRef cutscene) => false;
            public void StartObjective(ObjectiveRef objective) =>
                throw new AssertionException("This integration rule must not start an objective.");
            public void StartQuest(QuestRef quest) =>
                throw new AssertionException("This integration rule must not start a quest.");
            public void PlayCutscene(CutsceneRef cutscene) =>
                throw new AssertionException("This integration rule must not play a cutscene.");
            public void JoinPartyMember(string memberId) => _joinedPartyMembers.Add(memberId);
            public void GrantSpell(string spellId) =>
                throw new AssertionException("This integration rule must not grant a spell.");
        }

        private sealed class SingleGraphFactory : ISessionRuntimeGraphFactory
        {
            private readonly ISessionRuntimeGraph _graph;
            public SingleGraphFactory(ISessionRuntimeGraph graph) { _graph = graph; }
            public ISessionRuntimeGraph Compose(GameSessionIdentity identity) => _graph;
        }

        private sealed class FixtureCharacterQuery : ICharacterQuery
        {
            private readonly Dictionary<CharacterId, CharacterSnapshot> _snapshots =
                new Dictionary<CharacterId, CharacterSnapshot>();

            public FixtureCharacterQuery(params CharacterId[] ids)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    CharacterId id = ids[i];
                    _snapshots.Add(id, new CharacterSnapshot(
                        new CharacterDefinition(id, CharacterTraits.Combatant),
                        CharacterLifecycleState.Active,
                        default,
                        1));
                }
            }

            public IReadOnlyList<CharacterSnapshot> GetAll() =>
                new List<CharacterSnapshot>(_snapshots.Values).AsReadOnly();
            public bool TryGet(CharacterId id, out CharacterSnapshot snapshot) =>
                _snapshots.TryGetValue(id, out snapshot);
            public bool TryResolve(CharacterBinding binding, out CharacterId id)
            {
                id = default;
                return false;
            }
        }
    }
}
