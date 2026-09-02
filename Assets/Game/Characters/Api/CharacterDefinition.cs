using System;

namespace Game.Characters.Api
{
    [Flags]
    public enum CharacterTraits
    {
        None = 0,
        PlayerControlled = 1 << 0,
        ConversationCapable = 1 << 1,
        Recruitable = 1 << 2,
        Combatant = 1 << 3
    }

    /// <summary>
    /// Stable semantic metadata for one gameplay character. Traits describe demonstrated consumer
    /// capabilities only; they do not create separate player/NPC/enemy runtime hierarchies and do
    /// not encode mutable combat team, party membership, AI intent, or presentation policy.
    /// </summary>
    public sealed class CharacterDefinition
    {
        public CharacterId Id { get; }
        public CharacterTraits Traits { get; }

        public CharacterDefinition(CharacterId id, CharacterTraits traits)
        {
            if (!id.IsValid) throw new ArgumentException("Character id is required.", nameof(id));
            Id = id;
            Traits = traits;
        }

        public bool HasTrait(CharacterTraits trait) => (Traits & trait) == trait;
    }
}
