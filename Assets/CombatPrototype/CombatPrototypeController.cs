using System.Collections.Generic;
using UnityEngine;

namespace MountingForce.CombatPrototype
{
    /// <summary>
    /// Rudimentary presentation and hot-seat input for the deterministic CombatBoard.
    /// The view intentionally exposes recruit capabilities but never calculates or highlights combo chains.
    /// </summary>
    public sealed class CombatPrototypeController : MonoBehaviour
    {
        private const float SidebarWidth = 360f;

        private enum CommandMode
        {
            None,
            Move,
            Strike,
            Uppercut,
            PortalEntrance,
            PortalExit,
            Amplifier,
            CrosswindAim,
            RepulsePick,
            RepulseAim,
            TimberAim
        }

        private readonly Dictionary<int, GameObject> _unitVisuals = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _treeVisuals = new Dictionary<int, GameObject>();
        private readonly List<GameObject> _constructVisuals = new List<GameObject>();
        private CombatBoard _board;
        private Camera _camera;
        private GameObject _visualRoot;
        private int _selectedUnitId;
        private int _repulseTargetId;
        private GridPos _portalEntrance;
        private bool _hasPortalEntrance;
        private CommandMode _command;
        private string _uiMessage;
        private Vector2 _sidebarScroll;
        private Vector2 _logScroll;
        private GUIStyle _titleStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _selectedStyle;
        private bool _constructsDirty;

        private Rect SidebarRect => new Rect(Screen.width - SidebarWidth, 0f, SidebarWidth, Screen.height);

        private void Awake()
        {
            _board = new CombatBoard();
            _uiMessage = "Click a friendly recruit. Read what they do, then try to build a chain yourself.";
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

            SyncUnitVisuals();
            SyncTreeVisuals();
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
            _visualRoot = new GameObject("Combat Prototype Visuals");
            _visualRoot.transform.SetParent(transform, false);

            BuildCamera();
            BuildLight();
            BuildGround();

            for (int i = 0; i < _board.Units.Count; i++)
            {
                UnitState unit = _board.Units[i];
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = $"Unit - {unit.Name}";
                visual.transform.SetParent(_visualRoot.transform, false);
                RemoveCollider(visual);
                visual.transform.localScale = UnitBaseScale(unit);
                SetColor(visual, UnitColor(unit));
                _unitVisuals[unit.Id] = visual;
            }

            for (int i = 0; i < _board.Trees.Count; i++)
            {
                TreeState tree = _board.Trees[i];
                _treeVisuals[tree.Id] = CreateTreeVisual(tree);
            }
        }

        private void BuildCamera()
        {
            GameObject cameraObject = new GameObject("Combat Prototype Camera");
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 7.2f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;
            _camera.clearFlags = CameraClearFlags.Skybox;

            float centerX = (CombatBoard.Width - 1) * 0.5f;
            float centerZ = (CombatBoard.Depth - 1) * 0.5f;
            _camera.transform.position = new Vector3(centerX, 14f, centerZ - 12f);
            _camera.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
        }

        private void BuildLight()
        {
            GameObject lightObject = new GameObject("Combat Prototype Light");
            lightObject.transform.SetParent(transform, false);
            Light lightComponent = lightObject.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 1.15f;
            lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        }

        private void BuildGround()
        {
            for (int x = 0; x < CombatBoard.Width; x++)
            {
                for (int z = 0; z < CombatBoard.Depth; z++)
                {
                    GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cell.name = $"Cell {x},{z}";
                    cell.transform.SetParent(_visualRoot.transform, false);
                    cell.transform.position = new Vector3(x, -0.08f, z);
                    cell.transform.localScale = new Vector3(0.96f, 0.12f, 0.96f);
                    RemoveCollider(cell);
                    float shade = (x + z) % 2 == 0 ? 0.28f : 0.32f;
                    SetColor(cell, new Color(shade, shade, shade, 1f));
                }
            }
        }

        private GameObject CreateTreeVisual(TreeState tree)
        {
            GameObject root = new GameObject($"Tree {tree.Id}");
            root.transform.SetParent(_visualRoot.transform, false);

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            trunk.transform.localScale = new Vector3(0.34f, 0.9f, 0.34f);
            RemoveCollider(trunk);
            SetColor(trunk, new Color(0.36f, 0.18f, 0.07f));

            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Crown";
            crown.transform.SetParent(root.transform, false);
            crown.transform.localPosition = new Vector3(0f, 2.15f, 0f);
            crown.transform.localScale = new Vector3(1.35f, 1.25f, 1.35f);
            RemoveCollider(crown);
            SetColor(crown, new Color(0.16f, 0.52f, 0.18f));

            root.transform.position = GridWorld(tree.Position, 0f);
            return root;
        }

        private void SyncUnitVisuals()
        {
            if (_board == null)
            {
                return;
            }

            for (int i = 0; i < _board.Units.Count; i++)
            {
                UnitState unit = _board.Units[i];
                if (!_unitVisuals.TryGetValue(unit.Id, out GameObject visual) || visual == null)
                {
                    continue;
                }

                visual.SetActive(unit.IsAlive);
                if (!unit.IsAlive)
                {
                    continue;
                }

                float y = unit.Airborne ? 2.25f : 0.62f;
                visual.transform.position = GridWorld(unit.Position, y);
                Vector3 baseScale = UnitBaseScale(unit);
                visual.transform.localScale = unit.Id == _selectedUnitId ? baseScale * 1.18f : baseScale;
            }
        }

        private void SyncTreeVisuals()
        {
            if (_board == null)
            {
                return;
            }

            for (int i = 0; i < _board.Trees.Count; i++)
            {
                TreeState tree = _board.Trees[i];
                if (!_treeVisuals.TryGetValue(tree.Id, out GameObject visual) || visual == null)
                {
                    continue;
                }

                if (tree.Standing)
                {
                    visual.transform.position = GridWorld(tree.Position, 0f);
                    visual.transform.rotation = Quaternion.identity;
                }
                else
                {
                    Vector3 direction = new Vector3(tree.FallDirection.X, 0f, tree.FallDirection.Z);
                    visual.transform.position = GridWorld(tree.Position, 0f) + direction * 1.55f;
                    if (direction.sqrMagnitude > 0.01f)
                    {
                        visual.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
                    }
                }
            }
        }

        private void RebuildConstructVisuals()
        {
            for (int i = 0; i < _constructVisuals.Count; i++)
            {
                if (_constructVisuals[i] != null)
                {
                    Destroy(_constructVisuals[i]);
                }
            }
            _constructVisuals.Clear();

            if (_board.PortalA.HasValue)
            {
                _constructVisuals.Add(CreatePad("Portal A", _board.PortalA.Value, new Color(0.28f, 0.55f, 1f), PrimitiveType.Cylinder));
            }

            if (_board.PortalB.HasValue)
            {
                _constructVisuals.Add(CreatePad("Portal B", _board.PortalB.Value, new Color(0.75f, 0.28f, 1f), PrimitiveType.Cylinder));
            }

            foreach (GridPos amplifier in _board.Amplifiers)
            {
                _constructVisuals.Add(CreatePad("Force x2", amplifier, new Color(1f, 0.75f, 0.12f), PrimitiveType.Cube));
            }
        }

        private GameObject CreatePad(string name, GridPos position, Color color, PrimitiveType type)
        {
            GameObject pad = GameObject.CreatePrimitive(type);
            pad.name = name;
            pad.transform.SetParent(_visualRoot.transform, false);
            pad.transform.position = GridWorld(position, 0.08f);
            pad.transform.localScale = type == PrimitiveType.Cylinder
                ? new Vector3(0.82f, 0.06f, 0.82f)
                : new Vector3(0.75f, 0.10f, 0.75f);
            RemoveCollider(pad);
            SetColor(pad, color);
            return pad;
        }

        private void DrawSidebar()
        {
            Rect panel = SidebarRect;
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 10f, 8f, panel.width - 20f, panel.height - 16f));
            _sidebarScroll = GUILayout.BeginScrollView(_sidebarScroll);

            GUILayout.Label("MOUNTING FORCE — CHAIN COMBAT LAB", _titleStyle);
            GUILayout.Label($"Round {_board.Round}   •   free-order co-op actions", _smallStyle);
            GUILayout.Space(4f);

            if (_board.BattleOver)
            {
                GUILayout.Label(_board.BattleResult, _selectedStyle);
            }
            else if (_board.PendingReaction != null)
            {
                GUILayout.Label("PHYSICS PAUSED", _selectedStyle);
                GUILayout.Label(_board.PendingReaction.Description);
                GUILayout.Label("The game is not suggesting who should react. Select a recruit if you think their capability applies.", _smallStyle);
                if (GUILayout.Button("Pass / let the event continue"))
                {
                    if (_board.PassReaction())
                    {
                        _command = CommandMode.None;
                        _uiMessage = _board.LastMessage;
                    }
                    else
                    {
                        _uiMessage = _board.LastMessage;
                    }
                }
                GUILayout.Space(6f);
            }

            GUILayout.Label("HOW TO PLAY", _selectedStyle);
            GUILayout.Label("Click a friendly piece → choose an action → click the board. Movement and reaction aiming are manual. P1/P2 labels are command groups; this build is local hot-seat so one mouse can impersonate both players.", _smallStyle);
            GUILayout.Space(8f);

            UnitState selected = _board.GetUnit(_selectedUnitId);
            if (selected == null || !selected.IsAlive)
            {
                SelectFirstFriendly();
                selected = _board.GetUnit(_selectedUnitId);
            }

            if (selected != null)
            {
                DrawSelectedUnit(selected);
            }

            if (_command != CommandMode.None)
            {
                GUILayout.Space(4f);
                GUILayout.Label($"AIMING: {CommandLabel(_command)}", _selectedStyle);
                if (GUILayout.Button("Cancel aim"))
                {
                    CancelCommand();
                }
            }

            GUILayout.Space(8f);
            GUILayout.Label("STATUS", _selectedStyle);
            GUILayout.Label(string.IsNullOrEmpty(_uiMessage) ? _board.LastMessage : _uiMessage, _smallStyle);

            GUILayout.Space(8f);
            GUI.enabled = !_board.BattleOver;
            if (GUILayout.Button("End round → enemies act"))
            {
                if (_board.EndRound())
                {
                    _uiMessage = _board.LastMessage;
                    CancelCommand();
                }
                else
                {
                    _uiMessage = _board.LastMessage;
                }
            }
            GUI.enabled = true;

            if (GUILayout.Button("Reset battle"))
            {
                _board.Reset();
                _uiMessage = "Battle reset. Try a different setup.";
                CancelCommand();
                SelectFirstFriendly();
                _constructsDirty = true;
            }

            GUILayout.Space(10f);
            GUILayout.Label("RECENT EVENTS", _selectedStyle);
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(190f));
            int first = Mathf.Max(0, _board.Log.Count - 10);
            for (int i = first; i < _board.Log.Count; i++)
            {
                GUILayout.Label("• " + _board.Log[i], _smallStyle);
            }
            GUILayout.EndScrollView();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSelectedUnit(UnitState unit)
        {
            string owner = unit.Team == CombatTeam.Enemy ? "ENEMY" : $"P{unit.CommandGroup} COMMAND";
            GUILayout.Label($"SELECTED — {unit.Name}  [{owner}]", _selectedStyle);
            GUILayout.Label($"HP {unit.Hp}/{unit.MaxHp}");

            if (unit.Team == CombatTeam.Enemy)
            {
                GUILayout.Label("Enemies use a simple deterministic move/attack step when you end the round.", _smallStyle);
                return;
            }

            GUILayout.Label($"Normal action: {(unit.ActionSpent ? "spent" : "ready")}   •   Reaction: {(unit.ReactionSpent ? "spent" : "ready")}", _smallStyle);
            GUILayout.Space(4f);

            if (GUILayout.Button("Move"))
            {
                BeginCommand(CommandMode.Move, "Move: click an empty cell up to 3 cells away.");
            }
            GUILayout.Label("Move up to 3 cells. Positioning is part of the combo setup.", _smallStyle);

            if (GUILayout.Button("Strike"))
            {
                BeginCommand(CommandMode.Strike, "Strike: click an adjacent enemy.");
            }
            GUILayout.Label("Deal 1 damage to an adjacent enemy.", _smallStyle);
            GUILayout.Space(5f);

            switch (unit.Kind)
            {
                case RecruitKind.Stephen:
                    if (GUILayout.Button("Uppercut"))
                    {
                        BeginCommand(CommandMode.Uppercut, "Uppercut: click an adjacent enemy.");
                    }
                    GUILayout.Label("Launch an adjacent enemy away from Stephen with strong momentum.", _smallStyle);
                    break;

                case RecruitKind.Mira:
                    if (GUILayout.Button("Place linked portals"))
                    {
                        _command = CommandMode.PortalEntrance;
                        _hasPortalEntrance = false;
                        _uiMessage = "Portal: click the entrance, then the exit. Moving bodies preserve direction and force.";
                    }
                    GUILayout.Label("Place two linked pads. A moving creature entering either pad exits the other without losing direction or force.", _smallStyle);

                    if (GUILayout.Button("Place force multiplier"))
                    {
                        BeginCommand(CommandMode.Amplifier, "Multiplier: click an empty cell in Mira's placement range.");
                    }
                    GUILayout.Label("Anything driven through the rune doubles its remaining momentum, up to the prototype cap.", _smallStyle);
                    break;

                case RecruitKind.Weldon:
                    if (GUILayout.Button("Crosswind"))
                    {
                        BeginCommand(CommandMode.CrosswindAim, "Crosswind: aim a cardinal direction from the airborne creature.");
                    }
                    GUILayout.Label("Reaction: when an airborne creature is within 6 cells, redirect its flight without replacing its momentum.", _smallStyle);
                    break;

                case RecruitKind.Madeline:
                    if (GUILayout.Button("Repulse"))
                    {
                        BeginCommand(CommandMode.RepulsePick, "Repulse: click one creature from the collision, then aim its blast direction.");
                    }
                    GUILayout.Label("Reaction: when two creatures collide within 5 cells, choose one and blast it away with fresh momentum.", _smallStyle);
                    break;

                case RecruitKind.Grom:
                    if (GUILayout.Button("Timber"))
                    {
                        BeginCommand(CommandMode.TimberAim, "Timber: aim away from the struck tree to choose where it falls.");
                    }
                    GUILayout.Label("Reaction: when a tree is hit within 5 cells, finish the cut and choose a four-cell fall direction.", _smallStyle);
                    break;
            }
        }

        private void DrawWorldLabels()
        {
            if (_camera == null || _board == null)
            {
                return;
            }

            for (int i = 0; i < _board.Units.Count; i++)
            {
                UnitState unit = _board.Units[i];
                if (!unit.IsAlive)
                {
                    continue;
                }

                float y = unit.Airborne ? 3.4f : 1.9f;
                string owner = unit.Team == CombatTeam.Enemy ? "E" : $"P{unit.CommandGroup}";
                DrawWorldLabel(GridWorld(unit.Position, y), $"{owner} {unit.Name}\n{unit.Hp}/{unit.MaxHp}");
            }

            for (int i = 0; i < _board.Trees.Count; i++)
            {
                TreeState tree = _board.Trees[i];
                if (tree.Standing)
                {
                    DrawWorldLabel(GridWorld(tree.Position, 3.0f), "TREE");
                }
            }

            if (_board.PortalA.HasValue)
            {
                DrawWorldLabel(GridWorld(_board.PortalA.Value, 0.45f), "PORTAL A");
            }
            if (_board.PortalB.HasValue)
            {
                DrawWorldLabel(GridWorld(_board.PortalB.Value, 0.45f), "PORTAL B");
            }
            foreach (GridPos amplifier in _board.Amplifiers)
            {
                DrawWorldLabel(GridWorld(amplifier, 0.45f), "×2 FORCE");
            }
        }

        private void DrawWorldLabel(Vector3 world, string text)
        {
            Vector3 screen = _camera.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                return;
            }

            float x = screen.x - 60f;
            float y = Screen.height - screen.y - 20f;
            if (x > SidebarRect.x - 120f)
            {
                return;
            }

            GUI.Label(new Rect(x, y, 120f, 42f), text, _smallStyle);
        }

        private void HandleBoardMouse(Event current)
        {
            if (current == null || current.type != EventType.MouseDown || current.button != 0)
            {
                return;
            }

            if (SidebarRect.Contains(current.mousePosition))
            {
                return;
            }

            if (!TryGuiMouseToGrid(current.mousePosition, out GridPos cell))
            {
                return;
            }

            UnitState clickedUnit = _board.FindUnitAt(cell);
            bool success = false;

            switch (_command)
            {
                case CommandMode.None:
                    if (clickedUnit != null && clickedUnit.Team == CombatTeam.Friendly)
                    {
                        _selectedUnitId = clickedUnit.Id;
                        _uiMessage = $"Selected {clickedUnit.Name}.";
                    }
                    break;

                case CommandMode.Move:
                    success = _board.TryMove(_selectedUnitId, cell);
                    break;

                case CommandMode.Strike:
                    success = clickedUnit != null && _board.TryBasicHit(_selectedUnitId, clickedUnit.Id);
                    if (clickedUnit == null)
                    {
                        _uiMessage = "Strike needs an adjacent enemy. Click the enemy piece.";
                    }
                    break;

                case CommandMode.Uppercut:
                    success = clickedUnit != null && _board.TryUppercut(_selectedUnitId, clickedUnit.Id);
                    if (clickedUnit == null)
                    {
                        _uiMessage = "Uppercut needs an adjacent enemy. Click the enemy piece.";
                    }
                    break;

                case CommandMode.PortalEntrance:
                    _portalEntrance = cell;
                    _hasPortalEntrance = true;
                    _command = CommandMode.PortalExit;
                    _uiMessage = $"Portal entrance marked at {cell}. Now click the exit.";
                    current.Use();
                    return;

                case CommandMode.PortalExit:
                    if (_hasPortalEntrance)
                    {
                        success = _board.TryPlacePortalPair(_selectedUnitId, _portalEntrance, cell);
                        if (success)
                        {
                            _constructsDirty = true;
                        }
                    }
                    break;

                case CommandMode.Amplifier:
                    success = _board.TryPlaceAmplifier(_selectedUnitId, cell);
                    if (success)
                    {
                        _constructsDirty = true;
                    }
                    break;

                case CommandMode.CrosswindAim:
                    success = _board.TryCrosswind(_selectedUnitId, cell);
                    break;

                case CommandMode.RepulsePick:
                    if (clickedUnit == null)
                    {
                        _uiMessage = "Repulse first needs one of the two creatures from the collision.";
                        current.Use();
                        return;
                    }
                    _repulseTargetId = clickedUnit.Id;
                    _command = CommandMode.RepulseAim;
                    _uiMessage = $"Repulse target: {clickedUnit.Name}. Now click a direction away from it.";
                    current.Use();
                    return;

                case CommandMode.RepulseAim:
                    success = _board.TryRepulse(_selectedUnitId, _repulseTargetId, cell);
                    break;

                case CommandMode.TimberAim:
                    success = _board.TryTimber(_selectedUnitId, cell);
                    if (success)
                    {
                        _constructsDirty = true;
                    }
                    break;
            }

            if (_command != CommandMode.None)
            {
                _uiMessage = _board.LastMessage;
                if (success)
                {
                    CancelCommand(false);
                }
            }

            current.Use();
        }

        private bool TryGuiMouseToGrid(Vector2 guiMouse, out GridPos cell)
        {
            cell = new GridPos(0, 0);
            if (_camera == null)
            {
                return false;
            }

            Vector3 screenPoint = new Vector3(guiMouse.x, Screen.height - guiMouse.y, 0f);
            Ray ray = _camera.ScreenPointToRay(screenPoint);
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float enter))
            {
                return false;
            }

            Vector3 world = ray.GetPoint(enter);
            GridPos candidate = new GridPos(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
            if (!_board.IsInBounds(candidate))
            {
                return false;
            }

            cell = candidate;
            return true;
        }

        private void BeginCommand(CommandMode command, string message)
        {
            _command = command;
            _uiMessage = message;
        }

        private void CancelCommand(bool updateMessage = true)
        {
            _command = CommandMode.None;
            _repulseTargetId = 0;
            _hasPortalEntrance = false;
            if (updateMessage)
            {
                _uiMessage = "Aim cancelled.";
            }
        }

        private void SelectFirstFriendly()
        {
            _selectedUnitId = 0;
            for (int i = 0; i < _board.Units.Count; i++)
            {
                UnitState unit = _board.Units[i];
                if (unit.Team == CombatTeam.Friendly && unit.IsAlive)
                {
                    _selectedUnitId = unit.Id;
                    return;
                }
            }
        }

        private static string CommandLabel(CommandMode command)
        {
            switch (command)
            {
                case CommandMode.Move: return "MOVE DESTINATION";
                case CommandMode.Strike: return "STRIKE TARGET";
                case CommandMode.Uppercut: return "UPPERCUT TARGET";
                case CommandMode.PortalEntrance: return "PORTAL ENTRANCE";
                case CommandMode.PortalExit: return "PORTAL EXIT";
                case CommandMode.Amplifier: return "FORCE MULTIPLIER";
                case CommandMode.CrosswindAim: return "CROSSWIND DIRECTION";
                case CommandMode.RepulsePick: return "COLLISION PARTICIPANT";
                case CommandMode.RepulseAim: return "REPULSE DIRECTION";
                case CommandMode.TimberAim: return "TREE FALL DIRECTION";
                default: return "NONE";
            }
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _selectedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                wordWrap = true
            };
        }

        private static Vector3 GridWorld(GridPos position, float y)
        {
            return new Vector3(position.X, y, position.Z);
        }

        private static Vector3 UnitBaseScale(UnitState unit)
        {
            if (unit.Kind == RecruitKind.Ogre)
            {
                return new Vector3(1.0f, 1.2f, 1.0f);
            }

            if (unit.Kind == RecruitKind.Goblin)
            {
                return new Vector3(0.62f, 0.72f, 0.62f);
            }

            return new Vector3(0.72f, 0.86f, 0.72f);
        }

        private static Color UnitColor(UnitState unit)
        {
            if (unit.Team == CombatTeam.Enemy)
            {
                return unit.Kind == RecruitKind.Ogre
                    ? new Color(0.68f, 0.18f, 0.13f)
                    : new Color(0.86f, 0.32f, 0.25f);
            }

            return unit.CommandGroup == 1
                ? new Color(0.18f, 0.52f, 0.95f)
                : new Color(0.15f, 0.78f, 0.52f);
        }

        private static void SetColor(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }

        private static void RemoveCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }
    }
}
