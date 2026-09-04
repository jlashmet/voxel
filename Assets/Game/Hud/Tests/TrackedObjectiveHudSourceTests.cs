using Game.Hud.Runtime;
using Game.Input.Api;
using Game.Progression.Api;
using Game.ProgressionPresentation.Api;
using Game.Sessions.Api;
using NUnit.Framework;

namespace Game.Hud.Tests
{
    public sealed class TrackedObjectiveHudSourceTests
    {
        [Test]
        public void MapsSystem19TrackedObjectiveWithoutOwningTrackingState()
        {
            var owner = new LocalPlayerId(0);
            var summary = new TrackedObjectiveSummary(
                new JournalObjectiveKey(
                    new QuestId("quest:old-road"),
                    new ObjectiveId("objective:reach-gate")),
                "The Old Road",
                "Reach the old gate",
                ProgressionLifecycleState.Active,
                2,
                3,
                17);
            var projection = new ProjectionStub(summary);
            var source = new TrackedObjectiveHudSource(owner, projection);

            Assert.That(source.TryGetTracked(owner, out var tracked), Is.True);
            Assert.That(tracked.Visible, Is.True);
            Assert.That(tracked.StableId, Is.EqualTo("quest:old-road/objective:reach-gate"));
            Assert.That(tracked.Label, Is.EqualTo("Reach the old gate"));
            Assert.That(tracked.ProgressText, Is.EqualTo("The Old Road · 2/3"));
            Assert.That(projection.ReadCount, Is.EqualTo(1));
        }

        [Test]
        public void DoesNotLeakTrackedObjectiveAcrossLocalPlayers()
        {
            var projection = new ProjectionStub(new TrackedObjectiveSummary(
                JournalObjectiveKey.Standalone(new ObjectiveId("objective:reach-town")),
                string.Empty,
                "Reach Kentridge",
                ProgressionLifecycleState.Active,
                0,
                1,
                18));
            var source = new TrackedObjectiveHudSource(new LocalPlayerId(0), projection);

            Assert.That(source.TryGetTracked(new LocalPlayerId(1), out _), Is.False);
            Assert.That(projection.ReadCount, Is.EqualTo(0));
        }

        private sealed class ProjectionStub : ITrackedObjectiveProjection
        {
            private readonly TrackedObjectiveSummary _summary;
            public int ReadCount { get; private set; }

            public ProjectionStub(TrackedObjectiveSummary summary) => _summary = summary;

            public bool TryGetTrackedObjective(out TrackedObjectiveSummary summary)
            {
                ReadCount++;
                summary = _summary;
                return true;
            }
        }
    }
}
