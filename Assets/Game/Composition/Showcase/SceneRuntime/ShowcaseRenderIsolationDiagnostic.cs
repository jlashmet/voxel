using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.FarWorld;

namespace VoxelEngine.Showcase
{
    // Temporary, assignment-scoped evidence instrumentation. Never installed in ordinary play.
    // Changes presentation for labelled diagnostic frames only, never voxels or collision.
    [DefaultExecutionOrder(32000)]
    internal sealed class ShowcaseRenderIsolationDiagnostic : MonoBehaviour
    {
        private const string Assignment = "20260828-180417-000-VoxelShowcaseMountainDragonCutscene";
        private string _directory;
        private ShowcaseWaypointReplayHarness _replay;
        private VoxelShowcase _showcase;
        private ProceduralFarFeatureRenderer[] _far;
        private bool[] _farEnabled;
        private Renderer[] _renderers;
        private bool[] _rendererEnabled;
        private bool _paused, _replayEnabled, _autoWalk, _surfaceBuild;
        private bool _suppressSurface, _suppressComponents;
        private int _originalPink;
        private bool _failed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string issue = Argument("-voxel-scene-issue");
            string directory = Argument("-voxel-screenshot-dir");
            if (string.IsNullOrEmpty(issue) || string.IsNullOrEmpty(directory)) return;
            if (!string.Equals(Path.GetFileName(Path.GetDirectoryName(issue)), Assignment,
                    StringComparison.Ordinal)) return;
            var root = new GameObject("Mountain Dragon Render Isolation (diagnostic only)");
            root.hideFlags = HideFlags.DontSave;
            root.AddComponent<ShowcaseRenderIsolationDiagnostic>()._directory = directory;
        }

        private IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 90f;
            string approach = Path.Combine(_directory, "01-mountain-approach.png");
            while (!File.Exists(approach) && Time.realtimeSinceStartup < deadline)
                yield return null;

            // FindFirstObjectByType does not reliably rediscover DontDestroyOnLoad / DontSave
            // evidence harnesses in a standalone player. Resources.FindObjectsOfTypeAll does;
            // require a live loaded scene so prefab/assets cannot satisfy the prerequisite.
            _replay = FindRuntimeObject<ShowcaseWaypointReplayHarness>();
            _showcase = FindRuntimeObject<VoxelShowcase>();
            bool approachExists = File.Exists(approach);
            if (!approachExists || _replay == null || _showcase == null)
            {
                Debug.LogError(
                    "RENDER_ISOLATION prerequisite missing "
                    + $"approach={approachExists} replay={(_replay != null)} showcase={(_showcase != null)}; "
                    + "no attribution is possible.");
                Application.Quit(26);
                yield break;
            }

            // Flatten the iterator so exceptions are reported and all state is restored.
            IEnumerator experiment = RunExperiment();
            try
            {
                while (true)
                {
                    bool more;
                    object next = null;
                    try
                    {
                        more = experiment.MoveNext();
                        if (more) next = experiment.Current;
                    }
                    catch (Exception error)
                    {
                        _failed = true;
                        Debug.LogError($"RENDER_ISOLATION failed: {error}");
                        break;
                    }
                    if (!more) break;
                    yield return next;
                }
            }
            finally
            {
                (experiment as IDisposable)?.Dispose();
                Restore();
            }
            // The shared capture runner checks process exit, not arbitrary Debug.LogError text.
            // Flush the restored frame before failing; an exclusion frame can never turn CI green.
            if (_failed || _originalPink > 0)
            {
                yield return new WaitForEndOfFrame();
                Application.Quit(26);
            }
        }

        private IEnumerator RunExperiment()
        {
            _replayEnabled = _replay.enabled;
            _autoWalk = _showcase.AutoWalk;
            _surfaceBuild = VoxelRenderBridge.SurfaceBuildEnabled;
            _paused = true;
            _replay.enabled = false;
            _showcase.AutoWalk = false;
            _far = UnityEngine.Object.FindObjectsByType<ProceduralFarFeatureRenderer>(FindObjectsSortMode.None);
            _farEnabled = new bool[_far.Length];
            for (int i = 0; i < _far.Length; i++) _farEnabled[i] = _far[i].enabled;
            _renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            _rendererEnabled = new bool[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _rendererEnabled[i] = _renderers[i].enabled;
            LogMaterials();
            var frame = new WaitForEndOfFrame();
            yield return frame;
            yield return frame;
            _originalPink = Capture("all-before");

            for (int i = 0; i < _far.Length; i++) _far[i].enabled = false;
            yield return frame;
            yield return frame;
            Capture("no-semantic-far");
            RestoreFar();

            _suppressComponents = true;
            yield return frame;
            yield return frame;
            Capture("no-component-renderers");
            _suppressComponents = false;
            RestoreRenderers();

            _suppressSurface = true;
            yield return frame;
            yield return frame;
            Capture("no-voxel-surface");
            _suppressSurface = false;
            VoxelRenderBridge.SurfaceBuildEnabled = _surfaceBuild;
            yield return frame;
            yield return frame;
            Capture("all-restored");
            Debug.Log($"RENDER_ISOLATION complete farRenderers={_far.Length} componentRenderers={_renderers.Length}; exclusion frames are NOT acceptance evidence.");
            if (_originalPink > 0)
                Debug.LogError($"RENDER_ISOLATION production baseline contains {_originalPink} error-magenta pixels; visual acceptance failed.");
        }

        private void LateUpdate()
        {
            if (_suppressSurface) VoxelRenderBridge.SurfaceBuildEnabled = false;
            if (_suppressComponents && _renderers != null)
                foreach (Renderer renderer in _renderers)
                    if (renderer != null) renderer.enabled = false;
        }

        private int Capture(string phase)
        {
            Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
            if (capture == null) throw new InvalidOperationException("Screen capture returned no image.");
            try
            {
                Color32[] pixels = capture.GetPixels32();
                int pink = 0;
                for (int i = 0; i < pixels.Length; i++)
                    if (pixels[i].r >= 240 && pixels[i].g <= 16 && pixels[i].b >= 240) pink++;
                string path = Path.Combine(_directory, $"diagnostic-{phase}.png");
                File.WriteAllBytes(path, capture.EncodeToPNG());
                Camera camera = Camera.main;
                Debug.Log($"RENDER_ISOLATION phase={phase} pinkPixels={pink} totalPixels={pixels.Length} "
                    + $"camera={(camera != null ? camera.transform.position.ToString("F3") : "none")} "
                    + $"surface={VoxelRenderBridge.LastSurfacePassState} capture={Path.GetFileName(path)}");
                return pink >= Math.Max(64, pixels.Length / 1000) ? pink : 0;
            }
            finally
            {
                UnityEngine.Object.Destroy(capture);
            }
        }

        private void LogMaterials()
        {
            Debug.Log($"RENDER_ISOLATION pipeline={GraphicsSettings.currentRenderPipeline?.name ?? "built-in"} "
                + $"device={SystemInfo.graphicsDeviceType} colorSpace={QualitySettings.activeColorSpace}");
            foreach (Material material in Resources.FindObjectsOfTypeAll<Material>())
            {
                Shader shader = material.shader;
                string color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor").ToString()
                    : material.HasProperty("_Color") ? material.GetColor("_Color").ToString() : "not-exposed";
                string passes = string.Empty;
                for (int i = 0; i < material.passCount; i++)
                    passes += (i == 0 ? "" : ",") + material.GetPassName(i);
                Debug.Log($"RENDER_MATERIAL name={material.name} shader={shader?.name ?? "null"} "
                    + $"supported={(shader != null && shader.isSupported)} instanced={material.enableInstancing} "
                    + $"pipelineTag={material.GetTag("RenderPipeline", false, "none")} passes={passes} "
                    + $"baseColor={color} keywords={string.Join(",", material.shaderKeywords)}");
            }
            foreach (Renderer renderer in _renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                foreach (Material material in renderer.sharedMaterials)
                    Debug.Log($"RENDER_COMPONENT object={renderer.name} type={renderer.GetType().Name} "
                        + $"bounds={renderer.bounds} material={material?.name ?? "null"} shader={material?.shader?.name ?? "null"}");
            }
        }

        private void RestoreFar()
        {
            if (_far == null || _farEnabled == null) return;
            for (int i = 0; i < _far.Length; i++)
                if (_far[i] != null) _far[i].enabled = _farEnabled[i];
        }

        private void RestoreRenderers()
        {
            if (_renderers == null || _rendererEnabled == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = _rendererEnabled[i];
        }

        private void Restore()
        {
            if (!_paused) return;
            _suppressSurface = false;
            _suppressComponents = false;
            VoxelRenderBridge.SurfaceBuildEnabled = _surfaceBuild;
            RestoreFar();
            RestoreRenderers();
            if (_showcase != null) _showcase.AutoWalk = _autoWalk;
            if (_replay != null) _replay.enabled = _replayEnabled;
            _paused = false;
        }

        private void OnDestroy() => Restore();

        private static T FindRuntimeObject<T>() where T : Component
        {
            T[] candidates = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < candidates.Length; i++)
            {
                T candidate = candidates[i];
                if (candidate == null) continue;
                UnityEngine.SceneManagement.Scene scene = candidate.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded) return candidate;
            }
            return null;
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal)) return args[i + 1];
            return null;
        }
    }
}
