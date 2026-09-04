using System;
using Game.Hud.Api;
using Game.ProgressionPresentation.Api;
using Game.Sessions.Api;

namespace Game.Hud.Runtime
{
    /// <summary>
    /// Read-only bridge from System19's local tracked-objective projection into the HUD view model.
    /// It owns no journal, tracking preference, or progression state.
    /// </summary>
    public sealed class TrackedObjectiveHudSource : IHudTrackedProgressionSource
    {
        private readonly LocalPlayerId _owner;
        private readonly ITrackedObjectiveProjection _projection;

        public TrackedObjectiveHudSource(LocalPlayerId owner, ITrackedObjectiveProjection projection)
        {
            _owner = owner;
            _projection = projection ?? throw new ArgumentNullException(nameof(projection));
        }

        public bool TryGetTracked(LocalPlayerId localPlayerId, out HudTrackedProgressionView tracked)
        {
            tracked = default;
            if (!localPlayerId.Equals(_owner)) return false;
            if (!_projection.TryGetTrackedObjective(out TrackedObjectiveSummary summary)) return false;

            string stableId = summary.Key.IsStandalone
                ? summary.Key.ObjectiveId.Value
                : summary.Key.QuestId.Value + "/" + summary.Key.ObjectiveId.Value;
            string progress = BuildProgressText(summary);
            tracked = new HudTrackedProgressionView(
                true,
                stableId,
                summary.ObjectiveLabel,
                progress);
            return true;
        }

        private static string BuildProgressText(TrackedObjectiveSummary summary)
        {
            string prefix = string.IsNullOrWhiteSpace(summary.QuestTitle)
                ? summary.State.ToString().ToUpperInvariant()
                : summary.QuestTitle;
            return summary.RequiredCount > 0
                ? prefix + " · " + summary.CurrentCount + "/" + summary.RequiredCount
                : prefix;
        }
    }
}
