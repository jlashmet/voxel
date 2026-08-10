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
        /// Voxels changed through batched column writes. These do not consume
        /// <see cref="WriteBudget"/> because a column segment performs one collapse scan per
        /// brick, rather than one scan per voxel.
        /// </summary>
        public long BulkVoxelsWritten;

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
            BulkVoxelsWritten = 0;
            WriteBudget = writeBudget;
            BudgetExceeded = false;
        }

        /// <summary>
        /// Twelve million slow-path voxel changes. Batched whole-brick and column operations are
        /// counted separately because they avoid the per-voxel collapse scan this ceiling guards.
        /// </summary>
        public const int DefaultWriteBudget = 12_000_000;

        public RegionTable Table => _table;
        public BrickPool Pool => _pool;
        public long TotalVoxelsWritten => VoxelsWritten + BulkVoxelsWritten;

        // -- primitives ----------------------------------------------------------

        public void Set(int x, int y, int z, byte material)
        {
            if (VoxelsWritten >= WriteBudget)
            {
                BudgetExceeded = true;
                return;
            }

            // VoxelBrush is the semantic boundary between authored structures and natural
            // terrain. Preserve that information instead of trying to infer geometry vocabulary
            // from material later (castle stone and cliff stone intentionally share a material).
            if (VoxelAccess.SetVoxel(ref _table, ref _pool, new int3(x, y, z), material,
                                     markHardSurface: true))
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

        /// <summary>
        /// Fills one vertical column, batching all writes in a brick before checking whether the
        /// brick collapsed to a uniform reference.
        ///
        /// A one-voxel-wide call to <see cref="FillBulk"/> cannot cover an 8x8x8 brick and falls
        /// back to <see cref="Set"/> for every voxel. Site sculpting issues hundreds of thousands
        /// of such columns, so that fallback turns a cheap height-volume rewrite into millions of
        /// 512-byte collapse scans. This is the column-shaped equivalent of the whole-brick path.
        /// </summary>
        public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material)
        {
            if (maxYExclusive <= minY) return;

            int firstBrickY = minY >> VoxelDimensions.BrickEdgeLog2;
            int lastBrickY = (maxYExclusive - 1) >> VoxelDimensions.BrickEdgeLog2;

            for (int brickY = firstBrickY; brickY <= lastBrickY; brickY++)
            {
                int brickOriginY = brickY << VoxelDimensions.BrickEdgeLog2;
                int fromY = math.max(minY, brickOriginY);
                int toY = math.min(maxYExclusive, brickOriginY + VoxelDimensions.BrickEdge);

                var world = new int3(x, brickOriginY, z);
                VoxelAccess.Decompose(world, out var regionCoord, out var brickInRegion,
                                      out var voxelInBrick);

                var region = _table.LoadRegion(regionCoord);
                int brickIndex = Region.BrickIndex(brickInRegion.x, brickInRegion.y, brickInRegion.z);
                var brick = region.BrickRefs[brickIndex];
                bool semanticChanged = region.MarkHardSurfaceBrick(brickIndex);

                if (brick.IsUniform && brick.UniformMaterial == material)
                {
                    if (semanticChanged)
                    {
                        region.Dirty = true;
                        _table.CommitRegion(region);
                        BricksWritten++;
                    }
                    continue;
                }

                int poolIndex;
                if (brick.IsUniform)
                {
                    poolIndex = _pool.Allocate();
                    _pool.FillBrick(poolIndex, brick.UniformMaterial);
                    region.BrickRefs[brickIndex] = BrickRef.FromPoolIndex(poolIndex);
                }
                else
                {
                    poolIndex = brick.PoolIndex;
                }

                int changed = 0;
                for (int y = fromY; y < toY; y++)
                {
                    int voxelIndex = VoxelEngine.Core.Occupancy.OccupancyMask.VoxelIndex(
                        voxelInBrick.x, y - brickOriginY, voxelInBrick.z);

                    if (_pool.GetVoxel(poolIndex, voxelIndex) == material) continue;
                    _pool.SetVoxel(poolIndex, voxelIndex, material);
                    changed++;
                }

                if (changed == 0)
                {
                    // This can only occur after materialising a uniform brick whose requested
                    // segment already matched. Avoid retaining a needless mixed allocation.
                    if (brick.IsUniform)
                    {
                        _pool.Free(poolIndex);
                        region.BrickRefs[brickIndex] = brick;
                    }
                    if (semanticChanged)
                    {
                        region.Dirty = true;
                        _table.CommitRegion(region);
                        BricksWritten++;
                    }
                    continue;
                }

                if (_pool.TryGetUniformMaterial(poolIndex, out var uniform))
                {
                    _pool.Free(poolIndex);
                    region.BrickRefs[brickIndex] = BrickRef.Uniform(uniform);
                }

                region.Dirty = true;
                _table.CommitRegion(region);
                BulkVoxelsWritten += changed;
                BricksWritten++;
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
            bool semanticChanged = region.MarkHardSurfaceBrick(index);

            // Hand back the pool slot, or the brick leaks for the life of the session.
            if (existing.IsMixed) _pool.Free(existing.PoolIndex);

            region.BrickRefs[index] = material == Mat.Empty
                ? BrickRef.Empty
                : BrickRef.Uniform(material);

            region.Dirty = true;
            _table.CommitRegion(region);

            // Counted separately: this is one pointer write, not 512 voxel writes, and charging
            // it as 512 would make the cheap path look expensive and push callers back onto the
            // expensive one. A semantic-only write still counts because it changes derived mesh
            // ownership even when the material reference already matched.
            if (semanticChanged || existing.Value != region.BrickRefs[index].Value)
                BricksWritten++;
        }

        public byte Get(int x, int y, int z) =>
            VoxelAccess.GetVoxel(ref _table, in _pool, new int3(x, y, z));

        public bool IsSolid(int x, int y, int z) => Get(x, y, z) != Mat.Empty;

        public void Box(int3 min, int3 size, byte material)
        {
            if (math.any(size <= 0)) return;
            FillBulk(min, size, material);
        }

        /// <summary>Walls only — the shell of a box, with an open interior.</summary>
        public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling)
        {
            if (math.any(size <= 0) || thickness <= 0) return;

            int t = math.min(thickness, math.min(size.x, size.z));
            FillBulk(min, new int3(t, size.y, size.z), material);
            FillBulk(new int3(min.x + size.x - t, min.y, min.z),
                     new int3(t, size.y, size.z), material);

            int middleX = math.max(0, size.x - t * 2);
            if (middleX > 0)
            {
                FillBulk(new int3(min.x + t, min.y, min.z),
                         new int3(middleX, size.y, t), material);
                FillBulk(new int3(min.x + t, min.y, min.z + size.z - t),
                         new int3(middleX, size.y, t), material);
            }

            if (floor)
                FillBulk(min, new int3(size.x, math.min(t, size.y), size.z), material);
            if (ceiling)
                FillBulk(new int3(min.x, min.y + math.max(0, size.y - t), min.z),
                         new int3(size.x, math.min(t, size.y), size.z), material);
        }

        /// <summary>Vertical cylinder. <paramref name="innerRadius"/> above zero leaves a shaft.</summary>
        public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material,
                             int innerRadius = 0)
        {
            int r2 = radius * radius;
            int ir2 = innerRadius * innerRadius;

            for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
            {
                int d2 = x * x + z * z;
                if (d2 > r2 || (innerRadius > 0 && d2 < ir2)) continue;

                FillColumnBulk(cx + x, baseY, baseY + height, cz + z, material);
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

                // A thicker shell than seems necessary. At tower radius a two-voxel skin leaves
                // gaps between successive layers as the radius shrinks, and the roof reads as a
                // cloud of fragments instead of a cone.
                int inner = math.max(0, r - 5);
                int inner2 = inner * inner;

                for (int z = -r; z <= r; z++)
                for (int x = -r; x <= r; x++)
                {
                    int d2 = x * x + z * z;
                    if (d2 <= r2 && (d2 >= inner2 || y >= height - 4))
                        Set(cx + x, baseY + y, cz + z, material);
                }
            }
        }

        /// <summary>Solid taper hanging from a ceiling, used for cave stalactites.</summary>
        public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material)
        {
            for (int y = 0; y < height; y++)
            {
                float t = 1f - (float)y / height;
                int r = math.max(0, (int)math.round(radius * t));
                int r2 = r * r;
                for (int z = -r; z <= r; z++)
                for (int x = -r; x <= r; x++)
                    if (x * x + z * z <= r2)
                        Set(cx + x, ceilingY - y, cz + z, material);
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

                    // Four layers of skin, not two: at a shallow pitch consecutive courses step
                    // sideways faster than a thin shell can bridge, and the roof develops holes.
                    if (y >= h - 3 || y <= 1)
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
            // Merlons as arcs of several degrees, three voxels deep. Single-voxel arcs at this
            // radius rasterise to scattered dots that read as debris around the parapet.
            for (int a = 0; a < 360; a += 24)
            {
                for (int step = 0; step < 14; step++)
                {
                    float rad = (a + step) * math.PI / 180f;

                    for (int inset = 0; inset < 3; inset++)
                    {
                        int x = cx + (int)math.round(math.cos(rad) * (radius - inset));
                        int z = cz + (int)math.round(math.sin(rad) * (radius - inset));

                        for (int h = 0; h < height; h++)
                            Set(x, y + h, z, material);
                    }
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
        /// Walkable spiral stair inside a shaft. Treads rise 20 cm and advance roughly 30 cm at
        /// their walking line, matching CharacterMotor's 30 cm step-up allowance. Each tread
        /// also carves 2 m of headroom; without that carve a stair drawn through a floor stack is
        /// only visible geometry, not circulation.
        /// </summary>
        public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material)
        {
            const int rise = 2;
            const int run = 3;
            const int headroom = 20;
            const int angularSamples = 5;

            int innerRadius = math.max(2, radius - 10);
            float walkingRadius = (innerRadius + radius) * 0.5f;
            float anglePerStep = run / walkingRadius;
            int steps = (height + rise - 1) / rise;

            for (int step = 0; step < steps; step++)
            {
                int treadY = baseY + step * rise;

                // A shallow wedge rather than a one-voxel spoke gives the character's 60 cm
                // footprint somewhere to stand while turning around the shaft.
                for (int sample = 0; sample < angularSamples; sample++)
                {
                    float angle = (step + sample / (float)angularSamples) * anglePerStep;

                    for (int r = innerRadius; r <= radius; r++)
                    {
                        int x = cx + (int)math.round(math.cos(angle) * r);
                        int z = cz + (int)math.round(math.sin(angle) * r);

                        for (int h = rise; h < headroom; h++)
                            Set(x, treadY + h, z, Mat.Empty);

                        Set(x, treadY, z, material);
                        Set(x, treadY + 1, z, material);
                    }
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
