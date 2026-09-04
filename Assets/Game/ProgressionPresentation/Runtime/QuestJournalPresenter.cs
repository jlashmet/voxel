using System;
using System.Collections.Generic;
using Game.Progression.Api;
using Game.ProgressionPresentation.Api;

namespace Game.ProgressionPresentation.Runtime
{
    public sealed class JournalLocalPreferences
    {
        private readonly HashSet<QuestId> _collapsed = new HashSet<QuestId>();

        public JournalSortMode SortMode { get; set; } = JournalSortMode.AuthoritativeOrder;
        public JournalFilterMode FilterMode { get; set; } = JournalFilterMode.AllVisible;
        public JournalObjectiveKey? Selected { get; set; }
        public JournalObjectiveKey? Tracked { get; set; }

        public bool IsCollapsed(QuestId questId) => _collapsed.Contains(questId);
        public void SetCollapsed(QuestId questId, bool collapsed)
        {
            if (collapsed) _collapsed.Add(questId);
            else _collapsed.Remove(questId);
        }
    }

    public sealed class QuestJournalPresenter : IQuestJournalPresentation, ITrackedObjectiveProjection
    {
        private readonly IProgressionQuery _progression;
        private readonly IProgressionPresentationCatalog _catalog;
        private readonly JournalLocalPreferences _preferences;
        private QuestJournalSnapshot _current;
        private readonly Dictionary<JournalObjectiveKey, VisibleObjective> _visible = new Dictionary<JournalObjectiveKey, VisibleObjective>();

        public QuestJournalSnapshot Current => _current;

        public QuestJournalPresenter(IProgressionQuery progression, IProgressionPresentationCatalog catalog, JournalLocalPreferences preferences = null)
        {
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _preferences = preferences ?? new JournalLocalPreferences();
            _current = new QuestJournalSnapshot(
                0,
                _preferences.SortMode,
                _preferences.FilterMode,
                Array.Empty<JournalQuestEntry>(),
                Array.Empty<JournalObjectiveEntry>());
        }

        public QuestJournalSnapshot Rebuild()
        {
            ProgressionSnapshot authoritative = _progression.Snapshot();
            if (authoritative == null) throw new InvalidOperationException("Progression query returned no snapshot.");

            _visible.Clear();
            var quests = new List<QuestBuild>();
            for (var q = 0; q < authoritative.Quests.Count; q++)
            {
                QuestProgressSnapshot quest = authoritative.Quests[q];
                if (!_catalog.TryGetQuest(quest.Id, out QuestPresentationContent questContent)) continue;
                if (!IsVisible(quest.State, questContent.VisibleWhileInactive)) continue;

                var objectives = new List<ObjectiveBuild>();
                for (var o = 0; o < quest.Objectives.Count; o++)
                {
                    ObjectiveProgressSnapshot objective = quest.Objectives[o];
                    if (!_catalog.TryGetObjective(quest.Id, objective.Id, out ObjectivePresentationContent objectiveContent)) continue;
                    if (!IsVisible(objective.State, objectiveContent.VisibleWhileInactive)) continue;
                    var key = new JournalObjectiveKey(quest.Id, objective.Id);
                    var visible = new VisibleObjective(key, questContent.Title, objectiveContent, objective, authoritative.Revision);
                    _visible[key] = visible;
                    objectives.Add(new ObjectiveBuild(visible, o));
                }

                quests.Add(new QuestBuild(quest, questContent, objectives, q));
            }

            var standalone = new List<ObjectiveBuild>();
            for (var o = 0; o < authoritative.StandaloneObjectives.Count; o++)
            {
                ObjectiveProgressSnapshot objective = authoritative.StandaloneObjectives[o];
                if (!_catalog.TryGetStandaloneObjective(objective.Id, out ObjectivePresentationContent content)) continue;
                if (!IsVisible(objective.State, content.VisibleWhileInactive)) continue;
                JournalObjectiveKey key = JournalObjectiveKey.Standalone(objective.Id);
                var visible = new VisibleObjective(key, string.Empty, content, objective, authoritative.Revision);
                _visible[key] = visible;
                standalone.Add(new ObjectiveBuild(visible, o));
            }

            ReconcileLocalSelection();
            ApplyQuestOrdering(quests);
            var projectedQuests = new List<JournalQuestEntry>();
            for (var q = 0; q < quests.Count; q++)
            {
                QuestBuild source = quests[q];
                var objectives = new List<JournalObjectiveEntry>();
                ApplyObjectiveOrdering(source.Objectives);
                for (var o = 0; o < source.Objectives.Count; o++)
                {
                    VisibleObjective visible = source.Objectives[o].Visible;
                    if (!MatchesFilter(visible.Progress.State)) continue;
                    objectives.Add(ToEntry(visible));
                }

                if (_preferences.FilterMode != JournalFilterMode.AllVisible && objectives.Count == 0) continue;
                projectedQuests.Add(new JournalQuestEntry(
                    source.Progress.Id,
                    source.Content.Title,
                    source.Progress.State,
                    authoritative.Revision,
                    _preferences.IsCollapsed(source.Progress.Id),
                    objectives));
            }

            ApplyObjectiveOrdering(standalone);
            var projectedStandalone = new List<JournalObjectiveEntry>();
            for (var i = 0; i < standalone.Count; i++)
            {
                VisibleObjective visible = standalone[i].Visible;
                if (!MatchesFilter(visible.Progress.State)) continue;
                projectedStandalone.Add(ToEntry(visible));
            }

            _current = new QuestJournalSnapshot(
                authoritative.Revision,
                _preferences.SortMode,
                _preferences.FilterMode,
                projectedQuests,
                projectedStandalone);
            return _current;
        }

        public bool SelectObjective(JournalObjectiveKey key)
        {
            if (!_visible.ContainsKey(key)) return false;
            _preferences.Selected = key;
            ReprojectLocalFlags();
            return true;
        }

        public bool TrackObjective(JournalObjectiveKey key)
        {
            if (!_visible.ContainsKey(key)) return false;
            _preferences.Tracked = key;
            ReprojectLocalFlags();
            return true;
        }

        public void ClearTracking()
        {
            _preferences.Tracked = null;
            ReprojectLocalFlags();
        }

        public void SetQuestCollapsed(QuestId questId, bool collapsed)
        {
            if (!questId.IsValid) throw new ArgumentException("Quest id is required.", nameof(questId));
            _preferences.SetCollapsed(questId, collapsed);
            ReprojectLocalFlags();
        }

        public void SetSortMode(JournalSortMode sortMode)
        {
            _preferences.SortMode = sortMode;
            Rebuild();
        }

        public void SetFilterMode(JournalFilterMode filterMode)
        {
            _preferences.FilterMode = filterMode;
            Rebuild();
        }

        public bool TryGetTrackedObjective(out TrackedObjectiveSummary summary)
        {
            if (_preferences.Tracked.HasValue && _visible.TryGetValue(_preferences.Tracked.Value, out VisibleObjective visible))
            {
                summary = new TrackedObjectiveSummary(
                    visible.Key,
                    visible.QuestTitle,
                    visible.Content.Label,
                    visible.Progress.State,
                    visible.Progress.CurrentCount,
                    visible.Progress.RequiredCount,
                    visible.ProgressionRevision);
                return true;
            }

            summary = default;
            return false;
        }

        private void ReconcileLocalSelection()
        {
            if (_preferences.Selected.HasValue && !_visible.ContainsKey(_preferences.Selected.Value)) _preferences.Selected = null;
            if (_preferences.Tracked.HasValue && !_visible.ContainsKey(_preferences.Tracked.Value)) _preferences.Tracked = null;
        }

        private void ReprojectLocalFlags()
        {
            if (_current == null) return;
            var quests = new List<JournalQuestEntry>(_current.Quests.Count);
            for (var q = 0; q < _current.Quests.Count; q++)
            {
                JournalQuestEntry source = _current.Quests[q];
                var objectives = new List<JournalObjectiveEntry>(source.Objectives.Count);
                for (var o = 0; o < source.Objectives.Count; o++) objectives.Add(Reproject(source.Objectives[o]));
                quests.Add(new JournalQuestEntry(
                    source.QuestId,
                    source.Title,
                    source.State,
                    source.AuthoritativeRevision,
                    _preferences.IsCollapsed(source.QuestId),
                    objectives));
            }

            var standalone = new List<JournalObjectiveEntry>(_current.StandaloneObjectives.Count);
            for (var i = 0; i < _current.StandaloneObjectives.Count; i++)
                standalone.Add(Reproject(_current.StandaloneObjectives[i]));

            _current = new QuestJournalSnapshot(
                _current.ProgressionRevision,
                _preferences.SortMode,
                _preferences.FilterMode,
                quests,
                standalone);
        }

        private JournalObjectiveEntry Reproject(JournalObjectiveEntry entry) => new JournalObjectiveEntry(
            entry.Key,
            entry.Label,
            entry.Detail,
            entry.State,
            entry.CurrentCount,
            entry.RequiredCount,
            entry.AuthoritativeRevision,
            IsSelected(entry.Key),
            IsTracked(entry.Key));

        private JournalObjectiveEntry ToEntry(VisibleObjective visible) => new JournalObjectiveEntry(
            visible.Key,
            visible.Content.Label,
            visible.Content.Detail,
            visible.Progress.State,
            visible.Progress.CurrentCount,
            visible.Progress.RequiredCount,
            visible.ProgressionRevision,
            IsSelected(visible.Key),
            IsTracked(visible.Key));

        private bool IsSelected(JournalObjectiveKey key) => _preferences.Selected.HasValue && _preferences.Selected.Value == key;
        private bool IsTracked(JournalObjectiveKey key) => _preferences.Tracked.HasValue && _preferences.Tracked.Value == key;

        private bool MatchesFilter(ProgressionLifecycleState state)
        {
            switch (_preferences.FilterMode)
            {
                case JournalFilterMode.ActiveOnly: return state == ProgressionLifecycleState.Active;
                case JournalFilterMode.Incomplete: return state != ProgressionLifecycleState.Completed;
                default: return true;
            }
        }

        private static bool IsVisible(ProgressionLifecycleState state, bool visibleWhileInactive) =>
            state != ProgressionLifecycleState.Inactive || visibleWhileInactive;

        private void ApplyQuestOrdering(List<QuestBuild> quests)
        {
            if (_preferences.SortMode == JournalSortMode.Title)
                quests.Sort((a, b) => StringComparer.Ordinal.Compare(a.Content.Title, b.Content.Title));
            else
                quests.Sort((a, b) => a.Content.Order != b.Content.Order
                    ? a.Content.Order.CompareTo(b.Content.Order)
                    : a.OriginalIndex.CompareTo(b.OriginalIndex));
        }

        private void ApplyObjectiveOrdering(List<ObjectiveBuild> objectives)
        {
            if (_preferences.SortMode == JournalSortMode.Title)
                objectives.Sort((a, b) => StringComparer.Ordinal.Compare(a.Visible.Content.Label, b.Visible.Content.Label));
            else
                objectives.Sort((a, b) => a.Visible.Content.Order != b.Visible.Content.Order
                    ? a.Visible.Content.Order.CompareTo(b.Visible.Content.Order)
                    : a.OriginalIndex.CompareTo(b.OriginalIndex));
        }

        private readonly struct VisibleObjective
        {
            public JournalObjectiveKey Key { get; }
            public string QuestTitle { get; }
            public ObjectivePresentationContent Content { get; }
            public ObjectiveProgressSnapshot Progress { get; }
            public ulong ProgressionRevision { get; }

            public VisibleObjective(JournalObjectiveKey key, string questTitle, ObjectivePresentationContent content, ObjectiveProgressSnapshot progress, ulong progressionRevision)
            {
                Key = key;
                QuestTitle = questTitle;
                Content = content;
                Progress = progress;
                ProgressionRevision = progressionRevision;
            }
        }

        private sealed class QuestBuild
        {
            public QuestProgressSnapshot Progress { get; }
            public QuestPresentationContent Content { get; }
            public List<ObjectiveBuild> Objectives { get; }
            public int OriginalIndex { get; }

            public QuestBuild(QuestProgressSnapshot progress, QuestPresentationContent content, List<ObjectiveBuild> objectives, int originalIndex)
            {
                Progress = progress;
                Content = content;
                Objectives = objectives;
                OriginalIndex = originalIndex;
            }
        }

        private readonly struct ObjectiveBuild
        {
            public VisibleObjective Visible { get; }
            public int OriginalIndex { get; }

            public ObjectiveBuild(VisibleObjective visible, int originalIndex)
            {
                Visible = visible;
                OriginalIndex = originalIndex;
            }
        }
    }
}
