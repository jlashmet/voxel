using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.AmbientLife.Api;
using VoxelEngine.Rendering.Runtime.AmbientLife;
using VoxelEngine.Showcase;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.CI
{
    [NUnit.Framework.Explicit("Temporal animation capture and image-space motion validation; run by rendering CI.")]
    public sealed class TemporalAnimationVisualTests
    {
        [UnityTest]
        public IEnumerator AmbientAndVegetationAnimationSequences_AreContinuousAndReadable()
        {
            yield return CaptureAmbientSequence();
            var vegetationFrames = new List<Texture2D>();
            try
            {
                yield return ValidateGrass(vegetationFrames);
                yield return ValidateVine(vegetationFrames);
                yield return ValidateWoody(vegetationFrames);
                Texture2D sheet = BuildContactSheet(vegetationFrames, 3, 4);
                try { File.WriteAllBytes(VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_animation_contact_sheet.png"), sheet.EncodeToPNG()); }
                finally { Object.DestroyImmediate(sheet); }
            }
            finally
            {
                for (int i = 0; i < vegetationFrames.Count; i++) if (vegetationFrames[i] != null) Object.DestroyImmediate(vegetationFrames[i]);
            }
        }

        private static IEnumerator CaptureAmbientSequence()
        {
            GameObject cameraObject = VegetationLifeRenderingVisualTests.CreateCamera(
                "CI Ambient Animation Validation Camera", new Vector3(0f, 9.2f, -17.5f), new Vector3(0f, 1.35f, 7.6f), 49f,
                out Camera camera, out RenderTexture target);
            GameObject root = new GameObject("CI Ambient Animation Validation Showcase");
            var frames = new List<Texture2D>();
            var backgrounds = new List<Texture2D>();
            Texture2D labelled = null;
            try
            {
                AmbientLifeAnimationValidationShowcase showcase = root.AddComponent<AmbientLifeAnimationValidationShowcase>();
                yield return null;
                Assert.That(showcase.ClusterCount, Is.EqualTo(AmbientLifeCatalogue.Count));
                VegetationLifeRenderingVisualTests.RemovePresentationGeometry(root.transform);
                showcase.SetLabelsVisible(false);
                ProceduralAmbientLifeBatchRenderer renderer = root.GetComponent<ProceduralAmbientLifeBatchRenderer>();
                Assert.That(renderer, Is.Not.Null);
                renderer.enabled = false;

                const int frameCount = 8;
                var changed = new int[frameCount - 1];
                var foregroundUnion = new int[frameCount - 1];
                int minChanged = int.MaxValue, maxChanged = 0, totalChanged = 0;
                for (int i = 0; i < frameCount; i++)
                {
                    // Capture the authored sky immediately before each deterministic agent frame.
                    // This keeps sky/cloud animation out of the creature-motion metric.
                    camera.Render();
                    Texture2D background = VegetationLifeRenderingVisualTests.ReadTarget(target);
                    backgrounds.Add(background);

                    renderer.DrawAtTime(i * 0.75f);
                    camera.Render();
                    Texture2D frame = VegetationLifeRenderingVisualTests.ReadTarget(target);
                    frames.Add(frame);
                    File.WriteAllBytes(VegetationLifeRenderingVisualTests.ArtifactPath($"ambient_animation_frame_{i:00}.png"), frame.EncodeToPNG());
                    VegetationLifeRenderingVisualTests.ForegroundMask(frame, background, out int foregroundCount);
                    Assert.That(foregroundCount, Is.GreaterThan(300), $"Frame {i} lost most visible agents.");
                    if (i == 0) continue;

                    int count = CountChangedForeground(
                        frames[i - 1], frame,
                        backgrounds[i - 1], background,
                        0.010f,
                        out int unionCount);
                    changed[i - 1] = count;
                    foregroundUnion[i - 1] = unionCount;
                    minChanged = Mathf.Min(minChanged, count);
                    maxChanged = Mathf.Max(maxChanged, count);
                    totalChanged += count;
                }

                float averageChanged = totalChanged / (float)changed.Length;
                var report = new StringBuilder();
                for (int i = 0; i < changed.Length; i++)
                {
                    report.AppendLine($"transition_{i:00}_{i + 1:00}_changed_pixels={changed[i]}");
                    report.AppendLine($"transition_{i:00}_{i + 1:00}_foreground_union_pixels={foregroundUnion[i]}");
                }
                report.AppendLine($"min_changed_pixels={minChanged}");
                report.AppendLine($"max_changed_pixels={maxChanged}");
                report.AppendLine($"average_changed_pixels={averageChanged:0.0}");
                File.WriteAllText(VegetationLifeRenderingVisualTests.ArtifactPath("ambient_animation_temporal_quality.txt"), report.ToString());

                Texture2D contact = BuildContactSheet(frames, 4, 4);
                try { File.WriteAllBytes(VegetationLifeRenderingVisualTests.ArtifactPath("ambient_animation_contact_sheet.png"), contact.EncodeToPNG()); }
                finally { Object.DestroyImmediate(contact); }

                showcase.SetLabelsVisible(true);
                renderer.DrawAtTime(0f); camera.Render(); labelled = VegetationLifeRenderingVisualTests.ReadTarget(target);
                File.WriteAllBytes(VegetationLifeRenderingVisualTests.ArtifactPath("ambient_animation_gallery_labeled.png"), labelled.EncodeToPNG());

                Assert.That(minChanged, Is.GreaterThan(650), "At least one 0.75-second interval is effectively frozen.");
                Assert.That(averageChanged, Is.GreaterThan(1200f), "Ambient animation produces too little continuous screen-space motion.");
                Assert.That(maxChanged, Is.LessThan(minChanged * 6), "Ambient motion is excessively intermittent.");
            }
            finally
            {
                if (labelled != null) Object.DestroyImmediate(labelled);
                for (int i = 0; i < frames.Count; i++) if (frames[i] != null) Object.DestroyImmediate(frames[i]);
                for (int i = 0; i < backgrounds.Count; i++) if (backgrounds[i] != null) Object.DestroyImmediate(backgrounds[i]);
                VegetationLifeRenderingVisualTests.ReleaseTarget(camera, target);
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(root);
            }
        }

        private static IEnumerator ValidateGrass(List<Texture2D> frames)
        {
            GameObject cameraObject = VegetationLifeRenderingVisualTests.CreateCamera("CI Grass Wind Camera", new Vector3(0f, 1.15f, -3.4f), new Vector3(0f, 0.72f, 0f), 30f, out Camera camera, out RenderTexture target);
            GameObject root = new GameObject("CI Grass Wind Validation");
            Texture2D backgroundFirst = null, backgroundSecond = null;
            try
            {
                VegetationRenderingShowcase showcase = root.AddComponent<VegetationRenderingShowcase>(); yield return null;
                VegetationLifeRenderingVisualTests.RemovePresentationGeometry(root.transform); showcase.Renderer.enabled = false; showcase.Renderer.Clear();
                showcase.Renderer.SetInstances(new[] { Instance(VegetationKind.Grass, new float3(0,0,0), new float3(0,1,0), 0x51A55EEDu, 2f) });

                camera.Render(); backgroundFirst = VegetationLifeRenderingVisualTests.ReadTarget(target);
                showcase.Renderer.DrawNow(); camera.Render(); Texture2D first = VegetationLifeRenderingVisualTests.ReadTarget(target); frames.Add(first);
                File.WriteAllBytes(VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_grass_t0.png"), first.EncodeToPNG());

                yield return new WaitForSeconds(0.70f);
                camera.Render(); backgroundSecond = VegetationLifeRenderingVisualTests.ReadTarget(target);
                showcase.Renderer.DrawNow(); camera.Render(); Texture2D second = VegetationLifeRenderingVisualTests.ReadTarget(target); frames.Add(second);
                File.WriteAllBytes(VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_grass_t1.png"), second.EncodeToPNG());

                VerticalMotion m = AnalyseVertical(first, second, backgroundFirst, backgroundSecond, 0.22f, 0.55f);
                File.WriteAllText(VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_grass_motion.txt"), m.Describe("base", "tip"));
                Assert.That(m.UpperRate, Is.GreaterThan(0.06f), "Grass tips are not visibly responding to wind.");
                Assert.That(m.LowerRate, Is.LessThan(0.12f), "Grass roots are sliding.");
                Assert.That(m.UpperRate, Is.GreaterThan(m.LowerRate * 1.4f), "Grass motion does not increase toward the tip.");
            }
            finally
            {
                if (backgroundFirst != null) Object.DestroyImmediate(backgroundFirst);
                if (backgroundSecond != null) Object.DestroyImmediate(backgroundSecond);
                VegetationLifeRenderingVisualTests.ReleaseTarget(camera, target); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(root);
            }
        }

        private static IEnumerator ValidateVine(List<Texture2D> frames)
        {
            GameObject cameraObject = VegetationLifeRenderingVisualTests.CreateCamera("CI Vine Wind Camera", new Vector3(0f, 1.4f, -4.2f), new Vector3(0f, 1.15f, 0f), 32f, out Camera camera, out RenderTexture target);
            GameObject root = new GameObject("CI Vine Wind Validation");
            Texture2D backgroundFirst = null, backgroundSecond = null;
            try
            {
                VegetationRenderingShowcase showcase = root.AddComponent<VegetationRenderingShowcase>(); yield return null;
                VegetationLifeRenderingVisualTests.RemovePresentationGeometry(root.transform); showcase.Renderer.enabled = false; showcase.Renderer.Clear();
                showcase.Renderer.SetInstances(new[] { Instance(VegetationKind.HangingVine, new float3(0,2.4f,0), new float3(0,0,-1), 0xA11CE551u, 1.35f) });

                camera.Render(); backgroundFirst = VegetationLifeRenderingVisualTests.ReadTarget(target);
                showcase.Renderer.DrawNow(); camera.Render(); Texture2D first = VegetationLifeRenderingVisualTests.ReadTarget(target); frames.Add(first);
                File.WriteAllBytes(VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_vine_t0.png"), first.EncodeToPNG());

                yield return new WaitForSeconds(0.75f);
                camera.Render(); backgroundSecond = VegetationLifeRenderingVisualTests.ReadTarget(target);
                showcase.Renderer.DrawNow(); camera.Render(); Texture2D second = VegetationLifeRenderingVisualTests.ReadTarget(target); frames.Add(second);
                File.WriteAllBytes(VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_vine_t1.png"), second.EncodeToPNG());

                VerticalMotion m = AnalyseVertical(first, second, backgroundFirst, backgroundSecond, 0.46f, 0.78f);
                File.WriteAllText(VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_vine_motion.txt"), m.Describe("free_end", "attachment"));
                Assert.That(m.LowerRate, Is.GreaterThan(0.05f), "Vine free end is not visibly swaying.");
                Assert.That(m.UpperRate, Is.LessThan(0.14f), "Vine attachment region is sliding too much.");
                Assert.That(m.LowerRate, Is.GreaterThan(m.UpperRate * 1.25f), "Vine motion should increase toward its free end.");
            }
            finally
            {
                if (backgroundFirst != null) Object.DestroyImmediate(backgroundFirst);
                if (backgroundSecond != null) Object.DestroyImmediate(backgroundSecond);
                VegetationLifeRenderingVisualTests.ReleaseTarget(camera, target); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(root);
            }
        }

        private static IEnumerator ValidateWoody(List<Texture2D> frames)
        {
            GameObject cameraObject = VegetationLifeRenderingVisualTests.CreateCamera("CI Woody Static Camera", new Vector3(0f, 1.4f, -3.8f), new Vector3(0f, 0.35f, 0f), 34f, out Camera camera, out RenderTexture target);
            GameObject root = new GameObject("CI Woody Static Validation");
            Texture2D backgroundFirst = null, backgroundSecond = null;
            try
            {
                VegetationRenderingShowcase showcase = root.AddComponent<VegetationRenderingShowcase>(); yield return null;
                VegetationLifeRenderingVisualTests.RemovePresentationGeometry(root.transform); showcase.Renderer.enabled = false; showcase.Renderer.Clear();
                showcase.Renderer.SetInstances(new[] { Instance(VegetationKind.FallenLog, new float3(0,0,0), new float3(0,1,0), 0xDEADBEEFu, 1.6f) });

                camera.Render(); backgroundFirst = VegetationLifeRenderingVisualTests.ReadTarget(target);
                showcase.Renderer.DrawNow(); camera.Render(); Texture2D first = VegetationLifeRenderingVisualTests.ReadTarget(target); frames.Add(first);
                File.WriteAllBytes(VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_woody_t0.png"), first.EncodeToPNG());

                yield return new WaitForSeconds(0.35f);
                camera.Render(); backgroundSecond = VegetationLifeRenderingVisualTests.ReadTarget(target);
                showcase.Renderer.DrawNow(); camera.Render(); Texture2D second = VegetationLifeRenderingVisualTests.ReadTarget(target); frames.Add(second);
                File.WriteAllBytes(VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_woody_t1.png"), second.EncodeToPNG());

                int changed = CountChangedForeground(first, second, backgroundFirst, backgroundSecond, 0.004f, out int foreground);
                float rate = changed / (float)Mathf.Max(1, foreground);
                File.WriteAllText(VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_woody_motion.txt"), $"foreground={foreground}\nchanged_pixels={changed}\nchanged_rate={rate:0.0000}\n");
                Assert.That(foreground, Is.GreaterThan(80));
                Assert.That(rate, Is.LessThan(0.01f), "Woody debris should remain static without physics interaction.");
            }
            finally
            {
                if (backgroundFirst != null) Object.DestroyImmediate(backgroundFirst);
                if (backgroundSecond != null) Object.DestroyImmediate(backgroundSecond);
                VegetationLifeRenderingVisualTests.ReleaseTarget(camera, target); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(root);
            }
        }

        private static VegetationInstance Instance(VegetationKind kind, float3 position, float3 normal, uint seed, float scale)
            => new VegetationInstance { Kind = kind, PositionMetres = position, SurfaceNormal = normal, Seed = seed, Scale = scale };

        private static VerticalMotion AnalyseVertical(
            Texture2D first,
            Texture2D second,
            Texture2D firstBackground,
            Texture2D secondBackground,
            float lowerMax,
            float upperMin)
        {
            bool[] aMask = VegetationLifeRenderingVisualTests.ForegroundMask(first, firstBackground, out _);
            bool[] bMask = VegetationLifeRenderingVisualTests.ForegroundMask(second, secondBackground, out _);
            Color32[] a = first.GetPixels32();
            Color32[] b = second.GetPixels32();
            Color32[] aBackground = firstBackground.GetPixels32();
            Color32[] bBackground = secondBackground.GetPixels32();
            int width = first.width, height = first.height, minY = height, maxY = -1;
            for (int i = 0; i < aMask.Length; i++)
            {
                if (aMask[i] || bMask[i])
                {
                    int y = i / width;
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }
            Assert.That(maxY, Is.GreaterThanOrEqualTo(minY), "No vegetation foreground was captured.");
            float span = Mathf.Max(1f, maxY - minY);
            int lowerPixels = 0, lowerChanged = 0, upperPixels = 0, upperChanged = 0;
            for (int i = 0; i < aMask.Length; i++)
            {
                if (!aMask[i] && !bMask[i]) continue;
                float y = (i / width - minY) / span;
                bool changed = ForegroundResidualDistanceSquared(a[i], aBackground[i], b[i], bBackground[i]) > 0.0025f;
                if (y <= lowerMax) { lowerPixels++; if (changed) lowerChanged++; }
                if (y >= upperMin) { upperPixels++; if (changed) upperChanged++; }
            }
            return new VerticalMotion(lowerPixels, lowerChanged, upperPixels, upperChanged);
        }

        private static int CountChangedForeground(
            Texture2D first,
            Texture2D second,
            Texture2D firstBackground,
            Texture2D secondBackground,
            float threshold,
            out int foreground)
        {
            bool[] aMask = VegetationLifeRenderingVisualTests.ForegroundMask(first, firstBackground, out _);
            bool[] bMask = VegetationLifeRenderingVisualTests.ForegroundMask(second, secondBackground, out _);
            Color32[] a = first.GetPixels32();
            Color32[] b = second.GetPixels32();
            Color32[] aBackground = firstBackground.GetPixels32();
            Color32[] bBackground = secondBackground.GetPixels32();
            int changed = 0;
            foreground = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (!aMask[i] && !bMask[i]) continue;
                foreground++;
                if (ForegroundResidualDistanceSquared(a[i], aBackground[i], b[i], bBackground[i]) > threshold) changed++;
            }
            return changed;
        }

        private static float ForegroundResidualDistanceSquared(Color32 first, Color32 firstBackground, Color32 second, Color32 secondBackground)
        {
            float firstR = (first.r - firstBackground.r) / 255f;
            float firstG = (first.g - firstBackground.g) / 255f;
            float firstB = (first.b - firstBackground.b) / 255f;
            float secondR = (second.r - secondBackground.r) / 255f;
            float secondG = (second.g - secondBackground.g) / 255f;
            float secondB = (second.b - secondBackground.b) / 255f;
            float dr = firstR - secondR;
            float dg = firstG - secondG;
            float db = firstB - secondB;
            return dr * dr + dg * dg + db * db;
        }

        private static Texture2D BuildContactSheet(IReadOnlyList<Texture2D> frames, int columns, int downsample)
        {
            int sw = frames[0].width, sh = frames[0].height, cw = sw / downsample, ch = sh / downsample, rows = (frames.Count + columns - 1) / columns, width = cw * columns, height = ch * rows;
            var destination = new Color32[width * height];
            for (int f = 0; f < frames.Count; f++)
            {
                Color32[] source = frames[f].GetPixels32(); int ox = (f % columns) * cw, oy = (rows - 1 - f / columns) * ch;
                for (int y = 0; y < ch; y++) for (int x = 0; x < cw; x++) destination[(oy + y) * width + ox + x] = source[Mathf.Min(sh - 1, y * downsample) * sw + Mathf.Min(sw - 1, x * downsample)];
            }
            var sheet = new Texture2D(width, height, TextureFormat.RGB24, false, false); sheet.SetPixels32(destination); sheet.Apply(false, false); return sheet;
        }

        private readonly struct VerticalMotion
        {
            public readonly int LowerPixels, LowerChanged, UpperPixels, UpperChanged;
            public float LowerRate => LowerChanged / (float)Mathf.Max(1, LowerPixels); public float UpperRate => UpperChanged / (float)Mathf.Max(1, UpperPixels);
            public VerticalMotion(int lowerPixels, int lowerChanged, int upperPixels, int upperChanged) { LowerPixels = lowerPixels; LowerChanged = lowerChanged; UpperPixels = upperPixels; UpperChanged = upperChanged; }
            public string Describe(string lowerName, string upperName) => $"{lowerName}_pixels={LowerPixels}\n{lowerName}_changed={LowerChanged}\n{lowerName}_changed_rate={LowerRate:0.0000}\n{upperName}_pixels={UpperPixels}\n{upperName}_changed={UpperChanged}\n{upperName}_changed_rate={UpperRate:0.0000}\n";
        }
    }
}
