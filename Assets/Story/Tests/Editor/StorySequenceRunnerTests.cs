using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MountingForce.Story.Tests
{
    public sealed class StorySequenceRunnerTests
    {
        [Test]
        public void StoryDurationsUseIntegerMilliseconds()
        {
            var step = new StoryStep(StoryStepType.Wait, default, default, default, default, 500);
            Assert.AreEqual(500, step.DurationMilliseconds);
        }

        [Test]
        public void ParallelStepStartsAllChildrenAndWaitsForEveryCompletion()
        {
            var first = new ManualStoryOperation();
            var second = new ManualStoryOperation();
            var actors = new RecordingActorController(first, second);
            var runner = new StorySequenceRunner();
            var sequence = new StorySequenceDefinition("parallel-test", new[]
            {
                StoryStep.Parallel(
                    new StoryStep(StoryStepType.FaceActor, new StoryActorId("madeline"), new StoryActorId("lead"), default, default, 0),
                    new StoryStep(StoryStepType.FaceActor, new StoryActorId("steven"), new StoryActorId("lead"), default, default, 0))
            });

            runner.Start(sequence, new StoryExecutionContext(actors, new NoOpPresentation(), new StoryStageBinding()));
            runner.Tick(0);

            Assert.AreEqual(2, actors.FaceActorCalls);
            Assert.AreEqual(0, runner.CurrentStepIndex);
            Assert.IsTrue(runner.IsRunning);

            first.Complete();
            runner.Tick(0);
            Assert.AreEqual(0, runner.CurrentStepIndex);
            Assert.IsTrue(runner.IsRunning);

            second.Complete();
            runner.Tick(0);
            Assert.AreEqual(1, runner.CurrentStepIndex);
            Assert.IsTrue(runner.IsComplete);
            Assert.IsFalse(runner.IsRunning);
        }

        [Test]
        public void ParallelStepRejectsWaitChildren()
        {
            var wait = new StoryStep(StoryStepType.Wait, default, default, default, default, 10);
            Assert.Throws<ArgumentException>(() => StoryStep.Parallel(wait));
        }

        private sealed class ManualStoryOperation : IStoryOperation
        {
            public bool IsComplete { get; private set; }
            public void Complete() => IsComplete = true;
        }

        private sealed class RecordingActorController : IStoryActorController
        {
            private readonly Queue<IStoryOperation> _faceOperations;
            public int FaceActorCalls { get; private set; }

            public RecordingActorController(params IStoryOperation[] faceOperations)
            {
                _faceOperations = new Queue<IStoryOperation>(faceOperations);
            }

            public bool Contains(StoryActorId actor) => true;
            public void PlaceAt(StoryActorId actor, StoryStagePoint destination) { }

            public IStoryOperation MoveTo(StoryActorId actor, StoryStagePoint destination, int durationHintMilliseconds)
                => CompletedStoryOperation.Instance;

            public IStoryOperation FaceActor(StoryActorId actor, StoryActorId target)
            {
                FaceActorCalls++;
                return _faceOperations.Dequeue();
            }

            public IStoryOperation FacePoint(StoryActorId actor, StoryStagePoint target)
                => CompletedStoryOperation.Instance;
        }

        private sealed class NoOpPresentation : IStoryPresentation
        {
            public IStoryOperation SetCamera(StoryCueId cameraCue) => CompletedStoryOperation.Instance;
            public IStoryOperation ShowDialogue(StoryCueId dialogueCue) => CompletedStoryOperation.Instance;
            public IStoryOperation PlaySound(StoryCueId soundCue) => CompletedStoryOperation.Instance;
        }
    }
}
