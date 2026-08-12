using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Stateless validation of a server-materialized AlterationEvent.
    ///
    /// Rate/allocation counters are queried outside this type and committed only after application.
    /// All spatial facts come from authoritative server state; client packets never provide identity,
    /// player position, reach, collision bounds, or edit permission.
    /// </summary>
    public static class AuthoritativeAlterationValidator
    {
        public static Validation.ValidationResult Validate(
            in AlterationEvent evt,
            in ServerPlayerRegistry.PlayerSession player,
            ServerPlayerRegistry players,
            ref RegionTable table,
            in BrickPool pool,
            Validation.DensityCap densityCap,
            in ProtectedZones zones = default)
        {
            if (!evt.Validate() || evt.playerId != player.PlayerId || !player.CanAlterWorld)
                return Validation.ValidationResult.InvalidTarget;

            int estimatedBricks = EstimateAffectedBricks(in evt);
            if (estimatedBricks <= 0 || estimatedBricks > Validation.k_MaxBricksPerTick)
                return Validation.ValidationResult.OverBudget;

            if (!IsWithinReach(evt.origin, player.PositionVoxels, player.ReachVoxels))
                return Validation.ValidationResult.OutOfReach;

            GetVoxelBounds(in evt, out int3 minVoxel, out int3 maxVoxel);

            // Check the whole deterministic effect bounds, not only the origin. The budget guard
            // above keeps this bounded even with the current sparse-zone implementation.
            if (zones.IsCreated && zones.IntersectsProtected(minVoxel, maxVoxel))
                return Validation.ValidationResult.ProtectedZone;

            // Destruction may pass through a player volume; constructive placement may not create
            // solid matter intersecting any authoritative collision volume.
            if (IsConstructive(in evt) && players != null && players.IntersectsPlayerVolume(minVoxel, maxVoxel))
                return Validation.ValidationResult.InPlayerVolume;

            if (IsConstructive(in evt) && !HasAttachment(in evt, minVoxel, maxVoxel, ref table, in pool))
                return Validation.ValidationResult.NotAttached;

            if (WouldExceedDensity(in evt, estimatedBricks, ref table, densityCap))
                return Validation.ValidationResult.OverDensity;

            return Validation.ValidationResult.Success;
        }

        public static int EstimateAffectedBricks(in AlterationEvent evt)
        {
            long estimate;
            switch (evt.kind)
            {
                case AlterationEvent.KindExplosion:
                {
                    long r = evt.Radius();
                    // Conservative integer sphere volume upper bound: ceil(4*pi*r^3/3).
                    estimate = (419L * r * r * r + 99L) / 100L;
                    break;
                }
                case AlterationEvent.KindBrush:
                {
                    int3 e = evt.BrushExtents();
                    estimate = (long)e.x * e.y * e.z;
                    break;
                }
                case AlterationEvent.KindRawBatch:
                    estimate = evt.shapeData & 0xFFFFu;
                    break;
                default:
                    return 0;
            }

            return estimate > int.MaxValue ? int.MaxValue : (int)estimate;
        }

        public static void GetVoxelBounds(in AlterationEvent evt, out int3 minVoxel, out int3 maxVoxel)
        {
            int3 padding;
            switch (evt.kind)
            {
                case AlterationEvent.KindExplosion:
                {
                    int radiusVoxels = evt.Radius() * VoxelDimensions.BrickEdge;
                    padding = new int3(radiusVoxels);
                    break;
                }
                case AlterationEvent.KindBrush:
                    padding = evt.BrushExtents() * VoxelDimensions.BrickEdge;
                    break;
                case AlterationEvent.KindRawBatch:
                    // Raw-batch exact bounds are not canonical yet. One brick around the origin is
                    // the smallest safe validation envelope until the payload exposes its run bounds.
                    padding = new int3(VoxelDimensions.BrickEdge);
                    break;
                default:
                    padding = int3.zero;
                    break;
            }

            minVoxel = evt.origin - padding;
            maxVoxel = evt.origin + padding;
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

        private static bool HasAttachment(
            in AlterationEvent evt,
            int3 minVoxel,
            int3 maxVoxel,
            ref RegionTable table,
            in BrickPool pool)
        {
            // A placement is attached if any one face-center just outside its validation bounds is
            // already solid. This is intentionally conservative and integer-only; exact brush-contact
            // testing can replace it when the canonical brush expansion exposes its boundary cheaply.
            int3 center = (minVoxel + maxVoxel) / 2;
            int3[] samples =
            {
                new int3(minVoxel.x - 1, center.y, center.z),
                new int3(maxVoxel.x + 1, center.y, center.z),
                new int3(center.x, minVoxel.y - 1, center.z),
                new int3(center.x, maxVoxel.y + 1, center.z),
                new int3(center.x, center.y, minVoxel.z - 1),
                new int3(center.x, center.y, maxVoxel.z + 1),
            };

            for (int i = 0; i < samples.Length; i++)
            {
                if (IsSolidAtVoxel(ref table, in pool, samples[i]))
                    return true;
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

            int3 regionCoord = new int3(
                evt.origin.x >> VoxelDimensions.RegionVoxelEdgeLog2,
                evt.origin.y >> VoxelDimensions.RegionVoxelEdgeLog2,
                evt.origin.z >> VoxelDimensions.RegionVoxelEdgeLog2);

            if (!table.TryGetRegion(regionCoord, out Region region) || !region.BrickRefs.IsCreated)
                return false;

            int currentMixed = 0;
            for (int i = 0; i < region.BrickRefs.Length; i++)
            {
                if (region.BrickRefs[i].IsMixed)
                    currentMixed++;
            }

            return currentMixed + estimatedBricks > densityCap.MaxMixedBricks();
        }

        private static bool IsSolidAtVoxel(ref RegionTable table, in BrickPool pool, int3 voxelCoord)
        {
            int3 brickCoord = new int3(
                voxelCoord.x >> VoxelDimensions.BrickEdgeLog2,
                voxelCoord.y >> VoxelDimensions.BrickEdgeLog2,
                voxelCoord.z >> VoxelDimensions.BrickEdgeLog2);

            int3 regionCoord = new int3(
                brickCoord.x >> VoxelDimensions.RegionEdgeLog2,
                brickCoord.y >> VoxelDimensions.RegionEdgeLog2,
                brickCoord.z >> VoxelDimensions.RegionEdgeLog2);

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
