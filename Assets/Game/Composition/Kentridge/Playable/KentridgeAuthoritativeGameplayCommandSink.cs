using System;
using Game.Characters.Api;
using Game.WorldObjects.Runtime;
using VoxelEngine.Net.Runtime.Protocol;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Production semantic command adapter after Sessions has resolved authenticated network input
    /// to a durable character. It delegates world use to the canonical WorldObjects interaction
    /// processor; it owns no transport/session identity and exposes no validation-only mutation seam.
    /// </summary>
    public sealed class KentridgeAuthoritativeGameplayCommandSink : IKentridgeAuthoritativePlayerCommandSink
    {
        private readonly InteractionClickedProcessor _worldInteractions;

        public KentridgeAuthoritativeGameplayCommandSink(InteractionClickedProcessor worldInteractions)
        {
            _worldInteractions = worldInteractions ?? throw new ArgumentNullException(nameof(worldInteractions));
        }

        public void Apply(CharacterId characterId, in C_PlayerInput input, uint serverTick)
        {
            C_PlayerInput.ActionBits actions = (C_PlayerInput.ActionBits)input.actions;
            if ((actions & C_PlayerInput.ActionBits.UseMain) != 0)
                _worldInteractions.Process(characterId);
        }
    }
}
