using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Replaces the original geometric water study at render time with the deterministic
    /// reference-aligned raster layer. This keeps the existing guarded CI entry point intact.
    /// </summary>
    [InitializeOnLoad]
    internal static class WaterStudyReferenceOverlayHook
    {
        private const int Width = 1024;
        private const int Height = 1536;
        private static GameObject overlay;
        private static Texture2D texture;
        private static Material material;

        static WaterStudyReferenceOverlayHook()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || camera.name != "Water Study Camera") return;

            GameObject source = GameObject.Find("Water Reference Study");
            if (source != null)
            {
                Renderer[] renderers = source.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers) renderer.enabled = false;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);

            if (overlay != null) return;

            Color32[] pixels = BuildReferencePixels();
            texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false)
            {
                name = "Reference Water Layer",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            Shader shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("No transparent unlit shader available for water overlay.");
            material = new Material(shader) { name = "Reference Water Overlay" };
            material.mainTexture = texture;
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);

            overlay = new GameObject("Reference Water Screen Layer");
            overlay.transform.SetParent(camera.transform, false);
            overlay.transform.localPosition = new Vector3(0f, 0f, 2f);
            overlay.transform.localRotation = Quaternion.identity;
            float h = camera.orthographicSize * 2f;
            float w = h * Width / Height;
            overlay.transform.localScale = new Vector3(w, h, 1f);

            Mesh mesh = new Mesh { name = "Reference Water Screen Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();

            MeshFilter filter = overlay.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer rendererComponent = overlay.AddComponent<MeshRenderer>();
            rendererComponent.sharedMaterial = material;
            rendererComponent.shadowCastingMode = ShadowCastingMode.Off;
            rendererComponent.receiveShadows = false;
        }

        private static Color32[] BuildReferencePixels()
        {
            Type type = typeof(WaterStudyRasterCapture);
            FieldInfo pixelsField = type.GetField("pixels", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo drawMethod = type.GetMethod("DrawWater", BindingFlags.NonPublic | BindingFlags.Static);
            if (pixelsField == null || drawMethod == null)
                throw new MissingMemberException("WaterStudyRasterCapture raster internals were not found.");

            var buffer = new Color32[Width * Height];
            pixelsField.SetValue(null, buffer);
            drawMethod.Invoke(null, null);
            Color32[] rendered = (Color32[])pixelsField.GetValue(null);
            pixelsField.SetValue(null, null);
            return rendered;
        }
    }
}
