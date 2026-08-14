using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.CI
{
    public static class WaterStudyCapture
    {
        private const int Width = 1024;
        private const int Height = 1536;
        private const int MaskWidth = 512;
        private const int MaskHeight = 768;

        public static void Run()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "Water");
            Directory.CreateDirectory(outputDirectory);

            GameObject quad = null;
            GameObject cameraObject = null;
            RenderTexture target = null;
            Texture2D capture = null;
            Texture2D maskTexture = null;
            Material material = null;
            Mesh mesh = null;

            try
            {
                Shader shader = Shader.Find("Hidden/VoxelEngine/StylizedWaterLookdev");
                if (shader == null)
                    throw new InvalidOperationException("StylizedWaterLookdev shader was not found.");

                maskTexture = BuildReferenceLikeMask();
                material = new Material(shader) { name = "AAA Stylized Water Material" };
                material.SetTexture("_ReferenceTex", maskTexture);
                material.SetColor("_DeepColor", new Color(0.018f, 0.30f, 0.56f, 1f));
                material.SetColor("_MidColor", new Color(0.02f, 0.66f, 0.88f, 1f));
                material.SetColor("_ShallowColor", new Color(0.31f, 0.90f, 0.99f, 1f));
                material.SetColor("_FoamColor", new Color(0.96f, 0.995f, 1f, 1f));
                material.SetFloat("_TimeOffset", 1.75f);
                material.SetFloat("_FoamWidth", 0.12f);
                material.SetFloat("_FlowStrength", 0.55f);
                material.SetFloat("_SparkleStrength", 0.18f);

                quad = new GameObject("Water Lookdev Quad");
                mesh = BuildQuadMesh();
                var filter = quad.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                var renderer = quad.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;

                cameraObject = new GameObject("Water Lookdev Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.86f, 0.94f, 0.91f, 1f);
                camera.allowHDR = false;
                camera.allowMSAA = true;
                camera.orthographic = true;
                camera.orthographicSize = 1f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 10f;
                camera.transform.position = new Vector3(0f, 0f, -2f);
                camera.transform.rotation = Quaternion.identity;

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 4,
                    name = "Water Study Target"
                };
                target.Create();
                camera.targetTexture = target;

                Shader.WarmupAllShaders();
                RenderTexture previous = RenderTexture.active;
                try
                {
                    camera.Render();
                    RenderTexture.active = target;
                    capture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                    capture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    capture.Apply(false, false);
                    File.WriteAllBytes(Path.Combine(outputDirectory, "water-study.png"), capture.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    camera.targetTexture = null;
                }

                File.WriteAllText(Path.Combine(outputDirectory, "water-study.txt"),
                    "target=Sunlit Cleric waterfall water\n" +
                    "mask=procedural authored terraces + narrow falls\n" +
                    "shader=layered stylized cyan water with foam, flow and sparkle\n" +
                    $"size={Width}x{Height}\n");

                Debug.Log($"Water study written to {outputDirectory}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                return;
            }
            finally
            {
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (quad != null) UnityEngine.Object.DestroyImmediate(quad);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                if (maskTexture != null) UnityEngine.Object.DestroyImmediate(maskTexture);
            }

            EditorApplication.Exit(0);
        }

        private static Texture2D BuildReferenceLikeMask()
        {
            var texture = new Texture2D(MaskWidth, MaskHeight, TextureFormat.RGBA32, false, true)
            {
                name = "Procedural Authored Water Silhouette",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[MaskWidth * MaskHeight];
            for (int y = 0; y < MaskHeight; y++)
            {
                float v = (y + 0.5f) / MaskHeight;
                for (int x = 0; x < MaskWidth; x++)
                {
                    float u = (x + 0.5f) / MaskWidth;
                    float mask = 0f;

                    mask = Mathf.Max(mask, Ellipse(u, v, 0.68f, 0.83f, 0.19f, 0.075f, 0.08f));
                    mask = Mathf.Max(mask, Ellipse(u, v, 0.60f, 0.68f, 0.23f, 0.085f, -0.05f));
                    mask = Mathf.Max(mask, Ellipse(u, v, 0.50f, 0.51f, 0.29f, 0.10f, 0.05f));
                    mask = Mathf.Max(mask, Ellipse(u, v, 0.44f, 0.33f, 0.35f, 0.12f, -0.06f));
                    mask = Mathf.Max(mask, Ellipse(u, v, 0.55f, 0.13f, 0.43f, 0.13f, 0.02f));

                    mask = Mathf.Max(mask, Capsule(u, v, 0.65f, 0.765f, 0.61f, 0.735f, 0.055f));
                    mask = Mathf.Max(mask, Capsule(u, v, 0.58f, 0.63f, 0.53f, 0.57f, 0.07f));
                    mask = Mathf.Max(mask, Capsule(u, v, 0.50f, 0.45f, 0.46f, 0.39f, 0.082f));
                    mask = Mathf.Max(mask, Capsule(u, v, 0.67f, 0.29f, 0.64f, 0.23f, 0.045f));

                    mask *= 1f - 0.95f * Ellipse(u, v, 0.36f, 0.51f, 0.11f, 0.045f, 0.1f);
                    mask *= 1f - 0.90f * Ellipse(u, v, 0.72f, 0.34f, 0.12f, 0.05f, -0.2f);
                    mask *= 1f - 0.88f * Ellipse(u, v, 0.30f, 0.14f, 0.14f, 0.055f, 0.1f);

                    float n = Mathf.PerlinNoise(u * 19.0f + 3.1f, v * 23.0f + 7.3f);
                    float edge = Mathf.SmoothStep(0.42f, 0.62f, n);
                    mask *= Mathf.Lerp(0.82f, 1f, edge);

                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(mask) * 255f);
                    pixels[y * MaskWidth + x] = new Color32(a, a, a, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static float Ellipse(float u, float v, float cx, float cy, float rx, float ry, float rot)
        {
            float c = Mathf.Cos(rot);
            float s = Mathf.Sin(rot);
            float dx = u - cx;
            float dy = v - cy;
            float x = (dx * c - dy * s) / rx;
            float y = (dx * s + dy * c) / ry;
            float d = Mathf.Sqrt(x * x + y * y);
            return 1f - Mathf.SmoothStep(0.88f, 1.02f, d);
        }

        private static float Capsule(float u, float v, float ax, float ay, float bx, float by, float radius)
        {
            Vector2 p = new Vector2(u, v);
            Vector2 a = new Vector2(ax, ay);
            Vector2 b = new Vector2(bx, by);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, Vector2.Dot(ab, ab)));
            float d = Vector2.Distance(p, a + ab * t) / radius;
            return 1f - Mathf.SmoothStep(0.82f, 1.04f, d);
        }

        private static Mesh BuildQuadMesh()
        {
            var mesh = new Mesh { name = "Water Lookdev Quad Mesh" };
            mesh.vertices = new[]
            {
                new Vector3(-2f / 3f, -1f, 0f),
                new Vector3( 2f / 3f, -1f, 0f),
                new Vector3(-2f / 3f,  1f, 0f),
                new Vector3( 2f / 3f,  1f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
