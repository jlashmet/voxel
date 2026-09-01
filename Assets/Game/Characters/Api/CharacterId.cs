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
