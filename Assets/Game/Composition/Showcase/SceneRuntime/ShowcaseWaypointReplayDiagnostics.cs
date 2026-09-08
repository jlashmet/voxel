using System;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;

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
        private const float StalledDistance = 0.01f;

        private static readonly FieldInfo IndexField = typeof(ShowcaseWaypointReplayHarness).GetField(
            "_index", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo RouteField = typeof(ShowcaseWaypointReplayHarness).GetField(
            "_route", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MotorField = typeof(ShowcaseWaypointReplayHarness).GetField(
            "_motor", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ShowcaseField = typeof(ShowcaseWaypointReplayHarness).GetField(
            "_showcase", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo WorldField = typeof(VoxelShowcase).GetField(
            "_world", BindingFlags.Instance | BindingFlags.NonPublic);

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
            if (IndexField == null || RouteField == null || MotorField == null || ShowcaseField == null || WorldField == null)
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

            if (_hasLastFeet && _lastIndex == index && moved <= StalledDistance)
                LogCollisionDiscriminator(motor, feet, dx, dz);

            _lastIndex = index;
            _lastFeet = feet;
            _hasLastFeet = true;
        }

        private void LogCollisionDiscriminator(CharacterMotor motor, Vector3 feet, float dx, float dz)
        {
            VoxelShowcase showcase = ShowcaseField.GetValue(_harness) as VoxelShowcase;
            ShowcaseWorld world = showcase == null ? null : WorldField.GetValue(showcase) as ShowcaseWorld;
            if (world == null) return;

            float probeDistance = ShowcaseWorld.VoxelSize * 0.5f;
            Vector3 xProbe = feet + new Vector3(Mathf.Sign(dx) * probeDistance, 0f, 0f);
            Vector3 zProbe = feet + new Vector3(0f, 0f, Mathf.Sign(dz) * probeDistance);
            Vector3 raised = feet + Vector3.up * motor.StepHeight;
            Vector3 raisedXProbe = xProbe + Vector3.up * motor.StepHeight;
            Vector3 raisedZProbe = zProbe + Vector3.up * motor.StepHeight;

            Debug.Log(
                "WAYPOINT_REPLAY blocker discriminator "
                + $"x={Probe(world, motor, xProbe)} z={Probe(world, motor, zProbe)} "
                + $"raised={Probe(world, motor, raised)} "
                + $"raisedX={Probe(world, motor, raisedXProbe)} raisedZ={Probe(world, motor, raisedZProbe)}");
        }

        private static string Probe(ShowcaseWorld world, CharacterMotor motor, Vector3 feet)
        {
            Vector3 min = new(feet.x - motor.Radius, feet.y, feet.z - motor.Radius);
            Vector3 max = new(feet.x + motor.Radius, feet.y + motor.Height, feet.z + motor.Radius);
            bool voxel = OverlapsVoxel(world, min, max);
            bool wood = VegetationComposition.TreeDamage.OverlapsWoodAabb(
                new float3(min.x, min.y, min.z),
                new float3(max.x, max.y, max.z));
            return $"voxel:{voxel}/wood:{wood}";
        }

        private static bool OverlapsVoxel(ShowcaseWorld world, Vector3 min, Vector3 max)
        {
            float voxelSize = ShowcaseWorld.VoxelSize;
            int minX = Mathf.FloorToInt(min.x / voxelSize);
            int minY = Mathf.FloorToInt(min.y / voxelSize);
            int minZ = Mathf.FloorToInt(min.z / voxelSize);
            int maxX = Mathf.FloorToInt((max.x - 1e-4f) / voxelSize);
            int maxY = Mathf.FloorToInt((max.y - 1e-4f) / voxelSize);
            int maxZ = Mathf.FloorToInt((max.z - 1e-4f) / voxelSize);

            IVoxelSurfaceQuery surface = world.SurfaceQuery;
            for (int y = minY; y <= maxY; y++)
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                if (surface.TryRead(new int3(x, y, z), out VoxelCell cell) &&
                    cell.BaseMaterialId != VoxelGrid.MaterialEmpty)
                    return true;
            }

            return false;
        }
    }
}
