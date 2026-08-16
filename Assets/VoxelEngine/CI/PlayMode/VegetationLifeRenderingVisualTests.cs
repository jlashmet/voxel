using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Image-space fidelity contracts for the lightweight vegetation and ambient-life renderers.
    /// These tests intentionally validate pixels rather than only renderer state: a renderer that
    /// submits zero visible geometry, collapses the catalogue to one colour, or projects everything
    /// into a tiny part of the frame must fail even if its semantic instance counts are correct.
    ///
    /// The PNG artefacts are retained for human review while the assertions provide deterministic
    /// CI gates for visibility, composition and palette diversity.
    /// </summary>
    [NUnit.Framework.Explicit("Visual-fidelity metrics and artefact capture; run by name in rendering CI.")]
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
                            "Fidelity test must exercise the broad vegetation catalogue, not a tiny sample.");

                // Draw synchronously into a deterministic camera target. LateUpdate also exercises
                // this path during normal PlayMode frames; DrawNow makes the capture independent of
                // render-loop ordering on the self-hosted runner.
                showcase.Renderer.DrawNow();
                camera.Render();
                capture = ReadTarget(target);
                File.WriteAllBytes(outputPath, capture.EncodeToPNG());

                ImageMetrics metrics = Analyse(capture, background);
                int imagePixels = Width * Height;

                Assert.That(metrics.ForegroundPixels, Is.GreaterThan(imagePixels * 0.0025f),
                            "Vegetation renderer produced an effectively empty frame.");
                Assert.That(metrics.HueBucketCount, Is.GreaterThanOrEqualTo(4),
                            "Vegetation collapsed to too little palette diversity; flowers/magical growth may be missing.");
                Assert.That(metrics.GreenPixels, Is.GreaterThan(350),
                            "Expected readable green foliage was not visible in the rendered frame.");
                Assert.That(metrics.WarmAccentPixels, Is.GreaterThan(80),
                            "Expected flower/warm-accent vegetation was not visibly distinguishable.");
                Assert.That(metrics.CoolAccentPixels, Is.GreaterThan(60),
                            "Expected magical/cool-accent vegetation was not visibly distinguishable.");
                Assert.That(metrics.BoundsWidth, Is.GreaterThan(Width * 0.32f),
                            "Vegetation composition collapsed into too narrow a screen-space strip.");
                Assert.That(metrics.BoundsHeight, Is.GreaterThan(Height * 0.22f),
                            "Vegetation composition has insufficient vertical readability.");
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

                Assert.That(metrics.ForegroundPixels, Is.GreaterThan(180),
                            "Ambient-life renderer produced an effectively empty frame.");
                Assert.That(metrics.HueBucketCount, Is.GreaterThanOrEqualTo(3),
                            "Ambient-life catalogue is not visually distinguishable enough in image space.");
                Assert.That(metrics.BrightSaturatedPixels, Is.GreaterThan(30),
                            "Luminous/colourful ambient agents are not visibly surviving the render path.");
                Assert.That(metrics.BoundsWidth, Is.GreaterThan(Width * 0.24f),
                            "Ambient agents collapsed into too narrow a screen-space region.");
                Assert.That(metrics.BoundsHeight, Is.GreaterThan(Height * 0.10f),
                            "Ambient agents collapsed into too short a screen-space region.");
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
            int foreground = 0;
            int green = 0;
            int warm = 0;
            int cool = 0;
            int brightSaturated = 0;
            int minX = Width;
            int minY = Height;
            int maxX = -1;
            int maxY = -1;
            int hueMask = 0;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color p = pixels[i];
                float dr = p.r - background.r;
                float dg = p.g - background.g;
                float db = p.b - background.b;
                if (dr * dr + dg * dg + db * db < 0.0064f)
                    continue;

                foreground++;
                int x = i % Width;
                int y = i / Width;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;

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
            }

            return new ImageMetrics(
                foreground,
                CountBits(hueMask),
                green,
                warm,
                cool,
                brightSaturated,
                maxX >= minX ? maxX - minX + 1 : 0,
                maxY >= minY ? maxY - minY + 1 : 0);
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
            public readonly int GreenPixels;
            public readonly int WarmAccentPixels;
            public readonly int CoolAccentPixels;
            public readonly int BrightSaturatedPixels;
            public readonly int BoundsWidth;
            public readonly int BoundsHeight;

            public ImageMetrics(
                int foregroundPixels,
                int hueBucketCount,
                int greenPixels,
                int warmAccentPixels,
                int coolAccentPixels,
                int brightSaturatedPixels,
                int boundsWidth,
                int boundsHeight)
            {
                ForegroundPixels = foregroundPixels;
                HueBucketCount = hueBucketCount;
                GreenPixels = greenPixels;
                WarmAccentPixels = warmAccentPixels;
                CoolAccentPixels = coolAccentPixels;
                BrightSaturatedPixels = brightSaturatedPixels;
                BoundsWidth = boundsWidth;
                BoundsHeight = boundsHeight;
            }
        }
    }
}
