using System;
using System.Collections.Generic;
using Game.Progression;

namespace Game.Progression.Runtime
{
    public sealed class QuestGraphRegistry : IReadOnlyQuestGraphRegistry
    {
        private readonly Dictionary<string, QuestGraphDefinition> _items = new Dictionary<string, QuestGraphDefinition>(StringComparer.Ordinal);

        public QuestGraphRegistry(IReadOnlyList<QuestGraphDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            var validator = new ProgressionRuntime();
            for (var i = 0; i < definitions.Count; i++)
            {
                QuestGraphDefinition definition = definitions[i] ?? throw new InvalidOperationException("Quest registry contains null at index " + i + ".");
                validator.RegisterQuest(definition);
                if (_items.ContainsKey(definition.QuestId)) throw new InvalidOperationException("Duplicate quest id '" + definition.QuestId + "'.");
                _items.Add(definition.QuestId, definition);
            }
        }

        public bool TryGet(string questId, out QuestGraphDefinition definition) => _items.TryGetValue(questId, out definition);
    }

    public sealed class StandaloneObjectiveRegistry : IReadOnlyStandaloneObjectiveRegistry
    {
        private readonly Dictionary<string, StandaloneObjectiveDefinition> _items = new Dictionary<string, StandaloneObjectiveDefinition>(StringComparer.Ordinal);

        public StandaloneObjectiveRegistry(IReadOnlyList<StandaloneObjectiveDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            var validator = new ProgressionRuntime();
            for (var i = 0; i < definitions.Count; i++)
            {
                StandaloneObjectiveDefinition definition = definitions[i] ?? throw new InvalidOperationException("Standalone objective registry contains null at index " + i + ".");
                validator.RegisterStandaloneObjective(definition);
                if (_items.ContainsKey(definition.ObjectiveId)) throw new InvalidOperationException("Duplicate standalone objective id '" + definition.ObjectiveId + "'.");
                _items.Add(definition.ObjectiveId, definition);
            }
        }

        public bool TryGet(string objectiveId, out StandaloneObjectiveDefinition definition) => _items.TryGetValue(objectiveId, out definition);
    }
}
