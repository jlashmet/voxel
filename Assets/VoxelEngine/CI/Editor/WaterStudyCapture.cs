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
                material.SetColor("_DeepColor", new Color(0.015f, 0.27f, 0.52f, 1f));
                material.SetColor("_MidColor", new Color(0.00f, 0.62f, 0.86f, 1f));
                material.SetColor("_ShallowColor", new Color(0.20f, 0.84f, 0.98f, 1f));
                material.SetColor("_FoamColor", new Color(0.96f, 0.995f, 1f, 1f));
                material.SetFloat("_FlowSpeed", 0.24f);
                material.SetFloat("_FlowStrength", 0.007f);
                material.SetFloat("_Shimmer", 0.42f);
                material.SetFloat("_EdgeFoam", 0.82f);
                material.SetFloat("_Alpha", 1f);

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
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
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
                    "mask=asymmetric fragmented pools + multiple offset waterfall sheets\n" +
                    "shader=opaque cyan water with bright edge foam, vertical falls and surface ribbons\n" +
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

                    // Upper-right source fall and catch pool.
                    mask = Mathf.Max(mask, RaggedPool(u, v, 0.77f, 0.935f, 0.105f, 0.050f, -0.18f));
                    mask = Mathf.Max(mask, FallSheet(u, v, 0.745f, 0.905f, 0.705f, 0.850f, 0.043f));
                    mask = Mathf.Max(mask, RaggedPool(u, v, 0.675f, 0.835f, 0.085f, 0.022f, 0.02f));

                    // Upper-middle broken double falls.
                    mask = Mathf.Max(mask, FallSheet(u, v, 0.660f, 0.817f, 0.645f, 0.755f, 0.060f));
                    mask = Mathf.Max(mask, FallSheet(u, v, 0.595f, 0.804f, 0.580f, 0.742f, 0.050f));
                    mask = Mathf.Max(mask, RaggedPool(u, v, 0.650f, 0.735f, 0.135f, 0.032f, -0.08f));

                    // Right stepping stream and small drops.
                    mask = Mathf.Max(mask, RaggedPool(u, v, 0.800f, 0.690f, 0.120f, 0.035f, -0.20f));
                    mask = Mathf.Max(mask, FallSheet(u, v, 0.830f, 0.675f, 0.850f, 0.610f, 0.043f));
                    mask = Mathf.Max(mask, RaggedPool(u, v, 0.865f, 0.595f, 0.130f, 0.030f, -0.10f));

                    // Main left waterfall and broad central river shelf.
                    mask = Mathf.Max(mask, FallSheet(u, v, 0.300f, 0.675f, 0.300f, 0.585f, 0.072f));
                    mask = Mathf.Max(mask, RaggedPool(u, v, 0.285f, 0.565f, 0.205f, 0.050f, 0.04f));
                    mask = Mathf.Max(mask, RaggedPool(u, v, 0.505f, 0.545f, 0.280f, 0.070f, -0.02f));
                    mask = Mathf.Max(mask, RaggedPool(u, v, 0.710f, 0.555f, 0.190f, 0.050f, 0.08f));
                    mask = Mathf.Max(mask, FallSheet(u, v, 0.730f, 0.560f, 0.760f, 0.500f, 0.045f));

                    // Scattered upper-left droplets/shelves from the reference silhouette.
                    mask = Mathf.Max(mask, RaggedPool(u, v, 0.120f, 0.785f, 0.050f, 0.016f, 0.1f));
                    mask = Mathf.Max(mask, RaggedPool(u, v, 0.205f, 0.750f, 0.072f, 0.018f, -0.1f));
                    mask = Mathf.Max(mask, RaggedPool(u, v, 0.270f, 0.700f, 0.090f, 0.024f, 0.15f));
                    mask = Mathf.Max(mask, RaggedPool(u, v, 0.410f, 0.650f, 0.070f, 0.017f, -0.1f));

                    // Dominant lower-right water mass, intentionally entering from the frame edge.
                    mask = Mathf.Max(mask, RaggedPool(u, v, 0.900f, 0.285f, 0.300f, 0.120f, -0.18f));
                    mask = Mathf.Max(mask, RaggedPool(u, v, 0.820f, 0.155f, 0.330f, 0.145f, -0.35f));
                    mask = Mathf.Max(mask, RaggedPool(u, v, 0.960f, 0.065f, 0.260f, 0.120f, -0.12f));
                    mask = Mathf.Max(mask, FallSheet(u, v, 0.875f, 0.395f, 0.840f, 0.315f, 0.060f));

                    // Large punched negative spaces are crucial to the reference read.
                    mask *= 1f - 0.98f * Ellipse(u, v, 0.775f, 0.335f, 0.075f, 0.043f, -0.10f);
                    mask *= 1f - 0.98f * Ellipse(u, v, 0.875f, 0.205f, 0.060f, 0.045f, 0.15f);
                    mask *= 1f - 0.98f * Ellipse(u, v, 0.935f, 0.080f, 0.075f, 0.050f, -0.10f);
                    mask *= 1f - 0.95f * Ellipse(u, v, 0.420f, 0.540f, 0.070f, 0.030f, 0.05f);
                    mask *= 1f - 0.92f * Ellipse(u, v, 0.620f, 0.555f, 0.055f, 0.024f, -0.10f);

                    // Quantized noise chips the silhouette instead of merely fading it.
                    float coarse = Mathf.PerlinNoise(u * 28f + 4.2f, v * 31f + 8.1f);
                    float fine = Mathf.PerlinNoise(u * 67f + 11.7f, v * 53f + 2.9f);
                    float chip = coarse * 0.70f + fine * 0.30f;
                    if (mask > 0.12f && chip < 0.205f)
                        mask *= 0.18f;

                    mask = Mathf.SmoothStep(0.16f, 0.52f, mask);
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(mask) * 255f);
                    pixels[y * MaskWidth + x] = new Color32(a, a, a, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static float RaggedPool(float u, float v, float cx, float cy, float rx, float ry, float rot)
        {
            float baseShape = Ellipse(u, v, cx, cy, rx, ry, rot);
            float n1 = Mathf.PerlinNoise((u - cx) * 47f + 6.3f, (v - cy) * 59f + 4.1f);
            float n2 = Mathf.PerlinNoise((u - cx) * 103f + 1.7f, (v - cy) * 83f + 9.4f);
            float irregular = Mathf.Lerp(0.80f, 1.12f, n1 * 0.75f + n2 * 0.25f);
            return Mathf.Clamp01(baseShape * irregular);
        }

        private static float FallSheet(float u, float v, float ax, float ay, float bx, float by, float halfWidth)
        {
            float sheet = Capsule(u, v, ax, ay, bx, by, halfWidth);
            float verticalBreak = Mathf.PerlinNoise(u * 72f + 2.3f, v * 24f + 4.8f);
            float ribs = 0.86f + 0.14f * Mathf.Sin(u * 210f + verticalBreak * 8f);
            return Mathf.Clamp01(sheet * ribs);
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
            return 1f - Mathf.SmoothStep(0.84f, 1.03f, d);
        }

        private static float Capsule(float u, float v, float ax, float ay, float bx, float by, float radius)
        {
            Vector2 p = new Vector2(u, v);
            Vector2 a = new Vector2(ax, ay);
            Vector2 b = new Vector2(bx, by);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, Vector2.Dot(ab, ab)));
            float d = Vector2.Distance(p, a + ab * t) / radius;
            return 1f - Mathf.SmoothStep(0.76f, 1.03f, d);
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