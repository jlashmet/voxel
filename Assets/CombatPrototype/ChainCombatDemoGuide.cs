using System.Collections;
using System.Reflection;
using UnityEngine;

namespace MountingForce.CombatPrototype
{
    /// <summary>
    /// Lightweight onboarding/showcase layer for the runtime-created combat lab.
    /// It does not own combat rules; it drives the same deterministic board and reservation
    /// coordinators that normal hot-seat play uses.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    [RequireComponent(typeof(ChainCombatLabController))]
    public sealed class ChainCombatDemoGuide : MonoBehaviour
    {
        private static readonly FieldInfo BoardField = typeof(ChainCombatLabController).GetField(
            "_board", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ReservationField = typeof(ChainCombatLabController).GetField(
            "_reactionReservations", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ReadinessField = typeof(ChainCombatLabController).GetField(
            "_roundReadiness", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ConstructsDirtyField = typeof(ChainCombatLabController).GetField(
            "_constructsDirty", BindingFlags.Instance | BindingFlags.NonPublic);

        private const float StepDelaySeconds = 0.85f;

        private ChainCombatLabController _controller;
        private ChainCombatBoard _board;
        private ChainReactionReservationCoordinator _reservations;
        private ChainRoundReadinessCoordinator _readiness;
        private ChainExecutionPlanner _planner;
        private IChainCombatEnvironmentBridge _environmentBridge;
        private ChainCombatDemoScenario _scenario;
        private ChainCombatBoard _scenarioBoard;
        private Coroutine _showcaseRoutine;
        private bool _expanded = true;
        private string _status = "Press PLAY EXAMPLE CASCADE to see the combat idea before experimenting yourself.";
        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _accent;

        public bool IsPlayingExample => _showcaseRoutine != null;

        private void Awake()
        {
            _controller = GetComponent<ChainCombatLabController>();
            _planner = GetComponent<ChainExecutionPlanner>();
            ResolveDependencies();
            EnsureScenario();
        }

        private void Update()
        {
            ResolveDependencies();
            EnsureScenario();
        }

        private void OnDisable()
        {
            if (_showcaseRoutine != null)
            {
                StopCoroutine(_showcaseRoutine);
                _showcaseRoutine = null;
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            float width = Mathf.Clamp(Screen.width - 880f, 360f, 700f);
            float x = Mathf.Max(8f, (Screen.width - width) * 0.5f);
            float height = _expanded ? 174f : 36f;
            Rect panel = new Rect(x, 8f, width, height);
            GUI.Box(panel, GUIContent.none);

            GUILayout.BeginArea(new Rect(panel.x + 10f, panel.y + 7f, panel.width - 20f, panel.height - 12f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("CHAIN COMBAT DEMO", _title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_expanded ? "Hide guide" : "Show guide", GUILayout.Width(92f)))
            {
                _expanded = !_expanded;
            }
            GUILayout.EndHorizontal();

            if (!_expanded)
            {
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label("The point of this prototype is cooperative cause-and-effect: one player creates a physical fact, another player turns it into the next fact, and the environment becomes part of the combo.", _body);

            GUILayout.BeginHorizontal();
            GUI.enabled = !IsPlayingExample && _board != null;
            if (GUILayout.Button("PLAY EXAMPLE CASCADE", GUILayout.Height(30f)))
            {
                PlayExampleCascade();
            }
            if (GUILayout.Button("Advance one example step", GUILayout.Height(30f)))
            {
                AdvanceOneStep();
            }
            if (GUILayout.Button("Reset guided demo", GUILayout.Height(30f)))
            {
                ResetGuidedDemo();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (_scenario != null)
            {
                GUILayout.Label(_scenario.CurrentStepLabel, _accent);
            }

            GUILayout.Label(_status, _body);
            if (_board != null && (_board.CurrentCascadeSteps > 0 || _board.LastCascadeSteps > 0))
            {
                int steps = _board.CurrentCascadeSteps > 0 ? _board.CurrentCascadeSteps : _board.LastCascadeSteps;
                int players = _board.CurrentCascadeSteps > 0 ? _board.CurrentCascadePlayers : _board.LastCascadePlayers;
                int handoffs = _board.CurrentCascadeSteps > 0 ? _board.CurrentHandoffs : _board.LastHandoffs;
                GUILayout.Label($"Cascade telemetry: {steps} steps • {players} players • {handoffs} handoffs", _accent);
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// Higher-level game composition may inject production environment capabilities without
        /// making CombatPrototype reference their runtime implementations.
        /// </summary>
        public void SetEnvironmentBridge(IChainCombatEnvironmentBridge environmentBridge)
        {
            if (ReferenceEquals(_environmentBridge, environmentBridge))
            {
                return;
            }

            _environmentBridge = environmentBridge;
            _scenario = null;
            _scenarioBoard = null;
            ResolveDependencies();
            EnsureScenario();
        }

        public void PlayExampleCascade()
        {
            if (_board == null || _reservations == null)
            {
                _status = "Combat demo dependencies are not ready.";
                return;
            }

            ResetGuidedDemo();
            _showcaseRoutine = StartCoroutine(RunExampleCascade());
        }

        public void AdvanceOneStep()
        {
            if (IsPlayingExample)
            {
                return;
            }

            EnsureScenario();
            if (_scenario == null)
            {
                _status = "Combat demo scenario is unavailable.";
                return;
            }

            if (_scenario.IsComplete)
            {
                _status = "Example already complete. Reset the guided demo to run it again.";
                return;
            }

            _status = _scenario.TryAdvance() ? _scenario.LastMessage : "Example stopped: " + _scenario.LastMessage;
        }

        public void ResetGuidedDemo()
        {
            if (_showcaseRoutine != null)
            {
                StopCoroutine(_showcaseRoutine);
                _showcaseRoutine = null;
            }

            ResolveDependencies();
            if (_board == null)
            {
                _status = "Combat board is unavailable.";
                return;
            }

            _board.Reset();
            _reservations?.Reset();
            _readiness?.Reset();
            _planner?.ResetForBattle();
            if (ConstructsDirtyField != null && _controller != null)
            {
                ConstructsDirtyField.SetValue(_controller, true);
            }

            _scenario = null;
            _scenarioBoard = null;
            EnsureScenario();
            _status = "Guided demo reset. The normal combat UI is still fully interactive.";
        }

        private IEnumerator RunExampleCascade()
        {
            _status = "Starting from the normal initial board. Watch the event markers and handoffs between players.";
            yield return new WaitForSecondsRealtime(0.5f);

            while (_scenario != null && !_scenario.IsComplete)
            {
                string step = _scenario.CurrentStepLabel;
                _status = step;
                yield return new WaitForSecondsRealtime(StepDelaySeconds);

                if (!_scenario.TryAdvance())
                {
                    _status = "Example stopped: " + _scenario.LastMessage;
                    _showcaseRoutine = null;
                    yield break;
                }

                _status = _scenario.LastMessage;
                yield return new WaitForSecondsRealtime(StepDelaySeconds);
            }

            if (_scenario != null && _scenario.IsComplete)
            {
                _status = $"Example complete. This was one causal chain across {_board.LastCascadePlayers} players with {_board.LastHandoffs} player-to-player handoffs. Now reset and try to invent a different one.";
            }

            _showcaseRoutine = null;
        }

        private void ResolveDependencies()
        {
            if (_controller == null)
            {
                _controller = GetComponent<ChainCombatLabController>();
            }

            if (_planner == null)
            {
                _planner = GetComponent<ChainExecutionPlanner>();
            }

            if (_controller == null)
            {
                return;
            }

            if (BoardField != null)
            {
                _board = BoardField.GetValue(_controller) as ChainCombatBoard;
            }

            if (ReservationField != null)
            {
                _reservations = ReservationField.GetValue(_controller) as ChainReactionReservationCoordinator;
            }

            if (ReadinessField != null)
            {
                _readiness = ReadinessField.GetValue(_controller) as ChainRoundReadinessCoordinator;
            }
        }

        private void EnsureScenario()
        {
            if (_board == null || _reservations == null)
            {
                return;
            }

            if (_scenario == null || !ReferenceEquals(_scenarioBoard, _board))
            {
                _scenario = new ChainCombatDemoScenario(_board, _reservations, _environmentBridge);
                _scenarioBoard = _board;
            }
        }

        private void EnsureStyles()
        {
            if (_title != null)
            {
                return;
            }

            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true
            };
            _accent = new GUIStyle(_body)
            {
                fontStyle = FontStyle.Bold
            };
        }
    }
}
