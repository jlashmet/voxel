using UnityEditor;
using UnityEngine;

namespace VoxelEngine.CI
{
    [InitializeOnLoad]
    internal static class AuthoredWaterColorGlobal
    {
        private static Texture2D s_Texture;

        static AuthoredWaterColorGlobal()
        {
            s_Texture = AuthoredWaterColor.Build();
            Shader.SetGlobalTexture("_AuthoredWaterTex", s_Texture);
        }
    }
}
