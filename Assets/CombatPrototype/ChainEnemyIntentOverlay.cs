using System.Reflection;
using UnityEngine;

namespace MountingForce.CombatPrototype
{
    /// <summary>
    /// Rudimentary readability layer for the tactical AI. It exposes only what each enemy committed to doing this
    /// round. It never recommends a response or highlights characters capable of exploiting the intent.
    /// </summary>
    [RequireComponent(typeof(ChainCombatLabController))]
    public sealed class ChainEnemyIntentOverlay : MonoBehaviour
    {
        private static readonly FieldInfo BoardField = typeof(ChainCombatLabController).GetField(
            "_board", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ReadinessField = typeof(ChainCombatLabController).GetField(
            "_roundReadiness", BindingFlags.Instance | BindingFlags.NonPublic);

        private ChainCombatLabController _controller;
        private ChainCombatBoard _board;
        private ChainRoundReadinessCoordinator _readiness;
        private Camera _camera;
        private GUIStyle _style;
        private GUIStyle _laneStyle;

        private void Awake()
        {
            _controller = GetComponent<ChainCombatLabController>();
        }

        private void Update()
        {
            if (_board == null && _controller != null && BoardField != null)
                _board = BoardField.GetValue(_controller) as ChainCombatBoard;
            if (_readiness == null && _controller != null && ReadinessField != null)
                _readiness = ReadinessField.GetValue(_controller) as ChainRoundReadinessCoordinator;
            if (_camera == null)
            {
                GameObject cameraObject = GameObject.Find("Chain Combat Lab Camera");
                if (cameraObject != null) _camera = cameraObject.GetComponent<Camera>();
            }
        }

        private void OnGUI()
        {
            if (_board == null || _readiness == null || _camera == null) return;
            EnsureStyles();

            var intents = _readiness.EnemyIntents;
            for (int i = 0; i < intents.Count; i++)
            {
                ChainEnemyIntent intent = intents[i];
                ChainUnitState enemy = _board.GetUnit(intent.EnemyId);
                if (enemy == null || !enemy.IsAlive) continue;

                DrawGeometry(enemy, intent);
                DrawIntentLabel(enemy, intent);
            }
        }

        private void DrawGeometry(ChainUnitState enemy, ChainEnemyIntent intent)
        {
            if (intent.Kind == ChainEnemyIntentKind.Charge)
            {
                DrawMotionLane(enemy.Position, enemy.Id, intent.Direction, 6);
            }
            else if (intent.Kind == ChainEnemyIntentKind.Shove)
            {
                ChainUnitState target = _board.GetUnit(intent.TargetUnitId);
                if (target != null && target.IsAlive)
                    DrawMotionLane(target.Position, target.Id, intent.Direction, 4);
            }
            else if (intent.Kind == ChainEnemyIntentKind.Advance)
            {
                GridPos destination = enemy.Position + intent.Direction;
                if (_board.IsInBounds(destination)) DrawCellMarker(destination, "MOVE");
            }
            else if (intent.Kind == ChainEnemyIntentKind.Attack)
            {
                ChainUnitState target = _board.GetUnit(intent.TargetUnitId);
                if (target != null && target.IsAlive) DrawCellMarker(target.Position, "HIT");
            }
        }

        private void DrawMotionLane(GridPos origin, int movingUnitId, GridPos direction, int distance)
        {
            for (int step = 1; step <= distance; step++)
            {
                GridPos cell = origin + direction * step;
                if (!_board.IsInBounds(cell)) break;
                DrawCellMarker(cell, DirectionGlyph(direction));

                ChainUnitState occupant = _board.FindUnitAt(cell);
                ChainTreeState tree = _board.FindStandingTreeAt(cell);
                if ((occupant != null && occupant.Id != movingUnitId) || tree != null) break;
            }
        }

        private void DrawIntentLabel(ChainUnitState enemy, ChainEnemyIntent intent)
        {
            Vector3 world = new Vector3(enemy.Position.X, enemy.Kind == ChainRecruitKind.Ogre ? 4.5f : 3.4f, enemy.Position.Z);
            Vector3 screen = _camera.WorldToScreenPoint(world);
            if (screen.z <= 0f) return;

            string shortIntent = ShortIntent(intent, enemy);
            float x = screen.x - 90f;
            float y = Screen.height - screen.y - 22f;
            GUI.Box(new Rect(x, y, 180f, 42f), GUIContent.none);
            GUI.Label(new Rect(x + 5f, y + 3f, 170f, 36f), shortIntent, _style);
        }

        private void DrawCellMarker(GridPos cell, string text)
        {
            Vector3 world = new Vector3(cell.X, 0.35f, cell.Z);
            Vector3 screen = _camera.WorldToScreenPoint(world);
            if (screen.z <= 0f) return;
            float x = screen.x - 24f;
            float y = Screen.height - screen.y - 12f;
            GUI.Box(new Rect(x, y, 48f, 24f), GUIContent.none);
            GUI.Label(new Rect(x, y, 48f, 24f), text, _laneStyle);
        }

        private string ShortIntent(ChainEnemyIntent intent, ChainUnitState enemy)
        {
            switch (intent.Kind)
            {
                case ChainEnemyIntentKind.Attack:
                {
                    ChainUnitState target = _board.GetUnit(intent.TargetUnitId);
                    return $"{enemy.Name}: ATTACK\n{(target == null ? "lost target" : target.Name)}";
                }
                case ChainEnemyIntentKind.Shove:
                {
                    ChainUnitState target = _board.GetUnit(intent.TargetUnitId);
                    return $"{enemy.Name}: SHOVE {DirectionName(intent.Direction).ToUpperInvariant()}\n{(target == null ? "lost target" : target.Name)} • force 4";
                }
                case ChainEnemyIntentKind.Charge:
                    return $"{enemy.Name}: CHARGE {DirectionName(intent.Direction).ToUpperInvariant()}\nforce 6";
                case ChainEnemyIntentKind.Advance:
                    return $"{enemy.Name}: ADVANCE {DirectionName(intent.Direction).ToUpperInvariant()}";
                default:
                    return $"{enemy.Name}: WAIT";
            }
        }

        private void EnsureStyles()
        {
            if (_style != null) return;
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _laneStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private static string DirectionGlyph(GridPos direction)
        {
            if (direction.X > 0) return ">>>";
            if (direction.X < 0) return "<<<";
            if (direction.Z > 0) return "^^^";
            if (direction.Z < 0) return "vvv";
            return "!";
        }

        private static string DirectionName(GridPos direction)
        {
            if (direction.X > 0) return "east";
            if (direction.X < 0) return "west";
            if (direction.Z > 0) return "north";
            if (direction.Z < 0) return "south";
            return "none";
        }
    }
}
