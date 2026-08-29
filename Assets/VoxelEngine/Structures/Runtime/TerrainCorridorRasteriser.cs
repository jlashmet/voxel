using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Pure sample returned by <see cref="TerrainCorridorRasteriser.TrySample"/>. The corridor
    /// primitive stores its resolved geometry in voxels, but influence is evaluated in the authored
    /// decimetre grid so WorldBuilder semantic queries and physical lowering use identical rounding,
    /// deterministic edge variation, target elevation and 0..31 coverage at shared sample points.
    /// </summary>
    public readonly struct TerrainCorridorSample
    {
        public int DistanceDm { get; }
        public int TargetHeightVoxels { get; }
        public byte Coverage31 { get; }
        public bool InCore { get; }

        public TerrainCorridorSample(
            int distanceDm,
            int targetHeightVoxels,
            byte coverage31,
            bool inCore)
        {
            DistanceDm = distanceDm;
            TargetHeightVoxels = targetHeightVoxels;
            Coverage31 = coverage31;
            InCore = inCore;
        }
    }

    /// <summary>
    /// Bounded, integer-only terrain-column lowering for generic resolved corridors. It owns no
    /// road vocabulary: callers provide resolved endpoints, core/outer influence, material and seed.
    /// The same scalar grades density, selects surface coverage, and is persisted as generic surface
    /// detail for presentation/LOD extraction.
    /// </summary>
    public static class TerrainCorridorRasteriser
    {
        private const int SurfacePaintDepth = 4;

        public static RasterResult Rasterise(
            in Primitive primitive,
            int3 subVolumeMin,
            int3 subVolumeMax,
            IRegionReadSource reads,
            IRegionMutationStore mutations)
        {
            var result = new RasterResult();
            if (primitive.Shape != PrimitiveShape.TerrainCorridor
                || primitive.Mode != PrimitiveMode.TerrainCorridor)
                return result;

            primitive.Bounds(out int3 boundsMin, out int3 boundsMax);
            int x0 = math.max(boundsMin.x, subVolumeMin.x);
            int x1 = math.min(boundsMax.x, subVolumeMax.x - 1);
            int z0 = math.max(boundsMin.z, subVolumeMin.z);
            int z1 = math.min(boundsMax.z, subVolumeMax.z - 1);
            if (x0 > x1 || z0 > z1) return result;

            var read = new WorldReadCursor(reads);
            var writes = new CellMutationCursor(mutations);
            int maximumCutFill = math.max(0, primitive.C.x);
            int clearAbove = math.max(0, primitive.C.z);
            int topDepth = math.max(1, math.min(SurfacePaintDepth, primitive.C.y));

            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                if (!TrySample(in primitive, x, z, out TerrainCorridorSample sample))
                    continue;
                if (!TryFindSurface(x, z, boundsMin.y, boundsMax.y,
                        ref read, out int sourceY, out VoxelCell sourceSurface))
                    continue;

                int desiredY = sourceY + DivideRounded(
                    (long)(sample.TargetHeightVoxels - sourceY) * sample.Coverage31,
                    31);
                desiredY = math.clamp(
                    desiredY,
                    sourceY - maximumCutFill,
                    sourceY + maximumCutFill);

                byte localMaterial = sourceSurface.BaseMaterialId;
                int low = math.min(sourceY + 1, desiredY - topDepth + 1);
                int high = math.max(sourceY, desiredY + clearAbove);
                low = math.max(low, subVolumeMin.y);
                high = math.min(high, subVolumeMax.y - 1);
                if (low > high) continue;

                bool roadCoverage = ShouldUsePrimaryMaterial(
                    in primitive, x, z, sample.Coverage31);
                for (int y = low; y <= high; y++)
                {
                    int3 voxel = new int3(x, y, z);
                    VoxelCell current = writes.ReadCell(voxel, ref read);
                    if (y > desiredY)
                    {
                        if (!current.IsSolid) continue;
                        if (writes.SetCell(voxel, default)) result.VoxelsWritten++;
                        continue;
                    }

                    bool inTop = y >= desiredY - topDepth + 1;
                    VoxelCell next = current;
                    if (!next.IsSolid)
                        next = new VoxelCell { BaseMaterialId = localMaterial };

                    if (inTop)
                    {
                        if (roadCoverage || sample.InCore
                            || next.BaseMaterialId == primitive.Material)
                        {
                            next.BaseMaterialId = primitive.Material;
                            next.Surface = new VoxelSurfaceSemantics
                            {
                                StyleId = SurfaceStyles.MaterialDefault,
                                Detail = sample.Coverage31,
                            };
                            next.Boundary = default;
                        }
                        else
                        {
                            next.Surface.Detail = (byte)math.max(
                                (int)next.Surface.Detail,
                                (int)sample.Coverage31);
                        }
                    }

                    if (current.Equals(next)) continue;
                    if (writes.SetCell(voxel, in next)) result.VoxelsWritten++;
                }
            }

            writes.Flush();
            result.PrimitivesRasterised = 1;
            return result;
        }

        /// <summary>
        /// Evaluates one horizontal sample. At voxel coordinates that map to an authored decimetre
        /// this intentionally mirrors WorldRoadInfluence exactly: closest-point rounding, edge hash,
        /// core/outer adjustment and coverage rounding are the same operations in the same order.
        /// </summary>
        public static bool TrySample(
            in Primitive primitive,
            int worldX,
            int worldZ,
            out TerrainCorridorSample sample)
        {
            if (primitive.Shape != PrimitiveShape.TerrainCorridor)
            {
                sample = default;
                return false;
            }

            int scale = math.max(1, primitive.D.z);
            int ax = DivideRounded(primitive.A.x, scale);
            int ay = DivideRounded(primitive.A.y, scale);
            int az = DivideRounded(primitive.A.z, scale);
            int bx = DivideRounded(primitive.B.x, scale);
            int by = DivideRounded(primitive.B.y, scale);
            int bz = DivideRounded(primitive.B.z, scale);
            int xdm = DivideRounded(worldX, scale);
            int zdm = DivideRounded(worldZ, scale);

            long dx = (long)bx - ax;
            long dz = (long)bz - az;
            long lengthSquared = dx * dx + dz * dz;
            long distanceSquared;
            int targetHeightDm;

            if (lengthSquared <= 0)
            {
                long px = (long)xdm - ax;
                long pz = (long)zdm - az;
                distanceSquared = px * px + pz * pz;
                targetHeightDm = ay;
            }
            else
            {
                long dot = ((long)xdm - ax) * dx + ((long)zdm - az) * dz;
                if (dot < 0) dot = 0;
                else if (dot > lengthSquared) dot = lengthSquared;

                long qx = (long)ax + DivideRounded(dx * dot, lengthSquared);
                long qz = (long)az + DivideRounded(dz * dot, lengthSquared);
                long ex = (long)xdm - qx;
                long ez = (long)zdm - qz;
                distanceSquared = ex * ex + ez * ez;
                targetHeightDm = ay + DivideRounded(
                    ((long)by - ay) * dot,
                    lengthSquared);
            }

            int distance = IntegerSqrt(distanceSquared);
            int edgeVariationDm = DivideRounded(
                math.max(0, primitive.D.x),
                scale);
            int edge = DeterministicEdgeOffset(
                unchecked((uint)primitive.D.y),
                xdm,
                zdm,
                edgeVariationDm);
            int coreBaseDm = DivideRounded(
                math.max(0, primitive.InnerRadius),
                scale);
            int maximumOuterDm = DivideRounded(
                math.max(primitive.InnerRadius, primitive.Radius),
                scale);
            int baseOuterDm = math.max(
                coreBaseDm,
                maximumOuterDm - edgeVariationDm);
            int core = math.max(0, coreBaseDm + edge);
            int outer = math.max(core, baseOuterDm + edge);
            if (distance > outer)
            {
                sample = default;
                return false;
            }

            int coverage = distance <= core || outer == core
                ? 31
                : ((outer - distance) * 31 + (outer - core) / 2)
                    / (outer - core);
            coverage = math.clamp(coverage, 0, 31);
            sample = new TerrainCorridorSample(
                distance,
                targetHeightDm * scale,
                (byte)coverage,
                distance <= core);
            return coverage > 0;
        }

        public static bool Contains(in Primitive primitive, int3 voxel)
        {
            if (!TrySample(
                    in primitive,
                    voxel.x,
                    voxel.z,
                    out TerrainCorridorSample sample))
                return false;

            int vertical = math.max(0, primitive.C.x);
            int fillDepth = math.max(1, primitive.C.y);
            int clearAbove = math.max(0, primitive.C.z);
            return voxel.y >= sample.TargetHeightVoxels - vertical - fillDepth
                && voxel.y <= sample.TargetHeightVoxels + vertical + clearAbove;
        }

        private static bool TryFindSurface(
            int x,
            int z,
            int minY,
            int maxY,
            ref WorldReadCursor read,
            out int surfaceY,
            out VoxelCell surface)
        {
            for (int y = maxY; y >= minY; y--)
            {
                VoxelCell cell = read.ReadCell(new int3(x, y, z));
                if (!cell.IsSolid) continue;
                surfaceY = y;
                surface = cell;
                return true;
            }

            surfaceY = 0;
            surface = default;
            return false;
        }

        private static bool ShouldUsePrimaryMaterial(
            in Primitive primitive,
            int worldX,
            int worldZ,
            int coverage31)
        {
            if (coverage31 >= 31) return true;
            if (coverage31 <= 0) return false;

            int scale = math.max(1, primitive.D.z);
            int xdm = DivideRounded(worldX, scale);
            int zdm = DivideRounded(worldZ, scale);
            unchecked
            {
                uint h = (uint)primitive.D.y ^ 0x85EBCA6Bu;
                h = (h ^ (uint)xdm) * 16777619u;
                h = (h ^ (uint)zdm) * 16777619u;
                return h % 31u < (uint)coverage31;
            }
        }

        private static int DeterministicEdgeOffset(
            uint seed,
            int x,
            int z,
            int amplitude)
        {
            if (amplitude <= 0) return 0;
            unchecked
            {
                uint h = seed ^ 0x9E3779B9u;
                h = (h ^ (uint)x) * 16777619u;
                h = (h ^ (uint)z) * 16777619u;
                return (int)(h % (uint)(amplitude * 2 + 1)) - amplitude;
            }
        }

        private static int DivideRounded(long numerator, long denominator)
        {
            if (denominator <= 0) return 0;
            if (numerator >= 0)
                return (int)((numerator + denominator / 2) / denominator);
            return (int)(-((-numerator + denominator / 2) / denominator));
        }

        private static int IntegerSqrt(long value)
        {
            if (value <= 0) return 0;
            long low = 1;
            long high = value < 3037000499L ? value : 3037000499L;
            while (low <= high)
            {
                long middle = low + ((high - low) >> 1);
                if (middle <= value / middle) low = middle + 1;
                else high = middle - 1;
            }

            long root = high;
            long lowerError = value - root * root;
            long next = root + 1;
            if (next <= 3037000499L)
            {
                long upperError = next * next - value;
                if (upperError <= lowerError) root = next;
            }

            return root > int.MaxValue ? int.MaxValue : (int)root;
        }

        private static int VoxelIndex(int3 worldVoxel)
        {
            int3 inner = worldVoxel & VoxelReadGrid.BlockEdgeMask;
            return inner.x
                | (inner.y << VoxelReadGrid.BlockEdgeLog2)
                | (inner.z << (VoxelReadGrid.BlockEdgeLog2 * 2));
        }

        private struct WorldReadCursor
        {
            private readonly IRegionReadSource _source;
            private RegionReadView _view;
            private int3 _regionCoord;
            private bool _hasView;

            public WorldReadCursor(IRegionReadSource source)
            {
                _source = source;
                _view = default;
                _regionCoord = default;
                _hasView = false;
            }

            public VoxelCell ReadCell(int3 worldVoxel)
            {
                int3 regionCoord = worldVoxel >> VoxelGrid.RegionVoxelEdgeLog2;
                if (!_hasView || math.any(regionCoord != _regionCoord))
                {
                    _regionCoord = regionCoord;
                    _hasView = _source != null
                        && _source.TryAcquireRegion(regionCoord, out _view);
                }

                if (!_hasView) return default;
                int3 localVoxel = worldVoxel
                    - (regionCoord << VoxelGrid.RegionVoxelEdgeLog2);
                return _view.TryReadCell(localVoxel, out VoxelCell cell)
                    ? cell
                    : default;
            }
        }

        private struct CellMutationCursor
        {
            private readonly IRegionMutationStore _store;
            private int3 _worldBlock;
            private VoxelBlockMutation _mutation;
            private bool _hasBlock;
            private bool _payloadChanged;

            public CellMutationCursor(IRegionMutationStore store)
            {
                _store = store;
                _worldBlock = default;
                _mutation = default;
                _hasBlock = false;
                _payloadChanged = false;
            }

            public VoxelCell ReadCell(
                int3 worldVoxel,
                ref WorldReadCursor fallback)
            {
                int3 worldBlock = worldVoxel >> VoxelReadGrid.BlockEdgeLog2;
                if (_hasBlock
                    && math.all(worldBlock == _worldBlock)
                    && _mutation.IsCreated)
                    return _mutation.GetCell(VoxelIndex(worldVoxel));
                return fallback.ReadCell(worldVoxel);
            }

            public bool SetCell(int3 worldVoxel, in VoxelCell next)
            {
                int3 worldBlock = worldVoxel >> VoxelReadGrid.BlockEdgeLog2;
                if (!_hasBlock || math.any(worldBlock != _worldBlock))
                {
                    Flush();
                    if (_store == null
                        || !_store.TryBeginCellBlock(
                            worldBlock,
                            false,
                            out _mutation))
                        return false;
                    _worldBlock = worldBlock;
                    _hasBlock = true;
                    _payloadChanged = false;
                }

                if (!_mutation.IsCreated) return false;
                int index = VoxelIndex(worldVoxel);
                VoxelCell current = _mutation.GetCell(index);
                if (current.Equals(next)) return false;
                bool changed = _mutation.SetCell(index, in next);
                _payloadChanged |= changed;
                return changed;
            }

            public void Flush()
            {
                if (!_hasBlock) return;
                _store.CompletePartialBlock(ref _mutation, _payloadChanged);
                _hasBlock = false;
                _payloadChanged = false;
            }
        }
    }
}
