using System;
using Game.Characters.Api;
using Game.WorldObjects.Runtime;
using VoxelEngine.Net.Runtime.Protocol;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>Narrow semantic combat action boundary after durable character identity is resolved.</summary>
    public interface IKentridgeAuthoritativeCombatActionSink
    {
        bool TryAttack(CharacterId characterId);
    }

    /// <summary>
    /// Production semantic command adapter after Sessions has resolved authenticated network input
    /// to a durable character. It delegates world use to canonical WorldObjects and optional combat
    /// use to the composed authoritative Combat owner; it owns no transport/session identity.
    /// </summary>
    public sealed class KentridgeAuthoritativeGameplayCommandSink : IKentridgeAuthoritativePlayerCommandSink
    {
        private readonly InteractionClickedProcessor _worldInteractions;
        private readonly IKentridgeAuthoritativeCombatActionSink _combatActions;

        public KentridgeAuthoritativeGameplayCommandSink(
            InteractionClickedProcessor worldInteractions,
            IKentridgeAuthoritativeCombatActionSink combatActions = null)
        {
            _worldInteractions = worldInteractions ?? throw new ArgumentNullException(nameof(worldInteractions));
            _combatActions = combatActions;
        }

        public long AppliedCombatActions { get; private set; }

        public void Apply(CharacterId characterId, in C_PlayerInput input, uint serverTick)
        {
            C_PlayerInput.ActionBits actions = (C_PlayerInput.ActionBits)input.actions;
            if ((actions & C_PlayerInput.ActionBits.UseMain) != 0)
                _worldInteractions.Process(characterId);
            if ((actions & C_PlayerInput.ActionBits.UseAlt) != 0 &&
                _combatActions != null &&
                _combatActions.TryAttack(characterId))
                AppliedCombatActions++;
        }
    }
}
