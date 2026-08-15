using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Streaming
{
    /// <summary>
    /// Movement-vector prefetch: load regions ahead of the player based on velocity rather than
    /// camera direction. Distances remain expressed in logical 0.8 m read blocks; Storage's
    /// physical allocator layout is irrelevant to this policy.
    /// </summary>
    public static class Prefetch
    {
        private const int PrefetchMarginBlocks = 8;
        private const float MinVelocityMagnitude = 0.5f;
        private const float PrefetchConeAngle = math.PI / 3f;

        public static NativeArray<int3> GetPrefetchTargets(float3 playerPosition, float3 velocity,
            int loadRadiusBlocks, Allocator allocator)
        {
            int3 centre = ResidencyManager.PositionToRegion(playerPosition);
            int regionLoadRadius = (int)math.ceil(
                loadRadiusBlocks / (float)VoxelReadGrid.BlocksPerRegionEdge);

            float velocityMagnitude = math.length(velocity);
            bool directional = velocityMagnitude >= MinVelocityMagnitude;
            float3 prefetchDirection = directional ? math.normalize(velocity) : float3.zero;
            float effectivePrefetchRadius = directional
                ? loadRadiusBlocks * 0.8f + PrefetchMarginBlocks * 0.8f
                : loadRadiusBlocks * 0.8f;

            int effectiveRegionRadius;
            if (directional)
            {
                float regionMetres = VoxelReadGrid.BlocksPerRegionEdge * 0.8f;
                int prefetchRadius = (int)math.ceil(effectivePrefetchRadius / regionMetres);
                effectiveRegionRadius = math.max(regionLoadRadius, prefetchRadius);
            }
            else
            {
                effectiveRegionRadius = regionLoadRadius;
            }

            NativeArray<int3> result = new NativeArray<int3>(
                (2 * effectiveRegionRadius + 1)
              * (2 * effectiveRegionRadius + 1)
              * (2 * effectiveRegionRadius + 1), allocator);

            int index = 0;
            for (int x = -effectiveRegionRadius; x <= effectiveRegionRadius; x++)
            for (int y = -effectiveRegionRadius; y <= effectiveRegionRadius; y++)
            for (int z = -effectiveRegionRadius; z <= effectiveRegionRadius; z++)
            {
                int3 regionCoord = new int3(centre.x + x, centre.y + y, centre.z + z);
                float3 regionWorldPosition = RegionWorldPos(regionCoord);
                float distanceToPlayer = math.distance(regionWorldPosition, playerPosition);
                float distanceBlocks = distanceToPlayer / 0.8f;

                bool include;
                if (directional)
                {
                    float3 toRegion = math.normalizesafe(
                        regionWorldPosition - playerPosition, float3.zero);
                    float cosAngle = math.dot(toRegion, prefetchDirection);
                    if (cosAngle >= math.cos(PrefetchConeAngle))
                        include = distanceBlocks <= effectivePrefetchRadius / 0.8f;
                    else
                        include = distanceBlocks <= loadRadiusBlocks;
                }
                else
                {
                    include = distanceToPlayer * 1.25f <= loadRadiusBlocks * 0.8f;
                }

                if (include) result[index++] = regionCoord;
            }

            NativeArray<int3> trimmed = new NativeArray<int3>(index, allocator);
            for (int i = 0; i < index; i++) trimmed[i] = result[i];
            result.Dispose();
            return trimmed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetDirection(float3 velocity)
        {
            float magnitude = math.length(velocity);
            return magnitude >= MinVelocityMagnitude ? math.normalize(velocity) : float3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 RegionWorldPos(int3 regionCoord)
        {
            float regionMetres = VoxelReadGrid.BlocksPerRegionEdge * 0.8f;
            return new float3(
                regionCoord.x * regionMetres,
                regionCoord.y * regionMetres,
                regionCoord.z * regionMetres);
        }
    }
}
