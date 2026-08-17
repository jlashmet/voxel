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
    /// generation never requires floating-point trigonometry. Flat roofs ignore pitch and ridge.
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
    }

    /// <summary>Shared vertical-access families used by structure archetypes.</summary>
    public enum StructureVerticalAccessKind : byte
    {
        Stairs = 0,
        Ramp = 1,
    }

    /// <summary>Plan shape for a stair run when more than one flight is required.</summary>
    public enum StructureStairLayout : byte
    {
        Straight = 0,
        QuarterTurn = 1,
        HalfTurn = 2,
        Switchback = 3,
    }

    /// <summary>
    /// Reusable landing slab configuration. Dimensions are integer voxel units and the material is
    /// semantic so archetypes do not embed material ids.
    /// </summary>
    public struct LandingConfig
    {
        public int Width;
        public int Length;
        public int Thickness;
        public StructureMaterialRole MaterialRole;

        public bool IsWellFormed => Width > 0 && Length > 0 && Thickness > 0;
    }

    /// <summary>
    /// Archetype-neutral stair configuration. Step rise/run stay integer; StepsPerFlight bounds
    /// flight length and causes the authoring component to insert the configured landing as needed.
    /// </summary>
    public struct StairConfig
    {
        public int Width;
        public int StepRise;
        public int StepRun;
        public int StepCount;
        public int StepsPerFlight;
        public StructureStairLayout Layout;
        public LandingConfig Landing;
        public StructureMaterialRole MaterialRole;

        public int TotalRise => StepRise * StepCount;
        public int TotalRun => StepRun * StepCount;
        public bool RequiresIntermediateLanding => StepCount > StepsPerFlight;

        public bool IsWellFormed
        {
            get
            {
                if (Width <= 0 || StepRise <= 0 || StepRun <= 0 || StepCount <= 0)
                    return false;
                if (StepsPerFlight <= 0 || StepsPerFlight > StepCount)
                    return false;
                if (RequiresIntermediateLanding && !Landing.IsWellFormed)
                    return false;
                return true;
            }
        }
    }

    /// <summary>
    /// Archetype-neutral ramp configuration. Rise/run are integer totals; MaxRunPerFlight bounds a
    /// continuous ramp flight and requires the configured landing when the total run exceeds it.
    /// </summary>
    public struct RampConfig
    {
        public int Width;
        public int Rise;
        public int Run;
        public int Thickness;
        public int MaxRunPerFlight;
        public LandingConfig Landing;
        public StructureMaterialRole MaterialRole;

        public bool RequiresIntermediateLanding => Run > MaxRunPerFlight;

        public bool IsWellFormed
        {
            get
            {
                if (Width <= 0 || Rise <= 0 || Run <= 0 || Thickness <= 0)
                    return false;
                if (MaxRunPerFlight <= 0 || MaxRunPerFlight > Run)
                    return false;
                if (RequiresIntermediateLanding && !Landing.IsWellFormed)
                    return false;
                return true;
            }
        }
    }

    /// <summary>One reusable vertical transition selecting either stairs or a ramp.</summary>
    public struct VerticalAccessConfig
    {
        public StructureVerticalAccessKind Kind;
        public StairConfig Stairs;
        public RampConfig Ramp;

        public bool IsWellFormed => Kind == StructureVerticalAccessKind.Stairs
            ? Stairs.IsWellFormed
            : Ramp.IsWellFormed;
    }
}
