using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Discriminator for SceneIssue 20260825-192751-413: proves whether the zero-frustum
    /// capture-pose failure comes from the camera frustum itself or from which surface chunks
    /// the scheduler currently considers in-band.
    /// </summary>
    public sealed class ShowcaseCapturePoseFrustumDiagnosticsTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private static readonly Vector3 CapturedPosition =
            new(77.953941f, 24.550051f, -3.345814f);
        private static readonly Quaternion CapturedRotation =
            new(-0.01155361f, -0.28760582f, -0.00346975f, 0.95767289f);

        [UnityTest, Timeout(900000)]
        public IEnumerator CapturePoseFrustumContainsForwardProbeAndSurfaceCandidates()
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
            showcase.transform.SetPositionAndRotation(CapturedPosition, CapturedRotation);

            var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32)
            {
                name = "ShowcaseCapturePoseFrustumDiagnosticsTests.CapturePose",
                antiAliasing = 1,
            };
            RenderTexture previousTarget = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;

            try
            {
                var last = VoxelRenderBridge.SurfaceMetrics;
                for (int frame = 0; frame < 1200; frame++)
                {
                    yield return null;
                    camera.Render();
                    last = VoxelRenderBridge.SurfaceMetrics;
                    if (last.VisibilityFrustumCandidates > 0)
                        yield break;
                }

                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
                Bounds forwardProbe = new(
                    camera.transform.position + camera.transform.forward * 16f,
                    Vector3.one * 2f);
                bool forwardProbeVisible = GeometryUtility.TestPlanesAABB(planes, forwardProbe);
                string rings = VoxelRenderBridge.DescribeRings?.Invoke() ?? "RINGS unavailable";

                Assert.IsTrue(forwardProbeVisible,
                    "Unity's camera frustum rejected a probe directly in front of the exact "
                  + $"captured camera pose. camera={camera.transform.position} "
                  + $"forward={camera.transform.forward} aspect={camera.aspect:F3} "
                  + $"target={camera.targetTexture?.width}x{camera.targetTexture?.height}.");

                Assert.Greater(last.VisibilityFrustumCandidates, 0,
                    "The exact captured camera frustum accepts a forward probe, but production "
                  + "surface routing supplied no frustum candidate for 1200 rendered frames. "
                  + $"known={last.VisibilityKnownCandidates} inBand={last.VisibilityInBandCandidates} "
                  + $"resident={last.SolidResidentChunks} dirty={last.SolidDirtyChunks} "
                  + $"missing={last.MissingVisibleSolidChunks} jobs={last.RunningSolidJobs}. "
                  + rings);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static void SetShowcaseField(VoxelShowcase showcase, string fieldName, object value)
        {
            FieldInfo field = typeof(VoxelShowcase).GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"VoxelShowcase field '{fieldName}' was not found.");
            field.SetValue(showcase, value);
        }
    }
}
