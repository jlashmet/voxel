using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase.Validation
{
    /// <summary>
    /// Built-player proof for the Showcase-owned presentation-readiness observer introduced by the
    /// macro-world work. The existing focused Showcase validation scene remains the visual consumer;
    /// this probe owns a second, isolated production ShowcaseWorld only long enough to prove that a
    /// real streamed presentation column transitions from not resident to content-settled without
    /// mutating the visual scene's world or renderer.
    /// </summary>
    internal static class ShowcaseStreamingReadinessValidationBootstrap
    {
        private const string ScenePath =
            "Assets/Game/Composition/Showcase/Validation/ShowcaseSecretDiscoveryValidation.unity";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!string.Equals(SceneManager.GetActiveScene().path, ScenePath, StringComparison.Ordinal))
                return;

            ShowcaseSecretDiscoveryValidation host =
                UnityEngine.Object.FindFirstObjectByType<ShowcaseSecretDiscoveryValidation>();
            if (host == null)
                throw new InvalidOperationException(
                    "Showcase streaming-readiness validation requires the production Showcase validation host.");

            if (host.GetComponent<ShowcaseStreamingReadinessValidationProbe>() == null)
                host.gameObject.AddComponent<ShowcaseStreamingReadinessValidationProbe>();
        }
    }

    [DisallowMultipleComponent]
    internal sealed class ShowcaseStreamingReadinessValidationProbe : MonoBehaviour
    {
        private const uint Seed = 0x5EED1234u;
        private const double TimeoutSeconds = 15.0;

        // Production ShowcaseCatalogue places its detailed farmhouse at (1540, *, 560) voxels.
        // Probe the presentation column at that authored feature rather than an empty terrain-only
        // column so the real feature queue participates in the readiness transition.
        private static readonly Vector3 TargetMetres = new Vector3(154f, 30f, 56f);

        private ShowcaseWorld _world;
        private double _startedAt;
        private bool _settled;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            _world = new ShowcaseWorld(
                Seed,
                brickPoolCapacity: 65536,
                loadRadiusRegions: 1,
                unloadRadiusRegions: 2);
            _startedAt = Time.realtimeSinceStartupAsDouble;

            if (_world.IsPresentationColumnContentSettled(TargetMetres))
                throw new InvalidOperationException(
                    "Showcase streaming-readiness validation expected a fresh production column to start unsettled.");

            Debug.Log("Showcase streaming readiness validation pending: initial=false target=detailed-farmhouse");
        }

        private void Update()
        {
            if (_world == null || _settled) return;

            _world.StepStreaming(TargetMetres, budgetMs: 6.0);
            if (_world.IsPresentationColumnContentSettled(TargetMetres))
            {
                _settled = true;
                Debug.Log(
                    "Showcase streaming readiness validation settled: " +
                    $"initial=false settled=true regionsGenerated={_world.RegionsGenerated}");
                enabled = false;
                return;
            }

            if (Time.realtimeSinceStartupAsDouble - _startedAt > TimeoutSeconds)
                throw new InvalidOperationException(
                    "Showcase streaming-readiness validation did not settle the production farmhouse column.");
        }

        private void OnDisable() => DisposeWorld();
        private void OnDestroy() => DisposeWorld();

        private void DisposeWorld()
        {
            if (_world == null) return;
            _world.StopBackgroundWork();
            _world.Dispose();
            _world = null;
        }
    }
}
