using Unity.Mathematics;
using Random = Unity.Mathematics.Random;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Drawing primitives for authored structures through Storage.Api capabilities.
    ///
    /// Storage owns region lookup, physical block representation, allocation/free, uniform
    /// materialisation/collapse and commit. The brush owns only authored geometry and write
    /// accounting. Hot column paths still borrow one mutable 8^3 block at a time, so they retain
    /// the old one-collapse-check-per-block behaviour without exposing pool or table identity.
    ///
    /// Float arithmetic is fine in this assembly. The constitution forbids float where *clients
    /// independently re-derive* a result; a structure baked once and shipped as identical bytes is
    /// data, not a computation anyone repeats. What must never happen is this code running
    /// per-client at runtime and being trusted to agree.
    /// </summary>
    public struct VoxelBrush
    {
        private IRegionReadSource _reads;
        private IRegionMutationStore _mutations;
        private IMaterialAuthoringCatalogue _materials;
        private IMaterialPlacementCatalogue _placement;

        /// <summary>Per-voxel writes — the expensive kind, and what the budget governs.</summary>
        public int VoxelsWritten;

        /// <summary>Whole-block writes. One Storage operation each, counted but not budgeted.</summary>
        public int BricksWritten;

        /// <summary>
        /// Voxels changed through batched column writes. These do not consume
        /// <see cref="WriteBudget"/> because a column segment performs one collapse scan per
        /// block, rather than one scan per voxel.
        /// </summary>
        public long BulkVoxelsWritten;

        /// <summary>
        /// Hard ceiling on slow-path writes. Once crossed, every further slow write is dropped and
        /// <see cref="BudgetExceeded"/> latches. Batched whole-block and column operations are
        /// counted separately because they avoid the per-voxel collapse scan this ceiling guards.
        /// </summary>
        public int WriteBudget;

        public bool BudgetExceeded { get; private set; }

        public VoxelBrush(IRegionReadSource reads, IRegionMutationStore mutations,
                          int writeBudget = DefaultWriteBudget)
        {
            _reads = reads;
            _mutations = mutations;
            _materials = null;
            _placement = null;
            VoxelsWritten = 0;
            BricksWritten = 0;
            BulkVoxelsWritten = 0;
            WriteBudget = writeBudget;
            BudgetExceeded = false;
        }

        public VoxelBrush(IRegionReadSource reads, IRegionMutationStore mutations,
                          IMaterialAuthoringCatalogue materials,
                          int writeBudget = DefaultWriteBudget)
            : this(reads, mutations, writeBudget)
        {
            _materials = materials;
            _placement = materials as IMaterialPlacementCatalogue;
        }

        /// <summary>
        /// Twelve million slow-path voxel changes. Batched whole-block and column operations are
        /// counted separately because they avoid the per-voxel collapse scan this ceiling guards.
        /// </summary>
        public const int DefaultWriteBudget = 12_000_000;

        public long TotalVoxelsWritten => VoxelsWritten + BulkVoxelsWritten;

        // -- primitives ----------------------------------------------------------

        public void Set(int x, int y, int z, byte material)
        {
            if (VoxelsWritten >= WriteBudget)
            {
                BudgetExceeded = true;
                return;
            }

            int3 voxel = new(x, y, z);
            VoxelCell current = ReadCell(voxel);
            byte placementCoating = PlacementCoating(material);
            if (placementCoating != Coatings.None && current.IsSolid)
            {
                Coat(x, y, z, placementCoating);
                return;
            }

            ushort style = PlacementSurfaceStyle(material);
            if (!current.IsSolid && style != SurfaceStyles.MaterialDefault)
                SetStyled(x, y, z, material, style);
            else if (WriteMaterial(voxel, material))
                VoxelsWritten++;
        }

        public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                              byte coating = Coatings.None,
                              VoxelSurfaceFlags flags = VoxelSurfaceFlags.None)
        {
            if (coating != Coatings.None && _materials != null
                && !_materials.AllowsCoating(material, coating)) return;
            if (VoxelsWritten >= WriteBudget)
            {
                BudgetExceeded = true;
                return;
            }

            var cell = new VoxelCell
            {
                BaseMaterialId = material,
                Surface = new VoxelSurfaceSemantics
                {
                    StyleId = surfaceStyle,
                    CoatingId = coating,
                    Flags = flags
                }
            };
            if (WriteCell(new int3(x, y, z), in cell))
                VoxelsWritten++;
        }

        /// <summary>Applies a presentation coating while preserving base material and style.</summary>
        public void Coat(int x, int y, int z, byte coating)
        {
            int3 voxel = new(x, y, z);
            VoxelCell cell = ReadCell(voxel);
            if (!cell.IsSolid || cell.Surface.CoatingId == coating) return;
            if (coating != Coatings.None && _materials != null
                && !_materials.AllowsCoating(cell.BaseMaterialId, coating)) return;
            cell.Surface.CoatingId = coating;
            if (WriteCell(voxel, in cell)) VoxelsWritten++;
        }

        /// <summary>
        /// Fills a box, replacing complete logical blocks where the box covers them and using the
        /// normal authored-cell path only for edge voxels.
        /// </summary>
        public void FillBulk(int3 min, int3 size, byte material)
        {
            if (PlacementCoating(material) != Coatings.None)
            {
                for (int z = 0; z < size.z; z++)
                for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                    Set(min.x + x, min.y + y, min.z + z, material);
                return;
            }

            int3 max = min + size;

            int3 brickMin = min >> VoxelReadGrid.BlockEdgeLog2;
            int3 brickMax = (max - 1) >> VoxelReadGrid.BlockEdgeLog2;

            for (int bz = brickMin.z; bz <= brickMax.z; bz++)
            for (int by = brickMin.y; by <= brickMax.y; by++)
            for (int bx = brickMin.x; bx <= brickMax.x; bx++)
            {
                int3 blockMin = new int3(bx, by, bz) << VoxelReadGrid.BlockEdgeLog2;
                int3 blockMax = blockMin + VoxelReadGrid.BlockEdge;

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
        /// Fills one vertical column, borrowing one mutable logical block for each vertical
        /// segment. Storage performs materialisation and one final collapse/commit per block.
        /// </summary>
        public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material)
        {
            if (maxYExclusive <= minY || _mutations == null) return;

            int firstBlockY = minY >> VoxelReadGrid.BlockEdgeLog2;
            int lastBlockY = (maxYExclusive - 1) >> VoxelReadGrid.BlockEdgeLog2;
            int localX = x & VoxelReadGrid.BlockEdgeMask;
            int localZ = z & VoxelReadGrid.BlockEdgeMask;

            for (int blockY = firstBlockY; blockY <= lastBlockY; blockY++)
            {
                int blockOriginY = blockY << VoxelReadGrid.BlockEdgeLog2;
                int fromY = math.max(minY, blockOriginY);
                int toY = math.min(maxYExclusive, blockOriginY + VoxelReadGrid.BlockEdge);
                int3 worldBlock = new(x >> VoxelReadGrid.BlockEdgeLog2,
                                      blockY,
                                      z >> VoxelReadGrid.BlockEdgeLog2);

                if (TryReadBlock(worldBlock, out VoxelReadBlock block)
                    && block.Kind == VoxelReadBlockKind.Uniform
                    && block.UniformMaterial == material)
                    continue;

                if (!_mutations.TryBeginCellBlock(worldBlock, false,
                                                  out VoxelBlockMutation mutation))
                    continue;

                int changed = 0;
                for (int y = fromY; y < toY; y++)
                {
                    int localY = y - blockOriginY;
                    int voxelIndex = localX
                                   | (localY << VoxelReadGrid.BlockEdgeLog2)
                                   | (localZ << (VoxelReadGrid.BlockEdgeLog2 * 2));
                    if (mutation.SetMaterial(voxelIndex, material)) changed++;
                }

                _mutations.CompletePartialBlock(ref mutation, changed != 0);
                if (changed == 0) continue;

                BulkVoxelsWritten += changed;
                BricksWritten++;
            }
        }

        /// <summary>
        /// Replaces one logical block. Storage owns region creation, representation choice and any
        /// physical allocation/free needed to preserve authored surface semantics.
        /// </summary>
        private void SetWholeBrick(int3 brickOrigin, byte material)
        {
            if (VoxelsWritten >= WriteBudget)
            {
                BudgetExceeded = true;
                return;
            }
            if (_mutations == null) return;

            ushort style = PlacementSurfaceStyle(material);
            var cell = new VoxelCell
            {
                BaseMaterialId = material,
                Surface = style == SurfaceStyles.MaterialDefault
                    ? default
                    : new VoxelSurfaceSemantics { StyleId = style }
            };
            int3 worldBlock = brickOrigin >> VoxelReadGrid.BlockEdgeLog2;
            _mutations.SetWholeCellBlock(worldBlock, in cell, false);

            // Counted separately: this is one block operation, not 512 voxel writes, and charging
            // it as 512 would make the cheap path look expensive and push callers back onto the
            // expensive one.
            BricksWritten++;
        }

        private VoxelCell ReadCell(int3 worldVoxel)
        {
            if (_reads == null) return default;
            int3 worldBlock = worldVoxel >> VoxelReadGrid.BlockEdgeLog2;
            if (!_reads.TryAcquireRegionContainingBlock(worldBlock, out RegionReadView region))
                return default;

            int3 localVoxel = worldVoxel - region.RegionCoord * VoxelGrid.RegionVoxelEdge;
            return region.TryReadCell(localVoxel, out VoxelCell cell) ? cell : default;
        }

        private bool TryReadBlock(int3 worldBlock, out VoxelReadBlock block)
        {
            if (_reads != null
                && _reads.TryAcquireRegionContainingBlock(worldBlock, out RegionReadView region)
                && region.TryGetWorldBlock(worldBlock, out block))
                return true;
            block = default;
            return false;
        }

        private bool WriteMaterial(int3 worldVoxel, byte material)
        {
            if (_mutations == null) return false;
            int3 worldBlock = worldVoxel >> VoxelReadGrid.BlockEdgeLog2;
            if (!_mutations.TryBeginCellBlock(worldBlock, false,
                                              out VoxelBlockMutation mutation))
                return false;

            bool changed = mutation.SetMaterial(VoxelIndex(worldVoxel), material);
            return _mutations.CompletePartialBlock(ref mutation, changed);
        }

        private bool WriteCell(int3 worldVoxel, in VoxelCell cell)
        {
            if (_mutations == null) return false;
            int3 worldBlock = worldVoxel >> VoxelReadGrid.BlockEdgeLog2;
            if (!_mutations.TryBeginCellBlock(worldBlock, false,
                                              out VoxelBlockMutation mutation))
                return false;

            bool changed = mutation.SetCell(VoxelIndex(worldVoxel), in cell);
            return _mutations.CompletePartialBlock(ref mutation, changed);
        }

        private static int VoxelIndex(int3 worldVoxel)
        {
            int localX = worldVoxel.x & VoxelReadGrid.BlockEdgeMask;
            int localY = worldVoxel.y & VoxelReadGrid.BlockEdgeMask;
            int localZ = worldVoxel.z & VoxelReadGrid.BlockEdgeMask;
            return localX
                 | (localY << VoxelReadGrid.BlockEdgeLog2)
                 | (localZ << (VoxelReadGrid.BlockEdgeLog2 * 2));
        }

        private ushort PlacementSurfaceStyle(byte material) =>
            _placement != null
                ? _placement.GetPlacementSurfaceStyle(material)
                : material == VoxelGrid.MaterialEmpty
                    ? SurfaceStyles.MaterialDefault
                    : SurfaceStyles.Planar;

        private byte PlacementCoating(byte material) =>
            _placement != null ? _placement.GetPlacementCoating(material) : Coatings.None;

        public byte Get(int x, int y, int z) =>
            ReadCell(new int3(x, y, z)).BaseMaterialId;

        public byte GetCoating(int x, int y, int z) =>
            ReadCell(new int3(x, y, z)).Surface.CoatingId;

        public bool IsSolid(int x, int y, int z) => Get(x, y, z) != VoxelGrid.MaterialEmpty;

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
                if (material == VoxelGrid.MaterialEmpty)
                    Set(x, min.y + h, z, material);
                else
                    SetStyled(x, min.y + h, z, material, SurfaceStyles.Rounded,
                              Coatings.None, VoxelSurfaceFlags.PreserveFeature);
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
                            Set(x, treadY + h, z, VoxelGrid.MaterialEmpty);

                        Set(x, treadY, z, material);
                        Set(x, treadY + 1, z, material);
                    }
                }
            }
        }

        /// <summary>Carves a volume back to empty.</summary>
        public void Carve(int3 min, int3 size) => Box(min, size, VoxelGrid.MaterialEmpty);

        /// <summary>
        /// Weathers a surface by speckling a second material onto exposed faces.
        ///
        /// Cheap and disproportionately effective: uniform colour is most of what makes voxel
        /// architecture look untouched by time.
        /// </summary>
        public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100)
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

                Coat(wx, wy, wz, coating);
            }
        }
    }
}
