using System;

namespace Game.Characters.Api
{
    /// <summary>
    /// Stable, serialization-safe gameplay character identity. The value is intentionally opaque to
    /// Characters; composition maps authored/session/combat identities to one canonical value.
    /// Equality is ordinal and serialization round-trips <see cref="Value"/> exactly.
    /// </summary>
    public readonly struct CharacterId : IEquatable<CharacterId>, IComparable<CharacterId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public CharacterId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Character id is required.", nameof(value));
            Value = value;
        }

        /// <summary>
        /// Canonical migration helper for existing semantic identity sources. Composition owns the
        /// scope/key choice (for example scope "npc" with an authored NpcRef id); Characters only
        /// guarantees a stable lower-case scope plus exact key in the serialized "scope:key" value.
        /// </summary>
        public static CharacterId FromStableKey(string scope, string key)
        {
            if (string.IsNullOrWhiteSpace(scope))
                throw new ArgumentException("Character identity scope is required.", nameof(scope));
            if (scope.IndexOf(':') >= 0)
                throw new ArgumentException("Character identity scope cannot contain ':'.", nameof(scope));
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Character identity key is required.", nameof(key));

            return new CharacterId(scope.Trim().ToLowerInvariant() + ":" + key);
        }

        public int CompareTo(CharacterId other) =>
            StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);

        public bool Equals(CharacterId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is CharacterId other && Equals(other);

        public override int GetHashCode() =>
            Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(CharacterId left, CharacterId right) => left.Equals(right);
        public static bool operator !=(CharacterId left, CharacterId right) => !left.Equals(right);
    }
}
