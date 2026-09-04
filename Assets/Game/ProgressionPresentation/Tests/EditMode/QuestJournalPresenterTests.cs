using System;
using System.Collections.Generic;
using Game.Progression.Api;
using Game.ProgressionPresentation.Api;
using Game.ProgressionPresentation.Runtime;
using NUnit.Framework;

namespace Game.ProgressionPresentation.Tests
{
    public sealed class QuestJournalPresenterTests
    {
        private static readonly QuestId Quest = new QuestId("quest:old-road");
        private static readonly ObjectiveId ReachGate = new ObjectiveId("objective:reach-gate");
        private static readonly ObjectiveId OpenGate = new ObjectiveId("objective:open-gate");

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

        private static ObjectiveProgressSnapshot Objective(ObjectiveId id, ProgressionLifecycleState state, int current, int required, ulong revision) =>
            new ObjectiveProgressSnapshot(id, state, current, required, revision);

        private static ProgressionSnapshot Snapshot(ulong revision, ProgressionLifecycleState questState, params ObjectiveProgressSnapshot[] objectives) =>
            new ProgressionSnapshot(revision, new[] { new QuestProgressSnapshot(Quest, questState, objectives, revision) }, Array.Empty<ObjectiveProgressSnapshot>());

        private static CatalogStub Catalog() => new CatalogStub(
            new QuestPresentationContent(Quest, "The Old Road", 10),
            new ObjectivePresentationContent(ReachGate, "Reach the old gate", "Follow the road to the gate.", 10),
            new ObjectivePresentationContent(OpenGate, "Open the gate", "Find a way through.", 20));

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
            private readonly Dictionary<ObjectiveId, ObjectivePresentationContent> _objectives = new Dictionary<ObjectiveId, ObjectivePresentationContent>();
            public CatalogStub(QuestPresentationContent quest, params ObjectivePresentationContent[] objectives)
            {
                _quest = quest;
                for (var i = 0; i < objectives.Length; i++) _objectives[objectives[i].ObjectiveId] = objectives[i];
            }
            public bool TryGetQuest(QuestId questId, out QuestPresentationContent content) { content = _quest; return questId == _quest.QuestId; }
            public bool TryGetObjective(QuestId questId, ObjectiveId objectiveId, out ObjectivePresentationContent content) => questId == _quest.QuestId && _objectives.TryGetValue(objectiveId, out content);
        }
    }
}
