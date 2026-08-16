using Unity.Mathematics;

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

            float2 a = new float2(start.x, start.y);
            float2 b = new float2(end.x, end.y);
            float2 delta = b - a;
            float lengthSquared = math.lengthsq(delta);
            float radius = math.max(0.5f, thickness * 0.5f);
            float radiusSquared = radius * radius;

            int minX = (int)math.floor(math.min(a.x, b.x) - radius);
            int maxX = (int)math.ceil(math.max(a.x, b.x) + radius);
            int minZ = (int)math.floor(math.min(a.y, b.y) - radius);
            int maxZ = (int)math.ceil(math.max(a.y, b.y) + radius);
            int maxYExclusive = baseY + height;

            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                float2 point = new float2(x, z);
                float along = lengthSquared > 0.0001f
                    ? math.saturate(math.dot(point - a, delta) / lengthSquared)
                    : 0f;
                float2 nearest = a + delta * along;
                if (math.lengthsq(point - nearest) > radiusSquared)
                    continue;

                brush.FillColumnBulk(x, baseY, maxYExclusive, z, material);
            }
        }
    }
}
