using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MountingForce.CombatPrototype
{
    /// <summary>
    /// V9 planning surface: players author an ordered shared plan on the left, drag root action blocks to reorder them,
    /// and watch a deterministic ghost replay driven by the exact ChainCombatBoard rules. Reactions remain nested under
    /// the root that caused them; they are not independently draggable ahead of their trigger.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(ChainCombatLabController))]
    public sealed class ChainExecutionPlanner : MonoBehaviour
    {
        private enum AimMode
        {
            None,
            Move,
            Strike,
            Uppercut,
            Gust,
            ShoulderTarget,
            ShoulderAim,
            PortalEntrance,
            PortalExit,
            Amplifier,
            ConvergeMoving,
            ConvergeAnchor,
            Harpoon,
            NotchTree,
            NotchDirection,
            ReactionTarget,
            ReactionAim
        }

        private const float MainSidebarWidth = 420f;
        private const float PanelX = 10f;
        private const float PanelY = 10f;
        private const float PanelWidth = 390f;
        private const float PreviewStepSeconds = 0.85f;

        private static readonly FieldInfo BoardField = typeof(ChainCombatLabController).GetField(
            "_board", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SelectedField = typeof(ChainCombatLabController).GetField(
            "_selectedUnitId", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ReadinessField = typeof(ChainCombatLabController).GetField(
            "_roundReadiness", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly ChainExecutionPlan _plan = new ChainExecutionPlan();
        private readonly Dictionary<int, GameObject> _ghostUnits = new Dictionary<int, GameObject>();

        private ChainCombatLabController _controller;
        private ChainCombatBoard _board;
        private ChainRoundReadinessCoordinator _roundReadiness;
        private ChainExecutionPreview _preview;
        private Camera _camera;
        private GameObject _ghostRoot;
        private Vector2 _scroll;
        private AimMode _aim;
        private int _stagedTargetId;
        private int _stagedSecondaryId;
        private int _stagedTreeId;
        private GridPos _stagedPosition;
        private ChainReactionAbility _stagedReactionAbility;
        private ChainReactionKind _stagedReactionKind;
        private string _message;
        private int _observedRevision = -1;
        private int _boardFingerprint;
        private int _frameIndex;
        private bool _autoPlay = true;
        private float _nextFrameAt;
        private int _dragRootPlanId;
        private GUIStyle _header;
        private GUIStyle _small;
        private GUIStyle _action;

        public ChainExecutionPlan Plan => _plan;
        public ChainExecutionPreview Preview => _preview;
        public ChainCombatBoard PreviewBoard => _preview?.FinalBoard ?? _board;
        public bool TeamReadyToExecute => _roundReadiness != null && _roundReadiness.AllLivingPlayersReady;
        public Rect PanelRect => new Rect(PanelX, PanelY, PanelWidth, Mathf.Max(240f, Screen.height - PanelY - 10f));

        private void Awake()
        {
            _controller = GetComponent<ChainCombatLabController>();
            _message = "Author the future here. Drag root actions to reorder; their reaction continuations move with them.";
            ResolveBoard();
            RebuildPreview(true);
        }

        public void ResetForBattle()
        {
            _plan.ResetWithoutHistory();
            _frameIndex = 0;
            _dragRootPlanId = 0;
            _autoPlay = true;
            ResetAim(false);
            ResolveBoard();
            RebuildPreview(true);
            _message = "Battle reset. Shared plan, edit history, aim state, and ghost playback were reset.";
        }

        private void OnDestroy()
        {
            if (_ghostRoot != null) Destroy(_ghostRoot);
        }

        private void Update()
        {
            ResolveBoard();
            ResolveCamera();
            if (_board == null) return;

            int fingerprint = Fingerprint(_board);
            if (_preview == null || _observedRevision != _plan.Revision || fingerprint != _boardFingerprint)
                RebuildPreview(false);

            if (_autoPlay && _preview != null && _preview.Frames.Count > 1 && Time.unscaledTime >= _nextFrameAt)
            {
                _frameIndex = (_frameIndex + 1) % _preview.Frames.Count;
                _nextFrameAt = Time.unscaledTime + PreviewStepSeconds;
                SyncGhosts();
            }
        }

        private void OnGUI()
        {
            if (_board == null || SelectedField == null) return;
            EnsureStyles();
            DrawPlanPanel();
            DrawGhostLabels();
            HandleBoardClick(Event.current);
        }

        private void ResolveBoard()
        {
            if (_controller == null) _controller = GetComponent<ChainCombatLabController>();
            if (_controller != null && BoardField != null)
                _board = BoardField.GetValue(_controller) as ChainCombatBoard;
            if (_controller != null && ReadinessField != null)
                _roundReadiness = ReadinessField.GetValue(_controller) as ChainRoundReadinessCoordinator;
        }

        private void ResolveCamera()
        {
            if (_camera != null) return;
            GameObject cameraObject = GameObject.Find("Chain Combat Lab Camera");
            if (cameraObject != null) _camera = cameraObject.GetComponent<Camera>();
        }

        private void RebuildPreview(bool forceStartFrame)
        {
            if (_board == null) return;
            _preview = ChainExecutionPlanSimulator.Simulate(_board, _plan.Actions);
            _observedRevision = _plan.Revision;
            _boardFingerprint = Fingerprint(_board);
            if (forceStartFrame) _frameIndex = 0;
            _frameIndex = Mathf.Clamp(_frameIndex, 0, Mathf.Max(0, _preview.Frames.Count - 1));
            _nextFrameAt = Time.unscaledTime + PreviewStepSeconds;
            EnsureGhostObjects();
            SyncGhosts();
        }

        private void DrawPlanPanel()
        {
            Rect panel = PanelRect;
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label("EXECUTION PLAN — GHOST", _header);
            GUILayout.Label("This is the proactive authoring surface. Shared order is explicit; reactions stay causally attached to their root.", _small);

            DrawPreviewTransport();
            GUILayout.Space(4f);
            DrawSelectedToolbox();
            GUILayout.Space(5f);
            DrawPlanActions();
            GUILayout.EndArea();
        }

        private void DrawPreviewTransport()
        {
            int frameCount = _preview?.Frames.Count ?? 0;
            string frameLabel = frameCount == 0 ? "No preview" : $"Ghost {_frameIndex + 1}/{frameCount}: {_preview.Frames[_frameIndex].Label}";
            GUILayout.Label(frameLabel, _small);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_autoPlay ? "Pause ghost" : "Play ghost"))
            {
                _autoPlay = !_autoPlay;
                _nextFrameAt = Time.unscaledTime + PreviewStepSeconds;
            }
            GUI.enabled = frameCount > 1;
            if (GUILayout.Button("◀")) StepFrame(-1);
            if (GUILayout.Button("▶")) StepFrame(1);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = _plan.CanUndo;
            if (GUILayout.Button("Undo"))
            {
                _plan.Undo();
                _message = "Undid the last plan edit.";
            }
            GUI.enabled = _plan.CanRedo;
            if (GUILayout.Button("Redo"))
            {
                _plan.Redo();
                _message = "Redid the plan edit.";
            }
            GUI.enabled = _plan.HasActions;
            if (GUILayout.Button("Clear"))
            {
                _plan.Clear();
                ResetAim(false);
                _message = "Cleared the shared execution plan.";
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (_preview != null && _preview.HasFailure)
                GUILayout.Label("GHOST STOPS: " + _preview.FailureMessage, _header);
            else if (_preview?.FinalBoard?.PendingReaction != null)
                GUILayout.Label($"GHOST STOPS AT {_preview.FinalBoard.PendingReaction.Kind}: choose a planned reaction or deliberately let it continue.", _header);

            bool canExecute = _plan.HasActions && _preview != null && !_preview.HasFailure && !_board.BattleOver && TeamReadyToExecute;
            GUI.enabled = canExecute;
            if (GUILayout.Button(TeamReadyToExecute ? "EXECUTE APPROVED SHARED PLAN" : "WAITING FOR ALL PLAYERS READY")) ExecutePlan();
            GUI.enabled = true;
            if (_plan.HasActions && !TeamReadyToExecute)
                GUILayout.Label("Every living player must mark Ready on the right before the shared plan can become real.", _small);

            GUILayout.Label(_message, _small);
        }

        private void DrawSelectedToolbox()
        {
            ChainCombatBoard previewBoard = PreviewBoard;
            if (previewBoard == null) return;

            int selectedId = (int)SelectedField.GetValue(_controller);
            ChainUnitState selected = previewBoard.GetUnit(selectedId);
            if (selected == null || !selected.IsAlive || selected.Team != CombatTeam.Friendly)
            {
                GUILayout.Label("Select a friendly recruit on the battlefield to author its instruction.", _small);
                return;
            }

            GUILayout.Label($"PLAN TOOLBOX — P{selected.CommandGroup} {selected.Name}", _header);
            GUILayout.Label($"ghost position {selected.Position} • move {(selected.MoveSpent ? "spent" : "ready")} • action {(selected.ActionSpent ? "spent" : "ready")} • reaction {(selected.ReactionSpent ? "spent" : "ready")}", _small);

            bool playerReady = _roundReadiness != null && _roundReadiness.IsReady(selected.CommandGroup);
            if (playerReady)
            {
                if (_aim != AimMode.None) ResetAim(false);
                GUILayout.Label($"P{selected.CommandGroup} is READY. Unready that player on the right before changing any shared-plan instruction. Live reactions remain available on the right during execution.", _small);
                return;
            }

            ChainReactionOpportunity pending = previewBoard.PendingReaction;
            if (pending != null)
            {
                GUILayout.Label($"Current ghost event: {pending.Kind} — {pending.Description}", _small);
                ChainReactionAbility ability = ReactionFor(selected.Kind);
                if (ability != ChainReactionAbility.None)
                {
                    if (GUILayout.Button($"Plan {ChainCombatBoard.AbilityName(ability)}")) BeginReaction(selected, pending, ability);
                    GUILayout.Label(ReactionDescription(selected.Kind), _small);
                }
                if (GUILayout.Button("Plan: let this event continue"))
                {
                    _plan.Add(ChainPlannedAction.Pass(pending.Kind));
                    ResetAim(false);
                    _message = $"Added a deliberate pass for {pending.Kind}.";
                }
                return;
            }

            if (_aim != AimMode.None)
            {
                GUILayout.Label("AIMING: " + AimName(_aim), _header);
                if (GUILayout.Button("Cancel plan aim")) ResetAim();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Plan Move")) BeginAim(AimMode.Move, "Choose the ghost destination.");
            if (GUILayout.Button("Plan Strike")) BeginAim(AimMode.Strike, "Choose a ghost enemy target.");
            GUILayout.EndHorizontal();

            switch (selected.Kind)
            {
                case ChainRecruitKind.Stephen:
                    if (GUILayout.Button("Plan Uppercut")) BeginAim(AimMode.Uppercut, "Choose the enemy Stephen should launch.");
                    break;
                case ChainRecruitKind.Brutus:
                    if (GUILayout.Button("Plan Shoulder Hurl")) BeginAim(AimMode.ShoulderTarget, "Choose the adjacent enemy, then its throw direction.");
                    break;
                case ChainRecruitKind.Weldon:
                    if (GUILayout.Button("Plan Gust")) BeginAim(AimMode.Gust, "Choose the enemy Weldon should push.");
                    break;
                case ChainRecruitKind.Madeline:
                    if (GUILayout.Button("Plan Converge")) BeginAim(AimMode.ConvergeMoving, "Choose the enemy to move, then the enemy to drive it toward.");
                    break;
                case ChainRecruitKind.Mira:
                    if (GUILayout.Button("Plan linked portals")) BeginAim(AimMode.PortalEntrance, "Choose portal entrance, then exit.");
                    if (GUILayout.Button("Plan force ×2")) BeginAim(AimMode.Amplifier, "Choose the force multiplier cell.");
                    break;
                case ChainRecruitKind.Grom:
                    if (GUILayout.Button("Plan tree notch")) BeginAim(AimMode.NotchTree, "Choose a standing tree, then its prepared fall direction.");
                    break;
                case ChainRecruitKind.Skitter:
                    if (GUILayout.Button("Plan Harpoon")) BeginAim(AimMode.Harpoon, "Choose the enemy Skitter should pull.");
                    break;
            }
        }

        private void DrawPlanActions()
        {
            GUILayout.Label("SHARED ACTION ORDER", _header);
            GUILayout.Label("Drag a non-indented row to move that whole causal block. × removes a root and its attached reactions.", _small);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Mathf.Max(130f, PanelRect.height - 430f)));
            Event current = Event.current;
            int rootOrdinal = -1;

            for (int i = 0; i < _plan.Actions.Count; i++)
            {
                ChainPlannedAction planned = _plan.Actions[i];
                if (!planned.IsReaction) rootOrdinal++;

                float indent = planned.IsReaction ? 22f : 0f;
                Rect row = GUILayoutUtility.GetRect(0f, 38f, GUILayout.ExpandWidth(true));
                Rect body = new Rect(row.x + indent, row.y, row.width - indent - 27f, row.height);
                Rect remove = new Rect(row.xMax - 24f, row.y + 7f, 22f, 22f);

                ChainPlanActionResult result = _preview?.ResultFor(planned.PlanId);
                string status = result == null ? "…" : result.Succeeded ? "✓" : "✕";
                string drag = planned.IsReaction ? "" : "☰ ";
                GUI.Box(body, $"{drag}{status}  {planned.Describe(_board)}", _action);
                if (GUI.Button(remove, "×"))
                {
                    _plan.Remove(planned.PlanId);
                    _message = planned.IsReaction ? "Removed the planned reaction." : "Removed the root action and its attached reaction chain.";
                    ResetAim(false);
                    current.Use();
                    break;
                }

                if (!planned.IsReaction)
                {
                    if (current.type == EventType.MouseDown && current.button == 0 && body.Contains(current.mousePosition))
                    {
                        _dragRootPlanId = planned.PlanId;
                        _plan.BeginCompoundEdit();
                        _autoPlay = false;
                        current.Use();
                    }
                    else if (current.type == EventType.MouseDrag && _dragRootPlanId != 0 && body.Contains(current.mousePosition))
                    {
                        if (_plan.MoveRootAction(_dragRootPlanId, rootOrdinal))
                            _message = "Reordering… ghost recalculates from the new shared order.";
                        current.Use();
                    }
                }
            }

            if (current.type == EventType.MouseUp && _dragRootPlanId != 0)
            {
                _plan.EndCompoundEdit();
                _dragRootPlanId = 0;
                _message = "Reorder committed as one undoable plan edit.";
                current.Use();
            }

            if (_plan.Actions.Count == 0)
                GUILayout.Label("No authored actions yet. Select a recruit and add a move/setup/action above.", _small);

            GUILayout.EndScrollView();
        }

        private void HandleBoardClick(Event current)
        {
            if (current == null || current.type != EventType.MouseDown || current.button != 0) return;
            if (PanelRect.Contains(current.mousePosition)) return;
            if (current.mousePosition.x >= Screen.width - MainSidebarWidth) return;
            if (!TryMouseToGrid(current.mousePosition, out GridPos cell)) return;

            ChainCombatBoard previewBoard = PreviewBoard;
            if (previewBoard == null) return;
            ChainUnitState clicked = previewBoard.FindUnitAt(cell);
            ChainTreeState clickedTree = previewBoard.FindStandingTreeAt(cell);
            int selectedId = (int)SelectedField.GetValue(_controller);
            ChainUnitState selected = previewBoard.GetUnit(selectedId);

            if (_aim == AimMode.None)
            {
                if (clicked != null && clicked.Team == CombatTeam.Friendly)
                {
                    SelectedField.SetValue(_controller, clicked.Id);
                    _message = $"Selected ghost P{clicked.CommandGroup} {clicked.Name}.";
                    current.Use();
                }
                return;
            }

            if (selected == null || !selected.IsAlive || selected.Team != CombatTeam.Friendly)
            {
                _message = "Select a living friendly recruit before authoring an action.";
                ResetAim(false);
                current.Use();
                return;
            }

            ChainPlannedAction planned = null;
            switch (_aim)
            {
                case AimMode.Move:
                    planned = ChainPlannedAction.Move(selected.CommandGroup, selected.Id, cell);
                    break;
                case AimMode.Strike:
                    if (!RequireEnemy(clicked, "Strike", current)) return;
                    planned = ChainPlannedAction.BasicHit(selected.CommandGroup, selected.Id, clicked.Id);
                    break;
                case AimMode.Uppercut:
                    if (!RequireEnemy(clicked, "Uppercut", current)) return;
                    planned = ChainPlannedAction.Uppercut(selected.CommandGroup, selected.Id, clicked.Id);
                    break;
                case AimMode.Gust:
                    if (!RequireEnemy(clicked, "Gust", current)) return;
                    planned = ChainPlannedAction.Gust(selected.CommandGroup, selected.Id, clicked.Id);
                    break;
                case AimMode.ShoulderTarget:
                    if (!RequireEnemy(clicked, "Shoulder Hurl", current)) return;
                    _stagedTargetId = clicked.Id;
                    _aim = AimMode.ShoulderAim;
                    _message = $"Hurl target: {clicked.Name}. Choose the throw direction.";
                    current.Use();
                    return;
                case AimMode.ShoulderAim:
                    planned = ChainPlannedAction.ShoulderHurl(selected.CommandGroup, selected.Id, _stagedTargetId, cell);
                    break;
                case AimMode.PortalEntrance:
                    _stagedPosition = cell;
                    _aim = AimMode.PortalExit;
                    _message = $"Portal entrance {cell}. Choose the linked exit.";
                    current.Use();
                    return;
                case AimMode.PortalExit:
                    planned = ChainPlannedAction.PortalPair(selected.CommandGroup, selected.Id, _stagedPosition, cell);
                    break;
                case AimMode.Amplifier:
                    planned = ChainPlannedAction.Amplifier(selected.CommandGroup, selected.Id, cell);
                    break;
                case AimMode.ConvergeMoving:
                    if (!RequireEnemy(clicked, "Converge", current)) return;
                    _stagedTargetId = clicked.Id;
                    _aim = AimMode.ConvergeAnchor;
                    _message = $"Converge will move {clicked.Name}. Choose the enemy it should be driven toward.";
                    current.Use();
                    return;
                case AimMode.ConvergeAnchor:
                    if (!RequireEnemy(clicked, "Converge", current)) return;
                    _stagedSecondaryId = clicked.Id;
                    planned = ChainPlannedAction.Converge(selected.CommandGroup, selected.Id, _stagedTargetId, _stagedSecondaryId);
                    break;
                case AimMode.Harpoon:
                    if (!RequireEnemy(clicked, "Harpoon", current)) return;
                    planned = ChainPlannedAction.Harpoon(selected.CommandGroup, selected.Id, clicked.Id);
                    break;
                case AimMode.NotchTree:
                    if (clickedTree == null)
                    {
                        _message = "Notch: choose a standing tree.";
                        current.Use();
                        return;
                    }
                    _stagedTreeId = clickedTree.Id;
                    _aim = AimMode.NotchDirection;
                    _message = $"Tree #{clickedTree.Id} selected. Choose its prepared fall direction.";
                    current.Use();
                    return;
                case AimMode.NotchDirection:
                    planned = ChainPlannedAction.NotchTree(selected.CommandGroup, selected.Id, _stagedTreeId, cell);
                    break;
                case AimMode.ReactionTarget:
                    if (clicked == null)
                    {
                        _message = "Choose a creature involved in the ghost event.";
                        current.Use();
                        return;
                    }
                    _stagedTargetId = clicked.Id;
                    _aim = AimMode.ReactionAim;
                    _message = $"Reaction target: {clicked.Name}. Choose the reaction direction.";
                    current.Use();
                    return;
                case AimMode.ReactionAim:
                    planned = ChainPlannedAction.React(
                        selected.CommandGroup,
                        selected.Id,
                        _stagedReactionAbility,
                        _stagedReactionKind,
                        _stagedTargetId,
                        cell);
                    break;
            }

            if (planned != null)
            {
                int id = _plan.Add(planned);
                ResetAim(false);
                RebuildPreview(false);
                ChainPlanActionResult result = _preview?.ResultFor(id);
                _message = result == null || result.Succeeded
                    ? "Added to the shared plan. The ghost has been recalculated."
                    : "Added, but the ghost stops here: " + result.Message;
            }
            current.Use();
        }

        private void BeginReaction(ChainUnitState selected, ChainReactionOpportunity pending, ChainReactionAbility ability)
        {
            _stagedReactionAbility = ability;
            _stagedReactionKind = pending.Kind;
            _stagedTargetId = 0;

            bool chooseParticipant = ability == ChainReactionAbility.Repulse ||
                                     ability == ChainReactionAbility.FollowThrough ||
                                     (ability == ChainReactionAbility.HookYank && pending.Kind == ChainReactionKind.Collision);
            if (chooseParticipant)
            {
                _aim = AimMode.ReactionTarget;
                _message = $"Plan {ChainCombatBoard.AbilityName(ability)}: choose an event participant, then aim.";
            }
            else
            {
                if (ability == ChainReactionAbility.HookYank) _stagedTargetId = pending.PrimaryUnitId;
                _aim = AimMode.ReactionAim;
                _message = $"Plan {ChainCombatBoard.AbilityName(ability)}: choose its direction.";
            }
        }

        private bool RequireEnemy(ChainUnitState clicked, string verb, Event current)
        {
            if (clicked != null && clicked.Team == CombatTeam.Enemy) return true;
            _message = verb + ": choose a living enemy in the current ghost state.";
            current.Use();
            return false;
        }

        private void ExecutePlan()
        {
            if (!TeamReadyToExecute)
            {
                _message = "Every living player must mark Ready before the shared plan can execute.";
                return;
            }
            if (_preview == null || _preview.HasFailure)
            {
                _message = "Fix the stopped ghost before executing the shared plan.";
                return;
            }

            if (!ChainExecutionPlanSimulator.ExecuteAll(_board, _plan.Actions, out string result))
            {
                _message = "Authoritative execution diverged: " + result;
                RebuildPreview(true);
                return;
            }

            int count = _plan.Actions.Count;
            _plan.ResetWithoutHistory();
            _frameIndex = 0;
            ResetAim(false);
            RebuildPreview(true);
            _message = $"Executed {count} approved instruction(s) on the authoritative board. Any unresolved event is now available for live improvisation.";
        }

        private void BeginAim(AimMode mode, string message)
        {
            _aim = mode;
            _stagedTargetId = 0;
            _stagedSecondaryId = 0;
            _stagedTreeId = 0;
            _stagedReactionAbility = ChainReactionAbility.None;
            _stagedReactionKind = ChainReactionKind.None;
            _message = message;
        }

        private void ResetAim(bool updateMessage = true)
        {
            _aim = AimMode.None;
            _stagedTargetId = 0;
            _stagedSecondaryId = 0;
            _stagedTreeId = 0;
            _stagedReactionAbility = ChainReactionAbility.None;
            _stagedReactionKind = ChainReactionKind.None;
            if (updateMessage) _message = "Plan aim cancelled.";
        }

        private void StepFrame(int delta)
        {
            if (_preview == null || _preview.Frames.Count == 0) return;
            _autoPlay = false;
            _frameIndex = (_frameIndex + delta + _preview.Frames.Count) % _preview.Frames.Count;
            SyncGhosts();
        }

        private bool TryMouseToGrid(Vector2 guiMouse, out GridPos cell)
        {
            cell = new GridPos(0, 0);
            if (_camera == null || _board == null) return false;
            Vector3 screenPoint = new Vector3(guiMouse.x, Screen.height - guiMouse.y, 0f);
            Ray ray = _camera.ScreenPointToRay(screenPoint);
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float enter)) return false;
            Vector3 world = ray.GetPoint(enter);
            GridPos candidate = new GridPos(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
            if (!_board.IsInBounds(candidate)) return false;
            cell = candidate;
            return true;
        }

        private void EnsureGhostObjects()
        {
            if (_board == null) return;
            if (_ghostRoot == null)
            {
                _ghostRoot = new GameObject("Chain Plan Ghost Visuals");
                _ghostRoot.transform.SetParent(transform, false);
            }

            for (int i = 0; i < _board.Units.Count; i++)
            {
                ChainUnitState unit = _board.Units[i];
                if (_ghostUnits.ContainsKey(unit.Id)) continue;
                GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                ghost.name = $"Chain Plan Ghost - {unit.Name}";
                ghost.transform.SetParent(_ghostRoot.transform, false);
                Collider collider = ghost.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                Renderer renderer = ghost.GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = new Color(0.3f, 0.9f, 1f, 0.35f);
                _ghostUnits[unit.Id] = ghost;
            }
        }

        private void SyncGhosts()
        {
            if (_preview == null || _preview.Frames.Count == 0) return;
            ChainCombatBoard frame = _preview.Frames[Mathf.Clamp(_frameIndex, 0, _preview.Frames.Count - 1)].Board;
            if (frame == null) return;

            foreach (KeyValuePair<int, GameObject> pair in _ghostUnits)
            {
                ChainUnitState unit = frame.GetUnit(pair.Key);
                GameObject ghost = pair.Value;
                if (ghost == null) continue;
                bool visible = unit != null && unit.IsAlive;
                ghost.SetActive(visible);
                if (!visible) continue;
                float y = unit.Airborne ? 2.5f : 0.72f;
                ghost.transform.position = new Vector3(unit.Position.X, y, unit.Position.Z);
                ghost.transform.localScale = GhostScale(unit);
            }
        }

        private void DrawGhostLabels()
        {
            if (_camera == null || _preview == null || _preview.Frames.Count == 0) return;
            ChainCombatBoard frame = _preview.Frames[Mathf.Clamp(_frameIndex, 0, _preview.Frames.Count - 1)].Board;
            if (frame == null) return;

            for (int i = 0; i < frame.Units.Count; i++)
            {
                ChainUnitState unit = frame.Units[i];
                if (!unit.IsAlive) continue;
                Vector3 screen = _camera.WorldToScreenPoint(new Vector3(unit.Position.X, unit.Airborne ? 4.1f : 2.55f, unit.Position.Z));
                if (screen.z <= 0f) continue;
                float x = screen.x - 64f;
                float y = Screen.height - screen.y - 16f;
                if (x >= Screen.width - MainSidebarWidth - 128f) continue;
                GUI.Label(new Rect(x, y, 128f, 32f), $"GHOST {unit.Name}\n{unit.Hp}/{unit.MaxHp}", _small);
            }

            if (frame.PendingReaction != null)
            {
                ChainReactionOpportunity pending = frame.PendingReaction;
                Vector3 screen = _camera.WorldToScreenPoint(new Vector3(pending.Position.X, 3.2f, pending.Position.Z));
                if (screen.z > 0f)
                    GUI.Label(new Rect(screen.x - 85f, Screen.height - screen.y - 22f, 170f, 44f), $"GHOST EVENT\n{pending.Kind} • force {pending.ImpactForce}", _header);
            }
        }

        private static int Fingerprint(ChainCombatBoard board)
        {
            unchecked
            {
                int hash = board.Round * 397 ^ (board.PendingReaction?.Id ?? 0);
                for (int i = 0; i < board.Units.Count; i++)
                {
                    ChainUnitState u = board.Units[i];
                    hash = hash * 31 + u.Id;
                    hash = hash * 31 + u.Position.X;
                    hash = hash * 31 + u.Position.Z;
                    hash = hash * 31 + u.Hp;
                    hash = hash * 31 + (u.MoveSpent ? 1 : 0);
                    hash = hash * 31 + (u.ActionSpent ? 1 : 0);
                    hash = hash * 31 + (u.ReactionSpent ? 1 : 0);
                }
                for (int i = 0; i < board.Trees.Count; i++)
                {
                    ChainTreeState tree = board.Trees[i];
                    hash = hash * 31 + tree.Id;
                    hash = hash * 31 + (tree.Standing ? 1 : 0);
                    hash = hash * 31 + tree.Stress;
                }
                if (board.PortalA.HasValue) hash = hash * 31 + board.PortalA.Value.GetHashCode();
                if (board.PortalB.HasValue) hash = hash * 31 + board.PortalB.Value.GetHashCode();
                hash = hash * 31 + board.Amplifiers.Count;
                return hash;
            }
        }

        private static ChainReactionAbility ReactionFor(ChainRecruitKind kind)
        {
            switch (kind)
            {
                case ChainRecruitKind.Stephen: return ChainReactionAbility.FollowThrough;
                case ChainRecruitKind.Brutus: return ChainReactionAbility.CatchThrow;
                case ChainRecruitKind.Weldon: return ChainReactionAbility.Crosswind;
                case ChainRecruitKind.Madeline: return ChainReactionAbility.Repulse;
                case ChainRecruitKind.Grom: return ChainReactionAbility.Timber;
                case ChainRecruitKind.Skitter: return ChainReactionAbility.HookYank;
                default: return ChainReactionAbility.None;
            }
        }

        private static string ReactionDescription(ChainRecruitKind kind)
        {
            switch (kind)
            {
                case ChainRecruitKind.Stephen: return "Follow Through: collision participant → chosen direction, force 5.";
                case ChainRecruitKind.Brutus: return "Catch & Throw: airborne body → chosen direction, force 7.";
                case ChainRecruitKind.Weldon: return "Crosswind: redirect an airborne body's existing momentum.";
                case ChainRecruitKind.Madeline: return "Repulse: collision participant → chosen direction, force 4.";
                case ChainRecruitKind.Grom: return "Timber: struck tree → chosen fall direction.";
                case ChainRecruitKind.Skitter: return "Hook Yank: collision/tree victim → pull toward Skitter, force 5.";
                default: return string.Empty;
            }
        }

        private static string AimName(AimMode mode)
        {
            switch (mode)
            {
                case AimMode.Move: return "MOVE DESTINATION";
                case AimMode.Strike: return "STRIKE TARGET";
                case AimMode.Uppercut: return "UPPERCUT TARGET";
                case AimMode.Gust: return "GUST TARGET";
                case AimMode.ShoulderTarget: return "HURL TARGET";
                case AimMode.ShoulderAim: return "HURL DIRECTION";
                case AimMode.PortalEntrance: return "PORTAL ENTRANCE";
                case AimMode.PortalExit: return "PORTAL EXIT";
                case AimMode.Amplifier: return "FORCE ×2 CELL";
                case AimMode.ConvergeMoving: return "CONVERGE MOVING TARGET";
                case AimMode.ConvergeAnchor: return "CONVERGE ANCHOR TARGET";
                case AimMode.Harpoon: return "HARPOON TARGET";
                case AimMode.NotchTree: return "TREE TO NOTCH";
                case AimMode.NotchDirection: return "NOTCH DIRECTION";
                case AimMode.ReactionTarget: return "REACTION PARTICIPANT";
                case AimMode.ReactionAim: return "REACTION DIRECTION";
                default: return "NONE";
            }
        }

        private static Vector3 GhostScale(ChainUnitState unit)
        {
            if (unit.Kind == ChainRecruitKind.Ogre) return new Vector3(0.86f, 1.05f, 0.86f);
            if (unit.Kind == ChainRecruitKind.Goblin) return new Vector3(0.5f, 0.58f, 0.5f);
            return new Vector3(0.58f, 0.7f, 0.58f);
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, wordWrap = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true };
            _action = new GUIStyle(GUI.skin.box) { fontSize = 10, alignment = TextAnchor.MiddleLeft, wordWrap = true, padding = new RectOffset(7, 5, 3, 3) };
        }
    }
}
