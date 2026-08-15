using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>What kind of thing a definition describes. Selects the generation path.</summary>
    public enum FeatureKind : byte
    {
        /// <summary>Gameplay buildings and other independently addressed structures.</summary>
        Structure = 0,

        /// <summary>Caves and interiors. Removes terrain rather than adding to it.</summary>
        Excavation = 1,

        /// <summary>Cliffs and ravines. Reshapes terrain at terrain scale.</summary>
        Landform = 2,

        /// <summary>Static water. A shape, not a simulation.</summary>
        WaterBody = 3,

        /// <summary>
        /// Built hardscape that belongs to settlement circulation/topography rather than the stable
        /// gameplay-building roster: retaining walls, stairs, bridges, arcades, campaniles, town
        /// walls, and similar civic fabric. Infrastructure renders through the crisp hard-surface
        /// path but is intentionally not counted as a gameplay <see cref="Structure"/>.
        /// </summary>
        Infrastructure = 4,
    }

    /// <summary>How an instance decides what altitude it sits at.</summary>
    public enum BasePlaneRule : byte
    {
        /// <summary>Lowest ground under the footprint. Nothing floats; some of it is buried.</summary>
        LowestGround = 0,

        /// <summary>Mean ground. Splits the difference — needs both fill and cut.</summary>
        MeanGround = 1,

        /// <summary>Highest ground. Nothing is buried; some of it floats without a skirt.</summary>
        HighestGround = 2,

        /// <summary>A fixed altitude, ignoring terrain. For things that belong at sea level.</summary>
        FixedAltitude = 3,
    }

    /// <summary>
    /// The reusable description of a kind of thing: a house, a tower, a cave system, a cliff.
    ///
    /// Holds no location. An instance is this plus a position, an orientation, and a set of
    /// parameter draws — all of which are derived from the seed rather than stored.
    ///
    /// Arrays are stored as offset and count into the catalogue's shared pools rather than as
    /// references, so the whole catalogue is one blittable blob that a Burst job can read without
    /// managed indirection.
    /// </summary>
    public struct FeatureDefinition
    {
        public FixedString64Bytes Name;

        public FeatureKind Kind;
        public BasePlaneRule BasePlane;

        /// <summary>
        /// Maximum extent in voxels.
        ///
        /// Load-bearing rather than advisory: it bounds the neighbourhood every region in the
        /// world scans, so raising it costs generation time everywhere, including in regions that
        /// contain nothing. Validation rejects content that escapes it, and generation clips.
        /// </summary>
        public int3 Footprint;

        /// <summary>Steepest ground this definition tolerates, as rise per 8 voxels of run.</summary>
        public int MaxSlope;

        /// <summary>Altitude used when <see cref="BasePlane"/> is <see cref="BasePlaneRule.FixedAltitude"/>.</summary>
        public int FixedAltitude;

        /// <summary>Higher wins contested space. Ties break on instance id, so the order is total.</summary>
        public int Precedence;

        // Ranges into the catalogue's shared pools.
        public int ParameterOffset, ParameterCount;
        public int AnchorOffset, AnchorCount;
        public int SlotOffset, SlotCount;
        public int ProgramOffset, ProgramLength;
        public int MaterialOffset, MaterialCount;

        /// <summary>
        /// Upper bound on primitives this definition can emit, proved by validation from the
        /// program's structure. Generation trusts it; exceeding it is a validation defect rather
        /// than a runtime condition.
        /// </summary>
        public int MaxPrimitives;

        /// <summary>True when the footprint fits inside the budget's hard ceiling.</summary>
        public bool FootprintWithinBudget =>
            Footprint.x > 0 && Footprint.y > 0 && Footprint.z > 0
            && Footprint.x <= FeatureBudget.MaxFootprintVoxels
            && Footprint.y <= FeatureBudget.MaxFootprintVoxels
            && Footprint.z <= FeatureBudget.MaxFootprintVoxels;
    }
}
