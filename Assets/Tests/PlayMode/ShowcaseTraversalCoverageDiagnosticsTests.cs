using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Short diagnostic form of the production traversal regression. The production failure can
    /// lose every voxel draw only a few frames after a fallback-safe view, so keep this test small
    /// enough to expose the exact scheduler state at the first discontinuity.
    /// </summary>
    public sealed class ShowcaseTraversalCoverageDiagnosticsTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";

        [UnityTest, Timeout(900000)]
        public IEnumerator ShortFlyTraversalKeepsAtLeastOneDrawableSurface()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Camera camera = Camera.main;
            Assert.NotNull(showcase);
            Assert.NotNull(camera);

            SetShowcaseField(showcase, "m_FlyMode", true);
            SetShowcaseField(showcase, "_mouseLook", false);

            var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32)
            {
                name = "ShowcaseTraversalCoverageDiagnosticsTests.ShortFlyTraversal",
                antiAliasing = 1,
            };
            RenderTexture previousTarget = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;

            try
            {
                int stableFrames = 0;
                VoxelSurfaceMetrics last = default;
                for (int frame = 0; frame < 1200 && stableFrames < 4; frame++)
                {
                    yield return null;
                    camera.Render();
                    last = VoxelRenderBridge.SurfaceMetrics;
                    stableFrames = last.VisibleSolidChunks > 0 ? stableFrames + 1 : 0;
                }

                Assert.GreaterOrEqual(stableFrames, 4,
                    "Diagnostic setup never reached four visible frames. " + Describe(-1, in last));

                Vector3 origin = showcase.transform.position;
                Quaternion originRotation = showcase.transform.rotation;
                for (int frame = 0; frame < 20; frame++)
                {
                    float progress = frame / 419f;
                    showcase.transform.position = origin + new Vector3(
                        frame * 0.5f,
                        0f,
                        Mathf.Sin(progress * Mathf.PI * 6f) * 18f);
                    showcase.transform.rotation = originRotation;

                    yield return null;
                    camera.Render();
                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;

                    Assert.AreEqual(0ul, metrics.FramePathBlockingCompletionViolations,
                        $"Short traversal frame {frame} blocked on geometry completion.");
                    Assert.Greater(metrics.VisibleSolidChunks, 0, Describe(frame, in metrics));
                }
            }
            finally
            {
                camera.targetTexture = previousTarget;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static string Describe(int frame, in VoxelSurfaceMetrics metrics) =>
            $"Short traversal frame {frame} lost every visible voxel draw; "
          + $"known={metrics.SolidKnownChunks} resident={metrics.SolidResidentChunks} "
          + $"dirty={metrics.SolidDirtyChunks} missing={metrics.MissingVisibleSolidChunks} "
          + $"jobs={metrics.RunningSolidJobs} uploads={metrics.SolidMeshesAwaitingUpload} "
          + $"candidates={metrics.VisibilityKnownCandidates}/{metrics.VisibilityInBandCandidates}/"
          + $"{metrics.VisibilityFrustumCandidates} "
          + $"step4 known={metrics.Step4KnownChunks} resident={metrics.Step4ResidentChunks} "
          + $"dirty={metrics.Step4DirtyChunks} missing={metrics.Step4MissingVisibleChunks} "
          + $"visibility={metrics.Step4VisibilityKnown}/{metrics.Step4VisibilityInBand}/"
          + $"{metrics.Step4VisibilityFrustum}/{metrics.Step4VisibilityReady}/"
          + $"{metrics.Step4VisibilityEmpty}.";

        private static void SetShowcaseField(VoxelShowcase showcase, string fieldName, object value)
        {
            FieldInfo field = typeof(VoxelShowcase).GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"VoxelShowcase field '{fieldName}' was not found.");
            field.SetValue(showcase, value);
        }
    }
}
