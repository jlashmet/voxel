using Unity.Mathematics;

namespace VoxelEngine.Structures
{
    /// <summary>
    /// Art-oriented voxel primitives used to explore the Mounting Force world vocabulary.
    ///
    /// These deliberately build on VoxelBrush so the result is ordinary brickmap content:
    /// destructible, meshable, and material-addressable exactly like terrain. They are lookdev
    /// helpers first; once a form proves useful it can be promoted into the deterministic
    /// ShapeProgram/rasterizer path.
    /// </summary>
    public static class WorldArtPrimitives
    {
        /// <summary>Filled ellipsoid. Useful for mounds, boulders and broad organic masses.</summary>
        public static void Ellipsoid(ref VoxelBrush brush, int3 centre, int3 radii, byte material)
        {
            if (math.any(radii <= 0)) return;

            double rx2 = (double)radii.x * radii.x;
            double rz2 = (double)radii.z * radii.z;

            for (int z = -radii.z; z <= radii.z; z++)
            for (int x = -radii.x; x <= radii.x; x++)
            {
                double horizontal = x * x / rx2 + z * z / rz2;
                if (horizontal > 1.0) continue;

                int halfY = (int)math.floor(radii.y * math.sqrt((float)(1.0 - horizontal)));
                brush.FillColumnBulk(centre.x + x,
                                     centre.y - halfY,
                                     centre.y + halfY + 1,
                                     centre.z + z,
                                     material);
            }
        }

        /// <summary>
        /// Circular frustum aligned to Y. bottomRadius == topRadius is a cylinder; a small top
        /// radius gives the gentle taper that makes columns, trunks and towers feel illustrated.
        /// </summary>
        public static void Frustum(ref VoxelBrush brush, int cx, int baseY, int cz,
                                   int bottomRadius, int topRadius, int height, byte material)
        {
            if (height <= 0 || bottomRadius < 0 || topRadius < 0) return;

            int denominator = math.max(1, height - 1);
            for (int y = 0; y < height; y++)
            {
                int radius = bottomRadius + ((topRadius - bottomRadius) * y) / denominator;
                int r2 = radius * radius;

                for (int z = -radius; z <= radius; z++)
                for (int x = -radius; x <= radius; x++)
                    if (x * x + z * z <= r2)
                        brush.Set(cx + x, baseY + y, cz + z, material);
            }
        }

        /// <summary>
        /// Capsule between arbitrary voxel points. This is the useful organic connector: roots,
        /// fallen branches, curved-looking rock bridges when chained, and thick vines.
        /// </summary>
        public static void Capsule(ref VoxelBrush brush, int3 a, int3 b, int radius, byte material)
        {
            if (radius <= 0) return;

            int3 delta = b - a;
            int steps = math.max(math.abs(delta.x), math.max(math.abs(delta.y), math.abs(delta.z)));
            if (steps == 0)
            {
                Sphere(ref brush, a, radius, material);
                return;
            }

            int3 previous = new int3(int.MinValue);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int3 p = new int3(
                    (int)math.round(math.lerp(a.x, b.x, t)),
                    (int)math.round(math.lerp(a.y, b.y, t)),
                    (int)math.round(math.lerp(a.z, b.z, t)));
                if (math.all(p == previous)) continue;
                Sphere(ref brush, p, radius, material);
                previous = p;
            }
        }

        /// <summary>Rounded rectangular mass. Useful for softened masonry and giant paving blocks.</summary>
        public static void RoundedBox(ref VoxelBrush brush, int3 min, int3 size, int radius, byte material)
        {
            if (math.any(size <= 0)) return;
            radius = math.clamp(radius, 0, math.min(size.x, math.min(size.y, size.z)) / 2);
            if (radius == 0)
            {
                brush.Box(min, size, material);
                return;
            }

            float3 half = (new float3(size) - 1f) * 0.5f;
            float3 centre = new float3(min) + half;
            float3 core = math.max(float3.zero, half - radius);
            float r2 = radius * radius + 0.25f;

            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                float3 q = math.max(math.abs(new float3(min.x + x, min.y + y, min.z + z) - centre) - core,
                                    float3.zero);
                if (math.lengthsq(q) <= r2)
                    brush.Set(min.x + x, min.y + y, min.z + z, material);
            }
        }

        /// <summary>
        /// Replaces the highest occupied voxel in each X/Z column with a coating material.
        /// This is a first-pass art rule for turf, moss, snow and dust rather than a new voxel type.
        /// </summary>
        public static void CoatExposedTops(ref VoxelBrush brush, int3 min, int3 size,
                                           byte coatingMaterial, int maxDepth = 1)
        {
            if (math.any(size <= 0) || maxDepth <= 0) return;

            int maxY = min.y + size.y - 1;
            for (int z = min.z; z < min.z + size.z; z++)
            for (int x = min.x; x < min.x + size.x; x++)
            {
                for (int y = maxY; y >= min.y; y--)
                {
                    if (!brush.IsSolid(x, y, z)) continue;
                    for (int d = 0; d < maxDepth && y - d >= min.y; d++)
                    {
                        if (!brush.IsSolid(x, y - d, z)) break;
                        brush.Set(x, y - d, z, coatingMaterial);
                    }
                    break;
                }
            }
        }

        public static void Sphere(ref VoxelBrush brush, int3 centre, int radius, byte material)
        {
            if (radius <= 0) return;
            int r2 = radius * radius;

            for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
            {
                int horizontal = x * x + z * z;
                if (horizontal > r2) continue;
                int halfY = (int)math.floor(math.sqrt(r2 - horizontal));
                brush.FillColumnBulk(centre.x + x,
                                     centre.y - halfY,
                                     centre.y + halfY + 1,
                                     centre.z + z,
                                     material);
            }
        }
    }
}
