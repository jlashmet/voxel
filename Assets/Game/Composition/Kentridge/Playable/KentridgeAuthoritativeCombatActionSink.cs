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

        public bool TryAttack(CharacterId characterId)
        {
            if (!characterId.IsValid) return false;
            if (!(_combat() is CombatService combat) || !combat.IsActive) return false;

            var actorId = new CombatParticipantId(characterId.Value);
            if (!combat.ActiveParticipant.Equals(actorId) || !combat.IsAlive(actorId)) return false;

            CombatParticipant actor = null;
            CombatParticipantId target = default;
            for (int i = 0; i < combat.ActiveParticipants.Count; i++)
            {
                CombatParticipant participant = combat.ActiveParticipants[i];
                if (participant.Id.Equals(actorId))
                {
                    actor = participant;
                    continue;
                }
                if (participant.Team != CombatTeam.Enemy || !combat.IsAlive(participant.Id)) continue;
                if (!target.IsValid || StringComparer.Ordinal.Compare(participant.Id.Value, target.Value) < 0)
                    target = participant.Id;
            }

            if (actor == null || actor.Team != CombatTeam.Player || !target.IsValid) return false;
            return combat.TryExecute(new AttackCombatantCommand(actorId, target)).Succeeded;
        }
    }
}
