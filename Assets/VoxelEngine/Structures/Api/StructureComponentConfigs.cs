namespace VoxelEngine.Structures.Api
{
    /// <summary>Reusable repeated-level/floor-slab configuration.</summary>
    public struct FloorLevelConfig
    {
        public int FloorCount;
        public int LevelHeight;
        public int SlabThickness;
        public int MinimumLevelHeightDelta;
        public int MaximumLevelHeightDelta;
        public StructureMaterialRole SlabMaterialRole;

        public bool IsWellFormed =>
            FloorCount > 0 && LevelHeight > 0 && SlabThickness > 0 &&
            SlabThickness < LevelHeight &&
            MinimumLevelHeightDelta <= MaximumLevelHeightDelta &&
            LevelHeight + MinimumLevelHeightDelta > SlabThickness;
    }

    /// <summary>Shared architectural opening families.</summary>
    public enum StructureOpeningKind : byte
    {
        Door = 0,
        Window = 1,
        Arch = 2,
        Niche = 3,
    }

    /// <summary>
    /// Reusable opening configuration. Width/height variation is resolved from semantic child seeds
    /// by the authoring component; spacing and margins remain integer voxels.
    /// </summary>
    public struct OpeningConfig
    {
        public StructureOpeningKind Kind;
        public int Width;
        public int Height;
        public int BottomOffset;
        public int Spacing;
        public int StartMargin;
        public int EndMargin;
        public int FrameThickness;
        public int LintelThickness;
        public int WidthVariation;
        public int HeightVariation;
        public StructureMaterialRole FrameMaterialRole;
        public StructureMaterialRole FillMaterialRole;

        /// <summary>
        /// Universal opening invariants. A positive spacing is a bay-to-bay pitch, so it must be at
        /// least the widest opening this config can deterministically produce; otherwise repeated
        /// openings can overlap for a valid seed.
        /// </summary>
        public bool IsWellFormed
        {
            get
            {
                if (Width <= 0 || Height <= 0 || BottomOffset < 0 ||
                    Spacing < 0 || StartMargin < 0 || EndMargin < 0 ||
                    FrameThickness < 0 || LintelThickness < 0 ||
                    WidthVariation < 0 || HeightVariation < 0)
                    return false;

                if (WidthVariation >= Width || HeightVariation >= Height)
                    return false;

                int maximumWidth = Width + WidthVariation;
                return Spacing == 0 || Spacing >= maximumWidth;
            }
        }

        /// <summary>
        /// Maximum non-overlapping repeated openings that fit in a wall span after margins. Zero
        /// spacing represents a single explicitly positioned opening rather than repetition.
        /// </summary>
        public int MaxCountForSpan(int span)
        {
            if (!IsWellFormed || span <= 0)
                return 0;

            long usable = (long)span - StartMargin - EndMargin;
            long maximumWidth = (long)Width + WidthVariation;
            if (usable < maximumWidth)
                return 0;
            if (Spacing == 0)
                return 1;

            return 1 + (int)((usable - maximumWidth) / Spacing);
        }
    }

    /// <summary>Roof families expressible using the current bounded integer primitive set.</summary>
    public enum RoofStyle : byte
    {
        Flat = 0,
        Shed = 1,
        Gable = 2,
        Hip = 3,
    }

    /// <summary>Local axis followed by a roof ridge or shed slope.</summary>
    public enum RoofAxis : byte
    {
        X = 0,
        Z = 1,
    }

    /// <summary>
    /// Reusable integer roof configuration. Pitch is rise/run rather than an angle so authoritative
    /// generation never requires floating-point trigonometry.
    /// </summary>
    public struct RoofConfig
    {
        public RoofStyle Style;
        public RoofAxis RidgeAxis;
        public int PitchRise;
        public int PitchRun;
        public int EaveOverhang;
        public int Thickness;
        public int ParapetHeight;
        public StructureMaterialRole MaterialRole;
        public StructureMaterialRole TrimMaterialRole;

        /// <summary>
        /// Rejects combinations the shared integer roof compiler cannot interpret unambiguously.
        /// Flat roofs have no pitch; pitched roofs require a positive rational pitch and do not
        /// carry a flat-roof parapet in the same component.
        /// </summary>
        public bool IsWellFormed
        {
            get
            {
                if (EaveOverhang < 0 || Thickness <= 0 || ParapetHeight < 0)
                    return false;

                if (Style == RoofStyle.Flat)
                    return PitchRise == 0 && PitchRun == 0;

                return PitchRise > 0 && PitchRun > 0 && ParapetHeight == 0;
            }
        }
    }
}
