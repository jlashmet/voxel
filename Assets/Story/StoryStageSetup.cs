using System;
using System.Collections.Generic;

namespace MountingForce.Story
{
    public readonly struct StoryActorPlacement
    {
        public StoryActorId Actor { get; }
        public StoryStagePointId StagePoint { get; }

        public StoryActorPlacement(StoryActorId actor, StoryStagePointId stagePoint)
        {
            if (string.IsNullOrWhiteSpace(actor.Value))
                throw new ArgumentException("Story setup actor id cannot be empty.", nameof(actor));
            if (string.IsNullOrWhiteSpace(stagePoint.Value))
                throw new ArgumentException("Story setup stage point id cannot be empty.", nameof(stagePoint));

            Actor = actor;
            StagePoint = stagePoint;
        }
    }

    /// <summary>
    /// Deterministic pre-sequence placement. Setup establishes the authored stage pose; it is not
    /// choreography and therefore does not consume sequence time or become a skippable story step.
    /// </summary>
    public sealed class StoryStageSetupDefinition
    {
        private readonly StoryActorPlacement[] _placements;
        public IReadOnlyList<StoryActorPlacement> Placements => _placements;

        public StoryStageSetupDefinition(IEnumerable<StoryActorPlacement> placements)
        {
            if (placements == null) throw new ArgumentNullException(nameof(placements));

            var copy = new List<StoryActorPlacement>();
            var actors = new HashSet<StoryActorId>();
            foreach (StoryActorPlacement placement in placements)
            {
                if (!actors.Add(placement.Actor))
                    throw new ArgumentException("Story setup contains actor more than once: " + placement.Actor, nameof(placements));
                copy.Add(placement);
            }
            _placements = copy.ToArray();
        }
    }

    public static class StoryStageSetup
    {
        public static void Validate(StoryStageSetupDefinition definition, IStoryActorController actors, StoryStageBinding stage)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (actors == null) throw new ArgumentNullException(nameof(actors));
            if (stage == null) throw new ArgumentNullException(nameof(stage));

            for (int i = 0; i < definition.Placements.Count; i++)
            {
                StoryActorPlacement placement = definition.Placements[i];
                if (!actors.Contains(placement.Actor))
                    throw new InvalidOperationException("Story setup actor '" + placement.Actor + "' is not registered.");
                if (!stage.TryResolve(placement.StagePoint, out _))
                    throw new InvalidOperationException("Story setup stage point '" + placement.StagePoint + "' is not bound.");
            }
        }

        public static void Apply(StoryStageSetupDefinition definition, IStoryActorController actors, StoryStageBinding stage)
        {
            Validate(definition, actors, stage);

            for (int i = 0; i < definition.Placements.Count; i++)
            {
                StoryActorPlacement placement = definition.Placements[i];
                actors.PlaceAt(placement.Actor, stage.Resolve(placement.StagePoint));
            }
        }
    }
}
