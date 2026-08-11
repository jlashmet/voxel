using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    /// <summary>
    /// SunlitClericCapture intentionally builds its smooth proxy materials at runtime. The first
    /// version used the built-in Standard shader, while this project is configured for URP. Keep
    /// the scene builder simple and convert those temporary materials immediately before the
    /// target camera renders. This touches only the named CI camera and therefore cannot alter
    /// normal game/editor rendering.
    /// </summary>
    [InitializeOnLoad]
    internal static class SunlitClericUrpMaterialBridge
    {
        static SunlitClericUrpMaterialBridge()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || camera.name != "Sunlit Cleric Camera") return;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("Sunlit Cleric: Universal Render Pipeline/Lit was not found.");
                return;
            }

            foreach (Renderer renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (renderer == null) continue;
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null || material.shader == null || material.shader.name != "Standard")
                        continue;

                    Color colour = material.HasProperty("_Color")
                        ? material.GetColor("_Color")
                        : Color.white;
                    Texture texture = material.HasProperty("_MainTex")
                        ? material.GetTexture("_MainTex")
                        : null;
                    Vector2 textureScale = material.HasProperty("_MainTex")
                        ? material.GetTextureScale("_MainTex")
                        : Vector2.one;
                    Vector2 textureOffset = material.HasProperty("_MainTex")
                        ? material.GetTextureOffset("_MainTex")
                        : Vector2.zero;
                    float smoothness = material.HasProperty("_Glossiness")
                        ? material.GetFloat("_Glossiness")
                        : 0.05f;
                    float metallic = material.HasProperty("_Metallic")
                        ? material.GetFloat("_Metallic")
                        : 0f;
                    Color emission = material.HasProperty("_EmissionColor")
                        ? material.GetColor("_EmissionColor")
                        : Color.black;
                    bool transparent = material.renderQueue >= (int)RenderQueue.Transparent || colour.a < 0.999f;

                    material.shader = urpLit;
                    material.SetColor("_BaseColor", colour);
                    material.SetColor("_Color", colour);
                    material.SetFloat("_Smoothness", smoothness);
                    material.SetFloat("_Metallic", metallic);
                    if (texture != null)
                    {
                        material.SetTexture("_BaseMap", texture);
                        material.SetTextureScale("_BaseMap", textureScale);
                        material.SetTextureOffset("_BaseMap", textureOffset);
                    }

                    if (emission.maxColorComponent > 0.001f)
                    {
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", emission);
                    }

                    if (transparent)
                    {
                        material.SetOverrideTag("RenderType", "Transparent");
                        material.SetFloat("_Surface", 1f);
                        material.SetFloat("_Blend", 0f);
                        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                        material.SetInt("_ZWrite", 0);
                        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        material.DisableKeyword("_ALPHATEST_ON");
                        material.renderQueue = (int)RenderQueue.Transparent;
                    }
                    else
                    {
                        material.SetOverrideTag("RenderType", "Opaque");
                        material.SetFloat("_Surface", 0f);
                        material.SetInt("_ZWrite", 1);
                        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        material.renderQueue = (int)RenderQueue.Geometry;
                    }

                    materials[i] = material;
                    changed = true;
                }

                if (changed) renderer.sharedMaterials = materials;
            }
        }
    }
}
