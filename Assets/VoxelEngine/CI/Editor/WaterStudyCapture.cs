using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Isolated look-development capture for the bright storybook water used by the
    /// Sunlit Cleric waterfall reference. The study intentionally contains water only:
    /// turquoise terraced pools, stepped falls, white foam, and painted highlight ribbons.
    /// </summary>
    public static class WaterStudyCapture
    {
        private const int Width = 1024;
        private const int Height = 1536;

        private sealed class Palette
        {
            public Material Deep;
            public Material Water;
            public Material Shallow;
            public Material Fall;
            public Material Foam;
            public Material Shine;
        }

        public static void Run()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "Water");
            Directory.CreateDirectory(outputDirectory);

            GameObject root = null;
            GameObject cameraObject = null;
            GameObject keyObject = null;
            RenderTexture target = null;
            Texture2D capture = null;
            var owned = new List<UnityEngine.Object>();

            Color previousSky = RenderSettings.ambientSkyColor;
            Color previousEquator = RenderSettings.ambientEquatorColor;
            Color previousGround = RenderSettings.ambientGroundColor;
            AmbientMode previousAmbientMode = RenderSettings.ambientMode;

            try
            {
                Palette palette = BuildPalette(owned);
                root = new GameObject("Water Reference Study");
                BuildWater(root.transform, palette, owned);

                cameraObject = new GameObject("Water Study Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.86f, 0.94f, 0.90f, 1f);
                camera.allowHDR = false;
                camera.allowMSAA = true;
                camera.orthographic = true;
                camera.orthographicSize = 10.7f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 80f;
                Vector3 focus = new Vector3(1.2f, 3.0f, 2.0f);
                camera.transform.position = focus + new Vector3(-0.6f, 14.5f, -18.0f);
                camera.transform.LookAt(focus + new Vector3(0.5f, 0.25f, 1.4f));

                keyObject = new GameObject("Water Study Sun");
                Light key = keyObject.AddComponent<Light>();
                key.type = LightType.Directional;
                key.color = new Color(1.0f, 0.96f, 0.86f);
                key.intensity = 1.2f;
                key.shadows = LightShadows.None;
                keyObject.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.55f, 0.79f, 0.93f);
                RenderSettings.ambientEquatorColor = new Color(0.42f, 0.67f, 0.74f);
                RenderSettings.ambientGroundColor = new Color(0.30f, 0.55f, 0.54f);

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Water Reference Capture",
                    antiAliasing = 4,
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

                string metadata =
                    "target=Sunlit Cleric waterfall water\n" +
                    "composition=portrait cascading pools\n" +
                    "palette=turquoise-cyan with white foam\n" +
                    $"size={Width}x{Height}\n";
                File.WriteAllText(Path.Combine(outputDirectory, "water-study.txt"), metadata);
                Debug.Log($"Water study written to {outputDirectory}\n{metadata}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                return;
            }
            finally
            {
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientSkyColor = previousSky;
                RenderSettings.ambientEquatorColor = previousEquator;
                RenderSettings.ambientGroundColor = previousGround;

                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (keyObject != null) UnityEngine.Object.DestroyImmediate(keyObject);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                foreach (UnityEngine.Object value in owned)
                    if (value != null) UnityEngine.Object.DestroyImmediate(value);
            }

            EditorApplication.Exit(0);
        }

        private static Palette BuildPalette(List<UnityEngine.Object> owned)
        {
            return new Palette
            {
                Deep = MakeMaterial("Water Deep", new Color(0.035f, 0.43f, 0.67f), owned),
                Water = MakeMaterial("Water Turquoise", new Color(0.055f, 0.67f, 0.86f), owned),
                Shallow = MakeMaterial("Water Shallow", new Color(0.25f, 0.82f, 0.94f), owned),
                Fall = MakeMaterial("Waterfall Cyan", new Color(0.42f, 0.86f, 0.97f), owned),
                Foam = MakeMaterial("Water Foam", new Color(0.91f, 0.985f, 1.0f), owned),
                Shine = MakeMaterial("Water Shine", new Color(0.69f, 0.96f, 1.0f), owned),
            };
        }

        private static Material MakeMaterial(string name, Color color, List<UnityEngine.Object> owned)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) throw new InvalidOperationException("No unlit shader available for water study.");
            Material material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            owned.Add(material);
            return material;
        }

        private static void BuildWater(Transform root, Palette p, List<UnityEngine.Object> owned)
        {
            AddPool(root, "Upper spring", new Vector3(4.8f, 6.9f, 8.7f), 2.35f, 1.55f, p.Deep, p.Water, p.Shallow, p.Shine, owned, 0.08f);
            AddPool(root, "Upper terrace", new Vector3(4.0f, 5.15f, 5.8f), 3.0f, 1.85f, p.Deep, p.Water, p.Shallow, p.Shine, owned, 0.45f);
            AddPool(root, "Middle terrace", new Vector3(2.9f, 3.35f, 2.8f), 3.7f, 2.15f, p.Deep, p.Water, p.Shallow, p.Shine, owned, 0.72f);
            AddPool(root, "Lower basin", new Vector3(1.1f, 1.45f, -0.7f), 5.4f, 2.75f, p.Deep, p.Water, p.Shallow, p.Shine, owned, 1.1f);

            Vector3[] channel =
            {
                new Vector3(1.0f, 0.78f, -2.5f),
                new Vector3(2.0f, 0.66f, -3.8f),
                new Vector3(3.5f, 0.52f, -5.2f),
                new Vector3(5.2f, 0.36f, -6.7f),
                new Vector3(6.4f, 0.25f, -8.0f),
            };
            float[] widths = { 4.2f, 4.8f, 5.2f, 5.7f, 6.2f };
            AddStream(root, "Foreground channel depth", channel, widths, -0.12f, p.Deep, owned);
            AddStream(root, "Foreground channel", channel, ScaleWidths(widths, 0.93f), 0.00f, p.Water, owned);
            AddStream(root, "Foreground shallow edge", channel, ScaleWidths(widths, 0.71f), 0.035f, p.Shallow, owned);
            AddChannelHighlights(root, channel, widths, p.Shine, owned);

            AddFall(root, "Top fall", new Vector3(5.05f, 6.85f, 7.75f), new Vector3(4.35f, 5.30f, 6.55f), 1.95f, p.Deep, p.Fall, p.Foam, p.Shine, owned, 11);
            AddFall(root, "Upper fall", new Vector3(4.15f, 5.05f, 4.75f), new Vector3(3.35f, 3.52f, 3.65f), 2.55f, p.Deep, p.Fall, p.Foam, p.Shine, owned, 23);
            AddFall(root, "Middle fall", new Vector3(2.85f, 3.25f, 1.68f), new Vector3(1.85f, 1.62f, 0.35f), 2.95f, p.Deep, p.Fall, p.Foam, p.Shine, owned, 37);
            AddFall(root, "Side fall", new Vector3(5.45f, 1.38f, -1.1f), new Vector3(5.9f, 0.44f, -2.45f), 1.25f, p.Deep, p.Fall, p.Foam, p.Shine, owned, 51);

            AddFoamArc(root, new Vector3(4.33f, 5.31f, 6.44f), 1.55f, 0.66f, 196f, 345f, 0.18f, p.Foam, owned);
            AddFoamArc(root, new Vector3(3.32f, 3.53f, 3.55f), 1.95f, 0.82f, 198f, 348f, 0.20f, p.Foam, owned);
            AddFoamArc(root, new Vector3(1.82f, 1.63f, 0.23f), 2.35f, 0.95f, 198f, 350f, 0.22f, p.Foam, owned);
            AddFoamArc(root, new Vector3(5.88f, 0.45f, -2.52f), 0.95f, 0.48f, 198f, 344f, 0.16f, p.Foam, owned);

            AddFoamFlecks(root, new Vector3(1.2f, 1.54f, -0.9f), 4.1f, 1.85f, 18, p.Foam, owned, 91);
            AddFoamFlecks(root, new Vector3(4.1f, 0.69f, -5.2f), 3.3f, 1.35f, 14, p.Foam, owned, 123);
        }

        private static float[] ScaleWidths(float[] source, float scale)
        {
            float[] result = new float[source.Length];
            for (int i = 0; i < source.Length; i++) result[i] = source[i] * scale;
            return result;
        }

        private static void AddPool(Transform parent, string name, Vector3 center, float radiusX, float radiusZ,
                                    Material deep, Material water, Material shallow, Material shine,
                                    List<UnityEngine.Object> owned, float ripplePhase)
        {
            AddEllipse(parent, name + " depth", center + Vector3.down * 0.18f, radiusX * 1.035f, radiusZ * 1.035f, deep, owned, 72);
            AddEllipse(parent, name, center, radiusX, radiusZ, water, owned, 72);
            AddEllipse(parent, name + " shallow", center + Vector3.up * 0.025f, radiusX * 0.77f, radiusZ * 0.76f, shallow, owned, 72);

            AddRipple(parent, center + Vector3.up * 0.055f, radiusX * 0.64f, radiusZ * 0.38f, 198f + ripplePhase * 13f, 332f + ripplePhase * 9f, 0.085f, shine, owned);
            AddRipple(parent, center + Vector3.up * 0.06f, radiusX * 0.42f, radiusZ * 0.25f, 18f + ripplePhase * 17f, 142f + ripplePhase * 11f, 0.055f, shine, owned);
            AddRipple(parent, center + Vector3.up * 0.065f, radiusX * 0.25f, radiusZ * 0.14f, 205f + ripplePhase * 7f, 318f + ripplePhase * 5f, 0.045f, shine, owned);
        }

        private static void AddEllipse(Transform parent, string name, Vector3 center, float radiusX, float radiusZ,
                                       Material material, List<UnityEngine.Object> owned, int segments)
        {
            Vector3[] vertices = new Vector3[segments + 1];
            Vector2[] uv = new Vector2[segments + 1];
            int[] triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * radiusX, 0f, Mathf.Sin(a) * radiusZ);
                uv[i + 1] = new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f);
                int next = ((i + 1) % segments) + 1;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = next;
            }
            CreateMeshObject(parent, name, center, vertices, uv, triangles, material, owned);
        }

        private static void AddStream(Transform parent, string name, Vector3[] path, float[] widths, float yOffset,
                                      Material material, List<UnityEngine.Object> owned)
        {
            int count = path.Length;
            Vector3[] vertices = new Vector3[count * 2];
            Vector2[] uv = new Vector2[count * 2];
            int[] triangles = new int[(count - 1) * 6];
            for (int i = 0; i < count; i++)
            {
                Vector3 tangent;
                if (i == 0) tangent = path[1] - path[0];
                else if (i == count - 1) tangent = path[count - 1] - path[count - 2];
                else tangent = path[i + 1] - path[i - 1];
                tangent.y = 0f;
                tangent.Normalize();
                Vector3 right = new Vector3(tangent.z, 0f, -tangent.x);
                Vector3 center = path[i] + Vector3.up * yOffset;
                vertices[i * 2] = center - right * widths[i] * 0.5f;
                vertices[i * 2 + 1] = center + right * widths[i] * 0.5f;
                uv[i * 2] = new Vector2(0f, i / (float)(count - 1));
                uv[i * 2 + 1] = new Vector2(1f, i / (float)(count - 1));
            }
            for (int i = 0; i < count - 1; i++)
            {
                int v = i * 2;
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }
            CreateMeshObject(parent, name, Vector3.zero, vertices, uv, triangles, material, owned);
        }

        private static void AddChannelHighlights(Transform parent, Vector3[] path, float[] widths,
                                                 Material shine, List<UnityEngine.Object> owned)
        {
            for (int band = 0; band < 4; band++)
            {
                Vector3[] line = new Vector3[path.Length];
                float[] thin = new float[path.Length];
                for (int i = 0; i < path.Length; i++)
                {
                    Vector3 tangent = i == path.Length - 1 ? path[i] - path[i - 1] : path[Math.Min(i + 1, path.Length - 1)] - path[Math.Max(i - 1, 0)];
                    tangent.y = 0f;
                    tangent.Normalize();
                    Vector3 right = new Vector3(tangent.z, 0f, -tangent.x);
                    float offset = Mathf.Lerp(-0.23f, 0.23f, band / 3f) * widths[i];
                    line[i] = path[i] + right * offset + Vector3.up * (0.05f + band * 0.004f);
                    thin[i] = 0.075f + 0.02f * ((i + band) % 2);
                }
                AddStream(parent, "Channel painted glint", line, thin, 0f, shine, owned);
            }
        }

        private static void AddFall(Transform parent, string name, Vector3 top, Vector3 bottom, float width,
                                    Material deep, Material fall, Material foam, Material shine,
                                    List<UnityEngine.Object> owned, int seed)
        {
            Vector3 forward = new Vector3(0f, 0f, -1f);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            int rows = 7;
            Vector3[] vertices = new Vector3[rows * 2];
            Vector2[] uv = new Vector2[rows * 2];
            int[] triangles = new int[(rows - 1) * 6];
            for (int r = 0; r < rows; r++)
            {
                float t = r / (float)(rows - 1);
                Vector3 center = Vector3.Lerp(top, bottom, t);
                center.z -= Mathf.Sin(t * Mathf.PI) * 0.28f;
                float w = width * (1f - 0.10f * Mathf.Sin(t * Mathf.PI));
                vertices[r * 2] = center - right * w * 0.5f;
                vertices[r * 2 + 1] = center + right * w * 0.5f;
                uv[r * 2] = new Vector2(0f, t);
                uv[r * 2 + 1] = new Vector2(1f, t);
            }
            for (int r = 0; r < rows - 1; r++)
            {
                int v = r * 2;
                int t = r * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }
            CreateMeshObject(parent, name + " depth", Vector3.back * 0.06f, vertices, uv, triangles, deep, owned);
            CreateMeshObject(parent, name, Vector3.zero, vertices, uv, triangles, fall, owned);

            for (int s = 0; s < 5; s++)
            {
                float u = Mathf.Lerp(-0.36f, 0.36f, s / 4f);
                float wobble = (Hash01(seed + s * 17) - 0.5f) * width * 0.08f;
                Vector3 streakTop = top + right * (u * width + wobble) + Vector3.back * 0.035f;
                Vector3 streakBottom = bottom + right * (u * width * 0.82f - wobble * 0.35f) + Vector3.back * 0.035f;
                AddFallStreak(parent, name + " white streak", streakTop, streakBottom,
                              width * (0.045f + Hash01(seed + s * 23) * 0.025f), s % 2 == 0 ? foam : shine, owned);
            }

            AddFallStreak(parent, name + " lip foam", top - right * width * 0.46f, top + right * width * 0.46f,
                          0.09f, foam, owned);
        }

        private static void AddFallStreak(Transform parent, string name, Vector3 start, Vector3 end, float width,
                                          Material material, List<UnityEngine.Object> owned)
        {
            Vector3 direction = (end - start).normalized;
            Vector3 cameraFacing = new Vector3(0f, 0f, -1f);
            Vector3 right = Vector3.Cross(direction, cameraFacing).normalized;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            Vector3[] vertices =
            {
                start - right * width * 0.5f,
                start + right * width * 0.5f,
                end - right * width * 0.5f,
                end + right * width * 0.5f,
            };
            Vector2[] uv = { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            int[] triangles = { 0, 1, 2, 1, 3, 2 };
            CreateMeshObject(parent, name, Vector3.zero, vertices, uv, triangles, material, owned);
        }

        private static void AddFoamArc(Transform parent, Vector3 center, float radiusX, float radiusZ,
                                       float startDegrees, float endDegrees, float width,
                                       Material material, List<UnityEngine.Object> owned)
        {
            AddRipple(parent, center + Vector3.up * 0.035f, radiusX, radiusZ, startDegrees, endDegrees, width, material, owned);
        }

        private static void AddRipple(Transform parent, Vector3 center, float radiusX, float radiusZ,
                                      float startDegrees, float endDegrees, float width,
                                      Material material, List<UnityEngine.Object> owned)
        {
            const int segments = 28;
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            Vector2[] uv = new Vector2[(segments + 1) * 2];
            int[] triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float a = Mathf.Lerp(startDegrees, endDegrees, t) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(a) * radiusX, 0f, Mathf.Sin(a) * radiusZ);
                Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)).normalized;
                vertices[i * 2] = p - outward * width * 0.5f;
                vertices[i * 2 + 1] = p + outward * width * 0.5f;
                uv[i * 2] = new Vector2(0f, t);
                uv[i * 2 + 1] = new Vector2(1f, t);
            }
            for (int i = 0; i < segments; i++)
            {
                int v = i * 2;
                int tr = i * 6;
                triangles[tr] = v;
                triangles[tr + 1] = v + 2;
                triangles[tr + 2] = v + 1;
                triangles[tr + 3] = v + 1;
                triangles[tr + 4] = v + 2;
                triangles[tr + 5] = v + 3;
            }
            CreateMeshObject(parent, "Painted water ripple", center, vertices, uv, triangles, material, owned);
        }

        private static void AddFoamFlecks(Transform parent, Vector3 center, float radiusX, float radiusZ,
                                          int count, Material foam, List<UnityEngine.Object> owned, int seed)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Hash01(seed + i * 17) * Mathf.PI * 2f;
                float r = Mathf.Sqrt(Hash01(seed + i * 31 + 7));
                Vector3 position = center + new Vector3(Mathf.Cos(a) * radiusX * r, 0.055f + (i % 3) * 0.006f, Mathf.Sin(a) * radiusZ * r);
                float sx = 0.08f + Hash01(seed + i * 43) * 0.13f;
                float sz = 0.035f + Hash01(seed + i * 59) * 0.055f;
                AddEllipse(parent, "Foam fleck", position, sx, sz, foam, owned, 12);
            }
        }

        private static void CreateMeshObject(Transform parent, string name, Vector3 position,
                                             Vector3[] vertices, Vector2[] uv, int[] triangles,
                                             Material material, List<UnityEngine.Object> owned)
        {
            Mesh mesh = new Mesh { name = name + " Mesh" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            owned.Add(mesh);

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static float Hash01(int n)
        {
            unchecked
            {
                uint x = (uint)n;
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;
                return (x & 0x00ffffff) / 16777215f;
            }
        }
    }
}
