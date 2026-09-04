using System;
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

        private IEnumerator Start()
        {
            VoxelShowcase showcase = GetComponent<VoxelShowcase>();
            if (showcase == null)
            {
                Fail("scene must host the production VoxelShowcase component");
                yield break;
            }

            if (YawField == null || PitchField == null || MouseLookField == null)
            {
                Fail("could not bind the production showcase inspection heading");
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

            // VoxelShowcase.OnEnable has already created the real ShowcaseWorld before Start.
            // Use its public player-placement seam, which is the same path used by SceneIssue replay.
            float entryX = ShowcaseMountainDragonLayout.EntryXdm * ShowcaseWorld.VoxelSize;
            float entryZ = ShowcaseMountainDragonLayout.EntryZdm * ShowcaseWorld.VoxelSize;
            showcase.TeleportTo(new Vector3(entryX, 0f, entryZ));
            showcase.AutoWalk = false;

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

            // Give production streaming/meshing several frames to publish the nearby landmark.
            yield return new WaitForSecondsRealtime(4f);

            string far = showcase.DescribeFarTerrain();
            Debug.Log(
                $"{ReadyPrefix} routePoints={route.Road.Points.Count} "
                + $"entry=({entryX:0.0},{entryZ:0.0}) centre="
                + $"({ShowcaseMountainDragonLayout.CentreXdm * ShowcaseWorld.VoxelSize:0.0},"
                + $"{ShowcaseMountainDragonLayout.CentreZdm * ShowcaseWorld.VoxelSize:0.0}) {far}");
        }

        private static void Fail(string detail)
        {
            Debug.LogError(FailurePrefix + " " + detail);
            Application.Quit(31);
        }
    }
}
