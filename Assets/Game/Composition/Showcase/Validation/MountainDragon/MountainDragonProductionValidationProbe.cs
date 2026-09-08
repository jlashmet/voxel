using System.Collections;
using System.Reflection;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Voxel;
using UnityEngine;

namespace VoxelEngine.Showcase.Validation
{
    /// <summary>
    /// Player-build orchestration for the Showcase-owned Mountain Dragon validation scene.
    /// Geometry, materials, streaming, rendering and encounter components remain the production
    /// VoxelShowcase path; this probe only places the real player at the authored approach and
    /// holds a deterministic inspection heading for the paired screenshot scenario.
    /// </summary>
    public sealed class MountainDragonProductionValidationProbe : MonoBehaviour
    {
        private const uint Seed = 0x5EED1234u;
        private const string ReadyPrefix = "MOUNTAIN_DRAGON_SHOWCASE_VALIDATION ready:";
        private const string FailurePrefix = "MOUNTAIN_DRAGON_SHOWCASE_VALIDATION failure:";

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
                Fail("scene could not bind the production VoxelShowcase inspection state");
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
                || !route.Road.IsResolved)
            {
                Fail("production Mountain Dragon ascent did not resolve");
                yield break;
            }

            // Use the production placement seam. The first teleport puts streaming at the authored
            // entrance even if that region has not yet become resident; after residency arrives,
            // repeat the same placement so SnapToGround reads authoritative road/terrain voxels.
            float entryX = ShowcaseMountainDragonLayout.EntryXdm * ShowcaseWorld.VoxelSize;
            float entryZ = ShowcaseMountainDragonLayout.EntryZdm * ShowcaseWorld.VoxelSize;
            Vector3 entry = new Vector3(entryX, 0f, entryZ);
            showcase.TeleportTo(entry);
            showcase.AutoWalk = false;

            float residencyDeadline = Time.realtimeSinceStartup + 8f;
            while (!world.IsGenerated(ShowcaseWorld.RegionAt(motor.Position)))
            {
                if (Time.realtimeSinceStartup >= residencyDeadline)
                {
                    Fail("the production region under the authored mountain entrance did not become resident");
                    yield break;
                }
                yield return null;
            }
            showcase.TeleportTo(entry);

            Vector3 target = new Vector3(
                ShowcaseMountainDragonLayout.CentreXdm * ShowcaseWorld.VoxelSize,
                transform.position.y + 6f,
                ShowcaseMountainDragonLayout.CentreZdm * ShowcaseWorld.VoxelSize);
            Vector3 delta = target - transform.position;
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float planar = new Vector2(delta.x, delta.z).magnitude;
            float pitch = -Mathf.Atan2(delta.y, Mathf.Max(0.001f, planar)) * Mathf.Rad2Deg;
            YawField.SetValue(showcase, yaw);
            PitchField.SetValue(showcase, pitch);
            MouseLookField.SetValue(showcase, false);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            // Let the production surface scheduler publish the nearby mountain before capture.
            yield return new WaitForSecondsRealtime(4f);

            string far = showcase.DescribeFarTerrain();
            Debug.Log(
                $"{ReadyPrefix} routePoints={route.Road.Points.Count} "
                + $"entry=({entryX:0.0},{entryZ:0.0}) centre="
                + $"({ShowcaseMountainDragonLayout.CentreXdm * ShowcaseWorld.VoxelSize:0.0},"
                + $"{ShowcaseMountainDragonLayout.CentreZdm * ShowcaseWorld.VoxelSize:0.0}) "
                + $"grounded={motor.Grounded} {far}");
        }

        private static void Fail(string detail)
        {
            Debug.LogError(FailurePrefix + " " + detail);
            Application.Quit(31);
        }
    }
}
