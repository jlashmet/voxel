using System;
using System.Collections.Generic;
using Game.Progression.Api;
using Game.Progression.Runtime;
using Game.Quests.Api;

namespace Game.Quests.Runtime
{
    /// <summary>
    /// Compatibility facade for existing quest-shaped callers. It owns immutable legacy definitions only;
    /// all mutable quest state and evaluation are delegated to the shared ProgressionRuntime.
    /// </summary>
    public sealed class QuestRuntime
    {
        private readonly List<QuestDefinition> _ordered = new List<QuestDefinition>();
        private readonly Dictionary<QuestRef, QuestDefinition> _byRef =
            new Dictionary<QuestRef, QuestDefinition>();
        private readonly ProgressionRuntime _progression;

        public QuestRuntime(IReadOnlyList<QuestDefinition> definitions)
            : this(definitions, new ProgressionRuntime()) { }

        public QuestRuntime(IReadOnlyList<QuestDefinition> definitions, ProgressionRuntime progression)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));

            for (var i = 0; i < definitions.Count; i++)
            {
                QuestDefinition definition = definitions[i]
                    ?? throw new InvalidOperationException(
                        "Quest definition collection contains null at index " + i + ".");
                if (_byRef.ContainsKey(definition.Ref))
                    throw new InvalidOperationException(
                        "Quest definition collection contains duplicate quest ref '" + definition.Ref + "'.");
                _ordered.Add(definition);
                _byRef.Add(definition.Ref, definition);
                _progression.RegisterQuest(ToProgressionDefinition(definition));
            }
        }

        public IProgressionQuery Progression => _progression;

        public IReadOnlyList<QuestEvent> Start(QuestRef quest)
        {
            QuestDefinition definition = RequireQuest(quest);
            ProgressionEntrySnapshot before = _progression.GetSnapshot(quest.Id);
            if (before.Status == ProgressionLifecycleState.Completed)
                throw new InvalidOperationException("Cannot restart completed quest '" + quest + "'.");
            if (before.Status == ProgressionLifecycleState.Active)
                return Array.Empty<QuestEvent>();

            ProgressionUpdateResult result = _progression.Start(quest.Id);
            return MapTransitions(result.Transitions);
        }

        public IReadOnlyList<QuestEvent> Observe(QuestObservation observation)
        {
            ProgressionSignalKind kind;
            switch (observation.Kind)
            {
                case QuestObservationKind.NpcInteracted:
                    kind = ProgressionSignalKind.NpcInteracted;
                    break;
                case QuestObservationKind.Interacted:
                    kind = ProgressionSignalKind.Interacted;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unsupported quest observation kind '" + observation.Kind + "'.");
            }

            ProgressionUpdateResult result = _progression.Observe(
                new ProgressionUpdateSignal(string.Empty, kind, observation.SubjectId, 1));
            return MapTransitions(result.Transitions);
        }

        public bool IsActive(QuestRef quest) =>
            _progression.GetSnapshot(RequireQuest(quest).Ref.Id).Status == ProgressionLifecycleState.Active;

        public bool IsCompleted(QuestRef quest) =>
            _progression.GetSnapshot(RequireQuest(quest).Ref.Id).Status == ProgressionLifecycleState.Completed;

        public QuestSnapshot GetSnapshot(QuestRef quest)
        {
            QuestDefinition definition = RequireQuest(quest);
            ProgressionEntrySnapshot snapshot = _progression.GetSnapshot(quest.Id);
            var completed = new HashSet<string>(snapshot.CompletedNodeIds, StringComparer.Ordinal);
            var steps = new QuestStepSnapshot[definition.Steps.Count];

            for (var i = 0; i < steps.Length; i++)
            {
                Game.Quests.Api.QuestStepDefinition step = definition.Steps[i];
                QuestStepStatus status = completed.Contains(step.Ref.Id)
                    ? QuestStepStatus.Completed
                    : string.Equals(snapshot.ActiveNodeId, step.Ref.Id, StringComparison.Ordinal)
                        ? QuestStepStatus.Active
                        : snapshot.Status == ProgressionLifecycleState.Completed
                            ? QuestStepStatus.Skipped
                            : QuestStepStatus.Locked;
                steps[i] = new QuestStepSnapshot(step.Ref, step.TargetId, status);
            }

            QuestStatus questStatus = snapshot.Status == ProgressionLifecycleState.Active
                ? QuestStatus.Active
                : snapshot.Status == ProgressionLifecycleState.Completed
                    ? QuestStatus.Completed
                    : QuestStatus.Inactive;
            return new QuestSnapshot(definition.Ref, questStatus, steps);
        }

        public bool TryGetQuestRef(string questId, out QuestRef quest)
        {
            for (var i = 0; i < _ordered.Count; i++)
            {
                if (!string.Equals(_ordered[i].Ref.Id, questId, StringComparison.Ordinal)) continue;
                quest = _ordered[i].Ref;
                return true;
            }
            quest = default;
            return false;
        }

        public IReadOnlyList<QuestEvent> MapTransitions(IReadOnlyList<ProgressionTransition> transitions)
        {
            if (transitions == null) throw new ArgumentNullException(nameof(transitions));
            var events = new List<QuestEvent>();
            for (var i = 0; i < transitions.Count; i++)
            {
                ProgressionTransition transition = transitions[i];
                if (!TryGetQuestRef(transition.EntryId, out QuestRef quest)) continue;
                AppendMapped(events, transition, quest);
            }
            return events;
        }

        private QuestDefinition RequireQuest(QuestRef quest)
        {
            if (!_byRef.TryGetValue(quest, out QuestDefinition definition))
                throw new InvalidOperationException("Unknown quest '" + quest + "'.");
            return definition;
        }

        private static QuestGraphDefinition ToProgressionDefinition(QuestDefinition definition)
        {
            var steps = new Game.Progression.Api.QuestStepDefinition[definition.Steps.Count];
            for (var i = 0; i < definition.Steps.Count; i++)
            {
                Game.Quests.Api.QuestStepDefinition source = definition.Steps[i];
                ProgressionCondition condition;
                if (source.Completion is NpcInteractionQuestCompletionSpec npc)
                    condition = ProgressionCondition.NpcInteraction(npc.NpcId);
                else if (source.Completion is InteractionQuestCompletionSpec interaction)
                    condition = ProgressionCondition.Interaction(interaction.SubjectId);
                else
                    throw new InvalidOperationException(
                        "Unsupported quest completion type: " +
                        (source.Completion?.GetType().FullName ?? "<null>") + ".");

                var objective = new ObjectiveDefinition(
                    source.Ref.Id + ".completion",
                    condition,
                    1);
                string next = i + 1 < definition.Steps.Count
                    ? definition.Steps[i + 1].Ref.Id
                    : string.Empty;
                steps[i] = new Game.Progression.Api.QuestStepDefinition(
                    source.Ref.Id,
                    new[] { objective },
                    next);
            }

            return new QuestGraphDefinition(
                definition.Ref.Id,
                definition.Steps[0].Ref.Id,
                steps);
        }

        private static void AppendMapped(
            List<QuestEvent> events,
            ProgressionTransition transition,
            QuestRef quest)
        {
            switch (transition.Kind)
            {
                case ProgressionTransitionKind.EntryStarted:
                    events.Add(QuestEvent.Started(quest));
                    break;
                case ProgressionTransitionKind.NodeActivated:
                    events.Add(QuestEvent.StepActivated(quest, new QuestStepRef(transition.NodeId)));
                    break;
                case ProgressionTransitionKind.NodeCompleted:
                    events.Add(QuestEvent.StepCompleted(quest, new QuestStepRef(transition.NodeId)));
                    break;
                case ProgressionTransitionKind.EntryCompleted:
                    events.Add(QuestEvent.Completed(quest));
                    break;
            }
        }
    }
}
