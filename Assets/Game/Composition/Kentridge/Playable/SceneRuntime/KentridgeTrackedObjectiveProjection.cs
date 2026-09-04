using System;
using Game.Progression.Api;
using Game.ProgressionPresentation.Api;
using Game.ProgressionPresentation.Runtime;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Kentridge composition for System19's local tracked-objective presentation state.
    /// Progression truth remains the campaign's System11 IProgressionQuery; this adapter only
    /// supplies authored presentation content and the opening slice's default local track choice.
    /// </summary>
    internal sealed class KentridgeTrackedObjectiveProjection : ITrackedObjectiveProjection
    {
        private readonly JournalObjectiveKey _travelObjective;
        private readonly QuestJournalPresenter _presenter;
        private bool _hasTrackedTravelObjective;

        public KentridgeTrackedObjectiveProjection(IProgressionQuery progression, string travelObjectiveId)
        {
            if (progression == null) throw new ArgumentNullException(nameof(progression));
            if (string.IsNullOrWhiteSpace(travelObjectiveId))
                throw new ArgumentException("Travel objective id is required.", nameof(travelObjectiveId));

            var objectiveId = new ObjectiveId(travelObjectiveId);
            _travelObjective = JournalObjectiveKey.Standalone(objectiveId);
            _presenter = new QuestJournalPresenter(
                progression,
                new KentridgeOpeningProgressionCatalog(objectiveId));
        }

        public void Refresh(bool travelObjectiveActive)
        {
            // Rebuild from current System11 state every presentation refresh. This keeps count/revision
            // changes current without caching or recreating authoritative progression state in HUD.
            _presenter.Rebuild();
            if (travelObjectiveActive && !_hasTrackedTravelObjective)
                _hasTrackedTravelObjective = _presenter.TrackObjective(_travelObjective);
        }

        public bool TryGetTrackedObjective(out TrackedObjectiveSummary summary) =>
            _presenter.TryGetTrackedObjective(out summary);

        private sealed class KentridgeOpeningProgressionCatalog : IProgressionPresentationCatalog
        {
            private readonly ObjectiveId _travelObjective;

            public KentridgeOpeningProgressionCatalog(ObjectiveId travelObjective)
            {
                _travelObjective = travelObjective;
            }

            public bool TryGetQuest(QuestId questId, out QuestPresentationContent content)
            {
                content = default;
                return false;
            }

            public bool TryGetObjective(
                QuestId questId,
                ObjectiveId objectiveId,
                out ObjectivePresentationContent content)
            {
                content = default;
                return false;
            }

            public bool TryGetStandaloneObjective(
                ObjectiveId objectiveId,
                out ObjectivePresentationContent content)
            {
                if (objectiveId != _travelObjective)
                {
                    content = default;
                    return false;
                }

                content = new ObjectivePresentationContent(
                    _travelObjective,
                    "Reach the first destination",
                    "Travel from the starting pub and speak with the contact at the destination.",
                    10);
                return true;
            }
        }
    }
}
