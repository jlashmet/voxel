using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CaveEntranceMode : byte
    {
        Surface = 0,
        StructureAttached = 1,
        Underground = 2,
    }

    /// <summary>One deterministic entry into the shared cave generator.</summary>
    public struct CaveEntranceConfig
    {
        public CaveEntranceMode Mode;
        public int3 LocalPosition;
        public Facing Facing;
        public int Width;
        public int Height;
        public int ClearanceLength;

        public bool IsWellFormed =>
            (Mode == CaveEntranceMode.Surface ||
             Mode == CaveEntranceMode.StructureAttached ||
             Mode == CaveEntranceMode.Underground) &&
            (Facing == Facing.North || Facing == Facing.East ||
             Facing == Facing.South || Facing == Facing.West) &&
            Width >= 3 && Height >= 3 && ClearanceLength >= 1;
    }

    /// <summary>
    /// Opaque material ids selected by game content for generic cave semantics. The cave generator
    /// knows only meaning; it never references game material constants.
    /// </summary>
    public struct CaveMaterialPalette
    {
        public byte Opening;
        public byte Rock;
        public byte Accent;
        public byte Decoration;
        public byte Water;
    }

    /// <summary>
    /// Reusable deterministic cave-network controls. Every limit is explicit so authoring remains
    /// bounded; the runtime uses integer hashing and integer geometry only.
    /// </summary>
    public struct CaveConfig
    {
        public int TunnelWidth;
        public int TunnelHeight;
        public int SegmentLength;
        public int MainSegmentCount;

        /// <summary>0..100 chance to turn left/right after each segment.</summary>
        public int TurnChancePercent;

        /// <summary>0..100 chance that a segment changes elevation.</summary>
        public int VerticalChancePercent;
        public int MaxVerticalStepPerSegment;

        public int BranchChancePercent;
        public int MaxBranches;
        public int MaxBranchDepth;
        public int BranchSegmentCount;
        public int MinBranchSeparation;

        public int ChamberChancePercent;
        public int MinChamberRadius;
        public int MaxChamberRadius;
        public int MinChamberHeight;
        public int MaxChamberHeight;

        /// <summary>
        /// Roughness expands the guaranteed tunnel core; it never shrinks that core, preserving
        /// required connectivity.
        /// </summary>
        public int FloorRoughness;
        public int CeilingRoughness;
        public int WallRoughness;

        /// <summary>Hard local-space authoring envelope around the request origin.</summary>
        public int3 BoundsHalfExtents;

        /// <summary>Allowed centreline vertical offsets from request origin, inclusive.</summary>
        public int MinVerticalOffset;
        public int MaxVerticalOffset;

        /// <summary>
        /// Loop/reconnection authoring is intentionally unsupported until the region-local portal
        /// contract can prove both sides make the same bounded choice. Validation rejects true.
        /// </summary>
        public bool EnableLoops;

        public bool IsWellFormed
        {
            get
            {
                if (TunnelWidth < 3 || TunnelHeight < 3 || SegmentLength < 2 ||
                    MainSegmentCount < 1 || MainSegmentCount > 512)
                    return false;

                if (!Percent(TurnChancePercent) || !Percent(VerticalChancePercent) ||
                    !Percent(BranchChancePercent) || !Percent(ChamberChancePercent))
                    return false;

                if (MaxVerticalStepPerSegment < 0 || MaxVerticalStepPerSegment > SegmentLength ||
                    MaxBranches < 0 || MaxBranches > 32 ||
                    MaxBranchDepth < 0 || MaxBranchDepth > 8 ||
                    BranchSegmentCount < 1 || BranchSegmentCount > 256 ||
                    MinBranchSeparation < 0)
                    return false;

                if (MinChamberRadius < 2 || MaxChamberRadius < MinChamberRadius ||
                    MinChamberHeight < 3 || MaxChamberHeight < MinChamberHeight)
                    return false;

                if (FloorRoughness < 0 || CeilingRoughness < 0 || WallRoughness < 0)
                    return false;

                if (BoundsHalfExtents.x <= TunnelWidth ||
                    BoundsHalfExtents.y <= TunnelHeight ||
                    BoundsHalfExtents.z <= TunnelWidth)
                    return false;

                if (MinVerticalOffset > MaxVerticalOffset ||
                    MinVerticalOffset < -BoundsHalfExtents.y ||
                    MaxVerticalOffset > BoundsHalfExtents.y)
                    return false;

                // WB059: deliberately rejected until deterministic region-local reconnection exists.
                return !EnableLoops;
            }
        }

        private static bool Percent(int value) => value >= 0 && value <= 100;

        public static CaveConfig Default => new CaveConfig
        {
            TunnelWidth = 11,
            TunnelHeight = 13,
            SegmentLength = 18,
            MainSegmentCount = 18,
            TurnChancePercent = 34,
            VerticalChancePercent = 32,
            MaxVerticalStepPerSegment = 4,
            BranchChancePercent = 22,
            MaxBranches = 6,
            MaxBranchDepth = 2,
            BranchSegmentCount = 6,
            MinBranchSeparation = 24,
            ChamberChancePercent = 28,
            MinChamberRadius = 10,
            MaxChamberRadius = 24,
            MinChamberHeight = 10,
            MaxChamberHeight = 24,
            FloorRoughness = 2,
            CeilingRoughness = 3,
            WallRoughness = 2,
            BoundsHalfExtents = new int3(320, 120, 320),
            MinVerticalOffset = -96,
            MaxVerticalOffset = 24,
            EnableLoops = false,
        };
    }

    /// <summary>
    /// Instance-level cave request. Standalone and structure-attached factories deliberately produce
    /// the same data shape and are consumed by the same runtime authorer.
    /// </summary>
    public struct CaveGenerationRequest
    {
        public ulong Seed;
        public int3 Origin;
        public CaveEntranceConfig Entrance;

        public bool IsWellFormed => Seed != 0 && Entrance.IsWellFormed;
        public int3 EntranceWorldPosition => Origin + Entrance.LocalPosition;

        public static CaveGenerationRequest Standalone(
            ulong seed,
            int3 surfaceAnchor,
            Facing facing,
            int width,
            int height,
            int clearanceLength) => new CaveGenerationRequest
        {
            Seed = seed,
            Origin = surfaceAnchor,
            Entrance = new CaveEntranceConfig
            {
                Mode = CaveEntranceMode.Surface,
                LocalPosition = int3.zero,
                Facing = facing,
                Width = width,
                Height = height,
                ClearanceLength = clearanceLength,
            },
        };

        public static CaveGenerationRequest Attached(
            ulong seed,
            int3 structureAnchor,
            Facing facing,
            int width,
            int height,
            int clearanceLength) => new CaveGenerationRequest
        {
            Seed = seed,
            Origin = structureAnchor,
            Entrance = new CaveEntranceConfig
            {
                Mode = CaveEntranceMode.StructureAttached,
                LocalPosition = int3.zero,
                Facing = facing,
                Width = width,
                Height = height,
                ClearanceLength = clearanceLength,
            },
        };

        public static CaveGenerationRequest Underground(
            ulong seed,
            int3 undergroundAnchor,
            Facing facing,
            int width,
            int height,
            int clearanceLength) => new CaveGenerationRequest
        {
            Seed = seed,
            Origin = undergroundAnchor,
            Entrance = new CaveEntranceConfig
            {
                Mode = CaveEntranceMode.Underground,
                LocalPosition = int3.zero,
                Facing = facing,
                Width = width,
                Height = height,
                ClearanceLength = clearanceLength,
            },
        };
    }
}
