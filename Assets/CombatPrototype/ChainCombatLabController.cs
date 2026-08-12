using System.Collections.Generic;
using UnityEngine;

namespace MountingForce.CombatPrototype
{
    /// <summary>
    /// Playable hot-seat stand-in for four network players. It exposes what each recruit does, but never computes
    /// or highlights compatible reactions. A player first reserves a physical event, then chooses a recruit/capability
    /// from their own roster and finally aims the reaction. Reservation therefore solves multiplayer ownership without
    /// turning the UI into a combo recommender.
    /// </summary>
    public sealed class ChainCombatLabController : MonoBehaviour
    {
        private const float SidebarWidth = 420f;

        private enum CommandMode
        {
            None,
            Move,
            Strike,
            Uppercut,
            Gust,
            ShoulderPick,
            ShoulderAim,
            PortalEntrance,
            PortalExit,
            Amplifier,
            ReactionPick,
            ReactionAim
        }

        private readonly Dictionary<int, GameObject> _unitVisuals = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _treeVisuals = new Dictionary<int, GameObject>();
        private readonly List<GameObject> _constructVisuals = new List<GameObject>();

        private ChainCombatBoard _board;
        private ChainReactionReservationCoordinator _reactionReservations;
        private ChainRoundReadinessCoordinator _roundReadiness;
        private Camera _camera;
        private GameObject _visualRoot;
        private int _selectedUnitId;
        private int _stagedTargetId;
        private GridPos _portalEntrance;
        private bool _hasPortalEntrance;
        private ChainReactionAbility _activeReactionAbility;
        private CommandMode _command;
        private string _uiMessage;
        private Vector2 _scroll;
        private Vector2 _logScroll;
        private GUIStyle _titleStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _smallStyle;
        private bool _constructsDirty;

        private Rect SidebarRect => new Rect(Screen.width - SidebarWidth, 0f, SidebarWidth, Screen.height);

        private void Awake()
        {
            _board = new ChainCombatBoard();
            _reactionReservations = new ChainReactionReservationCoordinator(_board);
            _roundReadiness = new ChainRoundReadinessCoordinator(_board);
            _uiMessage = "Four-player hot-seat lab: use one active recruit, reserve physical events, and mark each player Ready when proactive play is done.";
            BuildPresentation();
            SelectFirstFriendly();
            _constructsDirty = true;
        }

        private void Update()
        {
            if (_camera != null && Screen.width > 0)
            {
                float viewportWidth = Mathf.Clamp01((Screen.width - SidebarWidth) / Screen.width);
                _camera.rect = new Rect(0f, 0f, viewportWidth, 1f);
            }

            _reactionReservations?.Synchronize();
            _roundReadiness?.Synchronize();
            SyncUnits();
            SyncTrees();
            if (_constructsDirty)
            {
                RebuildConstructVisuals();
                _constructsDirty = false;
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawWorldLabels();
            DrawSidebar();
            HandleBoardMouse(Event.current);
        }

        private void BuildPresentation()
        {
            _visualRoot = new GameObject("Chain Combat Lab Visuals");
            _visualRoot.transform.SetParent(transform, false);
            BuildCamera();
            BuildLight();
            BuildGround();

            for (int i = 0; i < _board.Units.Count; i++)
            {
                ChainUnitState unit = _board.Units[i];
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = $"Chain Unit - {unit.Name}";
                visual.transform.SetParent(_visualRoot.transform, false);
                RemoveCollider(visual);
                SetColor(visual, UnitColor(unit));
                _unitVisuals[unit.Id] = visual;
            }

            for (int i = 0; i < _board.Trees.Count; i++)
            {
                ChainTreeState tree = _board.Trees[i];
                _treeVisuals[tree.Id] = CreateTree(tree);
            }
        }

        private void BuildCamera()
        {
            GameObject obj = new GameObject("Chain Combat Lab Camera");
            obj.transform.SetParent(transform, false);
            _camera = obj.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 7.2f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;
            _camera.clearFlags = CameraClearFlags.Skybox;
            _camera.transform.position = new Vector3(6.5f, 14f, -7.5f);
            _camera.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
        }

        private void BuildLight()
        {
            GameObject obj = new GameObject("Chain Combat Lab Light");
            obj.transform.SetParent(transform, false);
            Light light = obj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            obj.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        }

        private void BuildGround()
        {
            for (int x = 0; x < ChainCombatBoard.Width; x++)
            {
                for (int z = 0; z < ChainCombatBoard.Depth; z++)
                {
                    GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cell.name = $"Chain Cell {x},{z}";
                    cell.transform.SetParent(_visualRoot.transform, false);
                    cell.transform.position = new Vector3(x, -0.08f, z);
                    cell.transform.localScale = new Vector3(0.96f, 0.12f, 0.96f);
                    RemoveCollider(cell);
                    float shade = (x + z) % 2 == 0 ? 0.25f : 0.30f;
                    SetColor(cell, new Color(shade, shade, shade));
                }
            }
        }

        private GameObject CreateTree(ChainTreeState tree)
        {
            GameObject root = new GameObject($"Chain Tree {tree.Id}");
            root.transform.SetParent(_visualRoot.transform, false);

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            trunk.transform.localScale = new Vector3(0.34f, 0.9f, 0.34f);
            RemoveCollider(trunk);
            SetColor(trunk, new Color(0.36f, 0.18f, 0.07f));

            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.transform.SetParent(root.transform, false);
            crown.transform.localPosition = new Vector3(0f, 2.15f, 0f);
            crown.transform.localScale = new Vector3(1.35f, 1.25f, 1.35f);
            RemoveCollider(crown);
            SetColor(crown, new Color(0.16f, 0.52f, 0.18f));

            root.transform.position = World(tree.Position, 0f);
            return root;
        }

        private void SyncUnits()
        {
            if (_board == null) return;
            for (int i = 0; i < _board.Units.Count; i++)
            {
                ChainUnitState unit = _board.Units[i];
                if (!_unitVisuals.TryGetValue(unit.Id, out GameObject visual) || visual == null) continue;
                visual.SetActive(unit.IsAlive);
                if (!unit.IsAlive) continue;

                float y = unit.Airborne ? 2.35f : 0.62f;
                visual.transform.position = World(unit.Position, y);
                Vector3 scale = BaseScale(unit);
                if (unit.Id == _selectedUnitId) scale *= 1.2f;
                if (_board.PendingReaction != null && _board.PendingReaction.ClaimedByUnitId == unit.Id) scale *= 1.12f;
                visual.transform.localScale = scale;
            }
        }

        private void SyncTrees()
        {
            if (_board == null) return;
            for (int i = 0; i < _board.Trees.Count; i++)
            {
                ChainTreeState tree = _board.Trees[i];
                if (!_treeVisuals.TryGetValue(tree.Id, out GameObject visual) || visual == null) continue;
                if (tree.Standing)
                {
                    visual.transform.position = World(tree.Position, 0f);
                    visual.transform.rotation = Quaternion.identity;
                }
                else
                {
                    Vector3 direction = new Vector3(tree.FallDirection.X, 0f, tree.FallDirection.Z);
                    visual.transform.position = World(tree.Position, 0f) + direction * 1.55f;
                    if (direction.sqrMagnitude > 0.01f)
                        visual.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
                }
            }
        }

        private void RebuildConstructVisuals()
        {
            for (int i = 0; i < _constructVisuals.Count; i++)
            {
                if (_constructVisuals[i] != null) Destroy(_constructVisuals[i]);
            }
            _constructVisuals.Clear();

            if (_board.PortalA.HasValue) _constructVisuals.Add(CreatePad("Chain Portal A", _board.PortalA.Value, new Color(0.28f, 0.55f, 1f), PrimitiveType.Cylinder));
            if (_board.PortalB.HasValue) _constructVisuals.Add(CreatePad("Chain Portal B", _board.PortalB.Value, new Color(0.75f, 0.28f, 1f), PrimitiveType.Cylinder));
            foreach (GridPos position in _board.Amplifiers)
                _constructVisuals.Add(CreatePad("Chain Force x2", position, new Color(1f, 0.75f, 0.12f), PrimitiveType.Cube));
        }

        private GameObject CreatePad(string name, GridPos position, Color color, PrimitiveType type)
        {
            GameObject pad = GameObject.CreatePrimitive(type);
            pad.name = name;
            pad.transform.SetParent(_visualRoot.transform, false);
            pad.transform.position = World(position, 0.08f);
            pad.transform.localScale = type == PrimitiveType.Cylinder ? new Vector3(0.82f, 0.06f, 0.82f) : new Vector3(0.75f, 0.10f, 0.75f);
            RemoveCollider(pad);
            SetColor(pad, color);
            return pad;
        }

        private void DrawSidebar()
        {
            Rect panel = SidebarRect;
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 10f, 8f, panel.width - 20f, panel.height - 16f));
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("MOUNTING FORCE — CASCADE LAB", _titleStyle);
            GUILayout.Label($"Round {_board.Round} • active recruit + Ready • reserve → choose → execute", _smallStyle);
            GUILayout.Label($"Last cascade: {_board.LastCascadeSteps} steps / {_board.LastCascadePlayers} players   Best: {_board.BestCascadeSteps} / {_board.BestCascadePlayers}", _smallStyle);

            if (_board.CurrentCascadeSteps > 0)
                GUILayout.Label($"LIVE CASCADE: {_board.CurrentCascadeSteps} deliberate steps across {_board.CurrentCascadePlayers} player(s)", _headerStyle);

            GUILayout.Space(6f);
            DrawOpportunity();
            GUILayout.Space(6f);
            DrawRoster();
            GUILayout.Space(8f);

            ChainUnitState selected = _board.GetUnit(_selectedUnitId);
            if (selected == null || !selected.IsAlive)
            {
                SelectFirstFriendly();
                selected = _board.GetUnit(_selectedUnitId);
            }
            if (selected != null) DrawSelected(selected);

            if (_command != CommandMode.None)
            {
                GUILayout.Space(5f);
                GUILayout.Label($"AIMING: {CommandLabel(_command)}", _headerStyle);
                if (GUILayout.Button("Cancel current aim")) CancelCommand();
            }

            GUILayout.Space(8f);
            DrawReadiness();

            GUILayout.Space(8f);
            GUILayout.Label("STATUS", _headerStyle);
            GUILayout.Label(string.IsNullOrEmpty(_uiMessage) ? _board.LastMessage : _uiMessage, _smallStyle);

            if (GUILayout.Button("Reset battle"))
            {
                _board.Reset();
                _reactionReservations.Reset();
                _roundReadiness.Reset();
                _uiMessage = "Battle reset. Try a different route through the same geometry.";
                CancelCommand(false);
                SelectFirstFriendly();
                _constructsDirty = true;
            }

            GUILayout.Space(10f);
            GUILayout.Label("RECENT EVENTS", _headerStyle);
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(190f));
            int first = Mathf.Max(0, _board.Log.Count - 12);
            for (int i = first; i < _board.Log.Count; i++) GUILayout.Label("• " + _board.Log[i], _smallStyle);
            GUILayout.EndScrollView();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawReadiness()
        {
            GUILayout.Label("PLAYER READY", _headerStyle);
            GUILayout.Label("Ready means: no more proactive moves/actions this round. It does NOT disable reactions or event reservations.", _smallStyle);

            GUILayout.BeginHorizontal();
            for (int group = 1; group <= 4; group++)
            {
                bool ready = _roundReadiness.IsReady(group);
                GUI.enabled = !_board.BattleOver;
                if (GUILayout.Button(ready ? $"P{group} READY ✓" : $"P{group} Ready"))
                {
                    _roundReadiness.TrySetReady(group, !ready);
                    _uiMessage = _roundReadiness.LastMessage;
                    CancelCommand(false);
                }
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (_roundReadiness.AllLivingPlayersReady)
            {
                GUI.enabled = !_board.BattleOver && _board.PendingReaction == null;
                if (GUILayout.Button("All living players Ready → enemies act"))
                {
                    bool advanced = _roundReadiness.TryAdvanceRound();
                    _uiMessage = _roundReadiness.LastMessage;
                    if (advanced)
                    {
                        _reactionReservations.Reset();
                        CancelCommand(false);
                    }
                }
                GUI.enabled = true;

                if (_board.PendingReaction != null)
                    GUILayout.Label("Everyone is Ready, but the current physical event must still be resolved or passed before enemies act.", _smallStyle);
            }
            else
            {
                GUILayout.Label("Enemy phase is locked until every living player group is Ready.", _smallStyle);
            }
        }

        private void DrawOpportunity()
        {
            ChainReactionOpportunity reaction = _board.PendingReaction;
            if (reaction == null)
            {
                GUILayout.Label("No unresolved physical event.", _smallStyle);
                return;
            }

            _reactionReservations.Synchronize();
            int reservedBy = _reactionReservations.ReservedByCommandGroup;

            GUILayout.Label($"PHYSICAL EVENT #{reaction.Id}: {reaction.Kind}", _headerStyle);
            GUILayout.Label(reaction.Description, _smallStyle);

            if (reservedBy == 0)
            {
                GUILayout.Label("UNRESERVED — decide who will take responsibility for this physical fact. Reserving does not prove that player has a valid reaction.", _smallStyle);
                GUILayout.BeginHorizontal();
                for (int group = 1; group <= 4; group++)
                {
                    if (GUILayout.Button($"P{group} reserve"))
                    {
                        _reactionReservations.TryReserve(group);
                        _uiMessage = _reactionReservations.LastMessage;
                        CancelCommand(false);
                    }
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label($"RESERVED: P{reservedBy} — P{reservedBy} now chooses a recruit/ability from memory. Other players cannot steal the event.", _headerStyle);

                if (reaction.IsClaimed)
                {
                    ChainUnitState owner = _board.GetUnit(reaction.ClaimedByUnitId);
                    GUILayout.Label($"CONCRETE CHOICE: {owner?.Name} — {ChainCombatBoard.AbilityName(reaction.ClaimedAbility)}", _smallStyle);
                    if (owner != null && GUILayout.Button("Change recruit / ability (keep P reservation)"))
                    {
                        _reactionReservations.TryReleaseClaim(owner.Id);
                        _uiMessage = _reactionReservations.LastMessage;
                        CancelCommand(false);
                    }
                }
                else
                {
                    GUILayout.Label("No recruit/ability chosen yet. Select one of this player's recruits below and try the reaction you think applies.", _smallStyle);
                }

                if (GUILayout.Button($"P{reservedBy} releases event to everyone"))
                {
                    _reactionReservations.TryReleaseReservation(reservedBy);
                    _uiMessage = _reactionReservations.LastMessage;
                    CancelCommand(false);
                }
            }

            GUI.enabled = reservedBy == 0;
            if (GUILayout.Button("Everyone passes / let physics continue"))
            {
                _reactionReservations.TryPass();
                _uiMessage = _reactionReservations.LastMessage;
                CancelCommand(false);
            }
            GUI.enabled = true;

            if (reservedBy != 0)
                GUILayout.Label($"Passing is locked while P{reservedBy} owns the decision; that player must release it first.", _smallStyle);
        }

        private void DrawRoster()
        {
            GUILayout.Label("ROSTER MEMORY", _headerStyle);
            GUILayout.Label("This is your toolbox, not a combo list. Learn what each piece does and where it has to be.", _smallStyle);
            for (int i = 0; i < _board.Units.Count; i++)
            {
                ChainUnitState unit = _board.Units[i];
                if (unit.Team != CombatTeam.Friendly || !unit.IsAlive) continue;
                string marker = unit.Id == _selectedUnitId ? "▶ " : string.Empty;
                string ready = _roundReadiness.IsReady(unit.CommandGroup) ? " [READY]" : string.Empty;
                if (GUILayout.Button($"{marker}P{unit.CommandGroup} {unit.Name}{ready} — {RosterSummary(unit.Kind)}"))
                {
                    _selectedUnitId = unit.Id;
                    CancelCommand(false);
                    _uiMessage = $"Selected P{unit.CommandGroup} {unit.Name}.";
                }
            }
        }

        private void DrawSelected(ChainUnitState unit)
        {
            bool playerReady = _roundReadiness.IsReady(unit.CommandGroup);
            GUILayout.Label($"SELECTED — P{unit.CommandGroup} {unit.Name}", _headerStyle);
            GUILayout.Label($"HP {unit.Hp}/{unit.MaxHp} • move {(unit.MoveSpent ? "spent" : "ready")} • action {(unit.ActionSpent ? "spent" : "ready")} • reaction {(unit.ReactionSpent ? "spent" : "ready")} • player {(playerReady ? "READY" : "planning")}", _smallStyle);

            GUI.enabled = _board.PendingReaction == null && !playerReady;
            if (GUILayout.Button("Move")) BeginCommand(CommandMode.Move, "Click an empty cell within 3 Manhattan cells.");
            if (GUILayout.Button("Strike")) BeginCommand(CommandMode.Strike, "Click an adjacent enemy for a plain 1-damage strike.");

            switch (unit.Kind)
            {
                case ChainRecruitKind.Stephen:
                    if (GUILayout.Button("Uppercut")) BeginCommand(CommandMode.Uppercut, "Click an adjacent enemy. It launches away from Stephen with 5 momentum.");
                    GUILayout.Label("ACTION — Uppercut: launch an adjacent enemy away from Stephen.", _smallStyle);
                    break;
                case ChainRecruitKind.Brutus:
                    if (GUILayout.Button("Shoulder Hurl")) BeginCommand(CommandMode.ShoulderPick, "Click an adjacent enemy, then click a direction for the hurl.");
                    GUILayout.Label("ACTION — Shoulder Hurl: throw an adjacent enemy in a direction you choose with 5 force.", _smallStyle);
                    break;
                case ChainRecruitKind.Weldon:
                    if (GUILayout.Button("Gust")) BeginCommand(CommandMode.Gust, "Click an enemy within 4 cells. Gust pushes directly away from Weldon.");
                    GUILayout.Label("ACTION — Gust: push an enemy within 4 cells directly away from Weldon.", _smallStyle);
                    break;
                case ChainRecruitKind.Mira:
                    if (GUILayout.Button("Place linked portals"))
                    {
                        _command = CommandMode.PortalEntrance;
                        _hasPortalEntrance = false;
                        _uiMessage = "Click portal entrance, then exit. Moving bodies preserve direction and momentum.";
                    }
                    if (GUILayout.Button("Place force multiplier")) BeginCommand(CommandMode.Amplifier, "Click an empty cell within Mira's 6-cell range.");
                    GUILayout.Label("ACTION — Portal pair: preserve direction/momentum. Force multiplier: amplifies remaining force.", _smallStyle);
                    break;
            }
            GUI.enabled = true;

            if (playerReady)
                GUILayout.Label("P is Ready: proactive controls are closed, but reactions below remain fully available.", _smallStyle);

            ChainReactionAbility ability = ReactionFor(unit.Kind);
            if (ability != ChainReactionAbility.None)
            {
                GUILayout.Space(4f);
                GUILayout.Label($"REACTION — {ReactionDescription(unit.Kind)}", _smallStyle);

                ChainReactionOpportunity reaction = _board.PendingReaction;
                int reservedBy = _reactionReservations.ReservedByCommandGroup;
                bool canAttempt = reaction != null && reservedBy == unit.CommandGroup && !reaction.IsClaimed && !unit.ReactionSpent;
                GUI.enabled = canAttempt;
                if (GUILayout.Button($"Try {ChainCombatBoard.AbilityName(ability)} on P{unit.CommandGroup}'s reserved event"))
                    TryClaim(unit, ability);
                GUI.enabled = true;

                if (reaction != null && reservedBy == 0)
                    GUILayout.Label("Reserve the event for a player before choosing a concrete reaction.", _smallStyle);
                else if (reaction != null && reservedBy != unit.CommandGroup)
                    GUILayout.Label($"P{reservedBy} owns this decision. {unit.Name} cannot claim it unless that reservation is released.", _smallStyle);
            }
        }

        private void TryClaim(ChainUnitState unit, ChainReactionAbility ability)
        {
            if (!_reactionReservations.TryClaim(unit.Id, ability))
            {
                _uiMessage = _reactionReservations.LastMessage;
                return;
            }

            _activeReactionAbility = ability;
            _stagedTargetId = 0;
            ChainReactionOpportunity reaction = _board.PendingReaction;
            if (ability == ChainReactionAbility.Repulse || ability == ChainReactionAbility.FollowThrough)
            {
                _command = CommandMode.ReactionPick;
                _uiMessage = $"P{unit.CommandGroup} chose {unit.Name}. Click one of the collision participants, then aim.";
            }
            else if (ability == ChainReactionAbility.HookYank && reaction != null && reaction.Kind == ChainReactionKind.Collision)
            {
                _command = CommandMode.ReactionPick;
                _uiMessage = $"P{unit.CommandGroup} chose {unit.Name}. Choose a collision participant to hook, then aim the pull.";
            }
            else
            {
                if (ability == ChainReactionAbility.HookYank && reaction != null) _stagedTargetId = reaction.PrimaryUnitId;
                _command = CommandMode.ReactionAim;
                _uiMessage = $"P{unit.CommandGroup} owns the event with {unit.Name}. Aim {ChainCombatBoard.AbilityName(ability)} on the board.";
            }
        }

        private void HandleBoardMouse(Event current)
        {
            if (current == null || current.type != EventType.MouseDown || current.button != 0) return;
            if (SidebarRect.Contains(current.mousePosition)) return;
            if (!TryMouseToGrid(current.mousePosition, out GridPos cell)) return;

            ChainUnitState clicked = _board.FindUnitAt(cell);
            bool success = false;

            switch (_command)
            {
                case CommandMode.None:
                    if (clicked != null && clicked.Team == CombatTeam.Friendly)
                    {
                        _selectedUnitId = clicked.Id;
                        _uiMessage = $"Selected P{clicked.CommandGroup} {clicked.Name}.";
                    }
                    break;
                case CommandMode.Move:
                    success = TryProactive(() => _board.TryMove(_selectedUnitId, cell));
                    break;
                case CommandMode.Strike:
                    success = clicked != null && TryProactive(() => _board.TryBasicHit(_selectedUnitId, clicked.Id));
                    if (clicked == null) _uiMessage = "Click an adjacent enemy.";
                    break;
                case CommandMode.Uppercut:
                    success = clicked != null && TryProactive(() => _board.TryUppercut(_selectedUnitId, clicked.Id));
                    if (clicked == null) _uiMessage = "Click an adjacent enemy.";
                    break;
                case CommandMode.Gust:
                    success = clicked != null && TryProactive(() => _board.TryGust(_selectedUnitId, clicked.Id));
                    if (clicked == null) _uiMessage = "Click an enemy within Weldon's range.";
                    break;
                case CommandMode.ShoulderPick:
                    if (clicked == null)
                    {
                        _uiMessage = "Click an adjacent enemy for Brutus to hurl.";
                        current.Use();
                        return;
                    }
                    _stagedTargetId = clicked.Id;
                    _command = CommandMode.ShoulderAim;
                    _uiMessage = $"Shoulder Hurl target: {clicked.Name}. Now click the desired direction.";
                    current.Use();
                    return;
                case CommandMode.ShoulderAim:
                    success = TryProactive(() => _board.TryShoulderHurl(_selectedUnitId, _stagedTargetId, cell));
                    break;
                case CommandMode.PortalEntrance:
                    _portalEntrance = cell;
                    _hasPortalEntrance = true;
                    _command = CommandMode.PortalExit;
                    _uiMessage = $"Portal entrance at {cell}. Click the exit.";
                    current.Use();
                    return;
                case CommandMode.PortalExit:
                    if (_hasPortalEntrance)
                    {
                        success = TryProactive(() => _board.TryPlacePortalPair(_selectedUnitId, _portalEntrance, cell));
                        if (success) _constructsDirty = true;
                    }
                    break;
                case CommandMode.Amplifier:
                    success = TryProactive(() => _board.TryPlaceAmplifier(_selectedUnitId, cell));
                    if (success) _constructsDirty = true;
                    break;
                case CommandMode.ReactionPick:
                    if (clicked == null)
                    {
                        _uiMessage = "Choose a creature involved in the claimed physical event.";
                        current.Use();
                        return;
                    }
                    _stagedTargetId = clicked.Id;
                    _command = CommandMode.ReactionAim;
                    _uiMessage = $"Reaction target: {clicked.Name}. Now aim the reaction.";
                    current.Use();
                    return;
                case CommandMode.ReactionAim:
                    success = ExecuteReactionAim(cell);
                    break;
            }

            if (_command != CommandMode.None)
            {
                if (success)
                {
                    _reactionReservations.Synchronize();
                    _uiMessage = _board.LastMessage;
                    CancelCommand(false);
                }
                else if (!string.IsNullOrEmpty(_board.LastMessage) && string.IsNullOrEmpty(_uiMessage))
                {
                    _uiMessage = _board.LastMessage;
                }
            }

            current.Use();
        }

        private bool TryProactive(System.Func<bool> command)
        {
            ChainUnitState unit = _board.GetUnit(_selectedUnitId);
            if (unit == null) return false;
            if (!_roundReadiness.CanUseProactive(unit.CommandGroup))
            {
                _uiMessage = $"P{unit.CommandGroup} is Ready. Unready P{unit.CommandGroup} before taking another proactive action; reactions remain available.";
                return false;
            }
            return command();
        }

        private bool ExecuteReactionAim(GridPos cell)
        {
            switch (_activeReactionAbility)
            {
                case ChainReactionAbility.Crosswind: return _board.TryCrosswind(_selectedUnitId, cell);
                case ChainReactionAbility.CatchThrow: return _board.TryCatchThrow(_selectedUnitId, cell);
                case ChainReactionAbility.Repulse: return _board.TryRepulse(_selectedUnitId, _stagedTargetId, cell);
                case ChainReactionAbility.FollowThrough: return _board.TryFollowThrough(_selectedUnitId, _stagedTargetId, cell);
                case ChainReactionAbility.HookYank: return _board.TryHookYank(_selectedUnitId, _stagedTargetId, cell);
                case ChainReactionAbility.Timber: return _board.TryTimber(_selectedUnitId, cell);
                default:
                    _uiMessage = "No claimed reaction is waiting for aim.";
                    return false;
            }
        }

        private bool TryMouseToGrid(Vector2 guiMouse, out GridPos cell)
        {
            cell = new GridPos(0, 0);
            if (_camera == null) return false;
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

        private void DrawWorldLabels()
        {
            if (_camera == null || _board == null) return;
            for (int i = 0; i < _board.Units.Count; i++)
            {
                ChainUnitState unit = _board.Units[i];
                if (!unit.IsAlive) continue;
                string owner = unit.Team == CombatTeam.Enemy ? "E" : $"P{unit.CommandGroup}";
                string state = unit.Airborne ? " AIRBORNE" : string.Empty;
                DrawWorldLabel(World(unit.Position, unit.Airborne ? 3.6f : 2.0f), $"{owner} {unit.Name}{state}\n{unit.Hp}/{unit.MaxHp}");
            }
            for (int i = 0; i < _board.Trees.Count; i++)
            {
                ChainTreeState tree = _board.Trees[i];
                if (tree.Standing) DrawWorldLabel(World(tree.Position, 3f), "TREE");
            }
            if (_board.PortalA.HasValue) DrawWorldLabel(World(_board.PortalA.Value, 0.45f), "PORTAL A");
            if (_board.PortalB.HasValue) DrawWorldLabel(World(_board.PortalB.Value, 0.45f), "PORTAL B");
            foreach (GridPos amplifier in _board.Amplifiers) DrawWorldLabel(World(amplifier, 0.45f), "×2 FORCE");
        }

        private void DrawWorldLabel(Vector3 world, string text)
        {
            Vector3 screen = _camera.WorldToScreenPoint(world);
            if (screen.z <= 0f) return;
            float x = screen.x - 68f;
            float y = Screen.height - screen.y - 20f;
            if (x > SidebarRect.x - 136f) return;
            GUI.Label(new Rect(x, y, 136f, 45f), text, _smallStyle);
        }

        private void BeginCommand(CommandMode mode, string message)
        {
            ChainUnitState unit = _board.GetUnit(_selectedUnitId);
            if (unit != null && !_roundReadiness.CanUseProactive(unit.CommandGroup) && mode != CommandMode.ReactionPick && mode != CommandMode.ReactionAim)
            {
                _uiMessage = $"P{unit.CommandGroup} is Ready. Unready that player before starting another proactive action.";
                return;
            }

            _command = mode;
            _uiMessage = message;
            _stagedTargetId = 0;
            _activeReactionAbility = ChainReactionAbility.None;
        }

        private void CancelCommand(bool updateMessage = true)
        {
            _command = CommandMode.None;
            _stagedTargetId = 0;
            _activeReactionAbility = ChainReactionAbility.None;
            _hasPortalEntrance = false;
            if (updateMessage) _uiMessage = "Aim cancelled. Player readiness and event reservations remain unchanged.";
        }

        private void SelectFirstFriendly()
        {
            _selectedUnitId = 0;
            for (int i = 0; i < _board.Units.Count; i++)
            {
                ChainUnitState unit = _board.Units[i];
                if (unit.Team == CombatTeam.Friendly && unit.IsAlive)
                {
                    _selectedUnitId = unit.Id;
                    return;
                }
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
                case ChainRecruitKind.Stephen: return "Follow Through — after a collision within 4 cells, kick either participant in a chosen direction (5 force).";
                case ChainRecruitKind.Brutus: return "Catch & Throw — when a creature is airborne within 3 cells, catch it beside Brutus and rethrow it (7 force).";
                case ChainRecruitKind.Weldon: return "Crosswind — when an airborne creature is within 6 cells, redirect its current momentum without replacing it.";
                case ChainRecruitKind.Madeline: return "Repulse — after a collision within 5 cells, choose either participant and blast it away (4 force).";
                case ChainRecruitKind.Grom: return "Timber — after a tree impact within 5 cells, choose the tree's fall direction.";
                case ChainRecruitKind.Skitter: return "Hook Yank — after a collision or tree impact within 6 cells, pull an involved creature toward Skitter (5 force).";
                default: return "No special reaction in this lab.";
            }
        }

        private static string RosterSummary(ChainRecruitKind kind)
        {
            switch (kind)
            {
                case ChainRecruitKind.Stephen: return "Uppercut | collision kick";
                case ChainRecruitKind.Brutus: return "directed hurl | catch airborne";
                case ChainRecruitKind.Weldon: return "gust | redirect airborne";
                case ChainRecruitKind.Madeline: return "converge | collision repulse";
                case ChainRecruitKind.Mira: return "portals | force multiplier";
                case ChainRecruitKind.Grom: return "notch tree | fell struck tree";
                case ChainRecruitKind.Skitter: return "harpoon | hook collision/tree victims";
                default: return kind.ToString();
            }
        }

        private static string CommandLabel(CommandMode mode)
        {
            switch (mode)
            {
                case CommandMode.Move: return "MOVE";
                case CommandMode.Strike: return "STRIKE TARGET";
                case CommandMode.Uppercut: return "UPPERCUT TARGET";
                case CommandMode.Gust: return "GUST TARGET";
                case CommandMode.ShoulderPick: return "SHOULDER HURL TARGET";
                case CommandMode.ShoulderAim: return "SHOULDER HURL DIRECTION";
                case CommandMode.PortalEntrance: return "PORTAL ENTRANCE";
                case CommandMode.PortalExit: return "PORTAL EXIT";
                case CommandMode.Amplifier: return "FORCE MULTIPLIER";
                case CommandMode.ReactionPick: return "REACTION PARTICIPANT";
                case CommandMode.ReactionAim: return "RESERVED REACTION AIM";
                default: return "NONE";
            }
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = true };
            _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, wordWrap = true };
            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true };
        }

        private static Vector3 World(GridPos p, float y)
        {
            return new Vector3(p.X, y, p.Z);
        }

        private static Vector3 BaseScale(ChainUnitState unit)
        {
            if (unit.Kind == ChainRecruitKind.Ogre) return new Vector3(1f, 1.2f, 1f);
            if (unit.Kind == ChainRecruitKind.Goblin) return new Vector3(0.62f, 0.72f, 0.62f);
            if (unit.Kind == ChainRecruitKind.Brutus) return new Vector3(0.9f, 1.0f, 0.9f);
            return new Vector3(0.72f, 0.86f, 0.72f);
        }

        private static Color UnitColor(ChainUnitState unit)
        {
            if (unit.Team == CombatTeam.Enemy)
                return unit.Kind == ChainRecruitKind.Ogre ? new Color(0.68f, 0.18f, 0.13f) : new Color(0.86f, 0.32f, 0.25f);

            switch (unit.CommandGroup)
            {
                case 1: return new Color(0.18f, 0.52f, 0.95f);
                case 2: return new Color(0.25f, 0.75f, 0.95f);
                case 3: return new Color(0.62f, 0.34f, 0.95f);
                default: return new Color(0.15f, 0.78f, 0.52f);
            }
        }

        private static void SetColor(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = color;
        }

        private static void RemoveCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }
    }
}
