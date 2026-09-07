using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelEngine.Composition;
using Debug = UnityEngine.Debug;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Bounded standalone-capture orchestration over the real browser. No alternate content,
    /// rendering, or cleanup path. Snapshot allocations occur only at three cycle boundaries.
    /// </summary>
    public static class PropShowcaseCaptureStress
    {
        public static IEnumerator Run(PropShowcase browser, GameObject presentationRoot, Action completed)
        {
            if (browser == null || presentationRoot == null || completed == null)
                throw new ArgumentNullException("PropShowcase stress requires its live browser, root and completion callback.");

            // Repeat the same bounded set, ending on the same entry, so unlike different-prop
            // before/after measurements these samples can reveal accumulation after warm-up.
            int count = browser.EntryCount;
            if (count == 0)
            {
                Debug.LogError("PROP_SHOWCASE_VALIDATION failure: stress catalogue is empty");
                yield break;
            }
            int stride = Mathf.Max(1, count / 31);
            int samples = 0;
            double totalSwitchMs = 0;
            double maximumSwitchMs = 0;
            double started = Time.realtimeSinceStartupAsDouble;
            PropShowcaseResourceSnapshot baseline = default;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                for (int index = 0; index < count; index += stride)
                {
                    long before = Stopwatch.GetTimestamp();
                    bool selected = browser.Select(index);
                    double switchMs = (Stopwatch.GetTimestamp() - before) * 1000.0 / Stopwatch.Frequency;
                    samples++;
                    totalSwitchMs += switchMs;
                    maximumSwitchMs = Math.Max(maximumSwitchMs, switchMs);
                    if (!selected)
                    {
                        Debug.LogError($"PROP_SHOWCASE_VALIDATION failure: stress selection index={index}");
                        yield break;
                    }
                    // A presenter dictionary can be empty before Destroy retires its objects.
                    // Give actual cleanup and the production renderer separate frames to execute.
                    yield return null;
                    yield return null;
                    if (browser.OwnedPresentationCount > 1)
                    {
                        Debug.LogError("PROP_SHOWCASE_VALIDATION failure: stale-owned-presenters during stress");
                        yield break;
                    }
                }
                if (!browser.Select(count - 1))
                {
                    Debug.LogError("PROP_SHOWCASE_VALIDATION failure: stress endpoint selection");
                    yield break;
                }
                yield return null;
                yield return null;
                double settleStarted = Time.realtimeSinceStartupAsDouble;
                while (Time.realtimeSinceStartupAsDouble - settleStarted < 0.25 || !SurfaceSettled())
                {
                    if (Time.realtimeSinceStartupAsDouble - settleStarted > 3)
                    {
                        Debug.LogError("PROP_SHOWCASE_VALIDATION failure: stress endpoint did not settle");
                        yield break;
                    }
                    yield return null;
                }

                PropShowcaseResourceSnapshot current = PropShowcaseResourceSnapshot.Capture(presentationRoot);
                if (cycle == 0) baseline = current;
                else if (!current.HasSameOwnedObjects(in baseline))
                {
                    Debug.LogError($"PROP_SHOWCASE_VALIDATION failure: owned-object accumulation cycle={cycle}");
                    yield break;
                }
                if (presentationRoot.transform.parent != null)
                {
                    Debug.LogError("PROP_SHOWCASE_VALIDATION failure: presentation root is not independent of camera");
                    yield break;
                }
                Debug.Log(
                    $"PROP_SHOWCASE_VALIDATION resources cycle={cycle} selected={browser.SelectedStableId} " +
                    $"unityAllocatedBytes={current.UnityAllocatedBytes} unityReservedBytes={current.UnityReservedBytes} " +
                    $"managedBytes={current.ManagedBytes} residentGeometryBytes={current.ResidentGeometryBytes} " +
                    $"objects={current.Transforms} renderers={current.Renderers} colliders={current.Colliders} " +
                    $"lights={current.Lights} particles={current.Particles} globalMeshes={current.GlobalMeshes} " +
                    $"globalMaterials={current.GlobalMaterials} unityAllocatedDeltaBytes={current.UnityAllocatedBytes - baseline.UnityAllocatedBytes} " +
                    $"memoryAvailable={current.MemoryAvailable}");
            }
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "PROP_SHOWCASE_VALIDATION stress switches={0} owned={1} cycles=3 timedSelections={2} " +
                "meanSwitchMs={3:0.000} maxSwitchMs={4:0.000} elapsedSeconds={5:0.000}",
                browser.SwitchCount, browser.OwnedPresentationCount, samples, totalSwitchMs / samples,
                maximumSwitchMs, Time.realtimeSinceStartupAsDouble - started));
            // These short process-wide measurements are not the device-matrix two-hour,
            // world-attributable +/-2% memory gate, nor a GPU frame-time measurement.
            completed();
        }

        private static bool SurfaceSettled()
        {
            if (!RenderingComposition.TryGetWorld(out _, out _)) return true;
            RenderingComposition.GetVoxelSurfaceCounts(out int visible, out int missing);
            return RenderingComposition.TryGetSurfaceBuildStatus(out _, out int dirty, out int resident, out _)
                && dirty == 0 && resident > 0 && visible > 0 && missing == 0;
        }
    }

    /// <summary>Observations only: owned objects, global Unity resources and separate allocator domains.</summary>
    public readonly struct PropShowcaseResourceSnapshot
    {
        public readonly long UnityAllocatedBytes, UnityReservedBytes, ManagedBytes, ResidentGeometryBytes;
        public readonly int Transforms, Renderers, Colliders, Lights, Particles, GlobalMeshes, GlobalMaterials;
        public bool MemoryAvailable => UnityAllocatedBytes > 0 && UnityReservedBytes > 0;

        private PropShowcaseResourceSnapshot(GameObject root)
        {
            // Read allocator totals before allocating the bounded diagnostic component arrays.
            UnityAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong();
            UnityReservedBytes = Profiler.GetTotalReservedMemoryLong();
            ManagedBytes = GC.GetTotalMemory(false);
            RenderingComposition.TryGetSurfaceBuildStatus(out _, out _, out _, out long geometryBytes);
            ResidentGeometryBytes = geometryBytes;
            Transforms = root.GetComponentsInChildren<Transform>(true).Length;
            Renderers = root.GetComponentsInChildren<Renderer>(true).Length;
            Colliders = root.GetComponentsInChildren<Collider>(true).Length;
            Lights = root.GetComponentsInChildren<Light>(true).Length;
            Particles = root.GetComponentsInChildren<ParticleSystem>(true).Length;
            GlobalMeshes = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            GlobalMaterials = Resources.FindObjectsOfTypeAll<Material>().Length;
        }

        public static PropShowcaseResourceSnapshot Capture(GameObject root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            return new PropShowcaseResourceSnapshot(root);
        }

        public bool HasSameOwnedObjects(in PropShowcaseResourceSnapshot other) =>
            Transforms == other.Transforms && Renderers == other.Renderers &&
            Colliders == other.Colliders && Lights == other.Lights && Particles == other.Particles;
    }
}
