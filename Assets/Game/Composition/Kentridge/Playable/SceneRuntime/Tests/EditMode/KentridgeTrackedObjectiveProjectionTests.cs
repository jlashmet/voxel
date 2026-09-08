using System;
using Game.Hud.Api;
using Game.Hud.Runtime;
using Game.Input.Api;
using Game.Progression.Api;
using Game.Sessions.Api;
using NUnit.Framework;

namespace Game.Kentridge.PlayableSlice.Tests
{
    public sealed class KentridgeTrackedObjectiveProjectionTests
    {
        private const string TravelObjectiveId = "objective:travel-to-first-destination";
        private static readonly LocalPlayerId LocalPlayer = new LocalPlayerId(0);

        [Test]
        public void CanonicalProgressionRebuildFlowsThroughSystem19IntoHud()
        {
            var query = new MutableProgressionQuery(Snapshot(
                1,
                ProgressionLifecycleState.Inactive,
                0,
                1));
            var projection = new KentridgeTrackedObjectiveProjection(query, TravelObjectiveId);
            var source = new TrackedObjectiveHudSource(LocalPlayer, projection);

            projection.Refresh(false);
            Assert.That(source.TryGetTracked(LocalPlayer, out _), Is.False,
                "Inactive hidden progression must not leak into HUD before authority reveals it.");

            query.Current = Snapshot(2, ProgressionLifecycleState.Active, 0, 1);
            projection.Refresh(true);
            Assert.That(source.TryGetTracked(LocalPlayer, out HudTrackedProgressionView active), Is.True);
            Assert.That(active.StableId, Is.EqualTo(TravelObjectiveId));
            Assert.That(active.Label, Is.EqualTo("Reach the first destination"));
            Assert.That(active.ProgressText, Is.EqualTo("ACTIVE · 0/1"));
            Assert.That(source.TryGetTracked(new LocalPlayerId(1), out _), Is.False,
                "Tracked presentation remains local-player scoped.");

            query.Current = Snapshot(3, ProgressionLifecycleState.Completed, 1, 1);
            projection.Refresh(false);
            Assert.That(source.TryGetTracked(LocalPlayer, out HudTrackedProgressionView completed), Is.True);
            Assert.That(completed.ProgressText, Is.EqualTo("COMPLETED · 1/1"));
            Assert.That(query.ReadCount, Is.EqualTo(3),
                "Every refresh rebuilds from current System11 progression instead of caching authority in HUD.");
        }

        private static ProgressionSnapshot Snapshot(
            ulong revision,
            ProgressionLifecycleState state,
            int current,
            int required)
        {
            return new ProgressionSnapshot(
                revision,
                Array.Empty<QuestProgressSnapshot>(),
                new[]
                {
                    new ObjectiveProgressSnapshot(
                        new ObjectiveId(TravelObjectiveId),
                        state,
                        current,
                        required,
                        revision)
                });
        }

        private sealed class MutableProgressionQuery : IProgressionQuery
        {
            public ProgressionSnapshot Current { get; set; }
            public int ReadCount { get; private set; }

            public MutableProgressionQuery(ProgressionSnapshot current)
            {
                Current = current ?? throw new ArgumentNullException(nameof(current));
            }

            public ProgressionSnapshot Snapshot()
            {
                ReadCount++;
                return Current;
            }
        }
    }
}
