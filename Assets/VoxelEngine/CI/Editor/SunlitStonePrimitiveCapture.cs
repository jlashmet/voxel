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
                int baseY = world.SurfaceHeight(cx, cz) + 9;

                var brush = new VoxelBrush(world.Table, world.Pool, 1_000_000);
                brush.FillBulk(new int3(cx - 36, baseY - 8, cz - 24), new int3(72, 34, 48), Mat.Empty);

                // Isolate each test stone in air. Contact with a plinth previously fused occupancy and
                // hid whether the reusable block itself was good enough.
                WorldArtVoxelStoneSpec hero = WorldArtVoxelStoneSpec.DressedBlock(
                    new int3(cx - 8, baseY, cz - 6), new int3(16, 8, 12), Mat.Stone, seed + 1u, 1);
                WorldArtVoxelStonePrimitives.DressedBlock(ref brush, in hero);

                WorldArtVoxelStoneSpec left = WorldArtVoxelStoneSpec.DressedBlock(
                    new int3(cx - 25, baseY + 2, cz - 2), new int3(13, 7, 10), Mat.Stone, seed + 2u, 2);
                WorldArtVoxelStonePrimitives.DressedBlock(ref brush, in left);

                WorldArtVoxelStoneSpec right = WorldArtVoxelStoneSpec.DressedBlock(
                    new int3(cx + 13, baseY + 1, cz - 1), new int3(12, 6, 9), Mat.Stone, seed + 3u, 1);
                WorldArtVoxelStonePrimitives.DressedBlock(ref brush, in right);

                if (brush.BudgetExceeded)
                    throw new InvalidOperationException("Dressed stone primitive study exceeded VoxelBrush budget.");

                var profiles = new VoxelSurfaceProfileSet()
                    .Set(Mat.Stone, new VoxelSurfaceProfile(
                        smoothing: 0.52f,
                        blurPasses: 0,
                        densityBias: 0f,
                        planarization: 0.88f,
                        planarizationThreshold: 0.46f,
                        distanceRecovery: 0.28f,
                        curveRecovery: 0.03f,
                        normalPlanarization: 0.92f,
                        planarSnapDistanceVoxels: 0.10f,
                        featurePreservation: 0.52f,
                        featureNormalStrength: 0.38f,
                        featureCurvatureThreshold: 0.11f));

                world.DirtyRegions.Add(RegionCoord);
                surface = new VoxelHeroSurfaceRenderer(
                    new int3(cx - 34, baseY - 6, cz - 18), new int3(68, 30, 36), profiles)
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
                    "stones=3 isolated\n" +
                    "surface=plane-first low-recovery dressed stone\n" +
                    "unityPresentationMeshes=0\n" +
                    "voxelSizeMetres=0.10\n" +
                    "heroSurfaceSampleMetres=0.05\n" +
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
            SetColor(stone, "_BaseColor", new Color(0.66f, 0.60f, 0.49f));
            SetColor(stone, "_SecondaryColor", new Color(0.50f, 0.45f, 0.37f));
            SetColor(stone, "_TopColor", new Color(0.80f, 0.75f, 0.64f));
            SetFloat(stone, "_SurfaceKind", 2f);
            SetFloat(stone, "_TextureScale", 0.54f);
            SetFloat(stone, "_TextureStrength", 0.22f);
            SetFloat(stone, "_DetailScale", 0.94f);
            SetFloat(stone, "_DetailStrength", 0.030f);
            SetFloat(stone, "_TopStrength", 0.12f);
            SetFloat(stone, "_RimStrength", 0.003f);
            SetFloat(stone, "_Smoothness", 0.006f);
            SetFloat(stone, "_StoneReliefStrength", 0.12f);
            SetFloat(stone, "_StoneJointRelief", 0f);
            SetFloat(stone, "_StoneBlockVariation", 0.08f);
            SetFloat(stone, "_StoneWeathering", 0.12f);
            SetFloat(stone, "_StoneFacePlanarization", 0.94f);
            SetFloat(stone, "_ArchSeams", 0f);
            owned.Add(stone);

            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                renderer.sharedMaterial = stone;
        }

        private static void SetupLighting(out GameObject keyObject, out GameObject fillObject)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.25f, 0.28f, 0.32f);
            RenderSettings.ambientEquatorColor = new Color(0.16f, 0.16f, 0.16f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.045f);
            RenderSettings.ambientIntensity = 0.42f;
            RenderSettings.fog = false;

            keyObject = new GameObject("Dressed stone warm key");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1.0f, 0.86f, 0.68f);
            key.intensity = 1.55f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.80f;
            keyObject.transform.rotation = Quaternion.Euler(38f, -44f, 0f);

            fillObject = new GameObject("Dressed stone cool fill");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.56f, 0.67f, 0.84f);
            fill.intensity = 0.17f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(24f, 140f, 0f);
        }

        private static void SetupCamera(int cx, int baseY, int cz, out GameObject cameraObject, out Camera camera)
        {
            float s = VoxelSurfaceRenderer.VoxelSize;
            Vector3 focus = new Vector3(cx * s, (baseY + 4) * s, cz * s);
            cameraObject = new GameObject("Dressed Stone Primitive Camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.13f, 0.15f, 0.18f, 1f);
            camera.fieldOfView = 27f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            cameraObject.transform.position = focus + new Vector3(4.5f, 3.0f, -8.7f);
            cameraObject.transform.LookAt(focus + new Vector3(0f, 0.08f, 0f));
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
