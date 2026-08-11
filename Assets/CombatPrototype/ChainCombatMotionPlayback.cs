using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MountingForce.CombatPrototype
{
    /// <summary>
    /// Smooths the already-resolved authoritative integer state into readable presentation motion.
    /// It never feeds transforms back into combat state. The combat board remains deterministic and instantaneous.
    /// </summary>
    [RequireComponent(typeof(ChainCombatLabController))]
    public sealed class ChainCombatMotionPlayback : MonoBehaviour
    {
        private static readonly FieldInfo BoardField = typeof(ChainCombatLabController).GetField(
            "_board", BindingFlags.Instance | BindingFlags.NonPublic);

        private sealed class UnitPlayback
        {
            public Vector3 Displayed;
            public Vector3 Target;
            public bool Initialized;
        }

        private sealed class TreePlayback
        {
            public Quaternion Displayed = Quaternion.identity;
            public bool WasStanding = true;
        }

        private readonly Dictionary<int, UnitPlayback> _units = new Dictionary<int, UnitPlayback>();
        private readonly Dictionary<int, TreePlayback> _trees = new Dictionary<int, TreePlayback>();
        private ChainCombatLabController _controller;
        private ChainCombatBoard _board;

        [SerializeField] private float groundCellsPerSecond = 8.5f;
        [SerializeField] private float airborneCellsPerSecond = 6.5f;
        [SerializeField] private float treeDegreesPerSecond = 210f;

        private void Awake()
        {
            _controller = GetComponent<ChainCombatLabController>();
        }

        private void LateUpdate()
        {
            if (_board == null && _controller != null && BoardField != null)
            {
                _board = BoardField.GetValue(_controller) as ChainCombatBoard;
            }
            if (_board == null) return;

            SmoothUnits();
            SmoothTrees();
        }

        private void SmoothUnits()
        {
            for (int i = 0; i < _board.Units.Count; i++)
            {
                ChainUnitState unit = _board.Units[i];
                GameObject visual = GameObject.Find($"Chain Unit - {unit.Name}");
                if (visual == null || !unit.IsAlive) continue;

                if (!_units.TryGetValue(unit.Id, out UnitPlayback playback))
                {
                    playback = new UnitPlayback();
                    _units[unit.Id] = playback;
                }

                float y = unit.Airborne ? 2.35f : 0.62f;
                playback.Target = new Vector3(unit.Position.X, y, unit.Position.Z);
                if (!playback.Initialized)
                {
                    playback.Displayed = playback.Target;
                    playback.Initialized = true;
                }

                float planarDistance = Vector2.Distance(
                    new Vector2(playback.Displayed.x, playback.Displayed.z),
                    new Vector2(playback.Target.x, playback.Target.z));

                // Large jumps are usually portal traversal. A straight-line lerp would visually lie about the route,
                // so snap those while ordinary knockback/throws visibly travel across the board.
                if (planarDistance > 6.25f)
                {
                    playback.Displayed = playback.Target;
                }
                else
                {
                    float speed = unit.Airborne ? airborneCellsPerSecond : groundCellsPerSecond;
                    playback.Displayed = Vector3.MoveTowards(playback.Displayed, playback.Target, speed * Time.unscaledDeltaTime);
                    playback.Displayed.y = Mathf.MoveTowards(playback.Displayed.y, playback.Target.y, 7f * Time.unscaledDeltaTime);
                }

                visual.transform.position = playback.Displayed;
            }
        }

        private void SmoothTrees()
        {
            for (int i = 0; i < _board.Trees.Count; i++)
            {
                ChainTreeState tree = _board.Trees[i];
                GameObject visual = GameObject.Find($"Chain Tree {tree.Id}");
                if (visual == null) continue;

                if (!_trees.TryGetValue(tree.Id, out TreePlayback playback))
                {
                    playback = new TreePlayback();
                    _trees[tree.Id] = playback;
                }

                Quaternion target = Quaternion.identity;
                Vector3 basePosition = new Vector3(tree.Position.X, 0f, tree.Position.Z);
                if (!tree.Standing)
                {
                    Vector3 direction = new Vector3(tree.FallDirection.X, 0f, tree.FallDirection.Z);
                    if (direction.sqrMagnitude > 0.01f)
                    {
                        target = Quaternion.FromToRotation(Vector3.up, direction.normalized);
                        basePosition += direction * 1.55f;
                    }
                }

                if (playback.WasStanding && !tree.Standing)
                {
                    playback.Displayed = Quaternion.identity;
                }
                playback.WasStanding = tree.Standing;
                playback.Displayed = Quaternion.RotateTowards(
                    playback.Displayed,
                    target,
                    treeDegreesPerSecond * Time.unscaledDeltaTime);

                visual.transform.position = basePosition;
                visual.transform.rotation = playback.Displayed;
            }
        }
    }
}
