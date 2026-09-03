using System;
using Game.Characters.Api;

namespace Game.WorldObjects.Api
{
    public readonly struct WorldObjectId : IEquatable<WorldObjectId>, IComparable<WorldObjectId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public WorldObjectId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("World object id is required.", nameof(value));
            Value = value;
        }

        public int CompareTo(WorldObjectId other) =>
            StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);

        public bool Equals(WorldObjectId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is WorldObjectId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldObjectId left, WorldObjectId right) => left.Equals(right);
        public static bool operator !=(WorldObjectId left, WorldObjectId right) => !left.Equals(right);
    }

    public enum WorldInteractionFailure
    {
        None = 0,
        UnknownActor = 1,
        UnknownObject = 2,
        OutOfRange = 3,
        NotPermitted = 4,
        InvalidState = 5,
        UnsupportedCapability = 6
    }

    public readonly struct WorldInteractionResult
    {
        public bool Succeeded { get; }
        public WorldInteractionFailure Failure { get; }

        private WorldInteractionResult(bool succeeded, WorldInteractionFailure failure)
        {
            Succeeded = succeeded;
            Failure = failure;
        }

        public static WorldInteractionResult Success() =>
            new WorldInteractionResult(true, WorldInteractionFailure.None);

        public static WorldInteractionResult Reject(WorldInteractionFailure failure)
        {
            if (failure == WorldInteractionFailure.None)
                throw new ArgumentException("A rejection requires a failure reason.", nameof(failure));
            return new WorldInteractionResult(false, failure);
        }
    }

    public interface IWorldInteractionValidator
    {
        WorldInteractionResult Validate(CharacterId actorId, WorldObjectId objectId);
    }
}
