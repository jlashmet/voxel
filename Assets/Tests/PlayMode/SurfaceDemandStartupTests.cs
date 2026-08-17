using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;
using Object = UnityEngine.Object;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Proves the showcase startup renderer is demand-driven in the real production scene.
    /// Discovered/known chunks may be large, but expensive render work must remain proportional
    /// to the currently active/visible hierarchy rather than the complete discovered surface set.
    /// </summary>
    public sealed class SurfaceDemandStartupTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";

        [UnityTest, Timeout(180000)]
        public IEnumerator StartupRenderWorkTracksVisibleHierarchyInsteadOfKnownWorld()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            double readyDeadline = Time.realtimeSinceStartupAsDouble + 60.0;
            while ((!VoxelRenderBridge.SurfaceBuildEnabled || !VoxelRenderBridge.TryGetWorld(out _))
                   && Time.realtimeSinceStartupAsDouble < readyDeadline)
                yield return null;

            Assert.True(VoxelRenderBridge.SurfaceBuildEnabled,
                "VoxelShowcase never enabled production surface rendering.");
            Assert.True(VoxelRenderBridge.TryGetWorld(out _),
                "VoxelShowcase never bound a renderable world.");

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            typeof(VoxelShowcase).GetField(
                    "m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(showcase, true);

            double previousBuildBudget = VoxelRenderBridge.SolidBuildBudgetMs;
            int previousUploadBudget = VoxelRenderBridge.SolidUploadBudgetBytes;
            int previousUploadSlice = VoxelRenderBridge.SolidUploadSliceBytes;
            int previousUploadWorkers = VoxelRenderBridge.SolidUploadWorkerBudget;
            double previousUploadMs = VoxelRenderBridge.SolidUploadBudgetMs;
            try
            {
                // Accelerate the observation window without changing admission semantics.
                VoxelRenderBridge.SolidBuildBudgetMs = 8.0;
                VoxelRenderBridge.SolidUploadBudgetBytes = 16 * 1024 * 1024;
                VoxelRenderBridge.SolidUploadSliceBytes = 2 * 1024 * 1024;
                VoxelRenderBridge.SolidUploadWorkerBudget = 22;
                VoxelRenderBridge.SolidUploadBudgetMs = 8.0;

                VoxelSurfaceMetrics metrics = default;
                bool observedHierarchy = false;
                double deadline = Time.realtimeSinceStartupAsDouble + 30.0;
                while (Time.realtimeSinceStartupAsDouble < deadline)
                {
                    yield return null;
                    metrics = VoxelRenderBridge.SurfaceMetrics;
                    if (metrics.SolidKnownChunks > 0
                        && metrics.ActiveSolidCoverageNodes > 0
                        && metrics.ColdKnownSolidChunks > 0)
                    {
                        observedHierarchy = true;
                        break;
                    }
                }

                Assert.True(observedHierarchy,
                    "Startup never exposed a populated active/cold hierarchy. " + Summary(metrics));

                int requested = metrics.RequestedSolidP0MissingCoverage
                              + metrics.RequestedSolidP1PreserveCoverage
                              + metrics.RequestedSolidP2VisibleRefinement
                              + metrics.RequestedSolidP3Prefetch;
                int coverageScale = Mathf.Max(1,
                    metrics.ActiveSolidCoverageNodes + metrics.FallbackSolidParentNodes);

                // One active parent can request its eight children, and several adjacent hierarchy
                // levels may overlap transiently while refining. The generous 32x bound keeps the
                // assertion architectural rather than camera-content-specific while still rejecting
                // the old eager known=>dirty=>build behaviour by a wide margin.
                int requestedBound = coverageScale * 32 + 128;
                Assert.LessOrEqual(requested, requestedBound,
                    $"Render requests scaled beyond visible hierarchy coverage: requested={requested}, "
                  + $"bound={requestedBound}. {Summary(metrics)}");

                int dirtyBound = requested + metrics.RunningSolidJobs
                               + metrics.SolidMeshesAwaitingUpload + 32;
                Assert.LessOrEqual(metrics.SolidDirtyChunks, dirtyBound,
                    $"Dirty work exists without corresponding explicit demand: dirty={metrics.SolidDirtyChunks}, "
                  + $"bound={dirtyBound}. {Summary(metrics)}");

                Assert.Greater(metrics.ColdKnownSolidChunks, 0,
                    "The real startup scene must retain discovered cold chunks without eagerly building them.");
                Assert.Greater(metrics.SolidKnownChunks, metrics.ActiveSolidCoverageNodes,
                    "Known render space unexpectedly collapsed to only active coverage; this test needs a "
                  + "larger discovered set to prove demand-driven admission.");
            }
            finally
            {
                VoxelRenderBridge.SolidBuildBudgetMs = previousBuildBudget;
                VoxelRenderBridge.SolidUploadBudgetBytes = previousUploadBudget;
                VoxelRenderBridge.SolidUploadSliceBytes = previousUploadSlice;
                VoxelRenderBridge.SolidUploadWorkerBudget = previousUploadWorkers;
                VoxelRenderBridge.SolidUploadBudgetMs = previousUploadMs;
            }
        }

        private static string Summary(VoxelSurfaceMetrics metrics)
        {
            int requested = metrics.RequestedSolidP0MissingCoverage
                          + metrics.RequestedSolidP1PreserveCoverage
                          + metrics.RequestedSolidP2VisibleRefinement
                          + metrics.RequestedSolidP3Prefetch;
            return $"known={metrics.SolidKnownChunks} active={metrics.ActiveSolidCoverageNodes} "
                 + $"fallback={metrics.FallbackSolidParentNodes} cold={metrics.ColdKnownSolidChunks} "
                 + $"requested={requested} "
                 + $"p0/p1/p2/p3={metrics.RequestedSolidP0MissingCoverage}/"
                 + $"{metrics.RequestedSolidP1PreserveCoverage}/"
                 + $"{metrics.RequestedSolidP2VisibleRefinement}/"
                 + $"{metrics.RequestedSolidP3Prefetch} dirty={metrics.SolidDirtyChunks} "
                 + $"jobs={metrics.RunningSolidJobs} staging={metrics.SolidStagingBytes}B.";
        }
    }
}
