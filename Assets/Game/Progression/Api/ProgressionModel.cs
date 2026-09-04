using System;
using System.Collections.Generic;

namespace Game.Progression.Api
{
    public enum ProgressionEntryKind
    {
        Quest = 0,
        StandaloneObjective = 1
    }

    public enum ProgressionConditionKind
    {
        NpcInteraction = 0,
        Interaction = 1
    }

    public enum ProgressionSignalKind
    {
        NpcInteracted = 0,
        Interacted = 1
    }

    public enum ProgressionTransitionKind
    {
        EntryStarted = 0,
        NodeActivated = 1,
        ObjectiveProgressed = 2,
        NodeCompleted = 3,
        EntryCompleted = 4
    }

    public enum ProgressionApplyStatus
    {
        Applied = 0,
        Replay = 1,
        Rejected = 2
    }

    public readonly struct ProgressionCondition
    {
        private ProgressionCondition(ProgressionConditionKind kind, string subjectId)
        {
            if (string.IsNullOrWhiteSpace(subjectId))
                throw new ArgumentException("Progression condition subject id is required.", nameof(subjectId));
            Kind = kind;
            SubjectId = subjectId;
        }

        public ProgressionConditionKind Kind { get; }
        public string SubjectId { get; }

        public static ProgressionCondition NpcInteraction(string npcId) =>
            new ProgressionCondition(ProgressionConditionKind.NpcInteraction, npcId);

        public static ProgressionCondition Interaction(string subjectId) =>
            new ProgressionCondition(ProgressionConditionKind.Interaction, subjectId);
    }

    /// <summary>
    /// One reusable objective primitive. Quest steps compose these definitions and campaign objectives
    /// register the same type directly.
    /// </summary>
    public sealed class ObjectiveDefinition
    {
        public ObjectiveDefinition(ObjectiveId id, ProgressionCondition condition, int requiredCount = 1)
        {
            if (!id.IsValid) throw new ArgumentException("Objective id is required.", nameof(id));
            if (requiredCount <= 0) throw new ArgumentOutOfRangeException(nameof(requiredCount));
            Id = id;
            Condition = condition;
            RequiredCount = requiredCount;
        }

        public ObjectiveDefinition(string objectiveId, ProgressionCondition condition, int requiredCount = 1)
            : this(new ObjectiveId(objectiveId), condition, requiredCount) { }

        public ObjectiveId Id { get; }
        public ProgressionCondition Condition { get; }
        public int RequiredCount { get; }
    }

    public sealed class QuestStepDefinition
    {
        private readonly ObjectiveDefinition[] _objectives;

        public QuestStepDefinition(
            string stepId,
            IReadOnlyList<ObjectiveDefinition> objectives,
            string nextStepId = "")
        {
            if (string.IsNullOrWhiteSpace(stepId)) throw new ArgumentException("Quest step id is required.", nameof(stepId));
            if (objectives == null) throw new ArgumentNullException(nameof(objectives));
            _objectives = new ObjectiveDefinition[objectives.Count];
            for (var i = 0; i < objectives.Count; i++)
                _objectives[i] = objectives[i] ?? throw new ArgumentException("Quest objective cannot be null.", nameof(objectives));
            StepId = stepId;
            NextStepId = nextStepId ?? string.Empty;
        }

        public string StepId { get; }
        public IReadOnlyList<ObjectiveDefinition> Objectives => _objectives;
        public string NextStepId { get; }
    }

    public sealed class QuestGraphDefinition
    {
        private readonly QuestStepDefinition[] _steps;

        public QuestGraphDefinition(QuestId id, string firstStepId, IReadOnlyList<QuestStepDefinition> steps)
        {
            if (!id.IsValid) throw new ArgumentException("Quest id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(firstStepId)) throw new ArgumentException("First quest step id is required.", nameof(firstStepId));
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            _steps = new QuestStepDefinition[steps.Count];
            for (var i = 0; i < steps.Count; i++)
                _steps[i] = steps[i] ?? throw new ArgumentException("Quest step cannot be null.", nameof(steps));
            Id = id;
            FirstStepId = firstStepId;
        }

        public QuestGraphDefinition(string questId, string firstStepId, IReadOnlyList<QuestStepDefinition> steps)
            : this(new QuestId(questId), firstStepId, steps) { }

        public QuestId Id { get; }
        public string FirstStepId { get; }
        public IReadOnlyList<QuestStepDefinition> Steps => _steps;
    }

    /// <summary>
    /// Semantic gameplay fact. Callers report what happened; only Progression evaluates completion.
    /// </summary>
    public readonly struct ProgressionUpdateSignal
    {
        public ProgressionUpdateSignal(
            string operationId,
            ProgressionSignalKind kind,
            string subjectId,
            int amount = 1)
        {
            if (string.IsNullOrWhiteSpace(subjectId)) throw new ArgumentException("Progression subject id is required.", nameof(subjectId));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            OperationId = operationId ?? string.Empty;
            Kind = kind;
            SubjectId = subjectId;
            Amount = amount;
        }

        public string OperationId { get; }
        public ProgressionSignalKind Kind { get; }
        public string SubjectId { get; }
        public int Amount { get; }
    }

    public readonly struct ProgressionTransition
    {
        public ProgressionTransition(
            ProgressionTransitionKind kind,
            string entryId,
            string nodeId,
            string objectiveId = "",
            int currentCount = 0,
            int requiredCount = 0)
        {
            Kind = kind;
            EntryId = entryId ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            ObjectiveId = objectiveId ?? string.Empty;
            CurrentCount = currentCount;
            RequiredCount = requiredCount;
        }

        public ProgressionTransitionKind Kind { get; }
        public string EntryId { get; }
        public string NodeId { get; }
        public string ObjectiveId { get; }
        public int CurrentCount { get; }
        public int RequiredCount { get; }
    }

    public sealed class ProgressionUpdateResult
    {
        private readonly ProgressionTransition[] _transitions;

        public ProgressionUpdateResult(
            ProgressionApplyStatus status,
            IReadOnlyList<ProgressionTransition> transitions,
            string reason = "")
        {
            if (transitions == null) throw new ArgumentNullException(nameof(transitions));
            _transitions = new ProgressionTransition[transitions.Count];
            for (var i = 0; i < transitions.Count; i++) _transitions[i] = transitions[i];
            Status = status;
            Reason = reason ?? string.Empty;
        }

        public ProgressionApplyStatus Status { get; }
        public IReadOnlyList<ProgressionTransition> Transitions => _transitions;
        public string Reason { get; }
    }

    /// <summary>Focused entry view for Story/compatibility queries; persistence uses ProgressionSnapshot.</summary>
    public sealed class ProgressionEntrySnapshot
    {
        private readonly string[] _completedNodeIds;
        private readonly Dictionary<string, int> _objectiveCounts;

        public ProgressionEntrySnapshot(
            string entryId,
            ProgressionEntryKind kind,
            ProgressionLifecycleState status,
            string activeNodeId,
            IReadOnlyList<string> completedNodeIds,
            IReadOnlyDictionary<string, int> objectiveCounts,
            ulong revision)
        {
            if (string.IsNullOrWhiteSpace(entryId)) throw new ArgumentException("Progression entry id is required.", nameof(entryId));
            if (completedNodeIds == null) throw new ArgumentNullException(nameof(completedNodeIds));
            if (objectiveCounts == null) throw new ArgumentNullException(nameof(objectiveCounts));
            _completedNodeIds = new string[completedNodeIds.Count];
            for (var i = 0; i < completedNodeIds.Count; i++) _completedNodeIds[i] = completedNodeIds[i];
            _objectiveCounts = new Dictionary<string, int>(objectiveCounts, StringComparer.Ordinal);
            EntryId = entryId;
            Kind = kind;
            Status = status;
            ActiveNodeId = activeNodeId ?? string.Empty;
            Revision = revision;
        }

        public string EntryId { get; }
        public ProgressionEntryKind Kind { get; }
        public ProgressionLifecycleState Status { get; }
        public string ActiveNodeId { get; }
        public IReadOnlyList<string> CompletedNodeIds => _completedNodeIds;
        public IReadOnlyDictionary<string, int> ObjectiveCounts => _objectiveCounts;
        public ulong Revision { get; }
    }

    public interface IProgressionRuntime : IProgressionQuery
    {
        void RegisterQuest(QuestGraphDefinition definition);
        void RegisterStandaloneObjective(ObjectiveDefinition definition);
        ProgressionUpdateResult Start(string entryId, string operationId = "");
        ProgressionUpdateResult Observe(ProgressionUpdateSignal signal);
        ProgressionEntrySnapshot GetSnapshot(string entryId);
        void RestoreState(ProgressionSnapshot snapshot);
        void Reset();
    }
}
