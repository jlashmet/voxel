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
        /// <summary>
        /// Milliseconds per region spent generating features. Shares the streaming budget rather
        /// than adding to it.
        ///
        /// Provisional: terrain generation alone measures around 45 ms per region, so this is a
        /// target rather than an observation. Task T058 measures against it.
        /// </summary>
        public const int GenerationBudgetMs = 8;

        /// <summary>Primitives rasterised into one region before the generator reports overflow.</summary>
        public const int MaxPrimitivesPerRegion = 4096;

        /// <summary>Candidates a region may consider. Exceeding this is reported, never truncated.</summary>
        public const int MaxCandidatesPerRegion = 512;

        /// <summary>Primitives one instance may emit. Statically provable from the shape program.</summary>
        public const int MaxPrimitivesPerInstance = 512;

        /// <summary>
        /// Largest footprint any definition may declare, in voxels (128 m).
        ///
        /// Load-bearing rather than advisory: it bounds the neighbourhood every region scans, so
        /// raising it costs generation time in every region of the world, including regions that
        /// contain nothing.
        /// </summary>
        public const int MaxFootprintVoxels = 1280;

        /// <summary>Placement lattice cell edge in voxels (64 m).</summary>
        public const int PlacementCellEdgeVoxels = 640;

        /// <summary>
        /// Definitions one catalogue may hold. Definition index is its identity.
        ///
        /// Raised from 256 when the world first held two settlements: one town's voxel pass alone
        /// produces most of that budget, so a second pushed the combined catalogue over and
        /// generation failed validation. The index is stored as an int throughout, so the ceiling
        /// is about how much a single world may describe rather than about packing.
        /// </summary>
        public const int MaxDefinitions = 1024;

        /// <summary>Bytes of stored state per instance a player has touched.</summary>
        public const int BytesPerTouchedInstance = 64;
    }
}
