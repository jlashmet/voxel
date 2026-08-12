using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Keeps the isolated Kentridge street diagnostics on the authored settlement surface.
    ///
    /// The runtime capture predates Kentridge's macro vertical profile and still derives street-eye
    /// height from raw TerrainSampler columns. Once the town owns 15-17 m of authored relief that can
    /// put a diagnostic camera inside a retaining shoulder. This editor-only hook adjusts only the
    /// four FOV-52 street captures immediately before render; production cameras and worldgen remain
    /// untouched.
    /// </summary>
    [InitializeOnLoad]
    internal static class KentridgeCameraCaptureHook
    {
        private const uint Seed = 0x4B454E54u;
        private const string CameraObjectName = "CI Kentridge Runtime Camera";
        private const float EyeHeightMetres = 3.4f;
        private const float StreetFov = 52f;
        private const float FovTolerance = 0.25f;
        private const float DecimetresPerMetre = 10f;
        private const float MetresPerVoxel = 0.1f;

        static KentridgeCameraCaptureHook()
        {
            Camera.onPreCull += OnCameraPreCull;
        }

        private static void OnCameraPreCull(Camera camera)
        {
            if (camera == null || camera.gameObject.name != CameraObjectName) return;
            if (Mathf.Abs(camera.fieldOfView - StreetFov) > FovTolerance) return;

            Transform transform = camera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.5f) return;

            int cameraXDm = Mathf.RoundToInt(transform.position.x * DecimetresPerMetre);
            int cameraZDm = Mathf.RoundToInt(transform.position.z * DecimetresPerMetre);
            int surfaceY = KentridgeVerticalProfile.SurfaceYAtDm(
                cameraXDm, cameraZDm, Seed, scale: 1);

            Vector3 position = transform.position;
            position.y = surfaceY * MetresPerVoxel + EyeHeightMetres;
            transform.position = position;

            // Side views look toward the market crossing. The south/downhill view looks toward the
            // lower residential band, while the north/uphill view is deliberately aimed high enough
            // to stack market roofs below the civic summit in one frame.
            Vector3 target;
            if (Mathf.Abs(forward.z) >= Mathf.Abs(forward.x))
            {
                if (forward.z < 0f)
                {
                    // Low-town camera looking north/uphill toward the civic shelf.
                    target = TargetAt(
                        KentridgeTownPlanner.MainSpineXDm,
                        190,
                        verticalLiftMetres: 4.8f);
                }
                else
                {
                    // Summit-side camera looking south/downhill across market and residences.
                    target = TargetAt(
                        KentridgeTownPlanner.MainSpineXDm,
                        760,
                        verticalLiftMetres: 3.0f);
                }
            }
            else
            {
                target = TargetAt(
                    KentridgeDefinition.TownCentreDm.X,
                    KentridgeDefinition.TownCentreDm.Y,
                    verticalLiftMetres: 3.8f);
            }

            transform.LookAt(target);
        }

        private static Vector3 TargetAt(int xDm, int zDm, float verticalLiftMetres)
        {
            int y = KentridgeVerticalProfile.SurfaceYAtDm(xDm, zDm, Seed, scale: 1);
            return new Vector3(
                xDm * MetresPerVoxel,
                y * MetresPerVoxel + verticalLiftMetres,
                zDm * MetresPerVoxel);
        }
    }
}
