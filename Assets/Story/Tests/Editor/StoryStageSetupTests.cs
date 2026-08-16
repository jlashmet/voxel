using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MountingForce.Story.Tests
{
    public sealed class StoryStageSetupTests
    {
        [Test]
        public void ApplySnapsActorsToResolvedStagePointsInDefinitionOrder()
        {
            var lead = new StoryActorId("lead");
            var ally = new StoryActorId("ally");
            var leadPoint = new StoryStagePointId("lead-start");
            var allyPoint = new StoryStagePointId("ally-start");
            var stage = new StoryStageBinding()
                .Bind(leadPoint, new StoryStagePoint(new StoryInt3(10, 20, 30), new StoryInt3(1, 0, 0)))
                .Bind(allyPoint, new StoryStagePoint(new StoryInt3(40, 20, 30), new StoryInt3(0, 0, 1)));
            var setup = new StoryStageSetupDefinition(new[]
            {
                new StoryActorPlacement(lead, leadPoint),
                new StoryActorPlacement(ally, allyPoint)
            });
            var actors = new RecordingActorController(lead, ally);

            StoryStageSetup.Apply(setup, actors, stage);

            Assert.AreEqual(2, actors.Actors.Count);
            Assert.AreEqual(lead, actors.Actors[0]);
            Assert.AreEqual(new StoryInt3(10, 20, 30), actors.Points[0].Position);
            Assert.AreEqual(new StoryInt3(1, 0, 0), actors.Points[0].Forward);
            Assert.AreEqual(ally, actors.Actors[1]);
            Assert.AreEqual(new StoryInt3(40, 20, 30), actors.Points[1].Position);
        }

        [Test]
        public void SetupRejectsDuplicateActorPlacements()
        {
            var lead = new StoryActorId("lead");
            Assert.Throws<ArgumentException>(() => new StoryStageSetupDefinition(new[]
            {
                new StoryActorPlacement(lead, new StoryStagePointId("a")),
                new StoryActorPlacement(lead, new StoryStagePointId("b"))
            }));
        }

        [Test]
        public void ApplyDoesNotPartiallyPlaceWhenLaterActorIsMissing()
        {
            var lead = new StoryActorId("lead");
            var missing = new StoryActorId("missing");
            var leadPoint = new StoryStagePointId("lead-start");
            var missingPoint = new StoryStagePointId("missing-start");
            var stage = new StoryStageBinding()
                .Bind(leadPoint, new StoryStagePoint(new StoryInt3(1, 0, 0), default))
                .Bind(missingPoint, new StoryStagePoint(new StoryInt3(2, 0, 0), default));
            var setup = new StoryStageSetupDefinition(new[]
            {
                new StoryActorPlacement(lead, leadPoint),
                new StoryActorPlacement(missing, missingPoint)
            });
            var actors = new RecordingActorController(lead);

            Assert.Throws<InvalidOperationException>(() => StoryStageSetup.Apply(setup, actors, stage));
            Assert.AreEqual(0, actors.Actors.Count);
        }

        [Test]
        public void ApplyDoesNotPartiallyPlaceWhenLaterStagePointIsMissing()
        {
            var lead = new StoryActorId("lead");
            var ally = new StoryActorId("ally");
            var leadPoint = new StoryStagePointId("lead-start");
            var missingPoint = new StoryStagePointId("missing-start");
            var stage = new StoryStageBinding()
                .Bind(leadPoint, new StoryStagePoint(new StoryInt3(1, 0, 0), default));
            var setup = new StoryStageSetupDefinition(new[]
            {
                new StoryActorPlacement(lead, leadPoint),
                new StoryActorPlacement(ally, missingPoint)
            });
            var actors = new RecordingActorController(lead, ally);

            Assert.Throws<InvalidOperationException>(() => StoryStageSetup.Apply(setup, actors, stage));
            Assert.AreEqual(0, actors.Actors.Count);
        }

        private sealed class RecordingActorController : IStoryActorController
        {
            private readonly HashSet<StoryActorId> _registered;
            public readonly List<StoryActorId> Actors = new List<StoryActorId>();
            public readonly List<StoryStagePoint> Points = new List<StoryStagePoint>();

            public RecordingActorController(params StoryActorId[] registered)
            {
                _registered = new HashSet<StoryActorId>(registered);
            }

            public bool Contains(StoryActorId actor) => _registered.Contains(actor);

            public void PlaceAt(StoryActorId actor, StoryStagePoint destination)
            {
                Actors.Add(actor);
                Points.Add(destination);
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
