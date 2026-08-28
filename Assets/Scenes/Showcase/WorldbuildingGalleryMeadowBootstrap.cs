using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// One-shot bridge for the Worldbuilding Gallery. The scene creates GalleryLifePopulation on
    /// its world-object host during OnEnable, so this bridge anchors on the authored showcase
    /// component, then resolves that already-populated runtime host after scene initialization.
    /// No other scene or GalleryLifePopulation consumer is changed.
    /// </summary>
    public static class WorldbuildingGalleryMeadowBootstrap
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
#pragma warning disable CS0618
            WorldbuildingGalleryShowcase[] showcases = Object.FindObjectsOfType<WorldbuildingGalleryShowcase>();
#pragma warning restore CS0618
            for (int i = 0; i < showcases.Length; i++)
            {
                WorldbuildingGalleryShowcase showcase = showcases[i];
                if (showcase.GetComponent<Bridge>() == null)
                    showcase.gameObject.AddComponent<Bridge>();
            }
        }

        private sealed class Bridge : MonoBehaviour
        {
            private IEnumerator Start()
            {
                // OnEnable builds the blocking gallery world and populates semantic vegetation.
                // Waiting one frame makes that ownership explicit and keeps mesh construction out
                // of the per-frame path.
                yield return null;

                WorldbuildingGalleryShowcase showcase = GetComponent<WorldbuildingGalleryShowcase>();
                FieldInfo lifeField = typeof(WorldbuildingGalleryShowcase).GetField("_life", PrivateInstance);
                GalleryLifePopulation population = lifeField?.GetValue(showcase) as GalleryLifePopulation;
                if (population == null || population.VegetationCount == 0)
                {
                    Debug.LogError("Worldbuilding Gallery meadow could not resolve populated gallery life.");
                    yield break;
                }

                FieldInfo vegetationField = typeof(GalleryLifePopulation).GetField("_vegetation", PrivateInstance);
                FieldInfo rendererField = typeof(GalleryLifePopulation).GetField("_vegetationRenderer", PrivateInstance);
                var vegetation = vegetationField?.GetValue(population) as List<VegetationInstance>;
                var fallback = rendererField?.GetValue(population) as IVegetationBatchRenderer;
                if (vegetation == null || fallback == null)
                {
                    Debug.LogError("Worldbuilding Gallery meadow could not resolve the populated vegetation batch.");
                    yield break;
                }

                WorldbuildingGalleryMeadowRenderer meadow =
                    population.GetComponent<WorldbuildingGalleryMeadowRenderer>() ??
                    population.gameObject.AddComponent<WorldbuildingGalleryMeadowRenderer>();
                meadow.Publish(vegetation, fallback, showcase.transform);
                Debug.Log($"Worldbuilding Gallery meadow: {meadow.BladeCount} packed blades, "
                        + $"{meadow.VertexCount} vertices, {meadow.TriangleCount} triangles; "
                        + "construction-only mesh with GPU wind/player deformation.");
                Destroy(this);
            }
        }
    }
}
