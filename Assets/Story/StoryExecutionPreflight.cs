using System;

namespace MountingForce.Story
{
    /// <summary>
    /// Non-mutating validation performed before stage setup. It verifies deterministic gameplay
    /// dependencies only; presentation cue availability is owned by the presentation layer.
    /// </summary>
    public static class StoryExecutionPreflight
    {
        public static void Validate(
            StoryStageSetupDefinition setup,
            StorySequenceDefinition sequence,
            IStoryActorController actors,
            StoryStageBinding stage)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            if (actors == null) throw new ArgumentNullException(nameof(actors));
            if (stage == null) throw new ArgumentNullException(nameof(stage));

            StoryStageSetup.Validate(setup, actors, stage);
            for (int i = 0; i < sequence.Steps.Count; i++)
                ValidateStep(sequence.Steps[i], actors, stage, sequence.Id + "[" + i + "]");
        }

        private static void ValidateStep(
            StoryStep step,
            IStoryActorController actors,
            StoryStageBinding stage,
            string path)
        {
            switch (step.Type)
            {
                case StoryStepType.Wait:
                    RequireNonNegativeDuration(step.DurationMilliseconds, path);
                    return;

                case StoryStepType.MoveActor:
                    RequireActor(step.Actor, actors, path, "actor");
                    RequireStagePoint(step.StagePoint, stage, path);
                    RequireNonNegativeDuration(step.DurationMilliseconds, path);
                    return;

                case StoryStepType.FaceActor:
                    RequireActor(step.Actor, actors, path, "actor");
                    RequireActor(step.TargetActor, actors, path, "target actor");
                    return;

                case StoryStepType.FacePoint:
                    RequireActor(step.Actor, actors, path, "actor");
                    RequireStagePoint(step.StagePoint, stage, path);
                    return;

                case StoryStepType.Dialogue:
                case StoryStepType.Camera:
                case StoryStepType.Sound:
                    if (string.IsNullOrWhiteSpace(step.Cue.Value))
                        throw new InvalidOperationException("Story step " + path + " has no cue id.");
                    return;

                case StoryStepType.Parallel:
                    if (step.Children.Count == 0)
                        throw new InvalidOperationException("Parallel story step " + path + " has no children.");
                    for (int i = 0; i < step.Children.Count; i++)
                        ValidateStep(step.Children[i], actors, stage, path + "/parallel[" + i + "]");
                    return;

                default:
                    throw new InvalidOperationException("Unsupported story step type at " + path + ": " + step.Type + ".");
            }
        }

        private static void RequireActor(StoryActorId actor, IStoryActorController actors, string path, string role)
        {
            if (string.IsNullOrWhiteSpace(actor.Value))
                throw new InvalidOperationException("Story step " + path + " has no " + role + " id.");
            if (!actors.Contains(actor))
                throw new InvalidOperationException("Story step " + path + " requires unregistered " + role + " '" + actor + "'.");
        }

        private static void RequireStagePoint(StoryStagePointId point, StoryStageBinding stage, string path)
        {
            if (string.IsNullOrWhiteSpace(point.Value))
                throw new InvalidOperationException("Story step " + path + " has no stage point id.");
            if (!stage.TryResolve(point, out _))
                throw new InvalidOperationException("Story step " + path + " requires unbound stage point '" + point + "'.");
        }

        private static void RequireNonNegativeDuration(int durationMilliseconds, string path)
        {
            if (durationMilliseconds < 0)
                throw new InvalidOperationException("Story step " + path + " has a negative duration.");
        }
    }
}
