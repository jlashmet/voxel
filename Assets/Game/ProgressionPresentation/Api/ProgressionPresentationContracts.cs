using System;
using System.Collections.Generic;
using Game.Progression.Api;

namespace Game.ProgressionPresentation.Api
{
    public enum JournalSortMode : byte
    {
        AuthoritativeOrder = 0,
        Title = 1
    }

    public enum JournalFilterMode : byte
    {
        AllVisible = 0,
        ActiveOnly = 1,
        Incomplete = 2
    }

    public readonly struct QuestPresentationContent
    {
        public QuestId QuestId { get; }
        public string Title { get; }
        public int Order { get; }
        public bool VisibleWhileInactive { get; }

        public QuestPresentationContent(QuestId questId, string title, int order, bool visibleWhileInactive = false)
        {
            if (!questId.IsValid) throw new ArgumentException("Quest id is required.", nameof(questId));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Quest title is required.", nameof(title));
            QuestId = questId;
            Title = title;
            Order = order;
            VisibleWhileInactive = visibleWhileInactive;
        }
    }

    public readonly struct ObjectivePresentationContent
    {
        public ObjectiveId ObjectiveId { get; }
        public string Label { get; }
        public string Detail { get; }
        public int Order { get; }
        public bool VisibleWhileInactive { get; }

        public ObjectivePresentationContent(ObjectiveId objectiveId, string label, string detail, int order, bool visibleWhileInactive = false)
        {
            if (!objectiveId.IsValid) throw new ArgumentException("Objective id is required.", nameof(objectiveId));
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Objective label is required.", nameof(label));
            ObjectiveId = objectiveId;
            Label = label;
            Detail = detail ?? string.Empty;
            Order = order;
            VisibleWhileInactive = visibleWhileInactive;
        }
    }

    public interface IProgressionPresentationCatalog
    {
        bool TryGetQuest(QuestId questId, out QuestPresentationContent content);
        bool TryGetObjective(QuestId questId, ObjectiveId objectiveId, out ObjectivePresentationContent content);
        bool TryGetStandaloneObjective(ObjectiveId objectiveId, out ObjectivePresentationContent content);
    }

    /// <summary>
    /// Typed replicated current-state payload for System19. Transport and serialization remain owned by
    /// GameplayReplication; this payload simply carries the canonical System11 snapshot.
    /// </summary>
    public readonly struct ProgressionPresentationCurrentState
    {
        public ProgressionSnapshot Snapshot { get; }

        public ProgressionPresentationCurrentState(ProgressionSnapshot snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }
    }

    public readonly struct JournalObjectiveKey : IEquatable<JournalObjectiveKey>
    {
        public QuestId QuestId { get; }
        public ObjectiveId ObjectiveId { get; }
        public bool IsStandalone { get; }
        public bool IsValid => ObjectiveId.IsValid && (IsStandalone || QuestId.IsValid);

        public JournalObjectiveKey(QuestId questId, ObjectiveId objectiveId)
        {
            if (!questId.IsValid) throw new ArgumentException("Quest id is required.", nameof(questId));
            if (!objectiveId.IsValid) throw new ArgumentException("Objective id is required.", nameof(objectiveId));
            QuestId = questId;
            ObjectiveId = objectiveId;
            IsStandalone = false;
        }

        private JournalObjectiveKey(ObjectiveId objectiveId)
        {
            if (!objectiveId.IsValid) throw new ArgumentException("Objective id is required.", nameof(objectiveId));
            QuestId = default;
            ObjectiveId = objectiveId;
            IsStandalone = true;
        }

        public static JournalObjectiveKey Standalone(ObjectiveId objectiveId) => new JournalObjectiveKey(objectiveId);

        public bool Equals(JournalObjectiveKey other) =>
            IsStandalone == other.IsStandalone && QuestId == other.QuestId && ObjectiveId == other.ObjectiveId;
        public override bool Equals(object obj) => obj is JournalObjectiveKey other && Equals(other);
        public override int GetHashCode() => ((QuestId.GetHashCode() * 397) ^ ObjectiveId.GetHashCode()) * 397 ^ IsStandalone.GetHashCode();
        public static bool operator ==(JournalObjectiveKey left, JournalObjectiveKey right) => left.Equals(right);
        public static bool operator !=(JournalObjectiveKey left, JournalObjectiveKey right) => !left.Equals(right);
    }

    public readonly struct JournalObjectiveEntry
    {
        public JournalObjectiveKey Key { get; }
        public string Label { get; }
        public string Detail { get; }
        public ProgressionLifecycleState State { get; }
        public int CurrentCount { get; }
        public int RequiredCount { get; }
        public ulong AuthoritativeRevision { get; }
        public bool IsSelected { get; }
        public bool IsTracked { get; }

        public JournalObjectiveEntry(
            JournalObjectiveKey key,
            string label,
            string detail,
            ProgressionLifecycleState state,
            int currentCount,
            int requiredCount,
            ulong authoritativeRevision,
            bool isSelected,
            bool isTracked)
        {
            Key = key;
            Label = label ?? string.Empty;
            Detail = detail ?? string.Empty;
            State = state;
            CurrentCount = currentCount;
            RequiredCount = requiredCount;
            AuthoritativeRevision = authoritativeRevision;
            IsSelected = isSelected;
            IsTracked = isTracked;
        }
    }

    public sealed class JournalQuestEntry
    {
        private readonly JournalObjectiveEntry[] _objectives;

        public QuestId QuestId { get; }
        public string Title { get; }
        public ProgressionLifecycleState State { get; }
        public ulong AuthoritativeRevision { get; }
        public bool IsCollapsed { get; }
        public IReadOnlyList<JournalObjectiveEntry> Objectives => _objectives;

        public JournalQuestEntry(QuestId questId, string title, ProgressionLifecycleState state, ulong authoritativeRevision, bool isCollapsed, IReadOnlyList<JournalObjectiveEntry> objectives)
        {
            if (!questId.IsValid) throw new ArgumentException("Quest id is required.", nameof(questId));
            if (objectives == null) throw new ArgumentNullException(nameof(objectives));
            QuestId = questId;
            Title = title ?? string.Empty;
            State = state;
            AuthoritativeRevision = authoritativeRevision;
            IsCollapsed = isCollapsed;
            _objectives = new JournalObjectiveEntry[objectives.Count];
            for (var i = 0; i < objectives.Count; i++) _objectives[i] = objectives[i];
        }
    }

    public sealed class QuestJournalSnapshot
    {
        private readonly JournalQuestEntry[] _quests;
        private readonly JournalObjectiveEntry[] _standaloneObjectives;

        public ulong ProgressionRevision { get; }
        public JournalSortMode SortMode { get; }
        public JournalFilterMode FilterMode { get; }
        public IReadOnlyList<JournalQuestEntry> Quests => _quests;
        public IReadOnlyList<JournalObjectiveEntry> StandaloneObjectives => _standaloneObjectives;

        public QuestJournalSnapshot(
            ulong progressionRevision,
            JournalSortMode sortMode,
            JournalFilterMode filterMode,
            IReadOnlyList<JournalQuestEntry> quests,
            IReadOnlyList<JournalObjectiveEntry> standaloneObjectives)
        {
            if (quests == null) throw new ArgumentNullException(nameof(quests));
            if (standaloneObjectives == null) throw new ArgumentNullException(nameof(standaloneObjectives));
            ProgressionRevision = progressionRevision;
            SortMode = sortMode;
            FilterMode = filterMode;
            _quests = new JournalQuestEntry[quests.Count];
            for (var i = 0; i < quests.Count; i++) _quests[i] = quests[i];
            _standaloneObjectives = new JournalObjectiveEntry[standaloneObjectives.Count];
            for (var i = 0; i < standaloneObjectives.Count; i++) _standaloneObjectives[i] = standaloneObjectives[i];
        }
    }

    public readonly struct TrackedObjectiveSummary
    {
        public JournalObjectiveKey Key { get; }
        public string QuestTitle { get; }
        public string ObjectiveLabel { get; }
        public ProgressionLifecycleState State { get; }
        public int CurrentCount { get; }
        public int RequiredCount { get; }
        public ulong ProgressionRevision { get; }

        public TrackedObjectiveSummary(JournalObjectiveKey key, string questTitle, string objectiveLabel, ProgressionLifecycleState state, int currentCount, int requiredCount, ulong progressionRevision)
        {
            Key = key;
            QuestTitle = questTitle ?? string.Empty;
            ObjectiveLabel = objectiveLabel ?? string.Empty;
            State = state;
            CurrentCount = currentCount;
            RequiredCount = requiredCount;
            ProgressionRevision = progressionRevision;
        }
    }

    public interface ITrackedObjectiveProjection
    {
        bool TryGetTrackedObjective(out TrackedObjectiveSummary summary);
    }

    public interface IQuestJournalPresentation
    {
        QuestJournalSnapshot Current { get; }
        QuestJournalSnapshot Rebuild();
        bool SelectObjective(JournalObjectiveKey key);
        bool TrackObjective(JournalObjectiveKey key);
        void ClearTracking();
        void SetQuestCollapsed(QuestId questId, bool collapsed);
        void SetSortMode(JournalSortMode sortMode);
        void SetFilterMode(JournalFilterMode filterMode);
    }
}
