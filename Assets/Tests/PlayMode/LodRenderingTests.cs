using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class LodRenderingTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";

        [Test]
        public void StepEightUsesFeaturePreservingVoxelSamples()
        {
            Assert.AreEqual(-1, VoxelReadGrid.LevelForStride(8),
                "Step 8 must not turn an any-solid 8^3 storage block into a render sample.");
            using var cache = new CpuTransvoxelChunkCache(8);
            Assert.False(cache.SamplesFromMips,
                "The castle's 288-420m LOD must preserve voxel features rather than OR-collapsing them.");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator CastleKeepsVoxelGeometryAcrossEveryLodBand()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            Camera camera = Camera.main;
            Assert.NotNull(camera);

            typeof(VoxelShowcase).GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase).GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);

            int ground = world.SurfaceHeight(256, 376);
            CastlePlan plan = CastleBuilder.Plan(new int3(256, ground, 376), world.Seed);
            Vector3 centre = new Vector3(plan.Centre.x, plan.Centre.y + plan.PlateauHeight,
                                         plan.Centre.z) * 0.1f;
            Vector3 lookAt = centre + Vector3.up * 10f;
            var bands = new (int step, float distance)[]
            {
                (1, 48f), (2, 144f), (4, 240f), (8, 340f),
            };

            var target = new RenderTexture(64, 36, 24, RenderTextureFormat.ARGB32);
            try
            {
                camera.targetTexture = target;
                foreach (var band in bands)
                {
                    camera.transform.position = centre + new Vector3(0f, 20f, -band.distance);
                    camera.transform.LookAt(lookAt);
                    for (int frame = 0; frame < 24; frame++)
                    {
                        camera.Render();
                        yield return null;
                    }

                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.Greater(metrics.VisibleSolidChunks, 0,
                        $"LOD step {band.step} produced no visible voxel geometry.");
                    Assert.AreEqual(0, metrics.MissingVisibleSolidChunks,
                        $"LOD step {band.step} retired voxel geometry before replacement was ready.");
                    Assert.Greater(metrics.UploadedGeometryBytes, 0ul,
                        $"LOD step {band.step} did not use the voxel surface extractor.");
                }
            }
            finally
            {
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator GeometryUploadStaysWithinGlobalBudgetWhileCrossingLodBands()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            Camera camera = Camera.main;
            Assert.NotNull(camera);

            typeof(VoxelShowcase).GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase).GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);

            int ground = world.SurfaceHeight(256, 376);
            CastlePlan plan = CastleBuilder.Plan(new int3(256, ground, 376), world.Seed);
            Vector3 centre = new Vector3(plan.Centre.x, plan.Centre.y + plan.PlateauHeight,
                                         plan.Centre.z) * 0.1f;
            Vector3 lookAt = centre + Vector3.up * 10f;

            int oldBudget = VoxelRenderBridge.SolidUploadBudgetBytes;
            int oldSlice = VoxelRenderBridge.SolidUploadSliceBytes;
            int oldWorkers = VoxelRenderBridge.SolidUploadWorkerBudget;
            double oldUploadMs = VoxelRenderBridge.SolidUploadBudgetMs;
            var target = new RenderTexture(64, 36, 24, RenderTextureFormat.ARGB32);
            bool sawUpload = false;
            bool sawQueuedReplacement = false;
            try
            {
                // Make payload bytes, not wall-clock time, the limiting factor so this test proves
                // a large replacement spans frames instead of being published in one render call.
                VoxelRenderBridge.SolidUploadBudgetBytes = 16 * 1024;
                VoxelRenderBridge.SolidUploadSliceBytes = 4 * 1024;
                VoxelRenderBridge.SolidUploadWorkerBudget = 4;
                VoxelRenderBridge.SolidUploadBudgetMs = 5.0;
                camera.targetTexture = target;

                for (int frame = 0; frame < 180; frame++)
                {
                    float phase = Mathf.PingPong(frame / 90f, 1f);
                    float distance = Mathf.Lerp(48f, 380f, phase);
                    camera.transform.position = centre
                        + new Vector3(Mathf.Sin(frame * 0.07f) * 18f, 20f, -distance);
                    camera.transform.LookAt(lookAt);
                    camera.Render();
                    yield return null;

                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.AreEqual(16 * 1024, metrics.SolidUploadBudgetBytes,
                        "Render pass did not apply the renderer-wide upload budget.");
                    Assert.LessOrEqual(metrics.LastFrameSolidUploadedBytes,
                        metrics.SolidUploadBudgetBytes,
                        "Solid geometry upload exceeded the renderer-wide frame budget.");
                    sawUpload |= metrics.LastFrameSolidUploadedBytes > 0;
                    sawQueuedReplacement |= metrics.SolidPendingUploadBytes > 0;
                }

                Assert.IsTrue(sawUpload,
                    "LOD traversal never exercised solid geometry publication.");
                Assert.IsTrue(sawQueuedReplacement,
                    "A 16 KiB frame cap should force at least one replacement to remain queued.");
            }
            finally
            {
                VoxelRenderBridge.SolidUploadBudgetBytes = oldBudget;
                VoxelRenderBridge.SolidUploadSliceBytes = oldSlice;
                VoxelRenderBridge.SolidUploadWorkerBudget = oldWorkers;
                VoxelRenderBridge.SolidUploadBudgetMs = oldUploadMs;
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}