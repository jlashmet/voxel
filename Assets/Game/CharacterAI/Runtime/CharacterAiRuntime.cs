using System;
using System.Collections.Generic;
using Game.CharacterAI.Api;
using Game.Characters.Api;
using Game.Combat.Api;
using Game.Combat.Runtime;

namespace Game.CharacterAI.Runtime
{
    /// <summary>
    /// Generic headless AI owner. Perception and policy choose semantic intent; an owning gameplay
    /// adapter executes it and may reject it. Rejected work never mutates domain truth here: the next
    /// tick re-observes authoritative state before selecting again.
    /// </summary>
    public sealed class CharacterAiController : ICharacterAiController
    {
        private readonly IAiPerceptionSource _perception;
        private readonly IAiIntentPolicy _policy;
        private readonly IAiIntentExecutor _executor;
        private bool _enabled = true;

        public CharacterAiController(CharacterId actor, IAiPerceptionSource perception, IAiIntentPolicy policy, IAiIntentExecutor executor)
        {
            if (!actor.IsValid) throw new ArgumentException("AI actor is required.", nameof(actor));
            Actor = actor;
            _perception = perception ?? throw new ArgumentNullException(nameof(perception));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            State = DisabledState("not ticked");
        }

        public CharacterId Actor { get; }
        public AiControlState State { get; private set; }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled) State = DisabledState("disabled");
        }

        public AiIntentExecutionResult Tick()
        {
            if (!_enabled)
            {
                AiIntentExecutionResult disabled = AiIntentExecutionResult.Reject("AI control is disabled.");
                State = DisabledState(disabled.Reason);
                return disabled;
            }

            AiPerceptionSnapshot snapshot = _perception.Observe(Actor);
            if (!snapshot.Actor.Equals(Actor))
                throw new InvalidOperationException("Perception source returned a snapshot for a different character.");

            AiIntent intent = _policy.SelectIntent(snapshot);
            if (!intent.Actor.Equals(Actor))
                throw new InvalidOperationException("AI policy returned an intent owned by a different character.");

            AiIntentExecutionResult result = _executor.TryExecute(intent);
            AiControlMode mode = snapshot.Has(AiObservationKind.Combat) || intent.Kind == AiIntentKind.TacticalCombat
                ? AiControlMode.Tactical
                : AiControlMode.Autonomous;
            State = new AiControlState(Actor, true, mode, intent, result.Accepted,
                result.Accepted ? "intent accepted" : "intent rejected: " + result.Reason);
            return result;
        }

        private AiControlState DisabledState(string diagnostic)
        {
            return new AiControlState(Actor, false, AiControlMode.Disabled, default(AiIntent), false, diagnostic);
        }
    }

    /// <summary>
    /// Data-configured non-combat policy. Composition supplies semantic rules; Runtime owns only
    /// deterministic matching and stable priority/tie-break ordering.
    /// </summary>
    public sealed class SemanticIntentRule
    {
        public AiObservationKind RequiredObservationKind { get; }
        public string RequiredSemanticId { get; }
        public AiIntentKind IntentKind { get; }
        public string TargetSemanticId { get; }
        public int Priority { get; }
        public string TieBreakKey { get; }

        public SemanticIntentRule(
            AiObservationKind requiredObservationKind,
            string requiredSemanticId,
            AiIntentKind intentKind,
            string targetSemanticId,
            int priority,
            string tieBreakKey)
        {
            RequiredObservationKind = requiredObservationKind;
            RequiredSemanticId = requiredSemanticId ?? string.Empty;
            IntentKind = intentKind;
            TargetSemanticId = targetSemanticId ?? string.Empty;
            Priority = priority;
            TieBreakKey = tieBreakKey ?? string.Empty;
        }
    }

    public sealed class SemanticIntentPolicy : IAiIntentPolicy
    {
        private readonly IReadOnlyList<SemanticIntentRule> _rules;

        public SemanticIntentPolicy(IReadOnlyList<SemanticIntentRule> rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            var copy = new SemanticIntentRule[rules.Count];
            for (int i = 0; i < rules.Count; i++)
                copy[i] = rules[i] ?? throw new ArgumentException("Intent rule cannot be null.", nameof(rules));
            _rules = Array.AsReadOnly(copy);
        }

        public AiIntent SelectIntent(AiPerceptionSnapshot perception)
        {
            if (perception == null) throw new ArgumentNullException(nameof(perception));
            SemanticIntentRule best = null;
            for (int i = 0; i < _rules.Count; i++)
            {
                SemanticIntentRule candidate = _rules[i];
                string required = string.IsNullOrEmpty(candidate.RequiredSemanticId) ? null : candidate.RequiredSemanticId;
                if (!perception.Has(candidate.RequiredObservationKind, required)) continue;
                if (best == null || Compare(candidate, best) < 0) best = candidate;
            }

            if (best == null)
                return new AiIntent(perception.Actor, AiIntentKind.Idle, default(CharacterId), string.Empty, 0, "idle");

            return new AiIntent(
                perception.Actor,
                best.IntentKind,
                default(CharacterId),
                best.TargetSemanticId,
                best.Priority,
                best.TieBreakKey);
        }

        private static int Compare(SemanticIntentRule left, SemanticIntentRule right)
        {
            int priority = right.Priority.CompareTo(left.Priority);
            if (priority != 0) return priority;
            return StringComparer.Ordinal.Compare(left.TieBreakKey, right.TieBreakKey);
        }
    }

    /// <summary>
    /// Perception adapter over Combat authority. It exposes only CharacterIds plus semantic combat facts
    /// to policy code; CombatService remains the tactical-domain owner.
    /// </summary>
    public sealed class CombatPerceptionSource : IAiPerceptionSource
    {
        private readonly CombatService _combat;
        private readonly IReadOnlyDictionary<CombatParticipantId, CharacterId> _characters;

        public CombatPerceptionSource(CombatService combat, IReadOnlyDictionary<CombatParticipantId, CharacterId> characters)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
        }

        public AiPerceptionSnapshot Observe(CharacterId actor)
        {
            var observations = new List<AiObservation>();
            if (_combat.IsActive)
            {
                observations.Add(new AiObservation(
                    AiObservationKind.Combat,
                    default(CharacterId),
                    "session:" + _combat.ActiveSessionId.Value,
                    _combat.TurnNumber));

                for (int i = 0; i < _combat.ActiveParticipants.Count; i++)
                {
                    CombatParticipant participant = _combat.ActiveParticipants[i];
                    CharacterId character;
                    if (!_characters.TryGetValue(participant.Id, out character) || !character.IsValid) continue;
                    observations.Add(new AiObservation(
                        AiObservationKind.Character,
                        character,
                        "combat-participant:" + participant.Id.Value,
                        _combat.IsAlive(participant.Id) ? 1 : 0));
                }
            }
            return new AiPerceptionSnapshot(actor, observations);
        }
    }

    /// <summary>
    /// Adapter around the existing deterministic CombatAiBattleDriver. CharacterAI does not reproduce
    /// tactical target selection or combat mutation; a TacticalCombat semantic intent delegates to the
    /// existing Combat-owned mechanic, which executes through CombatService's public command boundary.
    /// </summary>
    public sealed class CombatTacticalIntentPolicy : IAiIntentPolicy
    {
        public AiIntent SelectIntent(AiPerceptionSnapshot perception)
        {
            if (perception == null) throw new ArgumentNullException(nameof(perception));
            return perception.Has(AiObservationKind.Combat)
                ? new AiIntent(perception.Actor, AiIntentKind.TacticalCombat, default(CharacterId), "active-combat", 100, "combat")
                : new AiIntent(perception.Actor, AiIntentKind.Idle, default(CharacterId), string.Empty, 0, "idle");
        }
    }

    public sealed class CombatTacticalIntentExecutor : IAiIntentExecutor
    {
        private readonly CombatAiBattleDriver _driver;

        public CombatTacticalIntentExecutor(CombatAiBattleDriver driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public AiIntentExecutionResult TryExecute(AiIntent intent)
        {
            if (intent.Kind != AiIntentKind.TacticalCombat)
                return AiIntentExecutionResult.Reject("Combat adapter accepts only TacticalCombat intent.");
            try
            {
                return _driver.Step()
                    ? AiIntentExecutionResult.Accept()
                    : AiIntentExecutionResult.Reject("Combat authority is not active.");
            }
            catch (InvalidOperationException ex)
            {
                return AiIntentExecutionResult.Reject(ex.Message);
            }
        }
    }
}
