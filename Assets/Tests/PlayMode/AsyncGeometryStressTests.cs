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
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class AsyncGeometryStressTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";

        [UnityTest, Timeout(900000)]
        public IEnumerator ContinuousLodTraversalAndDestructionRespectFrameUploadBudget()
        {
            yield return LoadShowcase(out VoxelShowcase showcase, out ShowcaseWorld world,
                                     out Camera camera, out CastlePlan plan, out Vector3 centre);

            int oldBudget = VoxelRenderBridge.SolidUploadBudgetBytes;
            int oldSlice = VoxelRenderBridge.SolidUploadSliceBytes;
            int oldWorkers = VoxelRenderBridge.SolidUploadWorkerBudget;
            double oldUploadMs = VoxelRenderBridge.SolidUploadBudgetMs;
            var target = new RenderTexture(64, 36, 24, RenderTextureFormat.ARGB32);
            int successfulExplosions = 0;
            bool sawUpload = false;
            bool sawPendingReplacement = false;
            int maxVisible = 0;
            try
            {
                VoxelRenderBridge.SolidUploadBudgetBytes = 16 * 1024;
                VoxelRenderBridge.SolidUploadSliceBytes = 4 * 1024;
                VoxelRenderBridge.SolidUploadWorkerBudget = 4;
                VoxelRenderBridge.SolidUploadBudgetMs = 5.0;
                camera.targetTexture = target;

                // Warm the initial ring before mixing camera churn and edits.
                camera.transform.position = centre + new Vector3(0f, 20f, -96f);
                camera.transform.LookAt(centre + Vector3.up * 10f);
                for (int frame = 0; frame < 90; frame++)
                {
                    camera.Render();
                    yield return null;
                }

                for (int frame = 0; frame < 240; frame++)
                {
                    float phase = Mathf.PingPong(frame / 120f, 1f);
                    float distance = Mathf.Lerp(60f, 380f, phase);
                    camera.transform.position = centre + new Vector3(
                        Mathf.Sin(frame * 0.061f) * 22f,
                        18f + Mathf.Sin(frame * 0.037f) * 5f,
                        -distance);
                    camera.transform.LookAt(centre + Vector3.up * 10f);

                    if ((frame % 12) == 0)
                    {
                        float angle = frame * 0.31f;
                        int x = plan.Centre.x + Mathf.RoundToInt(Mathf.Cos(angle) * 28f);
                        int z = plan.Centre.z + Mathf.RoundToInt(Mathf.Sin(angle) * 28f);
                        int y = world.SurfaceHeight(x, z);
                        if (world.Explode(new int3(x, y, z), 2) > 0)
                            successfulExplosions++;
                    }

                    camera.Render();
                    yield return null;

                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.AreEqual(16 * 1024, metrics.SolidUploadBudgetBytes,
                        "Stress path lost the renderer-wide upload budget.");
                    Assert.LessOrEqual(metrics.LastFrameSolidUploadedBytes,
                        metrics.SolidUploadBudgetBytes,
                        "Camera/edit churn exceeded the renderer-wide geometry upload cap.");
                    Assert.GreaterOrEqual(metrics.LastFrameSolidUploadedBytes, 0);
                    maxVisible = Mathf.Max(maxVisible, metrics.VisibleSolidChunks);
                    sawUpload |= metrics.LastFrameSolidUploadedBytes > 0;
                    sawPendingReplacement |= metrics.SolidPendingUploadBytes > 0;
                }

                Assert.GreaterOrEqual(successfulExplosions, 6,
                    "Stress test did not produce enough real voxel mutations.");
                Assert.Greater(maxVisible, 0,
                    "Camera traversal never produced visible solid geometry.");
                Assert.True(sawUpload,
                    "Camera/edit stress never exercised GPU geometry publication.");
                Assert.True(sawPendingReplacement,
                    "A 16 KiB frame cap should produce queued replacement geometry under stress.");
            }
            finally
            {
                RestoreUploadBudget(oldBudget, oldSlice, oldWorkers, oldUploadMs);
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator EditedVisibleChunkKeepsOldGeometryUntilReplacementPublishes()
        {
            yield return LoadShowcase(out _, out ShowcaseWorld world,
                                     out Camera camera, out CastlePlan plan, out Vector3 centre);

            int oldBudget = VoxelRenderBridge.SolidUploadBudgetBytes;
            int oldSlice = VoxelRenderBridge.SolidUploadSliceBytes;
            int oldWorkers = VoxelRenderBridge.SolidUploadWorkerBudget;
            double oldUploadMs = VoxelRenderBridge.SolidUploadBudgetMs;
            var target = new RenderTexture(64, 36, 24, RenderTextureFormat.ARGB32);
            bool sawPendingReplacement = false;
            try
            {
                // Make even a modest rebuilt chunk span multiple frames so the invariant is
                // observable: the old published lease must remain draw-visible until swap.
                VoxelRenderBridge.SolidUploadBudgetBytes = 4 * 1024;
                VoxelRenderBridge.SolidUploadSliceBytes = 1024;
                VoxelRenderBridge.SolidUploadWorkerBudget = 2;
                VoxelRenderBridge.SolidUploadBudgetMs = 5.0;
                camera.targetTexture = target;
                camera.transform.position = centre + new Vector3(0f, 18f, -48f);
                camera.transform.LookAt(centre + Vector3.up * 8f);

                bool warmed = false;
                for (int frame = 0; frame < 180; frame++)
                {
                    camera.Render();
                    yield return null;
                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    warmed = metrics.VisibleSolidChunks > 0
                          && metrics.MissingVisibleSolidChunks == 0
                          && metrics.SolidPendingUploadBytes == 0;
                    if (warmed) break;
                }
                Assert.True(warmed, "Could not establish a fully published near-LOD baseline.");

                // Keep edits well inside one 64-voxel step-1 extraction chunk so a halo crossing
                // does not manufacture a newly visible neighbour and confuse replacement-hole
                // detection. These points remain close to the castle and in the camera view.
                var editOffsets = new[]
                {
                    new int2(24, -24), new int2(28, -18), new int2(20, -30),
                    new int2(30, -28), new int2(18, -20), new int2(26, -32),
                };

                int successfulExplosions = 0;
                foreach (int2 offset in editOffsets)
                {
                    int x = plan.Centre.x + offset.x;
                    int z = plan.Centre.z + offset.y;
                    int y = world.SurfaceHeight(x, z);
                    if (world.Explode(new int3(x, y, z), 2) > 0)
                        successfulExplosions++;

                    for (int frame = 0; frame < 24; frame++)
                    {
                        camera.Render();
                        yield return null;
                        VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                        Assert.LessOrEqual(metrics.LastFrameSolidUploadedBytes,
                            metrics.SolidUploadBudgetBytes,
                            "Replacement upload exceeded the frame budget.");
                        sawPendingReplacement |= metrics.SolidPendingUploadBytes > 0;
                        if (metrics.SolidPendingUploadBytes > 0)
                        {
                            Assert.AreEqual(0, metrics.MissingVisibleSolidChunks,
                                "Visible edited geometry disappeared while its replacement was still queued.");
                            Assert.Greater(metrics.VisibleSolidChunks, 0,
                                "Replacement staging created a visible geometry hole.");
                        }
                    }
                }

                Assert.GreaterOrEqual(successfulExplosions, 3,
                    "Replacement test did not mutate enough visible terrain near the castle.");
                Assert.True(sawPendingReplacement,
                    "Tiny publication slices never exposed a pending replacement window.");
            }
            finally
            {
                RestoreUploadBudget(oldBudget, oldSlice, oldWorkers, oldUploadMs);
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static IEnumerator LoadShowcase(out VoxelShowcase showcase,
                                                out ShowcaseWorld world,
                                                out Camera camera,
                                                out CastlePlan plan,
                                                out Vector3 centre)
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            camera = Camera.main;
            Assert.NotNull(camera);

            typeof(VoxelShowcase).GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase).GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);

            int ground = world.SurfaceHeight(256, 376);
            plan = CastleBuilder.Plan(new int3(256, ground, 376), world.Seed);
            centre = new Vector3(plan.Centre.x, plan.Centre.y + plan.PlateauHeight,
                                 plan.Centre.z) * 0.1f;
        }

        private static void RestoreUploadBudget(int bytes, int slice, int workers, double milliseconds)
        {
            VoxelRenderBridge.SolidUploadBudgetBytes = bytes;
            VoxelRenderBridge.SolidUploadSliceBytes = slice;
            VoxelRenderBridge.SolidUploadWorkerBudget = workers;
            VoxelRenderBridge.SolidUploadBudgetMs = milliseconds;
        }
    }
}
