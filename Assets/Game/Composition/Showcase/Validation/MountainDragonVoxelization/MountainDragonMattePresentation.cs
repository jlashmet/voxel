using Game.Materials.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene-owned presentation override for the Mountain Dragon comparison fixture.
    /// The canonical bake remains DarkStone world data; only this validation scene's GPU
    /// presentation is changed so the voxel blocks have zero smoothness.
    /// </summary>
    public static class MountainDragonMattePresentation
    {
        public const float DragonRoughness = 1f;
        public const float DragonSmoothness = 0f;
        private const string ValidationSceneName = "MountainDragonVoxelValidation";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyToLoadedValidationScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!string.Equals(scene.name, ValidationSceneName, System.StringComparison.Ordinal))
                return;

            MaterialPresentationComposition.Apply(CreateDefinitions());
            Debug.Log(
                "MOUNTAIN_DRAGON_MATTE_PRESENTATION material="
                + MountainDragonPalettePolicy.DragonMaterial
                + " roughness=" + DragonRoughness.ToString("F1")
                + " smoothness=" + DragonSmoothness.ToString("F1"));
        }

        public static MaterialPresentationDefinition[] CreateDefinitions()
        {
            MaterialPresentationDefinition[] definitions = GameMaterialRenderingDefinitions.Create();
            for (int i = 0; i < definitions.Length; i++)
            {
                MaterialPresentationDefinition definition = definitions[i];
                if (definition.MaterialIndex != MountainDragonPalettePolicy.DragonMaterial)
                    continue;

                definitions[i] = new MaterialPresentationDefinition(
                    definition.MaterialIndex,
                    definition.Albedo,
                    (int)definition.Sampling.x,
                    (int)definition.Sampling.y,
                    (MaterialTextureProjection)(byte)definition.Sampling.z,
                    definition.Sampling.w,
                    definition.Surface.x,
                    definition.Surface.y,
                    DragonRoughness,
                    definition.Surface.w > 0.5f,
                    definition.Variation.x,
                    definition.Variation.y,
                    definition.Variation.z,
                    definition.Variation.w,
                    definition.Water);
                return definitions;
            }

            throw new System.InvalidOperationException(
                "Mountain Dragon material is missing from the game rendering catalogue.");
        }
    }
}
