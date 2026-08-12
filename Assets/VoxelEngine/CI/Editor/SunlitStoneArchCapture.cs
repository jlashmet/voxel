using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Structures;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Deterministic hero-asset review for the reusable masonry arch bay. The camera is deliberately
    /// close: if joints, bevels, bond, silhouette or damage look procedural here, the asset fails.
    /// </summary>
    public static class SunlitStoneArchCapture
    {
        private const int Width = 1120;
        private const int Height = 1376;

        public static void Run()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outDir = Path.Combine(projectRoot, "Artifacts", "WorldArtKit");
            Directory.CreateDirectory(outDir);

            GameObject root = null;
            GameObject cameraObject = null;
            GameObject keyObject = null;
            GameObject fillObject = null;
            RenderTexture target = null;
            Texture2D capture = null;

            try
            {
                Shader shader = Shader.Find("VoxelEngine/SunlitSmooth");
                if (shader == null) throw new InvalidOperationException("SunlitSmooth shader not found.");

                WorldArtPalette palette = BuildPalette(shader);
                root = new GameObject("AAA Arch Bay Hero Study");

                WorldArtPiece hero = WorldArtArchBay.Build(root.transform,
                    "AAA reusable intact arch bay", Vector3.zero,
                    1.62f, 3.58f, 1.10f, 0.48f, 0.58f, 0.94f,
                    0xA341, palette, WorldArtArchDamage.Intact);

                // A small amount of restrained dressing tests sockets without hiding masonry.
                WorldArtKit.MossCluster(root.transform, "Hero study crown moss",
                    hero.Socket("crown").position + new Vector3(-0.54f, 0.04f, -0.50f),
                    0.24f, 0x719, palette.Get(WorldArtSurfaceRole.Moss));

                BuildGround(root.transform, shader);
                SetupLighting(out keyObject, out fillObject);
                SetupCamera(out cameraObject, out Camera camera, hero);

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "AAA Arch Bay Review",
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
                    File.WriteAllBytes(Path.Combine(outDir, "sunlit-cleric.png"), capture.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    camera.targetTexture = null;
                }

                File.WriteAllText(Path.Combine(outDir, "sunlit-cleric.txt"),
                    "capture=AAA reusable arch bay hero study\n" +
                    "qualityBar=close-up hero asset\n" +
                    "damage=Intact\n" +
                    "seed=0xA341\n");
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
                if (keyObject != null) UnityEngine.Object.DestroyImmediate(keyObject);
                if (fillObject != null) UnityEngine.Object.DestroyImmediate(fillObject);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static WorldArtPalette BuildPalette(Shader shader)
        {
            return new WorldArtPalette()
                .Set(WorldArtSurfaceRole.Stone, Stone(shader))
                .Set(WorldArtSurfaceRole.Moss, Moss(shader));
        }

        private static Material Stone(Shader shader)
        {
            Material m = new Material(shader) { name = "Hero study limestone" };
            SetColor(m, "_BaseColor", new Color(0.78f, 0.72f, 0.60f));
            SetColor(m, "_SecondaryColor", new Color(0.61f, 0.55f, 0.45f));
            SetColor(m, "_TopColor", new Color(0.92f, 0.87f, 0.75f));
            SetFloat(m, "_SurfaceKind", 2f);
            SetFloat(m, "_TextureScale", 0.24f);
            SetFloat(m, "_TextureStrength", 0.35f);
            SetFloat(m, "_DetailScale", 0.055f);
            SetFloat(m, "_DetailStrength", 0.08f);
            SetFloat(m, "_TopStrength", 0.26f);
            SetFloat(m, "_RimStrength", 0.025f);
            SetFloat(m, "_Smoothness", 0.035f);
            return m;
        }

        private static Material Moss(Shader shader)
        {
            Material m = new Material(shader) { name = "Hero study moss" };
            SetColor(m, "_BaseColor", new Color(0.25f, 0.40f, 0.10f));
            SetColor(m, "_SecondaryColor", new Color(0.16f, 0.28f, 0.07f));
            SetColor(m, "_TopColor", new Color(0.42f, 0.55f, 0.16f));
            SetFloat(m, "_SurfaceKind", 1f);
            SetFloat(m, "_TextureStrength", 0.20f);
            SetFloat(m, "_TopStrength", 0.34f);
            SetFloat(m, "_Smoothness", 0.015f);
            return m;
        }

        private static void BuildGround(Transform parent, Shader shader)
        {
            Material ground = new Material(shader) { name = "Neutral stone-study ground" };
            SetColor(ground, "_BaseColor", new Color(0.26f, 0.27f, 0.24f));
            SetColor(ground, "_SecondaryColor", new Color(0.21f, 0.22f, 0.20f));
            SetColor(ground, "_TopColor", new Color(0.31f, 0.32f, 0.28f));
            SetFloat(ground, "_SurfaceKind", 0f);
            SetFloat(ground, "_TextureStrength", 0.10f);
            SetFloat(ground, "_Smoothness", 0.02f);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Hero study ground";
            floor.transform.SetParent(parent, false);
            floor.transform.position = new Vector3(0f, -0.42f, 0.45f);
            floor.transform.localScale = new Vector3(12f, 0.55f, 8f);
            Collider c = floor.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.DestroyImmediate(c);
            floor.GetComponent<MeshRenderer>().sharedMaterial = ground;
        }

        private static void SetupLighting(out GameObject keyObject, out GameObject fillObject)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.35f, 0.39f, 0.45f);
            RenderSettings.ambientEquatorColor = new Color(0.27f, 0.28f, 0.27f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.12f, 0.11f);
            RenderSettings.ambientIntensity = 0.62f;
            RenderSettings.fog = false;

            keyObject = new GameObject("Arch study warm key");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1.0f, 0.88f, 0.69f);
            key.intensity = 1.18f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.72f;
            keyObject.transform.rotation = Quaternion.Euler(36f, -42f, 0f);

            fillObject = new GameObject("Arch study cool fill");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.62f, 0.73f, 0.88f);
            fill.intensity = 0.34f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(24f, 128f, 0f);
        }

        private static void SetupCamera(out GameObject cameraObject, out Camera camera, WorldArtPiece hero)
        {
            cameraObject = new GameObject("AAA Arch Study Camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.20f, 0.23f, 1f);
            camera.fieldOfView = 32f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            Vector3 focus = hero.Socket("opening").position + new Vector3(0f, 0.56f, 0f);
            cameraObject.transform.position = new Vector3(5.9f, 4.45f, -10.6f);
            cameraObject.transform.LookAt(focus);
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }
    }
}
