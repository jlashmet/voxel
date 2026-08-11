using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features.Emitters;
using VoxelEngine.Core.Storage;

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
                if (!primitive.Intersects(subVolumeMin, subVolumeMax)) continue;

                primitive.Bounds(out var min, out var max);

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

                bool hardWrite = markHardSurface
                              && primitive.Mode != PrimitiveMode.Carve
                              && primitive.Mode != PrimitiveMode.PaintSolid;

                for (int z = z0; z <= z1; z++)
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var voxel = new int3(x, y, z);
                    if (!Contains(in primitive, voxel)) continue;

                    if (primitive.Mode == PrimitiveMode.FillIfEmpty
                        && VoxelAccess.IsSolid(ref table, in pool, voxel))
                        continue;

                    if (primitive.Mode == PrimitiveMode.PaintSolid
                        && !VoxelAccess.IsSolid(ref table, in pool, voxel))
                        continue;

                    byte material = primitive.Mode == PrimitiveMode.Carve
                        ? VoxelDimensions.MaterialEmpty
                        : primitive.Material;

                    if (VoxelAccess.SetVoxel(ref table, ref pool, voxel, material, hardWrite))
                        result.VoxelsWritten++;
                }

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
                                                 primitive.Material, false))
                            result.VoxelsWritten++;
                    }

                    break;
                }
            }
        }

        /// <summary>Membership test, dispatched by shape.</summary>
        public static bool Contains(in Primitive primitive, int3 voxel)
        {
            switch (primitive.Shape)
            {
                case PrimitiveShape.Box: return BoxEmitter.BoxContains(in primitive, voxel);
                case PrimitiveShape.Ramp: return BoxEmitter.RampContains(in primitive, voxel);
                case PrimitiveShape.Cylinder: return CylinderEmitter.Contains(in primitive, voxel);
                case PrimitiveShape.Prism: return PrismEmitter.Contains(in primitive, voxel);
                case PrimitiveShape.Capsule: return CapsuleChainEmitter.Contains(in primitive, voxel);
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
