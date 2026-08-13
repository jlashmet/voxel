using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private void ConfigureEnvironment()
        {
            Camera camera = SceneCamera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.73f, 0.45f, 1f);
            camera.fieldOfView = 27.5f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 180f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.transform.position = new Vector3(-0.8f, 18.7f, -20.2f);
            camera.transform.LookAt(new Vector3(0.15f, 2.6f, 22.5f));

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.54f, 0.53f, 0.34f, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.74f, 0.75f, 0.47f, 1f);
            RenderSettings.fogStartDistance = 42f;
            RenderSettings.fogEndDistance = 92f;

            GameObject sunObject = new("Terrain Sun");
            sunObject.transform.SetParent(transform.parent, false);
            sunObject.transform.rotation = Quaternion.Euler(46f, -36f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.91f, 0.66f, 1f);
            sun.intensity = 1.55f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.58f;
        }
    }
}
