using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MountingForce.Story.Tests
{
    public sealed class StoryExecutionPreflightTests
    {
        [Test]
        public void MissingSequenceStagePointFailsBeforeSetupMutation()
        {
            var lead = new StoryActorId("lead");
            var start = new StoryStagePointId("start");
            var missing = new StoryStagePointId("missing");
            var setup = new StoryStageSetupDefinition(new[]
            {
                new StoryActorPlacement(lead, start)
            });
            var sequence = new StorySequenceDefinition("preflight-missing-stage", new[]
            {
                new StoryStep(StoryStepType.MoveActor, lead, default, missing, default, 100)
            });
            var stage = new StoryStageBinding()
                .Bind(start, new StoryStagePoint(new StoryInt3(1, 2, 3), default));
            var actors = new RecordingActorController(lead);

            Assert.Throws<InvalidOperationException>(() =>
                StoryExecutionPreflight.Validate(setup, sequence, actors, stage));
            Assert.AreEqual(0, actors.PlaceAtCalls);
        }

        [Test]
        public void MissingActorInsideParallelBeatIsDetectedRecursively()
        {
            var lead = new StoryActorId("lead");
            var missing = new StoryActorId("missing");
            var setup = new StoryStageSetupDefinition(new StoryActorPlacement[0]);
            var sequence = new StorySequenceDefinition("preflight-parallel", new[]
            {
                StoryStep.Parallel(
                    new StoryStep(StoryStepType.FaceActor, lead, missing, default, default, 0))
            });
            var actors = new RecordingActorController(lead);

            Assert.Throws<InvalidOperationException>(() =>
                StoryExecutionPreflight.Validate(setup, sequence, actors, new StoryStageBinding()));
        }

        [Test]
        public void NegativeWaitDurationIsRejected()
        {
            var setup = new StoryStageSetupDefinition(new StoryActorPlacement[0]);
            var sequence = new StorySequenceDefinition("preflight-duration", new[]
            {
                new StoryStep(StoryStepType.Wait, default, default, default, default, -1)
            });

            Assert.Throws<InvalidOperationException>(() =>
                StoryExecutionPreflight.Validate(setup, sequence, new RecordingActorController(), new StoryStageBinding()));
        }

        [Test]
        public void ValidDependenciesPassWithoutMutatingActors()
        {
            var lead = new StoryActorId("lead");
            var start = new StoryStagePointId("start");
            var setup = new StoryStageSetupDefinition(new[]
            {
                new StoryActorPlacement(lead, start)
            });
            var sequence = new StorySequenceDefinition("preflight-valid", new[]
            {
                new StoryStep(StoryStepType.FacePoint, lead, default, start, default, 0),
                new StoryStep(StoryStepType.Dialogue, default, default, default, new StoryCueId("dialogue.test"), 0)
            });
            var stage = new StoryStageBinding()
                .Bind(start, new StoryStagePoint(new StoryInt3(4, 5, 6), default));
            var actors = new RecordingActorController(lead);

            Assert.DoesNotThrow(() => StoryExecutionPreflight.Validate(setup, sequence, actors, stage));
            Assert.AreEqual(0, actors.PlaceAtCalls);
        }

        private sealed class RecordingActorController : IStoryActorController
        {
            private readonly HashSet<StoryActorId> _actors;
            public int PlaceAtCalls { get; private set; }

            public RecordingActorController(params StoryActorId[] actors)
            {
                _actors = new HashSet<StoryActorId>(actors);
            }

            public bool Contains(StoryActorId actor) => _actors.Contains(actor);

            public void PlaceAt(StoryActorId actor, StoryStagePoint destination)
            {
                PlaceAtCalls++;
            }

            public IStoryOperation MoveTo(StoryActorId actor, StoryStagePoint destination, int durationHintMilliseconds)
                => CompletedStoryOperation.Instance;

            public IStoryOperation FaceActor(StoryActorId actor, StoryActorId target)
                => CompletedStoryOperation.Instance;

            public IStoryOperation FacePoint(StoryActorId actor, StoryStagePoint target)
                => CompletedStoryOperation.Instance;
        }
    }
}
