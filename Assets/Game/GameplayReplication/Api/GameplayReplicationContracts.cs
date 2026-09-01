using System;
using System.Collections.Generic;

namespace Game.GameplayReplication.Api
{
    public readonly struct GameplayRevision : IEquatable<GameplayRevision>, IComparable<GameplayRevision>
    {
        public GameplayRevision(long value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }

        public long Value { get; }
        public bool IsInitial => Value == 0;
        public GameplayRevision Next() => new GameplayRevision(checked(Value + 1));
        public int CompareTo(GameplayRevision other) => Value.CompareTo(other.Value);
        public bool Equals(GameplayRevision other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GameplayRevision other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
        public static bool operator ==(GameplayRevision left, GameplayRevision right) => left.Equals(right);
        public static bool operator !=(GameplayRevision left, GameplayRevision right) => !left.Equals(right);
        public static bool operator <(GameplayRevision left, GameplayRevision right) => left.Value < right.Value;
        public static bool operator >(GameplayRevision left, GameplayRevision right) => left.Value > right.Value;
    }

    public readonly struct GameplayProjectionId : IEquatable<GameplayProjectionId>, IComparable<GameplayProjectionId>
    {
        public GameplayProjectionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Projection id must be non-empty.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public int CompareTo(GameplayProjectionId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(GameplayProjectionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is GameplayProjectionId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(GameplayProjectionId left, GameplayProjectionId right) => left.Equals(right);
        public static bool operator !=(GameplayProjectionId left, GameplayProjectionId right) => !left.Equals(right);
    }

    public sealed class GameplayProjectionDescriptor
    {
        public GameplayProjectionDescriptor(GameplayProjectionId id, int schemaVersion, bool requiredForGameplayReady)
        {
            if (schemaVersion <= 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            Id = id;
            SchemaVersion = schemaVersion;
            RequiredForGameplayReady = requiredForGameplayReady;
        }

        public GameplayProjectionId Id { get; }
        public int SchemaVersion { get; }
        public bool RequiredForGameplayReady { get; }
    }

    public readonly struct GameplayProjectionEntry : IEquatable<GameplayProjectionEntry>, IComparable<GameplayProjectionEntry>
    {
        public GameplayProjectionEntry(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Projection entry key must be non-empty.", nameof(key));
            Key = key;
            Value = value ?? string.Empty;
        }

        public string Key { get; }
        public string Value { get; }
        public int CompareTo(GameplayProjectionEntry other)
        {
            int key = string.CompareOrdinal(Key, other.Key);
            return key != 0 ? key : string.CompareOrdinal(Value, other.Value);
        }
        public bool Equals(GameplayProjectionEntry other) =>
            string.Equals(Key, other.Key, StringComparison.Ordinal) && string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is GameplayProjectionEntry other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(StringComparer.Ordinal.GetHashCode(Key ?? string.Empty), StringComparer.Ordinal.GetHashCode(Value ?? string.Empty));
    }

    public sealed class GameplayProjectionState
    {
        private readonly GameplayProjectionEntry[] _entries;

        public GameplayProjectionState(GameplayProjectionDescriptor descriptor, IEnumerable<GameplayProjectionEntry> entries)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            var copy = new List<GameplayProjectionEntry>(entries);
            copy.Sort();
            for (int i = 1; i < copy.Count; i++)
            {
                if (string.Equals(copy[i - 1].Key, copy[i].Key, StringComparison.Ordinal))
                    throw new ArgumentException("Projection entry keys must be unique: " + copy[i].Key, nameof(entries));
            }
            _entries = copy.ToArray();
        }

        public GameplayProjectionDescriptor Descriptor { get; }
        public IReadOnlyList<GameplayProjectionEntry> Entries => _entries;
    }

    public enum GameplayPublicationKind
    {
        Snapshot = 0,
        Delta = 1
    }

    public sealed class GameplayPublication
    {
        private readonly GameplayProjectionState[] _projections;

        public GameplayPublication(GameplayRevision revision, GameplayPublicationKind kind, IEnumerable<GameplayProjectionState> projections)
        {
            if (revision.IsInitial) throw new ArgumentException("Published gameplay revisions start at 1.", nameof(revision));
            if (projections == null) throw new ArgumentNullException(nameof(projections));
            Revision = revision;
            Kind = kind;
            var copy = new List<GameplayProjectionState>(projections);
            copy.Sort((left, right) => left.Descriptor.Id.CompareTo(right.Descriptor.Id));
            for (int i = 1; i < copy.Count; i++)
            {
                if (copy[i - 1].Descriptor.Id == copy[i].Descriptor.Id)
                    throw new ArgumentException("Projection ids must be unique within one publication: " + copy[i].Descriptor.Id, nameof(projections));
            }
            _projections = copy.ToArray();
        }

        public GameplayRevision Revision { get; }
        public GameplayPublicationKind Kind { get; }
        public IReadOnlyList<GameplayProjectionState> Projections => _projections;
    }

    public interface IGameplayProjectionSource
    {
        GameplayProjectionDescriptor Descriptor { get; }
        GameplayProjectionState Capture();
    }

    public enum GameplaySynchronizationState
    {
        Empty = 0,
        Synchronized = 1,
        RepairRequired = 2
    }

    public enum GameplayApplyResult
    {
        Applied = 0,
        DuplicateOrStale = 1,
        GapDetected = 2,
        IncompatibleProjection = 3
    }

    public interface IGameplayReplicationReadState
    {
        GameplayRevision Revision { get; }
        GameplaySynchronizationState SynchronizationState { get; }
        bool GameplayReady { get; }
        bool TryGetProjection(GameplayProjectionId id, out GameplayProjectionState state);
    }

    public interface IGameplayPublicationSink
    {
        GameplayApplyResult Apply(GameplayPublication publication);
    }
}
