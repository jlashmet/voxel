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
        public int CurrentCount { get; }
        public int RequiredCount { get; }
        public ulong Revision { get; }

        // Compatibility constructor retained for System 06 and existing projection fixtures.
        public ObjectiveProgressSnapshot(ObjectiveId id, ProgressionLifecycleState state, ulong revision)
            : this(id, state, 0, 0, revision) { }

        public ObjectiveProgressSnapshot(
            ObjectiveId id,
            ProgressionLifecycleState state,
            int currentCount,
            int requiredCount,
            ulong revision)
        {
            if (!id.IsValid) throw new ArgumentException("Objective id is required.", nameof(id));
            if (currentCount < 0) throw new ArgumentOutOfRangeException(nameof(currentCount));
            if (requiredCount < 0) throw new ArgumentOutOfRangeException(nameof(requiredCount));
            if (requiredCount > 0 && currentCount > requiredCount)
                throw new ArgumentException("Objective count cannot exceed its required count.", nameof(currentCount));
            Id = id;
            State = state;
            CurrentCount = currentCount;
            RequiredCount = requiredCount;
            Revision = revision;
        }
    }

    public sealed class QuestStepProgressSnapshot
    {
        private readonly ObjectiveProgressSnapshot[] _objectives;

        public string StepId { get; }
        public ProgressionLifecycleState State { get; }
        public IReadOnlyList<ObjectiveProgressSnapshot> Objectives => _objectives;

        public QuestStepProgressSnapshot(
            string stepId,
            ProgressionLifecycleState state,
            IReadOnlyList<ObjectiveProgressSnapshot> objectives)
        {
            if (string.IsNullOrWhiteSpace(stepId)) throw new ArgumentException("Quest step id is required.", nameof(stepId));
            if (objectives == null) throw new ArgumentNullException(nameof(objectives));
            _objectives = new ObjectiveProgressSnapshot[objectives.Count];
            for (var i = 0; i < objectives.Count; i++) _objectives[i] = objectives[i];
            StepId = stepId;
            State = state;
        }
    }

    public sealed class QuestProgressSnapshot
    {
        private readonly ObjectiveProgressSnapshot[] _objectives;
        private readonly QuestStepProgressSnapshot[] _steps;

        public QuestId Id { get; }
        public ProgressionLifecycleState State { get; }
        public IReadOnlyList<ObjectiveProgressSnapshot> Objectives => _objectives;
        public IReadOnlyList<QuestStepProgressSnapshot> Steps => _steps;
        public string ActiveStepId { get; }
        public ulong Revision { get; }

        // Compatibility constructor retained for existing replication fixtures.
        public QuestProgressSnapshot(
            QuestId id,
            ProgressionLifecycleState state,
            IReadOnlyList<ObjectiveProgressSnapshot> objectives,
            ulong revision)
        {
            if (!id.IsValid) throw new ArgumentException("Quest id is required.", nameof(id));
            if (objectives == null) throw new ArgumentNullException(nameof(objectives));
            _objectives = CopyObjectives(objectives);
            _steps = Array.Empty<QuestStepProgressSnapshot>();
            Id = id;
            State = state;
            ActiveStepId = string.Empty;
            Revision = revision;
        }

        public QuestProgressSnapshot(
            QuestId id,
            ProgressionLifecycleState state,
            string activeStepId,
            IReadOnlyList<QuestStepProgressSnapshot> steps,
            ulong revision)
        {
            if (!id.IsValid) throw new ArgumentException("Quest id is required.", nameof(id));
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            _steps = new QuestStepProgressSnapshot[steps.Count];
            var flattened = new List<ObjectiveProgressSnapshot>();
            for (var i = 0; i < steps.Count; i++)
            {
                QuestStepProgressSnapshot step = steps[i] ?? throw new ArgumentException("Quest step snapshot cannot be null.", nameof(steps));
                _steps[i] = step;
                for (var o = 0; o < step.Objectives.Count; o++) flattened.Add(step.Objectives[o]);
            }
            _objectives = flattened.ToArray();
            Id = id;
            State = state;
            ActiveStepId = activeStepId ?? string.Empty;
            Revision = revision;
        }

        private static ObjectiveProgressSnapshot[] CopyObjectives(IReadOnlyList<ObjectiveProgressSnapshot> objectives)
        {
            var copy = new ObjectiveProgressSnapshot[objectives.Count];
            for (var i = 0; i < objectives.Count; i++) copy[i] = objectives[i];
            return copy;
        }
    }

    public sealed class ProgressionSnapshot
    {
        private readonly QuestProgressSnapshot[] _quests;
        private readonly ObjectiveProgressSnapshot[] _standaloneObjectives;
        private readonly string[] _appliedOperationIds;

        public ulong Revision { get; }
        public IReadOnlyList<QuestProgressSnapshot> Quests => _quests;
        public IReadOnlyList<ObjectiveProgressSnapshot> StandaloneObjectives => _standaloneObjectives;
        public IReadOnlyList<string> AppliedOperationIds => _appliedOperationIds;
        public long CompatibilitySequence { get; }

        // Compatibility constructor retained for System 06 callers that only project state.
        public ProgressionSnapshot(
            ulong revision,
            IReadOnlyList<QuestProgressSnapshot> quests,
            IReadOnlyList<ObjectiveProgressSnapshot> standaloneObjectives)
            : this(revision, quests, standaloneObjectives, Array.Empty<string>(), 0) { }

        public ProgressionSnapshot(
            ulong revision,
            IReadOnlyList<QuestProgressSnapshot> quests,
            IReadOnlyList<ObjectiveProgressSnapshot> standaloneObjectives,
            IReadOnlyList<string> appliedOperationIds,
            long compatibilitySequence)
        {
            if (quests == null) throw new ArgumentNullException(nameof(quests));
            if (standaloneObjectives == null) throw new ArgumentNullException(nameof(standaloneObjectives));
            if (appliedOperationIds == null) throw new ArgumentNullException(nameof(appliedOperationIds));
            if (compatibilitySequence < 0) throw new ArgumentOutOfRangeException(nameof(compatibilitySequence));

            _quests = new QuestProgressSnapshot[quests.Count];
            for (var i = 0; i < quests.Count; i++)
                _quests[i] = quests[i] ?? throw new ArgumentException("Quest snapshot cannot be null.", nameof(quests));

            _standaloneObjectives = new ObjectiveProgressSnapshot[standaloneObjectives.Count];
            for (var i = 0; i < standaloneObjectives.Count; i++) _standaloneObjectives[i] = standaloneObjectives[i];

            _appliedOperationIds = new string[appliedOperationIds.Count];
            for (var i = 0; i < appliedOperationIds.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(appliedOperationIds[i]))
                    throw new ArgumentException("Applied operation id cannot be empty.", nameof(appliedOperationIds));
                _appliedOperationIds[i] = appliedOperationIds[i];
            }

            Revision = revision;
            CompatibilitySequence = compatibilitySequence;
        }
    }

    public interface IProgressionQuery
    {
        ProgressionSnapshot Snapshot();
    }
}
