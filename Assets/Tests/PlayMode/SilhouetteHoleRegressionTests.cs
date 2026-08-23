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
    /// <summary>
    /// Fast release-blocking reproduction for the coarse rectangular holes historically visible
    /// in the castle silhouette view. Keep this separate from screenshot/lookdev capture so the
    /// one number we care about is not hidden behind a memory-heavy multi-view shard.
    /// </summary>
    public sealed class SilhouetteHoleRegressionTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";

        [UnityTest, Timeout(900000)]
        public IEnumerator ProductionSilhouetteHasZeroMissingVisibleSolidChunks()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            Assert.NotNull(world);

            Camera camera = Camera.main;
            Assert.NotNull(camera);

            // Match the existing lookdev preparation exactly without allocating a screenshot.
            var drainTarget = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = drainTarget;
            for (int frame = 0; frame < 8; frame++)
            {
                camera.Render();
                yield return null;
            }
            camera.targetTexture = null;
            drainTarget.Release();
            Object.DestroyImmediate(drainTarget);

            typeof(VoxelShowcase)
                .GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase)
                .GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);

            int ground = world.SurfaceHeight(256, 376);
            CastlePlan plan = StructuresComposition.PlanCastle(
                new int3(256, ground, 376), world.Seed);
            int baseY = plan.Centre.y + plan.PlateauHeight;
            Vector3 centre = new Vector3(plan.Centre.x, baseY, plan.Centre.z) * 0.1f;

            // This is the exact historical failing view from CastleExteriorLookdevTests.
            camera.transform.position = centre + new Vector3(82f, 29f, -82f);
            camera.transform.LookAt(centre + new Vector3(0f, 11f, 0f));

            // Preserve the 16:9 frustum used by the real capture, then let the view settle.
            //
            // The camera was just teleported across the world, so the renderer starts this view
            // knowing nothing: a single warmup frame can only ever observe an empty scheduler, which
            // reads as "no holes" for the trivial reason that nothing is visible yet. The gate has to
            // drive the view to convergence and only then assert, or it proves nothing. Coverage must
            // also stay closed once reached, so the run requires several consecutive clean frames
            // rather than catching one lucky instant.
            var warmupTarget = new RenderTexture(64, 36, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = warmupTarget;

            VoxelSurfaceMetrics metrics = default;
            int cleanStreak = 0;
            int convergedFrame = -1;
            for (int frame = 0; frame < MaxConvergenceFrames; frame++)
            {
                camera.Render();
                yield return null;

                metrics = VoxelRenderBridge.SurfaceMetrics;
                bool covered = metrics.MissingVisibleSolidChunks == 0
                            && metrics.VisibleSolidChunks > 0;
                cleanStreak = covered ? cleanStreak + 1 : 0;
                if (cleanStreak < RequiredCleanFrames) continue;

                convergedFrame = frame;
                break;
            }

            camera.targetTexture = null;
            warmupTarget.Release();
            Object.DestroyImmediate(warmupTarget);

            Debug.Log($"### SILHOUETTE_HOLE_GATE holes={metrics.MissingVisibleSolidChunks} "
                    + $"visible={metrics.VisibleSolidChunks} "
                    + $"resident={metrics.SolidResidentChunks} "
                    + $"convergedFrame={convergedFrame} "
                    + $"arenaVerts={metrics.SolidArenaUsedVertices}/{metrics.SolidArenaVertexCapacity} "
                    + $"arenaIndices={metrics.SolidArenaUsedIndices}/{metrics.SolidArenaIndexCapacity} "
                    + $"arenaFail={metrics.SolidArenaAllocationFailures} "
                    + $"step4Missing={metrics.Step4MissingVisibleChunks} "
                    + $"step4Known={metrics.Step4KnownChunks} "
                    + $"step4Resident={metrics.Step4ResidentChunks} "
                    + $"pinReject={metrics.Step4ExactMetadataPinRejects}");

            Assert.Greater(metrics.VisibleSolidChunks, 0,
                "The gate did not exercise the authoritative voxel surface renderer.");
            Assert.AreEqual(0, metrics.MissingVisibleSolidChunks,
                "The production silhouette view contains coarse chunks inside the frustum without "
              + "drawable geometry. Rectangular rendering holes are release-blocking.");
            Assert.AreNotEqual(-1, convergedFrame,
                $"The silhouette view never held {RequiredCleanFrames} consecutive hole-free frames "
              + $"within {MaxConvergenceFrames} frames.");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator SceneIssue20260823013834177WallViewConvergesWithoutMissingSolidChunks()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            Assert.NotNull(world);
            Camera camera = Camera.main;
            Assert.NotNull(camera);

            typeof(VoxelShowcase)
                .GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase)
                .GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);

            var target = new RenderTexture(162, 90, 24, RenderTextureFormat.ARGB32);
            RenderTexture oldTarget = camera.targetTexture;
            float oldFov = camera.fieldOfView;
            float oldNear = camera.nearClipPlane;
            float oldFar = camera.farClipPlane;
            VoxelSurfaceMetrics metrics = default;
            int cleanStreak = 0;
            int convergedFrame = -1;

            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.fieldOfView = 70f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 16000f;
                camera.transform.SetPositionAndRotation(
                    new Vector3(99.23580932617188f, 26.450145721435548f, -4.822741508483887f),
                    new Quaternion(-0.11285238713026047f, 0.3324708342552185f,
                                   0.040107980370521548f, 0.9354779124259949f));

                // Capture 20260823-013834-177 was taken after the scene had been running for
                // ~43.5 seconds. The regression is stronger: a cold teleport to the same pose must
                // still converge and then hold complete authoritative surface coverage.
                for (int frame = 0; frame < MaxConvergenceFrames; frame++)
                {
                    camera.Render();
                    yield return null;
                    metrics = VoxelRenderBridge.SurfaceMetrics;
                    bool covered = metrics.MissingVisibleSolidChunks == 0
                                && metrics.VisibleSolidChunks > 0;
                    cleanStreak = covered ? cleanStreak + 1 : 0;
                    if (cleanStreak < RequiredCleanFrames) continue;
                    convergedFrame = frame;
                    break;
                }
            }
            finally
            {
                camera.targetTexture = oldTarget;
                camera.fieldOfView = oldFov;
                camera.nearClipPlane = oldNear;
                camera.farClipPlane = oldFar;
                target.Release();
                Object.DestroyImmediate(target);
            }

            Debug.Log($"### SCENE_ISSUE_20260823_013834_177 "
                    + $"holes={metrics.MissingVisibleSolidChunks} "
                    + $"visible={metrics.VisibleSolidChunks} resident={metrics.SolidResidentChunks} "
                    + $"convergedFrame={convergedFrame} arenaFail={metrics.SolidArenaAllocationFailures} "
                    + $"step4Missing={metrics.Step4MissingVisibleChunks} "
                    + $"step4Known={metrics.Step4KnownChunks} step4Resident={metrics.Step4ResidentChunks} "
                    + $"step4Dirty={metrics.Step4DirtyChunks} step4Jobs={metrics.Step4RunningJobs} "
                    + $"step4Frustum={metrics.Step4VisibilityFrustum} "
                    + $"step4Ready={metrics.Step4VisibilityReady} "
                    + $"step4Empty={metrics.Step4VisibilityEmpty} "
                    + $"pinReject={metrics.Step4ExactMetadataPinRejects}");

            Assert.Greater(metrics.VisibleSolidChunks, 0,
                "Capture 20260823-013834-177 did not exercise the surface renderer.");
            Assert.AreNotEqual(-1, convergedFrame,
                "Capture 20260823-013834-177 never reached four consecutive frames of complete "
              + "visible solid coverage; the marked wall view still exposes an unpublished chunk.");
            Assert.AreEqual(0, metrics.MissingVisibleSolidChunks,
                "Capture 20260823-013834-177 still contains visible solid chunks without drawable geometry.");
        }

        /// <summary>
        /// Enough frames for the extractor to fill a cold view at the production per-frame budgets,
        /// with room to spare. This bounds the gate; it is not a convergence-time target, which
        /// ShowcasePerformanceTests owns.
        /// </summary>
        private const int MaxConvergenceFrames = 3000;

        /// <summary>Consecutive hole-free frames required, so one lucky instant cannot pass.</summary>
        private const int RequiredCleanFrames = 4;
    }
}
