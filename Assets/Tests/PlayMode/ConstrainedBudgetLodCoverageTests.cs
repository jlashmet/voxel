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
    /// Graceful-degradation gate for hierarchical surface coverage. Once a valid representation
    /// exists, starving refinement is allowed to delay detail but never to expose sky.
    /// </summary>
    public sealed class ConstrainedBudgetLodCoverageTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const int Width = 128;
        private const int Height = 96;
        private const float MaxClearFraction = 0.0025f;
        private static readonly Color CoverageClear = new(1f, 0f, 1f, 1f);
        private static readonly float[] Boundaries = { 96f, 192f, 288f };

        [UnityTest, Timeout(900000)]
        public IEnumerator TerrainCoverageSurvivesBothDirectionsWithTinyRefinementBudget()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            double readyDeadline = Time.realtimeSinceStartupAsDouble + 60.0;
            while ((!VoxelRenderBridge.SurfaceBuildEnabled || !VoxelRenderBridge.TryGetWorld(out _))
                   && Time.realtimeSinceStartupAsDouble < readyDeadline)
                yield return null;
            Assert.True(VoxelRenderBridge.SurfaceBuildEnabled);
            Assert.True(VoxelRenderBridge.TryGetWorld(out _));

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            ShowcaseWorld world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            Assert.NotNull(world);
            typeof(VoxelShowcase).GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(showcase, true);
            typeof(VoxelShowcase).GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(showcase, false);

            Camera camera = Camera.main;
            Assert.NotNull(camera);
            int ground = world.SurfaceHeight(256, 376);
            CastlePlan plan = StructuresComposition.PlanCastle(new int3(256, ground, 376), world.Seed);
            int terrainX = plan.Centre.x + 800;
            int terrainZ = plan.Centre.z + 800;
            int terrainY = world.SurfaceHeight(terrainX, terrainZ);
            Vector3 terrainTarget = new(terrainX * ShowcaseWorld.VoxelSize,
                                        terrainY * ShowcaseWorld.VoxelSize,
                                        terrainZ * ShowcaseWorld.VoxelSize);

            VoxelFarTerrain farTerrain = Object.FindFirstObjectByType<VoxelFarTerrain>();
            bool farTerrainEnabled = farTerrain != null && farTerrain.enabled;
            if (farTerrain != null) farTerrain.enabled = false;

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(Width, Height, TextureFormat.RGB24, false, true);
            State state = new(camera);
            try
            {
                ConfigureCoverageCamera(camera, target);

                foreach (float boundary in Boundaries)
                {
                    float inner = boundary - 12f;
                    float outer = boundary + 12f;

                    ConfigureWarmupBudgets();
                    SetTopDownView(camera, terrainTarget, inner);
                    yield return WaitForCoverage(camera, target, readback,
                        $"warm inner side of {boundary:F0}m boundary");

                    ConfigureTinyBudgets();
                    yield return CrossWithoutHole(camera, target, readback, terrainTarget,
                        inner, outer, boundary, "outward");

                    ConfigureWarmupBudgets();
                    SetTopDownView(camera, terrainTarget, outer);
                    yield return WaitForCoverage(camera, target, readback,
                        $"warm outer side of {boundary:F0}m boundary");

                    ConfigureTinyBudgets();
                    yield return CrossWithoutHole(camera, target, readback, terrainTarget,
                        outer, inner, boundary, "inward");
                }
            }
            finally
            {
                state.Restore(camera);
                if (farTerrain != null) farTerrain.enabled = farTerrainEnabled;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
            }
        }

        private static IEnumerator CrossWithoutHole(Camera camera, RenderTexture target,
                                                    Texture2D readback, Vector3 terrainTarget,
                                                    float start, float finish, float boundary,
                                                    string direction)
        {
            const int frames = 48;
            for (int frame = 1; frame <= frames; frame++)
            {
                float distance = Mathf.Lerp(start, finish, frame / (float)frames);
                SetTopDownView(camera, terrainTarget, distance);
                RenderUrpCamera(camera);
                yield return null;

                float clear = CaptureClearFraction(target, readback);
                VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                Assert.LessOrEqual(clear, MaxClearFraction,
                    $"Terrain coverage disappeared while moving {direction} across {boundary:F0}m "
                  + $"at {distance:F2}m with constrained refinement (clear={clear:P2}). "
                  + Summary(metrics));
                Assert.AreEqual(0, metrics.MissingVisibleSolidChunks,
                    $"Renderer reported missing coverage while moving {direction} across "
                  + $"{boundary:F0}m at {distance:F2}m. {Summary(metrics)}");
            }
        }

        private static IEnumerator WaitForCoverage(Camera camera, RenderTexture target,
                                                   Texture2D readback, string label)
        {
            int stable = 0;
            VoxelSurfaceMetrics metrics = default;
            float clear = 1f;
            double deadline = Time.realtimeSinceStartupAsDouble + 25.0;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                RenderUrpCamera(camera);
                yield return null;
                clear = CaptureClearFraction(target, readback);
                metrics = VoxelRenderBridge.SurfaceMetrics;
                bool healthy = clear <= MaxClearFraction
                            && metrics.VisibleSolidChunks > 0
                            && metrics.MissingVisibleSolidChunks == 0;
                stable = healthy ? stable + 1 : 0;
                if (stable >= 4) yield break;
            }
            Assert.Fail($"{label} never established baseline coverage (clear={clear:P2}). "
                      + Summary(metrics));
        }

        private static void ConfigureWarmupBudgets()
        {
            VoxelRenderBridge.SolidBuildBudgetMs = 8.0;
            VoxelRenderBridge.SolidUploadBudgetBytes = 16 * 1024 * 1024;
            VoxelRenderBridge.SolidUploadSliceBytes = 2 * 1024 * 1024;
            VoxelRenderBridge.SolidUploadWorkerBudget = 22;
            VoxelRenderBridge.SolidUploadBudgetMs = 8.0;
        }

        private static void ConfigureTinyBudgets()
        {
            VoxelRenderBridge.SolidBuildBudgetMs = 0.05;
            VoxelRenderBridge.SolidUploadBudgetBytes = 64 * 1024;
            VoxelRenderBridge.SolidUploadSliceBytes = 32 * 1024;
            VoxelRenderBridge.SolidUploadWorkerBudget = 1;
            VoxelRenderBridge.SolidUploadBudgetMs = 0.05;
        }

        private static void ConfigureCoverageCamera(Camera camera, RenderTexture target)
        {
            camera.targetTexture = target;
            camera.orthographic = true;
            camera.orthographicSize = 20f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CoverageClear;
            camera.allowHDR = false;
            UniversalAdditionalCameraData additional = camera.GetUniversalAdditionalCameraData();
            if (additional != null) additional.renderPostProcessing = false;
            VoxelRenderBridge.SurfaceDebugTint = Color.magenta;
            VoxelRenderBridge.SkyHorizon = CoverageClear;
            VoxelRenderBridge.SkyZenith = CoverageClear;
            VoxelRenderBridge.CloudOpacity = 0f;
        }

        private static void SetTopDownView(Camera camera, Vector3 target, float distance)
        {
            camera.transform.position = target + Vector3.up * distance;
            camera.transform.LookAt(target, Vector3.forward);
            camera.nearClipPlane = Mathf.Max(0.3f, distance - 40f);
            camera.farClipPlane = distance + 40f;
        }

        private static void RenderUrpCamera(Camera camera)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest
            {
                destination = camera.targetTexture,
            };
            Assert.True(RenderPipeline.SupportsRenderRequest(camera, request));
            VoxelRenderBridge.ResetSurfacePassDiagnostics("constrained-budget-lod-coverage");
            RenderPipeline.SubmitRenderRequest(camera, request);
            Assert.Greater(VoxelRenderBridge.SurfacePassRecordCount, 0);
            Assert.AreEqual("feature-aware", VoxelRenderBridge.LastSurfacePassState);
        }

        private static float CaptureClearFraction(RenderTexture target, Texture2D readback)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            readback.Apply(false, false);
            RenderTexture.active = previous;
            Color32[] pixels = readback.GetPixels32();
            int clear = 0;
            for (int i = 0; i < pixels.Length; i++)
                if (pixels[i].r >= 220 && pixels[i].g <= 40 && pixels[i].b >= 220) clear++;
            return clear / (float)pixels.Length;
        }

        private static string Summary(VoxelSurfaceMetrics metrics)
        {
            int requested = metrics.RequestedSolidP0MissingCoverage
                          + metrics.RequestedSolidP1PreserveCoverage
                          + metrics.RequestedSolidP2VisibleRefinement
                          + metrics.RequestedSolidP3Prefetch;
            return $"known={metrics.SolidKnownChunks} active={metrics.ActiveSolidCoverageNodes} "
                 + $"fallback={metrics.FallbackSolidParentNodes} cold={metrics.ColdKnownSolidChunks} "
                 + $"requested={requested} dirty={metrics.SolidDirtyChunks} "
                 + $"visible={metrics.VisibleSolidChunks} missing={metrics.MissingVisibleSolidChunks} "
                 + $"jobs={metrics.RunningSolidJobs} staging={metrics.SolidStagingBytes}B.";
        }

        private readonly struct State
        {
            private readonly RenderTexture _target;
            private readonly bool _orthographic;
            private readonly float _orthographicSize;
            private readonly float _near;
            private readonly float _far;
            private readonly CameraClearFlags _clearFlags;
            private readonly Color _background;
            private readonly bool _allowHdr;
            private readonly bool _post;
            private readonly Color _surfaceDebugTint;
            private readonly Color _skyHorizon;
            private readonly Color _skyZenith;
            private readonly float _cloudOpacity;
            private readonly double _buildMs;
            private readonly int _uploadBytes;
            private readonly int _uploadSlice;
            private readonly int _uploadWorkers;
            private readonly double _uploadMs;

            public State(Camera camera)
            {
                _target = camera.targetTexture;
                _orthographic = camera.orthographic;
                _orthographicSize = camera.orthographicSize;
                _near = camera.nearClipPlane;
                _far = camera.farClipPlane;
                _clearFlags = camera.clearFlags;
                _background = camera.backgroundColor;
                _allowHdr = camera.allowHDR;
                UniversalAdditionalCameraData additional = camera.GetUniversalAdditionalCameraData();
                _post = additional != null && additional.renderPostProcessing;
                _surfaceDebugTint = VoxelRenderBridge.SurfaceDebugTint;
                _skyHorizon = VoxelRenderBridge.SkyHorizon;
                _skyZenith = VoxelRenderBridge.SkyZenith;
                _cloudOpacity = VoxelRenderBridge.CloudOpacity;
                _buildMs = VoxelRenderBridge.SolidBuildBudgetMs;
                _uploadBytes = VoxelRenderBridge.SolidUploadBudgetBytes;
                _uploadSlice = VoxelRenderBridge.SolidUploadSliceBytes;
                _uploadWorkers = VoxelRenderBridge.SolidUploadWorkerBudget;
                _uploadMs = VoxelRenderBridge.SolidUploadBudgetMs;
            }

            public void Restore(Camera camera)
            {
                camera.targetTexture = _target;
                camera.orthographic = _orthographic;
                camera.orthographicSize = _orthographicSize;
                camera.nearClipPlane = _near;
                camera.farClipPlane = _far;
                camera.clearFlags = _clearFlags;
                camera.backgroundColor = _background;
                camera.allowHDR = _allowHdr;
                UniversalAdditionalCameraData additional = camera.GetUniversalAdditionalCameraData();
                if (additional != null) additional.renderPostProcessing = _post;
                VoxelRenderBridge.SurfaceDebugTint = _surfaceDebugTint;
                VoxelRenderBridge.SkyHorizon = _skyHorizon;
                VoxelRenderBridge.SkyZenith = _skyZenith;
                VoxelRenderBridge.CloudOpacity = _cloudOpacity;
                VoxelRenderBridge.SolidBuildBudgetMs = _buildMs;
                VoxelRenderBridge.SolidUploadBudgetBytes = _uploadBytes;
                VoxelRenderBridge.SolidUploadSliceBytes = _uploadSlice;
                VoxelRenderBridge.SolidUploadWorkerBudget = _uploadWorkers;
                VoxelRenderBridge.SolidUploadBudgetMs = _uploadMs;
            }
        }
    }
}
