namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Definition-local direction followed by a stair or ramp run. Keeping direction cardinal
    /// avoids floating-point transforms in authoritative generation.
    /// </summary>
    public enum StructureRunDirection : byte
    {
        PositiveX = 0,
        NegativeX = 1,
        PositiveZ = 2,
        NegativeZ = 3,
    }

    /// <summary>Reusable stair layout families expressible as bounded straight flights.</summary>
    public enum StructureStairLayout : byte
    {
        Straight = 0,
        QuarterTurn = 1,
        HalfTurn = 2,
        Switchback = 3,
    }

    /// <summary>
    /// Optional rectangular landing used between bounded stair/ramp flights. Zero length disables
    /// the landing; enabled landings require positive width and thickness.
    /// </summary>
    public struct LandingConfig
    {
        public int Width;
        public int Length;
        public int Thickness;
        public StructureMaterialRole MaterialRole;

        public bool Enabled => Length > 0;
        public bool IsWellFormed =>
            Width >= 0 &&
            Length >= 0 &&
            Thickness >= 0 &&
            (!Enabled || (Width > 0 && Thickness > 0));
    }

    /// <summary>
    /// Reusable integer stair configuration. Flights remain bounded through StepsPerFlight; turn
    /// layouts compose those straight flights around an explicit landing instead of introducing a
    /// second curved-stair representation.
    /// </summary>
    public struct StairConfig
    {
        public StructureRunDirection Direction;
        public StructureStairLayout Layout;
        public int Width;
        public int StepCount;
        public int StepRise;
        public int StepRun;
        public int StepsPerFlight;
        public LandingConfig Landing;
        public StructureMaterialRole MaterialRole;

        // Compatibility aliases for the original straight-stair authorer. These deliberately map
        // onto the richer bounded-flight contract rather than restoring a second StairConfig type.
        public int Steps => StepCount;
        public int Rise => StepRise;
        public int Run => StepRun;
        public int LandingDepth => Landing.Length;

        public int TotalRise => StepCount * StepRise;
        public int TotalRun => StepCount * StepRun;
        public bool RequiresIntermediateLanding =>
            StepsPerFlight > 0 && StepsPerFlight < StepCount;

        public bool IsWellFormed
        {
            get
            {
                if (Width <= 0 || StepCount <= 0 || StepRise <= 0 || StepRun <= 0)
                    return false;
                if (StepsPerFlight < 0 || StepsPerFlight > StepCount)
                    return false;
                if (!Landing.IsWellFormed)
                    return false;
                if (RequiresIntermediateLanding && !Landing.Enabled)
                    return false;
                if (Layout != StructureStairLayout.Straight && !RequiresIntermediateLanding)
                    return false;

                return true;
            }
        }
    }

    /// <summary>
    /// Reusable integer ramp configuration. MaxRunPerFlight bounds long ramps and requires an
    /// explicit landing whenever a run must be split into multiple flights.
    /// </summary>
    public struct RampConfig
    {
        public StructureRunDirection Direction;
        public int Width;
        public int Rise;
        public int Run;
        public int Thickness;
        public int MaxRunPerFlight;
        public LandingConfig Landing;
        public StructureMaterialRole MaterialRole;

        public bool RequiresIntermediateLanding =>
            MaxRunPerFlight > 0 && Run > MaxRunPerFlight;

        public bool IsWellFormed
        {
            get
            {
                if (Width <= 0 || Rise <= 0 || Run <= 0 || Thickness <= 0)
                    return false;
                if (MaxRunPerFlight < 0 || !Landing.IsWellFormed)
                    return false;
                if (RequiresIntermediateLanding && !Landing.Enabled)
                    return false;

                return true;
            }
        }
    }
}
