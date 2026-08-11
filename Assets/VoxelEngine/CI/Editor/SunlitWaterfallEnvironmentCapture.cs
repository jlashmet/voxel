using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Showcase;
using VoxelEngine.Structures;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Environment-only lookdev for the "Sunlit Cleric by the Waterfall" target.
    /// Destructible voxels remain underneath; smooth skins and overlays define the small reusable
    /// art vocabulary: rounded turf, ashlar ruins, water ribbons, moss, ivy, flowers and foliage.
    /// </summary>
    public static class SunlitWaterfallEnvironmentCapture
    {
        private const int Width = 1120;
        private const int Height = 1376;
        private static readonly int3 RegionCoord = new(1, 0, 0);

        private sealed class Palette
        {
            public Material Grass, GrassShadow, Earth, Cliff, Stone, StoneLight, Moss;
            public Material Bark, Leaves, LeavesLight, Water, Waterfall, Foam, Cloud;
            public Material FlowerWhite, FlowerYellow, FlowerPink, FlowerBlue, Roof;
        }

        private struct Layout
        {
            public int Cx, Cz, StageY, PoolY;
            public Vector3 Origin;
        }

        public static void Run()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outDir = Path.Combine(projectRoot, "Artifacts", "WorldArtKit");
            Directory.CreateDirectory(outDir);

            ShowcaseWorld world = null;
            VoxelSurfaceRenderer voxelSurface = null;
            GameObject presentationRoot = null;
            GameObject cameraObject = null;
            GameObject sunObject = null;
            RenderTexture target = null;
            Texture2D capture = null;
            var owned = new List<UnityEngine.Object>();

            try
            {
                const uint seed = 0x57415452u;
                world = new ShowcaseWorld(seed, 64_000, 1, 2);
                world.GenerateRegionBlocking(RegionCoord);

                int cx = RegionCoord.x * ShowcaseWorld.RegionVoxelEdge + ShowcaseWorld.RegionVoxelEdge / 2;
                int cz = ShowcaseWorld.RegionVoxelEdge / 2;
                int terrainY = world.SurfaceHeight(cx, cz);
                Layout layout = BuildVoxelSubstrate(world, cx, terrainY, cz, out var brush);
                if (brush.BudgetExceeded) throw new InvalidOperationException("Sunlit environment exceeded VoxelBrush budget.");

                world.DirtyRegions.Add(RegionCoord);
                voxelSurface = new VoxelSurfaceRenderer { CastShadows = true };
                for (int i = 0; i < 100; i++)
                {
                    voxelSurface.Sync(world, 400.0);
                    if (world.DirtyRegions.Count == 0 && voxelSurface.PendingRebuilds == 0) break;
                }
                if (voxelSurface.RegionMeshCount == 0 || voxelSurface.VertexCount == 0)
                    throw new InvalidOperationException("Sunlit environment produced no voxel surface geometry.");

                Palette palette = BuildPalette(owned);
                ApplyVoxelPalette(voxelSurface.Root, palette, owned);

                presentationRoot = new GameObject("Sunlit Waterfall Art Kit");
                BuildPresentation(presentationRoot.transform, in layout, palette, owned);
                SetupLighting(out sunObject);
                SetupCamera(in layout, out cameraObject, out Camera camera);

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "CI Sunlit Waterfall Environment",
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
                    File.WriteAllBytes(Path.Combine(outDir, "sunlit-cleric.png"), capture.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    camera.targetTexture = null;
                }

                string metadata =
                    $"seed={seed}\nregion={RegionCoord.x},{RegionCoord.y},{RegionCoord.z}\n" +
                    $"terrainY={terrainY}\nstageY={layout.StageY}\npoolY={layout.PoolY}\n" +
                    $"voxelWrites={brush.VoxelsWritten}\nbulkVoxelWrites={brush.BulkVoxelsWritten}\n" +
                    $"brickWrites={brush.BricksWritten}\nsurfaceFaces={voxelSurface.FaceCount}\n" +
                    $"surfaceVertices={voxelSurface.VertexCount}\n";
                File.WriteAllText(Path.Combine(outDir, "sunlit-cleric.txt"), metadata);
                Debug.Log($"CI Sunlit Waterfall environment written to {outDir}\n{metadata}");
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
                if (sunObject != null) UnityEngine.Object.DestroyImmediate(sunObject);
                if (presentationRoot != null) UnityEngine.Object.DestroyImmediate(presentationRoot);
                foreach (UnityEngine.Object o in owned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
                voxelSurface?.Dispose();
                world?.Dispose();
            }
        }

        private static Layout BuildVoxelSubstrate(ShowcaseWorld world, int cx, int terrainY, int cz, out VoxelBrush brush)
        {
            brush = new VoxelBrush(world.Table, world.Pool, 6_000_000);
            var l = new Layout { Cx = cx, Cz = cz, StageY = terrainY + 28, PoolY = terrainY + 18 };
            l.Origin = new Vector3(cx, l.StageY, cz) * VoxelSurfaceRenderer.VoxelSize;

            WorldArtPrimitives.Ellipsoid(ref brush, new int3(cx, l.StageY - 16, cz - 36), new int3(70, 25, 58), Mat.DarkStone);
            WorldArtPrimitives.Ellipsoid(ref brush, new int3(cx - 8, l.StageY - 3, cz - 36), new int3(62, 15, 52), Mat.Dirt);
            WorldArtPrimitives.Ellipsoid(ref brush, new int3(cx - 70, l.StageY + 2, cz + 10), new int3(56, 33, 60), Mat.DarkStone);
            WorldArtPrimitives.Ellipsoid(ref brush, new int3(cx - 62, l.StageY + 18, cz + 14), new int3(48, 20, 50), Mat.Dirt);

            int[] xs = { 52, 64, 72, 78 };
            int[] zs = { -4, 28, 58, 86 };
            int[] ys = { 1, 20, 41, 63 };
            int[] rx = { 54, 58, 62, 66 };
            for (int i = 0; i < xs.Length; i++)
            {
                WorldArtPrimitives.Ellipsoid(ref brush, new int3(cx + xs[i], l.StageY + ys[i] - 15, cz + zs[i]), new int3(rx[i], 26, 49), Mat.DarkStone);
                WorldArtPrimitives.Ellipsoid(ref brush, new int3(cx + xs[i] - 3, l.StageY + ys[i] - 2, cz + zs[i]), new int3(rx[i] - 7, 14, 44), Mat.Dirt);
            }

            WorldArtPrimitives.Ellipsoid(ref brush, new int3(cx - 16, l.StageY + 13, cz + 58), new int3(72, 27, 52), Mat.DarkStone);
            WorldArtPrimitives.Ellipsoid(ref brush, new int3(cx - 14, l.StageY + 26, cz + 59), new int3(65, 13, 46), Mat.Dirt);

            WorldArtPrimitives.Ellipsoid(ref brush, new int3(cx + 24, l.PoolY, cz - 14), new int3(52, 13, 50), Mat.Empty);
            WorldArtPrimitives.Ellipsoid(ref brush, new int3(cx + 47, l.PoolY + 14, cz + 24), new int3(42, 11, 34), Mat.Empty);
            WorldArtPrimitives.Ellipsoid(ref brush, new int3(cx + 61, l.PoolY + 34, cz + 54), new int3(38, 10, 31), Mat.Empty);

            int archBase = l.StageY + 5;
            WorldArtPrimitives.RoundedBox(ref brush, new int3(cx - 93, archBase, cz - 1), new int3(78, 72, 18), 4, Mat.Stone);
            brush.Arch(new int3(cx - 71, archBase - 1, cz - 4), 25, 37, 24, 2, Mat.Empty);
            WorldArtPrimitives.Sphere(ref brush, new int3(cx - 86, archBase + 69, cz + 2), 9, Mat.Empty);
            WorldArtPrimitives.Sphere(ref brush, new int3(cx - 29, archBase + 68, cz + 1), 12, Mat.Empty);

            WorldArtPrimitives.RoundedBox(ref brush, new int3(cx - 91, l.StageY - 18, cz - 35), new int3(55, 44, 15), 3, Mat.Stone);
            brush.Arch(new int3(cx - 78, l.StageY - 20, cz - 38), 15, 23, 20, 2, Mat.Empty);

            for (int i = 0; i < 7; i++)
            {
                int x = cx - 44 + i * 14;
                int z = cz - 77 + (i % 2) * 3;
                WorldArtPrimitives.RoundedBox(ref brush, new int3(x, l.StageY + 2 + (i % 2), z), new int3(12, 8, 10), 2, Mat.Stone);
            }
            WorldArtPrimitives.Frustum(ref brush, cx + 92, l.StageY + 44, cz + 105, 16, 11, 66, Mat.Stone);

            WorldArtPrimitives.CoatExposedTops(ref brush, new int3(cx - 130, terrainY - 2, cz - 95), new int3(270, 190, 255), Mat.Grass, 2);
            brush.Weather(new int3(cx - 96, archBase - 3, cz - 5), new int3(86, 82, 27), Mat.Moss, 0x51554E4Cu, 25);
            return l;
        }

        private static Palette BuildPalette(List<UnityEngine.Object> owned)
        {
            var p = new Palette
            {
                Grass = MakeTextured("Sunlit Turf", new Color(0.34f, 0.52f, 0.15f), 13, 0.08f, 0.02f, owned),
                GrassShadow = MakeTextured("Deep Turf", new Color(0.21f, 0.35f, 0.10f), 17, 0.06f, 0.02f, owned),
                Earth = MakeTextured("Warm Earth", new Color(0.45f, 0.34f, 0.22f), 23, 0.07f, 0.01f, owned),
                Cliff = MakeTextured("Garden Rock", new Color(0.39f, 0.39f, 0.33f), 29, 0.06f, 0.02f, owned),
                Stone = MakeTextured("Warm Ruin Stone", new Color(0.66f, 0.61f, 0.50f), 31, 0.06f, 0.04f, owned),
                StoneLight = MakeTextured("Sunlit Pale Stone", new Color(0.77f, 0.72f, 0.61f), 37, 0.05f, 0.05f, owned),
                Moss = MakeTextured("Ruin Moss", new Color(0.25f, 0.42f, 0.12f), 41, 0.07f, 0.01f, owned),
                Bark = MakeTextured("Warm Bark", new Color(0.28f, 0.18f, 0.09f), 43, 0.08f, 0.01f, owned),
                Leaves = MakeTextured("Leaf Green", new Color(0.20f, 0.39f, 0.10f), 47, 0.06f, 0.01f, owned),
                LeavesLight = MakeTextured("Sunlit Leaves", new Color(0.38f, 0.56f, 0.15f), 53, 0.06f, 0.01f, owned),
                Roof = MakeTextured("Tower Roof", new Color(0.44f, 0.38f, 0.32f), 59, 0.04f, 0.10f, owned),
                FlowerWhite = MakeFlat("White Flowers", new Color(0.97f, 0.96f, 0.86f), 0.04f, owned),
                FlowerYellow = MakeFlat("Gold Flowers", new Color(0.93f, 0.68f, 0.14f), 0.04f, owned),
                FlowerPink = MakeFlat("Pink Flowers", new Color(0.90f, 0.44f, 0.52f), 0.04f, owned),
                FlowerBlue = MakeFlat("Blue Flowers", new Color(0.31f, 0.61f, 0.86f), 0.04f, owned),
                Cloud = MakeFlat("Cloud", new Color(0.98f, 0.98f, 0.96f), 0.02f, owned),
            };
            p.Water = MakeTransparent("Turquoise Pool", new Color(0.06f, 0.58f, 0.76f, 0.82f), 0.78f, 0.10f, owned);
            p.Waterfall = MakeTransparent("Sunlit Waterfall", new Color(0.67f, 0.91f, 0.98f, 0.82f), 0.58f, 0.24f, owned);
            p.Foam = MakeTransparent("Water Foam", new Color(0.94f, 0.99f, 1.00f, 0.68f), 0.25f, 0.30f, owned);
            return p;
        }

        private static void ApplyVoxelPalette(GameObject root, Palette p, List<UnityEngine.Object> owned)
        {
            Material stone = CloneMaterial(p.Stone, "Voxel Warm Stone", owned);
            Material dark = CloneMaterial(p.Cliff, "Voxel Cliff", owned);
            Material grass = CloneMaterial(p.GrassShadow, "Voxel Turf", owned);
            Material dirt = CloneMaterial(p.Earth, "Voxel Earth", owned);
            Material moss = CloneMaterial(p.Moss, "Voxel Moss", owned);
            Material wood = CloneMaterial(p.Bark, "Voxel Wood", owned);
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = renderer.gameObject.name.ToLowerInvariant();
                if (n.Contains("darkstone") || n.Contains("structural")) renderer.sharedMaterial = dark;
                else if (n.Contains("stone")) renderer.sharedMaterial = stone;
                else if (n.Contains("grass")) renderer.sharedMaterial = grass;
                else if (n.Contains("dirt")) renderer.sharedMaterial = dirt;
                else if (n.Contains("moss")) renderer.sharedMaterial = moss;
                else if (n.Contains("wood")) renderer.sharedMaterial = wood;
            }
        }

        private static void BuildPresentation(Transform root, in Layout l, Palette p, List<UnityEngine.Object> owned)
        {
            BuildTerrainSkins(root, in l, p);
            BuildAshlarRuins(root, in l, p);
            BuildWaterSystem(root, in l, p, owned);
            BuildTree(root, in l, p);
            BuildDistantCastle(root, in l, p, owned);
            BuildClouds(root, in l, p);
            ScatterFlowers(root, in l, p);
            ScatterRuinMossAndIvy(root, in l, p);
        }

        private static void BuildTerrainSkins(Transform root, in Layout l, Palette p)
        {
            AddMound(root, "Foreground rock skin", l.Origin + new Vector3(0f, -1.35f, -3.6f), new Vector3(7.2f, 2.15f, 5.9f), p.Cliff);
            AddMound(root, "Foreground turf", l.Origin + new Vector3(-0.15f, 0.10f, -3.7f), new Vector3(6.45f, 1.20f, 5.25f), p.Grass);
            AddMound(root, "Left bank rock", l.Origin + new Vector3(-7f, 0.1f, 1f), new Vector3(5.8f, 3.35f, 6f), p.Cliff);
            AddMound(root, "Left bank turf", l.Origin + new Vector3(-6.2f, 2.02f, 1.4f), new Vector3(5f, 1.75f, 5.1f), p.Grass);

            Vector3[] centres =
            {
                l.Origin + new Vector3(5.2f, -0.05f, -0.4f), l.Origin + new Vector3(6.4f, 1.95f, 2.8f),
                l.Origin + new Vector3(7.2f, 4.05f, 5.8f), l.Origin + new Vector3(7.8f, 6.25f, 8.6f),
            };
            Vector3[] scales = { new(5.5f,2.2f,4.9f), new(5.9f,2.25f,4.8f), new(6.3f,2.3f,4.7f), new(6.7f,2.35f,4.8f) };
            for (int i = 0; i < centres.Length; i++)
            {
                AddMound(root, $"Cascade rock {i}", centres[i] + new Vector3(0f,-0.9f,0f), scales[i], p.Cliff);
                AddMound(root, $"Cascade turf {i}", centres[i] + new Vector3(-0.25f,0.45f,0f), new Vector3(scales[i].x-0.55f,1.05f,scales[i].z-0.5f), p.Grass);
            }
            AddMound(root, "Mid garden rock", l.Origin + new Vector3(-1.6f,1.15f,5.8f), new Vector3(7.4f,2.45f,5.3f), p.Cliff);
            AddMound(root, "Mid garden turf", l.Origin + new Vector3(-1.4f,2.75f,5.9f), new Vector3(6.8f,1.25f,4.7f), p.Grass);
            AddMound(root, "Front island left", l.Origin + new Vector3(-5.1f,-0.8f,-6.8f), new Vector3(2.9f,1.3f,2.3f), p.Cliff);
            AddMound(root, "Front island left turf", l.Origin + new Vector3(-5.1f,0f,-6.8f), new Vector3(2.55f,0.72f,2f), p.Grass);
            AddMound(root, "Front island right", l.Origin + new Vector3(5.7f,-1f,-5.3f), new Vector3(3.1f,1.35f,2.5f), p.Cliff);
            AddMound(root, "Front island right turf", l.Origin + new Vector3(5.7f,-0.15f,-5.3f), new Vector3(2.7f,0.72f,2.15f), p.Grass);
        }

        private static void BuildAshlarRuins(Transform root, in Layout l, Palette p)
        {
            Vector3 centre = l.Origin + new Vector3(-5.8f, 4.1f, 0.4f);
            const float radius = 2.55f;
            for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < 7; row++)
                    AddAshlar(root, "Hero arch pier", centre + new Vector3(side*radius,-3.7f+row*0.78f,0f), new Vector3(0.92f,0.74f,0.86f), p.Stone, row*17 + (side>0?5:0));
            for (int i = 0; i < 12; i++)
            {
                if (i >= 10) continue;
                float a = Mathf.Lerp(180f,0f,i/11f) * Mathf.Deg2Rad;
                GameObject b = AddAshlar(root, "Hero arch ring", centre + new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0f), new Vector3(0.92f,0.72f,0.88f), i%4==0?p.StoneLight:p.Stone, 100+i);
                b.transform.rotation = Quaternion.Euler(0f,0f,-a*Mathf.Rad2Deg+90f);
            }

            Vector3 small = l.Origin + new Vector3(-6.5f,0.75f,-3f);
            const float sr = 1.45f;
            for (int side = -1; side <= 1; side += 2)
                for (int row=0; row<4; row++)
                    AddAshlar(root,"Lower arch pier",small+new Vector3(side*sr,-1.85f+row*0.58f,0f),new Vector3(0.65f,0.53f,0.72f),p.Stone,200+row+side);
            for (int i=0;i<9;i++)
            {
                float a=Mathf.Lerp(180f,0f,i/8f)*Mathf.Deg2Rad;
                GameObject b=AddAshlar(root,"Lower arch ring",small+new Vector3(Mathf.Cos(a)*sr,Mathf.Sin(a)*sr,0f),new Vector3(0.67f,0.50f,0.72f),p.Stone,240+i);
                b.transform.rotation=Quaternion.Euler(0f,0f,-a*Mathf.Rad2Deg+90f);
            }

            Vector3[] rubble = { l.Origin+new Vector3(-4f,0.45f,-7.2f), l.Origin+new Vector3(-2.8f,0.33f,-6.8f), l.Origin+new Vector3(3.5f,-0.2f,-5.1f), l.Origin+new Vector3(4.4f,-0.1f,-4.7f), l.Origin+new Vector3(6.2f,0.1f,-2.8f) };
            for (int i=0;i<rubble.Length;i++) AddAshlar(root,"Garden rubble",rubble[i],new Vector3(1f,0.62f,0.85f),i%2==0?p.StoneLight:p.Stone,300+i);
        }

        private static void BuildWaterSystem(Transform root, in Layout l, Palette p, List<UnityEngine.Object> owned)
        {
            AddPool(root,"Foreground turquoise channel",l.Origin+new Vector3(2.2f,-0.77f,-5f),6f,3.4f,p.Water,owned);
            AddPool(root,"Lower cascade pool",l.Origin+new Vector3(4.5f,0.42f,-1f),4.7f,2.6f,p.Water,owned);
            AddPool(root,"Middle cascade pool",l.Origin+new Vector3(5.9f,2.45f,2.6f),4f,2.3f,p.Water,owned);
            AddPool(root,"Upper cascade pool",l.Origin+new Vector3(7f,4.52f,5.8f),3.3f,2f,p.Water,owned);
            AddFall(root,l.Origin+new Vector3(4.9f,2.60f,0.55f),l.Origin+new Vector3(4.2f,0.63f,-0.55f),2.4f,p.Waterfall,p.Foam,owned);
            AddFall(root,l.Origin+new Vector3(6.2f,4.66f,4.2f),l.Origin+new Vector3(5.6f,2.62f,3.1f),2.15f,p.Waterfall,p.Foam,owned);
            AddFall(root,l.Origin+new Vector3(7.5f,6.77f,7.55f),l.Origin+new Vector3(6.8f,4.70f,6.2f),1.85f,p.Waterfall,p.Foam,owned);
            AddFall(root,l.Origin+new Vector3(8.9f,0.8f,-2f),l.Origin+new Vector3(8.5f,-1.4f,-3.6f),1f,p.Waterfall,p.Foam,owned);
        }

        private static void BuildTree(Transform root, in Layout l, Palette p)
        {
            Vector3 b=l.Origin+new Vector3(-8.2f,0.15f,-0.1f);
            AddCapsule(root,"Oak trunk",b,b+new Vector3(0.2f,6.3f,0.3f),0.62f,p.Bark);
            AddCapsule(root,"Oak left bough",b+new Vector3(0f,4.2f,0f),b+new Vector3(-3.2f,7.1f,0.5f),0.34f,p.Bark);
            AddCapsule(root,"Oak right bough",b+new Vector3(0.2f,4.6f,0.1f),b+new Vector3(3f,7.2f,1.2f),0.32f,p.Bark);
            Vector3[] crown={new(-2.7f,7.4f,0.2f),new(-1.3f,8.2f,0.3f),new(0.4f,8.4f,0.7f),new(2f,7.9f,1f),new(3f,7.1f,0.8f),new(-3.4f,6.6f,0.5f),new(-0.1f,7f,-0.4f),new(1.6f,6.8f,-0.2f)};
            for(int i=0;i<crown.Length;i++)
            {
                GameObject leaf=MakePrimitive(PrimitiveType.Sphere,"Oak canopy",i%3==0?p.LeavesLight:p.Leaves,root);
                leaf.transform.position=b+crown[i];
                leaf.transform.localScale=new Vector3(2.7f+(i%2)*0.5f,1.9f+(i%3)*0.25f,2.2f+(i%2)*0.4f);
            }
        }

        private static void BuildDistantCastle(Transform root,in Layout l,Palette p,List<UnityEngine.Object> owned)
        {
            Vector3 b=l.Origin+new Vector3(8f,6.7f,20f);
            AddTower(root,b,1.45f,7.5f,p.StoneLight,p.Roof,owned);
            AddTower(root,b+new Vector3(-2f,-1f,-0.4f),0.95f,5.7f,p.StoneLight,p.Roof,owned);
            AddTower(root,b+new Vector3(2f,-0.7f,0.5f),0.86f,5f,p.StoneLight,p.Roof,owned);
            AddTower(root,b+new Vector3(0.9f,1.3f,0.2f),0.56f,4.4f,p.StoneLight,p.Roof,owned);
            GameObject keep=MakePrimitive(PrimitiveType.Cube,"Distant castle keep",p.StoneLight,root);
            keep.transform.position=b+new Vector3(0f,1f,0.6f); keep.transform.localScale=new Vector3(5.1f,4.5f,2.7f);
        }

        private static void BuildClouds(Transform root,in Layout l,Palette p)
        {
            AddCloudCluster(root,l.Origin+new Vector3(1f,12.4f,27f),new Vector3(5.5f,2.2f,1.2f),p.Cloud);
            AddCloudCluster(root,l.Origin+new Vector3(11f,13f,31f),new Vector3(4.5f,1.9f,1.1f),p.Cloud);
            AddCloudCluster(root,l.Origin+new Vector3(-8f,10.3f,26f),new Vector3(3.8f,1.7f,1f),p.Cloud);
        }

        private static void ScatterFlowers(Transform root,in Layout l,Palette p)
        {
            Material[] petals={p.FlowerWhite,p.FlowerYellow,p.FlowerPink,p.FlowerBlue};
            Vector3[] a={new(-4.2f,0.75f,-5.2f),new(-2.9f,0.85f,-6.1f),new(-1.2f,0.92f,-4.8f),new(2.1f,0.58f,-4.1f),new(3.1f,0.66f,-3.1f),new(-6.2f,2.9f,-0.2f),new(-5f,3.1f,1f),new(4.9f,1.2f,-0.4f),new(6.2f,3.2f,2.7f)};
            for(int i=0;i<a.Length;i++) AddFlowerClump(root,l.Origin+a[i],petals[i%petals.Length],p.LeavesLight,i);
        }

        private static void ScatterRuinMossAndIvy(Transform root,in Layout l,Palette p)
        {
            Vector3[] moss={new(-7.8f,7.15f,0.2f),new(-6.2f,6.7f,0.2f),new(-4.6f,6.15f,0.2f),new(-8.1f,3.8f,0.25f),new(-4f,3.2f,0.25f),new(-6.8f,1.75f,-2.8f)};
            for(int i=0;i<moss.Length;i++)
            {
                GameObject patch=MakePrimitive(PrimitiveType.Sphere,"Moss cap",p.Moss,root);
                patch.transform.position=l.Origin+moss[i]; patch.transform.localScale=new Vector3(1.2f,0.22f,0.7f)*(0.8f+(i%3)*0.12f);
            }
            for(int strand=0;strand<3;strand++)
            {
                Vector3 start=l.Origin+new Vector3(-7.7f+strand*1.35f,6.75f-strand*0.15f,0.15f);
                for(int i=0;i<5+strand;i++)
                {
                    GameObject leaf=MakePrimitive(PrimitiveType.Sphere,"Arch ivy",p.Leaves,root);
                    leaf.transform.position=start+new Vector3(Mathf.Sin(i*1.4f)*0.12f,-i*0.42f,-0.22f); leaf.transform.localScale=new Vector3(0.32f,0.22f,0.12f);
                }
            }
        }

        private static void AddMound(Transform root,string name,Vector3 position,Vector3 scale,Material material)
        { GameObject go=MakePrimitive(PrimitiveType.Sphere,name,material,root); go.transform.position=position; go.transform.localScale=scale; }

        private static GameObject AddAshlar(Transform root,string name,Vector3 position,Vector3 scale,Material material,int seed)
        {
            GameObject b=MakePrimitive(PrimitiveType.Cube,name,material,root); b.transform.position=position;
            b.transform.localScale=scale*(0.96f+Hash01(seed)*0.07f);
            b.transform.rotation=Quaternion.Euler((Hash01(seed+9)-0.5f)*2.2f,(Hash01(seed+19)-0.5f)*4f,(Hash01(seed+27)-0.5f)*2.2f); return b;
        }

        private static void AddPool(Transform root,string name,Vector3 pos,float rx,float rz,Material material,List<UnityEngine.Object> owned)
        { Mesh m=BuildEllipseMesh(rx,rz,64); owned.Add(m); GameObject go=MakeMesh(name,m,material,root); go.transform.position=pos; }

        private static void AddFall(Transform root,Vector3 top,Vector3 bottom,float width,Material waterfall,Material foam,List<UnityEngine.Object> owned)
        {
            Mesh ribbon=BuildWaterfallRibbon(top,bottom,width,20); owned.Add(ribbon); MakeMesh("Waterfall ribbon",ribbon,waterfall,root);
            for(int i=0;i<6;i++) { GameObject puff=MakePrimitive(PrimitiveType.Sphere,"Waterfall foam",foam,root); float f=(i-2.5f)/5f; puff.transform.position=bottom+new Vector3(f*width*0.75f,0.05f+(i%2)*0.08f,((i*7)%3-1)*0.10f); puff.transform.localScale=new Vector3(width*0.28f,0.15f,0.34f); }
        }

        private static void AddTower(Transform root,Vector3 basePos,float radius,float height,Material stone,Material roof,List<UnityEngine.Object> owned)
        {
            GameObject tower=MakePrimitive(PrimitiveType.Cylinder,"Distant pale tower",stone,root); tower.transform.position=basePos+Vector3.up*height*0.5f; tower.transform.localScale=new Vector3(radius,height*0.5f,radius);
            Mesh cone=BuildConeMesh(radius*1.15f,height*0.42f,18); owned.Add(cone); GameObject roofGo=MakeMesh("Distant spire",cone,roof,root); roofGo.transform.position=basePos+Vector3.up*(height+height*0.20f);
        }

        private static void AddCloudCluster(Transform root,Vector3 centre,Vector3 scale,Material cloud)
        {
            Vector3[] offsets={new(-0.9f,0f,0f),new(-0.35f,0.35f,0f),new(0.25f,0.2f,0f),new(0.9f,-0.02f,0f),new(0.15f,-0.22f,0f)};
            for(int i=0;i<offsets.Length;i++) { GameObject puff=MakePrimitive(PrimitiveType.Sphere,"Storybook cloud",cloud,root); puff.transform.position=centre+Vector3.Scale(offsets[i],scale); puff.transform.localScale=new Vector3(scale.x*0.56f,scale.y*0.74f,scale.z); puff.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off; }
        }

        private static void AddFlowerClump(Transform root,Vector3 position,Material petals,Material stem,int seed)
        {
            for(int f=0;f<3;f++)
            {
                Vector3 q=position+new Vector3((Hash01(seed*17+f)-0.5f)*0.55f,0f,(Hash01(seed*29+f+5)-0.5f)*0.55f);
                AddCapsule(root,"Flower stem",q,q+Vector3.up*(0.25f+0.10f*f),0.025f,stem);
                for(int petal=0;petal<5;petal++) { float a=petal*Mathf.PI*2f/5f; GameObject s=MakePrimitive(PrimitiveType.Sphere,"Flower petal",petals,root); s.transform.position=q+Vector3.up*(0.28f+0.10f*f)+new Vector3(Mathf.Cos(a)*0.08f,0f,Mathf.Sin(a)*0.08f); s.transform.localScale=new Vector3(0.11f,0.045f,0.07f); }
            }
        }

        private static void AddCapsule(Transform root,string name,Vector3 a,Vector3 b,float radius,Material material)
        { Vector3 d=b-a; if(d.sqrMagnitude<0.0001f)return; GameObject c=MakePrimitive(PrimitiveType.Capsule,name,material,root); c.transform.position=(a+b)*0.5f; c.transform.rotation=Quaternion.FromToRotation(Vector3.up,d.normalized); c.transform.localScale=new Vector3(radius*2f,d.magnitude*0.5f,radius*2f); }

        private static void SetupLighting(out GameObject sunObject)
        {
            RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(0.48f,0.66f,0.86f); RenderSettings.ambientEquatorColor=new Color(0.50f,0.52f,0.45f); RenderSettings.ambientGroundColor=new Color(0.18f,0.20f,0.15f); RenderSettings.ambientIntensity=0.55f;
            RenderSettings.fog=true; RenderSettings.fogMode=FogMode.Linear; RenderSettings.fogColor=new Color(0.55f,0.76f,0.93f); RenderSettings.fogStartDistance=31f; RenderSettings.fogEndDistance=60f;
            sunObject=new GameObject("Warm storybook sun"); Light sun=sunObject.AddComponent<Light>(); sun.type=LightType.Directional; sun.color=new Color(1f,0.91f,0.72f); sun.intensity=0.95f; sun.shadows=LightShadows.Soft; sun.shadowStrength=0.50f; sunObject.transform.rotation=Quaternion.Euler(42f,-31f,0f);
        }

        private static void SetupCamera(in Layout l,out GameObject cameraObject,out Camera camera)
        {
            cameraObject=new GameObject("Sunlit Waterfall Environment Camera"); camera=cameraObject.AddComponent<Camera>(); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(0.13f,0.51f,0.86f,1f); camera.fieldOfView=35f; camera.nearClipPlane=0.1f; camera.farClipPlane=90f; camera.allowHDR=false; camera.allowMSAA=true;
            Vector3 focus=l.Origin+new Vector3(0.1f,3.6f,1.9f); cameraObject.transform.position=l.Origin+new Vector3(0.7f,4.5f,-20.2f); cameraObject.transform.LookAt(focus);
        }

        private static Material MakeTextured(string name,Color colour,int seed,float variation,float smoothness,List<UnityEngine.Object> owned)
        { Texture2D t=CreatePainterlyTexture(name+" Texture",colour,seed,variation); owned.Add(t); Material m=MakeFlat(name,Color.white,smoothness,owned); m.SetTexture("_MainTex",t); m.mainTextureScale=new Vector2(2f,2f); return m; }

        private static Material MakeFlat(string name,Color colour,float smoothness,List<UnityEngine.Object> owned)
        {
            Shader shader=Shader.Find("Standard")??Shader.Find("Universal Render Pipeline/Lit"); if(shader==null)throw new InvalidOperationException("No Standard/Lit shader available.");
            var m=new Material(shader){name=name}; m.SetColor("_Color",colour); m.SetColor("_BaseColor",colour); m.SetFloat("_Glossiness",smoothness); m.SetFloat("_Smoothness",smoothness); m.SetFloat("_Metallic",0f); owned.Add(m); return m;
        }

        private static Material MakeTransparent(string name,Color colour,float smoothness,float emission,List<UnityEngine.Object> owned)
        {
            Material m=MakeFlat(name,colour,smoothness,owned); m.SetFloat("_Mode",3f); m.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha); m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite",0); m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON"); m.renderQueue=(int)RenderQueue.Transparent;
            if(emission>0f){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(colour.r,colour.g,colour.b,1f)*emission);} return m;
        }

        private static Material CloneMaterial(Material source,string name,List<UnityEngine.Object> owned){var m=new Material(source){name=name};owned.Add(m);return m;}

        private static Texture2D CreatePainterlyTexture(string name,Color baseColour,int seed,float variation)
        {
            const int size=64; var t=new Texture2D(size,size,TextureFormat.RGBA32,false,false){name=name,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear}; var pixels=new Color32[size*size]; float ox=seed*0.173f,oy=seed*0.311f;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++){float broad=Mathf.PerlinNoise(ox+x/22f,oy+y/22f)-0.5f;float med=Mathf.PerlinNoise(oy+x/9f,ox+y/9f)-0.5f;float d=broad*variation+med*variation*0.30f;Color c=new Color(Mathf.Clamp01(baseColour.r+d),Mathf.Clamp01(baseColour.g+d),Mathf.Clamp01(baseColour.b+d),1f);pixels[x+y*size]=c;} t.SetPixels32(pixels);t.Apply(false,false);return t;
        }

        private static GameObject MakePrimitive(PrimitiveType type,string name,Material material,Transform root)
        { GameObject go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(root,false);Collider c=go.GetComponent<Collider>();if(c!=null)UnityEngine.Object.DestroyImmediate(c);MeshRenderer r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=material.renderQueue>=(int)RenderQueue.Transparent?ShadowCastingMode.Off:ShadowCastingMode.On;r.receiveShadows=true;return go; }

        private static GameObject MakeMesh(string name,Mesh mesh,Material material,Transform root)
        {var go=new GameObject(name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;MeshRenderer r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=material.renderQueue>=(int)RenderQueue.Transparent?ShadowCastingMode.Off:ShadowCastingMode.On;r.receiveShadows=true;return go;}

        private static Mesh BuildEllipseMesh(float rx,float rz,int segments)
        {
            var v=new Vector3[segments+1];var n=new Vector3[segments+1];var uv=new Vector2[segments+1];var tri=new int[segments*3];v[0]=Vector3.zero;n[0]=Vector3.up;uv[0]=new Vector2(0.5f,0.5f);
            for(int i=0;i<segments;i++){float a=i*Mathf.PI*2f/segments;float x=Mathf.Cos(a),z=Mathf.Sin(a);v[i+1]=new Vector3(x*rx,0f,z*rz);n[i+1]=Vector3.up;uv[i+1]=new Vector2(x*0.5f+0.5f,z*0.5f+0.5f);int next=(i+1)%segments;tri[i*3]=0;tri[i*3+1]=i+1;tri[i*3+2]=next+1;}var m=new Mesh{name="Sunlit pool ellipse"};m.vertices=v;m.normals=n;m.uv=uv;m.triangles=tri;m.RecalculateBounds();return m;
        }

        private static Mesh BuildWaterfallRibbon(Vector3 top,Vector3 bottom,float width,int segments)
        {
            var v=new Vector3[(segments+1)*2];var n=new Vector3[v.Length];var uv=new Vector2[v.Length];var tri=new int[segments*6];Vector3 d=bottom-top;Vector3 side=Vector3.Cross(d.normalized,Vector3.up);if(side.sqrMagnitude<0.001f)side=Vector3.right;side.Normalize();
            for(int i=0;i<=segments;i++){float t=i/(float)segments;Vector3 c=Vector3.Lerp(top,bottom,t);c+=side*(Mathf.Sin(t*9f)*width*0.035f+Mathf.Sin(t*19f)*width*0.012f);float w=width*(0.94f+Mathf.Sin(t*6f)*0.05f);int q=i*2;v[q]=c-side*w*0.5f;v[q+1]=c+side*w*0.5f;n[q]=Vector3.back;n[q+1]=Vector3.back;uv[q]=new Vector2(0f,t*3f);uv[q+1]=new Vector2(1f,t*3f);if(i<segments){int k=i*6;tri[k]=q;tri[k+1]=q+2;tri[k+2]=q+1;tri[k+3]=q+1;tri[k+4]=q+2;tri[k+5]=q+3;}}
            var m=new Mesh{name="Sunlit waterfall ribbon"};m.vertices=v;m.normals=n;m.uv=uv;m.triangles=tri;m.RecalculateBounds();return m;
        }

        private static Mesh BuildConeMesh(float radius,float height,int segments)
        {var v=new Vector3[segments+1];var tri=new int[segments*3];v[0]=Vector3.up*height*0.5f;for(int i=0;i<segments;i++){float a=i*Mathf.PI*2f/segments;v[i+1]=new Vector3(Mathf.Cos(a)*radius,-height*0.5f,Mathf.Sin(a)*radius);int next=(i+1)%segments;tri[i*3]=0;tri[i*3+1]=i+1;tri[i*3+2]=next+1;}var m=new Mesh{name="Storybook tower cone"};m.vertices=v;m.triangles=tri;m.RecalculateNormals();m.RecalculateBounds();return m;}

        private static float Hash01(int n){unchecked{uint x=(uint)n;x^=x>>16;x*=0x7feb352d;x^=x>>15;x*=0x846ca68b;x^=x>>16;return(x&0x00ffffff)/16777215f;}}
    }
}
