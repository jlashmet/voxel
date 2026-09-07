using System;
using Game.Characters.Api;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Kentridge composition policy for choosing the persistent player Character used by encounter/combat.
    /// Multiplayer slot zero wins when present; single-player keeps the existing campaign character.
    /// </summary>
    public static class KentridgeCombatPlayerResolver
    {
        private static readonly CharacterBinding MultiplayerSlotZero =
            new CharacterBinding("multiplayer-slot", "0");

        public static CharacterId Resolve(ICharacterQuery characters, CharacterId singlePlayerFallback)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            if (!singlePlayerFallback.IsValid)
                throw new ArgumentException("Single-player fallback character is required.", nameof(singlePlayerFallback));

            if (characters.TryResolve(MultiplayerSlotZero, out CharacterId multiplayer) && multiplayer.IsValid)
                return multiplayer;
            return singlePlayerFallback;
        }
    }
}
