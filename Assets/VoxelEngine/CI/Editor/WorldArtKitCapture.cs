using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Vegetation;
using VoxelEngine.Rendering.Vegetation;
using VoxelEngine.Showcase;
using VoxelEngine.Structures;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Deterministic art-direction capture built from the real voxel brickmap.
    ///
    /// The point of this scene is not to prove world placement. It is a compact visual laboratory
    /// for the vocabulary available to worldgen: organic voxel masses, softened construction
    /// pieces, material coatings, triplanar painterly textures, and smooth procedural vegetation.
    /// </summary>
    public static class WorldArtKitCapture
    {
        private const int Width = 1280;
        private const int Height = 960;
        private static readonly int3 RegionCoord = new(1, 0, 0);

        public static void Run()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "WorldArtKit");
            Directory.CreateDirectory(outputDirectory);

            ShowcaseWorld world = null;
            VoxelSurfaceRenderer surface = null;
            GameObject cameraObject = null;
            GameObject sunObject = null;
            GameObject treeObject = null;
            Mesh treeMesh = null;
            Material treeBark = null;
            Material treeLeaves = null;
            RenderTexture target = null;
            Texture2D capture = null;
            var lookdevMaterials = new List<Material>();

            try
            {
                const uint seed = 0x4D46574Fu; // "MFWO"
                world = new ShowcaseWorld(seed, 32_768, 1, 2);
                world.GenerateRegionBlocking(RegionCoord);

                int cx = RegionCoord.x * ShowcaseWorld.RegionVoxelEdge + ShowcaseWorld.RegionVoxelEdge / 2;
                int cz = ShowcaseWorld.RegionVoxelEdge / 2;
                int terrainY = world.SurfaceHeight(cx, cz);
                int ruinY = BuildStorybookRuin(world, cx, terrainY, cz, out var brush);

                if (brush.BudgetExceeded)
                    throw new InvalidOperationException("World art lookdev exceeded the VoxelBrush write budget.");

                world.DirtyRegions.Add(RegionCoord);
                surface = new VoxelSurfaceRenderer { CastShadows = true };

                for (int i = 0; i < 64; i++)
                {
                    surface.Sync(world, 300.0);
                    if (world.DirtyRegions.Count == 0 && surface.PendingRebuilds == 0) break;
                }

                if (surface.RegionMeshCount == 0 || surface.VertexCount == 0)
                    throw new InvalidOperationException("World art scene produced no voxel surface geometry.");

                ApplyLookdevMaterials(surface.Root, lookdevMaterials);
                CreateTree(cx - 67, ruinY + 1, cz + 31,
                           out treeObject, out treeMesh, out treeBark, out treeLeaves);

                SetupLighting(out sunObject);
                SetupCamera(cx, ruinY, cz, out cameraObject, out var camera);

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "CI World Art Kit Capture",
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
                    File.WriteAllBytes(Path.Combine(outputDirectory, "world-art-ruin.png"), capture.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    camera.targetTexture = null;
                }

                string metadata =
                    $"seed={seed}\n" +
                    $"region={RegionCoord.x},{RegionCoord.y},{RegionCoord.z}\n" +
                    $"terrainY={terrainY}\n" +
                    $"ruinY={ruinY}\n" +
                    $"voxelWrites={brush.VoxelsWritten}\n" +
                    $"bulkVoxelWrites={brush.BulkVoxelsWritten}\n" +
                    $"brickWrites={brush.BricksWritten}\n" +
                    $"surfaceRegions={surface.RegionMeshCount}\n" +
                    $"surfaceFaces={surface.FaceCount}\n" +
                    $"surfaceVertices={surface.VertexCount}\n" +
                    $"treeVertices={treeMesh.vertexCount}\n";
                File.WriteAllText(Path.Combine(outputDirectory, "world-art-ruin.txt"), metadata);
                Debug.Log($"CI world-art capture written to {outputDirectory}\n{metadata}");
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
                if (sunObject != null) UnityEngine.Object.DestroyImmediate(sunObject);
                if (treeObject != null) UnityEngine.Object.DestroyImmediate(treeObject);
                if (treeMesh != null) UnityEngine.Object.DestroyImmediate(treeMesh);
                if (treeBark != null) UnityEngine.Object.DestroyImmediate(treeBark);
                if (treeLeaves != null) UnityEngine.Object.DestroyImmediate(treeLeaves);
                foreach (var material in lookdevMaterials)
                    if (material != null) UnityEngine.Object.DestroyImmediate(material);
                surface?.Dispose();
                world?.Dispose();
            }
        }

        private static int BuildStorybookRuin(ShowcaseWorld world, int cx, int terrainY, int cz,
                                               out VoxelBrush brush)
        {
            brush = new VoxelBrush(world.Table, world.Pool, 3_000_000);

            // Broad sculpted outcrop: overlapping ellipsoids avoid the regular stepped pyramid
            // silhouette that immediately reads as "voxel game" from a distance.
            WorldArtPrimitives.Ellipsoid(ref brush, new int3(cx, terrainY + 8, cz),
                                         new int3(104, 29, 82), Mat.DarkStone);
            WorldArtPrimitives.Ellipsoid(ref brush, new int3(cx - 28, terrainY + 17, cz + 7),
                                         new int3(72, 22, 61), Mat.Stone);

            int ruinY = terrainY + 38;
            WorldArtPrimitives.CoatExposedTops(ref brush,
                new int3(cx - 105, terrainY - 4, cz - 84), new int3(210, 76, 168), Mat.Grass, 2);

            // A dirt approach breaks the sea of green and leads the eye to the arch.
            WorldArtPrimitives.RoundedBox(ref brush,
                new int3(cx - 14, ruinY - 7, cz - 51), new int3(28, 5, 41), 2, Mat.Dirt);
            brush.Stairs(new int3(cx - 12, ruinY - 15, cz - 38), 24, 8, 2, 3, 2, Mat.Stone);

            // Main ruin wall. RoundedBox is intentionally subtle at 10 cm voxels: it preserves
            // a constructed silhouette but removes needle-sharp outer corners.
            int3 wallMin = new(cx - 46, ruinY, cz + 4);
            WorldArtPrimitives.RoundedBox(ref brush, wallMin, new int3(92, 47, 9), 3, Mat.Stone);
            brush.Arch(new int3(cx - 14, ruinY, cz + 3), 28, 34, 11, 2, Mat.Empty);

            // Knock irregular bites from the top so this is an old place, not a grey rectangle.
            WorldArtPrimitives.Sphere(ref brush, new int3(cx + 38, ruinY + 44, cz + 8), 10, Mat.Empty);
            WorldArtPrimitives.Sphere(ref brush, new int3(cx - 32, ruinY + 47, cz + 8), 7, Mat.Empty);
            WorldArtPrimitives.Sphere(ref brush, new int3(cx + 17, ruinY + 48, cz + 8), 5, Mat.Empty);

            // Tapered remnants and oversized masonry sell the "chunky storybook" construction
            // language without reducing the scene to metre-wide Minecraft blocks.
            WorldArtPrimitives.Frustum(ref brush, cx - 58, ruinY - 1, cz + 25, 9, 6, 43, Mat.Stone);
            WorldArtPrimitives.Frustum(ref brush, cx + 61, ruinY - 1, cz + 21, 8, 5, 31, Mat.DarkStone);
            WorldArtPrimitives.RoundedBox(ref brush,
                new int3(cx + 48, ruinY - 1, cz - 7), new int3(22, 9, 17), 4, Mat.Stone);

            // Three boulder silhouettes with different proportions. Their scale is intentionally
            // larger than individual voxel noise; form first, texture second.
            WorldArtPrimitives.Ellipsoid(ref brush, new int3(cx - 77, ruinY - 8, cz - 25),
                                         new int3(18, 13, 14), Mat.DarkStone);
            WorldArtPrimitives.Ellipsoid(ref brush, new int3(cx + 78, ruinY - 11, cz - 32),
                                         new int3(15, 10, 20), Mat.Stone);
            WorldArtPrimitives.Ellipsoid(ref brush, new int3(cx + 91, ruinY - 15, cz + 8),
                                         new int3(11, 16, 10), Mat.DarkStone);

            // Organic connectors show why capsule chains belong in the primitive vocabulary.
            WorldArtPrimitives.Capsule(ref brush,
                new int3(cx - 64, ruinY, cz + 19), new int3(cx - 87, ruinY - 7, cz - 3), 3, Mat.Wood);
            WorldArtPrimitives.Capsule(ref brush,
                new int3(cx - 67, ruinY + 1, cz + 24), new int3(cx - 46, ruinY - 4, cz + 5), 2, Mat.Wood);

            // Material-as-surface-rule experiment: moss is not a special geometry type. It is a
            // sparse exposed-top coating laid over old masonry and boulders.
            brush.Weather(new int3(cx - 63, ruinY - 2, cz - 2), new int3(128, 55, 39),
                          Mat.Moss, 0xA17C9E2Du, 42);

            // A small fixed pool gives blue contrast and tests that "water as material" still
            // composes with the same voxel geometry vocabulary.
            WorldArtPrimitives.Ellipsoid(ref brush, new int3(cx + 53, ruinY - 12, cz - 57),
                                         new int3(30, 5, 20), Mat.Empty);
            brush.Box(new int3(cx + 28, ruinY - 13, cz - 72), new int3(50, 3, 29), Mat.Water);

            return ruinY;
        }

        private static void ApplyLookdevMaterials(GameObject root, List<Material> owned)
        {
            Shader shader = Shader.Find("VoxelEngine/WorldArtLookdev");
            if (shader == null)
                throw new InvalidOperationException("VoxelEngine/WorldArtLookdev shader was not found.");

            Material Make(string name, string textureName, Color tint, float scale, float smoothness, float topLift)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    $"Assets/Textures/Stylized/{textureName}_color.png");
                if (texture == null)
                    throw new InvalidOperationException($"Stylized texture '{textureName}' was not found.");

                var material = new Material(shader) { name = $"WorldArt.{name}" };
                material.SetTexture("_MainTex", texture);
                material.SetColor("_Tint", tint);
                material.SetFloat("_TextureScale", scale);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_TopLight", topLift);
                owned.Add(material);
                return material;
            }

            var materials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase)
            {
                ["stone"] = Make("WarmRuinsStone", "stone", new Color(1.13f, 1.03f, 0.88f), 0.34f, 0.08f, 0.14f),
                ["darkstone"] = Make("WarmCliffRock", "rock", new Color(0.88f, 0.76f, 0.62f), 0.29f, 0.05f, 0.12f),
                ["wood"] = Make("RootWood", "wood", new Color(0.83f, 0.68f, 0.48f), 0.38f, 0.04f, 0.08f),
                ["grass"] = Make("StorybookGrass", "grass", new Color(1.05f, 1.10f, 0.76f), 0.24f, 0.02f, 0.18f),
                ["dirt"] = Make("PathDirt", "dirt", new Color(1.12f, 0.92f, 0.70f), 0.30f, 0.02f, 0.10f),
                ["moss"] = Make("Moss", "grass", new Color(0.68f, 0.82f, 0.45f), 0.42f, 0.02f, 0.20f),
                ["water"] = Make("PoolWater", "rock", new Color(0.30f, 0.80f, 0.94f), 0.18f, 0.78f, 0.24f),
            };

            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (materials.TryGetValue(renderer.gameObject.name, out var replacement))
                    renderer.sharedMaterial = replacement;
            }
        }

        private static void CreateTree(int x, int y, int z,
                                       out GameObject treeObject, out Mesh mesh,
                                       out Material barkMaterial, out Material leafMaterial)
        {
            var instance = new TreeInstance
            {
                PositionMetres = float3.zero,
                Species = TreeSpecies.Oak,
                Seed = 0x73A9C41Du,
                Scale = 1.22f,
            };

            var skeleton = ProceduralTreeMeshBuilder.GenerateSkeleton(in instance);
            mesh = ProceduralTreeMeshBuilder.BuildMesh(skeleton, 0);
            if (mesh == null || mesh.vertexCount == 0)
                throw new InvalidOperationException("World-art procedural tree produced no mesh.");

            Shader barkShader = Shader.Find("VoxelEngine/ProceduralTreeBark");
            Shader leafShader = Shader.Find("VoxelEngine/ProceduralTreeLeaves");
            if (barkShader == null || leafShader == null)
                throw new InvalidOperationException("Procedural tree shaders were not found.");

            barkMaterial = new Material(barkShader) { name = "World Art Tree Bark" };
            leafMaterial = new Material(leafShader) { name = "World Art Tree Leaves" };
            leafMaterial.SetFloat("_WindStrength", 0f);
            leafMaterial.SetFloat("_Damage", 0f);

            Vector4 sun = new(-0.42f, 0.78f, -0.46f, 0f);
            Color horizon = new(0.78f, 0.83f, 0.88f, 1f);
            Color zenith = new(0.37f, 0.59f, 0.83f, 1f);
            barkMaterial.SetVector("_SunDirection", sun);
            barkMaterial.SetColor("_SkyHorizon", horizon);
            barkMaterial.SetColor("_SkyZenith", zenith);
            leafMaterial.SetVector("_SunDirection", sun);
            leafMaterial.SetColor("_SkyHorizon", horizon);
            leafMaterial.SetColor("_SkyZenith", zenith);

            treeObject = new GameObject("World Art Oak");
            treeObject.transform.position = new Vector3(x, y, z) * VoxelSurfaceRenderer.VoxelSize;
            treeObject.transform.rotation = Quaternion.Euler(0f, -22f, 0f);
            treeObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = treeObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { barkMaterial, leafMaterial };
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private static void SetupLighting(out GameObject sunObject)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.70f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.69f, 0.61f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.27f, 0.22f);
            RenderSettings.ambientIntensity = 0.82f;
            RenderSettings.fog = false;

            sunObject = new GameObject("World Art Sun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.91f, 0.77f);
            sun.intensity = 1.32f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            sunObject.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
        }

        private static void SetupCamera(int cx, int ruinY, int cz,
                                        out GameObject cameraObject, out Camera camera)
        {
            Vector3 focus = new Vector3(cx * 0.1f, (ruinY + 17) * 0.1f, (cz + 2) * 0.1f);

            cameraObject = new GameObject("World Art Camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.66f, 0.79f, 0.91f, 1f);
            camera.fieldOfView = 36f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 180f;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            cameraObject.transform.position = focus + new Vector3(15.8f, 8.8f, -23.5f);
            cameraObject.transform.LookAt(focus + new Vector3(0f, 0.6f, 0f));
        }
    }
}
