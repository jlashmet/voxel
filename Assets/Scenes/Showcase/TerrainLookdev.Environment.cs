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
            camera.backgroundColor = new Color(0.72f, 0.72f, 0.42f, 1f);
            camera.fieldOfView = 28f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 180f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.transform.position = new Vector3(-0.7f, 18.8f, -20.6f);
            camera.transform.LookAt(new Vector3(0.15f, 2.6f, 24.5f));

            VoxelRenderBridge.SurfaceDebugTint = Color.white;
            VoxelRenderBridge.SunDirection = new Vector3(-0.47f, 0.80f, -0.37f).normalized;
            VoxelRenderBridge.SkyHorizon = new Color(0.73f, 0.76f, 0.50f, 1f);
            VoxelRenderBridge.SkyZenith = new Color(0.65f, 0.70f, 0.43f, 1f);
        }
    }
}
