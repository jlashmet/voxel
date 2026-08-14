using UnityEditor;
using UnityEngine;

namespace VoxelEngine.CI
{
    [InitializeOnLoad]
    internal static class AuthoredWaterColorGlobal
    {
        private static Texture2D s_Texture;
        private static readonly int AuthoredWaterTexId = Shader.PropertyToID("_AuthoredWaterTex");

        static AuthoredWaterColorGlobal()
        {
            EnsureTexture();
            Publish();
            Camera.onPreRender -= OnCameraPreRender;
            Camera.onPreRender += OnCameraPreRender;
            EditorApplication.delayCall += Publish;
        }

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            EnsureTexture();
            Publish();
            Camera.onPreRender -= OnCameraPreRender;
            Camera.onPreRender += OnCameraPreRender;
        }

        private static void EnsureTexture()
        {
            if (s_Texture == null)
                s_Texture = AuthoredWaterColor.Build();
        }

        private static void Publish()
        {
            EnsureTexture();
            Shader.SetGlobalTexture(AuthoredWaterTexId, s_Texture);
        }

        private static void OnCameraPreRender(Camera camera)
        {
            Publish();
            if (camera != null && camera.name == "Water Lookdev Camera")
                Debug.Log($"Authored water color field rebound before {camera.name}: {s_Texture.width}x{s_Texture.height}");
        }
    }
}
