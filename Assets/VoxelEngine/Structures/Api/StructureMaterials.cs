using System;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Temporary compatibility facade for structure code that has not yet been rewritten to use
    /// purpose-based material roles directly. These names no longer define numeric material IDs;
    /// Composition binds them from an application-owned <see cref="StructureMaterialSet"/>.
    /// </summary>
    public static class Mat
    {
        private static StructureMaterialSet s_Roles;
        private static bool s_Configured;

        public static bool IsConfigured => s_Configured;

        internal static void ConfigureCompatibility(in StructureMaterialSet roles)
        {
            s_Roles = roles;
            s_Configured = true;
        }

        internal static void ResetCompatibility()
        {
            s_Roles = default;
            s_Configured = false;
        }

        private static ref readonly StructureMaterialSet Roles
        {
            get
            {
                if (!s_Configured)
                    throw new InvalidOperationException(
                        "Structure material roles have not been configured by the application composition root.");
                return ref s_Roles;
            }
        }

        public static byte Empty => Roles.Void;
        public static byte Stone => Roles.PrimaryMasonry;
        public static byte Wood => Roles.Timber;
        public static byte Sand => Roles.LooseAggregate;
        public static byte Glass => Roles.TransparentInfill;
        public static byte Bedrock => Roles.IndestructibleBase;
        public static byte DarkStone => Roles.DarkMasonry;
        public static byte Slate => Roles.SlateRoof;
        public static byte Tile => Roles.TileRoof;
        public static byte Cloth => Roles.TextileAccent;
        public static byte Grass => Roles.GroundCover;
        public static byte Water => Roles.Water;
        public static byte Gold => Roles.MetalAccent;
        public static byte Dirt => Roles.Earth;
        public static byte Moss => Roles.Overgrowth;
        public static byte LitWindow => Roles.WarmWindow;
        public static byte Cascade => Roles.AeratedWater;
        public static byte Crystal => Roles.CoolEmissiveAccent;
        public static byte MasonrySmall => Roles.FineMasonry;
        public static byte MasonryMedium => Roles.MediumMasonry;
        public static byte MasonryLarge => Roles.LargeMasonry;
        public static byte FlowerWhite => Roles.PaleFlora;

        public static byte TerrainTurf => Roles.GroundCover;
        public static byte TerrainLimestone => Roles.MediumMasonry;
        public static byte TerrainEarth => Roles.Earth;
        public static byte TerrainPathStone => Roles.FineMasonry;

        // Transitional aliases still share identity, but the choice now belongs to game content.
        public static byte FlowerYellow => Roles.MetalAccent;
        public static byte FlowerPink => Roles.TextileAccent;
        public static byte FlowerBlue => Roles.AeratedWater;
    }
}
