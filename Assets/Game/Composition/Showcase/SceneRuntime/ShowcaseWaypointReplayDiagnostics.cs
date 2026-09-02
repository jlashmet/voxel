using System;
using System.Reflection;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Diagnostic-only telemetry for built-player SceneIssue waypoint replay.
    /// The active replay harness attaches this observer after it has bound the production motor/route.
    /// This component observes replay and CharacterMotor state; it never changes movement or world state.
    /// </summary>
    [DefaultExecutionOrder(-8999)]
    internal sealed class ShowcaseWaypointReplayDiagnostics : MonoBehaviour
    {
        private const float SampleSeconds = 1f;

        private static readonly FieldInfo IndexField = typeof(ShowcaseWaypointReplayHarness).GetField(
            "_index", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo RouteField = typeof(ShowcaseWaypointReplayHarness).GetField(
            "_route", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MotorField = typeof(ShowcaseWaypointReplayHarness).GetField(
            "_motor", BindingFlags.Instance | BindingFlags.NonPublic);

        private ShowcaseWaypointReplayHarness _harness;
        private float _sampleElapsed;
        private int _lastIndex = -1;
        private Vector3 _lastFeet;
        private bool _hasLastFeet;

        internal static void AttachTo(GameObject root, ShowcaseWaypointReplayHarness harness)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (harness == null) throw new ArgumentNullException(nameof(harness));

            var diagnostics = root.AddComponent<ShowcaseWaypointReplayDiagnostics>();
            diagnostics._harness = harness;
            Debug.Log("WAYPOINT_REPLAY diagnostic activated");
        }

        private void Update()
        {
            if (_harness == null) return;
            if (IndexField == null || RouteField == null || MotorField == null)
            {
                Debug.LogError("WAYPOINT_REPLAY diagnostic could not bind replay state.");
                enabled = false;
                return;
            }

            _sampleElapsed += Time.unscaledDeltaTime;
            if (_sampleElapsed < SampleSeconds) return;
            _sampleElapsed = 0f;

            int index = (int)IndexField.GetValue(_harness);
            object route = RouteField.GetValue(_harness);
            CharacterMotor motor = MotorField.GetValue(_harness) as CharacterMotor;
            if (route == null || motor == null) return;

            FieldInfo waypointsField = route.GetType().GetField(
                "waypoints", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Array waypoints = waypointsField?.GetValue(route) as Array;
            if (waypoints == null || index < 0 || index >= waypoints.Length) return;

            object waypoint = waypoints.GetValue(index);
            if (waypoint == null) return;
            Type waypointType = waypoint.GetType();
            string name = waypointType.GetField("name")?.GetValue(waypoint) as string ?? "<unnamed>";
            float targetX = Convert.ToSingle(waypointType.GetField("x")?.GetValue(waypoint));
            float targetZ = Convert.ToSingle(waypointType.GetField("z")?.GetValue(waypoint));

            Vector3 feet = motor.Position;
            float dx = targetX - feet.x;
            float dz = targetZ - feet.z;
            float horizontal = Mathf.Sqrt(dx * dx + dz * dz);
            float moved = _hasLastFeet && _lastIndex == index
                ? Vector3.Distance(feet, _lastFeet)
                : 0f;

            Debug.Log(
                $"WAYPOINT_REPLAY diagnostic waypoint={index}/{waypoints.Length} '{name}' "
                + $"feet=({feet.x:0.000},{feet.y:0.000},{feet.z:0.000}) "
                + $"target=({targetX:0.000},{targetZ:0.000}) horizontal={horizontal:0.000} "
                + $"moved1s={moved:0.000} grounded={motor.Grounded}");

            _lastIndex = index;
            _lastFeet = feet;
            _hasLastFeet = true;
        }
    }
}
