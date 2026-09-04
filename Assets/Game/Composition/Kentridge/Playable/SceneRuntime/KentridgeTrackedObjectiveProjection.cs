using System;
using Game.Composition.Kentridge.Api;
using Game.Progression.Api;
using Game.ProgressionPresentation.Api;
using Game.ProgressionPresentation.Runtime;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Kentridge composition of System19 over the canonical campaign progression query. The only
    /// mutable state here is System19's local presentation preference for which visible objective is
    /// tracked; authoritative quest/objective state remains entirely in System11 progression.
    /// </summary>
    internal sealed class KentridgeTrackedObjectiveProjection : ITrackedObjectiveProjection
    {
        private static readonly QuestId WellQuest = new QuestId(KentridgeWellQuestDefinition.QuestId);
        private static readonly ObjectiveId RescueBoy = new ObjectiveId("rescue-boy-at-well.completion");
        private static readonly ObjectiveId ReturnToMadeline = new ObjectiveId("return-to-madeline.completion");

        private readonly QuestJournalPresenter _journal;

        public KentridgeTrackedObjectiveProjection(
            IProgressionQuery progression,
            ObjectiveId travelObjective)
        {
            if (!travelObjective.IsValid)
                throw new ArgumentException("Travel objective id is required.", nameof(travelObjective));
            _journal = new QuestJournalPresenter(
                progression ?? throw new ArgumentNullException(nameof(progression)),
                new KentridgeCatalog(travelObjective));
        }

        public bool TryGetTrackedObjective(out TrackedObjectiveSummary summary)
        {
            QuestJournalSnapshot journal = _journal.Rebuild();
            bool hasTracked = _journal.TryGetTrackedObjective(out summary);
            if (hasTracked && summary.State == ProgressionLifecycleState.Active)
                return true;

            if (TryFindFirstActive(journal, out JournalObjectiveKey next))
            {
                if (!hasTracked || summary.Key != next)
                    _journal.TrackObjective(next);
                return _journal.TryGetTrackedObjective(out summary);
            }

            return hasTracked;
        }

        private static bool TryFindFirstActive(
            QuestJournalSnapshot journal,
            out JournalObjectiveKey key)
        {
            for (int q = 0; q < journal.Quests.Count; q++)
            {
                var objectives = journal.Quests[q].Objectives;
                for (int o = 0; o < objectives.Count; o++)
                {
                    if (objectives[o].State != ProgressionLifecycleState.Active) continue;
                    key = objectives[o].Key;
                    return true;
                }
            }

            for (int i = 0; i < journal.StandaloneObjectives.Count; i++)
            {
                if (journal.StandaloneObjectives[i].State != ProgressionLifecycleState.Active) continue;
                key = journal.StandaloneObjectives[i].Key;
                return true;
            }

            key = default;
            return false;
        }

        private sealed class KentridgeCatalog : IProgressionPresentationCatalog
        {
            private readonly ObjectiveId _travelObjective;

            public KentridgeCatalog(ObjectiveId travelObjective) => _travelObjective = travelObjective;

            public bool TryGetQuest(QuestId questId, out QuestPresentationContent content)
            {
                if (questId == WellQuest)
                {
                    content = new QuestPresentationContent(WellQuest, "Kid in the Well", 10);
                    return true;
                }
                content = default;
                return false;
            }

            public bool TryGetObjective(
                QuestId questId,
                ObjectiveId objectiveId,
                out ObjectivePresentationContent content)
            {
                if (questId == WellQuest && objectiveId == RescueBoy)
                {
                    content = new ObjectivePresentationContent(
                        RescueBoy,
                        "Rescue the boy at the well",
                        "Find and help the boy trapped at Kentridge's well.",
                        10);
                    return true;
                }
                if (questId == WellQuest && objectiveId == ReturnToMadeline)
                {
                    content = new ObjectivePresentationContent(
                        ReturnToMadeline,
                        "Return to Madeline",
                        "Tell Madeline the boy is safe.",
                        20);
                    return true;
                }
                content = default;
                return false;
            }

            public bool TryGetStandaloneObjective(
                ObjectiveId objectiveId,
                out ObjectivePresentationContent content)
            {
                if (objectiveId == _travelObjective)
                {
                    content = new ObjectivePresentationContent(
                        _travelObjective,
                        "Travel to the first destination",
                        "Leave the pub and continue to the first destination.",
                        10);
                    return true;
                }
                content = default;
                return false;
            }
        }
    }
}
