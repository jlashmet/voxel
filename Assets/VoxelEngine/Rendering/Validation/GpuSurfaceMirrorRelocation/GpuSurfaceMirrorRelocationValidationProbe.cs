using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Validation
{
    /// <summary>
    /// Rendering-owned built-player acceptance probe for the shared GPU surface mirror. It uses
    /// the production voxel showcase already serialized into the scene, primes real GPU work with
    /// bounded movement, then performs the same distant relocation as the focused regression.
    /// Success requires useful GPU completion after recovery; merely keeping the player process
    /// alive is not sufficient evidence.
    /// </summary>
    [AddComponentMenu("VoxelEngine/Validation/GPU Surface Mirror Relocation Probe")]
    [DisallowMultipleComponent]
    public sealed class GpuSurfaceMirrorRelocationValidationProbe : MonoBehaviour
    {
        private const float PrimeTravelMetres = 32f;
        private const float PrimeStepMetres = 0.5f;
        private const float RelocationMetres = 384f;
        private const float CoverageWarmupSeconds = 30f;
        private const float GpuPrimingSeconds = 45f;
        private const float ObservationSeconds = 60f;
        private const float MaximumSaturatedAdmissionSeconds = 20f;

        private Camera _camera;
        private RenderTexture _target;
        private string _status = "warming production surface coverage";
        private string _metrics = string.Empty;

        private IEnumerator Start()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                Fail("main camera is missing");
                yield break;
            }

            if (CpuTransvoxelChunkCache.GpuCutoverDisabled)
            {
                Fail("production near-ring GPU cutover is disabled");
                yield break;
            }

            _target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32)
            {
                name = "GpuSurfaceMirrorRelocationValidation.Relocation",
                antiAliasing = 1
            };
            _target.Create();
            _camera.targetTexture = _target;

            VoxelSurfaceMetrics surface = default;
            var coverage = Stopwatch.StartNew();
            while (coverage.Elapsed.TotalSeconds < CoverageWarmupSeconds)
            {
                yield return null;
                _camera.Render();
                surface = VoxelRenderBridge.SurfaceMetrics;
                PublishMetrics(surface);
                if (surface.VisibleSolidChunks > 0)
                    break;
            }

            if (surface.VisibleSolidChunks <= 0)
            {
                Fail($"initial visible coverage never converged; missing={surface.MissingVisibleSolidChunks}, jobs={surface.RunningSolidJobs}");
                yield break;
            }
            if (!surface.GpuCutoverAvailable)
            {
                Fail("production workers do not advertise the near-ring GPU cutover");
                yield break;
            }

            Transform traveller = _camera.transform;
            Vector3 primeOrigin = traveller.position;
            Vector3 primed = primeOrigin;
            _status = "priming real GPU extraction with bounded movement";
            var priming = Stopwatch.StartNew();
            while (priming.Elapsed.TotalSeconds < GpuPrimingSeconds)
            {
                if (primed.x - primeOrigin.x < PrimeTravelMetres)
                {
                    primed.x += Mathf.Min(PrimeStepMetres, PrimeTravelMetres - (primed.x - primeOrigin.x));
                    traveller.position = primed;
                }

                yield return null;
                _camera.Render();
                surface = VoxelRenderBridge.SurfaceMetrics;
                PublishMetrics(surface);
                if (GpuSurfaceMirrorCoordinator.ReadyBlockCount > 0
                    && surface.GpuCompletedSolidBuilds > 0)
                    break;
            }

            if (GpuSurfaceMirrorCoordinator.ReadyBlockCount <= 0 || surface.GpuCompletedSolidBuilds <= 0)
            {
                Fail($"GPU mirror never primed; ready={GpuSurfaceMirrorCoordinator.ReadyBlockCount}, completed={surface.GpuCompletedSolidBuilds}, fallback={surface.GpuFallbackSolidBuilds}");
                yield break;
            }

            ulong baselineCompleted = surface.GpuCompletedSolidBuilds;
            Vector3 relocated = traveller.position;
            relocated.x += RelocationMetres;
            traveller.position = relocated;
            _status = $"recovering after {RelocationMetres:F0} m relocation";

            bool sawRecoveryBacklog = false;
            bool sawSaturatedAdmission = false;
            double saturatedAdmissionStarted = -1.0;
            var observation = Stopwatch.StartNew();
            while (observation.Elapsed.TotalSeconds < ObservationSeconds)
            {
                yield return null;
                _camera.Render();
                surface = VoxelRenderBridge.SurfaceMetrics;
                PublishMetrics(surface);

                int pending = GpuSurfaceMirrorCoordinator.PendingBlockCount;
                int demand = GpuSurfaceMirrorCoordinator.DemandFootprintCount;
                int active = GpuSurfaceMirrorCoordinator.ActiveExtractions;
                bool recoveryBacklog = pending > 0;
                bool saturatedAdmission = recoveryBacklog
                    && demand >= GpuSurfaceMirrorCoordinator.MaxConcurrentExtractionChains
                    && active == 0;

                sawRecoveryBacklog |= recoveryBacklog;
                sawSaturatedAdmission |= saturatedAdmission;
                if (saturatedAdmission)
                {
                    if (saturatedAdmissionStarted < 0.0)
                        saturatedAdmissionStarted = observation.Elapsed.TotalSeconds;
                    double stalledSeconds = observation.Elapsed.TotalSeconds - saturatedAdmissionStarted;
                    if (stalledSeconds >= MaximumSaturatedAdmissionSeconds)
                    {
                        Fail($"all GPU workers remained mirror-admission pending for {stalledSeconds:F1}s; ready={GpuSurfaceMirrorCoordinator.ReadyBlockCount}, pending={pending}, demand={demand}, active={active}");
                        yield break;
                    }
                }
                else
                {
                    saturatedAdmissionStarted = -1.0;
                }

                if (sawRecoveryBacklog
                    && surface.GpuCompletedSolidBuilds >= baselineCompleted + 4
                    && surface.VisibleSolidChunks > 0
                    && demand < GpuSurfaceMirrorCoordinator.MaxConcurrentExtractionChains)
                    break;
            }

            ulong recoveredBuilds = surface.GpuCompletedSolidBuilds - baselineCompleted;
            if (!sawRecoveryBacklog)
            {
                Fail("distant relocation never exercised shared-mirror recovery");
                yield break;
            }
            if (recoveredBuilds < 4)
            {
                Fail($"GPU extraction did not recover useful throughput; completed={recoveredBuilds}, ready={GpuSurfaceMirrorCoordinator.ReadyBlockCount}, pending={GpuSurfaceMirrorCoordinator.PendingBlockCount}, demand={GpuSurfaceMirrorCoordinator.DemandFootprintCount}, active={GpuSurfaceMirrorCoordinator.ActiveExtractions}");
                yield break;
            }

            long managed = Profiler.GetTotalAllocatedMemoryLong();
            long reserved = Profiler.GetTotalReservedMemoryLong();
            Debug.Log(
                "GPU_SURFACE_MIRROR_RELOCATION_COST " +
                $"prime_seconds={priming.Elapsed.TotalSeconds:F2} recovery_seconds={observation.Elapsed.TotalSeconds:F2} " +
                $"completed={recoveredBuilds} ready={GpuSurfaceMirrorCoordinator.ReadyBlockCount} " +
                $"pending={GpuSurfaceMirrorCoordinator.PendingBlockCount} demand={GpuSurfaceMirrorCoordinator.DemandFootprintCount} " +
                $"active={GpuSurfaceMirrorCoordinator.ActiveExtractions} mixed_resident={GpuSurfaceMirrorCoordinator.ResidentMixedBrickCount} " +
                $"mirror_slots={GpuSurfaceMirrorCoordinator.MirrorSlotCapacity} managed_bytes={managed} reserved_bytes={reserved}");
            Debug.Log(
                "GPU_SURFACE_MIRROR_RELOCATION_VALIDATION ready: " +
                $"relocation_metres={RelocationMetres:F0} recovered_gpu_builds={recoveredBuilds} " +
                $"visible={surface.VisibleSolidChunks} saturated_admission_observed={sawSaturatedAdmission}");
            _status = "ready — recovery resumed useful GPU extraction";
        }

        private void PublishMetrics(VoxelSurfaceMetrics surface)
        {
            _metrics =
                $"visible {surface.VisibleSolidChunks}  missing {surface.MissingVisibleSolidChunks}  GPU completed {surface.GpuCompletedSolidBuilds}\n" +
                $"mirror ready {GpuSurfaceMirrorCoordinator.ReadyBlockCount}  pending {GpuSurfaceMirrorCoordinator.PendingBlockCount}  " +
                $"demand {GpuSurfaceMirrorCoordinator.DemandFootprintCount}  active {GpuSurfaceMirrorCoordinator.ActiveExtractions}";
        }

        private void Fail(string reason)
        {
            _status = "FAILED — " + reason;
            Debug.LogError("GPU_SURFACE_MIRROR_RELOCATION_VALIDATION FAILED: " + reason);
        }

        private void OnDestroy()
        {
            if (_camera != null && _camera.targetTexture == _target)
                _camera.targetTexture = null;
            if (_target == null) return;
            _target.Release();
            Destroy(_target);
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(18, 18, 660, 104), "GPU Surface Mirror · Distant Relocation");
            GUI.Label(new Rect(32, 48, 630, 24), _status);
            GUI.Label(new Rect(32, 72, 630, 44), _metrics);
        }
    }
}
