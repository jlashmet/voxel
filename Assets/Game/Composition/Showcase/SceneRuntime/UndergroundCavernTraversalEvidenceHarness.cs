using System;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Standalone-player evidence driver for the underground-cavern production route. This does
    /// not own movement: it only supplies the heading and AutoWalk forward input, leaving the
    /// ordinary VoxelShowcase MovePlayer -> CharacterMotor.Step -> streaming path authoritative.
    /// It is inert unless the explicit command-line flag is present.
    /// </summary>
    public static class UndergroundCavernTraversalEvidenceHarness
    {
        private const string ArgumentName = "-voxel-underground-cavern-traversal";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument(ArgumentName)) return;

            var root = new GameObject("Underground Cavern Player Traversal Evidence")
            {
                hideFlags = HideFlags.DontSave
            };
            root.AddComponent<RouteDriver>();
            UnityEngine.Object.DontDestroyOnLoad(root);
            Debug.Log("SCENEISSUE cavern player traversal armed (production AutoWalk/CharacterMotor path)");
        }

        private static bool HasArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    return true;
            return false;
        }

        [DefaultExecutionOrder(-10000)]
        private sealed class RouteDriver : MonoBehaviour
        {
            private static readonly FieldInfo WorldField = typeof(VoxelShowcase).GetField(
                "_world", BindingFlags.Instance | BindingFlags.NonPublic);
            private static readonly FieldInfo YawField = typeof(VoxelShowcase).GetField(
                "_yaw", BindingFlags.Instance | BindingFlags.NonPublic);
            private static readonly FieldInfo PitchField = typeof(VoxelShowcase).GetField(
                "_pitch", BindingFlags.Instance | BindingFlags.NonPublic);

            private VoxelShowcase _showcase;
            private ShowcaseWorld _world;
            private float3[] _route;
            private int _targetIndex;
            private bool _started;
            private bool _complete;
            private float _waitSeconds;

            private void Update()
            {
                if (_complete) return;
                if (!TryResolve())
                {
                    _waitSeconds += Time.unscaledDeltaTime;
                    if (_waitSeconds > 35f)
                    {
                        Debug.LogError("SCENEISSUE cavern player traversal could not resolve the production world/route.");
                        _complete = true;
                    }
                    return;
                }

                if (!_started)
                {
                    StartAtSurfaceMouth();
                    return;
                }

                SteerNormalPlayerTowardNextWaypoint();
            }

            private bool TryResolve()
            {
                if (_showcase == null)
                    _showcase = UnityEngine.Object.FindFirstObjectByType<VoxelShowcase>();
                if (_showcase == null || WorldField == null || YawField == null || PitchField == null)
                    return false;

                if (_world == null)
                    _world = WorldField.GetValue(_showcase) as ShowcaseWorld;
                if (_world == null || !_world.HasUndergroundCavernRuins)
                    return false;

                if (_route == null || _route.Length == 0)
                    _route = _world.UndergroundCavernTraversalWaypointsMetres;
                return _route != null && _route.Length >= 25;
            }

            private void StartAtSurfaceMouth()
            {
                // Initial evidence placement puts the normal player at the authored surface mouth;
                // every metre after this point is covered by production movement/collision.
                _showcase.TeleportTo((Vector3)_route[0]);
                _showcase.AutoSurvey = false;
                _showcase.AutoRecede = false;
                _showcase.AutoWalk = true;
                _targetIndex = 1;
                _started = true;
                AimAt((Vector3)_route[_targetIndex]);
                Debug.Log($"SCENEISSUE cavern player traversal started at surface mouth {_route[0]} " +
                          $"waypoints={_route.Length}");
            }

            private void SteerNormalPlayerTowardNextWaypoint()
            {
                Vector3 eye = _showcase.transform.position;
                Vector3 target = (Vector3)_route[_targetIndex];
                float distance = HorizontalDistance(eye, target);
                while (distance <= 0.85f && _targetIndex < _route.Length - 1)
                {
                    if (_targetIndex == 1 || (_targetIndex % 5) == 0)
                        Debug.Log($"SCENEISSUE cavern traversal reached waypoint {_targetIndex}/{_route.Length - 1} " +
                                  $"at {_showcase.transform.position}");
                    _targetIndex++;
                    target = (Vector3)_route[_targetIndex];
                    distance = HorizontalDistance(eye, target);
                }

                if (_targetIndex == _route.Length - 1 && distance <= 0.85f)
                {
                    _showcase.AutoWalk = false;
                    AimAt(target + Vector3.up * 2f);
                    _complete = true;
                    Debug.Log($"SCENEISSUE cavern player traversal complete at ruin " +
                              $"waypoint {_targetIndex}/{_route.Length - 1} position={_showcase.transform.position}");
                    return;
                }

                AimAt(target);
            }

            private void AimAt(Vector3 target)
            {
                Vector3 delta = target - _showcase.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < 1e-6f) return;

                float desiredYaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                // VoxelShowcase.StepAutoWalk adds 24 degrees/second before MovePlayer. Offset that
                // known synthetic-input turn so the resulting production forward vector aims at
                // the semantic route target for this frame.
                float preTurnYaw = desiredYaw - 24f * Time.deltaTime;
                YawField.SetValue(_showcase, preTurnYaw);
                PitchField.SetValue(_showcase, 0f);
            }

            private static float HorizontalDistance(Vector3 a, Vector3 b)
            {
                float dx = a.x - b.x;
                float dz = a.z - b.z;
                return math.sqrt(dx * dx + dz * dz);
            }
        }
    }
}
