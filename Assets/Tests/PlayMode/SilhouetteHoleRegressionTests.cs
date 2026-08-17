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

            // Preserve the 16:9 frustum used by the real capture. One normal warmup frame is
            // intentional: atomic LOD coverage must retain a drawable parent during refinement;
            // convergence delays are not allowed to become visible holes.
            var warmupTarget = new RenderTexture(64, 36, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = warmupTarget;
            camera.Render();
            yield return null;
            camera.targetTexture = null;
            warmupTarget.Release();
            Object.DestroyImmediate(warmupTarget);

            VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
            Debug.Log($"### SILHOUETTE_HOLE_GATE holes={metrics.MissingVisibleSolidChunks} "
                    + $"visible={metrics.VisibleSolidChunks} "
                    + $"step4Missing={metrics.Step4MissingVisibleChunks} "
                    + $"step4Known={metrics.Step4KnownChunks} "
                    + $"step4Resident={metrics.Step4ResidentChunks} "
                    + $"pinReject={metrics.Step4ExactMetadataPinRejects}");

            Assert.AreEqual(0, metrics.MissingVisibleSolidChunks,
                "The production silhouette view contains coarse chunks inside the frustum without "
              + "drawable geometry. Rectangular rendering holes are release-blocking.");
            Assert.Greater(metrics.VisibleSolidChunks, 0,
                "The gate did not exercise the authoritative voxel surface renderer.");
        }
    }
}
