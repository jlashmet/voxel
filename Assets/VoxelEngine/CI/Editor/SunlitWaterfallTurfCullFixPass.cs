using UnityEngine;

namespace VoxelEngine.CI
{
    internal static class SunlitWaterfallTurfCullFixPass
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
                if (renderer == null || !renderer.gameObject.name.StartsWith("Reference3 grass turf cap"))
                    continue;

                Material material = renderer.sharedMaterial;
                if (material != null && material.HasProperty("_Cull"))
                    material.SetFloat("_Cull", 0f);
            }
        }
    }
}
