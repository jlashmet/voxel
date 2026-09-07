using System;
using System.Globalization;
using Game.Characters.Api;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Production Kentridge composition for durable multiplayer player characters. Sessions owns
    /// membership/slot identity; Characters remains the authoritative gameplay identity/state store.
    /// This adapter only ensures that the deterministic CharacterId assigned by session admission is
    /// backed by one real Characters entry before PartySession binds the durable party-member key.
    /// Initial placement is composition policy so shipped worlds and focused validation consumers can
    /// choose spawn layout without changing durable identity or bypassing Characters authority.
    /// </summary>
    public sealed class KentridgeMultiplayerCharacterRoster
    {
        private const string CharacterPrefix = "kentridge-player-";
        private const string SlotBindingScope = "multiplayer-slot";
        private const string CombatBindingScope = "combat-participant";

        private readonly ICharacterRegistry _characters;
        private readonly Func<int, CharacterVector3> _initialPosition;

        public KentridgeMultiplayerCharacterRoster(
            ICharacterRegistry characters,
            Func<int, CharacterVector3> initialPosition = null)
        {
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _initialPosition = initialPosition ?? DefaultInitialPosition;
        }

        public ICharacterRegistry Characters => _characters;

        public void EnsureCapacity(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Multiplayer capacity must be positive.");

            for (int slot = 0; slot < capacity; slot++)
                EnsureSlot(slot);
        }

        public CharacterId EnsureSlot(int slot)
        {
            CharacterId id = CharacterIdForSlot(slot);
            if (_characters.TryGet(id, out CharacterSnapshot existing))
            {
                CharacterTraits required = CharacterTraits.PlayerControlled | CharacterTraits.Combatant;
                if (!existing.Definition.HasTrait(required))
                    throw new InvalidOperationException(
                        "Existing multiplayer character does not provide required player/combat traits: " + id.Value);
            }
            else
            {
                var definition = new CharacterDefinition(
                    id,
                    CharacterTraits.PlayerControlled | CharacterTraits.Combatant);
                var initial = new CharacterKinematicState(
                    _initialPosition(slot),
                    new CharacterVector3(0f, 0f, 0f),
                    new CharacterVector3(0f, 0f, 1f));
                CharacterRegistryFailure created = _characters.Create(definition, initial, out _);
                if (created != CharacterRegistryFailure.None)
                    throw new InvalidOperationException(
                        "Failed to create multiplayer character " + id.Value + ": " + created);
            }

            BindRequired(id, new CharacterBinding(
                SlotBindingScope,
                slot.ToString(CultureInfo.InvariantCulture)));
            BindRequired(id, new CharacterBinding(CombatBindingScope, id.Value));
            return id;
        }

        public static CharacterId CharacterIdForSlot(int slot)
        {
            if (slot < 0)
                throw new ArgumentOutOfRangeException(nameof(slot), "Player slot cannot be negative.");
            return new CharacterId(CharacterPrefix + (slot + 1).ToString(CultureInfo.InvariantCulture));
        }

        private static CharacterVector3 DefaultInitialPosition(int slot) =>
            new CharacterVector3(slot, 0f, 0f);

        private void BindRequired(CharacterId id, CharacterBinding binding)
        {
            CharacterRegistryFailure failure = _characters.Bind(id, binding);
            if (failure != CharacterRegistryFailure.None)
                throw new InvalidOperationException(
                    "Failed to bind multiplayer character " + id.Value + " to " + binding + ": " + failure);
        }
    }
}
