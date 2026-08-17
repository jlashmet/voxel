using Unity.Collections;
using Unity.Mathematics;

namespace Game.Structures.Api
{
    public enum CastleCurtainLayoutKind : byte
    {
        Rectangle = 0,
        RectilinearPolygon = 1,
    }

    /// <summary>
    /// Castle-specific perimeter policy layered over shared wall-run components. Polygon vertices
    /// are definition-local X/Z coordinates and must form a closed rectilinear perimeter; the
    /// implicit final edge connects the last vertex back to the first. Fixed storage keeps the
    /// layout bounded and Burst-compatible.
    /// </summary>
    public struct CastleCurtainLayoutConfig
    {
        public CastleCurtainLayoutKind Kind;

        /// <summary>
        /// Optional maximum wall-run chunk length. Zero authors each perimeter edge as one run;
        /// positive values deterministically split long edges without changing their footprint.
        /// </summary>
        public int SegmentLength;

        public FixedList128Bytes<int2> PolygonVertices;

        public bool IsWellFormed
        {
            get
            {
                if (SegmentLength < 0) return false;
                if (Kind == CastleCurtainLayoutKind.Rectangle)
                    return PolygonVertices.Length == 0;
                if (Kind != CastleCurtainLayoutKind.RectilinearPolygon ||
                    PolygonVertices.Length < 4)
                    return false;

                for (int i = 0; i < PolygonVertices.Length; i++)
                {
                    int2 a = PolygonVertices[i];
                    int2 b = PolygonVertices[(i + 1) % PolygonVertices.Length];
                    int dx = b.x - a.x;
                    int dz = b.y - a.y;
                    if ((dx == 0) == (dz == 0))
                        return false;
                }

                return true;
            }
        }
    }
}
