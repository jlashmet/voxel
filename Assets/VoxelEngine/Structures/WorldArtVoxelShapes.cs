using Unity.Mathematics;

namespace VoxelEngine.Structures
{
    /// <summary>
    /// Reusable destructible counterparts to the presentation-side WorldArtKit pieces. These
    /// routines only write ordinary VoxelBrush data, so ruins, cliffs, roots and architecture
    /// remain editable/destructible and can be meshed by the normal voxel surface pipeline.
    /// </summary>
    public static class WorldArtVoxelShapes
    {
        public static void Ashlar(ref VoxelBrush brush, int3 min, int3 size, int bevel, byte material)
        {
            WorldArtPrimitives.RoundedBox(ref brush, min, size, bevel, material);
        }

        public static void Slab(ref VoxelBrush brush, int3 min, int3 size, byte material)
        {
            WorldArtPrimitives.RoundedBox(ref brush, min, size, math.max(1, math.min(size.y / 3, 2)), material);
        }

        public static void PathStone(ref VoxelBrush brush, int3 centre, int3 radii, byte material)
        {
            int3 r = new int3(math.max(1, radii.x), math.max(1, radii.y), math.max(1, radii.z));
            WorldArtPrimitives.Ellipsoid(ref brush, centre, r, material);
        }

        public static void Boulder(ref VoxelBrush brush, int3 centre, int3 radii, byte material)
        {
            WorldArtPrimitives.Ellipsoid(ref brush, centre, radii, material);
        }

        public static void Pillar(ref VoxelBrush brush, int3 baseCentre, int radius, int height, byte material)
        {
            WorldArtPrimitives.Frustum(ref brush, baseCentre.x, baseCentre.y, baseCentre.z,
                radius, math.max(1, radius - 1), height, material);
        }

        public static void ColumnWithCapital(ref VoxelBrush brush, int3 baseCentre, int radius,
            int height, byte shaftMaterial, byte capitalMaterial)
        {
            Pillar(ref brush, baseCentre, radius, height, shaftMaterial);
            int capRadius = radius + math.max(1, radius / 2);
            int3 capMin = new int3(baseCentre.x - capRadius, baseCentre.y + height - 1, baseCentre.z - capRadius);
            Slab(ref brush, capMin, new int3(capRadius * 2 + 1, math.max(2, radius), capRadius * 2 + 1), capitalMaterial);
        }

        public static void Spire(ref VoxelBrush brush, int3 baseCentre, int radius, int height, byte material)
        {
            WorldArtPrimitives.Frustum(ref brush, baseCentre.x, baseCentre.y, baseCentre.z,
                math.max(1, radius), 0, math.max(1, height), material);
        }

        public static void WedgeX(ref VoxelBrush brush, int3 min, int3 size, bool risePositiveX, byte material)
        {
            if (math.any(size <= 0)) return;
            int denominator = math.max(1, size.x - 1);
            for (int x = 0; x < size.x; x++)
            {
                int u = risePositiveX ? x : size.x - 1 - x;
                int height = 1 + (u * (size.y - 1)) / denominator;
                for (int z = 0; z < size.z; z++)
                    brush.FillColumnBulk(min.x + x, min.y, min.y + height, min.z + z, material);
            }
        }

        public static void WedgeZ(ref VoxelBrush brush, int3 min, int3 size, bool risePositiveZ, byte material)
        {
            if (math.any(size <= 0)) return;
            int denominator = math.max(1, size.z - 1);
            for (int z = 0; z < size.z; z++)
            {
                int u = risePositiveZ ? z : size.z - 1 - z;
                int height = 1 + (u * (size.y - 1)) / denominator;
                for (int x = 0; x < size.x; x++)
                    brush.FillColumnBulk(min.x + x, min.y, min.y + height, min.z + z, material);
            }
        }

        public static void Buttress(ref VoxelBrush brush, int3 wallFoot, int width, int height,
            int depth, bool projectsPositiveZ, byte material)
        {
            int3 min = projectsPositiveZ
                ? wallFoot
                : new int3(wallFoot.x, wallFoot.y, wallFoot.z - depth + 1);
            WedgeZ(ref brush, min, new int3(width, height, depth), !projectsPositiveZ, material);
        }

        public static void StairRun(ref VoxelBrush brush, int3 min, int width, int steps,
            int treadDepth, int rise, bool positiveZ, byte material)
        {
            if (width <= 0 || steps <= 0 || treadDepth <= 0 || rise <= 0) return;
            for (int i = 0; i < steps; i++)
            {
                int z = positiveZ ? min.z + i * treadDepth : min.z - (i + 1) * treadDepth + 1;
                int3 stepMin = new int3(min.x, min.y, z);
                int3 size = new int3(width, (i + 1) * rise, treadDepth);
                brush.Box(stepMin, size, material);
            }
        }

        public static void Arch(ref VoxelBrush brush, int3 centre, int halfOpening, int pierHeight,
            int thickness, int depth, byte material)
        {
            if (halfOpening <= 0 || pierHeight <= 0 || thickness <= 0 || depth <= 0) return;

            int outerRadius = halfOpening + thickness;
            int innerRadius = halfOpening - 1;
            int z0 = centre.z - depth / 2;
            int z1 = z0 + depth;

            // Two straight piers.
            for (int z = z0; z < z1; z++)
            for (int y = 0; y < pierHeight; y++)
            for (int x = -outerRadius; x <= outerRadius; x++)
            {
                bool left = x >= -outerRadius && x <= -halfOpening;
                bool right = x >= halfOpening && x <= outerRadius;
                if (left || right) brush.Set(centre.x + x, centre.y + y, z, material);
            }

            // Semicircular ring above the piers.
            int cy = centre.y + pierHeight - 1;
            int outer2 = outerRadius * outerRadius;
            int inner2 = math.max(0, innerRadius * innerRadius);
            for (int z = z0; z < z1; z++)
            for (int y = 0; y <= outerRadius; y++)
            for (int x = -outerRadius; x <= outerRadius; x++)
            {
                int d2 = x * x + y * y;
                if (d2 <= outer2 && d2 >= inner2)
                    brush.Set(centre.x + x, cy + y, z, material);
            }
        }

        public static void BrokenArch(ref VoxelBrush brush, int3 centre, int halfOpening, int pierHeight,
            int thickness, int depth, byte material)
        {
            Arch(ref brush, centre, halfOpening, pierHeight, thickness, depth, material);

            // Add an offset broken crown and one surviving shoulder. Destruction can remove more at runtime;
            // this shape starts with the asymmetry used by the storybook ruin language.
            int3 crown = new int3(centre.x - halfOpening - thickness / 2,
                centre.y + pierHeight + halfOpening, centre.z - depth / 2);
            WorldArtPrimitives.RoundedBox(ref brush, crown,
                new int3(math.max(2, thickness + 1), math.max(2, thickness), depth), 1, material);
        }

        public static void RuinWall(ref VoxelBrush brush, int3 min, int blocksX, int rows,
            int3 blockSize, int gap, byte material, uint seed)
        {
            if (blocksX <= 0 || rows <= 0 || math.any(blockSize <= 0)) return;
            uint state = seed == 0 ? 1u : seed;
            for (int row = 0; row < rows; row++)
            for (int bx = 0; bx < blocksX; bx++)
            {
                state = Hash(state + (uint)(row * 131 + bx * 17));
                // Sparse deterministic omissions make a ruin without introducing non-deterministic RNG.
                if (row > 0 && (state & 15u) == 0u) continue;

                int offsetX = (row & 1) == 0 ? 0 : blockSize.x / 2;
                int3 p = new int3(
                    min.x + bx * (blockSize.x + gap) + offsetX,
                    min.y + row * (blockSize.y + gap),
                    min.z);
                WorldArtPrimitives.RoundedBox(ref brush, p, blockSize, math.max(1, blockSize.y / 5), material);
            }
        }

        public static void Terrace(ref VoxelBrush brush, int3 centre, int3 lowerRadii,
            int3 upperRadii, int upperYOffset, byte rockMaterial, byte turfMaterial)
        {
            WorldArtPrimitives.Ellipsoid(ref brush, centre, lowerRadii, rockMaterial);
            WorldArtPrimitives.Ellipsoid(ref brush, centre + new int3(0, upperYOffset, 0), upperRadii, rockMaterial);

            int3 min = centre - lowerRadii;
            int3 size = lowerRadii * 2 + 1;
            WorldArtPrimitives.CoatExposedTops(ref brush, min, size, turfMaterial, 1);
        }

        public static void CliffShelf(ref VoxelBrush brush, int3 centre, int3 radii,
            byte rockMaterial, byte turfMaterial)
        {
            WorldArtPrimitives.Ellipsoid(ref brush, centre, radii, rockMaterial);
            int3 min = centre - radii;
            WorldArtPrimitives.CoatExposedTops(ref brush, min, radii * 2 + 1, turfMaterial, 1);
        }

        public static void FloatingIsland(ref VoxelBrush brush, int3 centre, int3 radii,
            byte rockMaterial, byte turfMaterial)
        {
            WorldArtPrimitives.Ellipsoid(ref brush, centre, radii, rockMaterial);
            // A tapered underside keeps the silhouette pointed instead of reading as a floating ball.
            int coneHeight = math.max(2, radii.y);
            for (int y = 0; y < coneHeight; y++)
            {
                float t = y / (float)coneHeight;
                int rx = math.max(1, (int)math.round(radii.x * (1f - t)));
                int rz = math.max(1, (int)math.round(radii.z * (1f - t)));
                int yy = centre.y - radii.y - y;
                for (int z = -rz; z <= rz; z++)
                for (int x = -rx; x <= rx; x++)
                    if ((x * x) / (float)(rx * rx) + (z * z) / (float)(rz * rz) <= 1f)
                        brush.Set(centre.x + x, yy, centre.z + z, rockMaterial);
            }
            WorldArtPrimitives.CoatExposedTops(ref brush, centre - radii, radii * 2 + 1, turfMaterial, 1);
        }

        public static void Root(ref VoxelBrush brush, int3 start, int3 end, int radius, byte material)
        {
            WorldArtPrimitives.Capsule(ref brush, start, end, radius, material);
        }

        public static void Trunk(ref VoxelBrush brush, int3 basePoint, int3 topPoint,
            int radius, byte material)
        {
            WorldArtPrimitives.Capsule(ref brush, basePoint, topPoint, radius, material);
        }

        public static void Canopy(ref VoxelBrush brush, int3 centre, int3 radii,
            byte leafMaterial, uint seed)
        {
            WorldArtPrimitives.Ellipsoid(ref brush, centre, radii, leafMaterial);
            uint state = seed == 0 ? 1u : seed;
            for (int i = 0; i < 5; i++)
            {
                state = Hash(state + (uint)(i * 97 + 3));
                int ox = (int)(state % (uint)(radii.x * 2 + 1)) - radii.x;
                state = Hash(state + 11u);
                int oz = (int)(state % (uint)(radii.z * 2 + 1)) - radii.z;
                int oy = (i & 1) == 0 ? radii.y / 3 : -radii.y / 5;
                int3 puffRadii = new int3(math.max(1, radii.x / 2), math.max(1, radii.y / 2), math.max(1, radii.z / 2));
                WorldArtPrimitives.Ellipsoid(ref brush, centre + new int3(ox / 2, oy, oz / 2), puffRadii, leafMaterial);
            }
        }

        public static void RootFan(ref VoxelBrush brush, int3 trunkBase, int radius,
            int length, byte material)
        {
            int rootRadius = math.max(1, radius / 2);
            int3[] directions =
            {
                new int3(1, -1, 0), new int3(-1, -1, 0),
                new int3(0, -1, 1), new int3(0, -1, -1),
                new int3(1, -1, 1), new int3(-1, -1, -1)
            };
            for (int i = 0; i < directions.Length; i++)
            {
                int3 d = directions[i];
                int3 end = trunkBase + new int3(d.x * length, d.y * math.max(1, length / 4), d.z * length);
                WorldArtPrimitives.Capsule(ref brush, trunkBase, end, rootRadius, material);
            }
        }

        public static void MossCap(ref VoxelBrush brush, int3 min, int3 size,
            byte mossMaterial, int depth = 1)
        {
            WorldArtPrimitives.CoatExposedTops(ref brush, min, size, mossMaterial, math.max(1, depth));
        }

        public static void GardenSteps(ref VoxelBrush brush, int3 min, int width, int count,
            int treadDepth, int rise, byte stoneMaterial, byte mossMaterial)
        {
            StairRun(ref brush, min, width, count, treadDepth, rise, true, stoneMaterial);
            int3 totalSize = new int3(width, count * rise, count * treadDepth);
            WorldArtPrimitives.CoatExposedTops(ref brush, min, totalSize, mossMaterial, 1);
        }

        private static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }
    }
}
