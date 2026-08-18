using Unity.Collections;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// How a wall run yields space to neighbouring perpendicular runs at its endpoints.
    /// Insets are derived from the wall thickness so adjacent runs can compose without each
    /// archetype re-implementing corner overlap rules.
    /// </summary>
    public enum StructureWallCornerBehavior : byte
    {
        /// <summary>Author the full run. Suitable when overlap is harmless or intentional.</summary>
        Overlap = 0,

        /// <summary>Inset the start by one wall thickness.</summary>
        TrimStart = 1,

        /// <summary>Inset the end by one wall thickness.</summary>
        TrimEnd = 2,

        /// <summary>Inset both endpoints by one wall thickness.</summary>
        TrimBoth = 3,
    }

    /// <summary>
    /// One vertical material override within a wall run. StartY is relative to the bottom of the
    /// run and Height is positive. Bands are bounded configuration, not extra voxel state.
    /// </summary>
    public struct StructureWallMaterialBand
    {
        public int StartY;
        public int Height;
        public StructureMaterialRole Material;

        public int EndYExclusive => StartY + Height;

        public StructureWallMaterialBand(
            int startY,
            int height,
            StructureMaterialRole material)
        {
            StartY = startY;
            Height = height;
            Material = material;
        }

        public bool FitsWithin(int wallHeight) =>
            StartY >= 0 && Height > 0 && EndYExclusive <= wallHeight;
    }

    /// <summary>
    /// Archetype-neutral configuration for one straight wall run.
    ///
    /// Geometry/orientation is supplied by the composing structure. This type owns only reusable
    /// wall policy: dimensions, semantic materials, vertical material bands, corner trimming, and
    /// optional deterministic repetition spacing for bays/openings/details layered on the run.
    /// Fixed-size native storage keeps authored complexity bounded and Burst-compatible.
    /// </summary>
    public struct StructureWallRunConfig
    {
        public int Length;
        public int Height;
        public int Thickness;
        public StructureMaterialRole PrimaryMaterial;

        /// <summary>
        /// Vertical overrides applied over <see cref="PrimaryMaterial"/>. Bands must fit inside the
        /// wall height and may not overlap; deterministic emitters can therefore process them in any
        /// order without changing the resolved material at a voxel.
        /// </summary>
        public FixedList128Bytes<StructureWallMaterialBand> MaterialBands;

        public StructureWallCornerBehavior CornerBehavior;

        /// <summary>
        /// Distance between repeated bays/details along the usable run. Zero disables repetition.
        /// The config does not prescribe what is repeated; opening/decorative components consume it.
        /// </summary>
        public int RepetitionSpacing;

        /// <summary>
        /// Offset from the usable run start to the first repeated bay/detail. Must be non-negative
        /// and smaller than <see cref="RepetitionSpacing"/> when repetition is enabled.
        /// </summary>
        public int RepetitionOffset;

        public int StartInset =>
            CornerBehavior == StructureWallCornerBehavior.TrimStart ||
            CornerBehavior == StructureWallCornerBehavior.TrimBoth
                ? Thickness
                : 0;

        public int EndInset =>
            CornerBehavior == StructureWallCornerBehavior.TrimEnd ||
            CornerBehavior == StructureWallCornerBehavior.TrimBoth
                ? Thickness
                : 0;

        public int UsableLength => Length - StartInset - EndInset;

        /// <summary>
        /// Cheap structural validity. Higher-level component validation may impose archetype- or
        /// budget-specific limits, but every wall run must satisfy these universal invariants.
        /// </summary>
        public bool IsWellFormed
        {
            get
            {
                if (Length <= 0 || Height <= 0 || Thickness <= 0)
                    return false;
                if (UsableLength <= 0)
                    return false;
                if (RepetitionSpacing < 0 || RepetitionOffset < 0)
                    return false;
                if (RepetitionSpacing == 0 && RepetitionOffset != 0)
                    return false;
                if (RepetitionSpacing > 0 && RepetitionOffset >= RepetitionSpacing)
                    return false;

                for (var i = 0; i < MaterialBands.Length; i++)
                {
                    var band = MaterialBands[i];
                    if (!band.FitsWithin(Height))
                        return false;

                    for (var j = 0; j < i; j++)
                    {
                        var previous = MaterialBands[j];
                        bool overlaps = band.StartY < previous.EndYExclusive
                                     && previous.StartY < band.EndYExclusive;
                        if (overlaps)
                            return false;
                    }
                }

                return true;
            }
        }
    }
}
