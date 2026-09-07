using System;
using Game.Characters.Api;
using Game.Combat.Api;
using Game.Combat.Runtime;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Adapts one authenticated durable-character attack intent into the already composed CombatService.
    /// Target selection is deterministic and semantic; this adapter owns no vitality, encounter, turn,
    /// or participant state and cannot start a separate combat runtime.
    /// </summary>
    public sealed class KentridgeAuthoritativeCombatActionSink : IKentridgeAuthoritativeCombatActionSink
    {
        private readonly Func<ICombatService> _combat;

        public KentridgeAuthoritativeCombatActionSink(Func<ICombatService> combat)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public CharacterId LastTargetCharacterId { get; private set; }

        public bool TryAttack(CharacterId characterId)
        {
            if (!characterId.IsValid) return false;
            if (!(_combat() is CombatService combat) || !combat.IsActive) return false;

            var actorId = new CombatParticipantId(characterId.Value);
            if (!combat.ActiveParticipant.Equals(actorId) || !combat.IsAlive(actorId)) return false;

            CombatParticipant actor = null;
            CombatParticipant targetParticipant = null;
            for (int i = 0; i < combat.ActiveParticipants.Count; i++)
            {
                CombatParticipant participant = combat.ActiveParticipants[i];
                if (participant.Id.Equals(actorId))
                {
                    actor = participant;
                    continue;
                }
                if (participant.Team != CombatTeam.Enemy || !combat.IsAlive(participant.Id)) continue;
                if (targetParticipant == null ||
                    StringComparer.Ordinal.Compare(participant.Id.Value, targetParticipant.Id.Value) < 0)
                    targetParticipant = participant;
            }

            if (actor == null || actor.Team != CombatTeam.Player || targetParticipant == null) return false;
            CombatCommandResult result = combat.TryExecute(
                new AttackCombatantCommand(actorId, targetParticipant.Id));
            if (!result.Succeeded) return false;
            LastTargetCharacterId = targetParticipant.CharacterId;
            return true;
        }
    }
}
