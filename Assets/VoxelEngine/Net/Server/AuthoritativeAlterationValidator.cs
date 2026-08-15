using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Stateless validation of a server-materialized AlterationEvent.
    /// Bounds and budget estimates use the same canonical shape semantics as the shared applier.
    /// </summary>
    public static class AuthoritativeAlterationValidator
    {
        public static Validation.ValidationResult Validate(
            in AlterationEvent evt,
            in ServerPlayerRegistry.PlayerSession player,
            ServerPlayerRegistry players,
            IRegionMutationStore mutationStorage,
            ref RegionTable table,
            in BrickPool pool,
            Validation.DensityCap densityCap,
            in ProtectedZones zones = default)
        {
            if (!evt.Validate() || evt.playerId != player.PlayerId || !player.CanAlterWorld ||
                !DeterministicAlterationApplier.Supports(in evt))
                return Validation.ValidationResult.InvalidTarget;

            int estimatedBricks = EstimateAffectedBricks(in evt);
            if (estimatedBricks <= 0 || estimatedBricks > Validation.k_MaxBricksPerTick)
                return Validation.ValidationResult.OverBudget;

            if (!IsWithinReach(evt.origin, player.PositionVoxels, player.ReachVoxels))
                return Validation.ValidationResult.OutOfReach;

            GetVoxelBounds(in evt, out int3 minVoxel, out int3 maxVoxel);

            // Fail before expensive placement checks and before the applier has a chance to see a
            // partially resident effect. Missing streaming state is not a valid mutation target.
            if (!DeterministicAlterationApplier.HasRequiredResidency(mutationStorage, in evt))
                return Validation.ValidationResult.InvalidTarget;

            if (zones.IsCreated && zones.IntersectsProtected(minVoxel, maxVoxel))
                return Validation.ValidationResult.ProtectedZone;

            bool constructive = IsConstructive(in evt);
            if (constructive && players != null && players.IntersectsPlayerVolume(minVoxel, maxVoxel))
                return Validation.ValidationResult.InPlayerVolume;

            if (constructive && !HasAttachment(minVoxel, maxVoxel, ref table, in pool))
                return Validation.ValidationResult.NotAttached;

            if (constructive && WouldExceedDensity(in evt, estimatedBricks, ref table, densityCap))
                return Validation.ValidationResult.OverDensity;

            return Validation.ValidationResult.Success;
        }

        public static int EstimateAffectedBricks(in AlterationEvent evt)
        {
            switch (evt.kind)
            {
                case AlterationEvent.KindExplosion:
                {
                    long r = evt.Radius();
                    long estimate = (419L * r * r * r + 99L) / 100L;
                    return estimate > int.MaxValue ? int.MaxValue : (int)estimate;
                }

                case AlterationEvent.KindBrush when BrushShapeCodec.Validate(evt.shapeKind, evt.shapeData):
                {
                    BrushShapeCodec.GetCubeVoxelBounds(
                        evt.origin,
                        evt.BrushExtents(),
                        out int3 minVoxel,
                        out int3 maxVoxel);
                    int3 minBrick = minVoxel >> VoxelDimensions.BrickEdgeLog2;
                    int3 maxBrick = maxVoxel >> VoxelDimensions.BrickEdgeLog2;
                    long sx = (long)maxBrick.x - minBrick.x + 1;
                    long sy = (long)maxBrick.y - minBrick.y + 1;
                    long sz = (long)maxBrick.z - minBrick.z + 1;
                    long estimate = sx * sy * sz;
                    return estimate > int.MaxValue ? int.MaxValue : (int)estimate;
                }

                case AlterationEvent.KindRawBatch:
                    return (int)(evt.shapeData & 0xFFFFu);

                default:
                    return 0;
            }
        }

        public static void GetVoxelBounds(in AlterationEvent evt, out int3 minVoxel, out int3 maxVoxel)
        {
            switch (evt.kind)
            {
                case AlterationEvent.KindExplosion:
                {
                    int radiusVoxels = evt.Radius() * VoxelDimensions.BrickEdge;
                    int3 padding = new int3(radiusVoxels);
                    minVoxel = evt.origin - padding;
                    maxVoxel = evt.origin + padding;
                    return;
                }

                case AlterationEvent.KindBrush when BrushShapeCodec.Validate(evt.shapeKind, evt.shapeData):
                    BrushShapeCodec.GetCubeVoxelBounds(
                        evt.origin,
                        evt.BrushExtents(),
                        out minVoxel,
                        out maxVoxel);
                    return;

                case AlterationEvent.KindRawBatch:
                {
                    int3 padding = new int3(VoxelDimensions.BrickEdge);
                    minVoxel = evt.origin - padding;
                    maxVoxel = evt.origin + padding;
                    return;
                }

                default:
                    minVoxel = evt.origin;
                    maxVoxel = evt.origin;
                    return;
            }
        }

        private static bool IsWithinReach(int3 target, int3 playerPosition, int reachVoxels)
        {
            long dx = (long)target.x - playerPosition.x;
            long dy = (long)target.y - playerPosition.y;
            long dz = (long)target.z - playerPosition.z;
            long reachSq = (long)reachVoxels * reachVoxels;
            return dx * dx + dy * dy + dz * dz <= reachSq;
        }

        private static bool IsConstructive(in AlterationEvent evt) =>
            evt.kind != AlterationEvent.KindExplosion && evt.material != VoxelDimensions.MaterialEmpty;

        /// <summary>
        /// A placement is attached when any voxel immediately outside one of its six faces is solid.
        /// The previous implementation sampled only face centers and rejected valid edge/corner
        /// attachment. This scans the actual boundary and exits on the first supporting voxel.
        /// </summary>
        private static bool HasAttachment(
            int3 minVoxel,
            int3 maxVoxel,
            ref RegionTable table,
            in BrickPool pool)
        {
            for (int y = minVoxel.y; y <= maxVoxel.y; y++)
            for (int z = minVoxel.z; z <= maxVoxel.z; z++)
            {
                if (IsSolidAtVoxel(ref table, in pool, new int3(minVoxel.x - 1, y, z))) return true;
                if (IsSolidAtVoxel(ref table, in pool, new int3(maxVoxel.x + 1, y, z))) return true;
            }

            for (int x = minVoxel.x; x <= maxVoxel.x; x++)
            for (int z = minVoxel.z; z <= maxVoxel.z; z++)
            {
                if (IsSolidAtVoxel(ref table, in pool, new int3(x, minVoxel.y - 1, z))) return true;
                if (IsSolidAtVoxel(ref table, in pool, new int3(x, maxVoxel.y + 1, z))) return true;
            }

            for (int x = minVoxel.x; x <= maxVoxel.x; x++)
            for (int y = minVoxel.y; y <= maxVoxel.y; y++)
            {
                if (IsSolidAtVoxel(ref table, in pool, new int3(x, y, minVoxel.z - 1))) return true;
                if (IsSolidAtVoxel(ref table, in pool, new int3(x, y, maxVoxel.z + 1))) return true;
            }

            return false;
        }

        private static bool WouldExceedDensity(
            in AlterationEvent evt,
            int estimatedBricks,
            ref RegionTable table,
            Validation.DensityCap densityCap)
        {
            if (densityCap.totalBricks <= 0)
                return false;

            int3 regionCoord = evt.origin >> VoxelDimensions.RegionVoxelEdgeLog2;
            if (!table.TryGetRegion(regionCoord, out Region region) || !region.BrickRefs.IsCreated)
                return false;

            int currentMixed = 0;
            for (int i = 0; i < region.BrickRefs.Length; i++)
                if (region.BrickRefs[i].IsMixed)
                    currentMixed++;

            return currentMixed + estimatedBricks > densityCap.MaxMixedBricks();
        }

        private static bool IsSolidAtVoxel(ref RegionTable table, in BrickPool pool, int3 voxelCoord)
        {
            int3 brickCoord = voxelCoord >> VoxelDimensions.BrickEdgeLog2;
            int3 regionCoord = brickCoord >> VoxelDimensions.RegionEdgeLog2;

            if (!table.TryGetRegion(regionCoord, out Region region) || !region.BrickRefs.IsCreated)
                return false;

            int brickIndex = Region.BrickIndex(
                brickCoord.x & VoxelDimensions.RegionEdgeMask,
                brickCoord.y & VoxelDimensions.RegionEdgeMask,
                brickCoord.z & VoxelDimensions.RegionEdgeMask);

            BrickRef brickRef = region.BrickRefs[brickIndex];
            if (!brickRef.IsMixed)
                return brickRef.UniformMaterial != VoxelDimensions.MaterialEmpty;

            int localX = voxelCoord.x & VoxelDimensions.BrickEdgeMask;
            int localY = voxelCoord.y & VoxelDimensions.BrickEdgeMask;
            int localZ = voxelCoord.z & VoxelDimensions.BrickEdgeMask;
            int voxelIndex = localX |
                             (localY << VoxelDimensions.BrickEdgeLog2) |
                             (localZ << (VoxelDimensions.BrickEdgeLog2 * 2));

            return pool.GetVoxel(brickRef.PoolIndex, voxelIndex) != VoxelDimensions.MaterialEmpty;
        }
    }
}
