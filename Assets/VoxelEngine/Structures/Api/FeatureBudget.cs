namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Budgets for world feature generation.
    ///
    /// These mirror the "World features" table in
    /// `specs/001-destructible-voxel-engine/device-matrix.md`, which is the authoritative source.
    /// If a number here and a number there disagree, the document is right and this file is a
    /// defect.
    ///
    /// Every value is a *simulation* parameter and is therefore identical on every device class
    /// (Constitution IV). None of these may be moved into <c>DeviceTierBudget</c>: a tiered
    /// placement budget would put a village on a PC and not on a phone, which is the same class
    /// of defect as tiering interest radius.
    /// </summary>
    public static class FeatureBudget
    {
        public const int GenerationBudgetMs = 8;
        public const int MaxPrimitivesPerRegion = 4096;
        public const int MaxCandidatesPerRegion = 512;
        public const int MaxPrimitivesPerInstance = 512;
        public const int MaxFootprintVoxels = 1280;
        public const int PlacementCellEdgeVoxels = 640;
        public const int MaxDefinitions = 1024;
        public const int BytesPerTouchedInstance = 64;

        // Structural composition budgets are deliberately below the existing world ceilings. They
        // bound graph planning without weakening any per-instance/region/device invariant.
        public const int MaxCompositionDepth = 12;
        public const int MaxCompositionChildren = 256;
        public const int MaxCompositionPrimitiveCost = MaxPrimitivesPerRegion;
        public const int MaxCompositionExtentVoxels = MaxFootprintVoxels * 8;
    }
}
