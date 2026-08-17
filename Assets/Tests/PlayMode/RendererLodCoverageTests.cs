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
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using Object = UnityEngine.Object;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Player-visible coverage gates for the production solid renderer.
    ///
    /// MissingVisibleSolidChunks is necessary but not sufficient: an expected chunk that never
    /// entered the renderer's known set is visually absent without incrementing that counter.
    /// These tests therefore combine scheduler telemetry with a real URP render-target coverage
    /// mask. The clear colour is deliberately impossible terrain/castle magenta, so uncovered
    /// pixels expose renderer holes directly.
    /// </summary>
    public sealed class RendererLodCoverageTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const int Width = 128;
        private const int Height = 96;
        private const int StableFrames = 4;
        private const float MaxTerrainClearFraction = 0.0025f;
        private const float MinCastleForegroundRecall = 0.82f;
        private const float MinCastleForegroundRatio = 0.76f;
        private static readonly Color CoverageClear = new(1f, 0f, 1f, 1f);

        private static readonly (int step, float distance)[] LodBands =
        {
            (1, 48f),
            (2, 144f),
            (4, 240f),
            (8, 348f),
        };

        private static readonly float[] LodBoundaries = { 96f, 192f, 288f };

        [UnityTest, Timeout(900000)]
        public IEnumerator OpenTerrain_IsHoleFreeAtEveryLodAndAcrossEveryTransition()
        {
            yield return LoadShowcaseScene();
            GetShowcaseContext(out VoxelShowcase showcase, out ShowcaseWorld world,
                               out Camera camera, out CastlePlan plan, out Vector3 castleCentre);
            ConfigureFlyCamera(showcase);

            VoxelFarTerrain farTerrain = Object.FindFirstObjectByType<VoxelFarTerrain>();
            if (farTerrain != null) farTerrain.enabled = false;

            // Stay inside the baked/startup neighbourhood while getting well clear of the castle.
            int terrainX = plan.Centre.x + 800;
            int terrainZ = plan.Centre.z + 800;
            int terrainY = world.SurfaceHeight(terrainX, terrainZ);
            Vector3 terrainTarget = new(terrainX * ShowcaseWorld.VoxelSize,
                                        terrainY * ShowcaseWorld.VoxelSize,
                                        terrainZ * ShowcaseWorld.VoxelSize);

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(Width, Height, TextureFormat.RGB24, false, true);
            CameraState cameraState = CaptureCameraState(camera);
            double oldBuildBudgetMs = VoxelRenderBridge.SolidBuildBudgetMs;
            try
            {
                ConfigureCoverageCamera(camera, target, orthographicSize: 20f);
                VoxelRenderBridge.SolidBuildBudgetMs = 8.0;

                foreach ((int step, float distance) in LodBands)
                {
                    SetTopDownTerrainView(camera, terrainTarget, distance);
                    yield return WaitForTerrainCoverage(
                        camera, target, readback, $"terrain LOD step {step} at {distance:F0}m");
                }

                // Warm just inside each boundary, then cross it without giving the renderer an
                // artificial convergence pause. The previous LOD must remain visible until the
                // replacement LOD is publishable; a single magenta frame is a real player hole.
                foreach (float boundary in LodBoundaries)
                {
                    float start = boundary - 12f;
                    float finish = boundary + 12f;
                    SetTopDownTerrainView(camera, terrainTarget, start);
                    yield return WaitForTerrainCoverage(
                        camera, target, readback, $"terrain transition warmup at {boundary:F0}m");

                    const int transitionFrames = 48;
                    for (int frame = 1; frame <= transitionFrames; frame++)
                    {
                        float distance = Mathf.Lerp(start, finish, frame / (float)transitionFrames);
                        SetTopDownTerrainView(camera, terrainTarget, distance);
                        RenderUrpCamera(camera);
                        yield return null;

                        float clearFraction = CaptureClearFraction(target, readback);
                        VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                        Assert.LessOrEqual(clearFraction, MaxTerrainClearFraction,
                            $"Visible terrain hole while crossing the {boundary:F0}m LOD boundary "
                          + $"at distance {distance:F2}m (clear={clearFraction:P2}). "
                          + MetricsSummary(metrics));
                        Assert.AreEqual(0, metrics.MissingVisibleSolidChunks,
                            $"Renderer reported missing visible chunks while crossing the "
                          + $"{boundary:F0}m LOD boundary at {distance:F2}m. "
                          + MetricsSummary(metrics));
                    }
                }
            }
            finally
            {
                VoxelRenderBridge.SolidBuildBudgetMs = oldBuildBudgetMs;
                RestoreCameraState(camera, in cameraState);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Castle_PreservesPublishedCoverageAtEveryLodAndAcrossTransitions()
        {
            yield return LoadShowcaseScene();
            GetShowcaseContext(out VoxelShowcase showcase, out ShowcaseWorld world,
                               out Camera camera, out CastlePlan plan, out Vector3 centre);
            ConfigureFlyCamera(showcase);

            VoxelFarTerrain farTerrain = Object.FindFirstObjectByType<VoxelFarTerrain>();
            if (farTerrain != null) farTerrain.enabled = false;

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(Width, Height, TextureFormat.RGB24, false, true);
            CameraState cameraState = CaptureCameraState(camera);
            double oldBuildBudgetMs = VoxelRenderBridge.SolidBuildBudgetMs;
            try
            {
                ConfigureCoverageCamera(camera, target, orthographicSize: 24f);
                VoxelRenderBridge.SolidBuildBudgetMs = 8.0;
                Vector3 lookAt = centre + Vector3.up * 10f;
                RectInt crop = new(Width / 4, Height / 5,
                                   Width / 2, Height * 13 / 20);

                // Step 1 is the authoritative visual coverage reference. Only pixels that are
                // actually occupied at full detail are required at coarser LODs, so legitimate
                // windows, doors, courtyards and sky are not mistaken for holes.
                SetCastleView(camera, centre, lookAt, LodBands[0].distance);
                var reference = new CoverageMask();
                yield return WaitForStableCastleCoverage(
                    camera, target, readback, crop, reference,
                    $"castle LOD step {LodBands[0].step}");
                Assert.Greater(reference.ForegroundCount, 500,
                    "Step-1 castle coverage reference is too small to be a useful hole detector.");

                for (int i = 1; i < LodBands.Length; i++)
                {
                    (int step, float distance) = LodBands[i];
                    SetCastleView(camera, centre, lookAt, distance);
                    var coarse = new CoverageMask();
                    yield return WaitForStableCastleCoverage(
                        camera, target, readback, crop, coarse, $"castle LOD step {step}");
                    AssertCastleCoverage(reference, coarse, step, distance, "stable band");
                }

                // Orthographic framing is invariant as the camera moves along its view axis, so
                // the step-1 mask remains a valid silhouette/coverage reference while crossing
                // each distance boundary. Do not wait for convergence inside the transition.
                foreach (float boundary in LodBoundaries)
                {
                    float start = boundary - 12f;
                    float finish = boundary + 12f;
                    SetCastleView(camera, centre, lookAt, start);
                    var warm = new CoverageMask();
                    yield return WaitForStableCastleCoverage(
                        camera, target, readback, crop, warm,
                        $"castle transition warmup at {boundary:F0}m");

                    const int transitionFrames = 48;
                    for (int frame = 1; frame <= transitionFrames; frame++)
                    {
                        float distance = Mathf.Lerp(start, finish, frame / (float)transitionFrames);
                        SetCastleView(camera, centre, lookAt, distance);
                        RenderUrpCamera(camera);
                        yield return null;

                        CoverageMask current = CaptureForegroundMask(target, readback, crop);
                        VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                        Assert.AreEqual(0, metrics.MissingVisibleSolidChunks,
                            $"Renderer reported a castle hole crossing {boundary:F0}m at "
                          + $"{distance:F2}m. {MetricsSummary(metrics)}");
                        AssertCastleCoverage(reference, current, ExpectedStep(distance), distance,
                                             $"transition across {boundary:F0}m");
                    }
                }
            }
            finally
            {
                VoxelRenderBridge.SolidBuildBudgetMs = oldBuildBudgetMs;
                RestoreCameraState(camera, in cameraState);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
            }
        }

        private static IEnumerator LoadShowcaseScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            int frames = 0;
            double deadline = Time.realtimeSinceStartupAsDouble + 60.0;
            while ((!VoxelRenderBridge.SurfaceBuildEnabled || !VoxelRenderBridge.TryGetWorld(out _))
                   && frames++ < 3600
                   && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            Assert.True(VoxelRenderBridge.SurfaceBuildEnabled,
                "VoxelShowcase never enabled production surface rendering.");
            Assert.True(VoxelRenderBridge.TryGetWorld(out _),
                "VoxelShowcase never bound a renderable world.");
        }

        private static void GetShowcaseContext(out VoxelShowcase showcase,
                                               out ShowcaseWorld world,
                                               out Camera camera,
                                               out CastlePlan plan,
                                               out Vector3 castleCentre)
        {
            showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            Assert.NotNull(world);
            camera = Camera.main;
            Assert.NotNull(camera);

            int ground = world.SurfaceHeight(256, 376);
            plan = StructuresComposition.PlanCastle(new int3(256, ground, 376), world.Seed);
            castleCentre = new Vector3(plan.Centre.x,
                                       plan.Centre.y + plan.PlateauHeight,
                                       plan.Centre.z) * ShowcaseWorld.VoxelSize;
        }

        private static void ConfigureFlyCamera(VoxelShowcase showcase)
        {
            typeof(VoxelShowcase).GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase).GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);
        }

        private static void ConfigureCoverageCamera(Camera camera, RenderTexture target,
                                                    float orthographicSize)
        {
            camera.targetTexture = target;
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CoverageClear;
            camera.allowHDR = false;
            UniversalAdditionalCameraData additional = camera.GetUniversalAdditionalCameraData();
            if (additional != null) additional.renderPostProcessing = false;
        }

        private static void SetTopDownTerrainView(Camera camera, Vector3 target, float distance)
        {
            camera.transform.position = target + Vector3.up * distance;
            camera.transform.LookAt(target, Vector3.forward);
            camera.nearClipPlane = Mathf.Max(0.3f, distance - 40f);
            camera.farClipPlane = distance + 40f;
        }

        private static void SetCastleView(Camera camera, Vector3 centre, Vector3 lookAt,
                                          float distance)
        {
            camera.transform.position = centre + new Vector3(0f, 20f, -distance);
            camera.transform.LookAt(lookAt);
            camera.nearClipPlane = Mathf.Max(0.3f, distance - 36f);
            camera.farClipPlane = distance + 36f;
        }

        private static IEnumerator WaitForTerrainCoverage(Camera camera, RenderTexture target,
                                                          Texture2D readback, string label)
        {
            int stable = 0;
            int frames = 0;
            float lastClear = 1f;
            VoxelSurfaceMetrics lastMetrics = default;
            double deadline = Time.realtimeSinceStartupAsDouble + 25.0;
            while (frames++ < 1500 && Time.realtimeSinceStartupAsDouble < deadline)
            {
                RenderUrpCamera(camera);
                yield return null;
                lastClear = CaptureClearFraction(target, readback);
                lastMetrics = VoxelRenderBridge.SurfaceMetrics;
                bool covered = lastClear <= MaxTerrainClearFraction
                            && lastMetrics.VisibleSolidChunks > 0
                            && lastMetrics.MissingVisibleSolidChunks == 0;
                stable = covered ? stable + 1 : 0;
                if (stable >= StableFrames) yield break;
            }

            Assert.Fail($"{label} never became hole-free after {frames} frames "
                      + $"(clear={lastClear:P2}). {MetricsSummary(lastMetrics)}");
        }

        private static IEnumerator WaitForStableCastleCoverage(Camera camera, RenderTexture target,
                                                               Texture2D readback, RectInt crop,
                                                               CoverageMask result, string label)
        {
            int stable = 0;
            int frames = 0;
            int previousForeground = -1;
            CoverageMask last = null;
            VoxelSurfaceMetrics lastMetrics = default;
            double deadline = Time.realtimeSinceStartupAsDouble + 25.0;
            while (frames++ < 1500 && Time.realtimeSinceStartupAsDouble < deadline)
            {
                RenderUrpCamera(camera);
                yield return null;
                last = CaptureForegroundMask(target, readback, crop);
                lastMetrics = VoxelRenderBridge.SurfaceMetrics;

                bool countStable = previousForeground > 0
                    && Mathf.Abs(last.ForegroundCount - previousForeground)
                       <= Mathf.Max(2, previousForeground / 100);
                bool healthy = last.ForegroundCount > 200
                            && lastMetrics.VisibleSolidChunks > 0
                            && lastMetrics.MissingVisibleSolidChunks == 0;
                stable = healthy && countStable ? stable + 1 : 0;
                previousForeground = last.ForegroundCount;
                if (stable < StableFrames) continue;

                result.CopyFrom(last);
                yield break;
            }

            Assert.Fail($"{label} never reached stable hole-free castle coverage after {frames} frames; "
                      + $"foreground={last?.ForegroundCount ?? 0}. {MetricsSummary(lastMetrics)}");
        }

        private static void AssertCastleCoverage(CoverageMask reference, CoverageMask current,
                                                 int step, float distance, string phase)
        {
            Assert.NotNull(reference);
            Assert.NotNull(current);
            float recall = ForegroundRecall(reference, current, tolerancePixels: 1);
            float ratio = current.ForegroundCount / (float)Mathf.Max(1, reference.ForegroundCount);
            VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
            Assert.GreaterOrEqual(recall, MinCastleForegroundRecall,
                $"Castle lost published foreground coverage at LOD step {step}, {distance:F2}m "
              + $"during {phase}: recall={recall:P1}, foregroundRatio={ratio:P1}. "
              + MetricsSummary(metrics));
            Assert.GreaterOrEqual(ratio, MinCastleForegroundRatio,
                $"Castle lost too much visible mass at LOD step {step}, {distance:F2}m "
              + $"during {phase}: foregroundRatio={ratio:P1}, recall={recall:P1}. "
              + MetricsSummary(metrics));
        }

        private static float CaptureClearFraction(RenderTexture target, Texture2D readback)
        {
            Color32[] pixels = ReadPixels(target, readback);
            int clear = 0;
            for (int i = 0; i < pixels.Length; i++)
                if (IsCoverageClear(pixels[i])) clear++;
            return clear / (float)pixels.Length;
        }

        private static CoverageMask CaptureForegroundMask(RenderTexture target, Texture2D readback,
                                                          RectInt crop)
        {
            Color32[] pixels = ReadPixels(target, readback);
            int width = crop.width;
            int height = crop.height;
            var foreground = new bool[width * height];
            int count = 0;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int source = crop.x + x + (crop.y + y) * target.width;
                bool occupied = !IsCoverageClear(pixels[source]);
                foreground[x + y * width] = occupied;
                if (occupied) count++;
            }
            return new CoverageMask(width, height, foreground, count);
        }

        private static Color32[] ReadPixels(RenderTexture target, Texture2D readback)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            readback.Apply(false, false);
            RenderTexture.active = previous;
            return readback.GetPixels32();
        }

        private static bool IsCoverageClear(Color32 pixel) =>
            pixel.r >= 220 && pixel.g <= 40 && pixel.b >= 220;

        private static float ForegroundRecall(CoverageMask reference, CoverageMask current,
                                              int tolerancePixels)
        {
            if (reference.ForegroundCount <= 0) return 0f;
            int matched = 0;
            for (int y = 0; y < reference.Height; y++)
            for (int x = 0; x < reference.Width; x++)
            {
                if (!reference.Foreground[x + y * reference.Width]) continue;
                bool found = false;
                for (int dy = -tolerancePixels; dy <= tolerancePixels && !found; dy++)
                for (int dx = -tolerancePixels; dx <= tolerancePixels; dx++)
                {
                    int sx = x + dx;
                    int sy = y + dy;
                    if (sx < 0 || sy < 0 || sx >= current.Width || sy >= current.Height) continue;
                    if (!current.Foreground[sx + sy * current.Width]) continue;
                    found = true;
                    break;
                }
                if (found) matched++;
            }
            return matched / (float)reference.ForegroundCount;
        }

        private static int ExpectedStep(float distance)
        {
            if (distance < 96f) return 1;
            if (distance < 192f) return 2;
            if (distance < 288f) return 4;
            return 8;
        }

        private static string MetricsSummary(VoxelSurfaceMetrics metrics) =>
            $"known={metrics.SolidKnownChunks} resident={metrics.SolidResidentChunks} "
          + $"dirty={metrics.SolidDirtyChunks} visible={metrics.VisibleSolidChunks} "
          + $"missing={metrics.MissingVisibleSolidChunks} jobs={metrics.RunningSolidJobs} "
          + $"pendingUpload={metrics.SolidPendingUploadBytes} "
          + $"completed={metrics.CompletedSolidBuilds} uploaded={metrics.UploadedGeometryBytes} "
          + $"arena={metrics.SolidArenaUsedBytes}/{metrics.SolidArenaCommittedBytes}B "
          + $"arenaFailures={metrics.SolidArenaAllocationFailures} "
          + $"pressureEvictions={metrics.SolidArenaPressureEvictions} "
          + $"step4=known:{metrics.Step4KnownChunks}/resident:{metrics.Step4ResidentChunks}/"
          + $"dirty:{metrics.Step4DirtyChunks}/missing:{metrics.Step4MissingVisibleChunks}/"
          + $"jobs:{metrics.Step4RunningJobs}.";

        private static void RenderUrpCamera(Camera camera)
        {
            Assert.NotNull(camera);
            Assert.NotNull(camera.targetTexture);
            Assert.True(VoxelRenderBridge.TryGetWorld(out _),
                "VoxelShowcase lost its render-world binding.");

            var request = new UniversalRenderPipeline.SingleCameraRequest
            {
                destination = camera.targetTexture,
            };
            Assert.True(RenderPipeline.SupportsRenderRequest(camera, request),
                "Active URP renderer does not support SingleCameraRequest.");
            VoxelRenderBridge.ResetSurfacePassDiagnostics("renderer-lod-coverage");
            RenderPipeline.SubmitRenderRequest(camera, request);
            Assert.Greater(VoxelRenderBridge.RenderFeatureEnqueueCount, 0,
                "URP request did not enqueue VoxelRenderFeature.");
            Assert.Greater(VoxelRenderBridge.SurfacePassRecordCount, 0,
                "URP request did not record VoxelRenderPass.");
            Assert.AreEqual("feature-aware", VoxelRenderBridge.LastSurfacePassState,
                $"VoxelRenderPass returned early: {VoxelRenderBridge.LastSurfacePassState}.");
        }

        private sealed class CoverageMask
        {
            public int Width { get; private set; }
            public int Height { get; private set; }
            public bool[] Foreground { get; private set; }
            public int ForegroundCount { get; private set; }

            public CoverageMask() { }

            public CoverageMask(int width, int height, bool[] foreground, int foregroundCount)
            {
                Width = width;
                Height = height;
                Foreground = foreground;
                ForegroundCount = foregroundCount;
            }

            public void CopyFrom(CoverageMask other)
            {
                Width = other.Width;
                Height = other.Height;
                Foreground = other.Foreground;
                ForegroundCount = other.ForegroundCount;
            }
        }

        private readonly struct CameraState
        {
            public readonly RenderTexture Target;
            public readonly bool Orthographic;
            public readonly float OrthographicSize;
            public readonly float Near;
            public readonly float Far;
            public readonly CameraClearFlags ClearFlags;
            public readonly Color Background;
            public readonly bool AllowHdr;
            public readonly bool RenderPostProcessing;

            public CameraState(Camera camera)
            {
                Target = camera.targetTexture;
                Orthographic = camera.orthographic;
                OrthographicSize = camera.orthographicSize;
                Near = camera.nearClipPlane;
                Far = camera.farClipPlane;
                ClearFlags = camera.clearFlags;
                Background = camera.backgroundColor;
                AllowHdr = camera.allowHDR;
                UniversalAdditionalCameraData additional = camera.GetUniversalAdditionalCameraData();
                RenderPostProcessing = additional != null && additional.renderPostProcessing;
            }
        }

        private static CameraState CaptureCameraState(Camera camera) => new(camera);

        private static void RestoreCameraState(Camera camera, in CameraState state)
        {
            camera.targetTexture = state.Target;
            camera.orthographic = state.Orthographic;
            camera.orthographicSize = state.OrthographicSize;
            camera.nearClipPlane = state.Near;
            camera.farClipPlane = state.Far;
            camera.clearFlags = state.ClearFlags;
            camera.backgroundColor = state.Background;
            camera.allowHDR = state.AllowHdr;
            UniversalAdditionalCameraData additional = camera.GetUniversalAdditionalCameraData();
            if (additional != null) additional.renderPostProcessing = state.RenderPostProcessing;
        }
    }
}
