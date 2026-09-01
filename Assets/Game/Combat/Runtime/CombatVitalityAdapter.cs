using System;
using Game.Combat.Api;
using Game.Vitality.Api;

namespace Game.Combat.Runtime
{
    /// <summary>
    /// Stateless translation between Combat participant identity and the actor-owned Vitality authority.
    /// Combat keeps team/turn/winner policy; this adapter owns no life state and never invents CharacterIds.
    /// </summary>
    public sealed class CombatVitalityAdapter
    {
        private readonly IVitalityService _vitality;

        public CombatVitalityAdapter(IVitalityService vitality)
        {
            _vitality = vitality ?? throw new ArgumentNullException(nameof(vitality));
        }

        public bool TryGetState(CombatParticipant participant, out VitalitySnapshot state)
        {
            if (participant == null || !participant.IsCharacterBacked)
            {
                state = default;
                return false;
            }

            return _vitality.TryGet(participant.CharacterId, out state);
        }

        public bool IsAlive(CombatParticipant participant)
        {
            return TryGetState(participant, out var state) && !state.IsDefeated;
        }

        public DamageResult ApplyDamage(CombatParticipant participant, int amount)
        {
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            if (!participant.IsCharacterBacked)
                throw new InvalidOperationException("Combat vitality requires a Character-backed participant.");

            return _vitality.ApplyDamage(new DamageRequest(participant.CharacterId, amount));
        }
    }
}
