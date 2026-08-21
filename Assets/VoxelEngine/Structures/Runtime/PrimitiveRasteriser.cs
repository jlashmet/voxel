using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Runtime.Emitters;
using VoxelEngine.Storage.Api;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>Outcome of rasterising a batch of primitives.</summary>
    public struct RasterResult
    {
        public int VoxelsWritten;
        public int PrimitivesRasterised;

        /// <summary>
        /// True when a batch exceeded <see cref="FeatureBudget.MaxPrimitivesPerRegion"/>.
        /// Generation reports the overflow rather than silently truncating geometry.
        /// </summary>
        public bool BudgetExceeded;
    }

    /// <summary>
    /// Turns primitives into voxels inside a sub-volume.
    ///
    /// Fill/carve membership is a pure function of world coordinate and primitive. Material-only
    /// paint modes additionally inspect existing occupancy, which is why they are intended for
    /// explicit ordered generation stages after their source terrain already exists.
    /// </summary>
    public static class PrimitiveRasteriser
    {
        // Four painted voxels align with the smooth renderer's four-voxel source step. Rendering
        // comparisons showed no useful gain from painting deeper, so keep the themed cap shallow
        // and leave mineral support immediately beneath it.
        private const int SurfacePaintDepth = 4;

        /// <summary>
        /// Rasterises primitives clipped to the half-open volume [subVolumeMin, subVolumeMax).
        ///
        /// Primitives are applied in order; later ones win where they overlap. PaintSolid changes
        /// material on existing solids only. PaintSurface finds the real highest solid in each
        /// horizontal column and repaints at most four contiguous solid voxels downward, preserving
        /// occupancy and leaving mineral support directly beneath biome ground cover.
        /// </summary>
        public static RasterResult Rasterise(
            NativeArray<Primitive> primitives,
            int3 subVolumeMin,
            int3 subVolumeMax,
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            bool markHardSurface = false)
        {
            var result = new RasterResult();

            if (primitives.Length > FeatureBudget.MaxPrimitivesPerRegion)
            {
                result.BudgetExceeded = true;
                return result;
            }

            for (var i = 0; i < primitives.Length; i++)
            {
                Primitive primitive = primitives[i];
                RasterResult primitiveResult = RasterisePrimitive(
                    in primitive, subVolumeMin, subVolumeMax,
                    reads, mutations, markHardSurface);
                result.VoxelsWritten += primitiveResult.VoxelsWritten;
                result.PrimitivesRasterised += primitiveResult.PrimitivesRasterised;
            }

            return result;
        }

        /// <summary>
        /// Rasterises one primitive clipped to a sub-volume. Streaming generation uses this entry
        /// point to partition a costly primitive into disjoint storage-block tiles; completing all
        /// tiles in order is voxel-for-voxel equivalent to the batch path above.
        /// </summary>
        public static RasterResult RasterisePrimitive(
            in Primitive primitive,
            int3 subVolumeMin,
            int3 subVolumeMax,
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            bool markHardSurface = false)
        {
            var result = new RasterResult();
            primitive.Bounds(out var min, out var max);
            bool hasBoundary = CurvedPrimitiveEmitter.TryBoundaryDistanceQ4(
                in primitive, primitive.A, out _);
            bool geometryIntersects = primitive.Intersects(subVolumeMin, subVolumeMax);
            bool boundaryIntersects = hasBoundary
                && math.all(min - 2 < subVolumeMax)
                && math.all(max + 2 >= subVolumeMin);
            if (!geometryIntersects && !boundaryIntersects) return result;

            int x0 = math.max(min.x, subVolumeMin.x), x1 = math.min(max.x, subVolumeMax.x - 1);
            int y0 = math.max(min.y, subVolumeMin.y), y1 = math.min(max.y, subVolumeMax.y - 1);
            int z0 = math.max(min.z, subVolumeMin.z), z1 = math.min(max.z, subVolumeMax.z - 1);

            if (primitive.Mode == PrimitiveMode.PaintSurface)
            {
                RasteriseSurfacePaint(in primitive,
                    x0, x1, z0, z1,
                    min.y, max.y,
                    subVolumeMin.y, subVolumeMax.y,
                    reads, mutations, ref result);
                result.PrimitivesRasterised++;
                return result;
            }

            RasterisePrimitiveBlocks(
                in primitive, x0, x1, y0, y1, z0, z1,
                reads, mutations, markHardSurface, ref result);

            if (primitive.Mode != PrimitiveMode.SurfaceDetail)
                RasteriseBoundaryHalo(in primitive, subVolumeMin, subVolumeMax,
                                      reads, mutations, ref result);

            result.PrimitivesRasterised++;
            return result;
        }

        private static void RasterisePrimitiveBlocks(
            in Primitive primitive,
            int x0, int x1, int y0, int y1, int z0, int z1,
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            bool markHardSurface,
            ref RasterResult result)
        {
            int3 blockMin = new int3(x0, y0, z0) >> VoxelReadGrid.BlockEdgeLog2;
            int3 blockMax = new int3(x1, y1, z1) >> VoxelReadGrid.BlockEdgeLog2;
            var read = new WorldReadCursor(reads);

            for (int bz = blockMin.z; bz <= blockMax.z; bz++)
            for (int by = blockMin.y; by <= blockMax.y; by++)
            for (int bx = blockMin.x; bx <= blockMax.x; bx++)
            {
                int3 worldBlock = new(bx, by, bz);
                int3 blockVoxelMin = worldBlock << VoxelReadGrid.BlockEdgeLog2;
                int bx0 = math.max(x0, blockVoxelMin.x);
                int bx1 = math.min(x1, blockVoxelMin.x + VoxelReadGrid.BlockEdgeMask);
                int by0 = math.max(y0, blockVoxelMin.y);
                int by1 = math.min(y1, blockVoxelMin.y + VoxelReadGrid.BlockEdgeMask);
                int bz0 = math.max(z0, blockVoxelMin.z);
                int bz1 = math.min(z1, blockVoxelMin.z + VoxelReadGrid.BlockEdgeMask);

                VoxelBlockMutation mutation = default;
                bool mutationOpen = false;
                bool payloadChanged = false;

                for (int z = bz0; z <= bz1; z++)
                for (int y = by0; y <= by1; y++)
                for (int x = bx0; x <= bx1; x++)
                {
                    var voxel = new int3(x, y, z);
                    bool contains = primitive.Mode == PrimitiveMode.SurfaceDetail
                        && primitive.Shape == PrimitiveShape.Capsule
                        ? CapsuleChainEmitter.ContainsQ4(in primitive, voxel, 8)
                        : Contains(in primitive, voxel);
                    if (!contains) continue;

                    int voxelIndex = VoxelIndex(voxel);
                    VoxelCell current = mutationOpen
                        ? mutation.GetCell(voxelIndex)
                        : read.ReadCell(voxel);

                    if (primitive.Mode == PrimitiveMode.FillIfEmpty && current.IsSolid)
                        continue;
                    if (primitive.Mode == PrimitiveMode.PaintSolid && !current.IsSolid)
                        continue;

                    VoxelCell next;
                    if (primitive.Mode == PrimitiveMode.SurfaceDetail)
                    {
                        if (!current.IsSolid) continue;
                        next = current;
                        if (primitive.SurfaceStyle != SurfaceStyles.MaterialDefault)
                            next.Surface.StyleId = primitive.SurfaceStyle;
                        next.Surface.Detail = (byte)math.min(31, primitive.SurfaceDetail);
                        next.Surface.Flags |= primitive.SurfaceFlags;
                    }
                    else if (primitive.Mode == PrimitiveMode.Carve)
                    {
                        next = default;
                    }
                    else if (primitive.Mode == PrimitiveMode.PaintSolid)
                    {
                        next = current;
                        next.BaseMaterialId = primitive.Material;
                    }
                    else
                    {
                        ushort style = primitive.SurfaceStyle;
                        if (markHardSurface && style == SurfaceStyles.MaterialDefault)
                            style = SurfaceStyles.Planar;
                        next = new VoxelCell
                        {
                            BaseMaterialId = primitive.Material,
                            Surface = new VoxelSurfaceSemantics
                            {
                                StyleId = style,
                                CoatingId = primitive.Coating,
                                Flags = primitive.SurfaceFlags,
                                Detail = (byte)math.min(31, primitive.SurfaceDetail)
                            }
                        };
                    }

                    if (current.Equals(next)) continue;

                    if (!mutationOpen)
                    {
                        // markHardSurface historically controls authored cell styling here. The
                        // region-level hard-surface bit is not added as part of this architecture
                        // cutover because that would change authoritative output.
                        if (!mutations.TryBeginCellBlock(worldBlock, false, out mutation))
                            continue;
                        mutationOpen = true;

                        // Re-read from the borrowed mutation payload so this remains correct if a
                        // preceding block operation changed the physical representation.
                        current = mutation.GetCell(voxelIndex);
                        if (primitive.Mode == PrimitiveMode.FillIfEmpty && current.IsSolid)
                            continue;
                        if (primitive.Mode == PrimitiveMode.PaintSolid && !current.IsSolid)
                            continue;

                        if (primitive.Mode == PrimitiveMode.SurfaceDetail)
                        {
                            if (!current.IsSolid) continue;
                            next = current;
                            if (primitive.SurfaceStyle != SurfaceStyles.MaterialDefault)
                                next.Surface.StyleId = primitive.SurfaceStyle;
                            next.Surface.Detail = (byte)math.min(31, primitive.SurfaceDetail);
                            next.Surface.Flags |= primitive.SurfaceFlags;
                        }
                        else if (primitive.Mode == PrimitiveMode.PaintSolid)
                        {
                            next = current;
                            next.BaseMaterialId = primitive.Material;
                        }
                    }

                    if (mutation.SetCell(voxelIndex, in next))
                    {
                        payloadChanged = true;
                        result.VoxelsWritten++;
                    }
                }

                if (mutationOpen)
                    mutations.CompletePartialBlock(ref mutation, payloadChanged);
            }
        }

        private static void RasteriseSurfacePaint(
            in Primitive primitive,
            int x0, int x1, int z0, int z1,
            int primitiveMinY, int primitiveMaxY,
            int subVolumeMinY, int subVolumeMaxY,
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            ref RasterResult result)
        {
            var read = new WorldReadCursor(reads);
            var writes = new MaterialMutationCursor(mutations);

            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                // Search the primitive's full Y extent rather than the clipped sub-volume extent.
                // If vertical sub-volumes are ever used, every slice therefore agrees on which
                // voxel is the column surface; a lower slice cannot mistake an internal solid for
                // the surface merely because the real top lives in the slice above it.
                for (int y = primitiveMaxY; y >= primitiveMinY; y--)
                {
                    var top = new int3(x, y, z);
                    if (!Contains(in primitive, top)) continue;
                    if (!read.ReadCell(top).IsSolid) continue;

                    for (int depth = 0; depth < SurfacePaintDepth; depth++)
                    {
                        int paintY = y - depth;
                        if (paintY < primitiveMinY) break;

                        var voxel = new int3(x, paintY, z);
                        if (!Contains(in primitive, voxel)) break;
                        if (!read.ReadCell(voxel).IsSolid) break;

                        if (paintY < subVolumeMinY || paintY >= subVolumeMaxY) continue;
                        if (writes.SetMaterial(voxel, primitive.Material))
                            result.VoxelsWritten++;
                    }

                    break;
                }
            }

            writes.Flush();
        }

        private static void RasteriseBoundaryHalo(
            in Primitive primitive, int3 subVolumeMin, int3 subVolumeMax,
            IRegionReadSource reads, IRegionMutationStore mutations, ref RasterResult result)
        {
            if (!CurvedPrimitiveEmitter.TryBoundaryDistanceQ4(
                    in primitive, primitive.A, out _)) return;

            primitive.Bounds(out int3 boundsMin, out int3 boundsMax);
            int3 min = math.max(boundsMin - 2, subVolumeMin);
            int3 max = math.min(boundsMax + 2, subVolumeMax - 1);
            int3 blockMin = min >> VoxelReadGrid.BlockEdgeLog2;
            int3 blockMax = max >> VoxelReadGrid.BlockEdgeLog2;
            var read = new WorldReadCursor(reads);

            for (int bz = blockMin.z; bz <= blockMax.z; bz++)
            for (int by = blockMin.y; by <= blockMax.y; by++)
            for (int bx = blockMin.x; bx <= blockMax.x; bx++)
            {
                int3 worldBlock = new(bx, by, bz);
                int3 blockVoxelMin = worldBlock << VoxelReadGrid.BlockEdgeLog2;
                int bx0 = math.max(min.x, blockVoxelMin.x);
                int bx1 = math.min(max.x, blockVoxelMin.x + VoxelReadGrid.BlockEdgeMask);
                int by0 = math.max(min.y, blockVoxelMin.y);
                int by1 = math.min(max.y, blockVoxelMin.y + VoxelReadGrid.BlockEdgeMask);
                int bz0 = math.max(min.z, blockVoxelMin.z);
                int bz1 = math.min(max.z, blockVoxelMin.z + VoxelReadGrid.BlockEdgeMask);

                VoxelBlockMutation mutation = default;
                bool mutationOpen = false;
                bool payloadChanged = false;

                for (int z = bz0; z <= bz1; z++)
                for (int y = by0; y <= by1; y++)
                for (int x = bx0; x <= bx1; x++)
                {
                    int3 voxel = new(x, y, z);
                    if (!CurvedPrimitiveEmitter.TryBoundaryDistanceQ4(
                            in primitive, voxel, out int shapeDistanceQ4)) continue;
                    if (math.abs(shapeDistanceQ4) > 32) continue;

                    int solidDistanceQ4 = primitive.Mode == PrimitiveMode.Carve
                        ? -shapeDistanceQ4 : shapeDistanceQ4;
                    int voxelIndex = VoxelIndex(voxel);
                    VoxelCell current = mutationOpen
                        ? mutation.GetCell(voxelIndex)
                        : read.ReadCell(voxel);
                    bool signMatchesOccupancy = current.IsSolid
                        ? solidDistanceQ4 >= 0 : solidDistanceQ4 <= 0;
                    if (!signMatchesOccupancy) continue;

                    if (primitive.Mode != PrimitiveMode.Carve && current.IsSolid
                        && current.BaseMaterialId != primitive.Material) continue;

                    if (solidDistanceQ4 == 0)
                        solidDistanceQ4 = current.IsSolid ? 1 : -1;

                    int extrusionAxis = primitive.Shape == PrimitiveShape.Annulus
                        || primitive.Shape == PrimitiveShape.ArcWedge
                        || primitive.Shape == PrimitiveShape.Frustum
                        || primitive.Shape == PrimitiveShape.RoundedBox && primitive.Axis <= 2
                        ? primitive.Axis : 3;
                    VoxelBoundarySample boundary =
                        VoxelBoundarySample.FromSignedQ4(solidDistanceQ4, extrusionAxis);
                    if (current.Boundary.IsAuthored)
                    {
                        int existingDistanceQ4 = current.Boundary.SignedQ4;
                        bool candidateWins = primitive.Mode == PrimitiveMode.Carve
                            ? solidDistanceQ4 < existingDistanceQ4
                            : solidDistanceQ4 > existingDistanceQ4;
                        if (!candidateWins) continue;
                    }
                    if (current.Boundary.Equals(boundary)) continue;

                    if (!mutationOpen)
                    {
                        if (!mutations.TryBeginCellBlock(worldBlock, false, out mutation))
                            continue;
                        mutationOpen = true;
                        current = mutation.GetCell(voxelIndex);

                        bool recheckSign = current.IsSolid
                            ? solidDistanceQ4 >= 0 : solidDistanceQ4 <= 0;
                        if (!recheckSign) continue;
                        if (primitive.Mode != PrimitiveMode.Carve && current.IsSolid
                            && current.BaseMaterialId != primitive.Material) continue;
                        if (current.Boundary.IsAuthored)
                        {
                            int existingDistanceQ4 = current.Boundary.SignedQ4;
                            bool candidateWins = primitive.Mode == PrimitiveMode.Carve
                                ? solidDistanceQ4 < existingDistanceQ4
                                : solidDistanceQ4 > existingDistanceQ4;
                            if (!candidateWins) continue;
                        }
                        if (current.Boundary.Equals(boundary)) continue;
                    }

                    current.Boundary = boundary;
                    if (mutation.SetCell(voxelIndex, in current))
                    {
                        payloadChanged = true;
                        result.VoxelsWritten++;
                    }
                }

                if (mutationOpen)
                    mutations.CompletePartialBlock(ref mutation, payloadChanged);
            }
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
                    _hasView = _source != null && _source.TryAcquireRegion(regionCoord, out _view);
                }

                if (!_hasView) return default;
                int3 localVoxel = worldVoxel - (regionCoord << VoxelGrid.RegionVoxelEdgeLog2);
                return _view.TryReadCell(localVoxel, out VoxelCell cell) ? cell : default;
            }
        }

        private struct MaterialMutationCursor
        {
            private readonly IRegionMutationStore _store;
            private int3 _worldBlock;
            private VoxelBlockMutation _mutation;
            private bool _hasBlock;
            private bool _payloadChanged;

            public MaterialMutationCursor(IRegionMutationStore store)
            {
                _store = store;
                _worldBlock = default;
                _mutation = default;
                _hasBlock = false;
                _payloadChanged = false;
            }

            public bool SetMaterial(int3 worldVoxel, byte material)
            {
                int3 worldBlock = worldVoxel >> VoxelReadGrid.BlockEdgeLog2;
                if (!_hasBlock || math.any(worldBlock != _worldBlock))
                {
                    Flush();
                    if (_store == null
                        || !_store.TryBeginPartialBlock(worldBlock, material, false, out _mutation))
                        return false;
                    _worldBlock = worldBlock;
                    _hasBlock = true;
                    _payloadChanged = false;
                }

                if (!_mutation.IsCreated) return false;
                bool changed = _mutation.SetMaterial(VoxelIndex(worldVoxel), material);
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

        /// <summary>
        /// Membership test, dispatched by shape.
        ///
        /// A pure function of the primitive and the world coordinate — deliberately taking no
        /// sub-volume, so it is impossible to write a shape whose answer depends on which region
        /// is asking.
        /// </summary>
        public static bool Contains(in Primitive primitive, int3 voxel)
        {
            switch (primitive.Shape)
            {
                case PrimitiveShape.Box: return BoxEmitter.BoxContains(in primitive, voxel);
                case PrimitiveShape.Ramp: return BoxEmitter.RampContains(in primitive, voxel);
                case PrimitiveShape.Cylinder: return CylinderEmitter.Contains(in primitive, voxel);
                case PrimitiveShape.Prism: return PrismEmitter.Contains(in primitive, voxel);
                case PrimitiveShape.Capsule: return CapsuleChainEmitter.Contains(in primitive, voxel);
                case PrimitiveShape.RoundedBox:
                case PrimitiveShape.Ellipsoid:
                case PrimitiveShape.Frustum:
                case PrimitiveShape.Annulus:
                case PrimitiveShape.ArcWedge:
                    return CurvedPrimitiveEmitter.Contains(in primitive, voxel);
                default: return false;
            }
        }

        /// <summary>
        /// Counts geometric membership only. Material-dependent paint modes cannot know their
        /// actual write count without storage, so this remains the authoring geometry-cost query.
        /// </summary>
        public static int CountVoxels(NativeArray<Primitive> primitives, int3 min, int3 max)
        {
            int count = 0;

            for (var i = 0; i < primitives.Length; i++)
            {
                var primitive = primitives[i];
                if (!primitive.Intersects(min, max)) continue;

                primitive.Bounds(out var pMin, out var pMax);

                int x0 = math.max(pMin.x, min.x), x1 = math.min(pMax.x, max.x - 1);
                int y0 = math.max(pMin.y, min.y), y1 = math.min(pMax.y, max.y - 1);
                int z0 = math.max(pMin.z, min.z), z1 = math.min(pMax.z, max.z - 1);

                for (int z = z0; z <= z1; z++)
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (Contains(in primitive, new int3(x, y, z))) count++;
            }

            return count;
        }
    }
}
