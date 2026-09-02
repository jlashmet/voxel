using System;
using System.Collections.Generic;

namespace Game.Progression.Api
{
    public readonly struct ObjectiveId : IEquatable<ObjectiveId>, IComparable<ObjectiveId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public ObjectiveId(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Objective id is required.", nameof(value)); Value = value; }
        public int CompareTo(ObjectiveId other) => StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(ObjectiveId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ObjectiveId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(ObjectiveId left, ObjectiveId right) => left.Equals(right);
        public static bool operator !=(ObjectiveId left, ObjectiveId right) => !left.Equals(right);
    }

    public readonly struct QuestId : IEquatable<QuestId>, IComparable<QuestId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public QuestId(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Quest id is required.", nameof(value)); Value = value; }
        public int CompareTo(QuestId other) => StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(QuestId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is QuestId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(QuestId left, QuestId right) => left.Equals(right);
        public static bool operator !=(QuestId left, QuestId right) => !left.Equals(right);
    }

    public enum ProgressionLifecycleState : byte
    {
        Inactive = 0,
        Active = 1,
        Completed = 2,
        Failed = 3
    }

    public readonly struct ObjectiveProgressSnapshot
    {
        public ObjectiveId Id { get; }
        public ProgressionLifecycleState State { get; }
        public ulong Revision { get; }
        public ObjectiveProgressSnapshot(ObjectiveId id, ProgressionLifecycleState state, ulong revision)
        {
            if (!id.IsValid) throw new ArgumentException("Objective id is required.", nameof(id));
            Id = id; State = state; Revision = revision;
        }
    }

    public sealed class QuestProgressSnapshot
    {
        public QuestId Id { get; }
        public ProgressionLifecycleState State { get; }
        public IReadOnlyList<ObjectiveProgressSnapshot> Objectives { get; }
        public ulong Revision { get; }

        public QuestProgressSnapshot(QuestId id, ProgressionLifecycleState state, IReadOnlyList<ObjectiveProgressSnapshot> objectives, ulong revision)
        {
            if (!id.IsValid) throw new ArgumentException("Quest id is required.", nameof(id));
            if (objectives == null) throw new ArgumentNullException(nameof(objectives));
            var copy = new ObjectiveProgressSnapshot[objectives.Count];
            for (int i = 0; i < objectives.Count; i++) copy[i] = objectives[i];
            Id = id; State = state; Objectives = copy; Revision = revision;
        }
    }

    public sealed class ProgressionSnapshot
    {
        public ulong Revision { get; }
        public IReadOnlyList<QuestProgressSnapshot> Quests { get; }
        public IReadOnlyList<ObjectiveProgressSnapshot> StandaloneObjectives { get; }

        public ProgressionSnapshot(ulong revision, IReadOnlyList<QuestProgressSnapshot> quests, IReadOnlyList<ObjectiveProgressSnapshot> standaloneObjectives)
        {
            if (quests == null) throw new ArgumentNullException(nameof(quests));
            if (standaloneObjectives == null) throw new ArgumentNullException(nameof(standaloneObjectives));
            var questCopy = new QuestProgressSnapshot[quests.Count];
            for (int i = 0; i < quests.Count; i++) questCopy[i] = quests[i] ?? throw new ArgumentException("Quest snapshot cannot be null.", nameof(quests));
            var objectiveCopy = new ObjectiveProgressSnapshot[standaloneObjectives.Count];
            for (int i = 0; i < standaloneObjectives.Count; i++) objectiveCopy[i] = standaloneObjectives[i];
            Revision = revision; Quests = questCopy; StandaloneObjectives = objectiveCopy;
        }
    }

    public interface IProgressionQuery
    {
        ProgressionSnapshot Snapshot();
    }
}
