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
                brush.FillBulk(new int3(cx - 42, baseY, cz - 24),
                    new int3(84, 72, 48), Mat.Empty);
                WorldArtPrimitives.RoundedBox(ref brush,
                    new int3(cx - 34, baseY - 4, cz - 18),
                    new int3(68, 5, 36), 2, Mat.Stone);

                WorldArtVoxelArchSpec spec = WorldArtVoxelArchSpec.Hero(
                    new int3(cx, baseY, cz), Mat.Stone, Mat.Empty, seed, Mat.DarkStone);
                spec.Damage = WorldArtVoxelArchDamage.Intact;
                WorldArtVoxelArchSockets sockets = WorldArtVoxelArchitecture.ArchBay(ref brush, in spec);
                WorldArtVoxelSocket[] semanticSockets =
                    WorldArtVoxelArchSocketLibrary.Build(in spec, in sockets);

                if (semanticSockets.Length < 8)
                    throw new InvalidOperationException("Voxel hero arch emitted too few semantic sockets.");
                if (brush.BudgetExceeded)
                    throw new InvalidOperationException("Voxel hero arch exceeded VoxelBrush budget.");

                var surfaceProfiles = new VoxelSurfaceProfileSet()
                    .Set(Mat.Stone, VoxelSurfaceProfile.DressedStone)
                    .Set(Mat.DarkStone, VoxelSurfaceProfile.RecessedMasonryJoint)
                    .Set(Mat.Moss, new VoxelSurfaceProfile(
                        smoothing: 0.90f,
                        densityBias: -0.004f,
                        planarization: 0.45f,
                        planarizationThreshold: 0.92f,
                        distanceRecovery: 0.75f,
                        curveRecovery: 0.65f));

                world.DirtyRegions.Add(RegionCoord);
                voxelSurface = new VoxelHeroSurfaceRenderer(
                    new int3(cx - 42, baseY - 5, cz - 20),
                    new int3(84, 72, 40),
                    surfaceProfiles)
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

                ApplyVoxelPalette(voxelSurface.Root, in spec, owned);
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
                    "geometry=VoxelBrush -> material-aware bounded voxel extraction\n" +
                    "surfaceProfile=stone:s1.00/dr1.00/cr0.72/p0.78 joint:bias-0.085\n" +
                    $"archivoltProjectionVoxels={spec.ArchivoltProjection}\n" +
                    "masonryDetail=component-driven seams + recessed joint geometry + deterministic stone relief\n" +
                    "unityPresentationMeshes=0\n" +
                    "voxelSizeMetres=0.10\n" +
                    "heroSurfaceSampleMetres=0.05\n" +
                    $"semanticSocketCount={semanticSockets.Length}\n" +
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

        private static void ApplyVoxelPalette(GameObject root, in WorldArtVoxelArchSpec spec,
                                              List<UnityEngine.Object> owned)
        {
            Shader shader = Shader.Find("VoxelEngine/SunlitSmooth");
            if (shader == null) throw new InvalidOperationException("SunlitSmooth shader not found.");

            Material stone = MakeStone(shader, "Hero cut limestone",
                new Color(0.69f, 0.64f, 0.53f),
                new Color(0.56f, 0.51f, 0.43f),
                new Color(0.82f, 0.77f, 0.66f));
            ConfigureArchMasonry(stone, in spec);
            owned.Add(stone);

            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                renderer.sharedMaterial = stone;
        }

        private static Material MakeStone(Shader shader, string name, Color baseColor,
            Color secondary, Color top)
        {
            Material m = new Material(shader) { name = name };
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Textures/Stylized/stone_color.png");
            if (texture != null) m.SetTexture("_MainTex", texture);
            SetColor(m, "_BaseColor", baseColor);
            SetColor(m, "_SecondaryColor", secondary);
            SetColor(m, "_TopColor", top);
            SetFloat(m, "_SurfaceKind", 2f);
            SetFloat(m, "_TextureScale", 0.48f);
            SetFloat(m, "_TextureStrength", 0.20f);
            SetFloat(m, "_DetailScale", 0.72f);
            SetFloat(m, "_DetailStrength", 0.038f);
            SetFloat(m, "_TopStrength", 0.16f);
            SetFloat(m, "_RimStrength", 0.006f);
            SetFloat(m, "_Smoothness", 0.012f);
            SetFloat(m, "_StoneReliefStrength", 0.76f);
            SetFloat(m, "_StoneJointRelief", 0.88f);
            SetFloat(m, "_StoneBlockVariation", 0.72f);
            SetFloat(m, "_StoneWeathering", 0.46f);
            return m;
        }

        private static void ConfigureArchMasonry(Material material, in WorldArtVoxelArchSpec spec)
        {
            float s = VoxelSurfaceRenderer.VoxelSize;
            int halfOpening = math.max(4, spec.HalfOpening);
            int pierHeight = math.max(8, spec.PierHeight);
            int pierWidth = math.max(4, spec.PierWidth);
            int courseHeight = math.max(3, spec.CourseHeight);
            int ringThickness = math.max(3, spec.RingThickness);
            int depth = math.max(4, spec.Depth);
            int outerRadius = halfOpening + ringThickness;
            int springY = spec.BaseCentre.y + pierHeight;
            int plinthHeight = math.max(3, courseHeight - 1);
            int shaftY = spec.BaseCentre.y + plinthHeight;
            int pierOffset = halfOpening + (pierWidth + 1) / 2;
            int frontZ = spec.BaseCentre.z - depth / 2;
            int projection = math.clamp(spec.ArchivoltProjection > 0 ? spec.ArchivoltProjection : 2,
                                        1, depth - 2);
            int backingFrontZ = frontZ + projection;

            SetFloat(material, "_ArchSeams", 1f);
            SetColor(material, "_ArchJointColor", new Color(0.305f, 0.278f, 0.235f, 1f));
            SetVector(material, "_ArchCenterSpring", new Vector4(
                spec.BaseCentre.x * s,
                springY * s,
                (frontZ - 0.5f) * s,
                0f));
            SetVector(material, "_ArchRadii", new Vector4(
                halfOpening * s,
                outerRadius * s,
                15f,
                0.019f));
            SetVector(material, "_ArchPier", new Vector4(
                pierOffset * s,
                pierWidth * s,
                courseHeight * s,
                shaftY * s));
            SetVector(material, "_ArchVertical", new Vector4(
                spec.BaseCentre.y * s,
                springY * s,
                (backingFrontZ - 0.5f) * s,
                depth * s));
        }

        private static void SetupLighting(out GameObject keyObject, out GameObject fillObject)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.34f, 0.40f);
            RenderSettings.ambientEquatorColor = new Color(0.21f, 0.22f, 0.22f);
            RenderSettings.ambientGroundColor = new Color(0.075f, 0.075f, 0.070f);
            RenderSettings.ambientIntensity = 0.50f;
            RenderSettings.fog = false;

            keyObject = new GameObject("Voxel arch warm key");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1.0f, 0.88f, 0.70f);
            key.intensity = 1.38f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.76f;
            keyObject.transform.rotation = Quaternion.Euler(34f, -40f, 0f);

            fillObject = new GameObject("Voxel arch cool fill");
            Light fill = fillObject.AddComponent<Light>();
            fill.color = new Color(0.60f, 0.70f, 0.86f);
            fill.type = LightType.Directional;
            fill.intensity = 0.25f;
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
            camera.backgroundColor = new Color(0.15f, 0.17f, 0.20f, 1f);
            camera.fieldOfView = 31f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            cameraObject.transform.position = focus + new Vector3(4.5f, 2.6f, -10.4f);
            cameraObject.transform.LookAt(focus + new Vector3(0f, 0.12f, 0f));
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        private static void SetVector(Material material, string property, Vector4 value)
        {
            if (material.HasProperty(property)) material.SetVector(property, value);
        }
    }
}
