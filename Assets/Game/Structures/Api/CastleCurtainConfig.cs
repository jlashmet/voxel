using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>Supported bounded curtain-wall plan families.</summary>
    public enum CastleCurtainLayoutKind : byte
    {
        Rectangular = 0,
        Polygon = 1,
    }

    /// <summary>
    /// Castle-specific curtain composition over the shared wall and battlement contracts.
    /// Rectangles use integer half-extents; polygons use an explicitly bounded local vertex list.
    /// Polygon edges are orthogonal because the existing deterministic wall-run authoring primitive
    /// is axis-aligned. MaximumSegmentLength controls deterministic wall subdivision without changing
    /// the wall style.
    /// </summary>
    public struct CastleCurtainConfig
    {
        public CastleCurtainLayoutKind Layout;
        public int2 RectangularHalfExtents;
        public FixedList512Bytes<int2> PolygonVertices;
        public StructureWallRunConfig Wall;
        public int MaximumSegmentLength;
        public BattlementConfig Battlements;
        public StructureMaterialPalette Palette;

        public int Height => Wall.Height;
        public int Thickness => Wall.Thickness;
        public int PolygonVertexCount => PolygonVertices.Length;

        public bool IsWellFormed
        {
            get
            {
                if (!Wall.IsWellFormed || !Battlements.IsWellFormed || MaximumSegmentLength <= 0)
                    return false;
                if (MaximumSegmentLength < Wall.Thickness)
                    return false;

                switch (Layout)
                {
                    case CastleCurtainLayoutKind.Rectangular:
                        return RectangularHalfExtents.x > Wall.Thickness &&
                               RectangularHalfExtents.y > Wall.Thickness;

                    case CastleCurtainLayoutKind.Polygon:
                        if (PolygonVertices.Length < 4)
                            return false;
                        for (int i = 0; i < PolygonVertices.Length; i++)
                        {
                            int2 a = PolygonVertices[i];
                            int2 b = PolygonVertices[(i + 1) % PolygonVertices.Length];
                            int dx = b.x - a.x;
                            int dz = b.y - a.y;
                            if ((dx == 0 && dz == 0) || (dx != 0 && dz != 0))
                                return false;
                            if (math.abs(dx) + math.abs(dz) <= Wall.Thickness)
                                return false;
                        }
                        return true;

                    default:
                        return false;
                }
            }
        }

        /// <summary>Returns the shared wall style with only its span length specialized.</summary>
        public StructureWallRunConfig WallForSpan(int length)
        {
            StructureWallRunConfig result = Wall;
            result.Length = length;
            return result;
        }

        public StructureWallRunConfig RectangularWallX() =>
            WallForSpan(RectangularHalfExtents.x * 2);

        public StructureWallRunConfig RectangularWallZ() =>
            WallForSpan(RectangularHalfExtents.y * 2);
    }

    public static class CastleCurtainPresets
    {
        /// <summary>
        /// Projects the canonical legacy-compatible castle components into the richer curtain
        /// surface without changing geometry: one segment per historical rectangular wall.
        /// </summary>
        public static CastleCurtainConfig Compatibility(in CastleComponentConfig components)
        {
            int halfX = components.CurtainWallX.Length / 2;
            int halfZ = components.CurtainWallZ.Length / 2;
            StructureWallRunConfig wall = components.CurtainWallX;
            int longestWall = math.max(components.CurtainWallX.Length, components.CurtainWallZ.Length);

            return new CastleCurtainConfig
            {
                Layout = CastleCurtainLayoutKind.Rectangular,
                RectangularHalfExtents = new int2(halfX, halfZ),
                Wall = wall,
                MaximumSegmentLength = longestWall,
                Battlements = components.CurtainBattlements,
                Palette = components.Palette,
            };
        }
    }
}
