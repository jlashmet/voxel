using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Stylized water lookdev capture using layered procedural flow, stepped color bands,
    /// broken foam, directional waterfall streaks, and irregular hand-authored silhouettes.
    /// </summary>
    public static class WaterStudyCapture
    {
        private const int Width = 1024;
        private const int Height = 1536;
        private const int Segments = 72;

        private sealed class Mats
        {
            public Material Pool;
            public Material Fall;
            public Material Foam;
            public Material Shine;
            public Material Deep;
        }

        public static void Run()
        {
            string rootPath = Directory.GetParent(Application.dataPath).FullName;
            string outDir = Path.Combine(rootPath, "Artifacts", "Water");
            Directory.CreateDirectory(outDir);

            GameObject root = null;
            GameObject cameraGo = null;
            RenderTexture rt = null;
            Texture2D tex = null;
            var owned = new List<UnityEngine.Object>();

            try
            {
                Mats mats = BuildMaterials(owned);
                root = new GameObject("AAA Stylized Water Study");
                BuildComposition(root.transform, mats, owned);

                cameraGo = new GameObject("Water Camera");
                Camera cam = cameraGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.orthographic = true;
                cam.orthographicSize = 11.25f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 50f;
                cam.allowHDR = false;
                cam.allowMSAA = true;
                cam.transform.position = new Vector3(0.3f, -0.2f, -20f);
                cam.transform.rotation = Quaternion.identity;

                rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Stylized Water Capture",
                    antiAliasing = 4
                };
                rt.Create();
                cam.targetTexture = rt;

                Shader.WarmupAllShaders();
                RenderTexture previous = RenderTexture.active;
                try
                {
                    cam.Render();
                    RenderTexture.active = rt;
                    tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                    tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    tex.Apply(false, false);
                    File.WriteAllBytes(Path.Combine(outDir, "water-study.png"), tex.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    cam.targetTexture = null;
                }

                File.WriteAllText(Path.Combine(outDir, "water-study.txt"),
                    "target=Sunlit Cleric stylized waterfall water\n" +
                    "techniques=dual flow noise; stepped depth bands; broken foam; directional fall streaks; sparkle ribbons\n" +
                    "background=transparent\n" +
                    "composition=irregular terraced pools and waterfall ribbons\n" +
                    $"size={Width}x{Height}\n");

                Debug.Log("AAA stylized water capture written to " + outDir);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
                return;
            }
            finally
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                if (rt != null)
                {
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }
                if (cameraGo != null) UnityEngine.Object.DestroyImmediate(cameraGo);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                foreach (var o in owned)
                    if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }

            EditorApplication.Exit(0);
        }

        private static Mats BuildMaterials(List<UnityEngine.Object> owned)
        {
            Shader shader = Shader.Find("Hidden/VoxelEngine/StylizedWaterLookdev");
            if (shader == null)
                throw new InvalidOperationException("StylizedWaterLookdev shader was not found.");

            Mats m = new Mats();
            m.Pool = Make(shader, "Pool Water", owned, 0.00f, 0.00f, 10.5f, 0.28f, 0.08f, 1.00f);
            m.Fall = Make(shader, "Waterfall Flow", owned, 1.00f, 0.08f, 15.0f, 0.56f, 1.77f, 0.98f);
            m.Foam = Make(shader, "Broken Foam", owned, 0.25f, 1.00f, 11.0f, 0.38f, 2.63f, 0.95f);
            m.Shine = Make(shader, "Water Shine", owned, 0.00f, 0.74f, 17.0f, 0.18f, 4.20f, 0.78f);
            m.Deep = Make(shader, "Deep Underlay", owned, 0.00f, 0.00f, 7.0f, 0.12f, 5.40f, 0.82f);
            m.Deep.SetColor("_DeepColor", new Color(0.015f, 0.24f, 0.44f, 1f));
            m.Deep.SetColor("_MidColor", new Color(0.02f, 0.47f, 0.67f, 1f));
            m.Deep.SetColor("_ShallowColor", new Color(0.10f, 0.66f, 0.80f, 1f));
            return m;
        }

        private static Material Make(Shader shader, string name, List<UnityEngine.Object> owned,
                                     float flowMode, float foam, float scale, float speed, float phase, float alpha)
        {
            Material mat = new Material(shader) { name = name };
            mat.SetColor("_DeepColor", new Color(0.015f, 0.35f, 0.59f, 1f));
            mat.SetColor("_MidColor", new Color(0.025f, 0.69f, 0.88f, 1f));
            mat.SetColor("_ShallowColor", new Color(0.36f, 0.90f, 0.98f, 1f));
            mat.SetColor("_FoamColor", new Color(0.94f, 0.99f, 1.00f, 1f));
            mat.SetFloat("_FlowMode", flowMode);
            mat.SetFloat("_FoamAmount", foam);
            mat.SetFloat("_WaveScale", scale);
            mat.SetFloat("_FlowSpeed", speed);
            mat.SetFloat("_Phase", phase);
            mat.SetFloat("_Alpha", alpha);
            owned.Add(mat);
            return mat;
        }

        private static void BuildComposition(Transform root, Mats m, List<UnityEngine.Object> owned)
        {
            AddPool(root, "Upper Spring", new Vector2(3.85f, 7.65f), 2.25f, 1.18f, 0.8f, m, owned);
            AddPool(root, "Upper Terrace", new Vector2(2.55f, 5.00f), 3.05f, 1.52f, 1.9f, m, owned);
            AddPool(root, "Middle Terrace", new Vector2(0.75f, 2.15f), 3.75f, 1.85f, 2.8f, m, owned);
            AddPool(root, "Lower Basin", new Vector2(-1.25f, -1.70f), 4.75f, 2.25f, 4.1f, m, owned);

            AddFall(root, "Top Fall", new Vector2(3.85f, 7.10f), new Vector2(2.92f, 5.70f), new Vector2(2.62f, 5.45f), 1.35f, 0.78f, m, owned, 0.7f);
            AddFall(root, "Upper Fall", new Vector2(2.50f, 4.45f), new Vector2(1.35f, 3.15f), new Vector2(0.92f, 2.88f), 1.80f, 0.96f, m, owned, 1.4f);
            AddFall(root, "Middle Fall", new Vector2(0.52f, 1.40f), new Vector2(-0.85f, 0.00f), new Vector2(-1.25f, -0.38f), 2.15f, 1.10f, m, owned, 2.2f);
            AddFall(root, "Side Fall", new Vector2(2.15f, -1.85f), new Vector2(2.72f, -3.35f), new Vector2(2.95f, -4.00f), 1.18f, 0.64f, m, owned, 3.3f);

            Vector2[] stream =
            {
                new Vector2(-2.05f, -2.85f),
                new Vector2(-1.10f, -3.72f),
                new Vector2(0.25f, -4.62f),
                new Vector2(1.48f, -5.82f),
                new Vector2(2.20f, -7.05f),
                new Vector2(3.28f, -8.18f)
            };
            float[] streamWidths = { 4.1f, 4.6f, 5.0f, 5.25f, 5.55f, 5.85f };
            AddRibbon(root, "Deep Stream", stream, Scale(streamWidths, 1.08f), -0.18f, m.Deep, owned, 0f);
            AddRibbon(root, "Stream", stream, streamWidths, -0.10f, m.Pool, owned, 0f);
            AddRibbon(root, "Stream Foam Left", Offset(stream, -1.78f), Scale(streamWidths, 0.10f), -0.02f, m.Foam, owned, 0.75f);
            AddRibbon(root, "Stream Foam Right", Offset(stream, 1.78f), Scale(streamWidths, 0.10f), -0.02f, m.Foam, owned, 1.25f);

            AddArc(root, "Lower Highlight A", new Vector2(-1.35f, -1.42f), 3.5f, 1.15f, 205f, 337f, 0.12f, m.Shine, owned, 0.15f);
            AddArc(root, "Lower Highlight B", new Vector2(-0.55f, -2.15f), 2.45f, 0.72f, 12f, 145f, 0.09f, m.Shine, owned, 0.55f);
            AddArc(root, "Middle Highlight", new Vector2(0.85f, 2.28f), 2.55f, 0.72f, 205f, 335f, 0.10f, m.Shine, owned, 1.1f);
            AddArc(root, "Upper Highlight", new Vector2(2.62f, 5.04f), 1.95f, 0.55f, 206f, 332f, 0.08f, m.Shine, owned, 1.7f);
        }

        private static void AddPool(Transform parent, string name, Vector2 center, float rx, float ry, float seed,
                                    Mats m, List<UnityEngine.Object> owned)
        {
            Vector2[] outer = IrregularLoop(center, rx, ry, seed, Segments);
            Vector2[] deep = ScaleLoop(outer, center, 1.055f);
            AddPolygon(parent, name + " Deep", deep, -0.20f, m.Deep, owned);
            AddPolygon(parent, name, outer, -0.12f, m.Pool, owned);

            Vector2[] foamOuter = ScaleLoop(outer, center, 1.018f);
            Vector2[] foamInner = ScaleLoop(outer, center, 0.925f);
            AddRing(parent, name + " Broken Foam", foamOuter, foamInner, -0.04f, m.Foam, owned, seed);

            AddArc(parent, name + " Shine 1", center + new Vector2(-rx * 0.08f, ry * 0.05f), rx * 0.72f, ry * 0.36f, 198f, 330f, Mathf.Max(0.055f, ry * 0.055f), m.Shine, owned, seed);
            AddArc(parent, name + " Shine 2", center + new Vector2(rx * 0.12f, -ry * 0.12f), rx * 0.48f, ry * 0.24f, 18f, 142f, Mathf.Max(0.045f, ry * 0.045f), m.Shine, owned, seed + 2.1f);
        }

        private static void AddFall(Transform parent, string name, Vector2 start, Vector2 control, Vector2 end,
                                    float startWidth, float endWidth, Mats m, List<UnityEngine.Object> owned, float phase)
        {
            Vector2[] path = Bezier(start, control, end, 18);
            float[] foamWidths = WidthRamp(path.Length, startWidth * 1.18f, endWidth * 1.25f);
            float[] waterWidths = WidthRamp(path.Length, startWidth, endWidth);
            AddRibbon(parent, name + " Foam Edge", path, foamWidths, 0.02f, m.Foam, owned, phase);
            AddRibbon(parent, name, path, waterWidths, 0.05f, m.Fall, owned, phase + 0.33f);

            AddArc(parent, name + " Lip", start + new Vector2(0f, 0.05f), startWidth * 0.72f, 0.23f, 192f, 350f, 0.10f, m.Foam, owned, phase + 0.8f);
            AddArc(parent, name + " Splash", end + new Vector2(0f, -0.02f), endWidth * 1.65f, 0.40f, 188f, 352f, 0.14f, m.Foam, owned, phase + 1.4f);
        }

        private static Vector2[] IrregularLoop(Vector2 center, float rx, float ry, float seed, int count)
        {
            Vector2[] loop = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                float r = 1f
                    + 0.075f * Mathf.Sin(a * 3f + seed * 1.71f)
                    + 0.048f * Mathf.Sin(a * 7f + seed * 2.37f)
                    + 0.022f * Mathf.Sin(a * 13f + seed * 0.83f);
                loop[i] = center + new Vector2(Mathf.Cos(a) * rx * r, Mathf.Sin(a) * ry * r);
            }
            return loop;
        }

        private static Vector2[] ScaleLoop(Vector2[] src, Vector2 center, float scale)
        {
            Vector2[] dst = new Vector2[src.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = center + (src[i] - center) * scale;
            return dst;
        }

        private static void AddPolygon(Transform parent, string name, Vector2[] loop, float z, Material mat, List<UnityEngine.Object> owned)
        {
            Vector2 center = Vector2.zero;
            for (int i = 0; i < loop.Length; i++) center += loop[i];
            center /= loop.Length;

            Vector3[] v = new Vector3[loop.Length + 1];
            Vector2[] uv = new Vector2[v.Length];
            Color[] colors = new Color[v.Length];
            int[] tris = new int[loop.Length * 3];
            v[0] = new Vector3(center.x, center.y, z);
            uv[0] = new Vector2(0.5f, 0.5f);
            colors[0] = Color.white;
            for (int i = 0; i < loop.Length; i++)
            {
                Vector2 p = loop[i];
                v[i + 1] = new Vector3(p.x, p.y, z);
                Vector2 d = p - center;
                uv[i + 1] = new Vector2(0.5f + d.x * 0.08f, 0.5f + d.y * 0.08f);
                colors[i + 1] = Color.white;
                int next = ((i + 1) % loop.Length) + 1;
                tris[i * 3] = 0;
                tris[i * 3 + 1] = next;
                tris[i * 3 + 2] = i + 1;
            }
            CreateMesh(parent, name, v, uv, colors, tris, mat, owned);
        }

        private static void AddRing(Transform parent, string name, Vector2[] outer, Vector2[] inner, float z,
                                    Material mat, List<UnityEngine.Object> owned, float phase)
        {
            int n = outer.Length;
            Vector3[] v = new Vector3[n * 2];
            Vector2[] uv = new Vector2[n * 2];
            Color[] c = new Color[n * 2];
            int[] tris = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                v[i * 2] = new Vector3(outer[i].x, outer[i].y, z);
                v[i * 2 + 1] = new Vector3(inner[i].x, inner[i].y, z);
                uv[i * 2] = new Vector2(u * 4f + phase, 1f);
                uv[i * 2 + 1] = new Vector2(u * 4f + phase, 0f);
                c[i * 2] = new Color(1f, 1f, 1f, 0.94f);
                c[i * 2 + 1] = new Color(0.72f, 1f, 1f, 0.82f);
                int j = (i + 1) % n;
                int o = i * 6;
                tris[o] = i * 2;
                tris[o + 1] = j * 2;
                tris[o + 2] = i * 2 + 1;
                tris[o + 3] = j * 2;
                tris[o + 4] = j * 2 + 1;
                tris[o + 5] = i * 2 + 1;
            }
            CreateMesh(parent, name, v, uv, c, tris, mat, owned);
        }

        private static void AddRibbon(Transform parent, string name, Vector2[] path, float[] widths, float z,
                                      Material mat, List<UnityEngine.Object> owned, float phase)
        {
            int n = path.Length;
            Vector3[] v = new Vector3[n * 2];
            Vector2[] uv = new Vector2[n * 2];
            Color[] c = new Color[n * 2];
            int[] tris = new int[(n - 1) * 6];
            for (int i = 0; i < n; i++)
            {
                Vector2 tangent = i == 0 ? path[1] - path[0] : (i == n - 1 ? path[n - 1] - path[n - 2] : path[i + 1] - path[i - 1]);
                tangent.Normalize();
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                float half = widths[i] * 0.5f;
                Vector2 left = path[i] - normal * half;
                Vector2 right = path[i] + normal * half;
                v[i * 2] = new Vector3(left.x, left.y, z);
                v[i * 2 + 1] = new Vector3(right.x, right.y, z);
                float vv = i / (float)(n - 1);
                uv[i * 2] = new Vector2(phase, vv * 3.4f);
                uv[i * 2 + 1] = new Vector2(1f + phase, vv * 3.4f);
                c[i * 2] = new Color(0.95f, 1f, 1f, 0.94f);
                c[i * 2 + 1] = new Color(0.95f, 1f, 1f, 0.94f);
                if (i < n - 1)
                {
                    int o = i * 6;
                    tris[o] = i * 2;
                    tris[o + 1] = (i + 1) * 2;
                    tris[o + 2] = i * 2 + 1;
                    tris[o + 3] = (i + 1) * 2;
                    tris[o + 4] = (i + 1) * 2 + 1;
                    tris[o + 5] = i * 2 + 1;
                }
            }
            CreateMesh(parent, name, v, uv, c, tris, mat, owned);
        }

        private static void AddArc(Transform parent, string name, Vector2 center, float rx, float ry,
                                   float startDeg, float endDeg, float thickness, Material mat,
                                   List<UnityEngine.Object> owned, float phase)
        {
            const int n = 28;
            Vector3[] v = new Vector3[n * 2];
            Vector2[] uv = new Vector2[n * 2];
            Color[] c = new Color[n * 2];
            int[] tris = new int[(n - 1) * 6];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float a = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
                Vector2 p = center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                Vector2 d = new Vector2(Mathf.Cos(a) / Mathf.Max(rx, 0.001f), Mathf.Sin(a) / Mathf.Max(ry, 0.001f)).normalized;
                v[i * 2] = new Vector3(p.x + d.x * thickness, p.y + d.y * thickness, 0.12f);
                v[i * 2 + 1] = new Vector3(p.x - d.x * thickness, p.y - d.y * thickness, 0.12f);
                uv[i * 2] = new Vector2(t * 3f + phase, 1f);
                uv[i * 2 + 1] = new Vector2(t * 3f + phase, 0f);
                c[i * 2] = new Color(1f, 1f, 1f, 0.92f);
                c[i * 2 + 1] = new Color(0.72f, 1f, 1f, 0.54f);
                if (i < n - 1)
                {
                    int o = i * 6;
                    tris[o] = i * 2;
                    tris[o + 1] = (i + 1) * 2;
                    tris[o + 2] = i * 2 + 1;
                    tris[o + 3] = (i + 1) * 2;
                    tris[o + 4] = (i + 1) * 2 + 1;
                    tris[o + 5] = i * 2 + 1;
                }
            }
            CreateMesh(parent, name, v, uv, c, tris, mat, owned);
        }

        private static Vector2[] Bezier(Vector2 a, Vector2 b, Vector2 c, int count)
        {
            Vector2[] p = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float u = 1f - t;
                p[i] = u * u * a + 2f * u * t * b + t * t * c;
            }
            return p;
        }

        private static float[] WidthRamp(int count, float a, float b)
        {
            float[] widths = new float[count];
            for (int i = 0; i < count; i++) widths[i] = Mathf.Lerp(a, b, i / (float)(count - 1));
            return widths;
        }

        private static float[] Scale(float[] src, float factor)
        {
            float[] dst = new float[src.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = src[i] * factor;
            return dst;
        }

        private static Vector2[] Offset(Vector2[] src, float x)
        {
            Vector2[] dst = new Vector2[src.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = src[i] + new Vector2(x, 0f);
            return dst;
        }

        private static void CreateMesh(Transform parent, string name, Vector3[] vertices, Vector2[] uv, Color[] colors,
                                       int[] triangles, Material mat, List<UnityEngine.Object> owned)
        {
            Mesh mesh = new Mesh { name = name + " Mesh" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            owned.Add(mesh);

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = mat;
        }
    }
}
