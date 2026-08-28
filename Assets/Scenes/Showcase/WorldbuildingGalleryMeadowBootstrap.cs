using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// One-shot bridge for the Worldbuilding Gallery. It runs after GalleryLifePopulation has
    /// generated/published its semantic instances, then replaces only meadow tufts with the packed
    /// meadow presentation. The reflection is construction-time only and deliberately guarded by
    /// WorldbuildingGalleryShowcase so no other GalleryLifePopulation consumer is changed.
    /// </summary>
    public static class WorldbuildingGalleryMeadowBootstrap
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
#pragma warning disable CS0618
            GalleryLifePopulation[] populations = Object.FindObjectsOfType<GalleryLifePopulation>();
#pragma warning restore CS0618
            for (int i = 0; i < populations.Length; i++)
            {
                GalleryLifePopulation population = populations[i];
                if (population.GetComponent<WorldbuildingGalleryShowcase>() == null) continue;
                if (population.GetComponent<Bridge>() == null)
                    population.gameObject.AddComponent<Bridge>();
            }
        }

        private sealed class Bridge : MonoBehaviour
        {
            private IEnumerator Start()
            {
                // WorldbuildingGalleryShowcase populates life during Start. Waiting one frame makes
                // this ordering explicit and keeps the semantic generation path unchanged.
                yield return null;

                GalleryLifePopulation population = GetComponent<GalleryLifePopulation>();
                if (population == null || population.VegetationCount == 0) yield break;

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
                    GetComponent<WorldbuildingGalleryMeadowRenderer>() ??
                    gameObject.AddComponent<WorldbuildingGalleryMeadowRenderer>();
                meadow.Publish(vegetation, fallback);
                Debug.Log($"Worldbuilding Gallery meadow: {meadow.BladeCount} packed blades, "
                        + $"{meadow.VertexCount} vertices, {meadow.TriangleCount} triangles; "
                        + "construction-only mesh with GPU wind/player deformation.");
                Destroy(this);
            }
        }
    }
}
