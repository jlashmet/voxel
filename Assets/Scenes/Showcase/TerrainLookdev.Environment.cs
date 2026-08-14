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
            camera.fieldOfView = 28f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 120f;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            // The reference is a terrain-filled portrait composition with a long valley view and
            // no sky. Keep the upper camera ray on the far terrain while preserving foreground
            // perspective and a deeper midground than the first lookdev framing.
            camera.transform.position = new Vector3(-0.6f, 23.0f, -20.0f);
            camera.transform.LookAt(new Vector3(0.1f, 2.0f, 12.0f));

            // Keep the production material colours intact. The earlier non-white debug tint
            // exaggerated channels into a neon false-colour look and is not presentation lighting.
            VoxelRenderBridge.SurfaceDebugTint = Color.white;
            VoxelRenderBridge.SunDirection = new Vector3(-0.58f, 0.74f, -0.34f).normalized;
            VoxelRenderBridge.SkyHorizon = new Color(0.78f, 0.77f, 0.47f, 1f);
            VoxelRenderBridge.SkyZenith = new Color(0.68f, 0.72f, 0.42f, 1f);
        }
    }
}
