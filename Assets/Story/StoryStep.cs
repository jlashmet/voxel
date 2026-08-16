using System;
using System.Collections.Generic;

namespace MountingForce.Story
{
    public readonly struct StoryStep
    {
        private static readonly StoryStep[] EmptyChildren = new StoryStep[0];
        private readonly StoryStep[] _children;

        public StoryStepType Type { get; }
        public StoryActorId Actor { get; }
        public StoryActorId TargetActor { get; }
        public StoryStagePointId StagePoint { get; }
        public StoryCueId Cue { get; }
        public int DurationMilliseconds { get; }
        public IReadOnlyList<StoryStep> Children => _children ?? EmptyChildren;

        public StoryStep(StoryStepType type, StoryActorId actor, StoryActorId targetActor,
            StoryStagePointId stagePoint, StoryCueId cue, int durationMilliseconds)
        {
            if (type == StoryStepType.Parallel)
                throw new ArgumentException("Use StoryStep.Parallel to create parallel story work.", nameof(type));

            Type = type;
            Actor = actor;
            TargetActor = targetActor;
            StagePoint = stagePoint;
            Cue = cue;
            DurationMilliseconds = durationMilliseconds;
            _children = null;
        }

        private StoryStep(StoryStep[] children)
        {
            Type = StoryStepType.Parallel;
            Actor = default;
            TargetActor = default;
            StagePoint = default;
            Cue = default;
            DurationMilliseconds = 0;
            _children = children;
        }

        public static StoryStep Parallel(params StoryStep[] children)
        {
            if (children == null) throw new ArgumentNullException(nameof(children));
            if (children.Length == 0) throw new ArgumentException("Parallel story work must contain at least one child step.", nameof(children));

            var copy = new StoryStep[children.Length];
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].Type == StoryStepType.Wait)
                    throw new ArgumentException("Wait steps cannot be children of a parallel story step.", nameof(children));
                copy[i] = children[i];
            }
            return new StoryStep(copy);
        }
    }
}
