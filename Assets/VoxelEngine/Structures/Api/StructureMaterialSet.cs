namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Opaque material roles consumed by structure-generation algorithms. The engine owns the
    /// purposes; the application owns which semantic material index occupies each role.
    /// </summary>
    public readonly struct StructureMaterialSet
    {
        public readonly byte Void;
        public readonly byte PrimaryMasonry;
        public readonly byte Timber;
        public readonly byte LooseAggregate;
        public readonly byte TransparentInfill;
        public readonly byte IndestructibleBase;
        public readonly byte DarkMasonry;
        public readonly byte SlateRoof;
        public readonly byte TileRoof;
        public readonly byte TextileAccent;
        public readonly byte GroundCover;
        public readonly byte Water;
        public readonly byte MetalAccent;
        public readonly byte Earth;
        public readonly byte Overgrowth;
        public readonly byte WarmWindow;
        public readonly byte AeratedWater;
        public readonly byte CoolEmissiveAccent;
        public readonly byte FineMasonry;
        public readonly byte MediumMasonry;
        public readonly byte LargeMasonry;
        public readonly byte PaleFlora;

        public StructureMaterialSet(
            byte @void,
            byte primaryMasonry,
            byte timber,
            byte looseAggregate,
            byte transparentInfill,
            byte indestructibleBase,
            byte darkMasonry,
            byte slateRoof,
            byte tileRoof,
            byte textileAccent,
            byte groundCover,
            byte water,
            byte metalAccent,
            byte earth,
            byte overgrowth,
            byte warmWindow,
            byte aeratedWater,
            byte coolEmissiveAccent,
            byte fineMasonry,
            byte mediumMasonry,
            byte largeMasonry,
            byte paleFlora)
        {
            Void = @void;
            PrimaryMasonry = primaryMasonry;
            Timber = timber;
            LooseAggregate = looseAggregate;
            TransparentInfill = transparentInfill;
            IndestructibleBase = indestructibleBase;
            DarkMasonry = darkMasonry;
            SlateRoof = slateRoof;
            TileRoof = tileRoof;
            TextileAccent = textileAccent;
            GroundCover = groundCover;
            Water = water;
            MetalAccent = metalAccent;
            Earth = earth;
            Overgrowth = overgrowth;
            WarmWindow = warmWindow;
            AeratedWater = aeratedWater;
            CoolEmissiveAccent = coolEmissiveAccent;
            FineMasonry = fineMasonry;
            MediumMasonry = mediumMasonry;
            LargeMasonry = largeMasonry;
            PaleFlora = paleFlora;
        }
    }
}
