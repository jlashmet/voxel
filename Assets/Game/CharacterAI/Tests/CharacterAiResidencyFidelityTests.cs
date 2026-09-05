using System.Collections.Generic;
using Game.CharacterAI.Api;
using Game.CharacterAI.Runtime;
using Game.Characters.Api;
using NUnit.Framework;

namespace Game.CharacterAI.Tests
{
    public sealed class CharacterAiResidencyFidelityTests
    {
        [Test]
        public void CoarseFidelityAdvancesSemanticLifeWithoutDetailedPerceptionOrExecution()
        {
            CharacterId npc = CharacterId.FromStableKey("npc", "resident-cycle");
            var perception = new CountingPerception(npc);
            var executor = new CountingExecutor();
            var coarse = new SemanticCoarseCycleSimulation(npc, new[] { "Work", "TravelHome", "AtHome" });
            var controller = new CharacterAiController(npc, perception, new IdlePolicy(), executor, coarse);

            controller.SetSimulationFidelity(AiSimulationFidelity.Coarse);
            Assert.That(controller.Tick().Accepted, Is.True);
            Assert.That(controller.Tick().Accepted, Is.True);
            Assert.That(controller.TryGetCoarseState(out AiCoarseStateSnapshot state), Is.True);
            Assert.That(state.Actor, Is.EqualTo(npc));
            Assert.That(state.SemanticState, Is.EqualTo("AtHome"));
            Assert.That(perception.ObserveCount, Is.Zero, "Coarse simulation must not run detailed perception/navigation inputs.");
            Assert.That(executor.Count, Is.Zero, "Coarse simulation must not run detailed intent execution.");

            controller.SetSimulationFidelity(AiSimulationFidelity.Detailed);
            controller.Tick();
            Assert.That(perception.ObserveCount, Is.EqualTo(1));
            Assert.That(executor.Count, Is.EqualTo(1));
        }

        private sealed class CountingPerception : IAiPerceptionSource
        {
            private readonly CharacterId _actor;
            public CountingPerception(CharacterId actor) { _actor = actor; }
            public int ObserveCount { get; private set; }
            public AiPerceptionSnapshot Observe(CharacterId actor) { ObserveCount++; return new AiPerceptionSnapshot(_actor, new AiObservation[0]); }
        }
        private sealed class IdlePolicy : IAiIntentPolicy
        {
            public AiIntent SelectIntent(AiPerceptionSnapshot perception) => new AiIntent(perception.Actor, AiIntentKind.Idle, default(CharacterId), "", 0, "idle");
        }
        private sealed class CountingExecutor : IAiIntentExecutor
        {
            public int Count { get; private set; }
            public AiIntentExecutionResult TryExecute(AiIntent intent) { Count++; return AiIntentExecutionResult.Accept(); }
        }
    }
}
