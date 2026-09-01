using System;
using System.Collections.Generic;
using Game.CharacterAI.Api;
using Game.Characters.Api;
using Game.Combat.Api;

namespace Game.CharacterAI.Adapters.Combat
{
    /// <summary>
    /// Perception adapter over Combat authority. It exposes only CharacterIds plus semantic combat facts
    /// to policy code; the combat implementation remains the tactical-domain owner.
    /// </summary>
    public sealed class CombatPerceptionSource : IAiPerceptionSource
    {
        private readonly ICombatService _combat;
        private readonly IReadOnlyDictionary<CombatParticipantId, CharacterId> _characters;

        public CombatPerceptionSource(ICombatService combat, IReadOnlyDictionary<CombatParticipantId, CharacterId> characters)
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
    /// Semantic bridge into Combat-owned tactical execution. CharacterAI does not reproduce tactical
    /// target selection or combat mutation; composition supplies the authoritative combat step.
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
        private readonly Func<bool> _stepCombat;

        public CombatTacticalIntentExecutor(Func<bool> stepCombat)
        {
            _stepCombat = stepCombat ?? throw new ArgumentNullException(nameof(stepCombat));
        }

        public AiIntentExecutionResult TryExecute(AiIntent intent)
        {
            if (intent.Kind != AiIntentKind.TacticalCombat)
                return AiIntentExecutionResult.Reject("Combat adapter accepts only TacticalCombat intent.");
            try
            {
                return _stepCombat()
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
