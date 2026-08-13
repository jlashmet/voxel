using UnityEngine;

namespace VoxelEngine.CI
{
    /// <summary>
    /// The lookdev branch intentionally accumulated many experimental passes.  For the final
    /// reference shot keep only the coherent terrain mesh and objects deliberately rebuilt by the
    /// ReferencePass.  This makes the screenshot deterministic and prevents stale prototype shapes
    /// from showing through behind the target composition.
    /// </summary>
    internal static class SunlitWaterfallCleanupPass
    {
        private static bool _done;

        public static void Apply(Camera camera)
        {
            if (_done || camera == null) return;
            _done = true;

            GameObject scene = GameObject.Find("Sunlit Coherent Terrain Scene");
            if (scene == null) return;

            Renderer[] renderers = scene.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                string n = renderer.gameObject.name;
                if (n == "Reusable storybook terrain patch" || n.StartsWith("Reference "))
                    continue;
                renderer.gameObject.SetActive(false);
            }
        }
    }
}
