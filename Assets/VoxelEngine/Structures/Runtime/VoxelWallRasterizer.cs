using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Rasterizes a thick wall segment in the X/Z plane into vertical voxel columns.
    ///
    /// This is a realization primitive, not planning policy: callers choose the segment endpoints,
    /// height, thickness, and material. The capsule footprint deliberately overlaps at shared
    /// endpoints so independently realized perimeter edges seal without corner cracks.
    /// </summary>
    public static class VoxelWallRasterizer
    {
        public static void FillSegment(
            ref VoxelBrush brush,
            int2 start,
            int2 end,
            int baseY,
            int height,
            int thickness,
            byte material)
        {
            if (height <= 0 || thickness <= 0)
                return;

            CastleSegmentFootprint.Bounds(start, end, thickness, out int2 min, out int2 max);
            int maxYExclusive = baseY + height;

            for (int z = min.y; z <= max.y; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                var point = new int2(x, z);
                if (!CastleSegmentFootprint.Contains(point, start, end, thickness))
                    continue;

                brush.FillColumnBulk(x, baseY, maxYExclusive, z, material);
            }
        }
    }
}
