using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ShowcaseAllocationAndThrashTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const float VoxelSize = 0.1f;
        private const double MaxP95FrameMs = 18.0;
        private const double MaxP99FrameMs = 25.0;
        private const double MaxSingleFrameMs = 33.34;

        [UnityTest, Timeout(900000)]
        public IEnumerator WarmContinuousTraversalAllocatesZeroManagedBytesInSurfacePrepare()
        {
            yield return LoadReadyShowcase();

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Camera camera = Camera.main;
            Assert.NotNull(showcase);
            Assert.NotNull(camera);
            SetShowcaseField(showcase, "m_FlyMode", true);
            SetShowcaseField(showcase, "_mouseLook", false);

            Vector3 origin = showcase.transform.position;
            long largestWarmupAllocation = 0;
            for (int frame = 0; frame < 160; frame++)
            {
                showcase.transform.position = origin + new Vector3(
                    frame * 0.5f, 0f, Mathf.Sin(frame * 0.09f) * 14f);
                yield return null;
                camera.Render();
                VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                largestWarmupAllocation = System.Math.Max(
                    largestWarmupAllocation, metrics.LastFrameManagedAllocationBytes);
                Assert.AreEqual(0ul, metrics.FramePathBlockingCompletionViolations,
                    $"Warm traversal frame {frame} blocked on geometry completion.");
            }

            Assert.LessOrEqual(largestWarmupAllocation, 64 * 1024,
                $"Surface preparation allocated {largestWarmupAllocation:N0} managed bytes in one "
              + "warmup movement frame. One-time growth must remain small enough to avoid a GC hitch.");

            Vector3 measuredOrigin = showcase.transform.position;
            for (int frame = 0; frame < 240; frame++)
            {
                showcase.transform.position = measuredOrigin + new Vector3(
                    frame * 0.5f, 0f, Mathf.Sin(frame * 0.11f) * 16f);
                yield return null;
                camera.Render();

                VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                Assert.AreEqual(0L, metrics.LastFrameManagedAllocationBytes,
                    $"Surface scheduler allocated {metrics.LastFrameManagedAllocationBytes:N0} managed "
                  + $"bytes on warmed traversal frame {frame}. Recurring streaming/render preparation "
                  + "must be allocation-free so ordinary movement cannot accumulate GC stutter.");
                Assert.AreEqual(0ul, metrics.FramePathBlockingCompletionViolations,
                    $"Measured traversal frame {frame} blocked on geometry completion.");
                Assert.Greater(metrics.VisibleSolidChunks, 0,
                    $"Measured traversal frame {frame} lost all voxel draws.");
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator ReversingAcrossOneSnapBoundaryNeverThrashesOrStutters()
        {
            yield return LoadReadyShowcase();

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            VoxelFarTerrain far = Object.FindFirstObjectByType<VoxelFarTerrain>();
            Camera camera = Camera.main;
            Assert.NotNull(showcase);
            Assert.NotNull(far);
            Assert.NotNull(camera);
            SetShowcaseField(showcase, "m_FlyMode", true);
            SetShowcaseField(showcase, "_mouseLook", false);

            int spacing = far.SpacingForRing(0);
            float cellMetres = spacing * VoxelSize;
            int resolution = GetField<int>(far, "m_Resolution");
            int2 publishedOrigin = GetField<List<int2>>(far, "_ringOrigin")[0];
            float snappedCentreX = (publishedOrigin.x + spacing * resolution / 2) * VoxelSize;
            float boundaryX = snappedCentreX + cellMetres;
            Vector3 basePosition = showcase.transform.position;
            Vector3 left = new(boundaryX - 0.25f, basePosition.y, basePosition.z);
            Vector3 right = new(boundaryX + 0.25f, basePosition.y, basePosition.z);

            for (int frame = 0; frame < 40; frame++)
            {
                showcase.transform.position = (frame & 1) == 0 ? left : right;
                yield return null;
                camera.Render();
            }

            var frameTimes = new List<double>(180);
            var frameClock = new Stopwatch();
            ulong topologyStart = far.TopologyRebuildCount;
            int lagFrames = 0;

            for (int frame = 0; frame < 180; frame++)
            {
                showcase.transform.position = (frame & 1) == 0 ? left : right;

                frameClock.Restart();
                yield return null;
                camera.Render();
                frameClock.Stop();
                frameTimes.Add(frameClock.Elapsed.TotalMilliseconds);

                VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                Assert.AreEqual(0L, metrics.LastFrameManagedAllocationBytes,
                    $"Snap-boundary reversal allocated {metrics.LastFrameManagedAllocationBytes:N0} "
                  + $"managed bytes on warmed frame {frame}.");
                Assert.AreEqual(0ul, metrics.FramePathBlockingCompletionViolations,
                    $"Snap-boundary reversal frame {frame} blocked on geometry completion.");
                Assert.Greater(metrics.VisibleSolidChunks, 0,
                    $"Snap-boundary reversal frame {frame} lost all voxel draws.");

                int2 targetOrigin = InvokeOriginFor(far, showcase.transform.position, spacing);
                int2 currentOrigin = GetField<List<int2>>(far, "_ringOrigin")[0];
                if (!targetOrigin.Equals(currentOrigin))
                {
                    lagFrames++;
                    float publishedHole = GetField<List<float>>(
                        far, "_ringBuiltTopologyHoleMetres")[0];
                    Assert.LessOrEqual(publishedHole, 0.05f,
                        $"Boundary reversal frame {frame} retained a {publishedHole:F2}m hole "
                      + "while ring 0 lagged its target snap.");
                }
            }

            Assert.Greater(lagFrames, 0,
                "Boundary reversal never exercised a lagging ring-0 publication.");

            ulong topologyDelta = far.TopologyRebuildCount - topologyStart;
            Assert.Less(topologyDelta, 90ul,
                $"Ring-0 topology rebuilt {topologyDelta} times in 180 boundary-reversal frames. "
              + "The fallback is thrashing instead of retaining its full-square topology while stale.");

            AssertFrameTimes(frameTimes, "snap-boundary reversal");
        }

        private static IEnumerator LoadReadyShowcase()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            bool ready = false;
            VoxelSurfaceMetrics last = default;
            for (int frame = 0; frame < 1200; frame++)
            {
                yield return null;
                Camera camera = Camera.main;
                if (camera == null) continue;
                camera.Render();
                VoxelFarTerrain far = Object.FindFirstObjectByType<VoxelFarTerrain>();
                last = VoxelRenderBridge.SurfaceMetrics;
                if (far != null
                    && last.VisibleSolidChunks > 0
                    && last.MissingVisibleSolidChunks == 0
                    && RenderingComposition.HasCompletePublishedNearSurfaceCoverage()
                    && RingZeroPublished(far))
                {
                    ready = true;
                    break;
                }
            }

            Assert.True(ready,
                $"Showcase did not reach a stable rendered starting point; "
              + $"known={last.SolidKnownChunks} resident={last.SolidResidentChunks} "
              + $"dirty={last.SolidDirtyChunks} visible={last.VisibleSolidChunks} "
              + $"missing={last.MissingVisibleSolidChunks} jobs={last.RunningSolidJobs}.");
        }

        private static bool RingZeroPublished(VoxelFarTerrain far)
        {
            List<bool> valid = GetField<List<bool>>(far, "_ringHeightValid");
            List<float> holes = GetField<List<float>>(far, "_ringBuiltTopologyHoleMetres");
            return valid.Count > 0 && valid[0]
                && holes.Count > 0 && !float.IsNaN(holes[0]);
        }

        private static void AssertFrameTimes(List<double> values, string phase)
        {
            values.Sort();
            double p95 = Percentile(values, 0.95);
            double p99 = Percentile(values, 0.99);
            double maximum = values[^1];
            UnityEngine.Debug.Log(
                $"### SHOWCASE_THRASH_PERF phase={phase} frames={values.Count} "
              + $"p95={p95:F2}ms p99={p99:F2}ms max={maximum:F2}ms");

            Assert.Less(p95, MaxP95FrameMs,
                $"{phase} p95 was {p95:F2} ms (p99={p99:F2}, max={maximum:F2}).");
            Assert.Less(p99, MaxP99FrameMs,
                $"{phase} p99 was {p99:F2} ms (max={maximum:F2}).");
            Assert.Less(maximum, MaxSingleFrameMs,
                $"{phase} produced a {maximum:F2} ms player-visible hitch.");
        }

        private static double Percentile(List<double> sorted, double percentile)
        {
            int index = Mathf.Clamp(
                Mathf.CeilToInt((float)(sorted.Count * percentile)) - 1,
                0,
                sorted.Count - 1);
            return sorted[index];
        }

        private static int2 InvokeOriginFor(VoxelFarTerrain far, Vector3 position, int spacing)
        {
            MethodInfo method = typeof(VoxelFarTerrain).GetMethod(
                "OriginFor", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return (int2)method.Invoke(far, new object[] { position, spacing });
        }

        private static T GetField<T>(VoxelFarTerrain far, string fieldName)
        {
            FieldInfo field = typeof(VoxelFarTerrain).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, fieldName);
            return (T)field.GetValue(far);
        }

        private static void SetShowcaseField<T>(VoxelShowcase showcase, string fieldName, T value)
        {
            FieldInfo field = typeof(VoxelShowcase).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, fieldName);
            field.SetValue(showcase, value);
        }
    }
}
