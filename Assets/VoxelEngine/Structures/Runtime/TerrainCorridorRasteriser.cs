using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Pure sample returned by <see cref="TerrainCorridorRasteriser.TrySample"/>. The corridor
    /// primitive stores its resolved geometry in voxels, but influence is evaluated in the authored
    /// decimetre grid so semantic queries and physical lowering use identical rounding and bounded
    /// deterministic edge variation. Coverage31 controls physical grading while SurfaceCoverage31
    /// independently controls visible material/detail coverage for packed corridor programs.
    /// </summary>
    public readonly struct TerrainCorridorSample
    {
        public int DistanceDm { get; }
        public int TargetHeightVoxels { get; }
        public byte Coverage31 { get; }
        public byte SurfaceCoverage31 { get; }
        public byte SurfaceDetail31 { get; }
        public bool InCore { get; }

        public TerrainCorridorSample(
            int distanceDm,
            int targetHeightVoxels,
            byte coverage31,
            bool inCore)
            : this(
                distanceDm,
                targetHeightVoxels,
                coverage31,
                coverage31,
                coverage31,
                inCore)
        {
        }

        public TerrainCorridorSample(
            int distanceDm,
            int targetHeightVoxels,
            byte coverage31,
            byte surfaceDetail31,
            bool inCore)
            : this(
                distanceDm,
                targetHeightVoxels,
                coverage31,
                coverage31,
                surfaceDetail31,
                inCore)
        {
        }

        public TerrainCorridorSample(
            int distanceDm,
            int targetHeightVoxels,
            byte coverage31,
            byte surfaceCoverage31,
            byte surfaceDetail31,
            bool inCore)
        {
            DistanceDm = distanceDm;
            TargetHeightVoxels = targetHeightVoxels;
            Coverage31 = coverage31;
            SurfaceCoverage31 = surfaceCoverage31;
            SurfaceDetail31 = surfaceDetail31;
            InCore = inCore;
        }
    }

    /// <summary>
    /// Bounded, integer-only terrain-column lowering for generic resolved corridors. It owns no
    /// road vocabulary: callers provide resolved endpoints, core/outer influence, material and seed.
    /// Legacy plain-scale programs retain one shared scalar. Packed programs may keep a narrower
    /// visible surface envelope while grading density back to source terrain across a wider bounded
    /// envelope, without adding persisted storage or renderer-specific state.
    /// </summary>
    public static class TerrainCorridorRasteriser
    {
        private const int SurfacePaintDepth = 4;
        private const int EdgeNoiseCellDm = 64;

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
                ushort localStyle = sourceSurface.Surface.ReconstructionStyleId;
                int low = math.min(sourceY + 1, desiredY - topDepth + 1);
                int high = math.max(sourceY, desiredY + clearAbove);
                low = math.max(low, subVolumeMin.y);
                high = math.min(high, subVolumeMax.y - 1);
                if (low > high) continue;

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
                        if (sample.SurfaceCoverage31 == 0)
                        {
                            // This is grading-only terrain outside the authored visible corridor.
                            // Preserve the authoritative source surface while still moving density.
                            next.BaseMaterialId = localMaterial;
                            next.Surface = sourceSurface.Surface;
                        }
                        else if (sample.InCore || next.BaseMaterialId == primitive.Material)
                        {
                            next.BaseMaterialId = primitive.Material;
                            next.Surface = new VoxelSurfaceSemantics
                            {
                                StyleId = SurfaceStyles.MaterialDefault,
                                Detail = sample.SurfaceDetail31,
                            };
                        }
                        else
                        {
                            // Preserve authoritative terrain material for destruction/collision and
                            // carry only the visible surface envelope as presentation metadata.
                            next.BaseMaterialId = localMaterial;
                            next.Surface = VoxelSurfaceSemantics.MaterialBlend(
                                localStyle,
                                primitive.Material,
                                sample.SurfaceCoverage31,
                                sourceSurface.Surface.Flags);
                        }
                        next.Boundary = default;
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
        /// Evaluates one horizontal sample. Closest-point rounding and coherent edge variation are
        /// shared by both envelopes. Packed programs keep the cross-section bounded to the visible
        /// surface radius, hold grading fully formed through that shoulder, then blend density back
        /// to source terrain across the wider grading radius. Plain-scale programs preserve the
        /// original single tapered envelope exactly.
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

            int scale = ShapeOps.TerrainCorridorScale(primitive.D.z);
            bool hasIndependentSurface =
                ShapeOps.HasPackedTerrainCorridorSurfaceOuter(primitive.D.z);
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
            int centreXdm;
            int centreZdm;

            if (lengthSquared <= 0)
            {
                long px = (long)xdm - ax;
                long pz = (long)zdm - az;
                distanceSquared = px * px + pz * pz;
                targetHeightDm = ay;
                centreXdm = ax;
                centreZdm = az;
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
                centreXdm = (int)qx;
                centreZdm = (int)qz;
            }

            int distance = IntegerSqrt(distanceSquared);
            int edgeVariationDm = DivideRounded(
                math.max(0, primitive.D.x),
                scale);
            int edge = DeterministicEdgeOffset(
                unchecked((uint)primitive.D.y),
                centreXdm,
                centreZdm,
                edgeVariationDm);
            int coreBaseDm = DivideRounded(
                math.max(0, primitive.InnerRadius),
                scale);
            int gradingMaximumOuterDm = DivideRounded(
                math.max(primitive.InnerRadius, primitive.Radius),
                scale);
            int surfaceMaximumOuterVoxels =
                ShapeOps.TerrainCorridorSurfaceOuterRadius(
                    primitive.D.z,
                    primitive.Radius);
            int surfaceMaximumOuterDm = DivideRounded(
                math.max(primitive.InnerRadius, surfaceMaximumOuterVoxels),
                scale);
            int surfaceBaseOuterDm = math.max(
                coreBaseDm,
                surfaceMaximumOuterDm - edgeVariationDm);
            int gradingBaseOuterDm = math.max(
                surfaceBaseOuterDm,
                gradingMaximumOuterDm - edgeVariationDm);
            int core = math.max(0, coreBaseDm + edge);
            int surfaceOuter = math.max(core, surfaceBaseOuterDm + edge);
            int gradingOuter = math.max(surfaceOuter, gradingBaseOuterDm + edge);
            if (distance > gradingOuter)
            {
                sample = default;
                return false;
            }

            int surfaceCoverage = Coverage(distance, core, surfaceOuter);
            int gradingCoverage = !hasIndependentSurface || gradingOuter <= surfaceOuter
                ? surfaceCoverage
                : distance <= surfaceOuter
                    ? 31
                    : Coverage(distance, surfaceOuter, gradingOuter);
            int crossSectionDistance = math.min(distance, surfaceOuter);
            targetHeightDm += CrossSectionOffsetDm(
                crossSectionDistance,
                core,
                surfaceOuter);
            byte surfaceDetail = SurfaceDetail(
                unchecked((uint)primitive.D.y),
                xdm,
                zdm,
                distance,
                core,
                surfaceCoverage);
            sample = new TerrainCorridorSample(
                distance,
                targetHeightDm * scale,
                (byte)gradingCoverage,
                (byte)surfaceCoverage,
                surfaceDetail,
                distance <= core);
            return gradingCoverage > 0;
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

        private static int Coverage(int distanceDm, int innerDm, int outerDm)
        {
            if (distanceDm > outerDm) return 0;
            if (distanceDm <= innerDm || outerDm == innerDm) return 31;
            return math.clamp(
                ((outerDm - distanceDm) * 31 + (outerDm - innerDm) / 2)
                    / (outerDm - innerDm),
                0,
                31);
        }

        private static int CrossSectionOffsetDm(int distanceDm, int coreDm, int outerDm)
        {
            if (coreDm <= 0) return 0;
            int crown = math.clamp(coreDm / 12, 1, 3);
            if (distanceDm <= coreDm)
                return DivideRounded((long)crown * (coreDm - distanceDm), coreDm);

            int shoulderWidth = outerDm - coreDm;
            if (shoulderWidth <= 0) return 0;
            int shoulderDrop = math.clamp(shoulderWidth / 10, 1, 3);
            return -DivideRounded(
                (long)shoulderDrop * (distanceDm - coreDm),
                shoulderWidth);
        }

        private static byte SurfaceDetail(
            uint seed,
            int xdm,
            int zdm,
            int distanceDm,
            int coreDm,
            int coverage31)
        {
            if (coverage31 < 31 || coreDm <= 0) return (byte)coverage31;
            int lateralPermille = math.clamp(distanceDm * 1000 / coreDm, 0, 1000);
            int band = lateralPermille >= 430 && lateralPermille <= 760 ? 6
                : lateralPermille <= 220 ? 3
                : 0;
            int breakup = (int)(Hash(seed, xdm, zdm) % 7u) - 3;
            return (byte)math.clamp(21 + band + breakup, 8, 31);
        }

        private static uint Hash(uint seed, int x, int z)
        {
            unchecked
            {
                uint h = seed ^ 0xA511E9B3u;
                h = (h ^ (uint)x) * 16777619u;
                h = (h ^ (uint)z) * 16777619u;
                h ^= h >> 13;
                h *= 0x85EBCA6Bu;
                return h ^ (h >> 16);
            }
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

        private static int DeterministicEdgeOffset(
            uint seed,
            int x,
            int z,
            int amplitude)
        {
            if (amplitude <= 0) return 0;

            int cellX = FloorDiv(x, EdgeNoiseCellDm);
            int cellZ = FloorDiv(z, EdgeNoiseCellDm);
            int localX = x - cellX * EdgeNoiseCellDm;
            int localZ = z - cellZ * EdgeNoiseCellDm;
            int v00 = EdgeNoiseValue(seed, cellX, cellZ, amplitude);
            int v10 = EdgeNoiseValue(seed, cellX + 1, cellZ, amplitude);
            int v01 = EdgeNoiseValue(seed, cellX, cellZ + 1, amplitude);
            int v11 = EdgeNoiseValue(seed, cellX + 1, cellZ + 1, amplitude);
            int x0 = LerpRounded(v00, v10, localX, EdgeNoiseCellDm);
            int x1 = LerpRounded(v01, v11, localX, EdgeNoiseCellDm);
            return LerpRounded(x0, x1, localZ, EdgeNoiseCellDm);
        }

        private static int EdgeNoiseValue(uint seed, int x, int z, int amplitude)
        {
            unchecked
            {
                uint h = seed ^ 0x9E3779B9u;
                h = (h ^ (uint)x) * 16777619u;
                h = (h ^ (uint)z) * 16777619u;
                return (int)(h % (uint)(amplitude * 2 + 1)) - amplitude;
            }
        }

        private static int LerpRounded(int a, int b, int numerator, int denominator)
            => a + DivideRounded((long)(b - a) * numerator, denominator);

        private static int FloorDiv(int value, int divisor)
        {
            int q = value / divisor;
            int r = value % divisor;
            return r != 0 && value < 0 ? q - 1 : q;
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
