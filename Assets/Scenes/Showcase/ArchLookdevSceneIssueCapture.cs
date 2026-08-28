using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Produces clean camera-only evidence during the repository's saved SceneIssue replay path.
    /// Normal ArchLookdev sessions are untouched; the component installs only when the standalone
    /// player was launched with both -voxel-scene-issue and -voxel-screenshot-dir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArchLookdevSceneIssueCapture : MonoBehaviour
    {
        private const string ArchSceneName = "ArchLookdev";
        private const string SceneIssueArg = "-voxel-scene-issue";
        private const string ScreenshotDirArg = "-voxel-screenshot-dir";

        private string _screenshotDirectory;
        private Camera _camera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachStartupScene() => AttachIfReplay();

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == ArchSceneName) AttachIfReplay();
        }

        private static void AttachIfReplay()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!TryGetArgument(args, SceneIssueArg, out _) ||
                !TryGetArgument(args, ScreenshotDirArg, out string screenshotDirectory))
                return;

            ArchLookdev lookdev = UnityEngine.Object.FindAnyObjectByType<ArchLookdev>();
            if (lookdev == null || lookdev.GetComponent<ArchLookdevSceneIssueCapture>() != null)
                return;

            ArchLookdevSceneIssueCapture capture =
                lookdev.gameObject.AddComponent<ArchLookdevSceneIssueCapture>();
            capture._screenshotDirectory = screenshotDirectory;
        }

        private void Start()
        {
            _camera = GetComponent<Camera>();
            if (_camera != null && !string.IsNullOrEmpty(_screenshotDirectory))
                StartCoroutine(CaptureLateReplayFrames());
        }

        private IEnumerator CaptureLateReplayFrames()
        {
            Directory.CreateDirectory(_screenshotDirectory);
            yield return new WaitForSecondsRealtime(12f);

            for (int index = 1; index <= 4; index++)
            {
                yield return new WaitForEndOfFrame();
                CaptureCameraOnly(Path.Combine(
                    _screenshotDirectory, $"zz-arch-clean-{index:00}.png"));
                if (index < 4)
                    yield return new WaitForSecondsRealtime(10f);
            }
        }

        private void CaptureCameraOnly(string path)
        {
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture oldTarget = _camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            try
            {
                target.Create();
                _camera.targetTexture = target;
                _camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                image.Apply(false, false);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                _camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                target.Release();
                Destroy(target);
                Destroy(image);
            }
        }

        private static bool TryGetArgument(string[] args, string name, out string value)
        {
            for (int index = 0; index + 1 < args.Length; index++)
            {
                if (!string.Equals(args[index], name, StringComparison.Ordinal))
                    continue;
                value = args[index + 1];
                return !string.IsNullOrEmpty(value);
            }
            value = null;
            return false;
        }
    }
}
