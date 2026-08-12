using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Rendering.SurfaceExtraction;
using VoxelEngine.Showcase;
using VoxelEngine.Structures;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Minimal quality gate for reusable dressed voxel masonry. The arch is intentionally absent:
    /// a single stone language must look production-ready before larger components are rebuilt from it.
    /// </summary>
    public static class SunlitStonePrimitiveCapture
    {
        private const int Width = 1280;
        private const int Height = 960;
        private static readonly int3 RegionCoord = new(1, 0, 0);

        public static void Run()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outDir = Path.Combine(projectRoot, "Artifacts", "WorldArtKit");
            Directory.CreateDirectory(outDir);

            ShowcaseWorld world = null;
            VoxelHeroSurfaceRenderer surface = null;
            GameObject cameraObject = null;
            GameObject keyObject = null;
            GameObject fillObject = null;
            RenderTexture target = null;
            Texture2D capture = null;
            var owned = new List<UnityEngine.Object>();

            try
            {
                const uint seed = 0xB10Cu;
                world = new ShowcaseWorld(seed, 64_000, 1, 2);
                world.GenerateRegionBlocking(RegionCoord);

                int cx = RegionCoord.x * ShowcaseWorld.RegionVoxelEdge + ShowcaseWorld.RegionVoxelEdge / 2;
                int cz = ShowcaseWorld.RegionVoxelEdge / 2;
                int baseY = world.SurfaceHeight(cx, cz) + 7;

                var brush = new VoxelBrush(world.Table, world.Pool, 1_000_000);
                brush.FillBulk(new int3(cx - 36, baseY - 6, cz - 24), new int3(72, 32, 48), Mat.Empty);

                // Quiet voxel plinth so the stones are judged against a stable horizontal reference.
                brush.Box(new int3(cx - 28, baseY - 3, cz - 12), new int3(56, 3, 24), Mat.DarkStone);

                WorldArtVoxelStoneSpec hero = WorldArtVoxelStoneSpec.DressedBlock(
                    new int3(cx - 8, baseY, cz - 6), new int3(16, 8, 12), Mat.Stone, seed + 1u, 1);
                WorldArtVoxelStonePrimitives.DressedBlock(ref brush, in hero);

                WorldArtVoxelStoneSpec left = WorldArtVoxelStoneSpec.DressedBlock(
                    new int3(cx - 25, baseY, cz - 3), new int3(13, 7, 10), Mat.Stone, seed + 2u, 2);
                WorldArtVoxelStonePrimitives.DressedBlock(ref brush, in left);

                WorldArtVoxelStoneSpec right = WorldArtVoxelStoneSpec.DressedBlock(
                    new int3(cx + 13, baseY, cz - 2), new int3(12, 6, 9), Mat.Stone, seed + 3u, 1);
                WorldArtVoxelStonePrimitives.DressedBlock(ref brush, in right);

                if (brush.BudgetExceeded)
                    throw new InvalidOperationException("Dressed stone primitive study exceeded VoxelBrush budget.");

                var profiles = new VoxelSurfaceProfileSet()
                    .Set(Mat.Stone, new VoxelSurfaceProfile(
                        smoothing: 0.84f,
                        blurPasses: 0,
                        densityBias: -0.002f,
                        planarization: 0.56f,
                        planarizationThreshold: 0.60f,
                        distanceRecovery: 0.78f,
                        curveRecovery: 0.14f,
                        normalPlanarization: 0.68f,
                        planarSnapDistanceVoxels: 0.13f,
                        featurePreservation: 0.78f,
                        featureNormalStrength: 0.62f,
                        featureCurvatureThreshold: 0.075f))
                    .Set(Mat.DarkStone, VoxelSurfaceProfile.HardManufactured);

                world.DirtyRegions.Add(RegionCoord);
                surface = new VoxelHeroSurfaceRenderer(
                    new int3(cx - 34, baseY - 5, cz - 18), new int3(68, 28, 36), profiles)
                {
                    CastShadows = true
                };
                for (int i = 0; i < 8; i++)
                {
                    surface.Sync(world, 400.0);
                    if (surface.PendingRebuilds == 0) break;
                }
                if (surface.RegionMeshCount == 0 || surface.VertexCount == 0)
                    throw new InvalidOperationException("Dressed stone primitive study produced no surface geometry.");

                ApplyMaterial(surface.Root, owned);
                SetupLighting(out keyObject, out fillObject);
                SetupCamera(cx, baseY, cz, out cameraObject, out Camera camera);

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Dressed Voxel Stone Primitive Review",
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

                string metadata =
                    "capture=reusable dressed voxel stone primitive quality gate\n" +
                    "geometry=VoxelBrush -> bounded voxel extraction\n" +
                    "unityPresentationMeshes=0\n" +
                    "voxelSizeMetres=0.10\n" +
                    "heroSurfaceSampleMetres=0.05\n" +
                    "stones=3\n" +
                    $"voxelWrites={brush.VoxelsWritten}\n" +
                    $"surfaceVertices={surface.VertexCount}\n";
                File.WriteAllText(Path.Combine(outDir, "sunlit-cleric.txt"), metadata);
                Debug.Log($"CI dressed stone primitive study written to {outDir}\n{metadata}");
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
                foreach (UnityEngine.Object o in owned)
                    if (o != null) UnityEngine.Object.DestroyImmediate(o);
                surface?.Dispose();
                world?.Dispose();
            }
        }

        private static void ApplyMaterial(GameObject root, List<UnityEngine.Object> owned)
        {
            Shader shader = Shader.Find("VoxelEngine/SunlitSmooth");
            if (shader == null) throw new InvalidOperationException("SunlitSmooth shader not found.");
            Material stone = new Material(shader) { name = "Dressed limestone primitive" };
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Stylized/stone_color.png");
            if (texture != null) stone.SetTexture("_MainTex", texture);
            SetColor(stone, "_BaseColor", new Color(0.68f, 0.63f, 0.53f));
            SetColor(stone, "_SecondaryColor", new Color(0.54f, 0.49f, 0.41f));
            SetColor(stone, "_TopColor", new Color(0.79f, 0.74f, 0.64f));
            SetFloat(stone, "_SurfaceKind", 2f);
            SetFloat(stone, "_TextureScale", 0.62f);
            SetFloat(stone, "_TextureStrength", 0.13f);
            SetFloat(stone, "_DetailScale", 0.86f);
            SetFloat(stone, "_DetailStrength", 0.022f);
            SetFloat(stone, "_TopStrength", 0.10f);
            SetFloat(stone, "_RimStrength", 0.004f);
            SetFloat(stone, "_Smoothness", 0.008f);
            SetFloat(stone, "_StoneReliefStrength", 0.20f);
            SetFloat(stone, "_StoneJointRelief", 0f);
            SetFloat(stone, "_StoneBlockVariation", 0.10f);
            SetFloat(stone, "_StoneWeathering", 0.16f);
            SetFloat(stone, "_StoneFacePlanarization", 0.80f);
            SetFloat(stone, "_ArchSeams", 0f);
            owned.Add(stone);

            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                renderer.sharedMaterial = stone;
        }

        private static void SetupLighting(out GameObject keyObject, out GameObject fillObject)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.27f, 0.30f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.18f, 0.18f, 0.18f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.06f, 0.055f);
            RenderSettings.ambientIntensity = 0.46f;
            RenderSettings.fog = false;

            keyObject = new GameObject("Dressed stone warm key");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1.0f, 0.88f, 0.70f);
            key.intensity = 1.45f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.78f;
            keyObject.transform.rotation = Quaternion.Euler(36f, -42f, 0f);

            fillObject = new GameObject("Dressed stone cool fill");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.58f, 0.68f, 0.84f);
            fill.intensity = 0.20f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(25f, 138f, 0f);
        }

        private static void SetupCamera(int cx, int baseY, int cz, out GameObject cameraObject, out Camera camera)
        {
            float s = VoxelSurfaceRenderer.VoxelSize;
            Vector3 focus = new Vector3(cx * s, (baseY + 4) * s, cz * s);
            cameraObject = new GameObject("Dressed Stone Primitive Camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.14f, 0.16f, 0.19f, 1f);
            camera.fieldOfView = 29f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            cameraObject.transform.position = focus + new Vector3(4.8f, 3.2f, -9.4f);
            cameraObject.transform.LookAt(focus + new Vector3(0f, 0.05f, 0f));
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
