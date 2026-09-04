using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using VoxelEngine.Rendering.Runtime.FarWorld;

namespace VoxelEngine.Rendering.Validation
{
    /// <summary>
    /// Validation-only built-player probe for the FarWorld module scene. It observes frame timing,
    /// memory and renderer batching after warmup; none of these observations feed rendering or
    /// world state. The probe is installed only in FarWorldVisibilityDemo.
    /// </summary>
    internal sealed class FarWorldBudgetProbe : MonoBehaviour
    {
        private const float WarmupSeconds = 4f;
        private const float ReportSeconds = 26f;
        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private double _cpuTotalMs;
        private double _gpuTotalMs;
        private double _frameTotalMs;
        private double _cpuMaxMs;
        private double _gpuMaxMs;
        private double _frameMaxMs;
        private int _cpuSamples;
        private int _gpuSamples;
        private int _frameSamples;
        private bool _reported;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (SceneManager.GetActiveScene().name != "FarWorldVisibilityDemo") return;
            var probe = new GameObject("FarWorld Validation Budget Probe")
            {
                hideFlags = HideFlags.DontSave,
            };
            probe.AddComponent<FarWorldBudgetProbe>();
        }

        private void Update()
        {
            FrameTimingManager.CaptureFrameTimings();
            if (Time.realtimeSinceStartup < WarmupSeconds) return;

            double frame = Time.unscaledDeltaTime * 1000.0;
            if (frame > 0.0)
            {
                _frameTotalMs += frame;
                _frameMaxMs = System.Math.Max(_frameMaxMs, frame);
                _frameSamples++;
            }

            uint count = FrameTimingManager.GetLatestTimings(1, _timings);
            if (count > 0)
            {
                double cpu = _timings[0].cpuFrameTime;
                if (cpu > 0.0)
                {
                    _cpuTotalMs += cpu;
                    _cpuMaxMs = System.Math.Max(_cpuMaxMs, cpu);
                    _cpuSamples++;
                }

                double gpu = _timings[0].gpuFrameTime;
                if (gpu > 0.0)
                {
                    _gpuTotalMs += gpu;
                    _gpuMaxMs = System.Math.Max(_gpuMaxMs, gpu);
                    _gpuSamples++;
                }
            }

            if (_reported || Time.realtimeSinceStartup < ReportSeconds) return;
            _reported = true;
            Report();
        }

        private void Report()
        {
            ProceduralFarFeatureRenderer renderer = FindValidationRenderer();
            int batches = PrivateCollectionCount(renderer, "_batches");
            int meshes = PrivateCollectionCount(renderer, "_meshCache");
            int materials = PrivateCollectionCount(renderer, "_materialCache");
            int instances = renderer != null ? renderer.InstanceCount : 0;

            long allocated = Profiler.GetTotalAllocatedMemoryLong();
            long reserved = Profiler.GetTotalReservedMemoryLong();
            long graphics = Profiler.GetAllocatedMemoryForGraphicsDriver();
            double cpuAverage = _cpuSamples > 0 ? _cpuTotalMs / _cpuSamples : 0.0;
            double gpuAverage = _gpuSamples > 0 ? _gpuTotalMs / _gpuSamples : 0.0;
            double frameAverage = _frameSamples > 0 ? _frameTotalMs / _frameSamples : 0.0;

            Debug.Log(
                "FARWORLD_BUDGET "
                + $"frameAvgMs={frameAverage:F3} frameMaxMs={_frameMaxMs:F3} frameSamples={_frameSamples} "
                + $"cpuAvgMs={cpuAverage:F3} cpuMaxMs={_cpuMaxMs:F3} cpuSamples={_cpuSamples} "
                + $"gpuAvgMs={gpuAverage:F3} gpuMaxMs={_gpuMaxMs:F3} gpuSamples={_gpuSamples} "
                + $"allocatedBytes={allocated} reservedBytes={reserved} graphicsDriverBytes={graphics} "
                + $"rendererFound={(renderer != null ? 1 : 0)} instances={instances} batches={batches} "
                + $"cachedMeshes={meshes} cachedMaterials={materials}.");
        }

        private static ProceduralFarFeatureRenderer FindValidationRenderer()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            ProceduralFarFeatureRenderer[] renderers =
                Resources.FindObjectsOfTypeAll<ProceduralFarFeatureRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                ProceduralFarFeatureRenderer renderer = renderers[i];
                if (renderer == null || renderer.gameObject.scene != activeScene) continue;
                return renderer;
            }
            return null;
        }

        private static int PrivateCollectionCount(object target, string fieldName)
        {
            if (target == null) return 0;
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(target) is ICollection collection ? collection.Count : 0;
        }
    }
}
