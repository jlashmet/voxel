using System.Runtime.CompilerServices;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Collision
{
    /// <summary>
    /// Read-only view of a speculative overlay, supplied by the caller. Implementations remain
    /// structs so the overlay half of the hot path devirtualises without creating a Net dependency.
    /// </summary>
    public interface IOverlayQuery
    {
        bool TryGetPendingMaterial(int3 brickCoord, out byte material);
    }

    public struct CollisionResult
    {
        public bool BlockedX;
        public bool BlockedY;
        public bool BlockedZ;
        public float3 NormalX;
        public float3 NormalY;
        public float3 NormalZ;

        public int BlockedCount =>
            (BlockedX ? 1 : 0) + (BlockedY ? 1 : 0) + (BlockedZ ? 1 : 0);

        public bool IsFullyBlocked => BlockedX && BlockedY && BlockedZ;
    }

    /// <summary>Swept AABB collision against authoritative Storage read views.</summary>
    public static class SweptAabb
    {
        // Preserve the historical coordinate interpretation during the architecture cutover.
        // These are private consumer details, not public Storage layout vocabulary.
        private const int ReadBlockEdgeLog2 = 3;
        private const int RegionReadBlockEdgeLog2 = VoxelGrid.RegionVoxelEdgeLog2 - ReadBlockEdgeLog2;
        private const int RegionReadBlockEdgeMask = (1 << RegionReadBlockEdgeLog2) - 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CollisionResult Sweep(
            IRegionReadSource source,
            float3 min,
            float3 max,
            float3 delta)
        {
            var result = new CollisionResult();
            float3 currentMin = min;
            float3 currentMax = max;
            float3 residualDelta = delta;

            if (math.abs(delta.x) > 1e-6f)
            {
                CollisionResult xRes = SweepAxis(source, currentMin, currentMax, delta.x);
                result.BlockedX = xRes.BlockedX;
                result.NormalX = xRes.NormalX;
                if (!xRes.BlockedX) residualDelta.x = delta.x;
            }

            if (math.abs(residualDelta.y) > 1e-6f)
            {
                CollisionResult yRes = SweepAxis(source, currentMin, currentMax, residualDelta.y);
                result.BlockedY = yRes.BlockedY;
                result.NormalY = yRes.NormalY;
                if (!yRes.BlockedY) residualDelta.y = delta.y;
            }

            if (math.abs(residualDelta.z) > 1e-6f)
            {
                CollisionResult zRes = SweepAxis(source, currentMin, currentMax, residualDelta.z);
                result.BlockedZ = zRes.BlockedZ;
                result.NormalZ = zRes.NormalZ;
                if (!zRes.BlockedZ) residualDelta.z = delta.z;
            }

            return result;
        }

        public static CollisionResult SweepAgainstOverlay<TOverlay>(
            IRegionReadSource source,
            in TOverlay overlay,
            float3 min,
            float3 max,
            float3 delta)
            where TOverlay : struct, IOverlayQuery
        {
            var result = new CollisionResult();
            float3 currentMin = min;
            float3 currentMax = max;
            float3 residualDelta = delta;

            if (math.abs(delta.x) > 1e-6f)
            {
                CollisionResult xRes = SweepAxisAgainstOverlay(
                    source, overlay, currentMin, currentMax, delta.x);
                result.BlockedX = xRes.BlockedX;
                result.NormalX = xRes.NormalX;
                if (!xRes.BlockedX) residualDelta.x = delta.x;
            }

            if (math.abs(residualDelta.y) > 1e-6f)
            {
                CollisionResult yRes = SweepAxisAgainstOverlay(
                    source, overlay, currentMin, currentMax, residualDelta.y);
                result.BlockedY = yRes.BlockedY;
                result.NormalY = yRes.NormalY;
                if (!yRes.BlockedY) residualDelta.y = delta.y;
            }

            if (math.abs(residualDelta.z) > 1e-6f)
            {
                CollisionResult zRes = SweepAxisAgainstOverlay(
                    source, overlay, currentMin, currentMax, residualDelta.z);
                result.BlockedZ = zRes.BlockedZ;
                result.NormalZ = zRes.NormalZ;
                if (!zRes.BlockedZ) residualDelta.z = delta.z;
            }

            return result;
        }

        private static CollisionResult SweepAxisAgainstOverlay<TOverlay>(
            IRegionReadSource source,
            in TOverlay overlay,
            float3 min,
            float3 max,
            float delta)
            where TOverlay : struct, IOverlayQuery
        {
            var res = new CollisionResult();
            if (delta == 0f) return res;

            ComputeSweepBounds(min, max, delta,
                out int minX, out int maxX,
                out int minY, out int maxY,
                out int minZ, out int maxZ);

            float sign = math.sign(delta);
            int3 cachedRegionCoord = new int3(int.MinValue);
            RegionReadView region = default;

            for (int bx = minX; bx <= maxX; bx++)
            for (int by = minY; by <= maxY; by++)
            for (int bz = minZ; bz <= maxZ; bz++)
            {
                int3 brickCoord = new int3(bx, by, bz);

                // Overlay wins at a coordinate; only absent overlay entries fall back to Storage.
                if (IsOverlaySolidAtBrick(brickCoord, in overlay)
                    || IsSolidAtBrick(source, brickCoord, ref cachedRegionCoord, ref region))
                {
                    SetBlocked(ref res, sign);
                    return res;
                }
            }

            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsOverlaySolidAtBrick<TOverlay>(int3 brickCoord, in TOverlay overlay)
            where TOverlay : struct, IOverlayQuery
        {
            return overlay.TryGetPendingMaterial(brickCoord, out byte material)
                && material != VoxelGrid.MaterialEmpty;
        }

        private static CollisionResult SweepAxis(
            IRegionReadSource source,
            float3 min,
            float3 max,
            float delta)
        {
            var res = new CollisionResult();
            if (delta == 0f) return res;

            ComputeSweepBounds(min, max, delta,
                out int minX, out int maxX,
                out int minY, out int maxY,
                out int minZ, out int maxZ);

            float sign = math.sign(delta);
            int3 cachedRegionCoord = new int3(int.MinValue);
            RegionReadView region = default;

            for (int bx = minX; bx <= maxX; bx++)
            for (int by = minY; by <= maxY; by++)
            for (int bz = minZ; bz <= maxZ; bz++)
            {
                if (!IsSolidAtBrick(source, new int3(bx, by, bz),
                                    ref cachedRegionCoord, ref region))
                    continue;

                SetBlocked(ref res, sign);
                return res;
            }

            return res;
        }

        private static void ComputeSweepBounds(
            float3 min,
            float3 max,
            float delta,
            out int minX,
            out int maxX,
            out int minY,
            out int maxY,
            out int minZ,
            out int maxZ)
        {
            minX = math.min((int)math.floor(min.x), (int)math.floor(max.x + delta));
            maxX = math.max((int)math.floor(min.x - 1e-6f),
                            (int)math.floor(max.x - 1e-6f + delta));
            minY = math.min((int)math.floor(min.y), (int)math.floor(max.y + delta));
            maxY = math.max((int)math.floor(min.y - 1e-6f),
                            (int)math.floor(max.y - 1e-6f + delta));
            minZ = math.min((int)math.floor(min.z), (int)math.floor(max.z + delta));
            maxZ = math.max((int)math.floor(min.z - 1e-6f),
                            (int)math.floor(max.z - 1e-6f + delta));

            if (minX > maxX) minX = maxX;
            if (minY > maxY) minY = maxY;
            if (minZ > maxZ) minZ = maxZ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetBlocked(ref CollisionResult result, float sign)
        {
            result.BlockedX = true;
            result.BlockedY = true;
            result.BlockedZ = true;
            result.NormalX = -new float3(sign, 0, 0);
            result.NormalY = -new float3(0, sign, 0);
            result.NormalZ = -new float3(0, 0, sign);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSolidAtBrick(
            IRegionReadSource source,
            int3 brickCoord,
            ref int3 cachedRegionCoord,
            ref RegionReadView region)
        {
            int3 regionCoord = brickCoord >> RegionReadBlockEdgeLog2;
            if (!region.IsCreated || math.any(regionCoord != cachedRegionCoord))
            {
                if (!source.TryAcquireRegion(regionCoord, out region))
                {
                    region = default;
                    cachedRegionCoord = regionCoord;
                    return false;
                }
                cachedRegionCoord = regionCoord;
            }

            int3 localBlock = (brickCoord >> ReadBlockEdgeLog2) & RegionReadBlockEdgeMask;
            if (!region.TryGetBlock(localBlock, out VoxelReadBlock block)) return false;

            if (block.Kind == VoxelReadBlockKind.Uniform)
                return block.UniformMaterial != VoxelGrid.MaterialEmpty;

            return block.Kind == VoxelReadBlockKind.Mixed && region.IsBlockOccupied(localBlock);
        }
    }
}
