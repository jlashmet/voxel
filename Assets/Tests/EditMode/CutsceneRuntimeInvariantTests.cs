using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;
using Game.Cutscenes.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CutsceneRuntimeInvariantTests
    {
        [Test]
        public void PlaybackRejectsMissingSequenceStagePointBeforeSetupMutation()
        {
            var lead = new CutsceneActorId("lead");
            var start = new CutsceneStagePointId("start");
            var missing = new CutsceneStagePointId("missing");
            var definition = new CutsceneDefinition(
                "preflight-missing-stage",
                new CutsceneStageSetupDefinition(new[]
                {
                    new CutsceneActorPlacement(lead, start)
                }),
                new[]
                {
                    CutsceneStep.Move(lead, missing, 100)
                });
            var stage = new CutsceneStageBinding()
                .Bind(start, new CutsceneStagePoint(new CutsceneInt3(1, 2, 3), default(CutsceneInt3)));
            var actors = new RecordingActorController(lead);

            Assert.Throws<InvalidOperationException>(() =>
                CutscenePlayback.Start(definition, actors, new NoOpPresentation(), stage));
            Assert.That(actors.PlaceAtCalls, Is.EqualTo(0));
        }

        [Test]
        public void PreflightDetectsMissingActorInsideParallelStepRecursively()
        {
            var lead = new CutsceneActorId("lead");
            var missing = new CutsceneActorId("missing");
            var definition = new CutsceneDefinition(
                "preflight-parallel",
                CutsceneStageSetupDefinition.Empty,
                new[]
                {
                    CutsceneStep.Parallel(CutsceneStep.FaceActor(lead, missing))
                });
            var actors = new RecordingActorController(lead);

            Assert.Throws<InvalidOperationException>(() =>
                CutscenePreflight.Validate(definition, actors, new CutsceneStageBinding()));
        }

        [Test]
        public void ValidPreflightDoesNotMutateActors()
        {
            var lead = new CutsceneActorId("lead");
            var start = new CutsceneStagePointId("start");
            var definition = new CutsceneDefinition(
                "preflight-valid",
                new CutsceneStageSetupDefinition(new[]
                {
                    new CutsceneActorPlacement(lead, start)
                }),
                new[]
                {
                    CutsceneStep.FacePoint(lead, start),
                    CutsceneStep.Dialogue(new CutsceneCueId("dialogue.test"))
                });
            var stage = new CutsceneStageBinding()
                .Bind(start, new CutsceneStagePoint(new CutsceneInt3(4, 5, 6), default(CutsceneInt3)));
            var actors = new RecordingActorController(lead);

            Assert.DoesNotThrow(() => CutscenePreflight.Validate(definition, actors, stage));
            Assert.That(actors.PlaceAtCalls, Is.EqualTo(0));
        }

        [Test]
        public void DurationFactoriesRejectNegativeMilliseconds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CutsceneStep.Wait(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CutsceneStep.Move(
                new CutsceneActorId("lead"),
                new CutsceneStagePointId("destination"),
                -1));
        }

        [Test]
        public void ParallelStepStartsAllChildrenAndWaitsForEveryCompletion()
        {
            var first = new ManualOperation();
            var second = new ManualOperation();
            var actors = new RecordingActorController(first, second);
            var definition = new CutsceneDefinition(
                "parallel-test",
                CutsceneStageSetupDefinition.Empty,
                new[]
                {
                    CutsceneStep.Parallel(
                        CutsceneStep.FaceActor(new CutsceneActorId("madeline"), new CutsceneActorId("lead")),
                        CutsceneStep.FaceActor(new CutsceneActorId("steven"), new CutsceneActorId("lead")))
                });
            var runner = new CutsceneRunner();

            runner.Start(definition, new CutsceneExecutionContext(
                actors,
                new NoOpPresentation(),
                new CutsceneStageBinding()));
            runner.Tick(0);

            Assert.That(actors.FaceActorCalls, Is.EqualTo(2));
            Assert.That(runner.CurrentStepIndex, Is.EqualTo(0));
            Assert.That(runner.IsRunning, Is.True);

            first.Complete();
            runner.Tick(0);
            Assert.That(runner.CurrentStepIndex, Is.EqualTo(0));
            Assert.That(runner.IsRunning, Is.True);

            second.Complete();
            runner.Tick(0);
            Assert.That(runner.CurrentStepIndex, Is.EqualTo(1));
            Assert.That(runner.IsComplete, Is.True);
            Assert.That(runner.IsRunning, Is.False);
        }

        [Test]
        public void ParallelStepRejectsWaitChildren()
        {
            CutsceneStep wait = CutsceneStep.Wait(10);
            Assert.Throws<ArgumentException>(() => CutsceneStep.Parallel(wait));
        }

        [Test]
        public void BoundStagePointResolves()
        {
            var id = new CutsceneStagePointId("test");
            var point = new CutsceneStagePoint(
                new CutsceneInt3(1, 2, 3),
                new CutsceneInt3(0, 0, 1));
            var binding = new CutsceneStageBinding().Bind(id, point);

            Assert.That(binding.Resolve(id).Position, Is.EqualTo(point.Position));
            Assert.That(binding.Resolve(id).Forward, Is.EqualTo(point.Forward));
        }

        [Test]
        public void StageSetupAppliesPlacementsInDefinitionOrder()
        {
            var lead = new CutsceneActorId("lead");
            var ally = new CutsceneActorId("ally");
            var leadPoint = new CutsceneStagePointId("lead-start");
            var allyPoint = new CutsceneStagePointId("ally-start");
            var stage = new CutsceneStageBinding()
                .Bind(leadPoint, new CutsceneStagePoint(new CutsceneInt3(10, 20, 30), new CutsceneInt3(1, 0, 0)))
                .Bind(allyPoint, new CutsceneStagePoint(new CutsceneInt3(40, 20, 30), new CutsceneInt3(0, 0, 1)));
            var definition = new CutsceneDefinition(
                "setup-order",
                new CutsceneStageSetupDefinition(new[]
                {
                    new CutsceneActorPlacement(lead, leadPoint),
                    new CutsceneActorPlacement(ally, allyPoint)
                }),
                new CutsceneStep[0]);
            var actors = new RecordingActorController(lead, ally);

            CutsceneStageSetup.Apply(definition, actors, stage);

            Assert.That(actors.PlacedActors.Count, Is.EqualTo(2));
            Assert.That(actors.PlacedActors[0], Is.EqualTo(lead));
            Assert.That(actors.PlacedPoints[0].Position, Is.EqualTo(new CutsceneInt3(10, 20, 30)));
            Assert.That(actors.PlacedPoints[0].Forward, Is.EqualTo(new CutsceneInt3(1, 0, 0)));
            Assert.That(actors.PlacedActors[1], Is.EqualTo(ally));
            Assert.That(actors.PlacedPoints[1].Position, Is.EqualTo(new CutsceneInt3(40, 20, 30)));
        }

        [Test]
        public void StageSetupRejectsDuplicateActorPlacements()
        {
            var lead = new CutsceneActorId("lead");

            Assert.Throws<ArgumentException>(() => new CutsceneStageSetupDefinition(new[]
            {
                new CutsceneActorPlacement(lead, new CutsceneStagePointId("a")),
                new CutsceneActorPlacement(lead, new CutsceneStagePointId("b"))
            }));
        }

        [Test]
        public void StageSetupDoesNotPartiallyPlaceWhenLaterActorIsMissing()
        {
            var lead = new CutsceneActorId("lead");
            var missing = new CutsceneActorId("missing");
            var leadPoint = new CutsceneStagePointId("lead-start");
            var missingPoint = new CutsceneStagePointId("missing-start");
            var stage = new CutsceneStageBinding()
                .Bind(leadPoint, new CutsceneStagePoint(new CutsceneInt3(1, 0, 0), default(CutsceneInt3)))
                .Bind(missingPoint, new CutsceneStagePoint(new CutsceneInt3(2, 0, 0), default(CutsceneInt3)));
            var definition = new CutsceneDefinition(
                "setup-missing-actor",
                new CutsceneStageSetupDefinition(new[]
                {
                    new CutsceneActorPlacement(lead, leadPoint),
                    new CutsceneActorPlacement(missing, missingPoint)
                }),
                new CutsceneStep[0]);
            var actors = new RecordingActorController(lead);

            Assert.Throws<InvalidOperationException>(() => CutsceneStageSetup.Apply(definition, actors, stage));
            Assert.That(actors.PlaceAtCalls, Is.EqualTo(0));
        }

        [Test]
        public void StageSetupDoesNotPartiallyPlaceWhenLaterStagePointIsMissing()
        {
            var lead = new CutsceneActorId("lead");
            var ally = new CutsceneActorId("ally");
            var leadPoint = new CutsceneStagePointId("lead-start");
            var missingPoint = new CutsceneStagePointId("missing-start");
            var stage = new CutsceneStageBinding()
                .Bind(leadPoint, new CutsceneStagePoint(new CutsceneInt3(1, 0, 0), default(CutsceneInt3)));
            var definition = new CutsceneDefinition(
                "setup-missing-stage",
                new CutsceneStageSetupDefinition(new[]
                {
                    new CutsceneActorPlacement(lead, leadPoint),
                    new CutsceneActorPlacement(ally, missingPoint)
                }),
                new CutsceneStep[0]);
            var actors = new RecordingActorController(lead, ally);

            Assert.Throws<InvalidOperationException>(() => CutsceneStageSetup.Apply(definition, actors, stage));
            Assert.That(actors.PlaceAtCalls, Is.EqualTo(0));
        }

        private sealed class ManualOperation : ICutsceneOperation
        {
            public bool IsComplete { get; private set; }
            public void Complete() { IsComplete = true; }
        }

        private sealed class RecordingActorController : ICutsceneActorController
        {
            private readonly HashSet<CutsceneActorId> _registered;
            private readonly Queue<ICutsceneOperation> _faceOperations;

            public int PlaceAtCalls { get; private set; }
            public int FaceActorCalls { get; private set; }
            public readonly List<CutsceneActorId> PlacedActors = new List<CutsceneActorId>();
            public readonly List<CutsceneStagePoint> PlacedPoints = new List<CutsceneStagePoint>();

            public RecordingActorController(params CutsceneActorId[] registered)
            {
                _registered = new HashSet<CutsceneActorId>(registered);
                _faceOperations = new Queue<ICutsceneOperation>();
            }

            public RecordingActorController(params ICutsceneOperation[] faceOperations)
            {
                _registered = new HashSet<CutsceneActorId>();
                _faceOperations = new Queue<ICutsceneOperation>(faceOperations);
            }

            public bool Contains(CutsceneActorId actor) => _registered.Count == 0 || _registered.Contains(actor);

            public void PlaceAt(CutsceneActorId actor, CutsceneStagePoint destination)
            {
                PlaceAtCalls++;
                PlacedActors.Add(actor);
                PlacedPoints.Add(destination);
            }

            public ICutsceneOperation MoveTo(CutsceneActorId actor, CutsceneStagePoint destination, int durationHintMilliseconds)
                => CompletedCutsceneOperation.Instance;

            public ICutsceneOperation FaceActor(CutsceneActorId actor, CutsceneActorId target)
            {
                FaceActorCalls++;
                return _faceOperations.Count == 0
                    ? CompletedCutsceneOperation.Instance
                    : _faceOperations.Dequeue();
            }

            public ICutsceneOperation FacePoint(CutsceneActorId actor, CutsceneStagePoint target)
                => CompletedCutsceneOperation.Instance;
        }

        private sealed class NoOpPresentation : ICutscenePresentation
        {
            public ICutsceneOperation SetCamera(CutsceneCueId cameraCue) => CompletedCutsceneOperation.Instance;
            public ICutsceneOperation ShowDialogue(CutsceneActorId speaker, CutsceneCueId dialogueCue) => CompletedCutsceneOperation.Instance;
            public ICutsceneOperation PlaySound(CutsceneCueId soundCue) => CompletedCutsceneOperation.Instance;
        }
    }
}
