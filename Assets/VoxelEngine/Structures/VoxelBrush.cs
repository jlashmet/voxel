using Unity.Mathematics;
using Random = Unity.Mathematics.Random;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Structures
{
    /// <summary>
    /// Drawing primitives for authored structures, writing straight into the brickmap.
    ///
    /// This is offline-scale tooling, not the streaming path: it writes voxel by voxel through
    /// <see cref="VoxelAccess"/> so brick allocation, uniform collapse and dirty tracking behave
    /// exactly as they do for a player's edits. Slow per voxel, but it runs once per castle rather
    /// than once per frame, and correctness of the storage invariants matters more here than speed.
    ///
    /// Float arithmetic is fine in this assembly. The constitution forbids float where *clients
    /// independently re-derive* a result; a structure baked once and shipped as identical bytes is
    /// data, not a computation anyone repeats. What must never happen is this code running
    /// per-client at runtime and being trusted to agree.
    /// </summary>
    public struct VoxelBrush
    {
        private RegionTable _table;
        private BrickPool _pool;

        /// <summary>Per-voxel writes — the expensive kind, and what the budget governs.</summary>
        public int VoxelsWritten;

        /// <summary>Whole-brick writes. One pointer each, so they are counted but not budgeted.</summary>
        public int BricksWritten;

        /// <summary>
        /// Hard ceiling on writes. Once crossed, every further write is dropped and
        /// <see cref="BudgetExceeded"/> latches.
        ///
        /// This exists because a generator with one bad radius took the machine down. The Site
        /// loop was costed afterwards at 38–137 million writes, each triggering a 512-voxel
        /// collapse scan — twenty to seventy billion operations inside play mode. Nothing in the
        /// engine objected, because every individual write was legitimate.
        ///
        /// A budget here is not defensive programming, it is the difference between a bug that
        /// fails and a bug that takes the machine with it. Refusing to write is always
        /// recoverable; running for an hour is not.
        /// </summary>
        public int WriteBudget;

        public bool BudgetExceeded { get; private set; }

        public VoxelBrush(RegionTable table, BrickPool pool, int writeBudget = DefaultWriteBudget)
        {
            _table = table;
            _pool = pool;
            VoxelsWritten = 0;
            BricksWritten = 0;
            WriteBudget = writeBudget;
            BudgetExceeded = false;
        }

        /// <summary>
        /// Twelve million voxels — roughly a 240 m cube of surface, or several minutes of honest
        /// work. Anything above this is a mistake rather than an ambitious structure.
        /// </summary>
        public const int DefaultWriteBudget = 12_000_000;

        public RegionTable Table => _table;
        public BrickPool Pool => _pool;

        // -- primitives ----------------------------------------------------------

        public void Set(int x, int y, int z, byte material)
        {
            if (VoxelsWritten >= WriteBudget)
            {
                BudgetExceeded = true;
                return;
            }

            if (VoxelAccess.SetVoxel(ref _table, ref _pool, new int3(x, y, z), material))
                VoxelsWritten++;
        }

        /// <summary>
        /// Fills a box, writing whole bricks as uniform references where the box covers them.
        ///
        /// A solid volume written voxel by voxel pays a 512-voxel collapse scan on every write
        /// until the brick happens to become uniform — quadratic-feeling work for a result that
        /// is one pointer. Setting the pointer directly is thousands of times cheaper and is what
        /// makes sculpting terrain-scale volumes affordable at all.
        /// </summary>
        public void FillBulk(int3 min, int3 size, byte material)
        {
            int3 max = min + size;

            int3 brickMin = new int3(min.x >> 3, min.y >> 3, min.z >> 3);
            int3 brickMax = new int3((max.x - 1) >> 3, (max.y - 1) >> 3, (max.z - 1) >> 3);

            for (int bz = brickMin.z; bz <= brickMax.z; bz++)
            for (int by = brickMin.y; by <= brickMax.y; by++)
            for (int bx = brickMin.x; bx <= brickMax.x; bx++)
            {
                int3 blockMin = new int3(bx << 3, by << 3, bz << 3);
                int3 blockMax = blockMin + 8;

                bool covered = blockMin.x >= min.x && blockMax.x <= max.x
                            && blockMin.y >= min.y && blockMax.y <= max.y
                            && blockMin.z >= min.z && blockMax.z <= max.z;

                if (covered)
                {
                    SetWholeBrick(blockMin, material);
                    continue;
                }

                int3 overlapMin = math.max(blockMin, min);
                int3 overlapMax = math.min(blockMax, max);

                for (int z = overlapMin.z; z < overlapMax.z; z++)
                for (int y = overlapMin.y; y < overlapMax.y; y++)
                for (int x = overlapMin.x; x < overlapMax.x; x++)
                    Set(x, y, z, material);
            }
        }

        /// <summary>Replaces one brick with a uniform reference, returning any pool slot it held.</summary>
        private void SetWholeBrick(int3 brickOrigin, byte material)
        {
            if (VoxelsWritten >= WriteBudget)
            {
                BudgetExceeded = true;
                return;
            }

            var regionCoord = new int3(
                brickOrigin.x >> VoxelDimensions.RegionVoxelEdgeLog2,
                brickOrigin.y >> VoxelDimensions.RegionVoxelEdgeLog2,
                brickOrigin.z >> VoxelDimensions.RegionVoxelEdgeLog2);

            var region = _table.LoadRegion(regionCoord);

            int bx = (brickOrigin.x >> 3) & VoxelDimensions.RegionEdgeMask;
            int by = (brickOrigin.y >> 3) & VoxelDimensions.RegionEdgeMask;
            int bz = (brickOrigin.z >> 3) & VoxelDimensions.RegionEdgeMask;

            int index = Region.BrickIndex(bx, by, bz);
            var existing = region.BrickRefs[index];

            // Hand back the pool slot, or the brick leaks for the life of the session.
            if (existing.IsMixed) _pool.Free(existing.PoolIndex);

            region.BrickRefs[index] = material == Mat.Empty
                ? BrickRef.Empty
                : BrickRef.Uniform(material);

            region.Dirty = true;
            _table.CommitRegion(region);

            // Counted separately: this is one pointer write, not 512 voxel writes, and charging
            // it as 512 would make the cheap path look expensive and push callers back onto the
            // expensive one.
            BricksWritten++;
        }

        public byte Get(int x, int y, int z) =>
            VoxelAccess.GetVoxel(ref _table, in _pool, new int3(x, y, z));

        public bool IsSolid(int x, int y, int z) => Get(x, y, z) != Mat.Empty;

        public void Box(int3 min, int3 size, byte material)
        {
            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
                Set(min.x + x, min.y + y, min.z + z, material);
        }

        /// <summary>Walls only — the shell of a box, with an open interior.</summary>
        public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling)
        {
            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                bool shell = x < thickness || x >= size.x - thickness
                          || z < thickness || z >= size.z - thickness
                          || (floor && y < thickness)
                          || (ceiling && y >= size.y - thickness);

                if (shell) Set(min.x + x, min.y + y, min.z + z, material);
            }
        }

        /// <summary>Vertical cylinder. <paramref name="innerRadius"/> above zero leaves a shaft.</summary>
        public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material,
                             int innerRadius = 0)
        {
            int r2 = radius * radius;
            int ir2 = innerRadius * innerRadius;

            for (int y = 0; y < height; y++)
            for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
            {
                int d2 = x * x + z * z;
                if (d2 > r2 || (innerRadius > 0 && d2 < ir2)) continue;

                Set(cx + x, baseY + y, cz + z, material);
            }
        }

        /// <summary>Filled disc, one voxel thick. Floors, ceilings, platforms.</summary>
        public void Disc(int cx, int y, int cz, int radius, byte material)
        {
            int r2 = radius * radius;

            for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
                if (x * x + z * z <= r2) Set(cx + x, y, cz + z, material);
        }

        /// <summary>Cone, for tower roofs. Radius shrinks linearly to a point.</summary>
        public void Cone(int cx, int baseY, int cz, int radius, int height, byte material)
        {
            for (int y = 0; y < height; y++)
            {
                float t = 1f - (float)y / height;
                int r = math.max(0, (int)math.round(radius * t));
                int r2 = r * r;
                int inner = math.max(0, r - 2);
                int inner2 = inner * inner;

                for (int z = -r; z <= r; z++)
                for (int x = -r; x <= r; x++)
                {
                    int d2 = x * x + z * z;
                    // Shell only: a solid cone of roof is wasted voxels and hides the interior.
                    if (d2 <= r2 && (d2 >= inner2 || y >= height - 2))
                        Set(cx + x, baseY + y, cz + z, material);
                }
            }
        }

        /// <summary>Gabled roof running along X or Z.</summary>
        public void Gable(int3 min, int3 size, bool alongX, byte material)
        {
            int span = alongX ? size.z : size.x;
            int half = span / 2;

            for (int i = 0; i < span; i++)
            {
                int rise = half - math.abs(i - half);
                int h = math.min(size.y, rise);

                for (int j = 0; j < (alongX ? size.x : size.z); j++)
                for (int y = 0; y <= h; y++)
                {
                    int x = alongX ? j : i;
                    int z = alongX ? i : j;

                    // Shell: only the top two layers, so the loft stays open.
                    if (y >= h - 1 || y == 0)
                        Set(min.x + x, min.y + y, min.z + z, material);
                }
            }
        }

        /// <summary>
        /// Crenellations along a wall run: merlon, gap, merlon.
        ///
        /// The single most recognisable silhouette a castle has. Without it a curtain wall is a
        /// fence.
        /// </summary>
        public void Crenellate(int3 start, int3 step, int count, int width, int height,
                               int merlon, int gap, byte material)
        {
            int period = merlon + gap;

            for (int i = 0; i < count; i++)
            {
                if (i % period >= merlon) continue;

                for (int w = 0; w < width; w++)
                for (int h = 0; h < height; h++)
                {
                    int3 p = start + step * i;
                    int wx = step.x != 0 ? 0 : w;
                    int wz = step.z != 0 ? 0 : w;
                    Set(p.x + wx, p.y + h, p.z + wz, material);
                }
            }
        }

        /// <summary>Ring of crenellations around a tower.</summary>
        public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material)
        {
            for (int a = 0; a < 360; a += 6)
            {
                // Alternating arcs read as merlons once rasterised at this radius.
                if ((a / 6) % 2 == 1) continue;

                float rad = a * math.PI / 180f;
                int x = cx + (int)math.round(math.cos(rad) * radius);
                int z = cz + (int)math.round(math.sin(rad) * radius);

                for (int h = 0; h < height; h++)
                {
                    Set(x, y + h, z, material);

                    // Thicken inward so merlons are not one voxel wide at this scale.
                    int ix = cx + (int)math.round(math.cos(rad) * (radius - 1));
                    int iz = cz + (int)math.round(math.sin(rad) * (radius - 1));
                    Set(ix, y + h, iz, material);
                }
            }
        }

        /// <summary>Arched opening carved through a wall along <paramref name="depthAxis"/>.</summary>
        public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material)
        {
            int half = width / 2;

            for (int d = 0; d < depth; d++)
            for (int w = 0; w < width; w++)
            for (int h = 0; h < height; h++)
            {
                // Semicircular head: the arch is what separates a doorway from a hole.
                int dx = w - half;
                int archTop = height - half;
                if (h > archTop && dx * dx + (h - archTop) * (h - archTop) > half * half) continue;

                int x = min.x + (depthAxis == 0 ? d : w);
                int z = min.z + (depthAxis == 2 ? d : w);
                Set(x, min.y + h, z, material);
            }
        }

        /// <summary>Straight run of stairs climbing along one axis.</summary>
        public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material)
        {
            for (int s = 0; s < steps; s++)
            for (int r = 0; r < run; r++)
            for (int w = 0; w < width; w++)
            for (int y = 0; y < rise; y++)
            {
                int along = s * run + r;
                int x = min.x + (axis == 0 ? along : w);
                int z = min.z + (axis == 2 ? along : w);
                Set(x, min.y + s * rise + y, z, material);
            }
        }

        /// <summary>
        /// Spiral stair inside a shaft. How a keep gets from cellar to battlements without a
        /// straight run eating the floor plan.
        /// </summary>
        public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material)
        {
            for (int y = 0; y < height; y++)
            {
                float angle = y * 0.35f;

                for (int r = 1; r <= radius; r++)
                {
                    int x = cx + (int)math.round(math.cos(angle) * r);
                    int z = cz + (int)math.round(math.sin(angle) * r);
                    Set(x, baseY + y, z, material);

                    // A second voxel behind the tread, so the stair is walkable rather than a
                    // sequence of floating pads.
                    int x2 = cx + (int)math.round(math.cos(angle - 0.18f) * r);
                    int z2 = cz + (int)math.round(math.sin(angle - 0.18f) * r);
                    Set(x2, baseY + y, z2, material);
                }
            }
        }

        /// <summary>Carves a volume back to empty.</summary>
        public void Carve(int3 min, int3 size) => Box(min, size, Mat.Empty);

        /// <summary>
        /// Weathers a surface by speckling a second material onto exposed faces.
        ///
        /// Cheap and disproportionately effective: uniform colour is most of what makes voxel
        /// architecture look untouched by time.
        /// </summary>
        public void Weather(int3 min, int3 size, byte material, uint seed, int chanceOutOf100)
        {
            var rng = new Random(seed | 1u);

            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                int wx = min.x + x, wy = min.y + y, wz = min.z + z;
                if (!IsSolid(wx, wy, wz)) continue;
                if (IsSolid(wx, wy + 1, wz)) continue;          // only exposed tops
                if (rng.NextInt(0, 100) >= chanceOutOf100) continue;

                Set(wx, wy, wz, material);
            }
        }
    }
}
