namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Definition-local direction followed by a straight stair or ramp run. Keeping the direction
    /// cardinal avoids introducing floating-point transforms into authoritative authoring.
    /// </summary>
    public enum StructureRunDirection : byte
    {
        PositiveX = 0,
        NegativeX = 1,
        PositiveZ = 2,
        NegativeZ = 3,
    }

    /// <summary>
    /// Optional rectangular landing attached to the end of a stair or ramp. A zero length disables
    /// the landing; enabled landings require positive thickness. Width is inherited from the run.
    /// </summary>
    public struct LandingConfig
    {
        public int Length;
        public int Thickness;
        public StructureMaterialRole MaterialRole;

        public bool Enabled => Length > 0;
        public bool IsWellFormed => Length >= 0 && (!Enabled || Thickness > 0);
    }

    /// <summary>
    /// Reusable straight-stair configuration expressed entirely in integer voxel dimensions.
    /// Total rise and horizontal run are derived from the bounded step count rather than authored
    /// independently, preventing contradictory stair dimensions.
    /// </summary>
    public struct StairConfig
    {
        public StructureRunDirection Direction;
        public int Width;
        public int StepCount;
        public int StepRise;
        public int StepRun;
        public LandingConfig BottomLanding;
        public LandingConfig TopLanding;
        public StructureMaterialRole StepMaterialRole;

        public int TotalRise => StepCount * StepRise;
        public int TotalRun => StepCount * StepRun;

        public bool IsWellFormed =>
            Width > 0 &&
            StepCount > 0 &&
            StepRise > 0 &&
            StepRun > 0 &&
            BottomLanding.IsWellFormed &&
            TopLanding.IsWellFormed;
    }

    /// <summary>
    /// Reusable straight-ramp configuration. Rise and run are integer extents suitable for the
    /// existing bounded ramp primitive; no angle or trigonometric representation is required.
    /// </summary>
    public struct RampConfig
    {
        public StructureRunDirection Direction;
        public int Width;
        public int Rise;
        public int Run;
        public int Thickness;
        public LandingConfig BottomLanding;
        public LandingConfig TopLanding;
        public StructureMaterialRole RampMaterialRole;

        public bool IsWellFormed =>
            Width > 0 &&
            Rise > 0 &&
            Run > 0 &&
            Thickness > 0 &&
            BottomLanding.IsWellFormed &&
            TopLanding.IsWellFormed;
    }
}
