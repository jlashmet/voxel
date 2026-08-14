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
        private const int SourceMaskWidth = 256;
        private const int SourceMaskHeight = 384;

        // Binary alpha silhouette sampled from the approved water reference, row-major from top-left.
        // RLE alternates transparent/opaque runs beginning with transparent.
        private const string ReferenceMaskRle = "201,3,247,3,1,6,242,1,1,12,238,1,2,15,236,20,2,1,232,20,2,3,228,23,1,5,227,33,220,2,1,34,219,37,219,37,219,36,219,36,220,34,223,33,222,34,221,36,219,37,217,39,217,39,217,38,218,37,219,35,220,36,220,36,219,37,219,37,219,36,220,36,220,36,215,2,2,20,5,10,217,24,4,9,218,25,3,9,219,24,4,3,2,3,220,22,6,1,5,1,220,22,234,21,235,25,230,27,229,8,4,15,231,7,5,5,2,5,233,6,5,5,4,3,236,2,5,4,96,3,153,3,96,5,3,3,245,12,245,11,248,6,78,2,61,3,1,2,105,2,16,4,54,2,2,4,59,13,117,6,53,6,59,9,1,5,116,7,51,7,56,3,1,9,1,5,115,6,53,7,51,18,1,5,113,10,1,2,48,8,49,18,1,6,113,14,46,9,49,18,1,6,114,13,46,8,49,27,116,4,1,6,45,8,49,27,118,2,2,4,46,9,48,10,1,16,1,2,173,3,50,31,226,32,219,4,2,30,89,6,121,17,2,2,7,10,88,11,118,17,2,4,4,11,9,2,78,11,117,18,1,7,1,13,8,2,78,10,118,42,4,5,78,10,2,2,113,42,4,5,81,11,5,3,21,1,4,2,76,43,4,6,82,10,4,3,19,3,3,3,76,43,5,5,85,15,18,10,74,44,5,5,86,14,18,9,74,45,5,5,93,6,18,6,1,3,74,46,4,5,94,6,18,2,14,1,66,46,5,4,92,8,32,6,63,46,6,3,90,8,34,9,60,46,99,2,39,9,2,3,55,49,114,3,7,2,12,9,1,4,54,50,113,4,4,5,12,9,1,3,55,50,113,4,3,5,2,1,10,9,1,2,7,1,48,49,106,2,3,7,3,4,2,3,9,13,2,6,46,52,98,4,1,14,1,5,2,6,1,2,4,3,4,5,1,7,46,52,95,14,1,7,2,4,1,17,4,12,44,2,1,52,94,19,7,23,2,8,2,2,44,54,94,22,6,8,1,23,3,3,42,33,2,19,95,21,6,16,4,1,1,16,41,34,3,18,95,21,10,11,5,1,5,12,43,31,3,19,96,22,2,2,2,12,5,3,4,12,1,2,42,29,3,18,97,2,2,31,1,12,4,12,1,2,41,48,104,4,5,21,3,12,2,16,41,32,3,13,114,20,5,10,6,15,38,28,8,11,116,7,2,10,5,9,8,7,2,5,38,3,7,18,6,13,116,6,1,6,14,5,8,7,3,4,53,15,5,13,120,2,1,9,4,9,80,13,5,2,1,3,1,6,124,7,5,8,84,6,16,6,125,6,5,8,85,5,16,6,136,8,85,5,16,7,1,4,1,2,2,2,126,5,89,1,17,22,122,5,35,2,69,23,156,2,4,3,68,23,8,2,140,16,74,17,6,3,138,10,1,7,76,24,136,12,1,8,77,22,137,20,76,23,140,20,73,23,143,17,73,24,145,14,1,1,71,27,145,8,2,5,69,28,144,15,69,28,144,17,67,28,149,14,63,7,1,22,149,3,1,3,2,5,63,6,3,21,153,3,2,3,66,6,2,22,229,28,232,24,236,17,241,15,127,2,2,1,109,16,121,8,1,3,107,17,116,15,108,21,110,17,108,16,1,7,97,5,2,19,108,29,90,1,1,6,1,19,109,29,89,9,1,10,1,8,109,29,89,14,1,14,111,26,89,15,1,14,117,20,89,15,1,14,120,18,87,31,121,17,87,31,121,17,87,31,121,17,86,33,120,17,86,34,119,14,89,34,119,12,91,35,118,11,91,36,117,11,92,36,116,12,91,37,114,13,92,38,108,18,62,8,22,38,106,19,62,9,22,38,95,3,2,1,5,19,61,8,24,38,91,12,3,19,61,7,24,39,90,35,59,10,23,39,89,36,59,12,21,39,1,1,71,1,15,36,60,1,3,7,20,44,55,2,10,5,12,33,67,6,12,2,5,47,54,6,5,7,1,3,9,22,77,4,10,1,3,3,1,1,2,47,48,29,7,6,1,16,78,1,10,60,48,29,5,1,3,20,90,60,48,29,2,19,3,2,87,65,49,46,96,66,48,41,101,21,1,44,48,43,99,34,1,30,49,43,99,30,5,2,1,23,57,39,99,32,3,4,2,18,60,38,99,32,1,26,61,37,98,59,63,36,71,8,10,5,4,59,63,24,1,11,68,17,3,6,5,58,62,37,67,18,5,2,7,61,34,1,3,1,19,6,1,3,2,27,66,17,4,4,7,64,28,8,9,3,6,26,3,10,67,25,5,72,14,7,1,9,1,1,6,7,4,25,2,10,66,21,3,2,2,81,6,10,1,12,1,9,2,21,14,5,66,4,1,12,5,23,1,63,5,57,1,18,66,2,1,14,3,24,3,48,1,12,6,76,1,2,65,8,1,30,1,4,1,61,6,80,1,1,62,12,1,5,4,25,2,48,2,7,6,82,65,13,2,25,1,62,4,87,3,4,58,9,3,25,1,61,3,7,2,87,60,34,11,13,1,47,3,26,1,60,60,30,19,12,1,42,3,89,60,26,30,46,2,91,61,23,37,4,1,130,62,20,39,1,4,70,1,58,62,11,1,9,46,125,69,1,1,4,1,9,51,121,68,1,8,7,52,120,138,79,2,42,133,6,10,63,1,43,134,3,13,62,1,2,2,39,151,61,1,43,152,60,4,4,7,30,151,56,23,26,133,4,12,57,28,25,130,13,2,57,31,23,130,13,3,56,32,21,131,71,34,18,133,70,35,16,136,66,38,16,139,8,1,52,40,15,141,58,42,15,141,27,1,29,199,25,1,1,4,26,199,27,3,26,199,28,1,29,197,62,188,68,186,40,1,29,186,31,1,8,2,28,186,39,2,27,188,3,2,28,1,5,3,2,1,12,2,8,189,39,7,1,4,3,5,7,190,38,14,2,9,3,193,32,50,2,1,4,170,5,1,20,49,11,173,7,2,10,53,17,169,3,4,2,2,5,54,17,181,3,55,17,181,3,55,10,1,6,242,8,2,3,244,7,4,1,245,5,9039,4,250,7,248,8,225,2,20,9,225,4,9,3,5,10,225,7,3,8,1,12,221,1,1,33,217,1,1,37,216,40,211,45,207,2,1,46,206,50,206,50,207,18,10,21,207,15,15,19,208,12,20,16,210,9,23,14,212,7,24,13,212,6,25,13,215,3,26,12,216,2,26,12,217,1,3,3,20,12,221,6,15,14,222,8,8,1,1,16,219,37,220,36,220,36,220,36,221,35,221,35,221,35,221,35,221,35,221,35,222,34,222,34,222,34,222,34,222,34,221,35,221,35,221,35,221,35,220,36,220,36,220,36,220,36,219,37,219,37,217,1,1,37,216,40,214,42,213,43,213,1,1,41,215,41,209,2,4,41,208,3,1,44,208,3,1,44,207,49,206,50,206,50,207,49,206,50,203,53,202,54,201,55,190,3,1,1,3,58,190,66,190,66,189,67,191,65,190,66,190,36,8,22,190,34,12,20,189,34,15,18,189,2,2,29,17,17,188,33,19,16,188,34,19,15,184,2,2,34,20,14,184,2,1,35,20,14,184,38,20,14,184,38,20,14,184,39,6,4,9,14,184,39,1,3,2,5,3,3,2,14,184,50,3,3,2,14,184,50,2,5,1,14,184,50,1,21,183,73,184,72,185,71,184,72,182,74,182,74,180,76,143,1,31,81,142,2,30,82,142,2,28,84,142,4,25,85,142,7,2,2,17,86,143,11,15,87,143,11,1,1,3,3,6,88,143,20,5,88,145,18,5,88,147,16,4,89,149,2,1,12,3,89,153,103,153,103,153,103,152,104,152,104,131,4,18,103,129,6,18,103,127,9,16,104,126,13,13,59,4,33,1,7,126,14,13,91,7,5,127,13,8,1,1,92,10,4,128,11,7,92,16,1,137,3,7,91,165,91,166,90,148,3,20,84,149,5,19,82,150,7,16,83,150,8,15,83,150,10,15,80,151,11,14,80,151,13,12,80,153,15,2,1,5,79,157,1,2,15,2,72,4,3,152,4,4,88,12,3,145,96,11,7,3,3,136,95,11,14,136,95,11,16,134,95,11,17,133,94,12,17,132,94,11,20,129,97,10,23,124,99,9,24,123,101,9,23,122,103,9,22,122,104,9,21";

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
                material.SetFloat("_EdgeFoam", 0.48f);
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
                    "mask=authored reference silhouette reconstructed from compact RLE alpha\n" +
                    "shader=painterly cyan bands, sparse highlights, directional falls and broken foam\n" +
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
            bool[] source = DecodeReferenceMask();
            var texture = new Texture2D(MaskWidth, MaskHeight, TextureFormat.RGBA32, false, true)
            {
                name = "Authored Reference Water Silhouette",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[MaskWidth * MaskHeight];
            for (int y = 0; y < MaskHeight; y++)
            {
                int sourceYFromBottom = y * SourceMaskHeight / MaskHeight;
                int sourceY = SourceMaskHeight - 1 - sourceYFromBottom;
                for (int x = 0; x < MaskWidth; x++)
                {
                    int sourceX = x * SourceMaskWidth / MaskWidth;
                    bool on = source[sourceY * SourceMaskWidth + sourceX];
                    byte a = on ? (byte)255 : (byte)0;
                    pixels[y * MaskWidth + x] = new Color32(a, a, a, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static bool[] DecodeReferenceMask()
        {
            bool[] result = new bool[SourceMaskWidth * SourceMaskHeight];
            string[] runs = ReferenceMaskRle.Split(',');
            int cursor = 0;
            bool on = false;

            for (int i = 0; i < runs.Length; i++)
            {
                if (!int.TryParse(runs[i], out int count))
                    throw new InvalidOperationException("Invalid authored water silhouette RLE.");

                if (cursor + count > result.Length)
                    throw new InvalidOperationException("Authored water silhouette RLE exceeds expected size.");

                if (on)
                {
                    int end = cursor + count;
                    for (int p = cursor; p < end; p++) result[p] = true;
                }

                cursor += count;
                on = !on;
            }

            if (cursor != result.Length)
                throw new InvalidOperationException($"Authored water silhouette RLE size mismatch: {cursor} != {result.Length}.");

            return result;
        }

        private static Mesh BuildQuadMesh()
        {
            var mesh = new Mesh { name = "Water Lookdev Quad Mesh" };
            mesh.vertices = new[]
            {
                new Vector3(-2f / 3f, -1f, 0f),
                new Vector3( 2f / 3f, -1f, 0f),
                new Vector3(-2f / 3f,  1f, 0f),
                new Vector3( 2f / 3f,  1f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
