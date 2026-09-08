using System.Collections;
using System.Reflection;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Voxel;
using UnityEngine;

namespace VoxelEngine.Showcase.Validation
{
    /// <summary>
    /// Built-player proof for the SceneRuntime movement path. It supplies deterministic input to
    /// the production VoxelShowcase and then observes the real CharacterMotor; collision, gravity,
    /// streaming and voxel authority are never substituted by validation geometry or colliders.
    /// </summary>
    [DefaultExecutionOrder(-8000)]
    public sealed class CharacterMotorProductionValidationProbe : MonoBehaviour
    {
        private const uint Seed = 0x5EED1234u;
        private const float ExistingAutoWalkDegreesPerSecond = 24f;
        private const string ReadyPrefix = "CHARACTER_MOTOR_MODULE_VALIDATION ready:";
        private const string FailurePrefix = "CHARACTER_MOTOR_MODULE_VALIDATION failure:";

        private static readonly FieldInfo YawField = typeof(VoxelShowcase).GetField(
            "_yaw", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo PitchField = typeof(VoxelShowcase).GetField(
            "_pitch", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MouseLookField = typeof(VoxelShowcase).GetField(
            "_mouseLook", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MotorField = typeof(VoxelShowcase).GetField(
            "_motor", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo WorldField = typeof(VoxelShowcase).GetField(
            "_world", BindingFlags.Instance | BindingFlags.NonPublic);

        private IEnumerator Start()
        {
            VoxelShowcase showcase = GetComponent<VoxelShowcase>();
            if (showcase == null || YawField == null || PitchField == null || MouseLookField == null
                || MotorField == null || WorldField == null)
            {
                Fail("scene could not bind the production VoxelShowcase movement state");
                yield break;
            }

            CharacterMotor motor = MotorField.GetValue(showcase) as CharacterMotor;
            ShowcaseWorld world = WorldField.GetValue(showcase) as ShowcaseWorld;
            if (motor == null || world == null)
            {
                Fail("production world or CharacterMotor was not initialized");
                yield break;
            }

            MountainLandformSurface surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);
            WorldRoadNetwork network = ShowcaseMountainDragonLayout.CreateAscentNetwork(Seed, surface);
            if (!network.TryGetRoute(
                    ShowcaseMountainDragonLayout.AscentRouteId,
                    out WorldRoadNetworkRoute route)
                || !route.Road.IsResolved || route.Road.Points.Count < 3)
            {
                Fail("production Mountain Dragon route was unavailable");
                yield break;
            }

            Vector3 entry = new Vector3(
                ShowcaseMountainDragonLayout.EntryXdm * ShowcaseWorld.VoxelSize,
                0f,
                ShowcaseMountainDragonLayout.EntryZdm * ShowcaseWorld.VoxelSize);
            showcase.TeleportTo(entry);

            float residencyDeadline = Time.realtimeSinceStartup + 8f;
            while (!world.IsGenerated(ShowcaseWorld.RegionAt(motor.Position)))
            {
                if (Time.realtimeSinceStartup >= residencyDeadline)
                {
                    Fail("the production region under the authored road entrance did not become resident");
                    yield break;
                }
                yield return null;
            }

            // AutoWalk is intentionally a circular benchmark input: VoxelShowcase rotates it by
            // 24 degrees/second. A one-time heading therefore leaves the authored road immediately
            // and can collide with the mountain even when ordinary player movement is correct.
            // Compensate that same production turn every frame, exactly as the SceneIssue replay
            // does, so this module-local proof measures grounded CharacterMotor movement along the
            // resolved road rather than the benchmark's off-road circle.
            ResolvedWorldRoadPoint targetPoint = route.Road.Points[2];
            Vector2 target = new Vector2(
                targetPoint.Xdm * ShowcaseWorld.VoxelSize,
                targetPoint.Zdm * ShowcaseWorld.VoxelSize);
            Vector3 start = motor.Position;
            float movementDeadline = Time.realtimeSinceStartup + 2.5f;
            const float arrivalRadius = 0.75f;

            MouseLookField.SetValue(showcase, false);
            while (Time.realtimeSinceStartup < movementDeadline)
            {
                Vector2 current = new Vector2(motor.Position.x, motor.Position.z);
                Vector2 delta = target - current;
                if (delta.sqrMagnitude <= arrivalRadius * arrivalRadius)
                    break;

                float desiredYaw = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
                YawField.SetValue(
                    showcase,
                    desiredYaw - ExistingAutoWalkDegreesPerSecond * Time.deltaTime);
                PitchField.SetValue(showcase, 0f);
                showcase.AutoWalk = true;
                yield return null;
            }

            showcase.AutoWalk = false;
            yield return null;

            Vector3 end = motor.Position;
            float horizontal = Vector2.Distance(
                new Vector2(start.x, start.z),
                new Vector2(end.x, end.z));
            float remaining = Vector2.Distance(new Vector2(end.x, end.z), target);
            if (horizontal < 2f)
            {
                Fail($"production grounded movement advanced only {horizontal:0.00}m");
                yield break;
            }
            if (remaining > 1.25f)
            {
                Fail($"production grounded movement stayed {remaining:0.00}m from the resolved road target");
                yield break;
            }
            if (!motor.Grounded)
            {
                Fail("production CharacterMotor was not grounded after road traversal");
                yield break;
            }

            Debug.Log(
                $"{ReadyPrefix} moved={horizontal:0.00}m remaining={remaining:0.00}m grounded={motor.Grounded} "
                + $"start=({start.x:0.00},{start.y:0.00},{start.z:0.00}) "
                + $"end=({end.x:0.00},{end.y:0.00},{end.z:0.00})");
        }

        private static void Fail(string detail)
        {
            Debug.LogError(FailurePrefix + " " + detail);
            Application.Quit(32);
        }
    }
}
