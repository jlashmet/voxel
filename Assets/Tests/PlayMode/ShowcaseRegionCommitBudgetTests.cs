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
    /// Behavioral regression for the streamed-region completion hitch in
    /// SceneIssue 20260825-192751-413-VoxelShowcase.
    ///
    /// Region generation is deliberately sliced, so completing a region must not turn the final
    /// slice into a bulk catch-up frame. This drives the real VoxelShowcase player across more
    /// than four production region boundaries and gates the exact frames on which regions become
    /// committed, while preserving the same near/far coverage invariant as the traversal gates.
    /// </summary>
    public sealed class ShowcaseRegionCommitBudgetTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const int TraversalFrames = 420;
        private const float StepMetres = 0.5f;
        private const double MaxRegionCompletionFrameMs = 25.0;

        [UnityTest, Timeout(900000)]
        public IEnumerator RegionCompletionFramesStayInsideTraversalBudget()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            VoxelFarTerrain far = Object.FindFirstObjectByType<VoxelFarTerrain>();
            Camera camera = Camera.main;
            Assert.NotNull(showcase);
            Assert.NotNull(far);
            Assert.NotNull(camera);

            ShowcaseWorld world = GetWorld(showcase);
            Assert.NotNull(world);
            SetShowcaseField(showcase, "m_FlyMode", true);
            SetShowcaseField(showcase, "_mouseLook", false);

            var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32)
            {
                name = "ShowcaseRegionCommitBudgetTests.Traversal",
                antiAliasing = 1,
            };
            RenderTexture previousTarget = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;

            try
            {
                yield return WaitForFallbackSafeVisibleCoverage(camera, far, 1200);

                Vector3 origin = showcase.transform.position;
                Quaternion originRotation = showcase.transform.rotation;
                int previousRegionsGenerated = world.RegionsGenerated;
                int initialRegionsGenerated = previousRegionsGenerated;
                int completionFrames = 0;
                double worstCompletionFrameMs = 0.0;
                int crossedRegionBoundaries = 0;
                int previousRegionX = Mathf.FloorToInt(origin.x / ShowcaseWorld.RegionMetres);

                for (int frame = 0; frame < TraversalFrames; frame++)
                {
                    float progress = frame / (TraversalFrames - 1f);
                    Vector3 position = origin + new Vector3(
                        frame * StepMetres,
                        0f,
                        Mathf.Sin(progress * Mathf.PI * 6f) * 18f);
                    showcase.transform.position = position;
                    showcase.transform.rotation = originRotation;

                    int regionX = Mathf.FloorToInt(position.x / ShowcaseWorld.RegionMetres);
                    if (regionX != previousRegionX)
                    {
                        crossedRegionBoundaries += Mathf.Abs(regionX - previousRegionX);
                        previousRegionX = regionX;
                    }

                    yield return null;
                    camera.Render();

                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    Assert.AreEqual(0ul, metrics.FramePathBlockingCompletionViolations,
                        $"Region-commit traversal frame {frame} synchronously completed renderer work.");
                    Assert.Greater(metrics.VisibleSolidChunks, 0,
                        $"Region-commit traversal frame {frame} lost every visible solid chunk.");

                    if (NearCoverageIsIncomplete(in metrics))
                    {
                        Assert.LessOrEqual(far.HoleRadiusMetres, 0.05f,
                            $"Region-commit traversal frame {frame} had incomplete near coverage but "
                          + $"opened a {far.HoleRadiusMetres:F2} m far-field hole.");
                    }

                    int regionsGenerated = world.RegionsGenerated;
                    if (regionsGenerated <= previousRegionsGenerated)
                        continue;

                    completionFrames++;
                    worstCompletionFrameMs = System.Math.Max(
                        worstCompletionFrameMs, world.LastGenerateMs);
                    Assert.Less(world.LastGenerateMs, MaxRegionCompletionFrameMs,
                        $"Streaming committed {regionsGenerated - previousRegionsGenerated} region(s) "
                      + $"on traversal frame {frame} but StepStreaming consumed "
                      + $"{world.LastGenerateMs:F3} ms. Region completion must remain inside the "
                      + $"{MaxRegionCompletionFrameMs:F0} ms player-frame budget instead of paying "
                      + "a whole-region occupancy-summary catch-up.");
                    previousRegionsGenerated = regionsGenerated;
                }

                Assert.GreaterOrEqual(crossedRegionBoundaries, 4,
                    "Regression traversal did not cross enough production region boundaries.");
                Assert.Greater(world.RegionsGenerated, initialRegionsGenerated,
                    "Regression traversal did not stream and commit any new production regions.");
                Assert.Greater(completionFrames, 0,
                    "No region-completion frame was observed during the production traversal.");

                Debug.Log(
                    $"### SHOWCASE_REGION_COMMIT_BUDGET completions={completionFrames} "
                  + $"generatedDelta={world.RegionsGenerated - initialRegionsGenerated} "
                  + $"crossedRegions={crossedRegionBoundaries} "
                  + $"worstStepStreaming={worstCompletionFrameMs:F3}ms");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static IEnumerator WaitForFallbackSafeVisibleCoverage(
            Camera camera, VoxelFarTerrain far, int maxFrames)
        {
            int stableFrames = 0;
            VoxelSurfaceMetrics last = default;
            for (int frame = 0; frame < maxFrames; frame++)
            {
                yield return null;
                camera.Render();
                last = VoxelRenderBridge.SurfaceMetrics;
                Assert.AreEqual(0ul, last.FramePathBlockingCompletionViolations,
                    "Geometry work blocked while preparing the region-commit regression.");

                bool nearIncomplete = NearCoverageIsIncomplete(in last);
                bool fallbackSafe = !nearIncomplete || far.HoleRadiusMetres <= 0.05f;
                stableFrames = last.VisibleSolidChunks > 0 && fallbackSafe
                    ? stableFrames + 1 : 0;
                if (stableFrames >= 4)
                    yield break;
            }

            Assert.Fail(
                $"Showcase never reached four fallback-safe visible frames; "
              + $"known={last.SolidKnownChunks} resident={last.SolidResidentChunks} "
              + $"dirty={last.SolidDirtyChunks} visible={last.VisibleSolidChunks} "
              + $"missing={last.MissingVisibleSolidChunks} jobs={last.RunningSolidJobs} "
              + $"farHole={far.HoleRadiusMetres:F2}m.");
        }

        private static bool NearCoverageIsIncomplete(in VoxelSurfaceMetrics metrics) =>
            metrics.MissingVisibleSolidChunks > 0
            || metrics.SolidDirtyChunks > 0
            || metrics.RunningSolidJobs > 0
            || metrics.SolidMeshesAwaitingUpload > 0
            || metrics.SolidPendingUploadBytes > 0;

        private static ShowcaseWorld GetWorld(VoxelShowcase showcase)
        {
            FieldInfo field = typeof(VoxelShowcase).GetField(
                "_world", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, "VoxelShowcase._world was not found.");
            return field.GetValue(showcase) as ShowcaseWorld;
        }

        private static void SetShowcaseField<T>(VoxelShowcase showcase, string fieldName, T value)
        {
            FieldInfo field = typeof(VoxelShowcase).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"VoxelShowcase.{fieldName} was not found.");
            field.SetValue(showcase, value);
        }
    }
}
