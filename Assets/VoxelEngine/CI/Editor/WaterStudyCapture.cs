using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.CI
{
    public static class WaterStudyCapture
    {
        private const int Width = 1024;
        private const int Height = 1536;

        // 512x768 one-bit silhouette extracted from the approved water reference.
        // Keeping shape data separate from shading lets the shader animate/interpolate
        // without destroying the authored waterfall and pool silhouette.
        private const string MaskBase64 = "iVBORw0KGgoAAAANSUhEUgAAAgAAAAMAAQAAAAB6dOLjAAANzElEQVR42u1dz48cRxX+enrYmYSNZ0IsxQnrdANBREiIReRgwM4MKIrCyTnlykogwYGD+XHwwWTaSSR8QNjHSAnK/glIXDiA3ElMbKFEXkU5REjgdhySPUTZXnsj99o9/XGYX909PdNV9SyZRPVddmZ3+5tXr169X13VA1hYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhowZm+apgRrAgJGuuhjOC+tlADZxmKCL62Q9kQ3o0hUqLT9nFZMoIWyY5EgkMAuhICH4CfjN80DYcR0lwCt59fDY4BQZq/0kCCDABA2Wqc0AgIUnOCkS9IzAlSAAhjc0t2SXK6HBsQwoCgKSUQS9AGkME3J1gvXGZA0JcGJZIkI2MJiDszC5t3ahr1HYqTAQAbQgmyu2mJUoKVuy5B+85IkJoTJHddBwAY7G2b236HTNyb5kmW65GJS0FUOE8mnUyWICVeKpyFw0cXRAlFnNmDOUEbALYEhtMhKTUk3l1T7gLOOckneimZSiTwU2Fs9OLOR4lEgm0k+yKCtA0EojEkLenMH1AsCBSztIWeKxJaYqN7UjZQN02FqmIqXEzdPaEErTsawBq5uX8uU15N7rSoaJzM5Zn/CFRla05TAedkrmz9p/Lg2pemL2cFzjtGLm1tWmuvNXFOmWC94nc/bWtIEH4nyNXrBl45ODCfWQWgb7A2kU9IwtdUJTiz1a/IzR31IewnFWPf05nGfT8Xj+4MHJL8UFxxBebrlD6wXSj8dBd6fLZ/SKKD5wYhBmo5XuV/Hc4GF+Ep9R4eYlUA7KU8e/pKgkP1OlhZ4KTQ7aOZX1yLlbgoBH/TbwQKi2nhODcABxeatRLssalW+S8iGJ6+5SyujJ9X0MErTiOcU2ag4ZG2EQD4glNXqS/0SEM8+ZBKcb9kGo8AaCeCCNkLgc5OwYxG7ROGTyiRboUA7xcm29erfx0pEaxvLIpPignYICiv64kOIiVtOhz/CAslX74JtXwIdMb/MG8Lh/Sm9s25C5uqBC20C+mWrz2NwTayCu+oXPK0X4ghMqQuHADPOKYEvv/i+wF2/UeCJW2UJQRHngGa64iDjfJfVtUInvwj+vhxHDWWlkiLU5zWCzj2/U8a98IxbKN3eDF2SCaDqGTJr6rpoIs2iG0EpRGcKdrT0kSzg20MX57WT+2xFfXVCGKAWDuUxv6B8uyFijpgBpcROtMFPeAO4xappoOkH2dAEhUStJfQKaWdS/wBuu5Ha2n4i3x4OgIEqkOA55Lnr72aetMhkCSvKTdhrmZA/98RYjyR//WfAuXFxJHed7Gbu2Y/gHonbvAGw16KwcWxIYxmgJl6uh8e7rt7eePsh02dZvqgFcGJi8aRulTvBTpnI3SKWRpT9yGNZmInaWVz4crJdEJ+Ot/YoIZbv45mnbzLCdht1wXmmqotScrmCQC9r3rBLPmty9pfn+/ubP7lwb8pB9fKf3jloHatlF/BJNNpHafoqoNSvplq3qzrFTNeH5p33Dp8davkLsNIM9pEpeDa08pe14GnCyVxoJn+bpWSG3c41GysFUMB3FKqpzALWaEKdC/zfKylxK2SH/P1e3ssD2GoN43+rXILYpK3qNrBzV8qlIDLwlyhP+nm7t4rShAvdByKBLv9woLc0y/E3t9+pqo3p07w5UI5/rtAm+BCGhZLeANjyrmka9qLoYTfsLS8dOHOCMwaxsM+Jp1Rw47zpWnaaUggvV2o6xOCKi2qT+PqpfkPa5GbGks5rCJ4W1UHnag6N374kqIAP2FFZtoidxRvunU99ioJxuli3RC+8d+t/QqCNlTvJP38xo3XsspyZuwV6yR4aW9v5VZF8qVuie3ovQoJ9pXtkLzWy6r/MAqXNRI4wPvVs9VXCy0O+Ul1eu+pKbEBdNNFdamiKTvL820Vf9AQEDRl7LOpMEZr7n7CzBBSmU/sjEN0DUF7STISq0vgL8l+BbcyQxlBV9Ujc0Ek9yiMTBuK5TdJMq5applMggP1pvy9ZT04N96sleCp5aPs10ngBIujp3tBIbqPugTVSvToTDejGCkxwn9Quzlg1LpglQQOyUGtBF1nmRMa1st+YBAt0oFLxjvxZo0E13NZRSuYffgoJl+GU2vLPUz3pOa3+rsR3CS3vhaP5Bjg31v+5X3RITcoGMFiv70HoNyo6MT82RqyRDHFe/oHvfEQgnxeEeEs1Q5ADMiSDjokGWxc4ZpmqhvmEhNunPf3PxDkys6D7+AM9EYw1cEoUN2MlGNjWlnmJm1VguzTkg5GVxZ22SyP/TcwDEr5/jaKfeXlQ1jDi/GkAwcAu8HwJQBr6i59N2h5+fA+CjSJegpyYCPeyY8g1spQ9gDcv7ppnuKspsD1FwpNVK1ylSS5My0MppY0VLi/sAq0ggBI8faymnFJVE/RmplyWk66am/WOetuUt3CT8/5Kkpc6Re05QbTTKn97DaKxVYlQXqChZ3Vp6Yr6tGHgU/r0/wW37rxBPgqSXIwXs4Og1bYIfnXQX1YI/kHd3TtyAMA3i63WkmH3D3zUK01dMi42SN7U48Sw9tiNkiOk9HcxvEKgrPE6oBZZ3z5DWZgxoy3SUbBPbVDWO2kGJC8b0xwm6kzUQdv3lDY98fzV8m8BKnDHIZBbZPGy1oxXz8+viDzUienUoXmA7OsRTKdaNEtSsC36hZTP/s1ALwyef8WgPdyf1+vbzRFJPny9CPT1hsFEU7XjuEqyZtfnNmB9wbJs8wZRo1DvUbyOsBspIbolEtybNwqBL2xrjsc7XrI0hZJduYJGksCsg/sPrY5rvzaQXWV0VicTL4GAP/6ePT2gxMBEM7mf0thCON10SPJ/X0WEdRKEIx73/eMfNSKZhbhsZcUotkI09eZRoZyFRVrp/aOoe9NT0DC4TxO1X7u89NNA6PUbFgkULlnOc0i3EH58zPqnaAoXDxeVIlWmue0DVobhb7XeBvQR2Ty1GAuPKpUuTPHcKGnTzBJOjOS/LAkgU6q6wDA7X2jcyPTGJVGI8PQPRQ2Dch7fmmvqyLBrXyeN/RVauccTuY2UUTjdNfZ1FFA6hZrF1fzdGHm/r24QjLMboGqEPwIXy8tDoB9LSU+PHV0cx5F0yp2z2lkmyh4lEJcr49MxVCbTrNWGBE4bLNMEOlYInFz/Gpyn5S625xHW3A+Mr67xVfI3QE1R16SIOhVE4hPVKnNAsmYQgka2LwOwyc3uCR5k8c5MDwcMzblAUnB9VmP+1U9bgUdrF8CwGMRVgwnoUeS177UIc9n5rMQvfMoosjsXORRkkz37uEVLzGTIACA9u29+2G2mA6TzI7+Fk7kRUYSNAA4bh/sXzW1o6uL7nKorubLuWjsrukTeN7N2TocvKs/Cx/4h3KJ/mP6BMNj3WHByRtgNZctZSamnNudGxuthY0750V76SkZQSs9KCMoT4L2iDLpqUMcgoWFhYWFhYWFhYWFhQqET02BQ2ER15BWgU0JQQNAW0LgBCieXdSuIGM37lFCkHr7lMzCquuvSPagOmp7thfjz1Ij7nGfFOjA6xu3B0eYdOxNJVjx8bhIgMnJY9lqrN2HsFyCBB2ZBJewB4XjiQtAPvYtUPBIzNFOWY/mq3GQjjRhTOAlAFyBEmPZSRrgUwDIpE+1lTg0nBjNpjnBupSgKyXACcAZSAhOyewAeCPEmmQWnMuJS/IuxgUITRnSrfdyCSYxWnBtP20IJWhKhxBJCXx8Nh+L+39EEFodfB4I+p8XHQjOrPEzrwPHmrKcoHHXDekB6RAOSocgzVAc6SxIn3o/zfRdc4I75BONEUhr575QiU5LOgttoVd2U6EETYz3nkqmceWz7dJSKcGqNLhSKIH4O6oaUoK2kCA8KPtOFOfIbPOtGcHOjMARLcW7VngSj68IJfhYlieOWwenjXf8OeMzEJKGJBFuw/jZOs54D7xjKoE7LvsYGM5Cwy+UjvpoeeNV4Il2745eCEw5MLfE7uRFYkbgBtK44EsJ+rnwYkKg9fjD2iAvKX07phKMr/G7CEQSdHBapoNu1OhLCFJpM645sijRND5muGPUJZMBSe4MzZZzNj1d1DCMC5Sa8oY0np6nLD/Ine3hQEpgNAtdqT8Qfgsojl6mqRJH4nq9PIEjMyNNHTSk9YIToPxFLZpROSpbgaYS3SMbcwR6dvBst2RGu7qeJAIG+SP9Qz0JMjxwAEAW5uZAi4AnV38FhZPCi+GRASVKjKWF514oXIjoMBQNAcBXRj/OxaYSTA7MSjcw3JamOO3IjGC2EOLU1JImSjhupIOZ7hvf1h9CocRyfqgfEqM5b6I3hDVpEyYquKP3DabxQF6JVwCAjt6h8bwW/dGbfsNwEnA4AHBL2xL9ovLaeFODIAS8rXKUvK2XXXtk3qmngqVA8goTYbZusJxjKcGuX3irOYRVFL/3QNuUHwjKCVqsRxCPx20cXDrxaB53Cg8C0FFieyaFWWxcqXrIlPFynrRQtOygL80Tpxj2TQiu49ikH+2GC9KN5fgu32zln0nye4MO2MUcwWhGdLL14jc3YL/tUEsHnTkbXon0ZuERlBfz2lEtAn/Ott2+cbVHMkaHlPjEGF0g0CDoSqPzXLQGhlqx0Z8fhF4rrD+3mE+0tJ73XU5uQpDcUZfAVS0AVM2AEQZaSVZ3/hd6Lm3ekKW7wpr6LqkK/wOEMSXMy7lnqQAAAABJRU5ErkJggg==";

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

                maskTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
                {
                    name = "Authored Water Silhouette",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                if (!ImageConversion.LoadImage(maskTexture, Convert.FromBase64String(MaskBase64), false))
                    throw new InvalidOperationException("Could not decode the authored water silhouette.");
                maskTexture.wrapMode = TextureWrapMode.Clamp;
                maskTexture.filterMode = FilterMode.Bilinear;

                material = new Material(shader) { name = "AAA Stylized Water Material" };
                material.SetTexture("_ReferenceTex", maskTexture);
                material.SetColor("_DeepColor", new Color(0.025f, 0.33f, 0.55f, 1f));
                material.SetColor("_MidColor", new Color(0.025f, 0.68f, 0.88f, 1f));
                material.SetColor("_ShallowColor", new Color(0.31f, 0.89f, 0.98f, 1f));
                material.SetColor("_FoamColor", new Color(0.95f, 0.995f, 1f, 1f));
                material.SetFloat("_FlowSpeed", 0.28f);
                material.SetFloat("_FlowStrength", 0.0035f);
                material.SetFloat("_Shimmer", 0.28f);
                material.SetFloat("_EdgeFoam", 0.58f);
                material.SetFloat("_Alpha", 1f);

                mesh = BuildFullFrameQuad();
                quad = new GameObject("Reference Locked Stylized Water");
                quad.AddComponent<MeshFilter>().sharedMesh = mesh;
                quad.AddComponent<MeshRenderer>().sharedMaterial = material;

                cameraObject = new GameObject("Water Study Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.allowHDR = false;
                camera.allowMSAA = true;
                camera.orthographic = true;
                camera.orthographicSize = 11.25f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 50f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.transform.rotation = Quaternion.identity;

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Water Study Render",
                    antiAliasing = 4
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
                    "target=Sunlit Cleric extracted water silhouette\n" +
                    "shape=reference locked 512x768 authored mask\n" +
                    "shader=dual flow noise; stepped cyan depth bands; vertical waterfall streaks; shimmer; broken edge foam\n" +
                    "background=transparent\n" +
                    $"size={Width}x{Height}\n");
                Debug.Log("Reference-locked stylized water written to " + outputDirectory);
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
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (quad != null) UnityEngine.Object.DestroyImmediate(quad);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                if (maskTexture != null) UnityEngine.Object.DestroyImmediate(maskTexture);
            }

            EditorApplication.Exit(0);
        }

        private static Mesh BuildFullFrameQuad()
        {
            const float halfHeight = 11.25f;
            const float halfWidth = 7.5f;
            Mesh mesh = new Mesh { name = "Full Frame Water Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, -halfHeight, 0f),
                new Vector3( halfWidth, -halfHeight, 0f),
                new Vector3( halfWidth,  halfHeight, 0f),
                new Vector3(-halfWidth,  halfHeight, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
