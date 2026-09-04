using System;
using System.Collections.Generic;
using Game.Progression.Api;

namespace Game.Progression.Runtime
{
    public sealed class ProgressionConditionResolver
    {
        public bool Matches(ProgressionCondition condition, ProgressionUpdateSignal signal)
        {
            if (!string.Equals(condition.SubjectId, signal.SubjectId, StringComparison.Ordinal))
                return false;

            switch (condition.Kind)
            {
                case ProgressionConditionKind.NpcInteraction:
                    return signal.Kind == ProgressionSignalKind.NpcInteracted;
                case ProgressionConditionKind.Interaction:
                    return signal.Kind == ProgressionSignalKind.Interacted;
                default:
                    throw new InvalidOperationException("Unsupported progression condition kind '" + condition.Kind + "'.");
            }
        }
    }

    /// <summary>
    /// One authoritative deterministic state machine for quest objectives and standalone objectives.
    /// Gameplay reports semantic facts; this runtime alone evaluates progression transitions.
    /// </summary>
    public sealed class ProgressionRuntime : IProgressionRuntime
    {
        private sealed class EntryState
        {
            public ProgressionEntryKind Kind;
            public ProgressionLifecycleState Status;
            public string ActiveNodeId = string.Empty;
            public readonly HashSet<string> CompletedNodes = new HashSet<string>(StringComparer.Ordinal);
            public readonly Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.Ordinal);
            public ulong Revision;
        }

        private readonly Dictionary<string, QuestGraphDefinition> _quests =
            new Dictionary<string, QuestGraphDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, ObjectiveDefinition> _standalone =
            new Dictionary<string, ObjectiveDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, EntryState> _states =
            new Dictionary<string, EntryState>(StringComparer.Ordinal);
        private readonly List<string> _orderedIds = new List<string>();
        private readonly HashSet<string> _appliedOperations = new HashSet<string>(StringComparer.Ordinal);
        private readonly ProgressionConditionResolver _resolver;

        private ulong _revision;
        private long _compatibilitySequence;

        public ProgressionRuntime(ProgressionConditionResolver resolver = null)
        {
            _resolver = resolver ?? new ProgressionConditionResolver();
        }

        public void RegisterQuest(QuestGraphDefinition definition)
        {
            ValidateQuest(definition);
            string id = definition.Id.Value;
            RequireUniqueEntry(id);
            _quests.Add(id, definition);
            _states.Add(id, NewState(ProgressionEntryKind.Quest));
            _orderedIds.Add(id);
        }

        public void RegisterStandaloneObjective(ObjectiveDefinition definition)
        {
            ValidateObjective(definition);
            string id = definition.Id.Value;
            RequireUniqueEntry(id);
            _standalone.Add(id, definition);
            _states.Add(id, NewState(ProgressionEntryKind.StandaloneObjective));
            _orderedIds.Add(id);
        }

        public ProgressionUpdateResult Start(string entryId, string operationId = "")
        {
            EntryState state = RequireState(entryId);
            string op = ResolveOperation(operationId, "start");
            if (TryReplay(op, out ProgressionUpdateResult replay)) return replay;

            var transitions = new List<ProgressionTransition>();
            var touched = new List<EntryState>();
            if (state.Status == ProgressionLifecycleState.Completed)
                return Record(op, ProgressionApplyStatus.Rejected, transitions, touched,
                    "Completed progression entries cannot be restarted.");
            if (state.Status == ProgressionLifecycleState.Active)
                return Record(op, ProgressionApplyStatus.Applied, transitions, touched, string.Empty);

            state.Status = ProgressionLifecycleState.Active;
            touched.Add(state);
            transitions.Add(new ProgressionTransition(
                ProgressionTransitionKind.EntryStarted,
                entryId,
                string.Empty));

            if (state.Kind == ProgressionEntryKind.Quest)
            {
                QuestGraphDefinition definition = _quests[entryId];
                ActivateStep(entryId, state, definition, definition.FirstStepId, transitions);
            }
            else
            {
                state.ActiveNodeId = entryId;
                transitions.Add(new ProgressionTransition(
                    ProgressionTransitionKind.NodeActivated,
                    entryId,
                    entryId));
            }

            return Record(op, ProgressionApplyStatus.Applied, transitions, touched, string.Empty);
        }

        public ProgressionUpdateResult Observe(ProgressionUpdateSignal signal)
        {
            string op = ResolveOperation(signal.OperationId, "observe");
            if (TryReplay(op, out ProgressionUpdateResult replay)) return replay;

            var transitions = new List<ProgressionTransition>();
            var touched = new List<EntryState>();
            for (var i = 0; i < _orderedIds.Count; i++)
            {
                string id = _orderedIds[i];
                EntryState state = _states[id];
                if (state.Status != ProgressionLifecycleState.Active) continue;

                bool changed = state.Kind == ProgressionEntryKind.StandaloneObjective
                    ? ObserveStandalone(id, state, _standalone[id], signal, transitions)
                    : ObserveQuest(id, state, _quests[id], signal, transitions);
                if (changed) touched.Add(state);
            }

            return Record(op, ProgressionApplyStatus.Applied, transitions, touched, string.Empty);
        }

        public ProgressionEntrySnapshot GetSnapshot(string entryId)
        {
            EntryState state = RequireState(entryId);
            var completedNodes = new List<string>(state.CompletedNodes);
            completedNodes.Sort(StringComparer.Ordinal);
            var counts = new Dictionary<string, int>(state.Counts, StringComparer.Ordinal);
            return new ProgressionEntrySnapshot(
                entryId,
                state.Kind,
                state.Status,
                state.ActiveNodeId,
                completedNodes,
                counts,
                state.Revision);
        }

        public ProgressionSnapshot Snapshot()
        {
            var quests = new List<QuestProgressSnapshot>();
            var standalone = new List<ObjectiveProgressSnapshot>();

            for (var i = 0; i < _orderedIds.Count; i++)
            {
                string id = _orderedIds[i];
                EntryState state = _states[id];
                if (state.Kind == ProgressionEntryKind.Quest)
                    quests.Add(BuildQuestSnapshot(_quests[id], state));
                else
                    standalone.Add(BuildStandaloneSnapshot(_standalone[id], state));
            }

            var operations = new List<string>(_appliedOperations);
            operations.Sort(StringComparer.Ordinal);
            return new ProgressionSnapshot(
                _revision,
                quests,
                standalone,
                operations,
                _compatibilitySequence);
        }

        public void RestoreState(ProgressionSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Quests.Count + snapshot.StandaloneObjectives.Count != _states.Count)
                throw new InvalidOperationException("Progression snapshot does not contain the complete registered entry set.");

            Reset();
            var restoredEntries = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < snapshot.Quests.Count; i++)
            {
                QuestProgressSnapshot quest = snapshot.Quests[i]
                    ?? throw new InvalidOperationException("Progression snapshot contains a null quest.");
                string id = quest.Id.Value;
                if (!_quests.TryGetValue(id, out QuestGraphDefinition definition))
                    throw new InvalidOperationException("Progression snapshot references unknown quest '" + id + "'.");
                if (!restoredEntries.Add(id))
                    throw new InvalidOperationException("Progression snapshot contains duplicate entry '" + id + "'.");
                RestoreQuest(definition, _states[id], quest);
            }

            for (var i = 0; i < snapshot.StandaloneObjectives.Count; i++)
            {
                ObjectiveProgressSnapshot objective = snapshot.StandaloneObjectives[i];
                string id = objective.Id.Value;
                if (!_standalone.TryGetValue(id, out ObjectiveDefinition definition))
                    throw new InvalidOperationException("Progression snapshot references unknown standalone objective '" + id + "'.");
                if (!restoredEntries.Add(id))
                    throw new InvalidOperationException("Progression snapshot contains duplicate entry '" + id + "'.");
                RestoreStandalone(definition, _states[id], objective);
            }

            if (restoredEntries.Count != _states.Count)
                throw new InvalidOperationException("Progression snapshot omitted a registered entry.");

            for (var i = 0; i < snapshot.AppliedOperationIds.Count; i++)
            {
                string operationId = RequireId(snapshot.AppliedOperationIds[i], "operation id");
                if (!_appliedOperations.Add(operationId))
                    throw new InvalidOperationException("Progression snapshot contains duplicate operation id '" + operationId + "'.");
            }

            _revision = snapshot.Revision;
            _compatibilitySequence = snapshot.CompatibilitySequence;
            foreach (EntryState state in _states.Values)
                if (state.Revision > _revision)
                    throw new InvalidOperationException("Progression entry revision cannot exceed the snapshot revision.");
        }

        public void Reset()
        {
            foreach (EntryState state in _states.Values)
            {
                state.Status = ProgressionLifecycleState.Inactive;
                state.ActiveNodeId = string.Empty;
                state.CompletedNodes.Clear();
                state.Counts.Clear();
                state.Revision = 0;
            }
            _appliedOperations.Clear();
            _revision = 0;
            _compatibilitySequence = 0;
        }

        private bool ObserveStandalone(
            string id,
            EntryState state,
            ObjectiveDefinition definition,
            ProgressionUpdateSignal signal,
            List<ProgressionTransition> transitions)
        {
            if (!_resolver.Matches(definition.Condition, signal)) return false;

            int count = Increment(state, definition.Id.Value, signal.Amount, definition.RequiredCount);
            transitions.Add(new ProgressionTransition(
                ProgressionTransitionKind.ObjectiveProgressed,
                id,
                id,
                definition.Id.Value,
                count,
                definition.RequiredCount));
            if (count < definition.RequiredCount) return true;

            state.CompletedNodes.Add(id);
            transitions.Add(new ProgressionTransition(
                ProgressionTransitionKind.NodeCompleted,
                id,
                id));
            CompleteEntry(id, state, transitions);
            return true;
        }

        private bool ObserveQuest(
            string id,
            EntryState state,
            QuestGraphDefinition definition,
            ProgressionUpdateSignal signal,
            List<ProgressionTransition> transitions)
        {
            QuestStepDefinition step = FindStep(definition, state.ActiveNodeId);
            bool changed = false;
            for (var i = 0; i < step.Objectives.Count; i++)
            {
                ObjectiveDefinition objective = step.Objectives[i];
                if (!_resolver.Matches(objective.Condition, signal)) continue;

                int current = GetCount(state, objective.Id.Value);
                if (current >= objective.RequiredCount) continue;
                int count = Increment(state, objective.Id.Value, signal.Amount, objective.RequiredCount);
                transitions.Add(new ProgressionTransition(
                    ProgressionTransitionKind.ObjectiveProgressed,
                    id,
                    step.StepId,
                    objective.Id.Value,
                    count,
                    objective.RequiredCount));
                changed = true;
            }

            if (!changed || !StepComplete(state, step)) return changed;

            state.CompletedNodes.Add(step.StepId);
            transitions.Add(new ProgressionTransition(
                ProgressionTransitionKind.NodeCompleted,
                id,
                step.StepId));
            if (string.IsNullOrEmpty(step.NextStepId))
                CompleteEntry(id, state, transitions);
            else
                ActivateStep(id, state, definition, step.NextStepId, transitions);
            return true;
        }

        private static void ActivateStep(
            string entryId,
            EntryState state,
            QuestGraphDefinition definition,
            string stepId,
            List<ProgressionTransition> transitions)
        {
            var activationGuard = new HashSet<string>(StringComparer.Ordinal);
            string current = stepId;
            while (true)
            {
                if (!activationGuard.Add(current))
                    throw new InvalidOperationException("Progression graph entered a cycle at '" + current + "'.");

                QuestStepDefinition step = FindStep(definition, current);
                state.ActiveNodeId = current;
                transitions.Add(new ProgressionTransition(
                    ProgressionTransitionKind.NodeActivated,
                    entryId,
                    current));

                if (step.Objectives.Count != 0) return;

                state.CompletedNodes.Add(current);
                transitions.Add(new ProgressionTransition(
                    ProgressionTransitionKind.NodeCompleted,
                    entryId,
                    current));
                if (string.IsNullOrEmpty(step.NextStepId))
                {
                    CompleteEntry(entryId, state, transitions);
                    return;
                }
                current = step.NextStepId;
            }
        }

        private static void CompleteEntry(
            string id,
            EntryState state,
            List<ProgressionTransition> transitions)
        {
            state.Status = ProgressionLifecycleState.Completed;
            state.ActiveNodeId = string.Empty;
            transitions.Add(new ProgressionTransition(
                ProgressionTransitionKind.EntryCompleted,
                id,
                string.Empty));
        }

        private ProgressionUpdateResult Record(
            string operationId,
            ProgressionApplyStatus status,
            List<ProgressionTransition> transitions,
            List<EntryState> touched,
            string reason)
        {
            _appliedOperations.Add(operationId);
            _revision++;
            for (var i = 0; i < touched.Count; i++) touched[i].Revision = _revision;
            return new ProgressionUpdateResult(status, transitions, reason);
        }

        private bool TryReplay(string operationId, out ProgressionUpdateResult result)
        {
            if (_appliedOperations.Contains(operationId))
            {
                result = new ProgressionUpdateResult(
                    ProgressionApplyStatus.Replay,
                    Array.Empty<ProgressionTransition>(),
                    "Operation already applied.");
                return true;
            }
            result = null;
            return false;
        }

        private string ResolveOperation(string operationId, string prefix)
        {
            if (!string.IsNullOrWhiteSpace(operationId)) return operationId;
            _compatibilitySequence++;
            return "compat:" + prefix + ":" + _compatibilitySequence;
        }

        private QuestProgressSnapshot BuildQuestSnapshot(QuestGraphDefinition definition, EntryState state)
        {
            var steps = new QuestStepProgressSnapshot[definition.Steps.Count];
            for (var i = 0; i < definition.Steps.Count; i++)
            {
                QuestStepDefinition step = definition.Steps[i];
                ProgressionLifecycleState stepState = state.CompletedNodes.Contains(step.StepId)
                    ? ProgressionLifecycleState.Completed
                    : string.Equals(state.ActiveNodeId, step.StepId, StringComparison.Ordinal)
                        ? ProgressionLifecycleState.Active
                        : ProgressionLifecycleState.Inactive;

                var objectives = new ObjectiveProgressSnapshot[step.Objectives.Count];
                for (var o = 0; o < step.Objectives.Count; o++)
                {
                    ObjectiveDefinition objective = step.Objectives[o];
                    int count = GetCount(state, objective.Id.Value);
                    ProgressionLifecycleState objectiveState = count >= objective.RequiredCount
                        ? ProgressionLifecycleState.Completed
                        : stepState == ProgressionLifecycleState.Active
                            ? ProgressionLifecycleState.Active
                            : ProgressionLifecycleState.Inactive;
                    objectives[o] = new ObjectiveProgressSnapshot(
                        objective.Id,
                        objectiveState,
                        count,
                        objective.RequiredCount,
                        state.Revision);
                }

                steps[i] = new QuestStepProgressSnapshot(step.StepId, stepState, objectives);
            }

            return new QuestProgressSnapshot(
                definition.Id,
                state.Status,
                state.ActiveNodeId,
                steps,
                state.Revision);
        }

        private static ObjectiveProgressSnapshot BuildStandaloneSnapshot(
            ObjectiveDefinition definition,
            EntryState state)
        {
            int count = GetCount(state, definition.Id.Value);
            return new ObjectiveProgressSnapshot(
                definition.Id,
                state.Status,
                count,
                definition.RequiredCount,
                state.Revision);
        }

        private static void RestoreQuest(
            QuestGraphDefinition definition,
            EntryState state,
            QuestProgressSnapshot snapshot)
        {
            if (snapshot.Steps.Count != definition.Steps.Count)
                throw new InvalidOperationException("Quest '" + definition.Id + "' snapshot step count does not match its definition.");
            if (snapshot.State == ProgressionLifecycleState.Failed)
                throw new InvalidOperationException("Failed quest state is not supported by this progression runtime.");

            int activeSteps = 0;
            for (var i = 0; i < definition.Steps.Count; i++)
            {
                QuestStepDefinition expectedStep = definition.Steps[i];
                QuestStepProgressSnapshot savedStep = snapshot.Steps[i];
                if (!string.Equals(expectedStep.StepId, savedStep.StepId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Quest '" + definition.Id + "' snapshot step identity changed at index " + i + ".");
                if (savedStep.Objectives.Count != expectedStep.Objectives.Count)
                    throw new InvalidOperationException("Quest step '" + expectedStep.StepId + "' snapshot objective count changed.");

                if (savedStep.State == ProgressionLifecycleState.Completed)
                    state.CompletedNodes.Add(expectedStep.StepId);
                else if (savedStep.State == ProgressionLifecycleState.Active)
                {
                    state.ActiveNodeId = expectedStep.StepId;
                    activeSteps++;
                }
                else if (savedStep.State == ProgressionLifecycleState.Failed)
                    throw new InvalidOperationException("Failed quest step state is not supported.");

                for (var o = 0; o < expectedStep.Objectives.Count; o++)
                {
                    ObjectiveDefinition expected = expectedStep.Objectives[o];
                    ObjectiveProgressSnapshot saved = savedStep.Objectives[o];
                    if (saved.Id != expected.Id || saved.RequiredCount != expected.RequiredCount)
                        throw new InvalidOperationException("Quest objective definition changed for '" + expected.Id + "'.");
                    ValidateCount(saved.CurrentCount, expected.RequiredCount, expected.Id.Value);
                    if (saved.CurrentCount > 0) state.Counts[expected.Id.Value] = saved.CurrentCount;
                }
            }

            state.Status = snapshot.State;
            state.Revision = snapshot.Revision;
            if (!string.Equals(state.ActiveNodeId, snapshot.ActiveStepId ?? string.Empty, StringComparison.Ordinal))
                throw new InvalidOperationException("Quest '" + definition.Id + "' active step does not match the saved step states.");
            ValidateRestoredEntry(definition.Id.Value, state, activeSteps);
        }

        private static void RestoreStandalone(
            ObjectiveDefinition definition,
            EntryState state,
            ObjectiveProgressSnapshot snapshot)
        {
            if (snapshot.RequiredCount != definition.RequiredCount)
                throw new InvalidOperationException("Standalone objective definition changed for '" + definition.Id + "'.");
            if (snapshot.State == ProgressionLifecycleState.Failed)
                throw new InvalidOperationException("Failed standalone objective state is not supported.");
            ValidateCount(snapshot.CurrentCount, definition.RequiredCount, definition.Id.Value);

            state.Status = snapshot.State;
            state.ActiveNodeId = snapshot.State == ProgressionLifecycleState.Active ? definition.Id.Value : string.Empty;
            state.Revision = snapshot.Revision;
            if (snapshot.CurrentCount > 0) state.Counts[definition.Id.Value] = snapshot.CurrentCount;
            if (snapshot.State == ProgressionLifecycleState.Completed) state.CompletedNodes.Add(definition.Id.Value);

            if (snapshot.State == ProgressionLifecycleState.Inactive && snapshot.CurrentCount != 0)
                throw new InvalidOperationException("Inactive standalone objective cannot have progress.");
            if (snapshot.State == ProgressionLifecycleState.Active && snapshot.CurrentCount >= definition.RequiredCount)
                throw new InvalidOperationException("Active standalone objective cannot already satisfy completion.");
            if (snapshot.State == ProgressionLifecycleState.Completed && snapshot.CurrentCount != definition.RequiredCount)
                throw new InvalidOperationException("Completed standalone objective must have its required count.");
        }

        private static void ValidateRestoredEntry(string id, EntryState state, int activeSteps)
        {
            if (state.Status == ProgressionLifecycleState.Active)
            {
                if (activeSteps != 1 || string.IsNullOrEmpty(state.ActiveNodeId))
                    throw new InvalidOperationException("Active quest '" + id + "' must have exactly one active step.");
                return;
            }

            if (activeSteps != 0 || !string.IsNullOrEmpty(state.ActiveNodeId))
                throw new InvalidOperationException("Inactive/completed quest '" + id + "' cannot have an active step.");
            if (state.Status == ProgressionLifecycleState.Inactive && state.CompletedNodes.Count != 0)
                throw new InvalidOperationException("Inactive quest '" + id + "' cannot contain completed steps.");
        }

        private void RequireUniqueEntry(string id)
        {
            if (_states.ContainsKey(id))
                throw new InvalidOperationException("Duplicate progression entry id '" + id + "'.");
        }

        private EntryState RequireState(string id)
        {
            string required = RequireId(id, "entry id");
            if (!_states.TryGetValue(required, out EntryState state))
                throw new InvalidOperationException("Unknown progression entry '" + id + "'.");
            return state;
        }

        private static EntryState NewState(ProgressionEntryKind kind) =>
            new EntryState { Kind = kind, Status = ProgressionLifecycleState.Inactive };

        private static QuestStepDefinition FindStep(QuestGraphDefinition definition, string stepId)
        {
            for (var i = 0; i < definition.Steps.Count; i++)
                if (string.Equals(definition.Steps[i].StepId, stepId, StringComparison.Ordinal))
                    return definition.Steps[i];
            throw new InvalidOperationException("Quest '" + definition.Id + "' references unknown step '" + stepId + "'.");
        }

        private static int GetCount(EntryState state, string key)
        {
            state.Counts.TryGetValue(key, out int count);
            return count;
        }

        private static int Increment(EntryState state, string key, int amount, int required)
        {
            int current = GetCount(state, key);
            long next = (long)current + amount;
            int value = (int)Math.Min(required, next);
            state.Counts[key] = value;
            return value;
        }

        private static bool StepComplete(EntryState state, QuestStepDefinition step)
        {
            for (var i = 0; i < step.Objectives.Count; i++)
                if (GetCount(state, step.Objectives[i].Id.Value) < step.Objectives[i].RequiredCount)
                    return false;
            return true;
        }

        private static void ValidateQuest(QuestGraphDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.Steps.Count == 0)
                throw new InvalidOperationException("Quest '" + definition.Id + "' requires at least one step.");

            var steps = new Dictionary<string, QuestStepDefinition>(StringComparer.Ordinal);
            var objectiveIds = new HashSet<ObjectiveId>();
            for (var i = 0; i < definition.Steps.Count; i++)
            {
                QuestStepDefinition step = definition.Steps[i];
                if (!steps.TryAdd(step.StepId, step))
                    throw new InvalidOperationException("Quest '" + definition.Id + "' contains duplicate step id '" + step.StepId + "'.");
                for (var o = 0; o < step.Objectives.Count; o++)
                {
                    ObjectiveDefinition objective = step.Objectives[o];
                    ValidateObjective(objective);
                    if (!objectiveIds.Add(objective.Id))
                        throw new InvalidOperationException("Quest '" + definition.Id + "' contains duplicate objective id '" + objective.Id + "'.");
                }
            }

            if (!steps.ContainsKey(definition.FirstStepId))
                throw new InvalidOperationException("Quest '" + definition.Id + "' first step is missing.");
            foreach (QuestStepDefinition step in steps.Values)
                if (!string.IsNullOrEmpty(step.NextStepId) && !steps.ContainsKey(step.NextStepId))
                    throw new InvalidOperationException("Quest '" + definition.Id + "' references missing next step '" + step.NextStepId + "'.");

            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            Visit(definition.FirstStepId, steps, visiting, visited, definition.Id.Value);
            if (visited.Count != steps.Count)
                throw new InvalidOperationException("Quest '" + definition.Id + "' contains unreachable steps.");
        }

        private static void Visit(
            string id,
            Dictionary<string, QuestStepDefinition> steps,
            HashSet<string> visiting,
            HashSet<string> visited,
            string questId)
        {
            if (visited.Contains(id)) return;
            if (!visiting.Add(id))
                throw new InvalidOperationException("Quest '" + questId + "' contains a cycle at step '" + id + "'.");
            QuestStepDefinition step = steps[id];
            if (!string.IsNullOrEmpty(step.NextStepId))
                Visit(step.NextStepId, steps, visiting, visited, questId);
            visiting.Remove(id);
            visited.Add(id);
        }

        private static void ValidateObjective(ObjectiveDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!definition.Id.IsValid) throw new InvalidOperationException("Progression objective id is required.");
            if (definition.RequiredCount <= 0)
                throw new InvalidOperationException("Progression objective '" + definition.Id + "' requires a positive count.");
            RequireId(definition.Condition.SubjectId, "condition subject id");
        }

        private static void ValidateCount(int current, int required, string id)
        {
            if (current < 0 || current > required)
                throw new InvalidOperationException("Progression objective '" + id + "' has invalid restored progress.");
        }

        private static string RequireId(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("Progression " + label + " is required.");
            return value;
        }
    }
}
