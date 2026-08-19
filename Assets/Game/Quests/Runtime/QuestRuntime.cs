using System;
using System.Collections.Generic;
using Game.Quests.Api;

namespace Game.Quests.Runtime
{
    /// <summary>
    /// Deterministic authoritative quest-state machine. Definitions are immutable; this type owns only
    /// mutable progression state. The first slice supports linear multi-step quests while preserving an
    /// API shape that can later add branching without changing authored/generated QuestDefinition identity.
    /// </summary>
    public sealed class QuestRuntime
    {
        private sealed class RuntimeQuest
        {
            public QuestDefinition Definition { get; }
            public QuestStepStatus[] StepStates { get; }
            public QuestStatus Status { get; set; }
            public int ActiveStepIndex { get; set; }

            public RuntimeQuest(QuestDefinition definition)
            {
                Definition = definition ?? throw new ArgumentNullException(nameof(definition));
                StepStates = new QuestStepStatus[definition.Steps.Count];
                for (var i = 0; i < StepStates.Length; i++)
                    StepStates[i] = QuestStepStatus.Locked;
                Status = QuestStatus.Inactive;
                ActiveStepIndex = -1;
            }
        }

        private readonly List<RuntimeQuest> _ordered = new List<RuntimeQuest>();
        private readonly Dictionary<QuestRef, RuntimeQuest> _byRef =
            new Dictionary<QuestRef, RuntimeQuest>();

        public QuestRuntime(IReadOnlyList<QuestDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            for (var i = 0; i < definitions.Count; i++)
            {
                QuestDefinition definition = definitions[i]
                    ?? throw new InvalidOperationException(
                        "Quest definition collection contains null at index " + i + ".");
                if (_byRef.ContainsKey(definition.Ref))
                    throw new InvalidOperationException(
                        "Quest definition collection contains duplicate quest ref '" + definition.Ref + "'.");

                var runtime = new RuntimeQuest(definition);
                _ordered.Add(runtime);
                _byRef.Add(definition.Ref, runtime);
            }
        }

        public IReadOnlyList<QuestEvent> Start(QuestRef quest)
        {
            RuntimeQuest runtime = RequireQuest(quest);
            if (runtime.Status == QuestStatus.Completed)
                throw new InvalidOperationException("Cannot restart completed quest '" + quest + "'.");
            if (runtime.Status == QuestStatus.Failed)
                throw new InvalidOperationException("Cannot restart failed quest '" + quest + "'.");
            if (runtime.Status == QuestStatus.Active)
                return Array.Empty<QuestEvent>();

            runtime.Status = QuestStatus.Active;
            runtime.ActiveStepIndex = 0;
            runtime.StepStates[0] = QuestStepStatus.Active;

            return new[]
            {
                QuestEvent.Started(quest),
                QuestEvent.StepActivated(quest, runtime.Definition.Steps[0].Ref)
            };
        }

        public IReadOnlyList<QuestEvent> Observe(QuestObservation observation)
        {
            var events = new List<QuestEvent>();

            // Definition order is authoritative. Never use dictionary enumeration here: one gameplay
            // observation may advance several active quests and their emitted event order must be stable.
            for (var i = 0; i < _ordered.Count; i++)
            {
                RuntimeQuest runtime = _ordered[i];
                if (runtime.Status != QuestStatus.Active || runtime.ActiveStepIndex < 0)
                    continue;

                int stepIndex = runtime.ActiveStepIndex;
                QuestStepDefinition step = runtime.Definition.Steps[stepIndex];
                if (!Matches(step.Completion, observation))
                    continue;

                runtime.StepStates[stepIndex] = QuestStepStatus.Completed;
                runtime.ActiveStepIndex = -1;
                events.Add(QuestEvent.StepCompleted(runtime.Definition.Ref, step.Ref));

                int next = stepIndex + 1;
                if (next < runtime.Definition.Steps.Count)
                {
                    runtime.ActiveStepIndex = next;
                    runtime.StepStates[next] = QuestStepStatus.Active;
                    events.Add(QuestEvent.StepActivated(
                        runtime.Definition.Ref,
                        runtime.Definition.Steps[next].Ref));
                }
                else
                {
                    runtime.Status = QuestStatus.Completed;
                    events.Add(QuestEvent.Completed(runtime.Definition.Ref));
                }
            }

            return events;
        }

        public bool IsActive(QuestRef quest) => RequireQuest(quest).Status == QuestStatus.Active;
        public bool IsCompleted(QuestRef quest) => RequireQuest(quest).Status == QuestStatus.Completed;

        public QuestSnapshot GetSnapshot(QuestRef quest)
        {
            RuntimeQuest runtime = RequireQuest(quest);
            var steps = new QuestStepSnapshot[runtime.Definition.Steps.Count];
            for (var i = 0; i < steps.Length; i++)
            {
                QuestStepDefinition definition = runtime.Definition.Steps[i];
                steps[i] = new QuestStepSnapshot(
                    definition.Ref,
                    definition.TargetId,
                    runtime.StepStates[i]);
            }

            return new QuestSnapshot(runtime.Definition.Ref, runtime.Status, steps);
        }

        private RuntimeQuest RequireQuest(QuestRef quest)
        {
            RuntimeQuest runtime;
            if (!_byRef.TryGetValue(quest, out runtime))
                throw new InvalidOperationException("Unknown quest '" + quest + "'.");
            return runtime;
        }

        private static bool Matches(IQuestCompletionSpec completion, QuestObservation observation)
        {
            if (completion is NpcInteractionQuestCompletionSpec interaction)
            {
                return observation.Kind == QuestObservationKind.NpcInteracted
                    && string.Equals(interaction.NpcId, observation.SubjectId, StringComparison.Ordinal);
            }

            throw new InvalidOperationException(
                "Unsupported quest completion type: " +
                (completion?.GetType().FullName ?? "<null>") + ".");
        }
    }
}
