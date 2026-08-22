using Game.Materials.Api;
using VoxelEngine.Terrain.Api;

namespace Game.Materials.Runtime
{
    /// <summary>
    /// Game-owned semantic assignment for the engine's generic terrain material slots.
    /// Terrain itself only consumes the resulting opaque material indices.
    /// </summary>
    public static class GameTerrainMaterials
    {
        /// <summary>
        /// Ground cover for the inhabited valley: continuous turf over naturally generated ground,
        /// dirt immediately below it, and bedrock at depth.
        ///
        /// Dirt remains available to authored roads, fields, banks and excavations. It is not used
        /// as the generic low-elevation surface because a binary height split paints closed contour
        /// rings across rolling terrain, which reads as artificial crop circles rather than as a
        /// natural grass/dirt transition.
        /// </summary>
        public static readonly TerrainMaterialSet Default = new TerrainMaterialSet(
            deep: GameMaterialIds.Bedrock,
            subsurface: GameMaterialIds.Dirt,
            lowSurface: GameMaterialIds.Grass,
            surface: GameMaterialIds.Grass);
    }
}
