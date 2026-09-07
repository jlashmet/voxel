using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using VoxelEngine.Rendering.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Opt-in draw-owner experiment, not a production visibility policy. A replay may compare
    /// normal (25 s), far-feature-suppressed (35 s), and restored (45 s) captures without
    /// changing voxel state, geometry, material values, terrain, or extraction configuration.
    /// </summary>
    public static class SceneIssueFarFeatureReplayHarness
    {
        internal const string ProbeName = "far-feature-visibility";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            string issuePath = null;
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-voxel-scene-issue") issuePath = args[i + 1];
            if (string.IsNullOrEmpty(issuePath) || !File.Exists(issuePath)) return;

            try
            {
                if (!IsRequested(File.ReadAllText(issuePath))) return;
                var root = new GameObject("Scene Issue Far Feature Owner Probe")
                {
                    hideFlags = HideFlags.DontSave
                };
                root.AddComponent<Replay>();
                Debug.Log("SCENEISSUE FAR FEATURE PROBE armed; diagnostic-only, not visual acceptance.");
            }
            catch (Exception error)
            {
                Debug.LogError($"SCENEISSUE FAR FEATURE PROBE FAIL: {error.Message}");
            }
        }

        internal static bool IsRequested(string issueJson) =>
            !string.IsNullOrWhiteSpace(issueJson)
            && string.Equals(JsonUtility.FromJson<ProbeRecord>(issueJson)?.renderProbe,
                             ProbeName, StringComparison.Ordinal);

        [Serializable]
        private sealed class ProbeRecord
        {
            public string renderProbe;
        }

        [DefaultExecutionOrder(9000)]
        private sealed class Replay : MonoBehaviour
        {
            private FarFeatureVisibilityProbe _probe;
            private Camera _camera;
            private Vector3 _position;
            private Quaternion _rotation;
            private float _fieldOfView;
            private float _elapsed;
            private float _nextScan;
            private int _phase = -1;
            private bool _havePose;

            private void Update()
            {
                // Same clock as ShowcasePlayerHarness, including the initial loading frame.
                _elapsed += Time.unscaledDeltaTime;
                if (_probe == null)
                {
                    if (_elapsed >= 25f)
                    {
                        Fail("No populated far-feature renderer/camera before the normal capture.");
                        return;
                    }
                    if (_elapsed < _nextScan) return;
                    _nextScan = _elapsed + 0.5f;
                    _camera = Camera.main;
                    if (_camera == null) return;
                    var selected = new List<IFarFeatureRenderer>();
                    foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (behaviour == null || behaviour.gameObject.scene != gameObject.scene
                            || !(behaviour is IFarFeatureRenderer renderer)
                            || renderer.InstanceCount == 0) continue;
                        selected.Add(renderer);
                        if (selected.Count > FarFeatureVisibilityProbe.MaximumRenderers)
                        {
                            Fail("Far-feature renderer count exceeds the bounded probe capacity.");
                            return;
                        }
                    }
                    if (selected.Count == 0) return;
                    _probe = new FarFeatureVisibilityProbe(selected.ToArray());
                }

                int phase = FarFeatureVisibilityProbe.PhaseAt(_elapsed);
                if (phase != _phase)
                {
                    _phase = phase;
                    _probe.Apply(phase == 1);
                    string name = phase == 0 ? "normal" : phase == 1 ? "suppressed" : "restored";
                    Debug.Log($"SCENEISSUE FAR FEATURE PROBE phase={name} t={_elapsed:0.0}s " +
                              $"renderers={_probe.Count} instances={_probe.InstanceCount} diagnostic-only");
                }
                if (_elapsed >= 50f)
                {
                    _probe.Dispose();
                    Debug.Log("SCENEISSUE FAR FEATURE PROBE complete; original visibility restored.");
                    Destroy(gameObject);
                }
            }

            private void LateUpdate()
            {
                if (_probe == null || _camera == null) return;
                if (!_havePose)
                {
                    _position = _camera.transform.position;
                    _rotation = _camera.transform.rotation;
                    _fieldOfView = _camera.fieldOfView;
                    _havePose = true;
                    Debug.Log($"SCENEISSUE FAR FEATURE PROBE camera={_position} " +
                              $"rotation={_rotation} fov={_fieldOfView:0.###}");
                }
                // Preserve the actual authored view; do not encode captured coordinates.
                _camera.transform.SetPositionAndRotation(_position, _rotation);
                _camera.fieldOfView = _fieldOfView;
            }

            private void Fail(string message)
            {
                _probe?.Dispose();
                Debug.LogError("SCENEISSUE FAR FEATURE PROBE FAIL: " + message);
                enabled = false;
                Destroy(gameObject);
            }

            private void OnDisable() => _probe?.Dispose();
            private void OnDestroy() => _probe?.Dispose();
        }
    }

    /// <summary>Bounded, reversible instrumentation of the existing render enable contract.</summary>
    internal sealed class FarFeatureVisibilityProbe : IDisposable
    {
        internal const int MaximumRenderers = 8;
        private readonly IFarFeatureRenderer[] _renderers;
        private readonly bool[] _original;
        private bool _disposed;

        internal FarFeatureVisibilityProbe(IFarFeatureRenderer[] renderers)
        {
            if (renderers == null) throw new ArgumentNullException(nameof(renderers));
            if (renderers.Length == 0 || renderers.Length > MaximumRenderers)
                throw new ArgumentOutOfRangeException(nameof(renderers));
            _renderers = (IFarFeatureRenderer[])renderers.Clone();
            _original = new bool[renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (!IsAlive(_renderers[i])) throw new ArgumentException("Probe requires live renderers.");
                _original[i] = _renderers[i].enabled;
            }
        }

        internal int Count => _renderers.Length;
        internal int InstanceCount
        {
            get
            {
                int count = 0;
                foreach (IFarFeatureRenderer renderer in _renderers)
                    if (IsAlive(renderer)) count += renderer.InstanceCount;
                return count;
            }
        }

        internal static int PhaseAt(double elapsed) => elapsed < 30d ? 0 : elapsed < 40d ? 1 : 2;

        internal void Apply(bool suppress)
        {
            if (_disposed) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (IsAlive(_renderers[i])) _renderers[i].enabled = !suppress && _original[i];
        }

        public void Dispose()
        {
            if (_disposed) return;
            Apply(false);
            _disposed = true;
        }

        private static bool IsAlive(IFarFeatureRenderer renderer) =>
            renderer != null && (!(renderer is UnityEngine.Object value) || value != null);
    }
}
