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
            EnsureStyle();

            var intents = _readiness.EnemyIntents;
            for (int i = 0; i < intents.Count; i++)
            {
                ChainEnemyIntent intent = intents[i];
                ChainUnitState enemy = _board.GetUnit(intent.EnemyId);
                if (enemy == null || !enemy.IsAlive) continue;

                Vector3 world = new Vector3(enemy.Position.X, enemy.Kind == ChainRecruitKind.Ogre ? 4.5f : 3.4f, enemy.Position.Z);
                Vector3 screen = _camera.WorldToScreenPoint(world);
                if (screen.z <= 0f) continue;

                string shortIntent = ShortIntent(intent, enemy);
                float x = screen.x - 90f;
                float y = Screen.height - screen.y - 22f;
                GUI.Box(new Rect(x, y, 180f, 42f), GUIContent.none);
                GUI.Label(new Rect(x + 5f, y + 3f, 170f, 36f), shortIntent, _style);
            }
        }

        private static string ShortIntent(ChainEnemyIntent intent, ChainUnitState enemy)
        {
            switch (intent.Kind)
            {
                case ChainEnemyIntentKind.Attack:
                    return $"{enemy.Name}: ATTACK\n{TargetName(intent)}";
                case ChainEnemyIntentKind.Charge:
                    return $"{enemy.Name}: CHARGE {DirectionName(intent.Direction).ToUpperInvariant()}\nforce 6";
                case ChainEnemyIntentKind.Advance:
                    return $"{enemy.Name}: ADVANCE {DirectionName(intent.Direction).ToUpperInvariant()}";
                default:
                    return $"{enemy.Name}: WAIT";
            }
        }

        private static string TargetName(ChainEnemyIntent intent)
        {
            return intent.TargetUnitId == 0 ? "" : $"target #{intent.TargetUnitId}";
        }

        private void EnsureStyle()
        {
            if (_style != null) return;
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
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
