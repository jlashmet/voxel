using System.Collections.Generic;
using Game.Materials.Runtime;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Composition.Api;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Storage.Api;

namespace Game.Composition.Materials
{
    /// <summary>
    /// Single application composition entry point for the game's installed material presentation.
    /// Runtime and Editor bootstraps both call this method so the renderer sees the same game-owned
    /// projection in both modes.
    /// </summary>
    public static class GameMaterialComposition
    {
        private static readonly Dictionary<byte, Material> s_ProceduralMaterials =
            new Dictionary<byte, Material>();

        public static void Install()
        {
            MaterialPresentationComposition.Apply(GameMaterialRenderingDefinitions.Create());
        }

        // The showcase scene assembly is an API-only application shell and may not wire
        // Game.Materials.Runtime itself. Composition is the sanctioned place to reach concrete
        // Runtime implementations, so the few game-owned material facts the scene needs are
        // surfaced here rather than by widening the scene's references.

        /// <summary>Game-owned material roles for the showcase world and its far terrain.</summary>
        public static ShowcaseMaterialSet ShowcaseMaterials => GameShowcaseMaterials.Default;

        /// <summary>Game-owned simulation behaviour for each material.</summary>
        public static MaterialDefinition[] SimulationDefinitions() =>
            GameMaterialSimulationDefinitions.Create();

        /// <summary>Debris impulse weighting for a material.</summary>
        public static float DebrisImpulseScale(byte materialId) =>
            GameMaterialDebrisPresentation.ImpulseScale(materialId);

        /// <summary>Debris tint for a material at the given alpha.</summary>
        public static float4 DebrisColour(byte materialId, float alpha) =>
            GameMaterialDebrisPresentation.Colour(materialId, alpha);

        /// <summary>
        /// Resolves a game material id into the shared Unity material used by non-voxel procedural
        /// geometry. The adapter is driven from the same authoritative rendering definition installed
        /// into the voxel renderer, so procedural consumers preserve game material identity/albedo/
        /// roughness rather than inventing preview-only colors or shaders.
        /// </summary>
        public static bool TryGetProceduralMaterial(byte materialId, out Material material)
        {
            if (s_ProceduralMaterials.TryGetValue(materialId, out material) && material != null)
                return true;

            MaterialPresentationDefinition[] definitions = GameMaterialRenderingDefinitions.Create();
            MaterialPresentationDefinition definition = default;
            bool found = false;
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i].MaterialIndex != materialId) continue;
                definition = definitions[i];
                found = true;
                break;
            }
            if (!found)
            {
                material = null;
                return false;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                material = null;
                return false;
            }

            material = new Material(shader)
            {
                name = $"Game Material {materialId} (Procedural)",
                hideFlags = HideFlags.HideAndDontSave,
                color = new Color(
                    definition.Albedo.x,
                    definition.Albedo.y,
                    definition.Albedo.z,
                    definition.Albedo.w),
            };
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", Mathf.Clamp01(1f - definition.Surface.z));
            s_ProceduralMaterials[materialId] = material;
            return true;
        }
    }
}
