using System;

namespace MountingForce.Story
{
    public readonly struct StoryActorId : IEquatable<StoryActorId>
    {
        public string Value { get; }
        public StoryActorId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Story actor id cannot be empty.", nameof(value));
            Value = value;
        }
        public bool Equals(StoryActorId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is StoryActorId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(StoryActorId left, StoryActorId right) => left.Equals(right);
        public static bool operator !=(StoryActorId left, StoryActorId right) => !left.Equals(right);
    }

    public readonly struct StoryStagePointId : IEquatable<StoryStagePointId>
    {
        public string Value { get; }
        public StoryStagePointId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Story stage point id cannot be empty.", nameof(value));
            Value = value;
        }
        public bool Equals(StoryStagePointId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is StoryStagePointId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(StoryStagePointId left, StoryStagePointId right) => left.Equals(right);
        public static bool operator !=(StoryStagePointId left, StoryStagePointId right) => !left.Equals(right);
    }

    public readonly struct StoryCueId : IEquatable<StoryCueId>
    {
        public string Value { get; }
        public StoryCueId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Story cue id cannot be empty.", nameof(value));
            Value = value;
        }
        public bool Equals(StoryCueId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is StoryCueId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(StoryCueId left, StoryCueId right) => left.Equals(right);
        public static bool operator !=(StoryCueId left, StoryCueId right) => !left.Equals(right);
    }
}
