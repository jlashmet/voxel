using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Image-space acceptance test for every production solid LOD band. The old regression only
    /// required 18% of one front-facing edge map to survive, which can certify an obviously bad
    /// blocky castle. This gate compares the same fixed orthographic framing at three viewpoints
    /// and requires silhouette/detail edges, regional structure, and material colour distribution
    /// to remain recognisably close to the step-1 reference.
    /// </summary>
    public sealed class LodVisualFidelityTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const int Width = 180;
        private const int Height = 135;

        [UnityTest, Timeout(900000)]
        public IEnumerator EveryLod_PreservesCastleAppearanceFromMultipleViews()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return WaitForWorldReady();

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            ShowcaseWorld world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            Camera camera = Camera.main;
            Assert.NotNull(camera);

            typeof(VoxelShowcase)
                .GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase)
                .GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);

            int ground = world.SurfaceHeight(256, 376);
            CastlePlan plan = StructuresComposition.PlanCastle(
                new int3(256, ground, 376), world.Seed);
            Vector3 centre = new(
                plan.Centre.x * 0.1f,
                (plan.Centre.y + plan.PlateauHeight) * 0.1f,
                plan.Centre.z * 0.1f);
            Vector3 lookAt = centre + Vector3.up * 10f;

            var bands = new (int step, float distance)[]
            {
                (1, 48f), (2, 144f), (4, 240f), (8, 340f),
            };
            var viewDirections = new[]
            {
                new Vector3(0f, 0f, -1f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0.7071068f, 0f, -0.7071068f),
            };

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(Width, Height, TextureFormat.RGB24, false, true);
            bool oldOrthographic = camera.orthographic;
            float oldOrthographicSize = camera.orthographicSize;
            float oldNear = camera.nearClipPlane;
            float oldFar = camera.farClipPlane;
            double oldBuildBudget = VoxelRenderBridge.SolidBuildBudgetMs;

            try
            {
                VoxelRenderBridge.SolidBuildBudgetMs = 8.0;
                camera.targetTexture = target;
                camera.orthographic = true;
                camera.orthographicSize = 24f;

                for (int view = 0; view < viewDirections.Length; view++)
                {
                    VisualSignature reference = default;
                    for (int bandIndex = 0; bandIndex < bands.Length; bandIndex++)
                    {
                        (int step, float distance) = bands[bandIndex];
                        Vector3 horizontal = viewDirections[view].normalized;
                        camera.transform.position = centre
                            + horizontal * distance
                            + Vector3.up * 20f;
                        camera.transform.LookAt(lookAt);
                        camera.nearClipPlane = Mathf.Max(0.3f, distance - 38f);
                        camera.farClipPlane = distance + 38f;

                        VisualSignature signature = default;
                        VisualSignature previous = default;
                        bool havePrevious = false;
                        int stableCount = 0;
                        bool converged = false;
                        double deadline = Time.realtimeSinceStartupAsDouble + 20.0;
                        for (int frame = 0; frame < 1200
                             && Time.realtimeSinceStartupAsDouble < deadline; frame++)
                        {
                            RenderUrpCamera(camera);
                            yield return null;
                            if ((frame % 10) != 0) continue;

                            VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                            VisualSignature candidate = Capture(target, readback);
                            bool substantial = metrics.VisibleSolidChunks > 0
                                            && candidate.EdgeCount > 100;
                            bool stable = substantial && havePrevious
                                       && EdgeF1(previous, candidate, 1) >= 0.97f
                                       && Ratio(previous.EdgeCount, candidate.EdgeCount) >= 0.97f;
                            stableCount = stable ? stableCount + 1 : 0;
                            previous = candidate;
                            havePrevious = true;
                            if (stableCount < 2) continue;
                            signature = candidate;
                            converged = true;
                            break;
                        }

                        Assert.True(converged,
                            $"LOD {step}, view {view} never reached a stable production capture.");

                        if (step == 1)
                        {
                            reference = signature;
                            Assert.Greater(reference.EdgeCount, 260,
                                $"Step-1 reference is not detailed enough in view {view}.");
                            continue;
                        }

                        float edgeF1 = EdgeF1(reference, signature, 2);
                        float edgeRetention = Ratio(reference.EdgeCount, signature.EdgeCount);
                        float colourOverlap = HistogramOverlap(reference, signature);
                        float weakestRegion = WeakestRegionalRetention(reference, signature);

                        // All coarse levels must still look like the same castle. These values are
                        // intentionally much stronger than the former 18% single-view criterion.
                        Assert.GreaterOrEqual(edgeF1, 0.52f,
                            $"LOD {step}, view {view} preserved only {edgeF1:P0} of step-1 "
                          + "architectural edge placement.");
                        Assert.GreaterOrEqual(edgeRetention, 0.50f,
                            $"LOD {step}, view {view} retained only {edgeRetention:P0} of step-1 "
                          + "edge density; towers/openings are collapsing into a blob.");
                        Assert.GreaterOrEqual(weakestRegion, 0.35f,
                            $"LOD {step}, view {view} loses an entire architectural region "
                          + $"(weakest retention {weakestRegion:P0}).");
                        Assert.GreaterOrEqual(colourOverlap, 0.82f,
                            $"LOD {step}, view {view} materially changes the castle colour/material "
                          + $"distribution ({colourOverlap:P0} overlap with step-1).");
                    }
                }
            }
            finally
            {
                VoxelRenderBridge.SolidBuildBudgetMs = oldBuildBudget;
                camera.targetTexture = null;
                camera.orthographic = oldOrthographic;
                camera.orthographicSize = oldOrthographicSize;
                camera.nearClipPlane = oldNear;
                camera.farClipPlane = oldFar;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
            }
        }

        private static IEnumerator WaitForWorldReady()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 60.0;
            int frames = 0;
            while (!VoxelRenderBridge.SurfaceBuildEnabled
                   && frames++ < 3600
                   && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;

            Assert.True(VoxelRenderBridge.SurfaceBuildEnabled,
                "Showcase world never reached atomic render publication.");
            Assert.True(VoxelRenderBridge.TryGetWorld(out _),
                "Showcase lost the renderer world binding before LOD validation.");
        }

        private static void RenderUrpCamera(Camera camera)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest
            {
                destination = camera.targetTexture,
            };
            Assert.True(RenderPipeline.SupportsRenderRequest(camera, request));
            VoxelRenderBridge.ResetSurfacePassDiagnostics("lod-visual-fidelity");
            RenderPipeline.SubmitRenderRequest(camera, request);
            Assert.Greater(VoxelRenderBridge.SurfacePassRecordCount, 0);
            Assert.AreEqual("feature-aware", VoxelRenderBridge.LastSurfacePassState);
        }

        private readonly struct VisualSignature
        {
            public readonly bool[] Edges;
            public readonly int EdgeCount;
            public readonly int[] RegionEdges;
            public readonly int[] Histogram;
            public readonly int HistogramSamples;

            public VisualSignature(
                bool[] edges, int edgeCount, int[] regionEdges,
                int[] histogram, int histogramSamples)
            {
                Edges = edges;
                EdgeCount = edgeCount;
                RegionEdges = regionEdges;
                Histogram = histogram;
                HistogramSamples = histogramSamples;
            }
        }

        private static VisualSignature Capture(RenderTexture target, Texture2D readback)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                readback.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            Color32[] pixels = readback.GetPixels32();
            var edges = new bool[Width * Height];
            var regionEdges = new int[9];
            var histogram = new int[64];
            int edgeCount = 0;
            int histogramSamples = 0;

            int minX = Width / 5;
            int maxX = Width * 4 / 5;
            int minY = Height / 7;
            int maxY = Height * 13 / 14;
            int cropWidth = maxX - minX;
            int cropHeight = maxY - minY;
            const float edgeThreshold = 0.04f;

            for (int y = minY; y < maxY - 1; y++)
            for (int x = minX; x < maxX - 1; x++)
            {
                int i = x + y * Width;
                Color32 colour = pixels[i];
                int bin = (colour.r >> 6) | ((colour.g >> 6) << 2) | ((colour.b >> 6) << 4);
                histogram[bin]++;
                histogramSamples++;

                float l = Luminance(colour);
                float dx = Mathf.Abs(l - Luminance(pixels[i + 1]));
                float dy = Mathf.Abs(l - Luminance(pixels[i + Width]));
                if (Mathf.Max(dx, dy) < edgeThreshold) continue;

                edges[i] = true;
                edgeCount++;
                int gx = Mathf.Clamp((x - minX) * 3 / cropWidth, 0, 2);
                int gy = Mathf.Clamp((y - minY) * 3 / cropHeight, 0, 2);
                regionEdges[gx + gy * 3]++;
            }

            return new VisualSignature(
                edges, edgeCount, regionEdges, histogram, histogramSamples);
        }

        private static float EdgeF1(
            in VisualSignature a, in VisualSignature b, int tolerancePixels)
        {
            float recall = MatchedEdgeRecall(a, b, tolerancePixels);
            float precision = MatchedEdgeRecall(b, a, tolerancePixels);
            if (recall <= 0f || precision <= 0f) return 0f;
            return 2f * recall * precision / (recall + precision);
        }

        private static float MatchedEdgeRecall(
            in VisualSignature reference, in VisualSignature candidate, int tolerancePixels)
        {
            if (reference.EdgeCount == 0) return 0f;
            int matched = 0;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int i = x + y * Width;
                if (!reference.Edges[i]) continue;
                bool found = false;
                for (int yy = Mathf.Max(0, y - tolerancePixels);
                     yy <= Mathf.Min(Height - 1, y + tolerancePixels) && !found; yy++)
                for (int xx = Mathf.Max(0, x - tolerancePixels);
                     xx <= Mathf.Min(Width - 1, x + tolerancePixels); xx++)
                {
                    if (!candidate.Edges[xx + yy * Width]) continue;
                    found = true;
                    break;
                }
                if (found) matched++;
            }
            return matched / (float)reference.EdgeCount;
        }

        private static float WeakestRegionalRetention(
            in VisualSignature reference, in VisualSignature candidate)
        {
            float weakest = 1f;
            bool measured = false;
            for (int i = 0; i < reference.RegionEdges.Length; i++)
            {
                // Ignore cells that contain almost no castle structure in the reference.
                if (reference.RegionEdges[i] < 12) continue;
                measured = true;
                float ratio = candidate.RegionEdges[i] / (float)reference.RegionEdges[i];
                weakest = Mathf.Min(weakest, Mathf.Min(1f, ratio));
            }
            return measured ? weakest : 0f;
        }

        private static float HistogramOverlap(
            in VisualSignature reference, in VisualSignature candidate)
        {
            int overlap = 0;
            for (int i = 0; i < reference.Histogram.Length; i++)
                overlap += Mathf.Min(reference.Histogram[i], candidate.Histogram[i]);
            return overlap / (float)Mathf.Max(
                reference.HistogramSamples, candidate.HistogramSamples);
        }

        private static float Ratio(int a, int b) =>
            Mathf.Min(a, b) / (float)Mathf.Max(1, Mathf.Max(a, b));

        private static float Luminance(Color32 colour) =>
            (0.2126f * colour.r + 0.7152f * colour.g + 0.0722f * colour.b) / 255f;
    }
}
