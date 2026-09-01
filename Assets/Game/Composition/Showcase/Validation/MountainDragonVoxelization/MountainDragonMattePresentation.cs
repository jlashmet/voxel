using Game.Materials.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene-owned material policy for the Mountain Dragon comparison fixture. The canonical bake
    /// remains DarkStone world data; only this fixture requests cubic reconstruction and a fully
    /// rough GPU presentation for the Dragon material.
    /// </summary>
    public static class MountainDragonMattePresentation
    {
        public const float DragonRoughness = 1f;
        public const float DragonSmoothness = 0f;
        public const ushort DragonSurfaceStyle = SurfaceStyles.Cubic;
        private const string ValidationSceneName = "MountainDragonVoxelValidation";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyToLoadedValidationScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!string.Equals(scene.name, ValidationSceneName, System.StringComparison.Ordinal))
                return;

            MaterialPresentationComposition.Apply(CreateDefinitions());
            Debug.Log(
                "MOUNTAIN_DRAGON_BLOCK_PRESENTATION material="
                + MountainDragonPalettePolicy.DragonMaterial
                + " surface_style=" + DragonSurfaceStyle
                + " roughness=" + DragonRoughness.ToString("F1")
                + " smoothness=" + DragonSmoothness.ToString("F1"));
        }

        public static MaterialDefinition[] CreateSimulationDefinitions()
        {
            MaterialDefinition[] definitions = GameMaterialSimulationDefinitions.Create();
            for (int i = 0; i < definitions.Length; i++)
            {
                MaterialDefinition definition = definitions[i];
                if (definition.MaterialId != MountainDragonPalettePolicy.DragonMaterial)
                    continue;

                definitions[i] = new MaterialDefinition(
                    definition.MaterialId,
                    definition.Hardness,
                    definition.DestructionClass,
                    DragonSurfaceStyle,
                    definition.AllowedCoatings,
                    definition.Flammable,
                    definition.PlacementSurfaceStyle,
                    definition.PlacementCoating);
                return definitions;
            }

            throw new System.InvalidOperationException(
                "Mountain Dragon material is missing from the game simulation catalogue.");
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
