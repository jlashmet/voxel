using System;
using System.Collections.Generic;

namespace Game.Quests.Api
{
    public readonly struct QuestRef : IEquatable<QuestRef>
    {
        public string Id { get; }

        public QuestRef(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Quest id is required.", nameof(id));
            Id = id;
        }

        public bool Equals(QuestRef other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is QuestRef other && Equals(other);
        public override int GetHashCode() => Id == null ? 0 : StringComparer.Ordinal.GetHashCode(Id);
        public override string ToString() => Id ?? "<unset-quest>";
        public static bool operator ==(QuestRef left, QuestRef right) => left.Equals(right);
        public static bool operator !=(QuestRef left, QuestRef right) => !left.Equals(right);
    }

    public readonly struct QuestStepRef : IEquatable<QuestStepRef>
    {
        public string Id { get; }

        public QuestStepRef(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Quest step id is required.", nameof(id));
            Id = id;
        }

        public bool Equals(QuestStepRef other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is QuestStepRef other && Equals(other);
        public override int GetHashCode() => Id == null ? 0 : StringComparer.Ordinal.GetHashCode(Id);
        public override string ToString() => Id ?? "<unset-step>";
        public static bool operator ==(QuestStepRef left, QuestStepRef right) => left.Equals(right);
        public static bool operator !=(QuestStepRef left, QuestStepRef right) => !left.Equals(right);
    }

    public interface IQuestCompletionSpec { }

    public sealed class NpcInteractionQuestCompletionSpec : IQuestCompletionSpec
    {
        public string NpcId { get; }

        internal NpcInteractionQuestCompletionSpec(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId))
                throw new ArgumentException("NPC id is required.", nameof(npcId));
            NpcId = npcId;
        }
    }

    public static class QuestCompletion
    {
        public static IQuestCompletionSpec InteractWith(string npcId) =>
            new NpcInteractionQuestCompletionSpec(npcId);
    }

    public sealed class QuestStepDefinition
    {
        public QuestStepRef Ref { get; }
        public string TargetId { get; }
        public IQuestCompletionSpec Completion { get; }

        public QuestStepDefinition(
            QuestStepRef @ref,
            string targetId,
            IQuestCompletionSpec completion)
        {
            if (string.IsNullOrWhiteSpace(targetId))
                throw new ArgumentException("Quest step target id is required.", nameof(targetId));
            Ref = @ref;
            TargetId = targetId;
            Completion = completion ?? throw new ArgumentNullException(nameof(completion));
        }
    }

    public sealed class QuestDefinition
    {
        public QuestRef Ref { get; }
        public IReadOnlyList<QuestStepDefinition> Steps { get; }

        public QuestDefinition(QuestRef @ref, IReadOnlyList<QuestStepDefinition> steps)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            if (steps.Count == 0)
                throw new ArgumentException("Quest requires at least one step.", nameof(steps));

            var copy = new QuestStepDefinition[steps.Count];
            var ids = new HashSet<QuestStepRef>();
            for (var i = 0; i < steps.Count; i++)
            {
                QuestStepDefinition step = steps[i]
                    ?? throw new ArgumentException("Quest step collection contains null at index " + i + ".", nameof(steps));
                if (!ids.Add(step.Ref))
                    throw new ArgumentException(
                        "Quest '" + @ref + "' contains duplicate step ref '" + step.Ref + "'.",
                        nameof(steps));
                copy[i] = step;
            }

            Ref = @ref;
            Steps = copy;
        }
    }

    public enum QuestStatus
    {
        Inactive = 0,
        Active = 1,
        Completed = 2,
        Failed = 3
    }

    public enum QuestStepStatus
    {
        Locked = 0,
        Active = 1,
        Completed = 2,
        Failed = 3,
        Skipped = 4
    }

    public enum QuestObservationKind
    {
        NpcInteracted = 0
    }

    /// <summary>
    /// Semantic gameplay input observed by the quest runtime. It contains stable ids only and is
    /// deliberately independent of scene objects, coordinates, and Unity types.
    /// </summary>
    public readonly struct QuestObservation
    {
        public QuestObservationKind Kind { get; }
        public string SubjectId { get; }

        private QuestObservation(QuestObservationKind kind, string subjectId)
        {
            if (string.IsNullOrWhiteSpace(subjectId))
                throw new ArgumentException("Quest observation subject id is required.", nameof(subjectId));
            Kind = kind;
            SubjectId = subjectId;
        }

        public static QuestObservation NpcInteracted(string npcId) =>
            new QuestObservation(QuestObservationKind.NpcInteracted, npcId);
    }

    public enum QuestEventKind
    {
        QuestStarted = 0,
        QuestStepActivated = 1,
        QuestStepCompleted = 2,
        QuestCompleted = 3
    }

    public readonly struct QuestEvent
    {
        public QuestEventKind Kind { get; }
        public QuestRef Quest { get; }
        public QuestStepRef Step { get; }

        private QuestEvent(QuestEventKind kind, QuestRef quest, QuestStepRef step)
        {
            Kind = kind;
            Quest = quest;
            Step = step;
        }

        public static QuestEvent Started(QuestRef quest) =>
            new QuestEvent(QuestEventKind.QuestStarted, quest, default);

        public static QuestEvent StepActivated(QuestRef quest, QuestStepRef step) =>
            new QuestEvent(QuestEventKind.QuestStepActivated, quest, step);

        public static QuestEvent StepCompleted(QuestRef quest, QuestStepRef step) =>
            new QuestEvent(QuestEventKind.QuestStepCompleted, quest, step);

        public static QuestEvent Completed(QuestRef quest) =>
            new QuestEvent(QuestEventKind.QuestCompleted, quest, default);
    }

    public readonly struct QuestStepSnapshot
    {
        public QuestStepRef Ref { get; }
        public string TargetId { get; }
        public QuestStepStatus Status { get; }

        public QuestStepSnapshot(QuestStepRef @ref, string targetId, QuestStepStatus status)
        {
            Ref = @ref;
            TargetId = targetId;
            Status = status;
        }
    }

    public sealed class QuestSnapshot
    {
        public QuestRef Ref { get; }
        public QuestStatus Status { get; }
        public IReadOnlyList<QuestStepSnapshot> Steps { get; }

        public QuestSnapshot(QuestRef @ref, QuestStatus status, QuestStepSnapshot[] steps)
        {
            Ref = @ref;
            Status = status;
            Steps = steps ?? throw new ArgumentNullException(nameof(steps));
        }
    }
}
