using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Rendering;
using VoxelEngine.Rendering.SurfaceExtraction;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class TerrainLookdevScreenshotTests
    {
        private const int CaptureWidth = 512;
        private const int CaptureHeight = 768;
        private const int ReferenceWidth = 32;
        private const int ReferenceHeight = 48;
        private const float MinimumPatchSsim = 0.25f;

        // 32x48 RGB24 downsample of the original Mounting Force terrain reference.
        // Keeping this tiny reference in source makes the visual acceptance test deterministic
        // and avoids depending on editor import/compression settings for the reference itself.
        private const string ReferenceRgbBase64 =
            "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";

        [UnityTest]
        public IEnumerator CaptureTerrainLookdev()
        {
            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath)!.FullName,
                "Artifacts", "Terrain");
            Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("Terrain Lookdev Test Camera");
            root.tag = "MainCamera";
            TerrainLookdev lookdev = root.AddComponent<TerrainLookdev>();
            Camera camera = lookdev.SceneCamera;

            Assert.IsTrue(VoxelRenderBridge.TryGetWorld(out _),
                "Terrain lookdev did not register a valid voxel world.");
            VoxelRenderBridge.ResetSurfacePassDiagnostics("terrain-capture-start");

            var convergenceTarget = new RenderTexture(
                CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            convergenceTarget.Create();
            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = convergenceTarget;

            int stableFrames = 0;
            for (int frame = 0; frame < 360 && stableFrames < 3; frame++)
            {
                camera.Render();
                yield return null;
                VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                bool converged = metrics.SolidKnownChunks > 0
                    && metrics.SolidDirtyChunks == 0
                    && metrics.SolidResidentChunks >= metrics.SolidKnownChunks;
                stableFrames = converged ? stableFrames + 1 : 0;
            }

            camera.targetTexture = previousTarget;
            convergenceTarget.Release();
            UnityEngine.Object.DestroyImmediate(convergenceTarget);

            VoxelSurfaceMetrics finalMetrics = VoxelRenderBridge.SurfaceMetrics;
            Assert.Greater(VoxelRenderBridge.RenderFeatureEnqueueCount, 0,
                "VoxelRenderFeature never enqueued for the terrain camera.");
            Assert.Greater(VoxelRenderBridge.SurfacePassRecordCount, 0,
                "Voxel surface pass never recorded for the terrain camera.");
            Assert.GreaterOrEqual(stableFrames, 3,
                $"Terrain surface did not converge: known={finalMetrics.SolidKnownChunks}, " +
                $"resident={finalMetrics.SolidResidentChunks}, dirty={finalMetrics.SolidDirtyChunks}, " +
                $"featureEnqueues={VoxelRenderBridge.RenderFeatureEnqueueCount}, " +
                $"surfaceRecords={VoxelRenderBridge.SurfacePassRecordCount}, " +
                $"state={VoxelRenderBridge.LastSurfacePassState}");

            string capturePath = Path.Combine(outputDirectory, "terrain.png");
            Texture2D captured = Capture(camera, capturePath, CaptureWidth, CaptureHeight);
            byte[] actualRgb = DownsampleTopLeftRgb(captured, ReferenceWidth, ReferenceHeight);
            byte[] referenceRgb = Convert.FromBase64String(ReferenceRgbBase64);

            float rgbMae = RgbMae(actualRgb, referenceRgb);
            float patchSsim = PatchLumaSsim(
                actualRgb, referenceRgb, ReferenceWidth, ReferenceHeight, 4);
            WriteDiff(Path.Combine(outputDirectory, "terrain-diff.png"),
                actualRgb, referenceRgb, ReferenceWidth, ReferenceHeight);

            File.WriteAllText(Path.Combine(outputDirectory, "terrain-similarity.txt"),
                $"patchSsim={patchSsim:F4}\n" +
                $"rgbMae={rgbMae:F4}\n" +
                $"minimumPatchSsim={MinimumPatchSsim:F4}\n" +
                $"capture={CaptureWidth}x{CaptureHeight}\n" +
                $"reference={ReferenceWidth}x{ReferenceHeight}\n");

            File.WriteAllText(Path.Combine(outputDirectory, "terrain.txt"),
                $"knownChunks={finalMetrics.SolidKnownChunks}\n" +
                $"residentChunks={finalMetrics.SolidResidentChunks}\n" +
                $"dirtyChunks={finalMetrics.SolidDirtyChunks}\n" +
                $"featureEnqueues={VoxelRenderBridge.RenderFeatureEnqueueCount}\n" +
                $"surfaceRecords={VoxelRenderBridge.SurfacePassRecordCount}\n" +
                $"surfaceState={VoxelRenderBridge.LastSurfacePassState}\n" +
                $"patchSsim={patchSsim:F4}\n" +
                $"rgbMae={rgbMae:F4}\n");

            UnityEngine.Object.DestroyImmediate(captured);
            lookdev.Shutdown();
            UnityEngine.Object.Destroy(root);
            yield return null;

            Assert.GreaterOrEqual(patchSsim, MinimumPatchSsim,
                $"Terrain is not visually similar enough to the original reference. " +
                $"patch SSIM={patchSsim:F4} (required >= {MinimumPatchSsim:F4}), " +
                $"RGB MAE={rgbMae:F4}. Inspect terrain.png and terrain-diff.png.");
        }

        private static Texture2D Capture(Camera camera, string path, int width, int height)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = previousActive;
            camera.targetTexture = previousTarget;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            return texture;
        }

        private static byte[] DownsampleTopLeftRgb(Texture2D source, int width, int height)
        {
            Assert.AreEqual(0, source.width % width);
            Assert.AreEqual(0, source.height % height);
            int blockWidth = source.width / width;
            int blockHeight = source.height / height;
            Color32[] pixels = source.GetPixels32();
            var result = new byte[width * height * 3];
            for (int outY = 0; outY < height; outY++)
            {
                int topSourceY = outY * blockHeight;
                for (int outX = 0; outX < width; outX++)
                {
                    int sumR = 0, sumG = 0, sumB = 0;
                    for (int dy = 0; dy < blockHeight; dy++)
                    {
                        int unityY = source.height - 1 - (topSourceY + dy);
                        int row = unityY * source.width;
                        for (int dx = 0; dx < blockWidth; dx++)
                        {
                            Color32 p = pixels[row + outX * blockWidth + dx];
                            sumR += p.r;
                            sumG += p.g;
                            sumB += p.b;
                        }
                    }
                    int samples = blockWidth * blockHeight;
                    int index = (outY * width + outX) * 3;
                    result[index] = (byte)(sumR / samples);
                    result[index + 1] = (byte)(sumG / samples);
                    result[index + 2] = (byte)(sumB / samples);
                }
            }
            return result;
        }

        private static float RgbMae(byte[] actual, byte[] reference)
        {
            double sum = 0.0;
            for (int i = 0; i < actual.Length; i++)
                sum += Math.Abs(actual[i] - reference[i]) / 255.0;
            return (float)(sum / actual.Length);
        }

        private static float PatchLumaSsim(
            byte[] actual, byte[] reference, int width, int height, int patchSize)
        {
            const double c1 = 0.0001;
            const double c2 = 0.0009;
            double score = 0.0;
            int patches = 0;
            for (int y0 = 0; y0 < height; y0 += patchSize)
            for (int x0 = 0; x0 < width; x0 += patchSize)
            {
                int x1 = Math.Min(x0 + patchSize, width);
                int y1 = Math.Min(y0 + patchSize, height);
                int count = (x1 - x0) * (y1 - y0);
                double meanA = 0.0, meanB = 0.0;
                for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int i = (y * width + x) * 3;
                    meanA += Luma(actual, i);
                    meanB += Luma(reference, i);
                }
                meanA /= count;
                meanB /= count;
                double varianceA = 0.0, varianceB = 0.0, covariance = 0.0;
                for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int i = (y * width + x) * 3;
                    double da = Luma(actual, i) - meanA;
                    double db = Luma(reference, i) - meanB;
                    varianceA += da * da;
                    varianceB += db * db;
                    covariance += da * db;
                }
                varianceA /= count;
                varianceB /= count;
                covariance /= count;
                double numerator = (2.0 * meanA * meanB + c1) * (2.0 * covariance + c2);
                double denominator = (meanA * meanA + meanB * meanB + c1) *
                    (varianceA + varianceB + c2);
                score += denominator > 0.0 ? numerator / denominator : 1.0;
                patches++;
            }
            return (float)(score / patches);
        }

        private static double Luma(byte[] rgb, int index)
        {
            return (0.2126 * rgb[index] + 0.7152 * rgb[index + 1] +
                0.0722 * rgb[index + 2]) / 255.0;
        }

        private static void WriteDiff(
            string path, byte[] actual, byte[] reference, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var pixels = new Color32[width * height];
            for (int topY = 0; topY < height; topY++)
            for (int x = 0; x < width; x++)
            {
                int source = (topY * width + x) * 3;
                byte r = (byte)Math.Abs(actual[source] - reference[source]);
                byte g = (byte)Math.Abs(actual[source + 1] - reference[source + 1]);
                byte b = (byte)Math.Abs(actual[source + 2] - reference[source + 2]);
                int unityY = height - 1 - topY;
                pixels[unityY * width + x] = new Color32(r, g, b, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
