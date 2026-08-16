using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Anti-blob image-space contract for ambient life. The broad colour/coverage fidelity test can
    /// be satisfied by a field of overlapping luminous circles, so this test explicitly verifies
    /// that a useful fraction of reconstructed agents survive as distinct readable silhouettes and
    /// that the frame contains both wide winged forms and tall/drifting forms.
    /// </summary>
    [NUnit.Framework.Explicit("Ambient-life silhouette quality and artefact capture; run by rendering CI.")]
    public sealed class AmbientLifeSilhouetteQualityTests
    {
        private const int Width = 1280;
        private const int Height = 720;
        private const float ForegroundDifferenceSquared = 0.0025f;
        private const int MinimumReadableComponentPixels = 6;

        [UnityTest]
        public IEnumerator AmbientLifeShowcase_PreservesDistinctReadableAgentSilhouettes()
        {
            GameObject cameraObject = CreateCamera(out Camera camera, out RenderTexture target);
            GameObject root = new GameObject("CI Ambient Life Silhouette Showcase");
            Texture2D backgroundCapture = null;
            Texture2D capture = null;

            try
            {
                AmbientLifeRenderingShowcase showcase = root.AddComponent<AmbientLifeRenderingShowcase>();
                yield return null;

                RemovePresentationGeometry(root.transform);
                showcase.Renderer.enabled = false;
                Assert.That(showcase.AgentCount, Is.GreaterThan(80),
                    "Silhouette quality must exercise a populated ambient-life catalogue.");

                camera.Render();
                backgroundCapture = ReadTarget(target);

                showcase.Renderer.DrawNow();
                camera.Render();
                capture = ReadTarget(target);
                File.WriteAllBytes(ArtifactPath("ambient_life_silhouette.png"), capture.EncodeToPNG());

                bool[] foreground = BuildForegroundMask(capture, backgroundCapture, out int foregroundPixels);
                ComponentMetrics metrics = AnalyseComponents(foreground, foregroundPixels);
                File.WriteAllText(
                    ArtifactPath("ambient_life_silhouette_quality.txt"),
                    metrics.Describe(showcase.AgentCount, foregroundPixels));

                int minimumSeparated = Mathf.CeilToInt(showcase.AgentCount * 0.35f);
                float separatedRatio = metrics.ReadableComponents / (float)showcase.AgentCount;
                float largestRatio = metrics.LargestComponentPixels
                                     / (float)Mathf.Max(1, foregroundPixels);

                Assert.That(metrics.ReadableComponents, Is.GreaterThanOrEqualTo(minimumSeparated),
                    $"Only {metrics.ReadableComponents}/{showcase.AgentCount} ambient agents remain as " +
                    "separate readable silhouettes. Dense populations are merging into blobs.");
                Assert.That(separatedRatio, Is.GreaterThanOrEqualTo(0.35f),
                    "Ambient-life silhouette separation is below the minimum readable fraction.");
                Assert.That(largestRatio, Is.LessThan(0.12f),
                    "A single merged ambient-life blob occupies too much of the rendered subject area.");
                Assert.That(metrics.WideComponents, Is.GreaterThanOrEqualTo(8),
                    "Winged/darting ambient-life silhouettes are not reading as distinctly wide forms.");
                Assert.That(metrics.TallComponents, Is.GreaterThanOrEqualTo(4),
                    "Wisps, motes, and other vertical ambient-life silhouettes lack shape diversity.");
            }
            finally
            {
                if (backgroundCapture != null) Object.DestroyImmediate(backgroundCapture);
                if (capture != null) Object.DestroyImmediate(capture);
                if (target != null)
                {
                    camera.targetTexture = null;
                    target.Release();
                    Object.DestroyImmediate(target);
                }
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateCamera(out Camera camera, out RenderTexture target)
        {
            GameObject cameraObject = new GameObject("CI Ambient Life Silhouette Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.030f, 0.050f, 1f);
            camera.fieldOfView = 44f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            cameraObject.transform.position = new Vector3(0f, 4.8f, -10.0f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.45f, 7.0f));

            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                name = "CI Ambient Life Silhouette Target",
                antiAliasing = 1,
            };
            target.Create();
            camera.targetTexture = target;
            return cameraObject;
        }

        private static void RemovePresentationGeometry(Transform root)
        {
            Transform ground = root.Find("Showcase Ground");
            if (ground != null) Object.DestroyImmediate(ground.gameObject);
            Transform wall = root.Find("Showcase Vine Wall");
            if (wall != null) Object.DestroyImmediate(wall.gameObject);
        }

        private static Texture2D ReadTarget(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            texture.Apply(false, false);
            RenderTexture.active = previous;
            return texture;
        }

        private static bool[] BuildForegroundMask(
            Texture2D capture,
            Texture2D background,
            out int foregroundPixels)
        {
            Color32[] pixels = capture.GetPixels32();
            Color32[] backgroundPixels = background.GetPixels32();
            Assert.That(backgroundPixels.Length, Is.EqualTo(pixels.Length));

            bool[] mask = new bool[pixels.Length];
            foregroundPixels = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color p = pixels[i];
                Color bg = backgroundPixels[i];
                float dr = p.r - bg.r;
                float dg = p.g - bg.g;
                float db = p.b - bg.b;
                if (dr * dr + dg * dg + db * db < ForegroundDifferenceSquared)
                    continue;

                mask[i] = true;
                foregroundPixels++;
            }

            return mask;
        }

        private static ComponentMetrics AnalyseComponents(bool[] foreground, int foregroundPixels)
        {
            bool[] visited = new bool[foreground.Length];
            int[] queue = new int[foreground.Length];
            int readable = 0;
            int wide = 0;
            int tall = 0;
            int largest = 0;

            for (int start = 0; start < foreground.Length; start++)
            {
                if (!foreground[start] || visited[start]) continue;

                int head = 0;
                int tail = 0;
                queue[tail++] = start;
                visited[start] = true;
                int pixels = 0;
                int minX = Width;
                int minY = Height;
                int maxX = -1;
                int maxY = -1;

                while (head < tail)
                {
                    int index = queue[head++];
                    pixels++;
                    int x = index % Width;
                    int y = index / Width;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= Height) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx;
                            if (nx < 0 || nx >= Width) continue;
                            int neighbour = ny * Width + nx;
                            if (!foreground[neighbour] || visited[neighbour]) continue;
                            visited[neighbour] = true;
                            queue[tail++] = neighbour;
                        }
                    }
                }

                if (pixels < MinimumReadableComponentPixels) continue;
                readable++;
                if (pixels > largest) largest = pixels;

                int width = maxX - minX + 1;
                int height = maxY - minY + 1;
                if (pixels >= 12 && width >= height * 1.35f) wide++;
                if (pixels >= 12 && height >= width * 1.35f) tall++;
            }

            return new ComponentMetrics(readable, wide, tall, largest, foregroundPixels);
        }

        private static string ArtifactPath(string fileName)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string directory = Path.Combine(projectRoot, "Artifacts", "VegetationLifeVisual");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
        }

        private readonly struct ComponentMetrics
        {
            public readonly int ReadableComponents;
            public readonly int WideComponents;
            public readonly int TallComponents;
            public readonly int LargestComponentPixels;
            public readonly int ForegroundPixels;

            public ComponentMetrics(
                int readableComponents,
                int wideComponents,
                int tallComponents,
                int largestComponentPixels,
                int foregroundPixels)
            {
                ReadableComponents = readableComponents;
                WideComponents = wideComponents;
                TallComponents = tallComponents;
                LargestComponentPixels = largestComponentPixels;
                ForegroundPixels = foregroundPixels;
            }

            public string Describe(int agentCount, int foregroundPixels)
            {
                float separatedRatio = ReadableComponents / (float)Mathf.Max(1, agentCount);
                float largestRatio = LargestComponentPixels / (float)Mathf.Max(1, foregroundPixels);
                return
                    $"agent_count={agentCount}\n" +
                    $"foreground_pixels={foregroundPixels}\n" +
                    $"readable_components={ReadableComponents}\n" +
                    $"separated_agent_ratio={separatedRatio:0.0000}\n" +
                    $"wide_components={WideComponents}\n" +
                    $"tall_components={TallComponents}\n" +
                    $"largest_component_pixels={LargestComponentPixels}\n" +
                    $"largest_component_ratio={largestRatio:0.0000}\n";
            }
        }
    }
}
