using Game.Materials.Runtime;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Composition.Api;
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
    }
}
