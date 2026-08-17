namespace VoxelEngine.Structures.Api
{
    /// <summary>Reusable bounded straight stair configuration for structure approaches/interiors.</summary>
    public struct StairConfig
    {
        public int Width;
        public int Steps;
        public int Rise;
        public int Run;
        public int LandingDepth;
        public StructureMaterialRole MaterialRole;

        public int TotalRise => Steps * Rise;
        public int TotalRun => Steps * Run + LandingDepth;

        public bool IsWellFormed =>
            Width > 0 && Steps > 0 && Steps <= 256 &&
            Rise > 0 && Run > 0 && LandingDepth >= 0;
    }

    /// <summary>Simple integer ramp contract where slope is rise/run.</summary>
    public struct RampConfig
    {
        public int Width;
        public int Length;
        public int Rise;
        public int Run;
        public int Thickness;
        public StructureMaterialRole MaterialRole;

        public bool IsWellFormed =>
            Width > 0 && Length > 0 && Rise >= 0 && Run > 0 && Thickness > 0;
    }
}
