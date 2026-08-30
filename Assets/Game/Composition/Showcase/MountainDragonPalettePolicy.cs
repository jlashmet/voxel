using Game.Materials.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Deterministic game/composition-owned material policy for the mountain-dragon bake.
    /// The uploaded STL/derived OBJ has no standard source material regions, so this policy does
    /// not claim source-color preservation; it supplies one explicit canonical voxel material for
    /// otherwise-unmaterialed source triangles and interior fill.
    /// </summary>
    public static class MountainDragonPalettePolicy
    {
        public const byte DragonMaterial = GameMaterialIds.DarkStone;

        public static byte MapSourceMaterial(byte sourceMaterial)
        {
            // The current source has no authored material regions. If a future licensed source
            // supplies real region identity, that source-specific mapping belongs here rather than
            // in the shared mesh voxelizer.
            return sourceMaterial == GameMaterialIds.Empty
                ? DragonMaterial
                : sourceMaterial;
        }
    }
}
