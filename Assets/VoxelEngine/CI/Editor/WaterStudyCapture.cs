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
            string root = Directory.GetParent(Application.dataPath).FullName;
            string output = Path.Combine(root, "Artifacts", "Water");
            Directory.CreateDirectory(output);

            GameObject quad = null, cameraObject = null;
            RenderTexture target = null;
            Texture2D capture = null, mask = null;
            Material material = null;
            Mesh mesh = null;

            try
            {
                Shader shader = Shader.Find("Hidden/VoxelEngine/StylizedWaterLookdev");
                if (shader == null) throw new InvalidOperationException("StylizedWaterLookdev shader was not found.");

                mask = BuildMask();
                material = new Material(shader) { name = "Stylized Water Lookdev" };
                material.SetTexture("_ReferenceTex", mask);
                material.SetColor("_DeepColor", new Color(0.045f, 0.32f, 0.55f, 1f));
                material.SetColor("_MidColor", new Color(0.08f, 0.59f, 0.79f, 1f));
                material.SetColor("_ShallowColor", new Color(0.29f, 0.80f, 0.91f, 1f));
                material.SetColor("_FoamColor", new Color(0.90f, 0.97f, 0.98f, 1f));
                material.SetFloat("_FlowSpeed", 0.20f);
                material.SetFloat("_FlowStrength", 0.006f);
                material.SetFloat("_Shimmer", 0.22f);
                material.SetFloat("_EdgeFoam", 0.36f);
                material.SetFloat("_Alpha", 1f);

                quad = new GameObject("Water Lookdev Quad");
                mesh = BuildQuadMesh();
                quad.AddComponent<MeshFilter>().sharedMesh = mesh;
                quad.AddComponent<MeshRenderer>().sharedMaterial = material;

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

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
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
                    File.WriteAllBytes(Path.Combine(output, "water-study.png"), capture.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    camera.targetTexture = null;
                }

                File.WriteAllText(Path.Combine(output, "water-study.txt"),
                    "target=Sunlit Cleric waterfall water\n" +
                    "mask=fragmented asymmetric pools, narrow falls, negative-space holes\n" +
                    "shader=painterly cyan bands, sparse pool highlights, directional waterfall ribs\n" +
                    $"size={Width}x{Height}\n");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
                return;
            }
            finally
            {
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (quad != null) UnityEngine.Object.DestroyImmediate(quad);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                if (mask != null) UnityEngine.Object.DestroyImmediate(mask);
            }
            EditorApplication.Exit(0);
        }

        private static Texture2D BuildMask()
        {
            var texture = new Texture2D(MaskWidth, MaskHeight, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            var pixels = new Color32[MaskWidth * MaskHeight];

            for (int y = 0; y < MaskHeight; y++)
            {
                float v = (y + 0.5f) / MaskHeight;
                for (int x = 0; x < MaskWidth; x++)
                {
                    float u = (x + 0.5f) / MaskWidth;
                    float m = 0f;

                    // Upper-right waterfall chain.
                    m = Mathf.Max(m, Pool(u,v,0.76f,0.94f,0.105f,0.052f,-0.18f));
                    m = Mathf.Max(m, Fall(u,v,0.73f,0.91f,0.69f,0.84f,0.040f));
                    m = Mathf.Max(m, Pool(u,v,0.65f,0.82f,0.105f,0.030f,-0.02f));
                    m = Mathf.Max(m, Fall(u,v,0.64f,0.80f,0.61f,0.72f,0.060f));
                    m = Mathf.Max(m, Pool(u,v,0.61f,0.70f,0.125f,0.040f,-0.06f));
                    m = Mathf.Max(m, Pool(u,v,0.79f,0.66f,0.115f,0.038f,-0.18f));
                    m = Mathf.Max(m, Fall(u,v,0.81f,0.65f,0.83f,0.59f,0.038f));
                    m = Mathf.Max(m, Pool(u,v,0.86f,0.57f,0.125f,0.036f,-0.08f));

                    // Mid-left waterfall and main fragmented pool.
                    m = Mathf.Max(m, Fall(u,v,0.28f,0.67f,0.28f,0.58f,0.066f));
                    m = Mathf.Max(m, Pool(u,v,0.24f,0.55f,0.195f,0.060f,0.04f));
                    m = Mathf.Max(m, Pool(u,v,0.48f,0.51f,0.255f,0.075f,-0.02f));
                    m = Mathf.Max(m, Pool(u,v,0.69f,0.53f,0.165f,0.052f,0.05f));
                    m = Mathf.Max(m, Fall(u,v,0.72f,0.54f,0.75f,0.48f,0.042f));

                    // Tiny detached fragments seen in the reference.
                    m = Mathf.Max(m, Pool(u,v,0.10f,0.79f,0.035f,0.012f,0.1f));
                    m = Mathf.Max(m, Pool(u,v,0.18f,0.75f,0.050f,0.015f,-0.1f));
                    m = Mathf.Max(m, Pool(u,v,0.25f,0.70f,0.060f,0.020f,0.15f));
                    m = Mathf.Max(m, Pool(u,v,0.40f,0.65f,0.052f,0.015f,-0.1f));

                    // Lower-right foreground water, kept contained rather than filling the frame.
                    m = Mathf.Max(m, Pool(u,v,0.86f,0.23f,0.235f,0.115f,-0.24f));
                    m = Mathf.Max(m, Pool(u,v,0.82f,0.10f,0.205f,0.110f,-0.30f));
                    m = Mathf.Max(m, Fall(u,v,0.86f,0.35f,0.83f,0.28f,0.048f));

                    // Negative-space holes and chunks.
                    m *= 1f - 0.99f * Ellipse(u,v,0.79f,0.27f,0.060f,0.040f,-0.10f);
                    m *= 1f - 0.99f * Ellipse(u,v,0.86f,0.13f,0.050f,0.042f,0.12f);
                    m *= 1f - 0.98f * Ellipse(u,v,0.42f,0.51f,0.060f,0.028f,0.04f);
                    m *= 1f - 0.95f * Ellipse(u,v,0.60f,0.52f,0.046f,0.023f,-0.08f);

                    float coarse = Mathf.PerlinNoise(u*31f+4.2f,v*35f+8.1f);
                    float fine = Mathf.PerlinNoise(u*67f+11.7f,v*61f+2.9f);
                    m += (coarse-0.5f)*0.13f + (fine-0.5f)*0.045f;
                    float hard = m > 0.24f ? 1f : 0f;
                    if (hard > 0f && coarse < 0.075f && fine < 0.15f) hard = 0f;

                    byte a = hard > 0f ? (byte)255 : (byte)0;
                    pixels[y*MaskWidth+x] = new Color32(a,a,a,a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false,false);
            return texture;
        }

        private static float Pool(float u,float v,float cx,float cy,float rx,float ry,float rot)
        {
            float s = Ellipse(u,v,cx,cy,rx,ry,rot);
            float n = Mathf.PerlinNoise(u*41f+cx*17f,v*47f+cy*13f);
            return Mathf.Clamp01(s+(n-0.5f)*0.13f);
        }

        private static float Fall(float u,float v,float ax,float ay,float bx,float by,float w)
        {
            float s = Capsule(u,v,ax,ay,bx,by,w);
            float n = Mathf.PerlinNoise(u*57f+2.3f,v*21f+4.8f);
            return Mathf.Clamp01(s+(n-0.5f)*0.08f);
        }

        private static float SmoothInside(float d,float inner,float outer)
        {
            float t = Mathf.Clamp01((d-inner)/Mathf.Max(0.0001f,outer-inner));
            t = t*t*(3f-2f*t);
            return 1f-t;
        }

        private static float Ellipse(float u,float v,float cx,float cy,float rx,float ry,float rot)
        {
            float c=Mathf.Cos(rot), s=Mathf.Sin(rot), dx=u-cx, dy=v-cy;
            float px=(dx*c-dy*s)/rx, py=(dx*s+dy*c)/ry;
            return SmoothInside(Mathf.Sqrt(px*px+py*py),0.82f,1.02f);
        }

        private static float Capsule(float u,float v,float ax,float ay,float bx,float by,float radius)
        {
            Vector2 p=new Vector2(u,v), a=new Vector2(ax,ay), b=new Vector2(bx,by), ab=b-a;
            float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/Mathf.Max(0.0001f,Vector2.Dot(ab,ab)));
            float d=Vector2.Distance(p,a+ab*t)/radius;
            return SmoothInside(d,0.76f,1.03f);
        }

        private static Mesh BuildQuadMesh()
        {
            var mesh=new Mesh { name="Water Lookdev Quad Mesh" };
            mesh.vertices=new[] { new Vector3(-2f/3f,-1f,0f), new Vector3(2f/3f,-1f,0f), new Vector3(-2f/3f,1f,0f), new Vector3(2f/3f,1f,0f) };
            mesh.uv=new[] { new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1) };
            mesh.triangles=new[] {0,2,1,2,3,1};
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}