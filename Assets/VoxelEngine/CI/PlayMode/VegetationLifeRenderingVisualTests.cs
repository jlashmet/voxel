using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Image-space quality contracts for the lightweight vegetation and ambient-life renderers.
    ///
    /// These tests are deliberately stronger than "something rendered" checks. A frame must have
    /// enough visible subject area, colour variety, local edge detail, tonal separation and spatial
    /// distribution to be considered a reasonable rendering. The checks remain metric-based rather
    /// than exact-pixel golden comparisons so small shader/platform changes do not make CI brittle.
    ///
    /// PNG and metric artefacts are retained for human review. Once an approved art-direction
    /// baseline exists, a perceptual golden-image comparison can be layered on top of these gates.
    /// </summary>
    [NUnit.Framework.Explicit("Visual-quality metrics and artefact capture; run by name in rendering CI.")]
    public sealed class VegetationLifeRenderingVisualTests
    {
        private const int Width = 1280;
        private const int Height = 720;

        [UnityTest]
        public IEnumerator VegetationShowcase_RendersReadableDiverseFullFrameComposition()
        {
            string outputPath = ArtifactPath("vegetation_all_kinds.png");
            var background = new Color(0.055f, 0.070f, 0.095f, 1f);
            GameObject cameraObject = CreateCamera(
                "CI Vegetation Fidelity Camera",
                background,
                new Vector3(0f, 7.2f, -17.5f),
                new Vector3(0f, 1.7f, 6.2f),
                out Camera camera,
                out RenderTexture target);
            GameObject root = new GameObject("CI Vegetation Fidelity Showcase");

            Texture2D capture = null;
            try
            {
                VegetationRenderingShowcase showcase = root.AddComponent<VegetationRenderingShowcase>();
                yield return null;

                RemovePresentationGeometry(root.transform);
                Assert.That(showcase.InstanceCount, Is.GreaterThan(100),
                    "Quality test must exercise the broad vegetation catalogue, not a tiny sample.");

                showcase.Renderer.DrawNow();
                camera.Render();
                capture = ReadTarget(target);
                File.WriteAllBytes(outputPath, capture.EncodeToPNG());

                ImageMetrics metrics = Analyse(capture, background);
                float qualityScore = VegetationQualityScore(metrics);
                File.WriteAllText(ArtifactPath("vegetation_quality.txt"), metrics.Describe("vegetation", qualityScore));

                int imagePixels = Width * Height;
                float foregroundRatio = metrics.ForegroundPixels / (float)imagePixels;
                float edgeRatio = metrics.DetailEdgePixels / (float)Mathf.Max(1, metrics.ForegroundPixels);

                // Catastrophic-regression gates: these should never be traded off by a composite score.
                Assert.That(foregroundRatio, Is.GreaterThan(0.006f),
                    "Vegetation occupies too little of the frame to be visually readable.");
                Assert.That(metrics.BoundsWidth, Is.GreaterThan(Width * 0.38f),
                    "Vegetation composition collapsed into too narrow a screen-space strip.");
                Assert.That(metrics.BoundsHeight, Is.GreaterThan(Height * 0.24f),
                    "Vegetation composition has insufficient vertical readability.");
                Assert.That(metrics.OccupiedTiles, Is.GreaterThanOrEqualTo(10),
                    "Vegetation is not distributed across enough of the image.");
                Assert.That(metrics.MaxTileConcentration, Is.LessThan(0.46f),
                    "Too much vegetation collapsed into a single screen region.");

                // Quality gates: catch flat blobs, monochrome output and overly soft/featureless rendering.
                Assert.That(metrics.HueBucketCount, Is.GreaterThanOrEqualTo(5),
                    "Vegetation palette is too narrow; catalogue variety is not reading in image space.");
                Assert.That(metrics.QuantizedColorCount, Is.GreaterThanOrEqualTo(10),
                    "Vegetation lacks enough distinct rendered colour/tonal groups.");
                Assert.That(metrics.GreenPixels, Is.GreaterThan(500),
                    "Expected readable green foliage was not visible in the rendered frame.");
                Assert.That(metrics.WarmAccentPixels, Is.GreaterThan(100),
                    "Expected flower/warm-accent vegetation was not visibly distinguishable.");
                Assert.That(metrics.CoolAccentPixels, Is.GreaterThan(80),
                    "Expected magical/cool-accent vegetation was not visibly distinguishable.");
                Assert.That(metrics.LuminanceStdDev, Is.GreaterThan(0.045f),
                    "Vegetation render is too tonally flat to have reasonable depth/readability.");
                Assert.That(metrics.LuminanceRange, Is.GreaterThan(0.18f),
                    "Vegetation render lacks a useful dark-to-light range.");
                Assert.That(edgeRatio, Is.GreaterThan(0.035f),
                    "Vegetation render is too soft/simple; insufficient local silhouette/detail edges are visible.");
                Assert.That(metrics.LeftPixels, Is.GreaterThan(metrics.ForegroundPixels * 0.08f));
                Assert.That(metrics.RightPixels, Is.GreaterThan(metrics.ForegroundPixels * 0.08f));
                Assert.That(metrics.CenterPixels, Is.GreaterThan(metrics.ForegroundPixels * 0.18f));

                Assert.That(qualityScore, Is.GreaterThanOrEqualTo(68f),
                    $"Vegetation image quality score {qualityScore:0.0}/100 is below the minimum acceptable level. " +
                    "See vegetation_quality.txt and vegetation_all_kinds.png in the CI artefact.");
            }
            finally
            {
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

        [UnityTest]
        public IEnumerator AmbientLifeShowcase_RendersDistributedDistinctVisibleAgents()
        {
            string outputPath = ArtifactPath("ambient_life_all_kinds.png");
            var background = new Color(0.025f, 0.030f, 0.050f, 1f);
            GameObject cameraObject = CreateCamera(
                "CI Ambient Life Fidelity Camera",
                background,
                new Vector3(0f, 5.2f, -11.5f),
                new Vector3(0f, 1.3f, 7.0f),
                out Camera camera,
                out RenderTexture target);
            GameObject root = new GameObject("CI Ambient Life Fidelity Showcase");

            Texture2D capture = null;
            try
            {
                AmbientLifeRenderingShowcase showcase = root.AddComponent<AmbientLifeRenderingShowcase>();
                yield return null;

                RemovePresentationGeometry(root.transform);
                Assert.That(showcase.ClusterCount, Is.GreaterThanOrEqualTo(12));
                Assert.That(showcase.AgentCount, Is.GreaterThan(80));

                showcase.Renderer.DrawNow();
                camera.Render();
                capture = ReadTarget(target);
                File.WriteAllBytes(outputPath, capture.EncodeToPNG());

                ImageMetrics metrics = Analyse(capture, background);
                float qualityScore = AmbientLifeQualityScore(metrics);
                File.WriteAllText(ArtifactPath("ambient_life_quality.txt"), metrics.Describe("ambient-life", qualityScore));

                float edgeRatio = metrics.DetailEdgePixels / (float)Mathf.Max(1, metrics.ForegroundPixels);

                Assert.That(metrics.ForegroundPixels, Is.GreaterThan(300),
                    "Ambient-life renderer produced an effectively empty or unreadably sparse frame.");
                Assert.That(metrics.HueBucketCount, Is.GreaterThanOrEqualTo(4),
                    "Ambient-life catalogue is not visually distinguishable enough in image space.");
                Assert.That(metrics.QuantizedColorCount, Is.GreaterThanOrEqualTo(8),
                    "Ambient-life render has too little colour/tonal variety.");
                Assert.That(metrics.BrightSaturatedPixels, Is.GreaterThan(50),
                    "Luminous/colourful ambient agents are not visibly surviving the render path.");
                Assert.That(metrics.BoundsWidth, Is.GreaterThan(Width * 0.30f),
                    "Ambient agents collapsed into too narrow a screen-space region.");
                Assert.That(metrics.BoundsHeight, Is.GreaterThan(Height * 0.13f),
                    "Ambient agents collapsed into too short a screen-space region.");
                Assert.That(metrics.OccupiedTiles, Is.GreaterThanOrEqualTo(8),
                    "Ambient agents are not spatially distributed enough to read as ambient life.");
                Assert.That(metrics.MaxTileConcentration, Is.LessThan(0.52f),
                    "Ambient agents are excessively clumped into one screen region.");
                Assert.That(metrics.LuminanceStdDev, Is.GreaterThan(0.055f),
                    "Ambient-life render is too tonally flat to read as luminous moving agents.");
                Assert.That(metrics.LuminanceRange, Is.GreaterThan(0.22f),
                    "Ambient-life render lacks sufficient luminous contrast.");
                Assert.That(edgeRatio, Is.GreaterThan(0.025f),
                    "Ambient-life agents lack enough crisp visible silhouette/detail.");
                Assert.That(metrics.LeftPixels, Is.GreaterThan(metrics.ForegroundPixels * 0.05f));
                Assert.That(metrics.RightPixels, Is.GreaterThan(metrics.ForegroundPixels * 0.05f));

                Assert.That(qualityScore, Is.GreaterThanOrEqualTo(65f),
                    $"Ambient-life image quality score {qualityScore:0.0}/100 is below the minimum acceptable level. " +
                    "See ambient_life_quality.txt and ambient_life_all_kinds.png in the CI artefact.");
            }
            finally
            {
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

        private static float VegetationQualityScore(ImageMetrics m)
        {
            int imagePixels = Width * Height;
            float coverage = m.ForegroundPixels / (float)imagePixels;
            float edgeRatio = m.DetailEdgePixels / (float)Mathf.Max(1, m.ForegroundPixels);

            float score = 0f;
            score += 16f * Saturate(coverage / 0.018f);
            score += 12f * Saturate(m.HueBucketCount / 7f);
            score += 10f * Saturate(m.QuantizedColorCount / 18f);
            score += 18f * Saturate(edgeRatio / 0.075f);
            score += 14f * Saturate(m.LuminanceStdDev / 0.10f);
            score += 10f * Saturate(m.LuminanceRange / 0.45f);
            score += 12f * Saturate(m.OccupiedTiles / 18f);
            score += 8f * Saturate((0.50f - m.MaxTileConcentration) / 0.28f);
            return score;
        }

        private static float AmbientLifeQualityScore(ImageMetrics m)
        {
            float edgeRatio = m.DetailEdgePixels / (float)Mathf.Max(1, m.ForegroundPixels);
            float brightRatio = m.BrightSaturatedPixels / (float)Mathf.Max(1, m.ForegroundPixels);

            float score = 0f;
            score += 12f * Saturate(m.ForegroundPixels / 1500f);
            score += 12f * Saturate(m.HueBucketCount / 6f);
            score += 10f * Saturate(m.QuantizedColorCount / 14f);
            score += 16f * Saturate(brightRatio / 0.22f);
            score += 16f * Saturate(edgeRatio / 0.07f);
            score += 14f * Saturate(m.LuminanceStdDev / 0.12f);
            score += 10f * Saturate(m.LuminanceRange / 0.55f);
            score += 10f * Saturate(m.OccupiedTiles / 14f);
            return score;
        }

        private static float Saturate(float value)
        {
            return Mathf.Clamp01(value);
        }

        private static string ArtifactPath(string fileName)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string directory = Path.Combine(projectRoot, "Artifacts", "VegetationLifeVisual");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
        }

        private static GameObject CreateCamera(
            string name,
            Color background,
            Vector3 position,
            Vector3 focus,
            out Camera camera,
            out RenderTexture target)
        {
            GameObject cameraObject = new GameObject(name);
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.fieldOfView = 48f;
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

        private static ImageMetrics Analyse(Texture2D texture, Color background)
        {
            Color32[] pixels = texture.GetPixels32();
            bool[] foregroundMask = new bool[pixels.Length];
            int[] tilePixels = new int[8 * 4];
            bool[] colourBins = new bool[4 * 4 * 4];

            int foreground = 0;
            int green = 0;
            int warm = 0;
            int cool = 0;
            int brightSaturated = 0;
            int left = 0;
            int center = 0;
            int right = 0;
            int minX = Width;
            int minY = Height;
            int maxX = -1;
            int maxY = -1;
            int hueMask = 0;
            float luminanceSum = 0f;
            float luminanceSquaredSum = 0f;
            float minLuminance = 1f;
            float maxLuminance = 0f;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color p = pixels[i];
                float dr = p.r - background.r;
                float dg = p.g - background.g;
                float db = p.b - background.b;
                if (dr * dr + dg * dg + db * db < 0.0064f)
                    continue;

                foregroundMask[i] = true;
                foreground++;
                int x = i % Width;
                int y = i / Width;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;

                if (x < Width / 3) left++;
                else if (x >= Width * 2 / 3) right++;
                else center++;

                int tileX = Mathf.Clamp(x * 8 / Width, 0, 7);
                int tileY = Mathf.Clamp(y * 4 / Height, 0, 3);
                tilePixels[tileY * 8 + tileX]++;

                int rBin = Mathf.Clamp(Mathf.FloorToInt(p.r * 4f), 0, 3);
                int gBin = Mathf.Clamp(Mathf.FloorToInt(p.g * 4f), 0, 3);
                int bBin = Mathf.Clamp(Mathf.FloorToInt(p.b * 4f), 0, 3);
                colourBins[(rBin * 16) + (gBin * 4) + bBin] = true;

                Color.RGBToHSV(p, out float h, out float s, out float v);
                if (s > 0.18f && v > 0.09f)
                {
                    int bucket = Mathf.Clamp(Mathf.FloorToInt(h * 12f), 0, 11);
                    hueMask |= 1 << bucket;
                }

                if (s > 0.18f && v > 0.10f && h >= 0.18f && h <= 0.48f) green++;
                if (s > 0.24f && v > 0.16f && (h <= 0.17f || h >= 0.91f)) warm++;
                if (s > 0.24f && v > 0.16f && h >= 0.50f && h <= 0.83f) cool++;
                if (s > 0.28f && v > 0.42f) brightSaturated++;

                float luminance = Luminance(p);
                luminanceSum += luminance;
                luminanceSquaredSum += luminance * luminance;
                if (luminance < minLuminance) minLuminance = luminance;
                if (luminance > maxLuminance) maxLuminance = luminance;
            }

            int detailEdges = 0;
            for (int y = 1; y < Height; y++)
            {
                int row = y * Width;
                for (int x = 1; x < Width; x++)
                {
                    int i = row + x;
                    int leftIndex = i - 1;
                    int belowIndex = i - Width;
                    if (!foregroundMask[i] && !foregroundMask[leftIndex] && !foregroundMask[belowIndex])
                        continue;

                    float l = Luminance(pixels[i]);
                    float horizontal = Mathf.Abs(l - Luminance(pixels[leftIndex]));
                    float vertical = Mathf.Abs(l - Luminance(pixels[belowIndex]));
                    if (Mathf.Max(horizontal, vertical) > 0.055f)
                        detailEdges++;
                }
            }

            int occupiedTiles = 0;
            int maxTilePixels = 0;
            for (int i = 0; i < tilePixels.Length; i++)
            {
                if (tilePixels[i] > 0) occupiedTiles++;
                if (tilePixels[i] > maxTilePixels) maxTilePixels = tilePixels[i];
            }

            int quantizedColours = 0;
            for (int i = 0; i < colourBins.Length; i++)
            {
                if (colourBins[i]) quantizedColours++;
            }

            float mean = foreground > 0 ? luminanceSum / foreground : 0f;
            float variance = foreground > 0
                ? Mathf.Max(0f, (luminanceSquaredSum / foreground) - (mean * mean))
                : 0f;
            float stdDev = Mathf.Sqrt(variance);
            float luminanceRange = foreground > 0 ? maxLuminance - minLuminance : 0f;
            float maxTileConcentration = foreground > 0 ? maxTilePixels / (float)foreground : 0f;

            return new ImageMetrics(
                foreground,
                CountBits(hueMask),
                quantizedColours,
                green,
                warm,
                cool,
                brightSaturated,
                detailEdges,
                occupiedTiles,
                left,
                center,
                right,
                maxX >= minX ? maxX - minX + 1 : 0,
                maxY >= minY ? maxY - minY + 1 : 0,
                mean,
                stdDev,
                luminanceRange,
                maxTileConcentration);
        }

        private static float Luminance(Color p)
        {
            return p.r * 0.2126f + p.g * 0.7152f + p.b * 0.0722f;
        }

        private static int CountBits(int value)
        {
            int count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }
            return count;
        }

        private readonly struct ImageMetrics
        {
            public readonly int ForegroundPixels;
            public readonly int HueBucketCount;
            public readonly int QuantizedColorCount;
            public readonly int GreenPixels;
            public readonly int WarmAccentPixels;
            public readonly int CoolAccentPixels;
            public readonly int BrightSaturatedPixels;
            public readonly int DetailEdgePixels;
            public readonly int OccupiedTiles;
            public readonly int LeftPixels;
            public readonly int CenterPixels;
            public readonly int RightPixels;
            public readonly int BoundsWidth;
            public readonly int BoundsHeight;
            public readonly float MeanLuminance;
            public readonly float LuminanceStdDev;
            public readonly float LuminanceRange;
            public readonly float MaxTileConcentration;

            public ImageMetrics(
                int foregroundPixels,
                int hueBucketCount,
                int quantizedColorCount,
                int greenPixels,
                int warmAccentPixels,
                int coolAccentPixels,
                int brightSaturatedPixels,
                int detailEdgePixels,
                int occupiedTiles,
                int leftPixels,
                int centerPixels,
                int rightPixels,
                int boundsWidth,
                int boundsHeight,
                float meanLuminance,
                float luminanceStdDev,
                float luminanceRange,
                float maxTileConcentration)
            {
                ForegroundPixels = foregroundPixels;
                HueBucketCount = hueBucketCount;
                QuantizedColorCount = quantizedColorCount;
                GreenPixels = greenPixels;
                WarmAccentPixels = warmAccentPixels;
                CoolAccentPixels = coolAccentPixels;
                BrightSaturatedPixels = brightSaturatedPixels;
                DetailEdgePixels = detailEdgePixels;
                OccupiedTiles = occupiedTiles;
                LeftPixels = leftPixels;
                CenterPixels = centerPixels;
                RightPixels = rightPixels;
                BoundsWidth = boundsWidth;
                BoundsHeight = boundsHeight;
                MeanLuminance = meanLuminance;
                LuminanceStdDev = luminanceStdDev;
                LuminanceRange = luminanceRange;
                MaxTileConcentration = maxTileConcentration;
            }

            public string Describe(string subject, float qualityScore)
            {
                float foregroundRatio = ForegroundPixels / (float)(Width * Height);
                float edgeRatio = DetailEdgePixels / (float)Mathf.Max(1, ForegroundPixels);
                return
                    $"subject={subject}\n" +
                    $"quality_score={qualityScore:0.00}\n" +
                    $"foreground_pixels={ForegroundPixels}\n" +
                    $"foreground_ratio={foregroundRatio:0.0000}\n" +
                    $"bounds={BoundsWidth}x{BoundsHeight}\n" +
                    $"occupied_tiles={OccupiedTiles}/32\n" +
                    $"max_tile_concentration={MaxTileConcentration:0.0000}\n" +
                    $"left_center_right={LeftPixels},{CenterPixels},{RightPixels}\n" +
                    $"hue_buckets={HueBucketCount}/12\n" +
                    $"quantized_colours={QuantizedColorCount}/64\n" +
                    $"green_pixels={GreenPixels}\n" +
                    $"warm_accent_pixels={WarmAccentPixels}\n" +
                    $"cool_accent_pixels={CoolAccentPixels}\n" +
                    $"bright_saturated_pixels={BrightSaturatedPixels}\n" +
                    $"detail_edge_pixels={DetailEdgePixels}\n" +
                    $"detail_edge_ratio={edgeRatio:0.0000}\n" +
                    $"mean_luminance={MeanLuminance:0.0000}\n" +
                    $"luminance_stddev={LuminanceStdDev:0.0000}\n" +
                    $"luminance_range={LuminanceRange:0.0000}\n";
            }
        }
    }
}
