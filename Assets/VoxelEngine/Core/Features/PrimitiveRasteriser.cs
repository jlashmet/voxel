using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features.Emitters;
using VoxelEngine.Core.Storage;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Core.Features
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
            ref RegionTable table,
            ref BrickPool pool,
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
                var primitive = primitives[i];
                primitive.Bounds(out var min, out var max);
                bool hasBoundary = CurvedPrimitiveEmitter.TryBoundaryDistanceQ4(
                    in primitive, primitive.A, out _);
                bool geometryIntersects = primitive.Intersects(subVolumeMin, subVolumeMax);
                bool boundaryIntersects = hasBoundary
                    && math.all(min - 2 < subVolumeMax)
                    && math.all(max + 2 >= subVolumeMin);
                if (!geometryIntersects && !boundaryIntersects) continue;

                int x0 = math.max(min.x, subVolumeMin.x), x1 = math.min(max.x, subVolumeMax.x - 1);
                int y0 = math.max(min.y, subVolumeMin.y), y1 = math.min(max.y, subVolumeMax.y - 1);
                int z0 = math.max(min.z, subVolumeMin.z), z1 = math.min(max.z, subVolumeMax.z - 1);

                if (primitive.Mode == PrimitiveMode.PaintSurface)
                {
                    RasteriseSurfacePaint(in primitive,
                        x0, x1, z0, z1,
                        min.y, max.y,
                        subVolumeMin.y, subVolumeMax.y,
                        ref table, ref pool, ref result);
                    result.PrimitivesRasterised++;
                    continue;
                }

                for (int z = z0; z <= z1; z++)
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var voxel = new int3(x, y, z);
                    bool contains = primitive.Mode == PrimitiveMode.SurfaceDetail
                        && primitive.Shape == PrimitiveShape.Capsule
                        ? CapsuleChainEmitter.ContainsQ4(in primitive, voxel, 8)
                        : Contains(in primitive, voxel);
                    if (!contains) continue;

                    if (primitive.Mode == PrimitiveMode.FillIfEmpty
                        && VoxelAccess.IsSolid(ref table, in pool, voxel))
                        continue;

                    if (primitive.Mode == PrimitiveMode.PaintSolid
                        && !VoxelAccess.IsSolid(ref table, in pool, voxel))
                        continue;

                    if (primitive.Mode == PrimitiveMode.SurfaceDetail)
                    {
                        VoxelCell current = VoxelAccess.GetCell(ref table, in pool, voxel);
                        if (!current.IsSolid) continue;
                        if (primitive.SurfaceStyle != SurfaceStyles.MaterialDefault)
                            current.Surface.StyleId = primitive.SurfaceStyle;
                        current.Surface.Detail = (byte)math.min(31, primitive.SurfaceDetail);
                        current.Surface.Flags |= primitive.SurfaceFlags;
                        if (VoxelAccess.SetCell(ref table, ref pool, voxel, in current))
                            result.VoxelsWritten++;
                        continue;
                    }

                    VoxelCell cell;
                    if (primitive.Mode == PrimitiveMode.Carve)
                    {
                        cell = default;
                    }
                    else if (primitive.Mode == PrimitiveMode.PaintSolid)
                    {
                        cell = VoxelAccess.GetCell(ref table, in pool, voxel);
                        cell.BaseMaterialId = primitive.Material;
                    }
                    else
                    {
                        ushort style = primitive.SurfaceStyle;
                        if (markHardSurface && style == SurfaceStyles.MaterialDefault)
                            style = SurfaceStyles.Planar;
                        cell = new VoxelCell
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

                    if (VoxelAccess.SetCell(ref table, ref pool, voxel, in cell))
                        result.VoxelsWritten++;
                }

                if (primitive.Mode != PrimitiveMode.SurfaceDetail)
                    RasteriseBoundaryHalo(in primitive, subVolumeMin, subVolumeMax,
                                          ref table, ref pool, ref result);

                result.PrimitivesRasterised++;
            }

            return result;
        }

        private static void RasteriseSurfacePaint(
            in Primitive primitive,
            int x0, int x1, int z0, int z1,
            int primitiveMinY, int primitiveMaxY,
            int subVolumeMinY, int subVolumeMaxY,
            ref RegionTable table, ref BrickPool pool,
            ref RasterResult result)
        {
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
                    if (!VoxelAccess.IsSolid(ref table, in pool, top)) continue;

                    for (int depth = 0; depth < SurfacePaintDepth; depth++)
                    {
                        int paintY = y - depth;
                        if (paintY < primitiveMinY) break;

                        var voxel = new int3(x, paintY, z);
                        if (!Contains(in primitive, voxel)) break;
                        if (!VoxelAccess.IsSolid(ref table, in pool, voxel)) break;

                        if (paintY < subVolumeMinY || paintY >= subVolumeMaxY) continue;
                        if (VoxelAccess.SetVoxel(ref table, ref pool, voxel,
                                                 primitive.Material))
                            result.VoxelsWritten++;
                    }

                    break;
                }
            }
        }

        private static void RasteriseBoundaryHalo(
            in Primitive primitive, int3 subVolumeMin, int3 subVolumeMax,
            ref RegionTable table, ref BrickPool pool, ref RasterResult result)
        {
            if (!CurvedPrimitiveEmitter.TryBoundaryDistanceQ4(
                    in primitive, primitive.A, out _)) return;

            primitive.Bounds(out int3 boundsMin, out int3 boundsMax);
            int3 min = math.max(boundsMin - 2, subVolumeMin);
            int3 max = math.min(boundsMax + 2, subVolumeMax - 1);
            for (int z = min.z; z <= max.z; z++)
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                int3 voxel = new(x, y, z);
                if (!CurvedPrimitiveEmitter.TryBoundaryDistanceQ4(
                        in primitive, voxel, out int shapeDistanceQ4)) continue;
                if (math.abs(shapeDistanceQ4) > 32) continue;

                // A carve inverts the primitive field: inside the carved volume is outside solid.
                int solidDistanceQ4 = primitive.Mode == PrimitiveMode.Carve
                    ? -shapeDistanceQ4 : shapeDistanceQ4;
                VoxelCell current = VoxelAccess.GetCell(ref table, in pool, voxel);
                bool signMatchesOccupancy = current.IsSolid
                    ? solidDistanceQ4 >= 0 : solidDistanceQ4 <= 0;
                if (!signMatchesOccupancy) continue;

                // Fill constraints belong only to cells written with the primitive's material.
                // This prevents an overlapping decorative primitive from reshaping foreign solids.
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
                current.Boundary = boundary;
                if (VoxelAccess.SetCell(ref table, ref pool, voxel, in current))
                    result.VoxelsWritten++;
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
