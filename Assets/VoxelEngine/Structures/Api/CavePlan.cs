using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public struct CaveChamberPlan
    {
        public int Id;
        public int3 Centre;
        public int3 Radii;
        public float RotationRadians;
    }

    public struct CavePassagePlan
    {
        public int FromChamberId;
        public int ToChamberId;
        public int Width;
        public int Height;
    }

    /// <summary>
    /// Scale/topology constraints for a natural cave. Coordinates are owned by the caller; the
    /// planner has no castle, terrain, material, or voxel-storage dependency.
    /// </summary>
    public struct CavePlanningConstraints
    {
        public int3 Entrance;
        public int3 EntranceToMainOffset;
        public int3 MainRadii;
        public int SecondaryChamberCount;
        public int3 SecondaryMinRadii;
        public int3 SecondaryMaxRadii;
        public int MinimumHorizontalSpread;
        public int MaximumHorizontalSpread;
        public int VerticalSpread;
        public int PassageWidth;
        public int PassageHeight;
    }

    /// <summary>
    /// Pure natural-cave topology. Chambers and passages describe designed connectivity only;
    /// water, formations, crystals, vegetation, and materials remain realization/decor concerns.
    /// </summary>
    public sealed class CavePlan
    {
        public uint Seed { get; }
        public int3 Entrance { get; }
        public CaveChamberPlan[] Chambers { get; }
        public CavePassagePlan[] Passages { get; }
        public int EntryChamberId { get; }

        internal CavePlan(
            uint seed,
            int3 entrance,
            CaveChamberPlan[] chambers,
            CavePassagePlan[] passages,
            int entryChamberId)
        {
            Seed = seed;
            Entrance = entrance;
            Chambers = chambers;
            Passages = passages;
            EntryChamberId = entryChamberId;
        }

        /// <summary>Detached copy for incremental-realization trust boundaries.</summary>
        public CavePlan Snapshot() => new CavePlan(
            Seed,
            Entrance,
            Chambers != null ? (CaveChamberPlan[])Chambers.Clone() : null,
            Passages != null ? (CavePassagePlan[])Passages.Clone() : null,
            EntryChamberId);
    }
}
