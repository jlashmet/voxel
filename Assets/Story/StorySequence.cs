using System;
using System.Collections.Generic;

namespace MountingForce.Story
{
    public sealed class StorySequenceDefinition
    {
        private readonly StoryStep[] _steps;
        public string Id { get; }
        public IReadOnlyList<StoryStep> Steps => _steps;

        public StorySequenceDefinition(string id, IEnumerable<StoryStep> steps)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Sequence id cannot be empty.", nameof(id));
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            Id = id;
            _steps = new List<StoryStep>(steps).ToArray();
        }
    }
}
