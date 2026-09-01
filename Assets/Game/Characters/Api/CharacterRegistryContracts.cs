using System;
using System.Collections.Generic;

namespace Game.Characters.Api
{
    public readonly struct CharacterBinding : IEquatable<CharacterBinding>, IComparable<CharacterBinding>
    {
        public string Scope { get; }
        public string Key { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Scope) && !string.IsNullOrWhiteSpace(Key);

        public CharacterBinding(string scope, string key)
        {
            if (string.IsNullOrWhiteSpace(scope)) throw new ArgumentException("Binding scope is required.", nameof(scope));
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Binding key is required.", nameof(key));
            Scope = scope.Trim().ToLowerInvariant();
            Key = key;
        }

        public int CompareTo(CharacterBinding other)
        {
            int scope = StringComparer.Ordinal.Compare(Scope ?? string.Empty, other.Scope ?? string.Empty);
            return scope != 0 ? scope : StringComparer.Ordinal.Compare(Key ?? string.Empty, other.Key ?? string.Empty);
        }

        public bool Equals(CharacterBinding other) =>
            string.Equals(Scope, other.Scope, StringComparison.Ordinal) &&
            string.Equals(Key, other.Key, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CharacterBinding other && Equals(other);
        public override int GetHashCode() => ((Scope == null ? 0 : StringComparer.Ordinal.GetHashCode(Scope)) * 397) ^
                                             (Key == null ? 0 : StringComparer.Ordinal.GetHashCode(Key));
        public override string ToString() => IsValid ? Scope + ":" + Key : string.Empty;
    }

    public enum CharacterRegistryFailure
    {
        None = 0,
        DuplicateCharacterId = 1,
        RetiredCharacterId = 2,
        UnknownCharacterId = 3,
        DuplicateBinding = 4,
        UnknownBinding = 5,
        CharacterAlreadyDefeated = 6,
        RegistryNotEmpty = 7,
        InvalidState = 8
    }

    public enum CharacterEventKind
    {
        Created = 0,
        BindingAdded = 1,
        KinematicsChanged = 2,
        Defeated = 3,
        Removed = 4
    }

    public readonly struct CharacterEvent
    {
        public ulong Sequence { get; }
        public CharacterEventKind Kind { get; }
        public CharacterId CharacterId { get; }
        public CharacterBinding Binding { get; }

        public CharacterEvent(ulong sequence, CharacterEventKind kind, CharacterId characterId, CharacterBinding binding = default)
        {
            Sequence = sequence;
            Kind = kind;
            CharacterId = characterId;
            Binding = binding;
        }
    }

    /// <summary>
    /// Read-only consumer seam for gameplay identity/state. Systems such as AI, encounters,
    /// sessions, replication, inventory, cutscenes and presentation can resolve/query characters
    /// without receiving lifecycle mutation authority.
    /// </summary>
    public interface ICharacterQuery
    {
        IReadOnlyList<CharacterSnapshot> GetAll();
        bool TryGet(CharacterId id, out CharacterSnapshot snapshot);
        bool TryResolve(CharacterBinding binding, out CharacterId id);
    }

    public interface ICharacterRegistry : ICharacterQuery
    {
        event Action<CharacterEvent> Changed;

        CharacterRegistryFailure Create(CharacterDefinition definition, CharacterKinematicState initialState, out CharacterSnapshot snapshot);
        CharacterRegistryFailure Bind(CharacterId id, CharacterBinding binding);
        CharacterRegistryFailure UpdateKinematics(CharacterId id, CharacterKinematicState state, out CharacterSnapshot snapshot);
        CharacterRegistryFailure MarkDefeated(CharacterId id, out CharacterSnapshot snapshot);
        CharacterRegistryFailure Remove(CharacterId id);
    }
}
