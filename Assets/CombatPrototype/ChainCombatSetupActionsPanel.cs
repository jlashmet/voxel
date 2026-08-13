using System.Reflection;
using UnityEngine;

namespace MountingForce.CombatPrototype
{
    /// <summary>
    /// Experimental proactive setup verbs for the recruits that previously spent too much time waiting for a reaction.
    /// This is deliberately a separate prototype panel so we can change the verbs quickly without bloating the main UI.
    /// It exposes only the selected recruit's own capability, never a computed combo path.
    /// </summary>
    [RequireComponent(typeof(ChainCombatLabController))]
    public sealed class ChainCombatSetupActionsPanel : MonoBehaviour
    {
        private enum AimMode
        {
            None,
            ConvergeFirst,
            ConvergeSecond,
            Harpoon,
            NotchTree,
            NotchDirection
        }

        private static readonly FieldInfo BoardField = typeof(ChainCombatLabController).GetField(
            "_board", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SelectedField = typeof(ChainCombatLabController).GetField(
            "_selectedUnitId", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ReadinessField = typeof(ChainCombatLabController).GetField(
            "_roundReadiness", BindingFlags.Instance | BindingFlags.NonPublic);

        private const float MainSidebarWidth = 420f;

        private ChainCombatLabController _controller;
        private ChainCombatBoard _board;
        private ChainRoundReadinessCoordinator _roundReadiness;
        private Camera _camera;
        private AimMode _aim;
        private int _firstTargetId;
        private int _treeId;
        private string _message;
        private GUIStyle _header;
        private GUIStyle _small;

        private void Awake()
        {
            _controller = GetComponent<ChainCombatLabController>();
            _message = "Setup verbs are actions, not reactions. Use them to create the geometry another player can exploit.";
        }

        private void Update()
        {
            if (_board == null && _controller != null && BoardField != null)
                _board = BoardField.GetValue(_controller) as ChainCombatBoard;
            if (_roundReadiness == null && _controller != null && ReadinessField != null)
                _roundReadiness = ReadinessField.GetValue(_controller) as ChainRoundReadinessCoordinator;

            if (_camera == null)
            {
                GameObject cameraObject = GameObject.Find("Chain Combat Lab Camera");
                if (cameraObject != null) _camera = cameraObject.GetComponent<Camera>();
            }
        }

        private void OnGUI()
        {
            if (_board == null || SelectedField == null) return;
            EnsureStyles();

            int selectedId = (int)SelectedField.GetValue(_controller);
            ChainUnitState selected = _board.GetUnit(selectedId);
            if (selected == null || selected.Team != CombatTeam.Friendly || !selected.IsAlive) return;

            Rect panel = new Rect(10f, 10f, 310f, 0f);
            GUILayout.BeginArea(new Rect(panel.x, panel.y, panel.width, 185f), GUI.skin.box);
            GUILayout.Label("PROACTIVE SETUP", _header);
            GUILayout.Label($"P{selected.CommandGroup} {selected.Name}", _small);

            bool hasSpecial = DrawSpecialAction(selected);
            if (!hasSpecial)
                GUILayout.Label("This recruit's proactive specialty is already in the main sidebar.", _small);

            if (_roundReadiness != null && _roundReadiness.IsReady(selected.CommandGroup))
                GUILayout.Label($"P{selected.CommandGroup} is READY. Setup actions are closed; reactions remain available in the main sidebar.", _small);

            if (_aim != AimMode.None)
            {
                GUILayout.Label($"AIMING: {AimName(_aim)}", _header);
                if (GUILayout.Button("Cancel setup aim")) ResetAim();
            }

            GUILayout.Label(_message, _small);
            GUILayout.EndArea();

            HandleBoardClick(Event.current, selected);
            DrawNotchLabels();
        }

        private bool DrawSpecialAction(ChainUnitState unit)
        {
            bool proactiveOpen = _roundReadiness == null || _roundReadiness.CanUseProactive(unit.CommandGroup);
            GUI.enabled = proactiveOpen && _board.PendingReaction == null && !unit.ActionSpent && !_board.BattleOver;
            bool shown = true;

            switch (unit.Kind)
            {
                case ChainRecruitKind.Madeline:
                    if (GUILayout.Button("Converge two enemies"))
                    {
                        _aim = AimMode.ConvergeFirst;
                        _firstTargetId = 0;
                        _message = "Choose the enemy Madeline should move, then choose the enemy she should drive it toward.";
                    }
                    GUILayout.Label("Drive one enemy toward another with force 4. Alignment matters; a bad angle can miss.", _small);
                    break;

                case ChainRecruitKind.Skitter:
                    if (GUILayout.Button("Harpoon"))
                    {
                        _aim = AimMode.Harpoon;
                        _message = "Choose an enemy within 6 cells. Skitter pulls it toward himself with force 4.";
                    }
                    GUILayout.Label("A chain starter as well as a setup tool: pull a target toward Skitter and whatever lies between them.", _small);
                    break;

                case ChainRecruitKind.Grom:
                    if (GUILayout.Button("Notch a tree"))
                    {
                        _aim = AimMode.NotchTree;
                        _treeId = 0;
                        _message = "Choose a standing tree within 5 cells, then choose its prepared fall direction.";
                    }
                    GUILayout.Label("Spend an action preparing a fall direction. If Timber later follows the notch, the tree falls farther and hits harder.", _small);
                    break;

                default:
                    shown = false;
                    break;
            }

            GUI.enabled = true;
            return shown;
        }

        private void HandleBoardClick(Event current, ChainUnitState selected)
        {
            if (_aim == AimMode.None || current == null || current.type != EventType.MouseDown || current.button != 0) return;
            if (current.mousePosition.x >= Screen.width - MainSidebarWidth) return;
            if (new Rect(10f, 10f, 310f, 185f).Contains(current.mousePosition)) return;
            if (!TryMouseToGrid(current.mousePosition, out GridPos cell)) return;

            if (_roundReadiness != null && !_roundReadiness.CanUseProactive(selected.CommandGroup))
            {
                _message = $"P{selected.CommandGroup} is Ready. Unready that player before finishing a proactive setup action.";
                ResetAim(false);
                current.Use();
                return;
            }

            ChainUnitState clickedUnit = _board.FindUnitAt(cell);
            ChainTreeState clickedTree = _board.FindStandingTreeAt(cell);
            bool success = false;

            switch (_aim)
            {
                case AimMode.ConvergeFirst:
                    if (clickedUnit == null || clickedUnit.Team != CombatTeam.Enemy)
                    {
                        _message = "Converge: first click must be a living enemy to move.";
                        current.Use();
                        return;
                    }
                    _firstTargetId = clickedUnit.Id;
                    _aim = AimMode.ConvergeSecond;
                    _message = $"Moving {clickedUnit.Name}. Now choose the second enemy it should be driven toward.";
                    current.Use();
                    return;

                case AimMode.ConvergeSecond:
                    if (clickedUnit == null || clickedUnit.Team != CombatTeam.Enemy)
                    {
                        _message = "Converge: second click must be a different living enemy.";
                        current.Use();
                        return;
                    }
                    success = _board.TryConverge(selected.Id, _firstTargetId, clickedUnit.Id);
                    break;

                case AimMode.Harpoon:
                    if (clickedUnit == null || clickedUnit.Team != CombatTeam.Enemy)
                    {
                        _message = "Harpoon: click a living enemy.";
                        current.Use();
                        return;
                    }
                    success = _board.TryHarpoon(selected.Id, clickedUnit.Id);
                    break;

                case AimMode.NotchTree:
                    if (clickedTree == null)
                    {
                        _message = "Notch: click a standing tree first.";
                        current.Use();
                        return;
                    }
                    _treeId = clickedTree.Id;
                    _aim = AimMode.NotchDirection;
                    _message = $"Tree #{_treeId} selected. Click anywhere in the desired fall direction.";
                    current.Use();
                    return;

                case AimMode.NotchDirection:
                    success = _board.TryNotchTree(selected.Id, _treeId, cell);
                    break;
            }

            _message = _board.LastMessage;
            if (success) ResetAim(false);
            current.Use();
        }

        private void DrawNotchLabels()
        {
            if (_camera == null || _board == null) return;

            for (int i = 0; i < _board.Trees.Count; i++)
            {
                ChainTreeState tree = _board.Trees[i];
                if (!tree.Standing || !tree.IsNotched) continue;

                Vector3 screen = _camera.WorldToScreenPoint(new Vector3(tree.Position.X, 3.45f, tree.Position.Z));
                if (screen.z <= 0f) continue;
                string arrow = DirectionArrow(tree.NotchedDirection);
                GUI.Label(new Rect(screen.x - 55f, Screen.height - screen.y - 18f, 110f, 36f), $"NOTCHED {arrow}\nstress {tree.Stress}", _small);
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

        private void ResetAim(bool clearMessage = true)
        {
            _aim = AimMode.None;
            _firstTargetId = 0;
            _treeId = 0;
            if (clearMessage) _message = "Setup aim cancelled.";
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, wordWrap = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true };
        }

        private static string AimName(AimMode mode)
        {
            switch (mode)
            {
                case AimMode.ConvergeFirst: return "CONVERGE — MOVING TARGET";
                case AimMode.ConvergeSecond: return "CONVERGE — ANCHOR TARGET";
                case AimMode.Harpoon: return "HARPOON TARGET";
                case AimMode.NotchTree: return "TREE TO PREPARE";
                case AimMode.NotchDirection: return "PREPARED FALL DIRECTION";
                default: return "NONE";
            }
        }

        private static string DirectionArrow(GridPos direction)
        {
            if (direction.X > 0) return "→";
            if (direction.X < 0) return "←";
            if (direction.Z > 0) return "↑";
            return "↓";
        }
    }
}
