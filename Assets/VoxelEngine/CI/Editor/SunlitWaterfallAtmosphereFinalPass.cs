using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    /// <summary>Locks the CI capture to the reference's clean blue storybook atmosphere.</summary>
    internal static class SunlitWaterfallAtmosphereFinalPass
    {
        private static bool _done;

        public static void Apply(Camera camera)
        {
            if (_done || camera == null) return;
            _done = true;

            // Keep this assembly independent of the optional render-pipelines-core Volume type.
            // CI only needs to disable any loaded component whose runtime type is a Volume.
            Behaviour[] behaviours = Resources.FindObjectsOfTypeAll<Behaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (behaviour != null && behaviour.GetType().Name == "Volume")
                    behaviour.enabled = false;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.66f, 0.96f, 1f);
            RenderSettings.skybox = null;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.69f, 0.83f, 0.98f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.66f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.29f, 0.18f);
            RenderSettings.ambientIntensity = 0.78f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.58f, 0.82f, 0.97f);
            RenderSettings.fogStartDistance = 34f;
            RenderSettings.fogEndDistance = 72f;
        }
    }
}
