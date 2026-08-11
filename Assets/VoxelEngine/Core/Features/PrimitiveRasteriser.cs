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
        /// True when the batch exceeded <see cref="FeatureBudget.MaxPrimitivesPerRegion"/>.
        ///
        /// Reported rather than truncated (FR-036). Silently dropping primitives produces a
        /// half-built castle that looks like a design choice, and the region that dropped them
        /// would differ from the region next door that did not.
        /// </summary>
        public bool BudgetExceeded;
    }

    /// <summary>
    /// Turns primitives into voxels inside a sub-volume.
    ///
    /// The guarantee this type exists to provide: **rasterising a set of primitives into disjoint
    /// sub-volumes that tile a region produces exactly the same voxels as rasterising them into
    /// the region at once.** A castle spanning four regions is generated four times, once per
    /// region, and the seams have to line up without the regions communicating.
    ///
    /// That guarantee is structural rather than tested-in. Membership — "is this voxel inside this
    /// primitive?" — is a pure function of the world voxel coordinate and the primitive, with no
    /// reference to the volume being filled. Clipping therefore cannot change the answer for any
    /// voxel, only which voxels get asked.
    ///
    /// Writes go through <see cref="VoxelAccess.SetVoxel"/>, the same path edits and terrain use,
    /// so brick allocation, collapse-to-uniform, and dirty tracking behave identically for a
    /// generated castle and for a player's wall.
    /// </summary>
    public static class PrimitiveRasteriser
    {
        /// <summary>
        /// Rasterises primitives clipped to the half-open volume [subVolumeMin, subVolumeMax).
        ///
        /// Primitives are applied in the order given; later ones win where they overlap, which is
        /// how a window carves the wall that was filled a moment earlier.
        ///
        /// <see cref="PrimitiveMode.PaintSolid"/> is occupancy-preserving: empty voxels remain
        /// empty and existing solids change material only. This lets biome/theme passes repaint a
        /// surface without changing collision, density, caves, or terrain silhouette.
        ///
        /// When <paramref name="markHardSurface"/> is true, authored solid writes also tag the
        /// containing brick as hard architecture. Carves deliberately do not create hard semantics
        /// on their own: a structure fill already tagged those bricks, while a carve into natural
        /// terrain should not magically turn the exposed terrain into masonry.
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

                // Clip to the sub-volume. The half-open upper bound matches how regions tile, so
                // a voxel on a shared face belongs to exactly one side.
                int x0 = math.max(min.x, subVolumeMin.x), x1 = math.min(max.x, subVolumeMax.x - 1);
                int y0 = math.max(min.y, subVolumeMin.y), y1 = math.min(max.y, subVolumeMax.y - 1);
                int z0 = math.max(min.z, subVolumeMin.z), z1 = math.min(max.z, subVolumeMax.z - 1);

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
                default: return false;
            }
        }

        /// <summary>
        /// Voxels a batch would write inside a volume, without writing them.
        ///
        /// Used by validation and by the authoring preview, where the question is "how much does
        /// this cost" rather than "put it in the world".
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
