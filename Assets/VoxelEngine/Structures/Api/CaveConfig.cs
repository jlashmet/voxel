using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CaveEntranceMode : byte { Surface = 0, StructureAttached = 1, Underground = 2 }
    public enum CaveChamberShape : byte { Round = 0, Box = 1 }

    public struct CaveEntranceConfig
    {
        public CaveEntranceMode Mode;
        public int3 LocalPosition;
        public Facing Facing;
        public int Width;
        public int Height;
        public int ClearanceLength;
        public bool IsWellFormed =>
            (Mode == CaveEntranceMode.Surface || Mode == CaveEntranceMode.StructureAttached || Mode == CaveEntranceMode.Underground) &&
            (Facing == Facing.North || Facing == Facing.East || Facing == Facing.South || Facing == Facing.West) &&
            Width >= 3 && Height >= 3 && ClearanceLength >= 1;
    }

    public struct CaveMaterialPalette
    {
        public byte Opening;
        public byte Rock;
        public byte Accent;
        public byte Decoration;
        public byte Water;
    }

    public struct CaveConfig
    {
        public int TunnelWidth, TunnelHeight, SegmentLength, MainSegmentCount;
        public int TurnChancePercent;
        public int VerticalChancePercent, MaxVerticalStepPerSegment;
        public int SurfaceDescentSegments, SurfaceDescentPerSegment, MinimumSurfaceCover;
        public int BranchChancePercent, MaxBranches, MaxBranchDepth, BranchSegmentCount, MinBranchSeparation;
        public int ChamberChancePercent;
        public CaveChamberShape ChamberShape;
        public int MinChamberRadius, MaxChamberRadius, MinChamberHeight, MaxChamberHeight;
        public int FloorRoughness, CeilingRoughness, WallRoughness;
        public int3 BoundsHalfExtents;
        public int MinVerticalOffset, MaxVerticalOffset;
        public bool EnableLoops;

        public bool IsWellFormed
        {
            get
            {
                if (TunnelWidth < 3 || TunnelHeight < 3 || SegmentLength < 2 || MainSegmentCount < 1 || MainSegmentCount > 512) return false;
                if (!Percent(TurnChancePercent) || !Percent(VerticalChancePercent) || !Percent(BranchChancePercent) || !Percent(ChamberChancePercent)) return false;
                if (MaxVerticalStepPerSegment < 0 || MaxVerticalStepPerSegment > SegmentLength ||
                    SurfaceDescentSegments < 0 || SurfaceDescentSegments > MainSegmentCount ||
                    SurfaceDescentPerSegment < 0 || SurfaceDescentPerSegment > SegmentLength || MinimumSurfaceCover < 0 ||
                    MaxBranches < 0 || MaxBranches > 32 || MaxBranchDepth < 0 || MaxBranchDepth > 8 ||
                    BranchSegmentCount < 1 || BranchSegmentCount > 256 || MinBranchSeparation < 0) return false;
                if (ChamberShape != CaveChamberShape.Round && ChamberShape != CaveChamberShape.Box) return false;
                if (MinChamberRadius < 2 || MaxChamberRadius < MinChamberRadius || MinChamberHeight < 3 || MaxChamberHeight < MinChamberHeight) return false;
                if (FloorRoughness < 0 || CeilingRoughness < 0 || WallRoughness < 0) return false;
                if (BoundsHalfExtents.x <= TunnelWidth || BoundsHalfExtents.y <= TunnelHeight || BoundsHalfExtents.z <= TunnelWidth ||
                    MaxChamberRadius + WallRoughness >= BoundsHalfExtents.x || MaxChamberRadius + WallRoughness >= BoundsHalfExtents.z) return false;
                if (MinVerticalOffset > MaxVerticalOffset || MinVerticalOffset < -BoundsHalfExtents.y || MaxVerticalOffset > BoundsHalfExtents.y) return false;
                // WB059: loops remain deliberately unsupported until a deterministic region-local reconnection contract exists.
                return !EnableLoops;
            }
        }

        private static bool Percent(int value) => value >= 0 && value <= 100;

        /// <summary>
        /// Walkable defaults.
        ///
        /// A voxel is 10 cm and the character is 1.8 m tall and 0.6 m across, so the previous
        /// 11x13 tunnel was 1.1 m wide and 1.3 m high: a crawlspace no player could enter, in every
        /// cave in the project including the castle dungeon. Tunnels are now 2.4 m wide and 2.6 m
        /// high, and the smallest chamber is tall enough to stand up in, which is the least a space
        /// has to be to be worth generating.
        /// </summary>
        public static CaveConfig Default => new CaveConfig
        {
            TunnelWidth = 24, TunnelHeight = 26, SegmentLength = 18, MainSegmentCount = 18,
            TurnChancePercent = 34, VerticalChancePercent = 32, MaxVerticalStepPerSegment = 4,
            SurfaceDescentSegments = 5, SurfaceDescentPerSegment = 4, MinimumSurfaceCover = 12,
            BranchChancePercent = 22, MaxBranches = 6, MaxBranchDepth = 2, BranchSegmentCount = 6, MinBranchSeparation = 24,
            ChamberChancePercent = 28, ChamberShape = CaveChamberShape.Round,
            MinChamberRadius = 16, MaxChamberRadius = 34, MinChamberHeight = 28, MaxChamberHeight = 46,
            FloorRoughness = 2, CeilingRoughness = 3, WallRoughness = 2,
            BoundsHalfExtents = new int3(320, 120, 320), MinVerticalOffset = -96, MaxVerticalOffset = 24,
            EnableLoops = false,
        };
    }

    public struct CaveGenerationRequest
    {
        public ulong Seed;
        public uint TerrainSeed;
        public int3 Origin;
        public CaveEntranceConfig Entrance;
        public bool IsWellFormed => Seed != 0 && Entrance.IsWellFormed;
        public int3 EntranceWorldPosition => Origin + Entrance.LocalPosition;

        public bool TryGetWorldBounds(in CaveConfig config, out StructureGenerationBounds bounds)
        {
            bounds = default;
            if (!config.IsWellFormed) return false;
            long minX = (long)Origin.x - config.BoundsHalfExtents.x, minY = (long)Origin.y - config.BoundsHalfExtents.y, minZ = (long)Origin.z - config.BoundsHalfExtents.z;
            long sizeX = (long)config.BoundsHalfExtents.x * 2 + 1, sizeY = (long)config.BoundsHalfExtents.y * 2 + 1, sizeZ = (long)config.BoundsHalfExtents.z * 2 + 1;
            if (minX < int.MinValue || minY < int.MinValue || minZ < int.MinValue || sizeX > int.MaxValue || sizeY > int.MaxValue || sizeZ > int.MaxValue) return false;
            return StructureGenerationBounds.TryCreate(new int3((int)minX, (int)minY, (int)minZ), new int3((int)sizeX, (int)sizeY, (int)sizeZ), out bounds);
        }

        /// <summary>
        /// Proves the complete entrance clearance carve is inside the cave's declared local bounds.
        /// This runs before any authoring write so an oversized entrance is rejected, never clipped.
        /// </summary>
        public bool EntranceFitsBounds(in CaveConfig config)
        {
            if (!IsWellFormed || !config.IsWellFormed) return false;

            long x0 = Entrance.LocalPosition.x;
            long y0 = Entrance.LocalPosition.y;
            long z0 = Entrance.LocalPosition.z;
            long x1 = x0;
            long z1 = z0;
            switch (Entrance.Facing)
            {
                case Facing.North: z1 += Entrance.ClearanceLength; break;
                case Facing.East: x1 += Entrance.ClearanceLength; break;
                case Facing.South: z1 -= Entrance.ClearanceLength; break;
                case Facing.West: x1 -= Entrance.ClearanceLength; break;
                default: return false;
            }

            // Cross-section authoring is perpendicular to travel. Use the larger symmetric radius as
            // a conservative proof for both odd and even widths.
            long radius = (Entrance.Width + 1L) / 2L;
            long minX = x0 < x1 ? x0 : x1;
            long maxX = x0 > x1 ? x0 : x1;
            long minZ = z0 < z1 ? z0 : z1;
            long maxZ = z0 > z1 ? z0 : z1;
            if (Entrance.Facing == Facing.North || Entrance.Facing == Facing.South)
            {
                minX -= radius;
                maxX += radius;
            }
            else
            {
                minZ -= radius;
                maxZ += radius;
            }

            return minX >= -config.BoundsHalfExtents.x && maxX <= config.BoundsHalfExtents.x &&
                   minZ >= -config.BoundsHalfExtents.z && maxZ <= config.BoundsHalfExtents.z &&
                   y0 >= -config.BoundsHalfExtents.y &&
                   y0 + Entrance.Height <= config.BoundsHalfExtents.y;
        }

        public static CaveGenerationRequest Standalone(ulong seed, uint terrainSeed, int3 surfaceAnchor, Facing facing, int width, int height, int clearanceLength) =>
            Create(seed, terrainSeed, surfaceAnchor, CaveEntranceMode.Surface, facing, width, height, clearanceLength);
        public static CaveGenerationRequest Attached(ulong seed, int3 structureAnchor, Facing facing, int width, int height, int clearanceLength) =>
            Create(seed, 0, structureAnchor, CaveEntranceMode.StructureAttached, facing, width, height, clearanceLength);
        public static CaveGenerationRequest Underground(ulong seed, int3 undergroundAnchor, Facing facing, int width, int height, int clearanceLength) =>
            Create(seed, 0, undergroundAnchor, CaveEntranceMode.Underground, facing, width, height, clearanceLength);

        private static CaveGenerationRequest Create(ulong seed, uint terrainSeed, int3 origin, CaveEntranceMode mode, Facing facing, int width, int height, int clearanceLength) => new CaveGenerationRequest
        {
            Seed = seed, TerrainSeed = terrainSeed, Origin = origin,
            Entrance = new CaveEntranceConfig { Mode = mode, LocalPosition = int3.zero, Facing = facing, Width = width, Height = height, ClearanceLength = clearanceLength },
        };
    }
}
