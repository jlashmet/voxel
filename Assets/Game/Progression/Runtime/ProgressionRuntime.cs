using System;
using System.Collections.Generic;
using Game.Progression;

namespace Game.Progression.Runtime
{
    public sealed class ProgressionConditionResolver : IProgressionCompletionConditionResolver
    {
        public bool Matches(ProgressionCondition condition, ProgressionUpdateSignal signal)
        {
            if (condition.Kind == ProgressionConditionKind.Always) return true;
            if (string.IsNullOrWhiteSpace(condition.SubjectId)) return false;
            if (!string.Equals(condition.SubjectId, signal.SubjectId, StringComparison.Ordinal)) return false;
            switch (condition.Kind)
            {
                case ProgressionConditionKind.Event: return signal.Kind == ProgressionSignalKind.Event;
                case ProgressionConditionKind.NpcInteraction: return signal.Kind == ProgressionSignalKind.NpcInteracted;
                case ProgressionConditionKind.Interaction: return signal.Kind == ProgressionSignalKind.Interacted;
                default: return false;
            }
        }
    }

    public sealed class ProgressionRuntime : IProgressionRuntime, IProgressionSink
    {
        private sealed class EntryState
        {
            public ProgressionEntryKind Kind;
            public ProgressionNodeStatus Status;
            public string ActiveNodeId = string.Empty;
            public readonly HashSet<string> CompletedNodes = new HashSet<string>(StringComparer.Ordinal);
            public readonly Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private readonly Dictionary<string, QuestGraphDefinition> _quests = new Dictionary<string, QuestGraphDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, StandaloneObjectiveDefinition> _standalone = new Dictionary<string, StandaloneObjectiveDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, EntryState> _states = new Dictionary<string, EntryState>(StringComparer.Ordinal);
        private readonly List<string> _orderedIds = new List<string>();
        private readonly HashSet<string> _appliedOperations = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _emittedRewards = new HashSet<string>(StringComparer.Ordinal);
        private readonly IProgressionCompletionConditionResolver _resolver;
        private long _compatibilitySequence;

        public ProgressionRuntime(IProgressionCompletionConditionResolver resolver = null)
        {
            _resolver = resolver ?? new ProgressionConditionResolver();
        }

        public void RegisterQuest(QuestGraphDefinition definition)
        {
            ValidateQuest(definition);
            RequireUniqueEntry(definition.QuestId);
            _quests.Add(definition.QuestId, definition);
            _states.Add(definition.QuestId, NewState(ProgressionEntryKind.Quest));
            _orderedIds.Add(definition.QuestId);
        }

        public void RegisterStandaloneObjective(StandaloneObjectiveDefinition definition)
        {
            ValidateStandalone(definition);
            RequireUniqueEntry(definition.ObjectiveId);
            _standalone.Add(definition.ObjectiveId, definition);
            _states.Add(definition.ObjectiveId, NewState(ProgressionEntryKind.StandaloneObjective));
            _orderedIds.Add(definition.ObjectiveId);
        }

        public ProgressionUpdateResult Start(string entryId, string operationId = "")
        {
            EntryState state = RequireState(entryId);
            string op = ResolveOperation(operationId, "start");
            ProgressionUpdateResult replay;
            if (TryReplay(op, out replay)) return replay;
            var transitions = new List<ProgressionTransition>();
            if (state.Status == ProgressionNodeStatus.Completed)
                return Record(op, ProgressionApplyStatus.Rejected, transitions, "Completed progression entries cannot be restarted.");
            if (state.Status == ProgressionNodeStatus.Active)
                return Record(op, ProgressionApplyStatus.Applied, transitions, string.Empty);

            state.Status = ProgressionNodeStatus.Active;
            transitions.Add(new ProgressionTransition(ProgressionTransitionKind.EntryStarted, entryId, string.Empty));
            if (state.Kind == ProgressionEntryKind.Quest)
            {
                QuestGraphDefinition definition = _quests[entryId];
                ActivateStep(entryId, state, definition, definition.FirstStepId, transitions);
            }
            else
            {
                state.ActiveNodeId = entryId;
                transitions.Add(new ProgressionTransition(ProgressionTransitionKind.NodeActivated, entryId, entryId));
            }
            return Record(op, ProgressionApplyStatus.Applied, transitions, string.Empty);
        }

        public ProgressionUpdateResult Observe(ProgressionUpdateSignal signal)
        {
            if (signal.Amount <= 0 || string.IsNullOrWhiteSpace(signal.SubjectId))
                return new ProgressionUpdateResult(ProgressionApplyStatus.Rejected, Array.Empty<ProgressionTransition>(), "Observation subject and positive amount are required.");
            string op = ResolveOperation(signal.OperationId, "observe");
            ProgressionUpdateResult replay;
            if (TryReplay(op, out replay)) return replay;
            var transitions = new List<ProgressionTransition>();
            for (var i = 0; i < _orderedIds.Count; i++)
            {
                string id = _orderedIds[i];
                EntryState state = _states[id];
                if (state.Status != ProgressionNodeStatus.Active) continue;
                if (state.Kind == ProgressionEntryKind.StandaloneObjective)
                    ObserveStandalone(id, state, _standalone[id], signal, transitions);
                else
                    ObserveQuest(id, state, _quests[id], signal, transitions);
            }
            return Record(op, ProgressionApplyStatus.Applied, transitions, string.Empty);
        }

        ProgressionApplyResult IProgressionSink.Observe(ProgressionObservation observation)
        {
            ProgressionSignalKind kind = ProgressionSignalKind.Event;
            var result = Observe(new ProgressionUpdateSignal(observation.OperationId, kind, observation.EventId, observation.Amount));
            return new ProgressionApplyResult(result.Status, result.Reason);
        }

        public ProgressionUpdateResult ForceComplete(string entryId, string operationId = "")
        {
            EntryState state = RequireState(entryId);
            string op = ResolveOperation(operationId, "complete");
            ProgressionUpdateResult replay;
            if (TryReplay(op, out replay)) return replay;
            var transitions = new List<ProgressionTransition>();
            if (state.Status != ProgressionNodeStatus.Active)
                return Record(op, ProgressionApplyStatus.Rejected, transitions, "Only active progression entries can be completed.");
            if (state.Kind == ProgressionEntryKind.Quest && !string.IsNullOrEmpty(state.ActiveNodeId))
            {
                string node = state.ActiveNodeId;
                state.CompletedNodes.Add(node);
                transitions.Add(new ProgressionTransition(ProgressionTransitionKind.NodeCompleted, entryId, node));
            }
            CompleteEntry(entryId, state, transitions);
            return Record(op, ProgressionApplyStatus.Applied, transitions, string.Empty);
        }

        public ProgressionEntrySnapshot GetSnapshot(string entryId)
        {
            EntryState state = RequireState(entryId);
            var nodes = new List<string>(state.CompletedNodes);
            nodes.Sort(StringComparer.Ordinal);
            var counts = new Dictionary<string, int>(state.Counts, StringComparer.Ordinal);
            return new ProgressionEntrySnapshot(entryId, state.Kind, state.Status, state.ActiveNodeId, nodes, counts);
        }

        public ProgressionStateSnapshot CaptureState()
        {
            var entries = new List<ProgressionEntrySnapshot>();
            for (var i = 0; i < _orderedIds.Count; i++) entries.Add(GetSnapshot(_orderedIds[i]));
            var operations = new List<string>(_appliedOperations); operations.Sort(StringComparer.Ordinal);
            var rewards = new List<string>(_emittedRewards); rewards.Sort(StringComparer.Ordinal);
            return new ProgressionStateSnapshot(entries, operations, rewards, _compatibilitySequence);
        }

        public void RestoreState(ProgressionStateSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            Reset();
            for (var i = 0; i < snapshot.Entries.Count; i++)
            {
                ProgressionEntrySnapshot item = snapshot.Entries[i];
                EntryState state = RequireState(item.EntryId);
                if (state.Kind != item.Kind) throw new InvalidOperationException("Progression entry kind changed for '" + item.EntryId + "'.");
                state.Status = item.Status;
                state.ActiveNodeId = item.ActiveNodeId ?? string.Empty;
                for (var n = 0; n < item.CompletedNodeIds.Count; n++) state.CompletedNodes.Add(item.CompletedNodeIds[n]);
                foreach (var pair in item.ObjectiveCounts) state.Counts[pair.Key] = pair.Value;
                ValidateRestoredState(item.EntryId, state);
            }
            for (var i = 0; i < snapshot.AppliedOperationIds.Count; i++)
                _appliedOperations.Add(RequireId(snapshot.AppliedOperationIds[i], "operation id"));
            for (var i = 0; i < snapshot.EmittedRewardIds.Count; i++)
                _emittedRewards.Add(RequireId(snapshot.EmittedRewardIds[i], "reward id"));
            if (snapshot.CompatibilitySequence < 0) throw new InvalidOperationException("Progression compatibility sequence cannot be negative.");
            _compatibilitySequence = snapshot.CompatibilitySequence;
        }

        public void Reset()
        {
            foreach (EntryState state in _states.Values)
            {
                state.Status = ProgressionNodeStatus.Inactive;
                state.ActiveNodeId = string.Empty;
                state.CompletedNodes.Clear();
                state.Counts.Clear();
            }
            _appliedOperations.Clear();
            _emittedRewards.Clear();
            _compatibilitySequence = 0;
        }

        private void ObserveStandalone(string id, EntryState state, StandaloneObjectiveDefinition definition, ProgressionUpdateSignal signal, List<ProgressionTransition> transitions)
        {
            if (!_resolver.Matches(definition.Condition, signal)) return;
            int count = Increment(state, id, signal.Amount, definition.RequiredCount);
            if (count < definition.RequiredCount) return;
            state.CompletedNodes.Add(id);
            transitions.Add(new ProgressionTransition(ProgressionTransitionKind.NodeCompleted, id, id));
            EmitReward(id, id, definition.RewardId, transitions);
            CompleteEntry(id, state, transitions);
        }

        private void ObserveQuest(string id, EntryState state, QuestGraphDefinition definition, ProgressionUpdateSignal signal, List<ProgressionTransition> transitions)
        {
            QuestStepDefinition step = FindStep(definition, state.ActiveNodeId);
            bool changed = false;
            for (var i = 0; i < step.Objectives.Count; i++)
            {
                ObjectiveDefinition objective = step.Objectives[i];
                if (!_resolver.Matches(objective.Condition, signal)) continue;
                int count = Increment(state, ObjectiveKey(step.StepId, objective.ObjectiveId), signal.Amount, objective.RequiredCount);
                if (count >= objective.RequiredCount) EmitReward(id, objective.ObjectiveId, objective.RewardId, transitions);
                changed = true;
            }
            if (!changed || !StepComplete(state, step)) return;
            state.CompletedNodes.Add(step.StepId);
            transitions.Add(new ProgressionTransition(ProgressionTransitionKind.NodeCompleted, id, step.StepId));
            if (step.NextStepIds.Count == 0) CompleteEntry(id, state, transitions);
            else ActivateStep(id, state, definition, step.NextStepIds[0], transitions);
        }

        private void ActivateStep(string entryId, EntryState state, QuestGraphDefinition definition, string stepId, List<ProgressionTransition> transitions)
        {
            var guard = new HashSet<string>(StringComparer.Ordinal);
            string current = stepId;
            while (true)
            {
                if (!guard.Add(current)) throw new InvalidOperationException("Progression graph entered a cycle at '" + current + "'.");
                QuestStepDefinition step = FindStep(definition, current);
                state.ActiveNodeId = current;
                transitions.Add(new ProgressionTransition(ProgressionTransitionKind.NodeActivated, entryId, current));
                if (step.Objectives.Count != 0) return;
                state.CompletedNodes.Add(current);
                transitions.Add(new ProgressionTransition(ProgressionTransitionKind.NodeCompleted, entryId, current));
                if (step.NextStepIds.Count == 0) { CompleteEntry(entryId, state, transitions); return; }
                current = step.NextStepIds[0];
            }
        }

        private void CompleteEntry(string id, EntryState state, List<ProgressionTransition> transitions)
        {
            state.Status = ProgressionNodeStatus.Completed;
            state.ActiveNodeId = string.Empty;
            transitions.Add(new ProgressionTransition(ProgressionTransitionKind.EntryCompleted, id, string.Empty));
        }

        private void EmitReward(string entryId, string nodeId, string rewardId, List<ProgressionTransition> transitions)
        {
            if (string.IsNullOrWhiteSpace(rewardId)) return;
            string key = entryId + ":" + nodeId + ":" + rewardId;
            if (_emittedRewards.Add(key))
                transitions.Add(new ProgressionTransition(ProgressionTransitionKind.RewardEmitted, entryId, nodeId, rewardId));
        }

        private static int Increment(EntryState state, string key, int amount, int required)
        {
            int current; state.Counts.TryGetValue(key, out current);
            long next = (long)current + amount;
            int value = (int)Math.Min(required, next);
            state.Counts[key] = value;
            return value;
        }

        private static bool StepComplete(EntryState state, QuestStepDefinition step)
        {
            for (var i = 0; i < step.Objectives.Count; i++)
            {
                ObjectiveDefinition objective = step.Objectives[i];
                int count; state.Counts.TryGetValue(ObjectiveKey(step.StepId, objective.ObjectiveId), out count);
                if (count < objective.RequiredCount) return false;
            }
            return true;
        }

        private ProgressionUpdateResult Record(string operationId, ProgressionApplyStatus status, List<ProgressionTransition> transitions, string reason)
        {
            _appliedOperations.Add(operationId);
            return new ProgressionUpdateResult(status, transitions.ToArray(), reason);
        }

        private bool TryReplay(string operationId, out ProgressionUpdateResult result)
        {
            if (_appliedOperations.Contains(operationId))
            {
                result = new ProgressionUpdateResult(ProgressionApplyStatus.Replay, Array.Empty<ProgressionTransition>(), "Operation already applied.");
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

        private void RequireUniqueEntry(string id)
        {
            if (_states.ContainsKey(id)) throw new InvalidOperationException("Duplicate progression entry id '" + id + "'.");
        }

        private EntryState RequireState(string id)
        {
            EntryState state;
            if (!_states.TryGetValue(RequireId(id, "entry id"), out state)) throw new InvalidOperationException("Unknown progression entry '" + id + "'.");
            return state;
        }

        private static EntryState NewState(ProgressionEntryKind kind) => new EntryState { Kind = kind, Status = ProgressionNodeStatus.Inactive };
        private static string ObjectiveKey(string step, string objective) => step + "/" + objective;

        private static QuestStepDefinition FindStep(QuestGraphDefinition definition, string stepId)
        {
            for (var i = 0; i < definition.Steps.Count; i++) if (string.Equals(definition.Steps[i].StepId, stepId, StringComparison.Ordinal)) return definition.Steps[i];
            throw new InvalidOperationException("Quest '" + definition.QuestId + "' references unknown step '" + stepId + "'.");
        }

        private static void ValidateQuest(QuestGraphDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            string id = RequireId(definition.QuestId, "quest id");
            RequireId(definition.FirstStepId, "first step id");
            if (definition.Steps.Count == 0) throw new InvalidOperationException("Quest '" + id + "' requires at least one step.");
            var steps = new Dictionary<string, QuestStepDefinition>(StringComparer.Ordinal);
            for (var i = 0; i < definition.Steps.Count; i++)
            {
                QuestStepDefinition step = definition.Steps[i] ?? throw new InvalidOperationException("Quest '" + id + "' contains a null step.");
                string stepId = RequireId(step.StepId, "step id");
                if (steps.ContainsKey(stepId)) throw new InvalidOperationException("Quest '" + id + "' contains duplicate step id '" + stepId + "'.");
                steps.Add(stepId, step);
                var objectiveIds = new HashSet<string>(StringComparer.Ordinal);
                for (var o = 0; o < step.Objectives.Count; o++)
                {
                    ObjectiveDefinition objective = step.Objectives[o] ?? throw new InvalidOperationException("Quest step '" + stepId + "' contains a null objective.");
                    ValidateObjective(objective.ObjectiveId, objective.Condition, objective.RequiredCount, objective.RewardId);
                    if (!objectiveIds.Add(objective.ObjectiveId)) throw new InvalidOperationException("Quest step '" + stepId + "' contains duplicate objective id '" + objective.ObjectiveId + "'.");
                }
            }
            if (!steps.ContainsKey(definition.FirstStepId)) throw new InvalidOperationException("Quest '" + id + "' first step is missing.");
            foreach (QuestStepDefinition step in steps.Values)
                for (var i = 0; i < step.NextStepIds.Count; i++)
                    if (!steps.ContainsKey(RequireId(step.NextStepIds[i], "next step id"))) throw new InvalidOperationException("Quest '" + id + "' references missing next step '" + step.NextStepIds[i] + "'.");
            var visiting = new HashSet<string>(StringComparer.Ordinal); var visited = new HashSet<string>(StringComparer.Ordinal);
            Visit(definition.FirstStepId, steps, visiting, visited, id);
            if (visited.Count != steps.Count) throw new InvalidOperationException("Quest '" + id + "' contains unreachable steps.");
        }

        private static void Visit(string id, Dictionary<string, QuestStepDefinition> steps, HashSet<string> visiting, HashSet<string> visited, string questId)
        {
            if (visited.Contains(id)) return;
            if (!visiting.Add(id)) throw new InvalidOperationException("Quest '" + questId + "' contains a cycle at step '" + id + "'.");
            QuestStepDefinition step = steps[id];
            for (var i = 0; i < step.NextStepIds.Count; i++) Visit(step.NextStepIds[i], steps, visiting, visited, questId);
            visiting.Remove(id); visited.Add(id);
        }

        private static void ValidateStandalone(StandaloneObjectiveDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            ValidateObjective(definition.ObjectiveId, definition.Condition, definition.RequiredCount, definition.RewardId);
        }

        private static void ValidateObjective(string id, ProgressionCondition condition, int requiredCount, string rewardId)
        {
            RequireId(id, "objective id");
            if (requiredCount <= 0) throw new InvalidOperationException("Progression objective '" + id + "' requires a positive count.");
            if (condition.Kind != ProgressionConditionKind.Always) RequireId(condition.SubjectId, "completion condition subject id");
            if (!string.IsNullOrWhiteSpace(rewardId) && condition.Kind == ProgressionConditionKind.Always)
                throw new InvalidOperationException("Reward-bearing objective '" + id + "' must have a gated completion condition.");
        }

        private void ValidateRestoredState(string id, EntryState state)
        {
            if (state.Status == ProgressionNodeStatus.Active && string.IsNullOrEmpty(state.ActiveNodeId)) throw new InvalidOperationException("Active progression entry '" + id + "' is missing an active node.");
            if (state.Status != ProgressionNodeStatus.Active && !string.IsNullOrEmpty(state.ActiveNodeId)) throw new InvalidOperationException("Inactive/completed progression entry '" + id + "' cannot have an active node.");
        }

        private static string RequireId(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Progression " + label + " is required.");
            return value;
        }
    }
}
