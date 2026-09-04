using System;
using System.Collections.Generic;

namespace Game.Progression
{
    public enum ProgressionEntryKind { Quest = 0, StandaloneObjective = 1 }
    public enum ProgressionNodeStatus { Inactive = 0, Active = 1, Completed = 2 }
    public enum ProgressionConditionKind { Event = 0, NpcInteraction = 1, Interaction = 2, Always = 3 }
    public enum ProgressionTransitionKind { EntryStarted = 0, NodeActivated = 1, NodeCompleted = 2, EntryCompleted = 3, RewardEmitted = 4 }

    public readonly struct ProgressionCondition
    {
        public ProgressionCondition(ProgressionConditionKind kind, string subjectId)
        {
            Kind = kind;
            SubjectId = subjectId ?? string.Empty;
        }
        public ProgressionConditionKind Kind { get; }
        public string SubjectId { get; }
        public static ProgressionCondition Event(string eventId) => new ProgressionCondition(ProgressionConditionKind.Event, eventId);
        public static ProgressionCondition NpcInteraction(string npcId) => new ProgressionCondition(ProgressionConditionKind.NpcInteraction, npcId);
        public static ProgressionCondition Interaction(string subjectId) => new ProgressionCondition(ProgressionConditionKind.Interaction, subjectId);
        public static ProgressionCondition Always() => new ProgressionCondition(ProgressionConditionKind.Always, string.Empty);
    }

    public sealed class ObjectiveDefinition
    {
        public ObjectiveDefinition(string objectiveId, string eventId, int requiredCount)
            : this(objectiveId, ProgressionCondition.Event(eventId), requiredCount, string.Empty) { }
        public ObjectiveDefinition(string objectiveId, ProgressionCondition condition, int requiredCount, string rewardId = "")
        {
            ObjectiveId = objectiveId;
            Condition = condition;
            RequiredCount = requiredCount;
            RewardId = rewardId ?? string.Empty;
        }
        public string ObjectiveId { get; }
        public string EventId => Condition.Kind == ProgressionConditionKind.Event ? Condition.SubjectId : string.Empty;
        public ProgressionCondition Condition { get; }
        public int RequiredCount { get; }
        public string RewardId { get; }
    }

    public sealed class QuestStepDefinition
    {
        public QuestStepDefinition(string stepId, IReadOnlyList<ObjectiveDefinition> objectives, IReadOnlyList<string> nextStepIds)
        {
            StepId = stepId;
            Objectives = objectives ?? Array.Empty<ObjectiveDefinition>();
            NextStepIds = nextStepIds ?? Array.Empty<string>();
        }
        public string StepId { get; }
        public IReadOnlyList<ObjectiveDefinition> Objectives { get; }
        public IReadOnlyList<string> NextStepIds { get; }
    }

    public sealed class QuestGraphDefinition
    {
        public QuestGraphDefinition(string questId, string firstStepId, IReadOnlyList<QuestStepDefinition> steps)
        {
            QuestId = questId;
            FirstStepId = firstStepId;
            Steps = steps ?? Array.Empty<QuestStepDefinition>();
        }
        public string QuestId { get; }
        public string FirstStepId { get; }
        public IReadOnlyList<QuestStepDefinition> Steps { get; }
    }

    public sealed class StandaloneObjectiveDefinition
    {
        public StandaloneObjectiveDefinition(string objectiveId, string eventId, int requiredCount)
            : this(objectiveId, ProgressionCondition.Event(eventId), requiredCount, string.Empty) { }
        public StandaloneObjectiveDefinition(string objectiveId, ProgressionCondition condition, int requiredCount, string rewardId = "")
        {
            ObjectiveId = objectiveId;
            Condition = condition;
            RequiredCount = requiredCount;
            RewardId = rewardId ?? string.Empty;
        }
        public string ObjectiveId { get; }
        public string EventId => Condition.Kind == ProgressionConditionKind.Event ? Condition.SubjectId : string.Empty;
        public ProgressionCondition Condition { get; }
        public int RequiredCount { get; }
        public string RewardId { get; }
    }

    public enum ProgressionSignalKind { Event = 0, NpcInteracted = 1, Interacted = 2 }

    public readonly struct ProgressionUpdateSignal
    {
        public ProgressionUpdateSignal(string operationId, ProgressionSignalKind kind, string subjectId, int amount = 1)
        {
            OperationId = operationId ?? string.Empty;
            Kind = kind;
            SubjectId = subjectId ?? string.Empty;
            Amount = amount;
        }
        public string OperationId { get; }
        public ProgressionSignalKind Kind { get; }
        public string SubjectId { get; }
        public int Amount { get; }
    }

    public readonly struct ProgressionTransition
    {
        public ProgressionTransition(ProgressionTransitionKind kind, string entryId, string nodeId, string rewardId = "")
        {
            Kind = kind;
            EntryId = entryId ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            RewardId = rewardId ?? string.Empty;
        }
        public ProgressionTransitionKind Kind { get; }
        public string EntryId { get; }
        public string NodeId { get; }
        public string RewardId { get; }
    }

    public sealed class ProgressionUpdateResult
    {
        public ProgressionUpdateResult(ProgressionApplyStatus status, IReadOnlyList<ProgressionTransition> transitions, string reason = "")
        {
            Status = status;
            Transitions = transitions ?? Array.Empty<ProgressionTransition>();
            Reason = reason ?? string.Empty;
        }
        public ProgressionApplyStatus Status { get; }
        public IReadOnlyList<ProgressionTransition> Transitions { get; }
        public string Reason { get; }
    }

    public sealed class ProgressionEntrySnapshot
    {
        public ProgressionEntrySnapshot(string entryId, ProgressionEntryKind kind, ProgressionNodeStatus status, string activeNodeId,
            IReadOnlyList<string> completedNodeIds, IReadOnlyDictionary<string, int> objectiveCounts)
        {
            EntryId = entryId ?? string.Empty;
            Kind = kind;
            Status = status;
            ActiveNodeId = activeNodeId ?? string.Empty;
            CompletedNodeIds = completedNodeIds ?? Array.Empty<string>();
            ObjectiveCounts = objectiveCounts ?? new Dictionary<string, int>();
        }
        public string EntryId { get; }
        public ProgressionEntryKind Kind { get; }
        public ProgressionNodeStatus Status { get; }
        public string ActiveNodeId { get; }
        public IReadOnlyList<string> CompletedNodeIds { get; }
        public IReadOnlyDictionary<string, int> ObjectiveCounts { get; }
    }

    public sealed class ProgressionStateSnapshot
    {
        public ProgressionStateSnapshot(IReadOnlyList<ProgressionEntrySnapshot> entries, IReadOnlyList<string> appliedOperationIds,
            IReadOnlyList<string> emittedRewardIds, long compatibilitySequence)
        {
            Entries = entries ?? Array.Empty<ProgressionEntrySnapshot>();
            AppliedOperationIds = appliedOperationIds ?? Array.Empty<string>();
            EmittedRewardIds = emittedRewardIds ?? Array.Empty<string>();
            CompatibilitySequence = compatibilitySequence;
        }
        public IReadOnlyList<ProgressionEntrySnapshot> Entries { get; }
        public IReadOnlyList<string> AppliedOperationIds { get; }
        public IReadOnlyList<string> EmittedRewardIds { get; }
        public long CompatibilitySequence { get; }
        public static ProgressionStateSnapshot Empty => new ProgressionStateSnapshot(Array.Empty<ProgressionEntrySnapshot>(), Array.Empty<string>(), Array.Empty<string>(), 0);
    }

    public interface IReadOnlyQuestGraphRegistry { bool TryGet(string questId, out QuestGraphDefinition definition); }
    public interface IReadOnlyStandaloneObjectiveRegistry { bool TryGet(string objectiveId, out StandaloneObjectiveDefinition definition); }

    public interface IProgressionCompletionConditionResolver
    {
        bool Matches(ProgressionCondition condition, ProgressionUpdateSignal signal);
    }

    public interface IProgressionRuntime
    {
        void RegisterQuest(QuestGraphDefinition definition);
        void RegisterStandaloneObjective(StandaloneObjectiveDefinition definition);
        ProgressionUpdateResult Start(string entryId, string operationId = "");
        ProgressionUpdateResult Observe(ProgressionUpdateSignal signal);
        ProgressionUpdateResult ForceComplete(string entryId, string operationId = "");
        ProgressionEntrySnapshot GetSnapshot(string entryId);
        ProgressionStateSnapshot CaptureState();
        void RestoreState(ProgressionStateSnapshot snapshot);
        void Reset();
    }
}
