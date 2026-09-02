using System;
using System.Collections.Generic;
using Game.CharacterAI.Adapters.Combat;
using Game.CharacterAI.Api;
using Game.CharacterAI.Runtime;
using Game.Characters.Api;
using Game.Combat.Api;
using Game.Combat.Runtime;
using NUnit.Framework;

namespace Game.CharacterAI.Tests
{
    public sealed class CharacterAiRuntimeTests
    {
        [Test]
        public void TacticalEnemyUsesCommonIntentControllerAndExistingCombatDriver()
        {
            var combat = new CombatService();
            var enemyParticipant = new CombatParticipantId("enemy-1");
            var playerParticipant = new CombatParticipantId("player-1");
            combat.BeginCombat(new CombatEncounterRequest("fixture-combat", new[]
            {
                new CombatParticipant(enemyParticipant, CombatTeam.Enemy),
                new CombatParticipant(playerParticipant, CombatTeam.Player)
            }));

            CharacterId enemy = CharacterId.FromStableKey("enemy", "fixture-1");
            CharacterId player = CharacterId.FromStableKey("player", "fixture-1");
            var bindings = new Dictionary<CombatParticipantId, CharacterId>
            {
                { enemyParticipant, enemy },
                { playerParticipant, player }
            };
            var driver = new CombatAiBattleDriver(combat, 17);
            var controller = new CharacterAiController(
                enemy,
                new CombatPerceptionSource(combat, bindings),
                new CombatTacticalIntentPolicy(),
                new CombatTacticalIntentExecutor(driver.Step));

            AiIntentExecutionResult result = controller.Tick();

            Assert.That(result.Accepted, Is.True);
            Assert.That(controller.State.Mode, Is.EqualTo(AiControlMode.Tactical));
            Assert.That(controller.State.CurrentIntent.Kind, Is.EqualTo(AiIntentKind.TacticalCombat));
            Assert.That(combat.ActionCount, Is.EqualTo(1));
            Assert.That(driver.StepCount, Is.EqualTo(1));
        }

        [Test]
        public void AutonomousNonCombatNpcUsesSameControllerPathWithCompositionRules()
        {
            CharacterId npc = CharacterId.FromStableKey("npc", "independent-market-goer");
            var perception = new MutablePerceptionSource(npc,
                new AiObservation(AiObservationKind.Site, default(CharacterId), "market-open"));
            var policy = new SemanticIntentPolicy(new[]
            {
                new SemanticIntentRule(AiObservationKind.Site, "market-open", AiIntentKind.Move, "market-square", 50, "market")
            });
            var executor = new RecordingExecutor(true);
            var controller = new CharacterAiController(npc, perception, policy, executor);

            AiIntentExecutionResult first = controller.Tick();
            AiIntentExecutionResult second = controller.Tick();

            Assert.That(first.Accepted && second.Accepted, Is.True);
            Assert.That(controller.State.Mode, Is.EqualTo(AiControlMode.Autonomous));
            Assert.That(controller.State.CurrentIntent.Kind, Is.EqualTo(AiIntentKind.Move));
            Assert.That(controller.State.CurrentIntent.TargetSemanticId, Is.EqualTo("market-square"));
            Assert.That(executor.Executed.Count, Is.EqualTo(2));
            Assert.That(executor.Executed[0], Is.EqualTo(executor.Executed[1]));
        }

        [Test]
        public void EqualPriorityRulesUseStableOrdinalTieBreak()
        {
            CharacterId npc = CharacterId.FromStableKey("npc", "deterministic");
            var snapshot = new AiPerceptionSnapshot(npc, new[]
            {
                new AiObservation(AiObservationKind.Fact, default(CharacterId), "free")
            });
            var policy = new SemanticIntentPolicy(new[]
            {
                new SemanticIntentRule(AiObservationKind.Fact, "free", AiIntentKind.Move, "destination-b", 20, "b"),
                new SemanticIntentRule(AiObservationKind.Fact, "free", AiIntentKind.Move, "destination-a", 20, "a")
            });

            AiIntent first = policy.SelectIntent(snapshot);
            AiIntent second = policy.SelectIntent(snapshot);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.TargetSemanticId, Is.EqualTo("destination-a"));
            Assert.That(first.TieBreakKey, Is.EqualTo("a"));
        }

        [Test]
        public void RejectedIntentReobservesSemanticTruthBeforeNextDecision()
        {
            CharacterId npc = CharacterId.FromStableKey("npc", "rejection");
            var perception = new MutablePerceptionSource(npc,
                new AiObservation(AiObservationKind.Fact, default(CharacterId), "path-open"));
            var policy = new SemanticIntentPolicy(new[]
            {
                new SemanticIntentRule(AiObservationKind.Fact, "path-open", AiIntentKind.Move, "well", 10, "move")
            });
            var executor = new RecordingExecutor(false);
            var controller = new CharacterAiController(npc, perception, policy, executor);

            AiIntentExecutionResult rejected = controller.Tick();
            perception.Set(new AiObservation(AiObservationKind.Fact, default(CharacterId), "path-blocked"));
            executor.Accept = true;
            AiIntentExecutionResult refreshed = controller.Tick();

            Assert.That(rejected.Accepted, Is.False);
            Assert.That(refreshed.Accepted, Is.True);
            Assert.That(perception.ObserveCount, Is.EqualTo(2));
            Assert.That(controller.State.CurrentIntent.Kind, Is.EqualTo(AiIntentKind.Idle));
        }

        [Test]
        public void SameCharacterTransitionsBetweenAutonomousAndTacticalContext()
        {
            CharacterId npc = CharacterId.FromStableKey("npc", "context-transition");
            var perception = new MutablePerceptionSource(npc,
                new AiObservation(AiObservationKind.Site, default(CharacterId), "home"));
            var policy = new ContextPolicy();
            var executor = new RecordingExecutor(true);
            var controller = new CharacterAiController(npc, perception, policy, executor);

            controller.Tick();
            Assert.That(controller.State.Actor, Is.EqualTo(npc));
            Assert.That(controller.State.Mode, Is.EqualTo(AiControlMode.Autonomous));
            Assert.That(controller.State.CurrentIntent.Kind, Is.EqualTo(AiIntentKind.Idle));

            perception.Set(new AiObservation(AiObservationKind.Combat, default(CharacterId), "encounter:ambush"));
            controller.Tick();

            Assert.That(controller.State.Actor, Is.EqualTo(npc));
            Assert.That(controller.State.Mode, Is.EqualTo(AiControlMode.Tactical));
            Assert.That(controller.State.CurrentIntent.Kind, Is.EqualTo(AiIntentKind.TacticalCombat));
        }

        [Test]
        public void DisabledControllerDoesNotInvokePerceptionOrOwner()
        {
            CharacterId npc = CharacterId.FromStableKey("npc", "disabled");
            var perception = new MutablePerceptionSource(npc);
            var executor = new RecordingExecutor(true);
            var controller = new CharacterAiController(npc, perception, new ContextPolicy(), executor);
            controller.SetEnabled(false);

            AiIntentExecutionResult result = controller.Tick();

            Assert.That(result.Accepted, Is.False);
            Assert.That(perception.ObserveCount, Is.EqualTo(0));
            Assert.That(executor.Executed.Count, Is.EqualTo(0));
            Assert.That(controller.State.Mode, Is.EqualTo(AiControlMode.Disabled));
        }

        private sealed class MutablePerceptionSource : IAiPerceptionSource
        {
            private readonly CharacterId _actor;
            private AiObservation[] _observations;

            public MutablePerceptionSource(CharacterId actor, params AiObservation[] observations)
            {
                _actor = actor;
                _observations = observations ?? new AiObservation[0];
            }

            public int ObserveCount { get; private set; }

            public void Set(params AiObservation[] observations) => _observations = observations ?? new AiObservation[0];

            public AiPerceptionSnapshot Observe(CharacterId actor)
            {
                Assert.That(actor, Is.EqualTo(_actor));
                ObserveCount++;
                return new AiPerceptionSnapshot(_actor, _observations);
            }
        }

        private sealed class RecordingExecutor : IAiIntentExecutor
        {
            public RecordingExecutor(bool accept) => Accept = accept;
            public bool Accept { get; set; }
            public List<AiIntent> Executed { get; } = new List<AiIntent>();

            public AiIntentExecutionResult TryExecute(AiIntent intent)
            {
                Executed.Add(intent);
                return Accept ? AiIntentExecutionResult.Accept() : AiIntentExecutionResult.Reject("owner rejected fixture request");
            }
        }

        private sealed class ContextPolicy : IAiIntentPolicy
        {
            public AiIntent SelectIntent(AiPerceptionSnapshot perception)
            {
                return perception.Has(AiObservationKind.Combat)
                    ? new AiIntent(perception.Actor, AiIntentKind.TacticalCombat, default(CharacterId), "active-combat", 100, "combat")
                    : new AiIntent(perception.Actor, AiIntentKind.Idle, default(CharacterId), "home", 1, "idle");
            }
        }
    }
}
