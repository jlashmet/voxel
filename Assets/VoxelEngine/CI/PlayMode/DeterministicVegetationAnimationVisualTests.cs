using System.Collections;
using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.CI
{
    [NUnit.Framework.Explicit("Deterministic shader-time vegetation animation validation; run by animation CI.")]
    public sealed class DeterministicVegetationAnimationVisualTests
    {
        private const string UseClock = "_UseValidationAnimationTime";
        private const string Clock = "_ValidationAnimationTime";

        [UnityTest]
        public IEnumerator VegetationAnimation_UsesAnchoredWindAndDeterministicSurfaceShimmer()
        {
            try
            {
                yield return ValidateGrass();
                yield return ValidateVine();
                yield return ValidateSurfaceShimmer();
            }
            finally
            {
                Shader.SetGlobalFloat(UseClock, 0f);
                Shader.SetGlobalFloat(Clock, 0f);
            }
        }

        private static IEnumerator ValidateGrass()
        {
            GameObject cameraObject = VegetationLifeRenderingVisualTests.CreateCamera(
                "CI Deterministic Grass Camera",
                new Vector3(0f, 1.15f, -3.4f),
                new Vector3(0f, 0.72f, 0f),
                30f,
                out Camera camera,
                out RenderTexture target);
            GameObject root = new GameObject("CI Deterministic Grass");
            Texture2D background = null, first = null, second = null;

            try
            {
                VegetationRenderingShowcase showcase = root.AddComponent<VegetationRenderingShowcase>();
                yield return null;
                VegetationLifeRenderingVisualTests.RemovePresentationGeometry(root.transform);
                showcase.Renderer.enabled = false;
                showcase.Renderer.Clear();
                showcase.Renderer.SetInstances(new[]
                {
                    Instance(VegetationKind.Grass, new float3(0,0,0), new float3(0,1,0), 0x51A55EEDu, 2f)
                });

                camera.Render();
                background = VegetationLifeRenderingVisualTests.ReadTarget(target);
                SetTime(0f); showcase.Renderer.DrawNow(); camera.Render();
                first = VegetationLifeRenderingVisualTests.ReadTarget(target);
                SetTime(0.70f); showcase.Renderer.DrawNow(); camera.Render();
                second = VegetationLifeRenderingVisualTests.ReadTarget(target);

                File.WriteAllBytes(VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_grass_deterministic_t0.png"), first.EncodeToPNG());
                File.WriteAllBytes(VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_grass_deterministic_t070.png"), second.EncodeToPNG());

                VerticalMotion motion = AnalyseVertical(first, second, background, 0.22f, 0.55f);
                File.WriteAllText(VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_grass_deterministic_motion.txt"), motion.Describe("base", "tip"));
                Assert.That(motion.UpperRate, Is.GreaterThan(0.06f), "Grass tips are not visibly moving at deterministic times.");
                Assert.That(motion.LowerRate, Is.LessThan(0.12f), "Grass roots are sliding instead of staying anchored.");
                Assert.That(motion.UpperRate, Is.GreaterThan(motion.LowerRate * 1.4f));
            }
            finally
            {
                Destroy(first); Destroy(second); Destroy(background);
                VegetationLifeRenderingVisualTests.ReleaseTarget(camera, target);
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(root);
            }
        }

        private static IEnumerator ValidateVine()
        {
            GameObject cameraObject = VegetationLifeRenderingVisualTests.CreateCamera(
                "CI Deterministic Vine Camera",
                new Vector3(0f, 1.4f, -4.2f),
                new Vector3(0f, 1.15f, 0f),
                32f,
                out Camera camera,
                out RenderTexture target);
            GameObject root = new GameObject("CI Deterministic Vine");
            Texture2D background = null;
            const int sampleCount = 7;
            const float sampleWindowSeconds = 5f;
            Texture2D[] samples = new Texture2D[sampleCount];
            float[] sampleTimes = new float[sampleCount];

            try
            {
                VegetationRenderingShowcase showcase = root.AddComponent<VegetationRenderingShowcase>();
                yield return null;
                VegetationLifeRenderingVisualTests.RemovePresentationGeometry(root.transform);
                showcase.Renderer.enabled = false;
                showcase.Renderer.Clear();
                showcase.Renderer.SetInstances(new[]
                {
                    Instance(VegetationKind.HangingVine, new float3(0,2.4f,0), new float3(0,0,-1), 0xA11CE551u, 1.35f)
                });

                camera.Render();
                background = VegetationLifeRenderingVisualTests.ReadTarget(target);

                for (int i = 0; i < sampleCount; i++)
                {
                    float time = sampleWindowSeconds * i / (sampleCount - 1f);
                    sampleTimes[i] = time;
                    SetTime(time);
                    showcase.Renderer.DrawNow();
                    camera.Render();
                    samples[i] = VegetationLifeRenderingVisualTests.ReadTarget(target);
                }

                VerticalMotion best = new VerticalMotion(0, 0, 0, 0);
                int bestA = 0, bestB = 1;
                for (int a = 0; a < sampleCount - 1; a++)
                {
                    for (int b = a + 1; b < sampleCount; b++)
                    {
                        VerticalMotion candidate = AnalyseVertical(samples[a], samples[b], background, 0.46f, 0.78f);
                        if (candidate.LowerRate <= best.LowerRate) continue;
                        best = candidate;
                        bestA = a;
                        bestB = b;
                    }
                }

                File.WriteAllBytes(
                    VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_vine_deterministic_a.png"),
                    samples[bestA].EncodeToPNG());
                File.WriteAllBytes(
                    VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_vine_deterministic_b.png"),
                    samples[bestB].EncodeToPNG());
                File.WriteAllText(
                    VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_vine_deterministic_motion.txt"),
                    $"time_a={sampleTimes[bestA]:0.000}\ntime_b={sampleTimes[bestB]:0.000}\n" + best.Describe("free_end", "attachment"));

                Assert.That(best.LowerRate, Is.GreaterThan(0.05f), "Vine free end does not visibly sway across its deterministic wind envelope.");
                Assert.That(best.UpperRate, Is.LessThan(0.14f), "Vine attachment slides too much.");
                Assert.That(best.LowerRate, Is.GreaterThan(best.UpperRate * 1.25f));
            }
            finally
            {
                for (int i = 0; i < samples.Length; i++) Destroy(samples[i]);
                Destroy(background);
                VegetationLifeRenderingVisualTests.ReleaseTarget(camera, target);
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(root);
            }
        }

        private static IEnumerator ValidateSurfaceShimmer()
        {
            GameObject cameraObject = VegetationLifeRenderingVisualTests.CreateCamera(
                "CI Deterministic Surface Shimmer Camera",
                new Vector3(0f, 2.7f, -3.4f),
                new Vector3(0f, 0.05f, 0f),
                34f,
                out Camera camera,
                out RenderTexture target);
            GameObject root = new GameObject("CI Deterministic Surface Shimmer");
            Texture2D background = null, luminous0 = null, luminous1 = null, mundane0 = null, mundane1 = null;

            try
            {
                VegetationRenderingShowcase showcase = root.AddComponent<VegetationRenderingShowcase>();
                yield return null;
                VegetationLifeRenderingVisualTests.RemovePresentationGeometry(root.transform);
                showcase.Renderer.enabled = false;

                camera.Render();
                background = VegetationLifeRenderingVisualTests.ReadTarget(target);

                showcase.Renderer.Clear();
                showcase.Renderer.SetInstances(new[]
                {
                    Instance(VegetationKind.StarMoss, new float3(0,0,0), new float3(0,1,0), 0x57A2B055u, 2.2f)
                });
                SetTime(0f); showcase.Renderer.DrawNow(); camera.Render(); luminous0 = VegetationLifeRenderingVisualTests.ReadTarget(target);
                SetTime(1.30f); showcase.Renderer.DrawNow(); camera.Render(); luminous1 = VegetationLifeRenderingVisualTests.ReadTarget(target);

                showcase.Renderer.Clear();
                showcase.Renderer.SetInstances(new[]
                {
                    Instance(VegetationKind.Moss, new float3(0,0,0), new float3(0,1,0), 0x57A2B055u, 2.2f)
                });
                SetTime(0f); showcase.Renderer.DrawNow(); camera.Render(); mundane0 = VegetationLifeRenderingVisualTests.ReadTarget(target);
                SetTime(1.30f); showcase.Renderer.DrawNow(); camera.Render(); mundane1 = VegetationLifeRenderingVisualTests.ReadTarget(target);

                File.WriteAllBytes(VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_starmoss_shimmer_t0.png"), luminous0.EncodeToPNG());
                File.WriteAllBytes(VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_starmoss_shimmer_t130.png"), luminous1.EncodeToPNG());

                int luminousChanged = CountChangedForeground(luminous0, luminous1, background, 0.00018f, out int luminousForeground);
                int mundaneChanged = CountChangedForeground(mundane0, mundane1, background, 0.00018f, out int mundaneForeground);
                float luminousRate = luminousChanged / (float)Mathf.Max(1, luminousForeground);
                float mundaneRate = mundaneChanged / (float)Mathf.Max(1, mundaneForeground);
                File.WriteAllText(
                    VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_surface_shimmer.txt"),
                    $"luminous_foreground={luminousForeground}\nluminous_changed={luminousChanged}\nluminous_changed_rate={luminousRate:0.0000}\n" +
                    $"mundane_foreground={mundaneForeground}\nmundane_changed={mundaneChanged}\nmundane_changed_rate={mundaneRate:0.0000}\n");

                Assert.That(luminousForeground, Is.GreaterThan(80));
                Assert.That(luminousRate, Is.GreaterThan(0.12f), "Luminous surface vegetation does not visibly shimmer over time.");
                Assert.That(mundaneRate, Is.LessThan(0.015f), "Non-luminous surface vegetation changes despite having no temporal animation policy.");
                Assert.That(luminousRate, Is.GreaterThan(mundaneRate * 4f));
            }
            finally
            {
                Destroy(background); Destroy(luminous0); Destroy(luminous1); Destroy(mundane0); Destroy(mundane1);
                VegetationLifeRenderingVisualTests.ReleaseTarget(camera, target);
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(root);
            }
        }

        private static VegetationInstance Instance(VegetationKind kind, float3 position, float3 normal, uint seed, float scale)
            => new VegetationInstance { Kind = kind, PositionMetres = position, SurfaceNormal = normal, Seed = seed, Scale = scale };

        private static void SetTime(float time)
        {
            Shader.SetGlobalFloat(UseClock, 1f);
            Shader.SetGlobalFloat(Clock, time);
        }

        private static VerticalMotion AnalyseVertical(Texture2D first, Texture2D second, Texture2D background, float lowerMax, float upperMin)
        {
            bool[] aMask = VegetationLifeRenderingVisualTests.ForegroundMask(first, background, out _);
            bool[] bMask = VegetationLifeRenderingVisualTests.ForegroundMask(second, background, out _);
            Color32[] a = first.GetPixels32(); Color32[] b = second.GetPixels32();
            int width = first.width, height = first.height, minY = height, maxY = -1;
            for (int i = 0; i < aMask.Length; i++) if (aMask[i] || bMask[i]) { int y = i / width; minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
            Assert.That(maxY, Is.GreaterThanOrEqualTo(minY), "No vegetation foreground was captured.");
            float span = Mathf.Max(1f, maxY - minY);
            int lowerPixels = 0, lowerChanged = 0, upperPixels = 0, upperChanged = 0;
            for (int i = 0; i < aMask.Length; i++)
            {
                if (!aMask[i] && !bMask[i]) continue;
                float y = (i / width - minY) / span;
                bool changed = PixelDistanceSquared(a[i], b[i]) > 0.0025f;
                if (y <= lowerMax) { lowerPixels++; if (changed) lowerChanged++; }
                if (y >= upperMin) { upperPixels++; if (changed) upperChanged++; }
            }
            return new VerticalMotion(lowerPixels, lowerChanged, upperPixels, upperChanged);
        }

        private static int CountChangedForeground(Texture2D first, Texture2D second, Texture2D background, float threshold, out int foreground)
        {
            bool[] aMask = VegetationLifeRenderingVisualTests.ForegroundMask(first, background, out _);
            bool[] bMask = VegetationLifeRenderingVisualTests.ForegroundMask(second, background, out _);
            Color32[] a = first.GetPixels32(); Color32[] b = second.GetPixels32();
            int changed = 0; foreground = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (!aMask[i] && !bMask[i]) continue;
                foreground++;
                if (PixelDistanceSquared(a[i], b[i]) > threshold) changed++;
            }
            return changed;
        }

        private static float PixelDistanceSquared(Color32 a, Color32 b)
        {
            float dr = (a.r - b.r) / 255f, dg = (a.g - b.g) / 255f, db = (a.b - b.b) / 255f;
            return dr * dr + dg * dg + db * db;
        }

        private static void Destroy(Object value)
        {
            if (value != null) Object.DestroyImmediate(value);
        }

        private readonly struct VerticalMotion
        {
            public readonly int LowerPixels, LowerChanged, UpperPixels, UpperChanged;
            public float LowerRate => LowerChanged / (float)Mathf.Max(1, LowerPixels);
            public float UpperRate => UpperChanged / (float)Mathf.Max(1, UpperPixels);
            public VerticalMotion(int lowerPixels, int lowerChanged, int upperPixels, int upperChanged)
            { LowerPixels = lowerPixels; LowerChanged = lowerChanged; UpperPixels = upperPixels; UpperChanged = upperChanged; }
            public string Describe(string lowerName, string upperName)
                => $"{lowerName}_pixels={LowerPixels}\n{lowerName}_changed={LowerChanged}\n{lowerName}_changed_rate={LowerRate:0.0000}\n" +
                   $"{upperName}_pixels={UpperPixels}\n{upperName}_changed={UpperChanged}\n{upperName}_changed_rate={UpperRate:0.0000}\n";
        }
    }
}
