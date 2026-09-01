using System;
using System.Collections.Generic;
using Game.CharacterAI.Api;
using Game.Characters.Api;
using Game.Combat.Api;
using Game.Combat.Runtime;

namespace Game.CharacterAI.Adapters.Combat
{
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
    /// Semantic bridge into the existing deterministic CombatAiBattleDriver. CharacterAI does not
    /// reproduce tactical target selection or combat mutation; the existing Combat-owned mechanic
    /// remains authoritative and executes through CombatService's command boundary.
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
