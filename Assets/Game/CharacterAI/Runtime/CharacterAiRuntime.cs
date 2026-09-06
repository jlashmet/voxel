using System;
using System.Collections.Generic;
using Game.CharacterAI.Api;
using Game.Characters.Api;

namespace Game.CharacterAI.Runtime
{
    /// <summary>
    /// Generic headless AI owner. Detailed ticks use perception/policy/executor; Coarse ticks use only
    /// an optional semantic coarse simulation and therefore cannot accidentally perform detailed
    /// perception/navigation work.
    /// </summary>
    public sealed class CharacterAiController : ICharacterAiController, ICharacterAiSimulationFidelity
    {
        private readonly IAiPerceptionSource _perception;
        private readonly IAiIntentPolicy _policy;
        private readonly IAiIntentExecutor _executor;
        private readonly IAiCoarseSimulation _coarseSimulation;
        private bool _enabled = true;
        private AiSimulationFidelity _simulationFidelity = AiSimulationFidelity.Detailed;

        public CharacterAiController(CharacterId actor, IAiPerceptionSource perception, IAiIntentPolicy policy, IAiIntentExecutor executor, IAiCoarseSimulation coarseSimulation = null)
        {
            if (!actor.IsValid) throw new ArgumentException("AI actor is required.", nameof(actor));
            Actor = actor;
            _perception = perception ?? throw new ArgumentNullException(nameof(perception));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            if (coarseSimulation != null && coarseSimulation.Actor != actor) throw new ArgumentException("Coarse simulation belongs to a different actor.", nameof(coarseSimulation));
            _coarseSimulation = coarseSimulation;
            State = DisabledState("not ticked");
        }

        public CharacterId Actor { get; }
        public AiControlState State { get; private set; }
        public AiSimulationFidelity SimulationFidelity => _simulationFidelity;

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled) State = DisabledState("disabled");
        }

        public void SetSimulationFidelity(AiSimulationFidelity fidelity)
        {
            _simulationFidelity = fidelity;
            if (fidelity == AiSimulationFidelity.Dormant)
                State = DisabledState("simulation dormant");
            else if (fidelity == AiSimulationFidelity.Coarse)
                State = new AiControlState(Actor, _enabled, _enabled ? AiControlMode.Autonomous : AiControlMode.Disabled, default(AiIntent), false, CoarseDiagnostic());
        }

        public bool TryGetCoarseState(out AiCoarseStateSnapshot state)
        {
            if (_coarseSimulation == null) { state = default; return false; }
            state = _coarseSimulation.State; return true;
        }

        public AiIntentExecutionResult Tick()
        {
            if (!_enabled)
            {
                AiIntentExecutionResult disabled = AiIntentExecutionResult.Reject("AI control is disabled.");
                State = DisabledState(disabled.Reason); return disabled;
            }
            if (_simulationFidelity == AiSimulationFidelity.Dormant)
            {
                AiIntentExecutionResult dormant = AiIntentExecutionResult.Reject("AI simulation is dormant.");
                State = DisabledState(dormant.Reason); return dormant;
            }
            if (_simulationFidelity == AiSimulationFidelity.Coarse)
            {
                if (_coarseSimulation == null)
                {
                    AiIntentExecutionResult unavailable = AiIntentExecutionResult.Reject("Coarse semantic simulation is unavailable for this actor.");
                    State = new AiControlState(Actor, true, AiControlMode.Autonomous, default(AiIntent), false, unavailable.Reason); return unavailable;
                }
                bool advanced = _coarseSimulation.Advance();
                State = new AiControlState(Actor, true, AiControlMode.Autonomous, default(AiIntent), true, (advanced ? "coarse advanced: " : "coarse stable: ") + _coarseSimulation.State.SemanticState);
                return AiIntentExecutionResult.Accept();
            }

            AiPerceptionSnapshot snapshot = _perception.Observe(Actor);
            if (!snapshot.Actor.Equals(Actor)) throw new InvalidOperationException("Perception source returned a snapshot for a different character.");
            AiIntent intent = _policy.SelectIntent(snapshot);
            if (!intent.Actor.Equals(Actor)) throw new InvalidOperationException("AI policy returned an intent owned by a different character.");
            AiIntentExecutionResult result = _executor.TryExecute(intent);
            AiControlMode mode = snapshot.Has(AiObservationKind.Combat) || intent.Kind == AiIntentKind.TacticalCombat ? AiControlMode.Tactical : AiControlMode.Autonomous;
            State = new AiControlState(Actor, true, mode, intent, result.Accepted, result.Accepted ? "intent accepted" : "intent rejected: " + result.Reason);
            return result;
        }

        private string CoarseDiagnostic() => _coarseSimulation == null ? "coarse simulation unavailable" : "coarse: " + _coarseSimulation.State.SemanticState;
        private AiControlState DisabledState(string diagnostic) => new AiControlState(Actor, false, AiControlMode.Disabled, default(AiIntent), false, diagnostic);
    }

    /// <summary>Deterministic data-configured coarse life cycle; composition supplies semantic state names/order.</summary>
    public sealed class SemanticCoarseCycleSimulation : IAiCoarseSimulation
    {
        private readonly string[] _states;
        private int _index;
        private ulong _revision = 1;
        public SemanticCoarseCycleSimulation(CharacterId actor, IReadOnlyList<string> states)
        {
            if (!actor.IsValid) throw new ArgumentException("AI actor is required.", nameof(actor));
            if (states == null || states.Count == 0) throw new ArgumentException("At least one semantic coarse state is required.", nameof(states));
            Actor = actor; _states = new string[states.Count];
            for (int i = 0; i < states.Count; i++) { if (string.IsNullOrWhiteSpace(states[i])) throw new ArgumentException("Coarse state cannot be empty.", nameof(states)); _states[i] = states[i].Trim(); }
        }
        public CharacterId Actor { get; }
        public AiCoarseStateSnapshot State => new AiCoarseStateSnapshot(Actor, _states[_index], _revision);
        public bool Advance()
        {
            if (_index >= _states.Length - 1) return false;
            _index++; _revision++; return true;
        }
    }

    public sealed class SemanticIntentRule
    {
        public AiObservationKind RequiredObservationKind { get; }
        public string RequiredSemanticId { get; }
        public AiIntentKind IntentKind { get; }
        public string TargetSemanticId { get; }
        public int Priority { get; }
        public string TieBreakKey { get; }
        public SemanticIntentRule(AiObservationKind requiredObservationKind, string requiredSemanticId, AiIntentKind intentKind, string targetSemanticId, int priority, string tieBreakKey)
        { RequiredObservationKind = requiredObservationKind; RequiredSemanticId = requiredSemanticId ?? string.Empty; IntentKind = intentKind; TargetSemanticId = targetSemanticId ?? string.Empty; Priority = priority; TieBreakKey = tieBreakKey ?? string.Empty; }
    }

    public sealed class SemanticIntentPolicy : IAiIntentPolicy
    {
        private readonly IReadOnlyList<SemanticIntentRule> _rules;
        public SemanticIntentPolicy(IReadOnlyList<SemanticIntentRule> rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules)); var copy = new SemanticIntentRule[rules.Count];
            for (int i = 0; i < rules.Count; i++) copy[i] = rules[i] ?? throw new ArgumentException("Intent rule cannot be null.", nameof(rules)); _rules = Array.AsReadOnly(copy);
        }
        public AiIntent SelectIntent(AiPerceptionSnapshot perception)
        {
            if (perception == null) throw new ArgumentNullException(nameof(perception)); SemanticIntentRule best = null;
            for (int i = 0; i < _rules.Count; i++) { SemanticIntentRule candidate = _rules[i]; string required = string.IsNullOrEmpty(candidate.RequiredSemanticId) ? null : candidate.RequiredSemanticId; if (!perception.Has(candidate.RequiredObservationKind, required)) continue; if (best == null || Compare(candidate, best) < 0) best = candidate; }
            if (best == null) return new AiIntent(perception.Actor, AiIntentKind.Idle, default(CharacterId), string.Empty, 0, "idle");
            return new AiIntent(perception.Actor, best.IntentKind, default(CharacterId), best.TargetSemanticId, best.Priority, best.TieBreakKey);
        }
        private static int Compare(SemanticIntentRule left, SemanticIntentRule right) { int priority = right.Priority.CompareTo(left.Priority); return priority != 0 ? priority : StringComparer.Ordinal.Compare(left.TieBreakKey, right.TieBreakKey); }
    }
}
