using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.AmbientLife.Api;
using VoxelEngine.Rendering.Runtime.AmbientLife;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Showcase;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.CI
{
    public sealed class VegetationRenderingTests
    {
        [UnityTest]
        public IEnumerator StandaloneShowcase_CoversCatalogue_AndDrawsThroughInstancedRenderer()
        {
            GameObject root = new GameObject("Vegetation Rendering Contract");
            try
            {
                VegetationRenderingShowcase showcase = root.AddComponent<VegetationRenderingShowcase>();
                yield return null;

                Assert.That(showcase.InstanceCount,
                    Is.EqualTo(VegetationCatalogue.Count * VegetationRenderingShowcase.InstancesPerKind));
                Assert.That(showcase.Renderer, Is.Not.Null);
                Assert.That(showcase.Renderer.InstanceCount, Is.EqualTo(showcase.InstanceCount));
                Assert.That(ProceduralVegetationMaterials.Ensure(), Is.True);

                bool[] seen = new bool[VegetationCatalogue.Count];
                for (int i = 0; i < showcase.Instances.Count; i++)
                    seen[(int)showcase.Instances[i].Kind] = true;
                for (int i = 0; i < seen.Length; i++)
                    Assert.That(seen[i], Is.True, $"Vegetation kind {VegetationCatalogue.KindAt(i)} was missing.");

                Assert.DoesNotThrow(showcase.Renderer.DrawNow);
                showcase.Renderer.Clear();
                Assert.That(showcase.Renderer.InstanceCount, Is.Zero);
                showcase.Rebuild();
                Assert.That(showcase.Renderer.InstanceCount, Is.EqualTo(showcase.InstanceCount));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }

    public sealed class AmbientLifeRenderingTests
    {
        [UnityTest]
        public IEnumerator StandaloneShowcase_CoversCatalogue_AndReconstructsAllAgents()
        {
            GameObject root = new GameObject("Ambient Life Rendering Contract");
            try
            {
                AmbientLifeRenderingShowcase showcase = root.AddComponent<AmbientLifeRenderingShowcase>();
                yield return null;

                Assert.That(showcase.ClusterCount, Is.EqualTo(AmbientLifeCatalogue.Count));
                Assert.That(showcase.AgentCount, Is.GreaterThan(80));
                Assert.That(showcase.Renderer, Is.Not.Null);
                Assert.That(showcase.Renderer.AgentCount, Is.EqualTo(showcase.AgentCount));
                Assert.That(ProceduralAmbientLifeMaterials.Shared, Is.Not.Null);

                bool[] seen = new bool[AmbientLifeCatalogue.Count];
                for (int i = 0; i < showcase.Clusters.Count; i++)
                    seen[(int)showcase.Clusters[i].Kind] = true;
                for (int i = 0; i < seen.Length; i++)
                    Assert.That(seen[i], Is.True, $"Ambient kind {AmbientLifeCatalogue.KindAt(i)} was missing.");

                Assert.DoesNotThrow(showcase.Renderer.DrawNow);
                showcase.Renderer.Clear();
                Assert.That(showcase.Renderer.AgentCount, Is.Zero);
                showcase.Rebuild();
                Assert.That(showcase.Renderer.AgentCount, Is.GreaterThan(80));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }

    [NUnit.Framework.Explicit("Visual-quality metrics and artefact capture; run by rendering CI.")]
    public sealed class VegetationLifeRenderingVisualTests
    {
        private const int Width = 1280;
        private const int Height = 720;
        private const float ForegroundDifferenceSquared = 0.0025f;

        [UnityTest]
        public IEnumerator VegetationShowcase_RendersReadableDiverseFullFrameComposition()
        {
            GameObject cameraObject = CreateCamera(
                "CI Vegetation Fidelity Camera",
                new Vector3(0f, 6.0f, -13.5f),
                new Vector3(0f, 1.65f, 6.3f),
                42f,
                out Camera camera,
                out RenderTexture target);
            GameObject root = new GameObject("CI Vegetation Fidelity Showcase");
            Texture2D background = null;
            Texture2D capture = null;

            try
            {
                VegetationRenderingShowcase showcase = root.AddComponent<VegetationRenderingShowcase>();
                yield return null;
                RemovePresentationGeometry(root.transform);
                showcase.Renderer.enabled = false;

                camera.Render();
                background = ReadTarget(target);
                showcase.Renderer.DrawNow();
                camera.Render();
                capture = ReadTarget(target);
                File.WriteAllBytes(ArtifactPath("vegetation_all_kinds.png"), capture.EncodeToPNG());

                ImageMetrics metrics = Analyse(capture, background);
                File.WriteAllText(ArtifactPath("vegetation_quality.txt"), metrics.Describe("vegetation"));

                float foregroundRatio = metrics.ForegroundPixels / (float)(Width * Height);
                float edgeRatio = metrics.DetailEdges / (float)Mathf.Max(1, metrics.ForegroundPixels);
                Assert.That(foregroundRatio, Is.GreaterThan(0.006f));
                Assert.That(foregroundRatio, Is.LessThan(0.40f));
                Assert.That(metrics.BoundsWidth, Is.GreaterThan(Width * 0.38f));
                Assert.That(metrics.BoundsHeight, Is.GreaterThan(Height * 0.24f));
                Assert.That(metrics.OccupiedTiles, Is.GreaterThanOrEqualTo(10));
                Assert.That(metrics.ColourBins, Is.GreaterThanOrEqualTo(10));
                Assert.That(metrics.HueBuckets, Is.GreaterThanOrEqualTo(5));
                Assert.That(edgeRatio, Is.GreaterThan(0.035f),
                    "Vegetation lacks sufficient local silhouette/detail edges.");
                Assert.That(metrics.LuminanceStdDev, Is.GreaterThan(0.045f));
                Assert.That(metrics.MaxTileConcentration, Is.LessThan(0.46f));
            }
            finally
            {
                if (background != null) UnityEngine.Object.DestroyImmediate(background);
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                ReleaseTarget(camera, target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator AmbientLifeShowcase_RendersDistributedDistinctVisibleAgents()
        {
            GameObject cameraObject = CreateCamera(
                "CI Ambient Life Fidelity Camera",
                new Vector3(0f, 5.2f, -11.8f),
                new Vector3(0f, 1.55f, 8.0f),
                50f,
                out Camera camera,
                out RenderTexture target);
            GameObject root = new GameObject("CI Ambient Life Fidelity Showcase");
            Texture2D background = null;
            Texture2D capture = null;

            try
            {
                AmbientLifeRenderingShowcase showcase = root.AddComponent<AmbientLifeRenderingShowcase>();
                yield return null;
                RemovePresentationGeometry(root.transform);
                showcase.Renderer.enabled = false;

                camera.Render();
                background = ReadTarget(target);
                showcase.Renderer.DrawNow();
                camera.Render();
                capture = ReadTarget(target);
                File.WriteAllBytes(ArtifactPath("ambient_life_all_kinds.png"), capture.EncodeToPNG());

                ImageMetrics metrics = Analyse(capture, background);
                File.WriteAllText(ArtifactPath("ambient_life_quality.txt"), metrics.Describe("ambient-life"));

                float edgeRatio = metrics.DetailEdges / (float)Mathf.Max(1, metrics.ForegroundPixels);
                Assert.That(metrics.ForegroundPixels, Is.GreaterThan(300));
                Assert.That(metrics.BoundsWidth, Is.GreaterThan(Width * 0.30f));
                Assert.That(metrics.BoundsHeight, Is.GreaterThan(Height * 0.13f));
                Assert.That(metrics.OccupiedTiles, Is.GreaterThanOrEqualTo(8));
                Assert.That(metrics.ColourBins, Is.GreaterThanOrEqualTo(8));
                Assert.That(metrics.HueBuckets, Is.GreaterThanOrEqualTo(4));
                Assert.That(edgeRatio, Is.GreaterThan(0.025f));
                Assert.That(metrics.LuminanceStdDev, Is.GreaterThan(0.055f));
                Assert.That(metrics.MaxTileConcentration, Is.LessThan(0.52f));
            }
            finally
            {
                if (background != null) UnityEngine.Object.DestroyImmediate(background);
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                ReleaseTarget(camera, target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        internal static GameObject CreateCamera(
            string name, Vector3 position, Vector3 focus, float fieldOfView,
            out Camera camera, out RenderTexture target)
        {
            GameObject cameraObject = new GameObject(name);
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.030f, 0.050f, 1f);
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            cameraObject.transform.position = position;
            cameraObject.transform.LookAt(focus);
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                name = name + " Target",
                antiAliasing = 1,
            };
            target.Create();
            camera.targetTexture = target;
            return cameraObject;
        }

        internal static void RemovePresentationGeometry(Transform root)
        {
            Transform ground = root.Find("Showcase Ground");
            if (ground != null) UnityEngine.Object.DestroyImmediate(ground.gameObject);
            Transform wall = root.Find("Showcase Vine Wall");
            if (wall != null) UnityEngine.Object.DestroyImmediate(wall.gameObject);
        }

        internal static Texture2D ReadTarget(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            texture.Apply(false, false);
            RenderTexture.active = previous;
            return texture;
        }

        internal static void ReleaseTarget(Camera camera, RenderTexture target)
        {
            if (target == null) return;
            if (camera != null) camera.targetTexture = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }

        internal static string ArtifactPath(string fileName)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string directory = Path.Combine(projectRoot, "Artifacts", "VegetationLifeVisual");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
        }

        internal static bool[] ForegroundMask(Texture2D capture, Texture2D background, out int count)
        {
            Color32[] pixels = capture.GetPixels32();
            Color32[] bg = background.GetPixels32();
            bool[] mask = new bool[pixels.Length];
            count = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color p = pixels[i];
                Color b = bg[i];
                float dr = p.r - b.r;
                float dg = p.g - b.g;
                float db = p.b - b.b;
                if (dr * dr + dg * dg + db * db < ForegroundDifferenceSquared) continue;
                mask[i] = true;
                count++;
            }
            return mask;
        }

        private static ImageMetrics Analyse(Texture2D capture, Texture2D background)
        {
            Color32[] pixels = capture.GetPixels32();
            bool[] mask = ForegroundMask(capture, background, out int foreground);
            bool[] colourBins = new bool[64];
            int[] tiles = new int[32];
            int hueMask = 0;
            int minX = Width, minY = Height, maxX = -1, maxY = -1;
            float sum = 0f, sumSq = 0f;

            for (int i = 0; i < pixels.Length; i++)
            {
                if (!mask[i]) continue;
                int x = i % Width;
                int y = i / Width;
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                tiles[Mathf.Clamp(y * 4 / Height, 0, 3) * 8 + Mathf.Clamp(x * 8 / Width, 0, 7)]++;

                Color p = pixels[i];
                int rb = Mathf.Clamp(Mathf.FloorToInt(p.r * 4f), 0, 3);
                int gb = Mathf.Clamp(Mathf.FloorToInt(p.g * 4f), 0, 3);
                int bb = Mathf.Clamp(Mathf.FloorToInt(p.b * 4f), 0, 3);
                colourBins[rb * 16 + gb * 4 + bb] = true;
                Color.RGBToHSV(p, out float h, out float s, out float v);
                if (s > 0.18f && v > 0.09f) hueMask |= 1 << Mathf.Clamp(Mathf.FloorToInt(h * 12f), 0, 11);
                float l = Luminance(p);
                sum += l; sumSq += l * l;
            }

            int edges = 0;
            for (int y = 1; y < Height; y++)
            {
                int row = y * Width;
                for (int x = 1; x < Width; x++)
                {
                    int i = row + x;
                    int left = i - 1;
                    int below = i - Width;
                    if (!mask[i] && !mask[left] && !mask[below]) continue;
                    float l = Luminance(pixels[i]);
                    if (Mathf.Max(
                            Mathf.Abs(l - Luminance(pixels[left])),
                            Mathf.Abs(l - Luminance(pixels[below]))) > 0.055f)
                        edges++;
                }
            }

            int occupied = 0, maxTile = 0, colours = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] > 0) occupied++;
                maxTile = Mathf.Max(maxTile, tiles[i]);
            }
            for (int i = 0; i < colourBins.Length; i++) if (colourBins[i]) colours++;
            int hues = CountBits(hueMask);
            float mean = foreground > 0 ? sum / foreground : 0f;
            float variance = foreground > 0 ? Mathf.Max(0f, sumSq / foreground - mean * mean) : 0f;

            return new ImageMetrics(
                foreground, colours, hues, edges, occupied,
                maxX >= minX ? maxX - minX + 1 : 0,
                maxY >= minY ? maxY - minY + 1 : 0,
                Mathf.Sqrt(variance),
                foreground > 0 ? maxTile / (float)foreground : 0f);
        }

        private static float Luminance(Color p) => p.r * 0.2126f + p.g * 0.7152f + p.b * 0.0722f;

        private static int CountBits(int value)
        {
            int count = 0;
            while (value != 0) { value &= value - 1; count++; }
            return count;
        }

        private readonly struct ImageMetrics
        {
            public readonly int ForegroundPixels;
            public readonly int ColourBins;
            public readonly int HueBuckets;
            public readonly int DetailEdges;
            public readonly int OccupiedTiles;
            public readonly int BoundsWidth;
            public readonly int BoundsHeight;
            public readonly float LuminanceStdDev;
            public readonly float MaxTileConcentration;

            public ImageMetrics(int foregroundPixels, int colourBins, int hueBuckets, int detailEdges,
                int occupiedTiles, int boundsWidth, int boundsHeight, float luminanceStdDev,
                float maxTileConcentration)
            {
                ForegroundPixels = foregroundPixels;
                ColourBins = colourBins;
                HueBuckets = hueBuckets;
                DetailEdges = detailEdges;
                OccupiedTiles = occupiedTiles;
                BoundsWidth = boundsWidth;
                BoundsHeight = boundsHeight;
                LuminanceStdDev = luminanceStdDev;
                MaxTileConcentration = maxTileConcentration;
            }

            public string Describe(string subject)
            {
                float edgeRatio = DetailEdges / (float)Mathf.Max(1, ForegroundPixels);
                return $"subject={subject}\nforeground_pixels={ForegroundPixels}\n" +
                       $"bounds={BoundsWidth}x{BoundsHeight}\noccupied_tiles={OccupiedTiles}/32\n" +
                       $"max_tile_concentration={MaxTileConcentration:0.0000}\n" +
                       $"hue_buckets={HueBuckets}/12\nquantized_colours={ColourBins}/64\n" +
                       $"detail_edge_pixels={DetailEdges}\ndetail_edge_ratio={edgeRatio:0.0000}\n" +
                       $"luminance_stddev={LuminanceStdDev:0.0000}\n";
            }
        }
    }

    [NUnit.Framework.Explicit("Ambient-life silhouette quality and artefact capture; run by rendering CI.")]
    public sealed class AmbientLifeSilhouetteQualityTests
    {
        private const int Width = 1280;
        private const int Height = 720;
        private const int MinimumReadableComponentPixels = 6;

        [UnityTest]
        public IEnumerator AmbientLifeShowcase_PreservesDistinctReadableAgentSilhouettes()
        {
            GameObject cameraObject = VegetationLifeRenderingVisualTests.CreateCamera(
                "CI Ambient Life Silhouette Camera",
                new Vector3(0f, 5.4f, -13.2f),
                new Vector3(0f, 1.55f, 8.2f),
                52f,
                out Camera camera,
                out RenderTexture target);
            GameObject root = new GameObject("CI Ambient Life Silhouette Showcase");
            Texture2D background = null;
            Texture2D capture = null;

            try
            {
                AmbientLifeRenderingShowcase showcase = root.AddComponent<AmbientLifeRenderingShowcase>();
                yield return null;
                VegetationLifeRenderingVisualTests.RemovePresentationGeometry(root.transform);
                showcase.Renderer.enabled = false;

                camera.Render();
                background = VegetationLifeRenderingVisualTests.ReadTarget(target);
                showcase.Renderer.DrawNow();
                camera.Render();
                capture = VegetationLifeRenderingVisualTests.ReadTarget(target);
                File.WriteAllBytes(
                    VegetationLifeRenderingVisualTests.ArtifactPath("ambient_life_silhouette.png"),
                    capture.EncodeToPNG());

                bool[] foreground = VegetationLifeRenderingVisualTests.ForegroundMask(
                    capture, background, out int foregroundPixels);
                ComponentMetrics metrics = AnalyseComponents(foreground, foregroundPixels);
                File.WriteAllText(
                    VegetationLifeRenderingVisualTests.ArtifactPath("ambient_life_silhouette_quality.txt"),
                    metrics.Describe(showcase.AgentCount, foregroundPixels));

                float separatedRatio = metrics.ReadableComponents / (float)showcase.AgentCount;
                float largestRatio = metrics.LargestComponentPixels / (float)Mathf.Max(1, foregroundPixels);
                Assert.That(separatedRatio, Is.GreaterThanOrEqualTo(0.35f));
                Assert.That(largestRatio, Is.LessThan(0.12f),
                    "A single merged ambient-life blob occupies too much of the rendered subject area.");
                Assert.That(metrics.WideComponents, Is.GreaterThanOrEqualTo(8));
                Assert.That(metrics.TallComponents, Is.GreaterThanOrEqualTo(4));
            }
            finally
            {
                if (background != null) UnityEngine.Object.DestroyImmediate(background);
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                VegetationLifeRenderingVisualTests.ReleaseTarget(camera, target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static ComponentMetrics AnalyseComponents(bool[] foreground, int foregroundPixels)
        {
            bool[] visited = new bool[foreground.Length];
            int[] queue = new int[foreground.Length];
            int readable = 0, wide = 0, tall = 0, largest = 0;

            for (int start = 0; start < foreground.Length; start++)
            {
                if (!foreground[start] || visited[start]) continue;
                int head = 0, tail = 0, pixels = 0;
                int minX = Width, minY = Height, maxX = -1, maxY = -1;
                queue[tail++] = start;
                visited[start] = true;

                while (head < tail)
                {
                    int index = queue[head++];
                    pixels++;
                    int x = index % Width;
                    int y = index / Width;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= Height) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx;
                            if (nx < 0 || nx >= Width) continue;
                            int n = ny * Width + nx;
                            if (!foreground[n] || visited[n]) continue;
                            visited[n] = true;
                            queue[tail++] = n;
                        }
                    }
                }

                if (pixels < MinimumReadableComponentPixels) continue;
                readable++;
                largest = Mathf.Max(largest, pixels);
                int width = maxX - minX + 1;
                int height = maxY - minY + 1;
                if (pixels >= 12 && width >= height * 1.35f) wide++;
                if (pixels >= 12 && height >= width * 1.35f) tall++;
            }

            return new ComponentMetrics(readable, wide, tall, largest);
        }

        private readonly struct ComponentMetrics
        {
            public readonly int ReadableComponents;
            public readonly int WideComponents;
            public readonly int TallComponents;
            public readonly int LargestComponentPixels;

            public ComponentMetrics(int readableComponents, int wideComponents, int tallComponents,
                int largestComponentPixels)
            {
                ReadableComponents = readableComponents;
                WideComponents = wideComponents;
                TallComponents = tallComponents;
                LargestComponentPixels = largestComponentPixels;
            }

            public string Describe(int agentCount, int foregroundPixels)
            {
                float separatedRatio = ReadableComponents / (float)Mathf.Max(1, agentCount);
                float largestRatio = LargestComponentPixels / (float)Mathf.Max(1, foregroundPixels);
                return $"agent_count={agentCount}\nforeground_pixels={foregroundPixels}\n" +
                       $"readable_components={ReadableComponents}\nseparated_agent_ratio={separatedRatio:0.0000}\n" +
                       $"wide_components={WideComponents}\ntall_components={TallComponents}\n" +
                       $"largest_component_pixels={LargestComponentPixels}\n" +
                       $"largest_component_ratio={largestRatio:0.0000}\n";
            }
        }
    }
}
