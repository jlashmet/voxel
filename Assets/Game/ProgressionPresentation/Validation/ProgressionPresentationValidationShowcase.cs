using System;
using System.Collections.Generic;
using Game.Progression.Api;
using Game.ProgressionPresentation.Api;
using Game.ProgressionPresentation.Runtime;
using UnityEngine;

namespace Game.ProgressionPresentation.Validation
{
    public sealed class ProgressionPresentationValidationShowcase : MonoBehaviour
    {
        private static readonly QuestId Quest = new QuestId("quest:old-road");
        private static readonly ObjectiveId ReachGate = new ObjectiveId("objective:reach-gate");
        private static readonly ObjectiveId OpenGate = new ObjectiveId("objective:open-gate");

        private ValidationQuery _query;
        private ValidationCatalog _catalog;
        private JournalLocalPreferences _preferences;
        private QuestJournalPresenter _presenter;
        private float _startedAt;
        private bool _revealed;
        private bool _rebuilt;

        private void Start()
        {
            EnsureValidationCamera();
            _startedAt = Time.unscaledTime;
            _query = new ValidationQuery(Snapshot(1, ProgressionLifecycleState.Active, Objective(ReachGate, ProgressionLifecycleState.Active, 1, 3, 1), Objective(OpenGate, ProgressionLifecycleState.Inactive, 0, 1, 1)));
            _catalog = new ValidationCatalog();
            _preferences = new JournalLocalPreferences();
            _presenter = new QuestJournalPresenter(_query, _catalog, _preferences);
            QuestJournalSnapshot journal = _presenter.Rebuild();
            _presenter.TrackObjective(new JournalObjectiveKey(Quest, ReachGate));
            Debug.Log("PROGRESSION_PRESENTATION_VALIDATION ready: revision=" + journal.ProgressionRevision + " visibleObjectives=" + journal.Quests[0].Objectives.Count + " tracked=objective:reach-gate");
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - _startedAt;
            if (!_revealed && elapsed >= 3f)
            {
                _query.Current = Snapshot(2, ProgressionLifecycleState.Active, Objective(ReachGate, ProgressionLifecycleState.Completed, 3, 3, 2), Objective(OpenGate, ProgressionLifecycleState.Active, 0, 1, 2));
                QuestJournalSnapshot journal = _presenter.Rebuild();
                Debug.Log("PROGRESSION_PRESENTATION_VALIDATION reveal: revision=" + journal.ProgressionRevision + " visibleObjectives=" + journal.Quests[0].Objectives.Count + " firstState=" + journal.Quests[0].Objectives[0].State);
                _revealed = true;
            }

            if (!_rebuilt && elapsed >= 6f)
            {
                _presenter.TrackObjective(new JournalObjectiveKey(Quest, OpenGate));
                _presenter = new QuestJournalPresenter(_query, _catalog, _preferences);
                QuestJournalSnapshot journal = _presenter.Rebuild();
                bool hasTracked = _presenter.TryGetTrackedObjective(out TrackedObjectiveSummary tracked);
                Debug.Log("PROGRESSION_PRESENTATION_VALIDATION rebuild-stable: revision=" + journal.ProgressionRevision + " tracked=" + (hasTracked ? tracked.Key.ObjectiveId.Value : "none") + " authorityReads=" + _query.ReadCount);
                _rebuilt = true;
            }
        }

        private void OnGUI()
        {
            if (_presenter == null) return;
            QuestJournalSnapshot journal = _presenter.Current;
            GUI.Box(new Rect(36, 30, 760, 500), string.Empty);
            GUI.Label(new Rect(60, 50, 700, 35), "QUEST JOURNAL  •  authoritative revision " + journal.ProgressionRevision);
            if (journal.Quests.Count == 0) return;
            JournalQuestEntry quest = journal.Quests[0];
            GUI.Label(new Rect(60, 95, 700, 30), quest.Title + "  •  " + quest.State);
            for (var i = 0; i < quest.Objectives.Count; i++)
            {
                JournalObjectiveEntry objective = quest.Objectives[i];
                float y = 145 + i * 90;
                string marker = objective.IsTracked ? "[TRACKED] " : string.Empty;
                GUI.Label(new Rect(80, y, 660, 28), marker + objective.Label + "  •  " + objective.State);
                GUI.Label(new Rect(100, y + 30, 640, 26), objective.Detail);
                GUI.Label(new Rect(100, y + 56, 640, 24), "Progress " + objective.CurrentCount + "/" + objective.RequiredCount + "  rev " + objective.AuthoritativeRevision);
            }

            GUI.Box(new Rect(830, 30, 410, 230), string.Empty);
            GUI.Label(new Rect(855, 50, 360, 30), "COMPACT HUD PROJECTION");
            if (_presenter.TryGetTrackedObjective(out TrackedObjectiveSummary tracked))
            {
                GUI.Label(new Rect(855, 100, 350, 28), tracked.QuestTitle);
                GUI.Label(new Rect(875, 132, 330, 28), tracked.ObjectiveLabel);
                GUI.Label(new Rect(875, 165, 330, 24), tracked.State + "  " + tracked.CurrentCount + "/" + tracked.RequiredCount);
                GUI.Label(new Rect(875, 195, 330, 24), "Source revision " + tracked.ProgressionRevision);
            }
        }

        private static void EnsureValidationCamera()
        {
            if (Camera.main != null) return;
            var cameraObject = new GameObject("Progression Presentation Validation Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.045f, 0.03f, 1f);
        }

        private static ObjectiveProgressSnapshot Objective(ObjectiveId id, ProgressionLifecycleState state, int current, int required, ulong revision) => new ObjectiveProgressSnapshot(id, state, current, required, revision);
        private static ProgressionSnapshot Snapshot(ulong revision, ProgressionLifecycleState questState, params ObjectiveProgressSnapshot[] objectives) => new ProgressionSnapshot(revision, new[] { new QuestProgressSnapshot(Quest, questState, objectives, revision) }, Array.Empty<ObjectiveProgressSnapshot>());

        private sealed class ValidationQuery : IProgressionQuery
        {
            public ProgressionSnapshot Current { get; set; }
            public int ReadCount { get; private set; }
            public ValidationQuery(ProgressionSnapshot current) => Current = current;
            public ProgressionSnapshot Snapshot() { ReadCount++; return Current; }
        }

        private sealed class ValidationCatalog : IProgressionPresentationCatalog
        {
            private readonly Dictionary<ObjectiveId, ObjectivePresentationContent> _objectives = new Dictionary<ObjectiveId, ObjectivePresentationContent>
            {
                { ReachGate, new ObjectivePresentationContent(ReachGate, "Reach the old gate", "Follow the road to the ruined gate.", 10) },
                { OpenGate, new ObjectivePresentationContent(OpenGate, "Open the gate", "Find a way through without revealing future objectives early.", 20) }
            };
            public bool TryGetQuest(QuestId questId, out QuestPresentationContent content)
            {
                content = new QuestPresentationContent(Quest, "The Old Road", 10);
                return questId == Quest;
            }
            public bool TryGetObjective(QuestId questId, ObjectiveId objectiveId, out ObjectivePresentationContent content) => questId == Quest && _objectives.TryGetValue(objectiveId, out content);
            public bool TryGetStandaloneObjective(ObjectiveId objectiveId, out ObjectivePresentationContent content) { content = default; return false; }
        }
    }
}
