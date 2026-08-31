using UnityEngine;
using VoxelEngine.Rendering.Runtime.Vegetation;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene adapter from Showcase tree visibility composition to the engine canopy renderer.
    /// The renderer receives only deterministic presentation clusters and never tree world state.
    /// </summary>
    [DefaultExecutionOrder(420)]
    [DisallowMultipleComponent]
    public sealed class ShowcaseForestCanopyRendering : MonoBehaviour
    {
        private ShowcaseTreeVisibilityComposition _visibility;
        private ProceduralForestCanopyRenderer _renderer;

        public ProceduralForestCanopyRenderer Renderer => _renderer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<VoxelShowcase>() == null) return;
            if (FindFirstObjectByType<ShowcaseForestCanopyRendering>() != null) return;
            var go = new GameObject("Showcase Forest Canopy Rendering") { hideFlags = HideFlags.DontSave };
            go.AddComponent<ShowcaseForestCanopyRendering>();
        }

        private void LateUpdate()
        {
            _visibility = _visibility != null
                ? _visibility
                : FindFirstObjectByType<ShowcaseTreeVisibilityComposition>();
            if (_visibility == null) return;

            if (_renderer == null)
                _renderer = gameObject.GetComponent<ProceduralForestCanopyRenderer>()
                    ?? gameObject.AddComponent<ProceduralForestCanopyRenderer>();

            _renderer.SetClusters(_visibility.CanopyClusters);
        }

        private void OnDisable()
        {
            if (_renderer != null) _renderer.Clear();
        }
    }
}
