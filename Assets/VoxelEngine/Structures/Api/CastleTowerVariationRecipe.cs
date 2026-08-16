namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Frozen compatibility result for legacy tower visual variation. Production spatial castles
    /// carry the same values directly on CastleTowerPlacementSpec; this exists only so compatibility
    /// realization does not derive authored seeds inside Structures.Runtime.
    /// </summary>
    public readonly struct CastleTowerVariation
    {
        public readonly int HeightVariation;
        public readonly bool HasRoof;

        public CastleTowerVariation(int heightVariation, bool hasRoof)
        {
            HeightVariation = heightVariation;
            HasRoof = hasRoof;
        }
    }

    public static class CastleTowerVariationRecipe
    {
        public static CastleTowerVariation Historical(uint castleSeed, int index, bool corner)
        {
            uint variationSeed = CastleSeedPartition.Derive(
                castleSeed, CastleSeedDomain.Walls, (uint)(0x2000 + index));
            return new CastleTowerVariation(
                8 + (int)(variationSeed % 51u),
                corner && ((variationSeed >> 8) & 1u) != 0u);
        }
    }
}
