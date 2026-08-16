using Game.Materials.Api;
using VoxelEngine.Storage.Api;

namespace Game.Materials.Runtime
{
    /// <summary>
    /// Game-owned simulation/physical projection of the canonical material IDs.
    /// Rendering remains a separate projection; these definitions contain no textures or shaders.
    /// </summary>
    public static class GameMaterialSimulationDefinitions
    {
        public const int Count = GameMaterialCatalogue.Count - 1; // Empty is not a physical material.

        public static MaterialDefinition[] Create()
        {
            uint weather = (1u << Coatings.Moss) | (1u << Coatings.Snow)
                         | (1u << Coatings.Soot) | (1u << Coatings.Wet);

            return new[]
            {
                new MaterialDefinition(GameMaterialIds.Stone, 200, DestructionClass.Crumble,
                    SurfaceStyles.Smooth, weather, false),
                new MaterialDefinition(GameMaterialIds.Wood, 90, DestructionClass.Splinter,
                    SurfaceStyles.Planar, weather, true),
                new MaterialDefinition(GameMaterialIds.Sand, 20, DestructionClass.Powder,
                    SurfaceStyles.Smooth, 1u << Coatings.Wet, false),
                new MaterialDefinition(GameMaterialIds.Glass, 10, DestructionClass.Powder,
                    SurfaceStyles.Sharp, 1u << Coatings.Wet, false),
                new MaterialDefinition(GameMaterialIds.Bedrock, 255, DestructionClass.None,
                    SurfaceStyles.Planar, 0u, false),
                new MaterialDefinition(GameMaterialIds.DarkStone, 210, DestructionClass.Crumble,
                    SurfaceStyles.Smooth, weather, false),
                new MaterialDefinition(GameMaterialIds.Slate, 120, DestructionClass.Crumble,
                    SurfaceStyles.Planar, weather, false),
                new MaterialDefinition(GameMaterialIds.Tile, 110, DestructionClass.Crumble,
                    SurfaceStyles.Planar, weather, false),
                new MaterialDefinition(GameMaterialIds.Cloth, 15, DestructionClass.Splinter,
                    SurfaceStyles.Planar, weather, true),
                new MaterialDefinition(GameMaterialIds.Grass, 25, DestructionClass.Powder,
                    SurfaceStyles.Smooth, weather, false),
                new MaterialDefinition(GameMaterialIds.Water, 5, DestructionClass.Spreading,
                    SurfaceStyles.Smooth, 0u, false),
                new MaterialDefinition(GameMaterialIds.Gold, 180, DestructionClass.Crumble,
                    SurfaceStyles.Sharp, 1u << Coatings.Soot, false),
                new MaterialDefinition(GameMaterialIds.Dirt, 30, DestructionClass.Powder,
                    SurfaceStyles.Smooth, weather, false),
                new MaterialDefinition(GameMaterialIds.Moss, 40, DestructionClass.Powder,
                    SurfaceStyles.Smooth, weather, false),
                new MaterialDefinition(GameMaterialIds.LitWindow, 18, DestructionClass.Powder,
                    SurfaceStyles.Sharp, 1u << Coatings.Wet, false),

                // These rows were previously unregistered. Register them explicitly with the exact
                // neutral behavior they implicitly had so migration changes ownership, not gameplay.
                // Their real destruction behavior can now be authored deliberately in one place.
                new MaterialDefinition(GameMaterialIds.Cascade, 0, DestructionClass.None,
                    SurfaceStyles.Smooth, 0u, false),
                new MaterialDefinition(GameMaterialIds.Crystal, 0, DestructionClass.None,
                    SurfaceStyles.Smooth, 0u, false),

                new MaterialDefinition(GameMaterialIds.MasonrySmall, 200, DestructionClass.Crumble,
                    SurfaceStyles.MasonryJoint, weather, false),
                new MaterialDefinition(GameMaterialIds.MasonryMedium, 210, DestructionClass.Crumble,
                    SurfaceStyles.MasonryJoint, weather, false),
                new MaterialDefinition(GameMaterialIds.MasonryLarge, 220, DestructionClass.Crumble,
                    SurfaceStyles.MasonryJoint, weather, false),

                new MaterialDefinition(GameMaterialIds.FlowerWhite, 0, DestructionClass.None,
                    SurfaceStyles.Smooth, 0u, false),
            };
        }
    }
}
