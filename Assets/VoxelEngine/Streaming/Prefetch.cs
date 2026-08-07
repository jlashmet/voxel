using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Streaming
{
    /// <summary>
    /// Movement-vector prefetch: load regions ahead of the player based on velocity,
    /// not camera direction. This ensures regions are resident by the time the player
    /// reaches them, regardless of camera orientation.
    ///
    /// Key design decisions (T113):
    ///   - Prefetch direction = normalised velocity vector, NOT camera forward.
    ///     A player moving straight while looking backwards should still prefetch ahead.
    ///   - Prefetch distance is measured in bricks and extends beyond the load radius so that
    ///     regions are loaded and published before they enter the visible window.
    ///   - Regions along the velocity cone are added to the resident set first; off-axis
    ///     regions use the standard load-radius rule.
    /// </summary>
    public static class Prefetch
    {
        /// <summary>
        /// Prefetch margin in bricks: how far beyond the load radius to begin loading.
        /// 8 bricks = 6.4 m at 0.8 m/brick — enough for one full brick to stream in
        /// before the player reaches it, given typical movement speeds up to 10 m/s.
        /// </summary>
        private const int PrefetchMarginBricks = 8;

        /// <summary>
        /// Velocity magnitude threshold below which prefetch is suppressed.
        /// If |velocity| < this value, the player is effectively stationary and there is
        /// no benefit to directional prefetching — just load concentrically.
        /// </summary>
        private const float MinVelocityMagnitude = 0.5f; // m/s

        /// <summary>
        /// Cone half-angle in radians for the velocity-direction prefetch cone.
        /// 60 degrees covers enough lateral area without preloading unnecessary regions.
        /// </summary>
        private const float PrefetchConeAngle = math.PI / 3f;

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Determine which regions to prefetch based on player velocity (not view direction).
        /// The result includes: (a) regions within the load radius, plus (b) regions along the
        /// velocity cone that are between the load radius and the prefetch distance.
        /// </summary>
        /// <param name="playerPosition">Player world position in metres.</param>
        /// <param name="velocity">Player velocity in m/s. Magnitude drives prefetch direction;
        ///     zero magnitude produces concentric (non-directional) loading.</param>
        /// <param name="loadRadiusBricks">Load radius for the device tier — from ResidencyManager.</param>
        /// <param name="allocator">Allocator for the returned NativeArray. Caller must dispose.</param>
        public static NativeArray<int3> GetPrefetchTargets(float3 playerPosition, float3 velocity,
            int loadRadiusBricks, Allocator allocator)
        {
            var centre = ResidencyManager.PositionToRegion(playerPosition);
            var regionLoadRadius = (int)math.ceil(loadRadiusBricks / VoxelDimensions.RegionEdge);

            // Determine prefetch direction and effective radius.
            float velocityMag = math.length(velocity);
            bool directional = velocityMag >= MinVelocityMagnitude;
            float3 prefetchDir = float3.zero;
            float effectivePrefetchRadius;

            if (directional)
            {
                prefetchDir = math.normalize(velocity);
                // Prefetch distance = load radius + margin, in metres.
                effectivePrefetchRadius = loadRadiusBricks * 0.8f + PrefetchMarginBricks * 0.8f;
            }
            else
            {
                effectivePrefetchRadius = loadRadiusBricks * 0.8f;
            }

            // Upper bound on region count: box around the larger of load or prefetch radius.
            int effectiveRegionRadius;
            if (directional)
            {
                var prefR = (int)math.ceil(effectivePrefetchRadius / (VoxelDimensions.RegionEdge * 0.8f));
                effectiveRegionRadius = math.max(regionLoadRadius, prefR);
            }
            else
            {
                effectiveRegionRadius = regionLoadRadius;
            }

            NativeArray<int3> result = new NativeArray<int3>(
                (2 * effectiveRegionRadius + 1) * (2 * effectiveRegionRadius + 1) * (2 * effectiveRegionRadius + 1), allocator);

            int idx = 0;
            float loadDistSqBricks = (float)(loadRadiusBricks * loadRadiusBricks);

            for (var x = -effectiveRegionRadius; x <= effectiveRegionRadius; x++)
            {
                for (var y = -effectiveRegionRadius; y <= effectiveRegionRadius; y++)
                {
                    for (var z = -effectiveRegionRadius; z <= effectiveRegionRadius; z++)
                    {
                        var rc = new int3(centre.x + x, centre.y + y, centre.z + z);

                        // Compute region center in world space.
                        float3 regionWorldPos = RegionWorldPos(rc);
                        float distToPlayer = math.distance(regionWorldPos, playerPosition);

                        // Distance from centre to this region's brick-distance value.
                        float distBricks = distToPlayer / 0.8f;

                        bool shouldInclude = false;

                        if (directional)
                        {
                            // Check velocity cone.
                            float3 toRegion = math.normalizesafe(regionWorldPos - playerPosition, float3.zero);
                            float cosAngle = math.dot(toRegion, prefetchDir);
                            float cosHalfAngle = math.cos(PrefetchConeAngle);

                            if (cosAngle >= cosHalfAngle)
                            {
                                // Inside cone — load if within prefetch distance.
                                shouldInclude = distBricks <= effectivePrefetchRadius / 0.8f;
                            }
                            else if (distBricks <= loadRadiusBricks)
                            {
                                // Outside cone but within load radius — always include.
                                shouldInclude = true;
                            }
                        }
                        else
                        {
                            // Non-directional: concentric load from player position.
                            shouldInclude = distToPlayer * 1.25f <= loadRadiusBricks * 0.8f;
                        }

                        if (shouldInclude)
                            result[idx++] = rc;
                    }
                }
            }

            // Resize to actual count and return.
            NativeArray<int3> trimmed = new NativeArray<int3>(idx, allocator);
            for (int i = 0; i < idx; i++)
                trimmed[i] = result[i];
            result.Dispose();

            return trimmed;
        }

        /// <summary>Compute the preferred prefetch direction — the normalised velocity vector.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetDirection(float3 velocity)
        {
            float mag = math.length(velocity);
            return mag >= MinVelocityMagnitude ? math.normalize(velocity) : float3.zero;
        }

        /// <summary>Get the world position of a region's corner in metres.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 RegionWorldPos(int3 rc) => new float3(
            rc.x * VoxelDimensions.RegionEdge * 0.8f,
            rc.y * VoxelDimensions.RegionEdge * 0.8f,
            rc.z * VoxelDimensions.RegionEdge * 0.8f
        );
    }
}
