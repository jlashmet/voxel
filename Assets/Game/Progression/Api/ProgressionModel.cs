using System;
using System.Collections.Generic;

namespace Game.Progression.Api
{
    public enum ProgressionObservationKind : byte
    {
        NpcInteracted = 0,
        Interacted = 1,
        SiteEntered = 2
    }

    public readonly struct ProgressionObservation
    {
        public ProgressionObservationKind Kind { get; }
        public string SubjectId { get; }

        private ProgressionObservation(ProgressionObservationKind kind, string subjectId)
        {
            Kind = kind;
            SubjectId = RequireId(subjectId, nameof(subjectId));
        }

        public static ProgressionObservation NpcInteracted(string npcId) =>
            new ProgressionObservation(ProgressionObservationKind.NpcInteracted, npcId);

        public static ProgressionObservation Interacted(string subjectId) =>
            new ProgressionObservation(ProgressionObservationKind.Interacted, subjectId);

        public static ProgressionObservation SiteEntered(string siteId) =>
            new ProgressionObservation(ProgressionObservationKind.SiteEntered, siteId);

        private static string RequireId(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Stable semantic id is required.", paramName);
            return value;
        }
    }

    public readonly struct ObjectiveCompletionDefinition
    {
        public ProgressionObservationKind ObservationKind { get; }
        public string SubjectId { get; }

        public ObjectiveCompletionDefinition(ProgressionObservationKind observationKind, string subjectId)
        {
            if (string.IsNullOrWhiteSpace(subjectId)) throw new ArgumentException("Stable semantic id is required.", nameof(subjectId));
            ObservationKind = observationKind;
            SubjectId = subjectId;
        }

        public bool Matches(ProgressionObservation observation) =>
            observation.Kind == ObservationKind && string.Equals(observation.SubjectId, SubjectId, StringComparison.Ordinal);
    }

    public sealed class ObjectiveDefinition
    {
        public ObjectiveId Id { get; }
        public ObjectiveCompletionDefinition Completion { get; }

        public ObjectiveDefinition(ObjectiveId id, ObjectiveCompletionDefinition completion)
        {
            if (!id.IsValid) throw new ArgumentException("Objective id is required.", nameof(id));
            Id = id;
            Completion = completion;
        }
    }

    public sealed class QuestDefinition
    {
        public QuestId Id { get; }
        public IReadOnlyList<ObjectiveDefinition> Objectives { get; }

        public QuestDefinition(QuestId id, IReadOnlyList<ObjectiveDefinition> objectives)
        {
            if (!id.IsValid) throw new ArgumentException("Quest id is required.", nameof(id));
            if (objectives == null) throw new ArgumentNullException(nameof(objectives));
            if (objectives.Count == 0) throw new ArgumentException("Quest requires at least one objective.", nameof(objectives));

            var copy = new ObjectiveDefinition[objectives.Count];
            var ids = new HashSet<ObjectiveId>();
            for (var i = 0; i < objectives.Count; i++)
            {
                ObjectiveDefinition objective = objectives[i] ?? throw new ArgumentException("Quest objective cannot be null.", nameof(objectives));
                if (!ids.Add(objective.Id)) throw new ArgumentException("Quest objective ids must be unique.", nameof(objectives));
                copy[i] = objective;
            }

            Id = id;
            Objectives = copy;
        }
    }

    public enum ProgressionEventKind : byte
    {
        QuestActivated = 0,
        ObjectiveActivated = 1,
        ObjectiveCompleted = 2,
        QuestCompleted = 3,
        StandaloneObjectiveActivated = 4,
        StandaloneObjectiveCompleted = 5
    }

    public readonly struct ProgressionEvent
    {
        public ProgressionEventKind Kind { get; }
        public QuestId Quest { get; }
        public ObjectiveId Objective { get; }
        public ulong Revision { get; }

        public ProgressionEvent(ProgressionEventKind kind, QuestId quest, ObjectiveId objective, ulong revision)
        {
            Kind = kind;
            Quest = quest;
            Objective = objective;
            Revision = revision;
        }
    }

    public interface IProgressionRuntime : IProgressionQuery
    {
        IReadOnlyList<ProgressionEvent> ActivateQuest(QuestId quest);
        IReadOnlyList<ProgressionEvent> ActivateStandaloneObjective(ObjectiveId objective);
        IReadOnlyList<ProgressionEvent> Observe(ProgressionObservation observation);
        void Restore(ProgressionSnapshot snapshot);
        bool IsQuestActive(QuestId quest);
        bool IsQuestCompleted(QuestId quest);
        bool IsObjectiveActive(ObjectiveId objective);
        bool IsObjectiveCompleted(ObjectiveId objective);
    }
}
