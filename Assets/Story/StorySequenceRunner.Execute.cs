using System;
using System.Collections.Generic;

namespace MountingForce.Story
{
    public sealed partial class StorySequenceRunner
    {
        private IStoryOperation Execute(StoryStep step)
        {
            switch (step.Type)
            {
                case StoryStepType.MoveActor:
                    return _context.Actors.MoveTo(step.Actor, _context.Stage.Resolve(step.StagePoint), step.DurationMilliseconds);
                case StoryStepType.FaceActor:
                    return _context.Actors.FaceActor(step.Actor, step.TargetActor);
                case StoryStepType.FacePoint:
                    return _context.Actors.FacePoint(step.Actor, _context.Stage.Resolve(step.StagePoint));
                case StoryStepType.Dialogue:
                    return _context.Presentation.ShowDialogue(step.Cue);
                case StoryStepType.Camera:
                    return _context.Presentation.SetCamera(step.Cue);
                case StoryStepType.Sound:
                    return _context.Presentation.PlaySound(step.Cue);
                case StoryStepType.Parallel:
                    return ExecuteParallel(step);
                default:
                    throw new InvalidOperationException("Unsupported story step " + step.Type + ".");
            }
        }

        private IStoryOperation ExecuteParallel(StoryStep step)
        {
            IReadOnlyList<StoryStep> children = step.Children;
            if (children.Count == 0)
                throw new InvalidOperationException("Parallel story work must contain at least one child step.");

            var operations = new IStoryOperation[children.Count];
            for (int i = 0; i < children.Count; i++)
            {
                StoryStep child = children[i];
                if (child.Type == StoryStepType.Wait)
                    throw new InvalidOperationException("Wait steps cannot execute inside parallel story work.");

                operations[i] = Execute(child) ??
                    throw new InvalidOperationException("Story adapter returned a null operation for parallel child " + i + ".");
            }
            return new StoryParallelOperation(operations);
        }

        private sealed class StoryParallelOperation : IStoryOperation
        {
            private readonly IStoryOperation[] _operations;

            public StoryParallelOperation(IStoryOperation[] operations)
            {
                _operations = operations ?? throw new ArgumentNullException(nameof(operations));
            }

            public bool IsComplete
            {
                get
                {
                    for (int i = 0; i < _operations.Length; i++)
                    {
                        if (!_operations[i].IsComplete) return false;
                    }
                    return true;
                }
            }
        }
    }
}
