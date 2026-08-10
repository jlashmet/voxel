using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Tiny FPS-only diagnostic for the showcase. Kept deliberately separate from the old
    /// full-screen diagnostics HUD so performance can be watched without covering the scene.
    /// </summary>
    public sealed class CompactFpsOverlay : MonoBehaviour
    {
        private const float SampleSeconds = 0.25f;
        private const float Margin = 6f;
        private const float Width = 78f;
        private const float Height = 22f;

        private float _elapsed;
        private int _frames;
        private string _label = "FPS --";
        private GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForShowcase()
        {
            if (SceneManager.GetActiveScene().name != "VoxelShowcase") return;
            if (Object.FindFirstObjectByType<CompactFpsOverlay>() != null) return;

            var root = new GameObject("Compact FPS Overlay")
            {
                hideFlags = HideFlags.DontSave
            };
            root.AddComponent<CompactFpsOverlay>();
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            _frames++;
            if (_elapsed < SampleSeconds) return;

            float fps = _elapsed > 0f ? _frames / _elapsed : 0f;
            _label = $"FPS {fps:0}";
            _elapsed = 0f;
            _frames = 0;
        }

        private void OnGUI()
        {
            _style ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 2, 2)
            };

            GUI.Box(new Rect(Margin, Margin, Width, Height), _label, _style);
        }
    }
}
