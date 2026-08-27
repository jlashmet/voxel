using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Small performance diagnostic for the showcase. Shows current FPS plus rolling min/max
    /// without bringing back the old large diagnostics HUD.
    /// </summary>
    public sealed class CompactFpsOverlay : MonoBehaviour
    {
        private const float SampleSeconds = 0.25f;
        private const float StatsWindowSeconds = 5f;
        private const float Margin = 6f;
        private const float Width = 510f;
        private const float Height = 40f;

        private float _elapsed;
        private int _frames;
        private float _windowElapsed;
        private float _minFps = float.PositiveInfinity;
        private float _maxFps;
        private string _label = "FPS --   MIN --   MAX --";
        private GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForShowcase()
        {
            // SceneIssue evidence must be a clean saved-pose render rather than a diagnostics HUD.
            // Normal showcase players keep the overlay; only the explicit replay path suppresses it.
            if (HasFlag("-voxel-scene-issue")) return;

            // Keyed on the showcase component rather than one hard-coded scene name. The name
            // check silently skipped SmallVoxelShowcase, which is the same showcase with less
            // world in it and is precisely where a frame-rate readout is wanted.
            if (Object.FindFirstObjectByType<VoxelShowcase>() == null) return;
            if (Object.FindFirstObjectByType<CompactFpsOverlay>() != null) return;

            var root = new GameObject("Compact FPS Overlay")
            {
                hideFlags = HideFlags.DontSave
            };
            root.AddComponent<CompactFpsOverlay>();
        }

        private static bool HasFlag(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], name, System.StringComparison.Ordinal)) return true;
            return false;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _elapsed += dt;
            _windowElapsed += dt;
            _frames++;
            if (_elapsed < SampleSeconds) return;

            float fps = _elapsed > 0f ? _frames / _elapsed : 0f;

            if (_windowElapsed >= StatsWindowSeconds)
            {
                _windowElapsed = 0f;
                _minFps = fps;
                _maxFps = fps;
            }
            else
            {
                _minFps = Mathf.Min(_minFps, fps);
                _maxFps = Mathf.Max(_maxFps, fps);
            }

            _label = $"FPS {fps:0}   MIN {_minFps:0}   MAX {_maxFps:0}";
            SurfaceTimingDiagnostics metrics = VoxelEngineBootstrap.GetSurfaceTimingDiagnostics();
            _label += $"\nSURFACE p95  frame {metrics.FrameP95Ms:0.0}  "
                    + $"discover {metrics.DiscoveryP95Ms:0.0}  "
                    + $"snapshot {metrics.SnapshotP95Ms:0.0}  "
                    + $"compact {metrics.TopologyCompactP95Ms:0.0}  "
                    + $"merge {metrics.FacetedMergeP95Ms:0.0}  "
                    + $"upload {metrics.UploadP95Ms:0.0} ms  "
                    + $"queue {metrics.QueueLatencyP95Ms:0} ms";
            _elapsed = 0f;
            _frames = 0;
        }

        private void OnGUI()
        {
            _style ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(4, 4, 2, 2)
            };

            GUI.Box(new Rect(Margin, Margin, Width, Height), _label, _style);
        }
    }
}
