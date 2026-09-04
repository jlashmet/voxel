using System;
using System.Collections.Generic;
using Game.GameplayReplication.Api;
using Game.Progression.Api;
using Game.ProgressionPresentation.Api;
using Game.ProgressionPresentation.Runtime;
using Game.Sessions.Api;
using NUnit.Framework;

namespace Game.ProgressionPresentation.Tests
{
    public sealed class QuestJournalPresenterTests
    {
        private static readonly QuestId Quest = new QuestId("quest:old-road");
        private static readonly ObjectiveId ReachGate = new ObjectiveId("objective:reach-gate");
        private static readonly ObjectiveId OpenGate = new ObjectiveId("objective:open-gate");
        private static readonly ObjectiveId ReachTown = new ObjectiveId("objective:reach-town");

        [Test]
        public void ActivationAndCompletionComeFromOneCoherentProgressionRevision()
        {
            var query = new CountingQuery(Snapshot(7, ProgressionLifecycleState.Active, Objective(ReachGate, ProgressionLifecycleState.Active, 1, 3, 7), Objective(OpenGate, ProgressionLifecycleState.Inactive, 0, 1, 7)));
            var presenter = new QuestJournalPresenter(query, Catalog());

            QuestJournalSnapshot first = presenter.Rebuild();
            Assert.That(query.ReadCount, Is.EqualTo(1));
            Assert.That(first.ProgressionRevision, Is.EqualTo(7));
            Assert.That(first.Quests[0].Objectives.Count, Is.EqualTo(1));
            Assert.That(first.Quests[0].Objectives[0].State, Is.EqualTo(ProgressionLifecycleState.Active));
            Assert.That(first.Quests[0].Objectives[0].AuthoritativeRevision, Is.EqualTo(7));

            query.Current = Snapshot(8, ProgressionLifecycleState.Active, Objective(ReachGate, ProgressionLifecycleState.Completed, 3, 3, 8), Objective(OpenGate, ProgressionLifecycleState.Active, 0, 1, 8));
            QuestJournalSnapshot second = presenter.Rebuild();
            Assert.That(query.ReadCount, Is.EqualTo(2));
            Assert.That(second.ProgressionRevision, Is.EqualTo(8));
            Assert.That(second.Quests[0].Objectives.Count, Is.EqualTo(2));
            Assert.That(second.Quests[0].Objectives[0].AuthoritativeRevision, Is.EqualTo(8));
            Assert.That(second.Quests[0].Objectives[1].AuthoritativeRevision, Is.EqualTo(8));
        }

        [Test]
        public void HiddenObjectiveDoesNotLeakUntilAuthoritativeReveal()
        {
            var query = new CountingQuery(Snapshot(10, ProgressionLifecycleState.Active, Objective(ReachGate, ProgressionLifecycleState.Active, 0, 1, 10), Objective(OpenGate, ProgressionLifecycleState.Inactive, 0, 1, 10)));
            var presenter = new QuestJournalPresenter(query, Catalog());
            Assert.That(presenter.Rebuild().Quests[0].Objectives.Count, Is.EqualTo(1));

            query.Current = Snapshot(11, ProgressionLifecycleState.Active, Objective(ReachGate, ProgressionLifecycleState.Completed, 1, 1, 11), Objective(OpenGate, ProgressionLifecycleState.Active, 0, 1, 11));
            QuestJournalSnapshot revealed = presenter.Rebuild();
            Assert.That(revealed.Quests[0].Objectives.Count, Is.EqualTo(2));
            Assert.That(revealed.Quests[0].Objectives[1].Key.ObjectiveId, Is.EqualTo(OpenGate));
        }

        [Test]
        public void StandaloneCampaignObjectiveComesFromSameUnifiedSnapshotAndCanBeTrackedLocally()
        {
            var standalone = Objective(ReachTown, ProgressionLifecycleState.Active, 2, 5, 14);
            var query = new CountingQuery(SnapshotWithStandalone(14, ProgressionLifecycleState.Active, new[] { Objective(ReachGate, ProgressionLifecycleState.Active, 0, 1, 14) }, standalone));
            var presenter = new QuestJournalPresenter(query, Catalog());

            QuestJournalSnapshot journal = presenter.Rebuild();
            Assert.That(query.ReadCount, Is.EqualTo(1));
            Assert.That(journal.StandaloneObjectives.Count, Is.EqualTo(1));
            Assert.That(journal.StandaloneObjectives[0].Key.IsStandalone, Is.True);
            Assert.That(journal.StandaloneObjectives[0].Key.ObjectiveId, Is.EqualTo(ReachTown));
            Assert.That(journal.StandaloneObjectives[0].AuthoritativeRevision, Is.EqualTo(14));

            Assert.That(presenter.TrackObjective(JournalObjectiveKey.Standalone(ReachTown)), Is.True);
            Assert.That(presenter.TryGetTrackedObjective(out TrackedObjectiveSummary tracked), Is.True);
            Assert.That(tracked.Key.IsStandalone, Is.True);
            Assert.That(tracked.ObjectiveLabel, Is.EqualTo("Reach Kentridge"));
            Assert.That(query.Current, Is.SameAs(query.Current));
        }

        [Test]
        public void LocalTrackingDoesNotMutateAuthoritativeProgressionAndMayDifferPerClient()
        {
            ProgressionSnapshot shared = Snapshot(20, ProgressionLifecycleState.Active, Objective(ReachGate, ProgressionLifecycleState.Active, 0, 1, 20), Objective(OpenGate, ProgressionLifecycleState.Active, 0, 1, 20));
            var query = new CountingQuery(shared);
            var first = new QuestJournalPresenter(query, Catalog());
            var second = new QuestJournalPresenter(query, Catalog());
            first.Rebuild();
            second.Rebuild();

            Assert.That(first.TrackObjective(new JournalObjectiveKey(Quest, ReachGate)), Is.True);
            Assert.That(second.TrackObjective(new JournalObjectiveKey(Quest, OpenGate)), Is.True);
            Assert.That(first.TryGetTrackedObjective(out TrackedObjectiveSummary firstTracked), Is.True);
            Assert.That(second.TryGetTrackedObjective(out TrackedObjectiveSummary secondTracked), Is.True);
            Assert.That(firstTracked.Key.ObjectiveId, Is.EqualTo(ReachGate));
            Assert.That(secondTracked.Key.ObjectiveId, Is.EqualTo(OpenGate));
            Assert.That(query.Current, Is.SameAs(shared));
            Assert.That(first.Current.Quests[0].Objectives[0].State, Is.EqualTo(second.Current.Quests[0].Objectives[0].State));
        }

        [Test]
        public void ReconnectRebuildUsesCurrentSnapshotWithoutReplayingHistoryAndReconcilesTracking()
        {
            var preferences = new JournalLocalPreferences();
            var query = new CountingQuery(Snapshot(30, ProgressionLifecycleState.Active, Objective(ReachGate, ProgressionLifecycleState.Active, 0, 1, 30), Objective(OpenGate, ProgressionLifecycleState.Active, 0, 1, 30)));
            var first = new QuestJournalPresenter(query, Catalog(), preferences);
            first.Rebuild();
            first.TrackObjective(new JournalObjectiveKey(Quest, OpenGate));

            query.Current = Snapshot(40, ProgressionLifecycleState.Completed, Objective(ReachGate, ProgressionLifecycleState.Completed, 1, 1, 40));
            var rebuilt = new QuestJournalPresenter(query, Catalog(), preferences);
            QuestJournalSnapshot journal = rebuilt.Rebuild();

            Assert.That(journal.ProgressionRevision, Is.EqualTo(40));
            Assert.That(journal.Quests[0].Objectives.Count, Is.EqualTo(1));
            Assert.That(journal.Quests[0].Objectives[0].State, Is.EqualTo(ProgressionLifecycleState.Completed));
            Assert.That(rebuilt.TryGetTrackedObjective(out _), Is.False);
        }

        [Test]
        public void LocalSortFilterCollapseAndHudProjectionStayPresentationOnly()
        {
            ProgressionSnapshot shared = Snapshot(50, ProgressionLifecycleState.Active, Objective(ReachGate, ProgressionLifecycleState.Completed, 1, 1, 50), Objective(OpenGate, ProgressionLifecycleState.Active, 0, 1, 50));
            var query = new CountingQuery(shared);
            var preferences = new JournalLocalPreferences();
            var presenter = new QuestJournalPresenter(query, Catalog(), preferences);
            presenter.Rebuild();
            presenter.SetQuestCollapsed(Quest, true);
            presenter.SetFilterMode(JournalFilterMode.ActiveOnly);
            presenter.SetSortMode(JournalSortMode.Title);
            Assert.That(presenter.TrackObjective(new JournalObjectiveKey(Quest, OpenGate)), Is.True);

            Assert.That(presenter.Current.Quests[0].IsCollapsed, Is.True);
            Assert.That(presenter.Current.Quests[0].Objectives.Count, Is.EqualTo(1));
            Assert.That(presenter.TryGetTrackedObjective(out TrackedObjectiveSummary hud), Is.True);
            Assert.That(hud.ObjectiveLabel, Is.EqualTo("Open the gate"));
            Assert.That(hud.ProgressionRevision, Is.EqualTo(50));
            Assert.That(query.Current, Is.SameAs(shared));

            var recreated = new QuestJournalPresenter(query, Catalog(), preferences);
            recreated.Rebuild();
            Assert.That(recreated.TryGetTrackedObjective(out TrackedObjectiveSummary restoredHud), Is.True);
            Assert.That(restoredHud.Key.ObjectiveId, Is.EqualTo(OpenGate));
        }

        [Test]
        public void ReplicatedQueryReadsOnlyGameplayReadyCoherentCurrentState()
        {
            var member = new PartyMemberId("party:local");
            ProgressionSnapshot shared = Snapshot(60, ProgressionLifecycleState.Active, Objective(ReachGate, ProgressionLifecycleState.Active, 1, 2, 60));
            var replication = new ReplicationStub(member, GameplaySynchronizationPhase.GameplayReady, 9, shared);
            var query = new ReplicatedProgressionQuery(replication, member);
            var presenter = new QuestJournalPresenter(query, Catalog());

            QuestJournalSnapshot journal = presenter.Rebuild();
            Assert.That(journal.ProgressionRevision, Is.EqualTo(60));
            Assert.That(journal.Quests[0].Objectives[0].CurrentCount, Is.EqualTo(1));

            replication.Phase = GameplaySynchronizationPhase.Synchronizing;
            Assert.Throws<InvalidOperationException>(() => presenter.Rebuild());
        }

        private static ObjectiveProgressSnapshot Objective(ObjectiveId id, ProgressionLifecycleState state, int current, int required, ulong revision) =>
            new ObjectiveProgressSnapshot(id, state, current, required, revision);

        private static ProgressionSnapshot Snapshot(ulong revision, ProgressionLifecycleState questState, params ObjectiveProgressSnapshot[] objectives) =>
            new ProgressionSnapshot(revision, new[] { new QuestProgressSnapshot(Quest, questState, objectives, revision) }, Array.Empty<ObjectiveProgressSnapshot>());

        private static ProgressionSnapshot SnapshotWithStandalone(ulong revision, ProgressionLifecycleState questState, IReadOnlyList<ObjectiveProgressSnapshot> questObjectives, params ObjectiveProgressSnapshot[] standalone) =>
            new ProgressionSnapshot(revision, new[] { new QuestProgressSnapshot(Quest, questState, questObjectives, revision) }, standalone);

        private static CatalogStub Catalog() => new CatalogStub(
            new QuestPresentationContent(Quest, "The Old Road", 10),
            new[]
            {
                new ObjectivePresentationContent(ReachGate, "Reach the old gate", "Follow the road to the gate.", 10),
                new ObjectivePresentationContent(OpenGate, "Open the gate", "Find a way through.", 20)
            },
            new[]
            {
                new ObjectivePresentationContent(ReachTown, "Reach Kentridge", "Continue to the town boundary.", 10)
            });

        private sealed class CountingQuery : IProgressionQuery
        {
            public ProgressionSnapshot Current { get; set; }
            public int ReadCount { get; private set; }
            public CountingQuery(ProgressionSnapshot current) => Current = current;
            public ProgressionSnapshot Snapshot() { ReadCount++; return Current; }
        }

        private sealed class CatalogStub : IProgressionPresentationCatalog
        {
            private readonly QuestPresentationContent _quest;
            private readonly Dictionary<ObjectiveId, ObjectivePresentationContent> _questObjectives = new Dictionary<ObjectiveId, ObjectivePresentationContent>();
            private readonly Dictionary<ObjectiveId, ObjectivePresentationContent> _standalone = new Dictionary<ObjectiveId, ObjectivePresentationContent>();

            public CatalogStub(QuestPresentationContent quest, IReadOnlyList<ObjectivePresentationContent> questObjectives, IReadOnlyList<ObjectivePresentationContent> standalone)
            {
                _quest = quest;
                for (var i = 0; i < questObjectives.Count; i++) _questObjectives[questObjectives[i].ObjectiveId] = questObjectives[i];
                for (var i = 0; i < standalone.Count; i++) _standalone[standalone[i].ObjectiveId] = standalone[i];
            }

            public bool TryGetQuest(QuestId questId, out QuestPresentationContent content) { content = _quest; return questId == _quest.QuestId; }
            public bool TryGetObjective(QuestId questId, ObjectiveId objectiveId, out ObjectivePresentationContent content) => questId == _quest.QuestId && _questObjectives.TryGetValue(objectiveId, out content);
            public bool TryGetStandaloneObjective(ObjectiveId objectiveId, out ObjectivePresentationContent content) => _standalone.TryGetValue(objectiveId, out content);
        }

        private sealed class ReplicationStub : IGameplayReplicationClientState
        {
            private readonly PartyMemberId _memberId;
            private readonly ulong _revision;
            private readonly ProgressionSnapshot _progression;

            public GameplaySynchronizationPhase Phase { get; set; }

            public ReplicationStub(PartyMemberId memberId, GameplaySynchronizationPhase phase, ulong revision, ProgressionSnapshot progression)
            {
                _memberId = memberId;
                Phase = phase;
                _revision = revision;
                _progression = progression;
            }

            public void RequestRecovery(PartyMemberId memberId, GameplayRecoveryMode mode) { }

            public bool TryGetSynchronization(PartyMemberId memberId, out GameplaySynchronizationStatus status)
            {
                if (!memberId.Equals(_memberId)) { status = default; return false; }
                status = new GameplaySynchronizationStatus(Phase, new GameplayRevision(_revision));
                return true;
            }

            public bool TryGetCurrent<TState>(PartyMemberId memberId, out GameplayProjectionSnapshot<TState> snapshot) where TState : struct
            {
                if (!memberId.Equals(_memberId) || typeof(TState) != typeof(ProgressionPresentationCurrentState))
                {
                    snapshot = default;
                    return false;
                }

                var typed = new GameplayProjectionSnapshot<ProgressionPresentationCurrentState>(
                    new GameplayRevision(_revision),
                    new ProgressionPresentationCurrentState(_progression));
                snapshot = (GameplayProjectionSnapshot<TState>)(object)typed;
                return true;
            }
        }
    }
}
