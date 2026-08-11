using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    [InitializeOnLoad]
    internal static class SunlitWaterfallMaterialBridge
    {
        static SunlitWaterfallMaterialBridge()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || camera.name != "Sunlit Waterfall Environment Camera") return;

            GameObject voxelSurface = GameObject.Find("Voxel Surface");
            if (voxelSurface != null) voxelSurface.SetActive(false);

            Shader smooth = Shader.Find("VoxelEngine/SunlitSmooth");
            if (smooth == null)
            {
                Debug.LogError("Sunlit Waterfall: VoxelEngine/SunlitSmooth shader was not found.");
                return;
            }

            foreach (Renderer renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (renderer == null || renderer.sharedMaterials == null) continue;
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null || material.shader == null) continue;
                    if (material.shader.name != "Standard" && material.shader.name != "Universal Render Pipeline/Lit") continue;

                    Color colour = material.HasProperty("_Color") ? material.GetColor("_Color") : material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
                    Texture texture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                    Vector2 scale = material.HasProperty("_MainTex") ? material.GetTextureScale("_MainTex") : Vector2.one;
                    Vector2 offset = material.HasProperty("_MainTex") ? material.GetTextureOffset("_MainTex") : Vector2.zero;
                    float smoothness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : 0.05f;
                    Color emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
                    bool transparent = material.renderQueue >= (int)RenderQueue.Transparent || colour.a < 0.999f;

                    material.shader = smooth;
                    material.SetTexture("_MainTex", texture != null ? texture : Texture2D.whiteTexture);
                    material.SetTextureScale("_MainTex", scale);
                    material.SetTextureOffset("_MainTex", offset);
                    material.SetColor("_BaseColor", colour);
                    material.SetColor("_EmissionColor", emission);
                    material.SetFloat("_Smoothness", smoothness);
                    material.SetFloat("_Cull", transparent ? 0f : 2f);
                    material.SetFloat("_ZWrite", transparent ? 0f : 1f);
                    material.renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }

            SunlitWaterfallArtPass.Apply(camera);
            SunlitWaterfallTuningPass.Apply(camera);
            SunlitWaterfallMatchPass.Apply(camera);
            SunlitWaterfallOrganicPass.Apply(camera);
            SunlitWaterfallFinalShapePass.Apply(camera);
            SunlitWaterfallPolishPass.Apply(camera);
        }
    }
}
