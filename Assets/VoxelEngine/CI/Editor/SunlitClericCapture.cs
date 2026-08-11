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
    /// A deterministic 3D recreation of the Mounting Force "Sunlit Cleric by the Waterfall"
    /// concept image. The terrain and ruins are ordinary destructible brickmap voxels; the
    /// character, waterfalls, flowers and procedural trees are deliberately smooth layers.
    /// That split is the art-direction experiment: constructed matter stays chunky while living
    /// and flowing things soften the world around it.
    /// </summary>
    public static class SunlitClericCapture
    {
        private const int Width = 1120;
        private const int Height = 1376;
        private static readonly int3 RegionCoord = new(1, 0, 0);

        private struct Layout
        {
            public int Cx;
            public int Cz;
            public int StageY;
            public int PoolY;
            public int WaterfallTopY;
            public int3 ClericFeet;
        }

        public static void Run()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "WorldArtKit");
            Directory.CreateDirectory(outputDirectory);

            ShowcaseWorld world = null;
            VoxelSurfaceRenderer surface = null;
            GameObject smoothRoot = null;
            GameObject cameraObject = null;
            GameObject sunObject = null;
            RenderTexture target = null;
            Texture2D capture = null;
            var ownedAssets = new List<UnityEngine.Object>();
            var voxelMaterials = new List<Material>();

            try
            {
                const uint seed = 0x53434C52u; // "SCLR"
                world = new ShowcaseWorld(seed, 48_000, 1, 2);
                world.GenerateRegionBlocking(RegionCoord);

                int cx = RegionCoord.x * ShowcaseWorld.RegionVoxelEdge + ShowcaseWorld.RegionVoxelEdge / 2;
                int cz = ShowcaseWorld.RegionVoxelEdge / 2;
                int terrainY = world.SurfaceHeight(cx, cz);
                Layout layout = BuildWaterfallGarden(world, cx, terrainY, cz, out var brush);

                if (brush.BudgetExceeded)
                    throw new InvalidOperationException("Sunlit Cleric lookdev exceeded the VoxelBrush budget.");

                world.DirtyRegions.Add(RegionCoord);
                surface = new VoxelSurfaceRenderer { CastShadows = true };
                for (int i = 0; i < 80; i++)
                {
                    surface.Sync(world, 350.0);
                    if (world.DirtyRegions.Count == 0 && surface.PendingRebuilds == 0) break;
                }

                if (surface.RegionMeshCount == 0 || surface.VertexCount == 0)
                    throw new InvalidOperationException("Sunlit Cleric scene produced no voxel surface geometry.");

                ApplyStorybookVoxelMaterials(surface.Root, voxelMaterials);

                smoothRoot = new GameObject("Sunlit Cleric Smooth Layers");
                BuildSmoothLayers(smoothRoot.transform, in layout, ownedAssets);

                SetupLighting(out sunObject);
                SetupCamera(in layout, out cameraObject, out var camera);

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "CI Sunlit Cleric Capture",
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
                    File.WriteAllBytes(Path.Combine(outputDirectory, "sunlit-cleric.png"), capture.EncodeToPNG());
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
                    $"stageY={layout.StageY}\n" +
                    $"poolY={layout.PoolY}\n" +
                    $"waterfallTopY={layout.WaterfallTopY}\n" +
                    $"voxelWrites={brush.VoxelsWritten}\n" +
                    $"bulkVoxelWrites={brush.BulkVoxelsWritten}\n" +
                    $"brickWrites={brush.BricksWritten}\n" +
                    $"surfaceRegions={surface.RegionMeshCount}\n" +
                    $"surfaceFaces={surface.FaceCount}\n" +
                    $"surfaceVertices={surface.VertexCount}\n";
                File.WriteAllText(Path.Combine(outputDirectory, "sunlit-cleric.txt"), metadata);
                Debug.Log($"CI Sunlit Cleric capture written to {outputDirectory}\n{metadata}");
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
                if (smoothRoot != null) UnityEngine.Object.DestroyImmediate(smoothRoot);
                foreach (var material in voxelMaterials)
                    if (material != null) UnityEngine.Object.DestroyImmediate(material);
                foreach (var asset in ownedAssets)
                    if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
                surface?.Dispose();
                world?.Dispose();
            }
        }

        // ---------------------------------------------------------------------
        // Destructible voxel composition
        // ---------------------------------------------------------------------

        private static Layout BuildWaterfallGarden(ShowcaseWorld world, int cx, int terrainY, int cz,
                                                    out VoxelBrush brush)
        {
            brush = new VoxelBrush(world.Table, world.Pool, 4_500_000);

            var layout = new Layout
            {
                Cx = cx,
                Cz = cz,
                StageY = terrainY + 36,
            };
            layout.PoolY = layout.StageY - 11;
            layout.WaterfallTopY = layout.StageY + 57;
            layout.ClericFeet = new int3(cx - 22, layout.StageY + 8, cz - 43);

            // Foreground hero ledge. Two overlapping ellipsoids give a readable large shape while
            // keeping the 10 cm destructible substrate. A small planar cap gives Madeline a clean
            // place to stand without making the whole landscape architectural.
            WorldArtPrimitives.Ellipsoid(ref brush,
                new int3(cx - 22, layout.StageY - 15, cz - 34),
                new int3(92, 24, 70), Mat.DarkStone);
            WorldArtPrimitives.Ellipsoid(ref brush,
                new int3(cx - 38, layout.StageY - 6, cz - 28),
                new int3(58, 16, 52), Mat.Stone);
            WorldArtPrimitives.RoundedBox(ref brush,
                new int3(cx - 45, layout.StageY + 3, cz - 58),
                new int3(52, 6, 36), 4, Mat.Dirt);

            // Left garden bank frames the character and gives the procedural oak a believable
            // root mass. It is intentionally lower than the waterfall cliff so the eye climbs
            // from character -> arch -> waterfall.
            WorldArtPrimitives.Ellipsoid(ref brush,
                new int3(cx - 72, layout.StageY + 3, cz + 17),
                new int3(58, 31, 63), Mat.DarkStone);
            WorldArtPrimitives.Ellipsoid(ref brush,
                new int3(cx - 58, layout.StageY + 18, cz + 31),
                new int3(42, 22, 44), Mat.Stone);

            // Waterfall cliff. Big overlapping masses rather than a staircase are the important
            // visual rule here: silhouette first, visible voxel stepping only as close detail.
            WorldArtPrimitives.Ellipsoid(ref brush,
                new int3(cx + 29, layout.StageY + 23, cz + 55),
                new int3(82, 43, 66), Mat.DarkStone);
            WorldArtPrimitives.Ellipsoid(ref brush,
                new int3(cx + 48, layout.StageY + 31, cz + 63),
                new int3(55, 33, 47), Mat.Stone);
            WorldArtPrimitives.RoundedBox(ref brush,
                new int3(cx + 2, layout.WaterfallTopY - 5, cz + 26),
                new int3(64, 9, 43), 5, Mat.Stone);

            // Carve the central blue basin and a shallow channel toward the foreground. Smooth
            // water meshes sit on top later, but the actual world below them is still excavated.
            WorldArtPrimitives.Ellipsoid(ref brush,
                new int3(cx + 24, layout.PoolY - 4, cz + 7),
                new int3(53, 16, 47), Mat.Empty);
            WorldArtPrimitives.Ellipsoid(ref brush,
                new int3(cx + 12, layout.PoolY - 3, cz - 28),
                new int3(30, 9, 27), Mat.Empty);

            // Ruin at left: broad wall, arched opening, broken crown. The broken wall remains
            // chunky enough to read as deliberately constructed matter beside organic cliffs.
            int ruinY = layout.StageY + 18;
            WorldArtPrimitives.RoundedBox(ref brush,
                new int3(cx - 64, ruinY, cz + 12), new int3(63, 43, 10), 3, Mat.Stone);
            brush.Arch(new int3(cx - 47, ruinY, cz + 10), 25, 31, 14, 2, Mat.Empty);
            WorldArtPrimitives.Sphere(ref brush,
                new int3(cx - 58, ruinY + 41, cz + 17), 8, Mat.Empty);
            WorldArtPrimitives.Sphere(ref brush,
                new int3(cx - 8, ruinY + 43, cz + 17), 11, Mat.Empty);
            WorldArtPrimitives.RoundedBox(ref brush,
                new int3(cx - 73, ruinY - 2, cz + 4), new int3(12, 51, 18), 3, Mat.DarkStone);

            // Broken tower on the opposite bank, with a taper and missing top chunks so its
            // silhouette rhymes with the generated concept without becoming a perfect cylinder.
            int towerX = cx + 86;
            int towerZ = cz + 31;
            WorldArtPrimitives.Frustum(ref brush, towerX, layout.StageY + 5, towerZ,
                                       14, 10, 56, Mat.Stone);
            WorldArtPrimitives.Sphere(ref brush,
                new int3(towerX + 8, layout.StageY + 59, towerZ - 2), 10, Mat.Empty);
            WorldArtPrimitives.Sphere(ref brush,
                new int3(towerX - 9, layout.StageY + 55, towerZ + 3), 7, Mat.Empty);
            WorldArtPrimitives.RoundedBox(ref brush,
                new int3(towerX - 20, layout.StageY + 4, towerZ - 24),
                new int3(33, 10, 25), 4, Mat.Stone);

            // A few oversized fallen blocks create the concept-art "chunks of an old world"
            // feeling without covering the landscape in one-voxel noise.
            WorldArtPrimitives.RoundedBox(ref brush,
                new int3(cx + 55, layout.StageY - 4, cz - 34), new int3(19, 11, 15), 4, Mat.Stone);
            WorldArtPrimitives.RoundedBox(ref brush,
                new int3(cx - 2, layout.StageY + 5, cz + 3), new int3(14, 8, 13), 3, Mat.DarkStone);
            WorldArtPrimitives.RoundedBox(ref brush,
                new int3(cx + 68, layout.StageY + 3, cz + 2), new int3(16, 12, 14), 4, Mat.Stone);

            // Organic roots bridge the smooth procedural tree into the voxel substrate.
            WorldArtPrimitives.Capsule(ref brush,
                new int3(cx - 69, layout.StageY + 11, cz - 1),
                new int3(cx - 91, layout.StageY + 4, cz - 27), 4, Mat.Wood);
            WorldArtPrimitives.Capsule(ref brush,
                new int3(cx - 68, layout.StageY + 11, cz + 3),
                new int3(cx - 41, layout.StageY + 4, cz - 19), 3, Mat.Wood);

            // Grass is a surface rule, not green geology. Put it on the exposed crowns after all
            // major masses exist, then weather the ruins sparsely with moss.
            WorldArtPrimitives.CoatExposedTops(ref brush,
                new int3(cx - 112, terrainY - 4, cz - 88),
                new int3(224, 142, 185), Mat.Grass, 2);
            brush.Weather(new int3(cx - 78, ruinY - 4, cz + 2), new int3(91, 60, 29),
                          Mat.Moss, 0xB16B00B5u, 32);
            brush.Weather(new int3(towerX - 18, layout.StageY + 2, towerZ - 18),
                          new int3(38, 66, 38), Mat.Moss, 0xC1E12D5u, 25);

            // Repaint the standing pad after the global top coat so the foreground retains a
            // warm path under the white robe rather than becoming a featureless green carpet.
            WorldArtPrimitives.RoundedBox(ref brush,
                new int3(cx - 43, layout.StageY + 7, cz - 55),
                new int3(45, 3, 30), 2, Mat.Dirt);

            return layout;
        }

        // ---------------------------------------------------------------------
        // World materials
        // ---------------------------------------------------------------------

        private static void ApplyStorybookVoxelMaterials(GameObject root, List<Material> owned)
        {
            Shader shader = Shader.Find("VoxelEngine/WorldArtLookdev") ?? Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("No lookdev or Standard surface shader was found.");

            Material Make(string name, string textureName, Color tint, float scale, float detail,
                          float smoothness, float topLift)
            {
                var material = new Material(shader) { name = $"SunlitCleric.{name}" };
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    $"Assets/Textures/Stylized/{textureName}_color.png");
                if (texture != null) material.SetTexture("_MainTex", texture);
                material.SetColor("_Tint", tint);
                material.SetColor("_Color", tint);
                material.SetColor("_BaseColor", tint);
                material.SetFloat("_TextureScale", scale);
                material.SetFloat("_TextureInfluence", detail);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Glossiness", smoothness);
                material.SetFloat("_TopLight", topLift);
                owned.Add(material);
                return material;
            }

            var materials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase)
            {
                ["stone"] = Make("SunStone", "stone", new Color(0.78f, 0.72f, 0.60f), 0.20f, 0.10f, 0.06f, 0.17f),
                ["darkstone"] = Make("CliffRock", "rock", new Color(0.43f, 0.39f, 0.34f), 0.17f, 0.08f, 0.04f, 0.10f),
                ["wood"] = Make("RootWood", "wood", new Color(0.39f, 0.24f, 0.12f), 0.22f, 0.08f, 0.03f, 0.08f),
                ["grass"] = Make("SunGrass", "grass", new Color(0.39f, 0.59f, 0.20f), 0.15f, 0.07f, 0.02f, 0.21f),
                ["dirt"] = Make("WarmPath", "dirt", new Color(0.55f, 0.40f, 0.26f), 0.18f, 0.08f, 0.03f, 0.12f),
                ["moss"] = Make("RuinMoss", "grass", new Color(0.27f, 0.48f, 0.17f), 0.23f, 0.05f, 0.02f, 0.20f),
                ["water"] = Make("VoxelWater", "rock", new Color(0.16f, 0.58f, 0.70f), 0.12f, 0.04f, 0.68f, 0.12f),
            };

            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (materials.TryGetValue(renderer.gameObject.name, out var replacement))
                    renderer.sharedMaterial = replacement;
            }
        }

        // ---------------------------------------------------------------------
        // Smooth layers: water, character, trees, flowers
        // ---------------------------------------------------------------------

        private static void BuildSmoothLayers(Transform root, in Layout layout,
                                              List<UnityEngine.Object> owned)
        {
            Texture2D waterTexture = CreateWaterfallTexture();
            owned.Add(waterTexture);

            Material waterfall = CreateTransparentStandard(
                "Waterfall", new Color(0.66f, 0.91f, 1.0f, 0.74f), waterTexture, 0.78f, 0.22f);
            Material waterfallHighlight = CreateTransparentStandard(
                "Waterfall Highlight", new Color(0.92f, 0.98f, 1.0f, 0.50f), waterTexture, 0.55f, 0.34f);
            Material pool = CreateTransparentStandard(
                "Pool", new Color(0.18f, 0.68f, 0.78f, 0.72f), null, 0.90f, 0.10f);
            Material foam = CreateTransparentStandard(
                "Foam", new Color(0.92f, 0.98f, 1.0f, 0.32f), null, 0.32f, 0.45f);
            owned.Add(waterfall);
            owned.Add(waterfallHighlight);
            owned.Add(pool);
            owned.Add(foam);

            CreateWater(in layout, root, waterfall, waterfallHighlight, pool, foam, owned);
            CreateCleric(in layout, root, owned);
            CreateFlowers(in layout, root, owned);
            CreateTrees(in layout, root, owned);
        }

        private static void CreateWater(in Layout layout, Transform root,
                                        Material waterfall, Material highlight,
                                        Material pool, Material foam,
                                        List<UnityEngine.Object> owned)
        {
            float s = VoxelSurfaceRenderer.VoxelSize;

            Vector3 poolCentre = new((layout.Cx + 20) * s, (layout.PoolY + 2) * s, (layout.Cz + 3) * s);
            Mesh poolMesh = BuildEllipseMesh(5.4f, 4.4f, 64);
            owned.Add(poolMesh);
            var poolObject = CreateMeshObject("Turquoise Pool", poolMesh, pool, root);
            poolObject.transform.position = poolCentre;

            // A smaller foreground tongue makes the pool visually connect to the camera instead
            // of reading like a perfect isolated disc.
            Mesh streamMesh = BuildEllipseMesh(2.7f, 2.0f, 48);
            owned.Add(streamMesh);
            var stream = CreateMeshObject("Foreground Stream", streamMesh, pool, root);
            stream.transform.position = new Vector3((layout.Cx + 8) * s,
                                                    (layout.PoolY + 2.5f) * s,
                                                    (layout.Cz - 29) * s);
            stream.transform.rotation = Quaternion.Euler(0f, -12f, 0f);

            // Two main falls plus a slim secondary ribbon. The concept uses vertical white-blue
            // strokes against dark rock, so each fall has a translucent cyan body and a narrower
            // brighter layer slightly toward camera.
            CreateFall(layout.Cx + 18, layout.Cz + 31, layout.WaterfallTopY,
                       layout.Cx + 15, layout.Cz + 17, layout.PoolY + 5,
                       2.55f, waterfall, highlight, root, owned, 0.00f);
            CreateFall(layout.Cx + 44, layout.Cz + 38, layout.WaterfallTopY - 5,
                       layout.Cx + 39, layout.Cz + 21, layout.PoolY + 5,
                       1.75f, waterfall, highlight, root, owned, 0.67f);
            CreateFall(layout.Cx + 61, layout.Cz + 43, layout.WaterfallTopY - 13,
                       layout.Cx + 55, layout.Cz + 30, layout.PoolY + 8,
                       0.82f, waterfall, highlight, root, owned, 1.19f);

            // Soft foam volumes hide the mathematically exact intersection between ribbon and
            // pool. They also produce the bright focal accents visible in the concept painting.
            Vector3[] foamPositions =
            {
                new((layout.Cx + 15) * s, (layout.PoolY + 4.5f) * s, (layout.Cz + 16) * s),
                new((layout.Cx + 39) * s, (layout.PoolY + 4.0f) * s, (layout.Cz + 20) * s),
                new((layout.Cx + 9) * s, (layout.PoolY + 2.8f) * s, (layout.Cz - 27) * s),
            };
            foreach (Vector3 p in foamPositions)
            {
                for (int i = 0; i < 4; i++)
                {
                    var puff = CreatePrimitive(PrimitiveType.Sphere, "Waterfall Mist", foam, root);
                    puff.transform.position = p + new Vector3((i - 1.5f) * 0.22f,
                                                               0.04f + (i & 1) * 0.09f,
                                                               ((i * 7) % 3 - 1) * 0.17f);
                    puff.transform.localScale = new Vector3(0.55f, 0.16f, 0.38f) * (0.86f + i * 0.08f);
                }
            }
        }

        private static void CreateFall(int topX, int topZ, int topY,
                                       int bottomX, int bottomZ, int bottomY,
                                       float widthMetres, Material body, Material highlight,
                                       Transform root, List<UnityEngine.Object> owned, float phase)
        {
            float s = VoxelSurfaceRenderer.VoxelSize;
            Vector3 top = new(topX * s, topY * s, topZ * s);
            Vector3 bottom = new(bottomX * s, bottomY * s, bottomZ * s);

            Mesh bodyMesh = BuildWaterfallRibbon(top, bottom, widthMetres, 18, 0.22f, phase);
            owned.Add(bodyMesh);
            CreateMeshObject("Waterfall Body", bodyMesh, body, root);

            Vector3 cameraOffset = new(0f, 0f, -0.07f);
            Mesh highlightMesh = BuildWaterfallRibbon(top + cameraOffset, bottom + cameraOffset,
                                                      widthMetres * 0.48f, 18, 0.12f, phase + 0.43f);
            owned.Add(highlightMesh);
            CreateMeshObject("Waterfall Sun Streak", highlightMesh, highlight, root);
        }

        private static void CreateCleric(in Layout layout, Transform root,
                                         List<UnityEngine.Object> owned)
        {
            var cleric = new GameObject("Madeline Lookdev Proxy");
            cleric.transform.SetParent(root, false);
            cleric.transform.position = new Vector3(layout.ClericFeet.x,
                                                    layout.ClericFeet.y,
                                                    layout.ClericFeet.z)
                                        * VoxelSurfaceRenderer.VoxelSize;
            // The camera is on negative Z. Build the stylised face toward -Z and rotate just
            // enough to keep the pose three-quarter rather than mug-shot flat.
            cleric.transform.rotation = Quaternion.Euler(0f, -8f, 0f);

            Material white = CreateStandard("Cleric White", new Color(0.94f, 0.93f, 0.87f), 0.04f);
            Material warmWhite = CreateStandard("Cleric Warm White", new Color(0.83f, 0.81f, 0.74f), 0.03f);
            Material gold = CreateStandard("Cleric Gold", new Color(0.88f, 0.64f, 0.18f), 0.52f, 0.12f);
            Material blue = CreateStandard("Cleric Blue", new Color(0.38f, 0.68f, 0.83f), 0.18f);
            Material skin = CreateStandard("Cleric Skin", new Color(0.93f, 0.70f, 0.56f), 0.18f);
            Material hair = CreateStandard("Cleric Blonde", new Color(0.90f, 0.70f, 0.28f), 0.20f);
            Material eye = CreateStandard("Cleric Brown Eyes", new Color(0.24f, 0.11f, 0.055f), 0.40f);
            Material staffWood = CreateStandard("Staff Wood", new Color(0.31f, 0.17f, 0.085f), 0.10f);
            owned.Add(white); owned.Add(warmWhite); owned.Add(gold); owned.Add(blue);
            owned.Add(skin); owned.Add(hair); owned.Add(eye); owned.Add(staffWood);

            // Bell-shaped robe with a small gold hem. At gameplay distance this silhouette is far
            // more important than folds; the white/gold/blue colour blocks carry the identity.
            Mesh robeMesh = BuildFrustumMesh(0.43f, 0.235f, 0.92f, 28, true);
            owned.Add(robeMesh);
            var robe = CreateMeshObject("White Cleric Robe", robeMesh, white, cleric.transform);
            robe.transform.localPosition = new Vector3(0f, 0.48f, 0f);

            Mesh hemMesh = BuildFrustumMesh(0.445f, 0.41f, 0.075f, 28, true);
            owned.Add(hemMesh);
            var hem = CreateMeshObject("Gold Robe Hem", hemMesh, gold, cleric.transform);
            hem.transform.localPosition = new Vector3(0f, 0.075f, 0f);

            var torso = CreatePrimitive(PrimitiveType.Sphere, "Cleric Bodice", warmWhite, cleric.transform);
            torso.transform.localPosition = new Vector3(0f, 1.03f, 0f);
            torso.transform.localScale = new Vector3(0.45f, 0.40f, 0.30f);

            var collar = CreatePrimitive(PrimitiveType.Sphere, "Light Blue Cleric Collar", blue, cleric.transform);
            collar.transform.localPosition = new Vector3(0f, 1.20f, -0.015f);
            collar.transform.localScale = new Vector3(0.52f, 0.14f, 0.33f);

            var belt = CreatePrimitive(PrimitiveType.Cylinder, "Gold Belt", gold, cleric.transform);
            belt.transform.localPosition = new Vector3(0f, 0.90f, 0f);
            belt.transform.localScale = new Vector3(0.25f, 0.025f, 0.25f);

            // Hair mass is built behind the face, then a smaller cap and side locks are layered in
            // front. This is intentionally smooth/anime-like rather than voxel hair.
            var hairBack = CreatePrimitive(PrimitiveType.Sphere, "Blonde Hair Back", hair, cleric.transform);
            hairBack.transform.localPosition = new Vector3(0f, 1.57f, 0.075f);
            hairBack.transform.localScale = new Vector3(0.36f, 0.41f, 0.31f);

            var head = CreatePrimitive(PrimitiveType.Sphere, "Cleric Face", skin, cleric.transform);
            head.transform.localPosition = new Vector3(0f, 1.54f, -0.045f);
            head.transform.localScale = new Vector3(0.30f, 0.34f, 0.27f);

            var hairCap = CreatePrimitive(PrimitiveType.Sphere, "Blonde Hair Cap", hair, cleric.transform);
            hairCap.transform.localPosition = new Vector3(0f, 1.70f, -0.035f);
            hairCap.transform.localScale = new Vector3(0.32f, 0.20f, 0.29f);

            CreateCapsuleBetweenLocal("Left Hair Lock", new Vector3(-0.24f, 1.66f, -0.02f),
                                      new Vector3(-0.28f, 1.30f, 0.00f), 0.075f, hair, cleric.transform);
            CreateCapsuleBetweenLocal("Right Hair Lock", new Vector3(0.24f, 1.66f, -0.02f),
                                      new Vector3(0.27f, 1.33f, 0.00f), 0.070f, hair, cleric.transform);

            // Brown eyes preserve Madeline's source portrait identity.
            var leftEye = CreatePrimitive(PrimitiveType.Sphere, "Left Brown Eye", eye, cleric.transform);
            leftEye.transform.localPosition = new Vector3(-0.078f, 1.56f, -0.292f);
            leftEye.transform.localScale = new Vector3(0.038f, 0.030f, 0.018f);
            var rightEye = CreatePrimitive(PrimitiveType.Sphere, "Right Brown Eye", eye, cleric.transform);
            rightEye.transform.localPosition = new Vector3(0.078f, 1.56f, -0.292f);
            rightEye.transform.localScale = new Vector3(0.038f, 0.030f, 0.018f);

            // Sleeves are smooth capsules. The staff-side arm reaches outward so the silhouette
            // reads immediately in the portrait framing.
            CreateCapsuleBetweenLocal("Left Sleeve", new Vector3(-0.22f, 1.15f, -0.01f),
                                      new Vector3(-0.37f, 0.94f, -0.09f), 0.10f, white, cleric.transform);
            CreateCapsuleBetweenLocal("Right Sleeve", new Vector3(0.22f, 1.15f, -0.01f),
                                      new Vector3(0.43f, 1.02f, -0.11f), 0.10f, white, cleric.transform);
            var leftHand = CreatePrimitive(PrimitiveType.Sphere, "Left Hand", skin, cleric.transform);
            leftHand.transform.localPosition = new Vector3(-0.40f, 0.90f, -0.10f);
            leftHand.transform.localScale = Vector3.one * 0.13f;
            var rightHand = CreatePrimitive(PrimitiveType.Sphere, "Staff Hand", skin, cleric.transform);
            rightHand.transform.localPosition = new Vector3(0.46f, 0.98f, -0.12f);
            rightHand.transform.localScale = Vector3.one * 0.13f;

            // Staff: dark shaft + warm gold sun head + blue crystal. The head is intentionally
            // oversized because the concept art reads it as part of the character silhouette.
            CreateCapsuleBetweenLocal("Staff Shaft", new Vector3(0.49f, 0.04f, -0.10f),
                                      new Vector3(0.49f, 1.86f, -0.10f), 0.035f, staffWood, cleric.transform);
            var staffCore = CreatePrimitive(PrimitiveType.Sphere, "Staff Sun Core", gold, cleric.transform);
            staffCore.transform.localPosition = new Vector3(0.49f, 1.91f, -0.10f);
            staffCore.transform.localScale = Vector3.one * 0.22f;
            var crystal = CreatePrimitive(PrimitiveType.Sphere, "Staff Blue Crystal", blue, cleric.transform);
            crystal.transform.localPosition = new Vector3(0.49f, 1.91f, -0.225f);
            crystal.transform.localScale = Vector3.one * 0.105f;

            Vector3 sunCentre = new(0.49f, 1.91f, -0.10f);
            CreateCapsuleBetweenLocal("Staff Ray Up", sunCentre + new Vector3(0f, 0.10f, 0f),
                                      sunCentre + new Vector3(0f, 0.31f, 0f), 0.027f, gold, cleric.transform);
            CreateCapsuleBetweenLocal("Staff Ray Down", sunCentre + new Vector3(0f, -0.10f, 0f),
                                      sunCentre + new Vector3(0f, -0.25f, 0f), 0.027f, gold, cleric.transform);
            CreateCapsuleBetweenLocal("Staff Ray Left", sunCentre + new Vector3(-0.10f, 0f, 0f),
                                      sunCentre + new Vector3(-0.27f, 0f, 0f), 0.027f, gold, cleric.transform);
            CreateCapsuleBetweenLocal("Staff Ray Right", sunCentre + new Vector3(0.10f, 0f, 0f),
                                      sunCentre + new Vector3(0.27f, 0f, 0f), 0.027f, gold, cleric.transform);
        }

        private static void CreateFlowers(in Layout layout, Transform root,
                                          List<UnityEngine.Object> owned)
        {
            Material stem = CreateStandard("Flower Stems", new Color(0.22f, 0.43f, 0.12f), 0.02f);
            Material yellow = CreateStandard("Flower Yellow", new Color(0.96f, 0.76f, 0.20f), 0.12f);
            Material white = CreateStandard("Flower White", new Color(0.96f, 0.94f, 0.88f), 0.08f);
            Material pink = CreateStandard("Flower Pink", new Color(0.91f, 0.50f, 0.56f), 0.10f);
            Material lilac = CreateStandard("Flower Lilac", new Color(0.67f, 0.54f, 0.82f), 0.10f);
            owned.Add(stem); owned.Add(yellow); owned.Add(white); owned.Add(pink); owned.Add(lilac);

            float s = VoxelSurfaceRenderer.VoxelSize;
            Vector3 baseWorld = new(layout.Cx * s, (layout.StageY + 10) * s, layout.Cz * s);
            Vector3[] offsets =
            {
                new(-4.4f, 0f, -4.7f), new(-3.8f, 0f, -5.4f), new(-2.8f, 0f, -5.0f),
                new(-1.1f, 0f, -5.5f), new(0.4f, 0f, -5.0f), new(1.3f, 0f, -4.6f),
                new(-4.9f, 0f, -3.7f), new(1.9f, 0f, -3.4f), new(2.3f, 0f, -2.6f),
                new(-5.2f, 0f, -2.8f), new(-0.1f, 0f, -4.3f), new(3.0f, 0f, -1.9f),
            };
            Material[] petals = { white, yellow, pink, white, lilac, yellow, white, pink, yellow, lilac, white, pink };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 p = baseWorld + offsets[i];
                float height = 0.22f + (i % 4) * 0.035f;
                var flowerRoot = new GameObject($"Storybook Flower {i}");
                flowerRoot.transform.SetParent(root, false);
                flowerRoot.transform.position = p;

                CreateCapsuleBetweenLocal("Stem", Vector3.zero,
                                          new Vector3(0f, height, 0f), 0.012f, stem, flowerRoot.transform);
                Vector3 centre = new(0f, height, 0f);
                var flowerCentre = CreatePrimitive(PrimitiveType.Sphere, "Flower Centre", yellow, flowerRoot.transform);
                flowerCentre.transform.localPosition = centre;
                flowerCentre.transform.localScale = Vector3.one * 0.055f;

                for (int petal = 0; petal < 5; petal++)
                {
                    float angle = petal * Mathf.PI * 2f / 5f;
                    var petalObject = CreatePrimitive(PrimitiveType.Sphere, "Petal", petals[i], flowerRoot.transform);
                    petalObject.transform.localPosition = centre + new Vector3(Mathf.Cos(angle) * 0.075f,
                                                                               0f,
                                                                               Mathf.Sin(angle) * 0.075f);
                    petalObject.transform.localScale = new Vector3(0.085f, 0.025f, 0.055f);
                    petalObject.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                }
            }
        }

        private static void CreateTrees(in Layout layout, Transform root,
                                        List<UnityEngine.Object> owned)
        {
            CreateTreeInstance(new int3(layout.Cx - 72, layout.StageY + 11, layout.Cz - 2),
                               0x73A9C41Du, 0.92f, -18f, root, owned, "Sunlit Oak");
            CreateTreeInstance(new int3(layout.Cx + 92, layout.StageY + 10, layout.Cz + 56),
                               0x19D5A22Bu, 0.58f, 31f, root, owned, "Far Garden Tree");
        }

        private static void CreateTreeInstance(int3 voxelPosition, uint seed, float objectScale,
                                               float yaw, Transform root,
                                               List<UnityEngine.Object> owned, string name)
        {
            var instance = new TreeInstance
            {
                PositionMetres = float3.zero,
                Species = TreeSpecies.Oak,
                Seed = seed,
                Scale = 1.0f,
            };

            var skeleton = ProceduralTreeMeshBuilder.GenerateSkeleton(in instance);
            Mesh mesh = ProceduralTreeMeshBuilder.BuildMesh(skeleton, 0);
            if (mesh == null || mesh.vertexCount == 0)
                throw new InvalidOperationException($"{name} produced no procedural tree mesh.");
            owned.Add(mesh);

            Shader barkShader = Shader.Find("VoxelEngine/ProceduralTreeBark");
            Shader leafShader = Shader.Find("VoxelEngine/ProceduralTreeLeaves");
            if (barkShader == null || leafShader == null)
                throw new InvalidOperationException("Procedural tree shaders were not found.");

            var bark = new Material(barkShader) { name = $"{name} Bark" };
            var leaves = new Material(leafShader) { name = $"{name} Leaves" };
            owned.Add(bark);
            owned.Add(leaves);
            leaves.SetFloat("_WindStrength", 0f);
            leaves.SetFloat("_Damage", 0f);

            Vector4 sun = new(-0.36f, 0.82f, -0.42f, 0f);
            Color horizon = new(0.72f, 0.82f, 0.91f, 1f);
            Color zenith = new(0.34f, 0.61f, 0.87f, 1f);
            bark.SetVector("_SunDirection", sun);
            bark.SetColor("_SkyHorizon", horizon);
            bark.SetColor("_SkyZenith", zenith);
            leaves.SetVector("_SunDirection", sun);
            leaves.SetColor("_SkyHorizon", horizon);
            leaves.SetColor("_SkyZenith", zenith);

            var tree = new GameObject(name);
            tree.transform.SetParent(root, false);
            tree.transform.position = new Vector3(voxelPosition.x, voxelPosition.y, voxelPosition.z)
                                      * VoxelSurfaceRenderer.VoxelSize;
            tree.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            tree.transform.localScale = Vector3.one * objectScale;
            tree.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = tree.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { bark, leaves };
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        // ---------------------------------------------------------------------
        // Camera and lighting
        // ---------------------------------------------------------------------

        private static void SetupLighting(out GameObject sunObject)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.77f, 0.91f);
            RenderSettings.ambientEquatorColor = new Color(0.74f, 0.75f, 0.68f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.31f, 0.25f);
            RenderSettings.ambientIntensity = 0.92f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.66f, 0.80f, 0.91f);
            RenderSettings.fogStartDistance = 29f;
            RenderSettings.fogEndDistance = 70f;

            sunObject = new GameObject("Sunlit Cleric Sun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.91f, 0.73f);
            sun.intensity = 1.38f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.66f;
            sunObject.transform.rotation = Quaternion.Euler(45f, -34f, 0f);
        }

        private static void SetupCamera(in Layout layout,
                                        out GameObject cameraObject, out Camera camera)
        {
            float s = VoxelSurfaceRenderer.VoxelSize;
            Vector3 character = new(layout.ClericFeet.x * s,
                                    (layout.ClericFeet.y + 9) * s,
                                    layout.ClericFeet.z * s);
            Vector3 waterfall = new((layout.Cx + 27) * s,
                                    (layout.StageY + 28) * s,
                                    (layout.Cz + 27) * s);
            Vector3 focus = Vector3.Lerp(character, waterfall, 0.38f);

            cameraObject = new GameObject("Sunlit Cleric Camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.66f, 0.82f, 0.93f, 1f);
            camera.fieldOfView = 33f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 110f;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            // Portrait framing: Madeline is large in the lower third; the eye travels past her
            // staff to the bright waterfall and broken tower behind.
            cameraObject.transform.position = focus + new Vector3(8.3f, 5.0f, -18.8f);
            cameraObject.transform.LookAt(focus + new Vector3(0f, 0.45f, 0f));
        }

        // ---------------------------------------------------------------------
        // Runtime material helpers
        // ---------------------------------------------------------------------

        private static Material CreateStandard(string name, Color colour,
                                               float smoothness, float metallic = 0f)
        {
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("No Standard/Lit shader is available.");

            var material = new Material(shader) { name = name };
            material.SetColor("_Color", colour);
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            return material;
        }

        private static Material CreateTransparentStandard(string name, Color colour,
                                                          Texture texture, float smoothness,
                                                          float emissionStrength)
        {
            Material material = CreateStandard(name, colour, smoothness);
            if (texture != null) material.SetTexture("_MainTex", texture);

            // Built-in Standard transparent mode. The project currently runs built-in even though
            // URP packages are installed; these properties are harmless if the fallback Lit shader
            // is ever used instead.
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;

            if (emissionStrength > 0f)
            {
                material.EnableKeyword("_EMISSION");
                Color emission = new Color(colour.r, colour.g, colour.b, 1f) * emissionStrength;
                material.SetColor("_EmissionColor", emission);
            }
            return material;
        }

        private static Texture2D CreateWaterfallTexture()
        {
            const int width = 64;
            const int height = 128;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = "Generated Sunlit Waterfall Streaks",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                float v = y / (float)(height - 1);
                float streak = 0.5f + 0.5f * Mathf.Sin(u * 38f + Mathf.Sin(v * 9f) * 1.6f);
                streak = Mathf.Pow(streak, 3.0f);
                float broad = 0.5f + 0.5f * Mathf.Sin(u * 9f + v * 4f);
                float brightness = 0.72f + streak * 0.23f + broad * 0.05f;
                byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(205f + brightness * 48f), 0, 255);
                byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(224f + brightness * 31f), 0, 255);
                byte b = 255;
                byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(150f + streak * 74f), 0, 255);
                pixels[x + y * width] = new Color32(r, g, b, a);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        // ---------------------------------------------------------------------
        // Mesh / primitive helpers
        // ---------------------------------------------------------------------

        private static GameObject CreatePrimitive(PrimitiveType type, string name,
                                                  Material material, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            var collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        private static GameObject CreateMeshObject(string name, Mesh mesh, Material material,
                                                   Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = material.renderQueue >= (int)RenderQueue.Transparent
                ? ShadowCastingMode.Off
                : ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        private static void CreateCapsuleBetweenLocal(string name, Vector3 a, Vector3 b, float radius,
                                                      Material material, Transform parent)
        {
            Vector3 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.001f) return;

            var capsule = CreatePrimitive(PrimitiveType.Capsule, name, material, parent);
            capsule.transform.localPosition = (a + b) * 0.5f;
            capsule.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            capsule.transform.localScale = new Vector3(radius * 2f,
                                                       Mathf.Max(radius, length * 0.5f),
                                                       radius * 2f);
        }

        private static Mesh BuildFrustumMesh(float bottomRadius, float topRadius, float height,
                                             int segments, bool capped)
        {
            var vertices = new List<Vector3>(segments * 2 + 2);
            var normals = new List<Vector3>(segments * 2 + 2);
            var triangles = new List<int>(segments * 12);

            float slope = (bottomRadius - topRadius) / Mathf.Max(0.001f, height);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                Vector3 normal = new Vector3(x, slope, z).normalized;
                vertices.Add(new Vector3(x * bottomRadius, -height * 0.5f, z * bottomRadius));
                vertices.Add(new Vector3(x * topRadius, height * 0.5f, z * topRadius));
                normals.Add(normal);
                normals.Add(normal);
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int b0 = i * 2;
                int t0 = b0 + 1;
                int b1 = next * 2;
                int t1 = b1 + 1;
                triangles.Add(b0); triangles.Add(t0); triangles.Add(t1);
                triangles.Add(b0); triangles.Add(t1); triangles.Add(b1);
            }

            if (capped)
            {
                int bottomCentre = vertices.Count;
                vertices.Add(new Vector3(0f, -height * 0.5f, 0f));
                normals.Add(Vector3.down);
                int topCentre = vertices.Count;
                vertices.Add(new Vector3(0f, height * 0.5f, 0f));
                normals.Add(Vector3.up);

                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    triangles.Add(bottomCentre); triangles.Add(next * 2); triangles.Add(i * 2);
                    triangles.Add(topCentre); triangles.Add(i * 2 + 1); triangles.Add(next * 2 + 1);
                }
            }

            var mesh = new Mesh { name = "Runtime Stylized Frustum" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildEllipseMesh(float radiusX, float radiusZ, int segments)
        {
            var vertices = new Vector3[segments + 1];
            var normals = new Vector3[segments + 1];
            var uv = new Vector2[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uv[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                vertices[i + 1] = new Vector3(x * radiusX, 0f, z * radiusZ);
                normals[i + 1] = Vector3.up;
                uv[i + 1] = new Vector2(x * 0.5f + 0.5f, z * 0.5f + 0.5f);

                int next = (i + 1) % segments;
                int tri = i * 3;
                triangles[tri] = 0;
                triangles[tri + 1] = i + 1;
                triangles[tri + 2] = next + 1;
            }

            var mesh = new Mesh { name = "Runtime Storybook Water Ellipse" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildWaterfallRibbon(Vector3 top, Vector3 bottom, float width,
                                                 int segments, float sway, float phase)
        {
            var vertices = new Vector3[(segments + 1) * 2];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];

            Vector3 direction = bottom - top;
            Vector3 side = Vector3.Cross(direction.normalized, Vector3.up);
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            side.Normalize();

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 centre = Vector3.Lerp(top, bottom, t);
                float wave = Mathf.Sin(t * 8.0f + phase * 4.1f) * sway
                           + Mathf.Sin(t * 17.0f + phase) * sway * 0.28f;
                centre += side * wave;
                float localWidth = width * (0.92f + 0.10f * Mathf.Sin(t * 5f + phase));

                int v = i * 2;
                vertices[v] = centre - side * localWidth * 0.5f;
                vertices[v + 1] = centre + side * localWidth * 0.5f;
                normals[v] = Vector3.back;
                normals[v + 1] = Vector3.back;
                uv[v] = new Vector2(0f, t * 2.6f);
                uv[v + 1] = new Vector2(1f, t * 2.6f);

                if (i < segments)
                {
                    int tri = i * 6;
                    triangles[tri] = v;
                    triangles[tri + 1] = v + 2;
                    triangles[tri + 2] = v + 1;
                    triangles[tri + 3] = v + 1;
                    triangles[tri + 4] = v + 2;
                    triangles[tri + 5] = v + 3;
                }
            }

            var mesh = new Mesh { name = "Runtime Waterfall Ribbon" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
