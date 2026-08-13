using UnityEngine;
using VoxelEngine.Rendering;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private void ConfigureEnvironment()
        {
            Camera camera = SceneCamera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.73f, 0.43f, 1f);
            camera.fieldOfView = 31.5f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 120f;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            // The reference is a terrain-filled portrait composition with no visible horizon.
            // Pitch the production camera down far enough that even its upper ray lands on the
            // authored valley, while retaining enough perspective for large foreground stones.
            camera.transform.position = new Vector3(-0.8f, 20.5f, -16.5f);
            camera.transform.LookAt(new Vector3(0.2f, 1.8f, 6.0f));

            // Warm, high-key daylight is a major part of the reference's yellow-green palette.
            VoxelRenderBridge.SurfaceDebugTint = new Color(1.05f, 1.02f, 0.88f, 1f);
            VoxelRenderBridge.SunDirection = new Vector3(-0.58f, 0.74f, -0.34f).normalized;
            VoxelRenderBridge.SkyHorizon = new Color(0.79f, 0.80f, 0.51f, 1f);
            VoxelRenderBridge.SkyZenith = new Color(0.70f, 0.75f, 0.44f, 1f);
        }
    }
}
