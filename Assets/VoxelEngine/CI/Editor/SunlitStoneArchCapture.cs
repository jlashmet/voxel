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
    /// Close hero review for the real destructible voxel architecture path. No presentation meshes
    /// are used for the arch or its stones: VoxelBrush writes world data and the bounded hero
    /// surface extractor derives the visible masonry directly from that authoritative voxel field.
    /// </summary>
    public static class SunlitStoneArchCapture
    {
        private const int Width = 1120;
        private const int Height = 1376;
        private static readonly int3 RegionCoord = new(1, 0, 0);

        public static void Run()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outDir = Path.Combine(projectRoot, "Artifacts", "WorldArtKit");
            Directory.CreateDirectory(outDir);

            ShowcaseWorld world = null;
            VoxelHeroSurfaceRenderer voxelSurface = null;
            GameObject cameraObject = null;
            GameObject keyObject = null;
            GameObject fillObject = null;
            RenderTexture target = null;
            Texture2D capture = null;
            var owned = new List<UnityEngine.Object>();

            try
            {
                const uint seed = 0xA341u;
                world = new ShowcaseWorld(seed, 64_000, 1, 2);
                world.GenerateRegionBlocking(RegionCoord);

                int cx = RegionCoord.x * ShowcaseWorld.RegionVoxelEdge + ShowcaseWorld.RegionVoxelEdge / 2;
                int cz = ShowcaseWorld.RegionVoxelEdge / 2;
                int terrainY = world.SurfaceHeight(cx, cz);
                int baseY = terrainY + 5;

                var brush = new VoxelBrush(world.Table, world.Pool, 4_000_000);

                // Isolate the hero asset from generated terrain so every visible architectural
                // decision in this capture comes from the reusable voxel builder.
                brush.FillBulk(new int3(cx - 42, baseY, cz - 24),
                    new int3(84, 72, 48), Mat.Empty);
                WorldArtPrimitives.RoundedBox(ref brush,
                    new int3(cx - 34, baseY - 4, cz - 18),
                    new int3(68, 5, 36), 2, Mat.DarkStone);

                WorldArtVoxelArchSpec spec = WorldArtVoxelArchSpec.Hero(
                    new int3(cx, baseY, cz), Mat.Stone, Mat.Empty, seed);
                spec.Damage = WorldArtVoxelArchDamage.Intact;
                WorldArtVoxelArchSockets sockets = WorldArtVoxelArchitecture.ArchBay(ref brush, in spec);

                if (brush.BudgetExceeded)
                    throw new InvalidOperationException("Voxel hero arch exceeded VoxelBrush budget.");

                world.DirtyRegions.Add(RegionCoord);
                // Render only the architecture review volume at a 5 cm extraction lattice. The
                // source remains the 10 cm destructible world; this is visual surface quality, not
                // a second authored representation.
                voxelSurface = new VoxelHeroSurfaceRenderer(
                    new int3(cx - 42, baseY - 5, cz - 20),
                    new int3(84, 72, 40))
                {
                    CastShadows = true
                };
                for (int i = 0; i < 8; i++)
                {
                    voxelSurface.Sync(world, 400.0);
                    if (voxelSurface.PendingRebuilds == 0) break;
                }
                if (voxelSurface.RegionMeshCount == 0 || voxelSurface.VertexCount == 0)
                    throw new InvalidOperationException("Voxel hero arch produced no smooth surface geometry.");

                ApplyVoxelPalette(voxelSurface.Root, owned);
                SetupLighting(out keyObject, out fillObject);
                SetupCamera(sockets, out cameraObject, out Camera camera);

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "AAA Voxel Arch Review",
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
                    "capture=AAA voxel-only arch hero study\n" +
                    "geometry=VoxelBrush -> bounded smooth voxel extraction\n" +
                    "unityPresentationMeshes=0\n" +
                    "voxelSizeMetres=0.10\n" +
                    "heroSurfaceSampleMetres=0.05\n" +
                    $"seed=0x{seed:X}\n" +
                    $"baseY={baseY}\n" +
                    $"voxelWrites={brush.VoxelsWritten}\n" +
                    $"bulkVoxelWrites={brush.BulkVoxelsWritten}\n" +
                    $"surfaceVertices={voxelSurface.VertexCount}\n";
                File.WriteAllText(Path.Combine(outDir, "sunlit-cleric.txt"), metadata);
                Debug.Log($"CI voxel arch hero study written to {outDir}\n{metadata}");
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
                voxelSurface?.Dispose();
                world?.Dispose();
            }
        }

        private static void ApplyVoxelPalette(GameObject root, List<UnityEngine.Object> owned)
        {
            Shader shader = Shader.Find("VoxelEngine/SunlitSmooth");
            if (shader == null) throw new InvalidOperationException("SunlitSmooth shader not found.");

            Material stone = MakeStone(shader, "Hero voxel limestone",
                new Color(0.70f, 0.65f, 0.54f),
                new Color(0.53f, 0.49f, 0.41f),
                new Color(0.84f, 0.79f, 0.68f));
            Material baseStone = MakeStone(shader, "Hero voxel plinth stone",
                new Color(0.34f, 0.35f, 0.32f),
                new Color(0.25f, 0.26f, 0.24f),
                new Color(0.42f, 0.43f, 0.39f));
            owned.Add(stone);
            owned.Add(baseStone);

            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = renderer.gameObject.name.ToLowerInvariant();
                renderer.sharedMaterial = n.Contains("darkstone") || n.Contains("structural")
                    ? baseStone
                    : stone;
            }
        }

        private static Material MakeStone(Shader shader, string name, Color baseColor,
            Color secondary, Color top)
        {
            Material m = new Material(shader) { name = name };
            SetColor(m, "_BaseColor", baseColor);
            SetColor(m, "_SecondaryColor", secondary);
            SetColor(m, "_TopColor", top);
            SetFloat(m, "_SurfaceKind", 2f);
            SetFloat(m, "_TextureScale", 0.22f);
            SetFloat(m, "_TextureStrength", 0.30f);
            SetFloat(m, "_DetailScale", 0.050f);
            SetFloat(m, "_DetailStrength", 0.075f);
            SetFloat(m, "_TopStrength", 0.22f);
            SetFloat(m, "_RimStrength", 0.020f);
            SetFloat(m, "_Smoothness", 0.025f);
            return m;
        }

        private static void SetupLighting(out GameObject keyObject, out GameObject fillObject)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.34f, 0.38f, 0.44f);
            RenderSettings.ambientEquatorColor = new Color(0.25f, 0.26f, 0.25f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.10f, 0.095f);
            RenderSettings.ambientIntensity = 0.62f;
            RenderSettings.fog = false;

            keyObject = new GameObject("Voxel arch warm key");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1.0f, 0.88f, 0.70f);
            key.intensity = 1.20f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.74f;
            keyObject.transform.rotation = Quaternion.Euler(34f, -40f, 0f);

            fillObject = new GameObject("Voxel arch cool fill");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.62f, 0.72f, 0.88f);
            fill.intensity = 0.30f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(22f, 132f, 0f);
        }

        private static void SetupCamera(WorldArtVoxelArchSockets sockets,
            out GameObject cameraObject, out Camera camera)
        {
            float s = VoxelSurfaceRenderer.VoxelSize;
            Vector3 opening = new Vector3(sockets.Opening.x, sockets.Opening.y, sockets.Opening.z) * s;
            Vector3 crown = new Vector3(sockets.Crown.x, sockets.Crown.y, sockets.Crown.z) * s;
            Vector3 focus = Vector3.Lerp(opening, crown, 0.35f);

            cameraObject = new GameObject("AAA Voxel Arch Study Camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.17f, 0.19f, 0.22f, 1f);
            camera.fieldOfView = 31f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            cameraObject.transform.position = focus + new Vector3(5.8f, 2.8f, -9.8f);
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
